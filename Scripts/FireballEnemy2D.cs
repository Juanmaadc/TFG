using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FireballEnemy2D : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool autoFindPlayer = true;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 6f;
    [SerializeField] private bool requireLineOfSight = false;
    [SerializeField] private LayerMask lineOfSightObstacleLayers;

    [Header("Shooting")]
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireCooldown = 1.25f;
    [SerializeField] private float firstShotDelay = 0.35f;
    [SerializeField] private float fireballSpeed = 6f;
    [SerializeField] private int fireballDamage = 1;
    [SerializeField] private float fireballLifetime = 4f;
    [SerializeField] private float playerKnockbackDistance = 0.9f;
    [SerializeField] private float playerKnockbackDuration = 0.14f;

    [Header("Movement")]
    [Tooltip("Este enemigo estático puede desactivar EnemyChaser2D para no perseguir al jugador.")]
    [SerializeField] private bool disableEnemyChaserOnStart = true;

    [Header("Facing")]
    [Tooltip("Hace que el mago mire hacia el jugador cuando lo detecta.")]
    [SerializeField] private bool faceTargetWhenDetected = true;
    [Tooltip("Objeto visual que se gira. Si está vacío se usa este mismo GameObject.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("Actívalo si el sprite del mago mira hacia la derecha por defecto.")]
    [SerializeField] private bool spriteFacesRightByDefault = true;
    [Tooltip("Mantiene el tamaño original del sprite al girarlo.")]
    [SerializeField] private bool preserveOriginalScale = true;

    [Header("Knockback when hit")]
    [SerializeField] private bool allowKnockbackWhenHit = true;
    [SerializeField] private float knockbackMultiplier = 1f;
    [SerializeField] private bool pauseShootingDuringKnockback = true;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string idleBoolParameter = "IsIdle";
    [SerializeField] private string awakeBoolParameter = "IsAwake";
    [SerializeField] private string walkingBoolParameter = "IsWalking";
    [SerializeField] private string speedFloatParameter = "Speed";
    [SerializeField] private string attackTriggerParameter = "Attack";
    [SerializeField] private bool warnMissingAnimatorParameters = false;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Rigidbody2D rb;
    private EnemyChaser2D enemyChaser;
    private Vector2 knockbackVelocity;
    private float knockbackTimer;
    private float nextFireTime;
    private bool hasSeenPlayer;
    private HashSet<string> animatorParameterNames;
    private bool lastAwakeState;
    private bool animatorStateInitialized;
    private Vector3 originalVisualScale;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        enemyChaser = GetComponent<EnemyChaser2D>();
        if (enemyChaser == null)
            enemyChaser = GetComponentInParent<EnemyChaser2D>();

        if (disableEnemyChaserOnStart && enemyChaser != null)
            enemyChaser.enabled = false;

        ResolveVisualRoot();
        ResolveAnimator();
        UpdateAnimationState(false, true);
    }

    private void OnEnable()
    {
        nextFireTime = Time.time + Mathf.Max(0f, firstShotDelay);
        ResolveAnimator();
        UpdateAnimationState(false, true);
    }

    private void Start()
    {
        if (target == null && autoFindPlayer)
            FindPlayerTarget();
    }

    private void Update()
    {
        if (target == null && autoFindPlayer)
            FindPlayerTarget();

        bool canSeePlayer = CanDetectTarget();
        hasSeenPlayer = canSeePlayer;

        if (canSeePlayer)
            FaceTarget();

        UpdateAnimationState(false);

        if (!canSeePlayer)
            return;

        if (pauseShootingDuringKnockback && knockbackTimer > 0f)
            return;

        if (Time.time >= nextFireTime)
            ShootAtTarget();
    }

    private void FixedUpdate()
    {
        if (rb == null || knockbackTimer <= 0f)
            return;

        float step = Time.fixedDeltaTime;
        rb.MovePosition(rb.position + knockbackVelocity * step);
        knockbackTimer -= step;

        if (knockbackTimer <= 0f)
        {
            knockbackTimer = 0f;
            knockbackVelocity = Vector2.zero;
        }
    }

    public void Retarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void ApplyKnockback(Vector2 direction, float distance, float duration)
    {
        if (!allowKnockbackWhenHit || rb == null)
            return;

        if (direction.sqrMagnitude <= 0.001f || distance <= 0f || duration <= 0f)
            return;

        knockbackVelocity = direction.normalized * ((distance * Mathf.Max(0f, knockbackMultiplier)) / duration);
        knockbackTimer = duration;
    }

    private void FindPlayerTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
            target = player.transform;
    }

    private bool CanDetectTarget()
    {
        if (target == null)
            return false;

        Vector2 toTarget = target.position - transform.position;
        if (toTarget.sqrMagnitude > detectionRange * detectionRange)
            return false;

        if (!requireLineOfSight || lineOfSightObstacleLayers.value == 0)
            return true;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, toTarget.normalized, toTarget.magnitude, lineOfSightObstacleLayers);
        return hit.collider == null;
    }

    private void ResolveVisualRoot()
    {
        if (visualRoot == null)
        {
            SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            visualRoot = spriteRenderer != null ? spriteRenderer.transform : transform;
        }

        originalVisualScale = visualRoot != null ? visualRoot.localScale : transform.localScale;
    }

    private void FaceTarget()
    {
        if (!faceTargetWhenDetected || target == null)
            return;

        if (visualRoot == null)
            ResolveVisualRoot();

        if (visualRoot == null)
            return;

        float differenceX = target.position.x - visualRoot.position.x;
        if (Mathf.Abs(differenceX) <= 0.01f)
            return;

        bool targetIsOnRight = differenceX > 0f;
        float desiredSign = targetIsOnRight == spriteFacesRightByDefault ? 1f : -1f;

        Vector3 scale = preserveOriginalScale ? originalVisualScale : visualRoot.localScale;
        float baseX = Mathf.Abs(scale.x);
        if (baseX <= 0.0001f)
            baseX = 1f;

        scale.x = baseX * desiredSign;
        visualRoot.localScale = scale;
    }

    private void ShootAtTarget()
    {
        if (fireballPrefab == null || target == null)
            return;

        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
        Quaternion spawnRotation = firePoint != null ? firePoint.rotation : Quaternion.identity;
        GameObject projectileObject = Instantiate(fireballPrefab, spawnPosition, spawnRotation);

        Vector2 direction = ((Vector2)target.position - (Vector2)spawnPosition).normalized;
        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.right;

        FireballProjectile2D projectile = projectileObject.GetComponent<FireballProjectile2D>();
        if (projectile == null)
            projectile = projectileObject.AddComponent<FireballProjectile2D>();

        projectile.Launch(direction, fireballSpeed, fireballDamage, fireballLifetime, gameObject, playerKnockbackDistance, playerKnockbackDuration);

        TriggerAttackAnimation();
        nextFireTime = Time.time + Mathf.Max(0.05f, fireCooldown);

        if (debugLogs)
            Debug.Log($"FireballEnemy2D: {name} dispara una bola de fuego hacia {target.name}.", this);
    }

    private void ResolveAnimator()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        CacheAnimatorParameters();
    }

    private void CacheAnimatorParameters()
    {
        animatorParameterNames = new HashSet<string>();

        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (!string.IsNullOrWhiteSpace(parameter.name))
                animatorParameterNames.Add(parameter.name);
        }
    }

    private bool HasAnimatorParameter(string parameterName)
    {
        return !string.IsNullOrWhiteSpace(parameterName) &&
               animatorParameterNames != null &&
               animatorParameterNames.Contains(parameterName);
    }

    private void UpdateAnimationState(bool isWalking, bool force = false)
    {
        if (animator == null)
            ResolveAnimator();

        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        if (!force && animatorStateInitialized && lastAwakeState == hasSeenPlayer)
            return;

        SetAnimatorBool(idleBoolParameter, !hasSeenPlayer);
        SetAnimatorBool(awakeBoolParameter, hasSeenPlayer);
        SetAnimatorBool(walkingBoolParameter, false);
        SetAnimatorFloat(speedFloatParameter, 0f);

        lastAwakeState = hasSeenPlayer;
        animatorStateInitialized = true;
    }

    private void TriggerAttackAnimation()
    {
        if (animator == null)
            ResolveAnimator();

        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        SetAnimatorTrigger(attackTriggerParameter);
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            return;

        if (HasAnimatorParameter(parameterName))
            animator.SetBool(parameterName, value);
        else if (warnMissingAnimatorParameters)
            Debug.LogWarning($"FireballEnemy2D: el Animator de {name} no tiene el bool '{parameterName}'.", this);
    }

    private void SetAnimatorFloat(string parameterName, float value)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            return;

        if (HasAnimatorParameter(parameterName))
            animator.SetFloat(parameterName, value);
        else if (warnMissingAnimatorParameters)
            Debug.LogWarning($"FireballEnemy2D: el Animator de {name} no tiene el float '{parameterName}'.", this);
    }

    private void SetAnimatorTrigger(string parameterName)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            return;

        if (HasAnimatorParameter(parameterName))
            animator.SetTrigger(parameterName);
        else if (warnMissingAnimatorParameters)
            Debug.LogWarning($"FireballEnemy2D: el Animator de {name} no tiene el trigger '{parameterName}'.", this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
