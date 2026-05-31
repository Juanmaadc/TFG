using UnityEngine;

public class PlayerArrowShooter2D : MonoBehaviour
{
    [Header("Activation")]
    [Tooltip("Pon este script solo en el prefab del personaje que puede lanzar flechas. Si quieres una protección extra, escribe aquí el characterId permitido.")]
    [SerializeField] private string requiredCharacterId = "";

    [SerializeField] private KeyCode shootKey = KeyCode.F;
    [SerializeField] private bool allowShootingWhileMoving = true;

    [Header("Arrow")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform arrowSpawnPoint;
    [SerializeField] private float arrowSpeed = 8f;
    [SerializeField] private int arrowDamage = 1;
    [SerializeField] private float arrowLifeTime = 4f;
    [SerializeField] private float shootCooldown = 0.45f;

    [Header("Direction")]
    [Tooltip("Si está activo, la flecha solo se lanza en horizontal: derecha o izquierda, según hacia dónde mire el personaje.")]
    [SerializeField] private bool forceHorizontalShot = true;

    [Tooltip("Si no hay PlayerMovement, se usa la escala local X para saber si mira a izquierda o derecha.")]
    [SerializeField] private bool useScaleAsFallback = true;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string shootTriggerName = "Shoot";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private PlayerMovement movement;
    private PlayerCharacterId characterId;
    private float nextAllowedShotTime;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        if (movement == null)
            movement = GetComponentInChildren<PlayerMovement>();

        if (characterId == null)
            characterId = GetComponent<PlayerCharacterId>();

        if (characterId == null)
            characterId = GetComponentInChildren<PlayerCharacterId>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (arrowSpawnPoint == null)
        {
            Transform found = transform.Find("ArrowSpawnPoint");
            if (found == null)
                found = transform.Find("BowPoint");

            if (found != null)
                arrowSpawnPoint = found;
        }
    }

    private void Update()
    {
        if (!IsAllowedCharacter())
            return;

        if (Input.GetKeyDown(shootKey))
            TryShoot();
    }

    public void TryShoot()
    {
        if (Time.time < nextAllowedShotTime)
            return;

        if (arrowPrefab == null)
        {
            Debug.LogWarning("PlayerArrowShooter2D: no hay Arrow Prefab asignado.", this);
            return;
        }

        if (!allowShootingWhileMoving && movement != null && movement.IsBeingKnockedBack)
            return;

        Vector2 direction = GetShootDirection();
        Vector3 spawnPosition = arrowSpawnPoint != null ? arrowSpawnPoint.position : transform.position;
        Quaternion spawnRotation = Quaternion.Euler(0f, 0f, direction.x >= 0f ? 0f : 180f);

        GameObject arrowObject = Instantiate(arrowPrefab, spawnPosition, spawnRotation);

        ArrowProjectile2D arrow = arrowObject.GetComponent<ArrowProjectile2D>();
        if (arrow == null)
            arrow = arrowObject.AddComponent<ArrowProjectile2D>();

        arrow.Launch(direction, arrowSpeed, arrowDamage, arrowLifeTime, gameObject);

        nextAllowedShotTime = Time.time + shootCooldown;

        if (animator != null && !string.IsNullOrWhiteSpace(shootTriggerName))
            animator.SetTrigger(shootTriggerName);

        if (debugLogs)
            Debug.Log($"PlayerArrowShooter2D: {name} lanza flecha hacia {direction}.", this);
    }

    private bool IsAllowedCharacter()
    {
        if (string.IsNullOrWhiteSpace(requiredCharacterId))
            return true;

        if (characterId == null)
            ResolveReferences();

        return characterId != null && characterId.characterId == requiredCharacterId;
    }

    private Vector2 GetShootDirection()
    {
        int facing = 1;

        if (movement != null)
        {
            facing = movement.facingDirection >= 0 ? 1 : -1;
        }
        else if (useScaleAsFallback)
        {
            facing = transform.localScale.x >= 0f ? 1 : -1;
        }

        if (forceHorizontalShot)
            return new Vector2(facing, 0f);

        // Por si más adelante quieres permitir disparos no horizontales.
        return new Vector2(facing, 0f);
    }
}
