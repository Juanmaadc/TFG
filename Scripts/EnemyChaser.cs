using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyChaser2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float stopDistance = 0.1f;
    [SerializeField] private bool sleepOnStart = true;

    [Header("Wall collision / anti wall pass")]
    [Tooltip("Si está activo, el enemigo no atraviesa paredes: antes de moverse hace un Cast con su propio collider y se detiene si encuentra un muro.")]
    [SerializeField] private bool blockMovementWithColliders = true;

    [Tooltip("Separa el movimiento horizontal y vertical, igual que el jugador. Esto ayuda a que el enemigo se deslice por paredes y esquinas en lugar de atravesarlas o quedarse vibrando.")]
    [SerializeField] private bool useAxisSeparatedWallSlideMovement = true;

    [Tooltip("Pequeña separación que deja antes de tocar una pared. Evita vibraciones y que se meta parcialmente dentro de los muros.")]
    [SerializeField, Min(0f)] private float wallSlideSkinWidth = 0.02f;

    [Tooltip("Si se asigna, solo estas capas bloquean el movimiento. Si se deja en Nothing, se usan todos los colliders sólidos excepto Player, enemigos y el propio enemigo.")]
    [SerializeField] private LayerMask movementObstacleLayers;

    [Tooltip("Ignora el collider del jugador al calcular paredes. El jugador debe seguir pudiendo recibir daño por contacto.")]
    [SerializeField] private bool ignorePlayerAsMovementObstacle = true;

    [Tooltip("Ignora otros enemigos al calcular paredes, para que no se bloqueen entre ellos como si fueran muros.")]
    [SerializeField] private bool ignoreOtherEnemiesAsMovementObstacle = true;

    [Tooltip("Aplica automáticamente un PhysicsMaterial2D sin fricción a los colliders del enemigo.")]
    [SerializeField] private bool autoApplyFrictionlessMaterial = true;

    [Tooltip("Material físico opcional. Si se deja vacío, se crea uno temporal con fricción 0 y rebote 0.")]
    [SerializeField] private PhysicsMaterial2D frictionlessMaterial;

    [Tooltip("Usa detección continua para reducir atravesar colliders al moverse rápido.")]
    [SerializeField] private bool useContinuousCollisionDetection = true;

    [Header("Wake / pursuit behavior")]
    [Tooltip("Si está activo, cuando el enemigo se despierte una vez, no volverá a dormirse al salir el jugador de la sala.")]
    [SerializeField] private bool stayAwakeAfterFirstActivation = true;

    [Tooltip("Si está activo, cuando se intenta dormir a un enemigo ya activado, buscará de nuevo al Player si ha perdido la referencia.")]
    [SerializeField] private bool reacquirePlayerWhenSleepIsIgnored = true;

    [Header("Contact damage")]
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private float contactDamageCooldown = 1f;

    [Header("Player knockback on hit")]
    [SerializeField] private float playerKnockbackDistance = 1.25f;
    [SerializeField] private float playerKnockbackDuration = 0.18f;

    [Header("Animation")]
    [Tooltip("Animator del enemigo. Si se deja vacío, se busca automáticamente en este objeto o en sus hijos.")]
    [SerializeField] private Animator animator;

    [Tooltip("Parámetro bool opcional. True cuando el enemigo está en idle.")]
    [SerializeField] private string idleBoolParameter = "IsIdle";

    [Tooltip("Parámetro bool opcional. True cuando el enemigo está despierto/detectando al jugador.")]
    [SerializeField] private string awakeBoolParameter = "IsAwake";

    [Tooltip("Parámetro bool opcional. True cuando el enemigo se está moviendo hacia el jugador.")]
    [SerializeField] private string walkingBoolParameter = "IsWalking";

    [Tooltip("Parámetro float opcional. 0 en idle, 1 cuando camina.")]
    [SerializeField] private string speedFloatParameter = "Speed";

    [Tooltip("Parámetro trigger opcional. Se lanza cuando el enemigo consigue atacar/dañar al jugador.")]
    [SerializeField] private string attackTriggerParameter = "Attack";

    [Tooltip("Si está activo, avisa en consola si falta algún parámetro configurado en el Animator.")]
    [SerializeField] private bool warnMissingAnimatorParameters = false;

    private Rigidbody2D rb;
    private Transform target;
    private Vector3 spawnPoint;
    private bool isAwake;
    private bool hasBeenActivated;

    private Vector2 knockbackVelocity;
    private float knockbackTimer;
    private float lastDamageTime = -999f;

    private HashSet<string> animatorParameterNames;
    private bool lastWalkingState;
    private bool lastAwakeState;
    private bool animatorStateInitialized;

    private readonly RaycastHit2D[] movementCastResults = new RaycastHit2D[12];

    public bool IsAwake => isAwake;
    public bool HasBeenActivated => hasBeenActivated;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ConfigureRigidbody();
        ApplyFrictionlessMaterialIfNeeded();

        spawnPoint = transform.position;
        isAwake = !sleepOnStart;
        hasBeenActivated = isAwake;

        ResolveAnimator();
        UpdateAnimationState(false, true);
    }

    void OnEnable()
    {
        ResolveAnimator();
        UpdateAnimationState(false, true);
    }

    void Start()
    {
        if (isAwake && target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }

        UpdateAnimationState(false, true);
    }

    private void ConfigureRigidbody()
    {
        if (rb == null)
            return;

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        // Mantengo Kinematic para respetar tu lógica actual de movimiento con MovePosition,
        // pero ahora el propio script bloquea las paredes con Rigidbody2D.Cast antes de moverse.
        rb.bodyType = RigidbodyType2D.Kinematic;

        if (useContinuousCollisionDetection)
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void ApplyFrictionlessMaterialIfNeeded()
    {
        if (!autoApplyFrictionlessMaterial)
            return;

        if (frictionlessMaterial == null)
        {
            frictionlessMaterial = new PhysicsMaterial2D("Runtime_Enemy_Frictionless")
            {
                friction = 0f,
                bounciness = 0f
            };
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D col in colliders)
        {
            if (col == null || col.isTrigger)
                continue;

            col.sharedMaterial = frictionlessMaterial;
        }
    }

    public void WakeUp(Transform player)
    {
        target = player;
        isAwake = true;
        hasBeenActivated = true;
        UpdateAnimationState(false, true);
    }

    public void Retarget(Transform newTarget)
    {
        target = newTarget;

        if (stayAwakeAfterFirstActivation && hasBeenActivated && target != null)
        {
            isAwake = true;
            UpdateAnimationState(false, true);
        }
    }

    public void Sleep()
    {
        if (stayAwakeAfterFirstActivation && hasBeenActivated)
        {
            if (target == null && reacquirePlayerWhenSleepIsIgnored)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    target = player.transform;
            }

            isAwake = true;
            UpdateAnimationState(false, true);
            return;
        }

        isAwake = false;
        target = null;
        knockbackTimer = 0f;
        knockbackVelocity = Vector2.zero;
        UpdateAnimationState(false, true);
    }

    public void ReturnToSpawn()
    {
        rb.position = spawnPoint;
        UpdateAnimationState(false, true);
    }

    public void ApplyKnockback(Vector2 direction, float distance, float duration)
    {
        if (direction.sqrMagnitude <= 0.001f || distance <= 0f || duration <= 0f)
            return;

        knockbackVelocity = direction.normalized * (distance / duration);
        knockbackTimer = duration;

        // El knockback no se considera "walking"; evitamos que cambie a animación de caminar mientras es empujado.
        UpdateAnimationState(false);
    }

    void FixedUpdate()
    {
        Vector2 current = rb.position;

        if (knockbackTimer > 0f)
        {
            float step = Time.fixedDeltaTime;
            Vector2 desiredDelta = knockbackVelocity * step;
            MoveEnemy(desiredDelta);

            knockbackTimer -= step;

            if (knockbackTimer <= 0f)
            {
                knockbackTimer = 0f;
                knockbackVelocity = Vector2.zero;
            }

            UpdateAnimationState(false);
            return;
        }

        if (!isAwake || target == null)
        {
            UpdateAnimationState(false);
            return;
        }

        Vector2 targetPos = target.position;

        if (Vector2.Distance(current, targetPos) <= stopDistance)
        {
            UpdateAnimationState(false);
            return;
        }

        Vector2 direction = (targetPos - current).normalized;
        Vector2 desiredMove = direction * moveSpeed * Time.fixedDeltaTime;

        bool moved = MoveEnemy(desiredMove);
        UpdateAnimationState(moved);
    }

    private bool MoveEnemy(Vector2 desiredDelta)
    {
        if (rb == null || desiredDelta.sqrMagnitude <= 0.0000001f)
            return false;

        if (!blockMovementWithColliders)
        {
            rb.MovePosition(rb.position + desiredDelta);
            return true;
        }

        Vector2 before = rb.position;

        if (useAxisSeparatedWallSlideMovement)
        {
            MoveSingleAxis(new Vector2(desiredDelta.x, 0f));
            MoveSingleAxis(new Vector2(0f, desiredDelta.y));
        }
        else
        {
            Vector2 direction = desiredDelta.normalized;
            float allowedDistance = GetAllowedMoveDistance(direction, desiredDelta.magnitude);

            if (allowedDistance > 0f)
                rb.MovePosition(rb.position + direction * allowedDistance);
        }

        return ((Vector2)rb.position - before).sqrMagnitude > 0.0000001f;
    }

    private void MoveSingleAxis(Vector2 axisDelta)
    {
        if (axisDelta.sqrMagnitude <= 0.0000001f)
            return;

        Vector2 direction = axisDelta.normalized;
        float requestedDistance = axisDelta.magnitude;
        float allowedDistance = GetAllowedMoveDistance(direction, requestedDistance);

        if (allowedDistance <= 0f)
            return;

        rb.MovePosition(rb.position + direction * allowedDistance);
    }

    private float GetAllowedMoveDistance(Vector2 direction, float requestedDistance)
    {
        if (requestedDistance <= 0f || rb == null)
            return 0f;

        ContactFilter2D filter = new ContactFilter2D
        {
            useTriggers = false
        };

        if (movementObstacleLayers.value != 0)
        {
            filter.useLayerMask = true;
            filter.layerMask = movementObstacleLayers;
        }
        else
        {
            filter.useLayerMask = false;
        }

        int hitCount = rb.Cast(direction, filter, movementCastResults, requestedDistance + wallSlideSkinWidth);

        float allowedDistance = requestedDistance;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = movementCastResults[i];

            if (!IsValidMovementObstacle(hit.collider))
                continue;

            allowedDistance = Mathf.Min(
                allowedDistance,
                Mathf.Max(0f, hit.distance - wallSlideSkinWidth)
            );
        }

        return allowedDistance;
    }

    private bool IsValidMovementObstacle(Collider2D col)
    {
        if (col == null)
            return false;

        if (col.isTrigger)
            return false;

        Transform colTransform = col.transform;

        if (colTransform == transform || colTransform.IsChildOf(transform) || transform.IsChildOf(colTransform))
            return false;

        if (ignorePlayerAsMovementObstacle)
        {
            if (col.CompareTag("Player") ||
                col.GetComponent<PlayerHealth2D>() != null ||
                col.GetComponentInParent<PlayerHealth2D>() != null)
                return false;
        }

        if (ignoreOtherEnemiesAsMovementObstacle)
        {
            if (col.GetComponent<EnemyChaser2D>() != null ||
                col.GetComponentInParent<EnemyChaser2D>() != null ||
                col.GetComponent<EnemyHealth2D>() != null ||
                col.GetComponentInParent<EnemyHealth2D>() != null ||
                col.GetComponent<FireballEnemy2D>() != null ||
                col.GetComponentInParent<FireballEnemy2D>() != null)
                return false;
        }

        return true;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamagePlayer(collision.collider);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryDamagePlayer(collision.collider);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (other == null || !other.CompareTag("Player"))
            return;

        if (Time.time < lastDamageTime + contactDamageCooldown)
            return;

        PlayerHealth2D playerHealth = other.GetComponent<PlayerHealth2D>();
        if (playerHealth == null)
            playerHealth = other.GetComponentInParent<PlayerHealth2D>();

        if (playerHealth == null)
            return;

        Vector2 knockbackDirection = ((Vector2)playerHealth.transform.position - (Vector2)transform.position).normalized;

        if (knockbackDirection.sqrMagnitude <= 0.001f)
            knockbackDirection = Vector2.right;

        TriggerAttackAnimation();

        playerHealth.TakeDamage(
            contactDamage,
            knockbackDirection,
            playerKnockbackDistance,
            playerKnockbackDuration
        );

        lastDamageTime = Time.time;
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

        if (!force && animatorStateInitialized && lastWalkingState == isWalking && lastAwakeState == isAwake)
            return;

        bool isIdle = !isWalking;

        SetAnimatorBool(idleBoolParameter, isIdle);
        SetAnimatorBool(awakeBoolParameter, isAwake);
        SetAnimatorBool(walkingBoolParameter, isWalking);
        SetAnimatorFloat(speedFloatParameter, isWalking ? 1f : 0f);

        lastWalkingState = isWalking;
        lastAwakeState = isAwake;
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
            Debug.LogWarning($"EnemyChaser2D: el Animator de {name} no tiene el bool '{parameterName}'.", this);
    }

    private void SetAnimatorFloat(string parameterName, float value)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            return;

        if (HasAnimatorParameter(parameterName))
            animator.SetFloat(parameterName, value);
        else if (warnMissingAnimatorParameters)
            Debug.LogWarning($"EnemyChaser2D: el Animator de {name} no tiene el float '{parameterName}'.", this);
    }

    private void SetAnimatorTrigger(string parameterName)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            return;

        if (HasAnimatorParameter(parameterName))
            animator.SetTrigger(parameterName);
        else if (warnMissingAnimatorParameters)
            Debug.LogWarning($"EnemyChaser2D: el Animator de {name} no tiene el trigger '{parameterName}'.", this);
    }
}
