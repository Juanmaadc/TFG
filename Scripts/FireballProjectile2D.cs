using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D))]
public class FireballProjectile2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 6f;
    [SerializeField] private float lifeTime = 4f;

    [Header("Damage")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float playerKnockbackDistance = 0.9f;
    [SerializeField] private float playerKnockbackDuration = 0.14f;

    [Header("Collision")]
    [Tooltip("Si está activo, la bola de fuego se destruye al tocar al jugador.")]
    [SerializeField] private bool destroyOnPlayerHit = true;

    [Tooltip("Layers que destruyen la bola de fuego. Recomendado: asignar aquí la layer Walls si existe.")]
    [SerializeField] private LayerMask obstacleLayers;

    [Tooltip("Si está activo, se destruye al tocar colliders que parezcan paredes aunque obstacleLayers esté vacío. Detecta WallTilemap, Walls, Pared, Muro, etc.")]
    [SerializeField] private bool destroyOnWallTilemapColliders = true;

    [Tooltip("Palabras usadas para reconocer objetos/layers de paredes. Sepáralas por comas.")]
    [SerializeField] private string wallNameKeywords = "wall,walls,walltilemap,wall tilemap,pared,paredes,muro,muros,obstacle,obstaculo,obstáculo";

    [Tooltip("Tags que destruyen la bola de fuego. Sepáralos por comas. No hace falta crearlos si usas nombres/layers de pared.")]
    [SerializeField] private string wallTags = "Wall,Walls,Obstacle";

    [Tooltip("Usa detección continua para reducir que una bola rápida atraviese colliders.")]
    [SerializeField] private bool useContinuousCollisionDetection = true;

    [Tooltip("Si está activo, se destruye al chocar con cualquier objeto que no sea el dueño. Normalmente déjalo desactivado para no destruirla con triggers de salas.")]
    [SerializeField] private bool destroyOnAnyNonOwnerCollision = false;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Rigidbody2D rb;
    private Vector2 direction = Vector2.right;
    private GameObject owner;
    private float destroyAtTime;
    private bool launched;
    private bool alreadyHit;

    private void Awake()
    {
        EnsureComponents();
    }

    private void OnEnable()
    {
        alreadyHit = false;
        destroyAtTime = Time.time + lifeTime;
    }

    private void Update()
    {
        if (lifeTime > 0f && Time.time >= destroyAtTime)
            Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        if (rb != null && launched && !alreadyHit)
            rb.linearVelocity = direction * speed;
    }

    public void Launch(Vector2 launchDirection, float launchSpeed, int launchDamage, float projectileLifetime, GameObject projectileOwner)
    {
        Launch(launchDirection, launchSpeed, launchDamage, projectileLifetime, projectileOwner, playerKnockbackDistance, playerKnockbackDuration);
    }

    public void Launch(Vector2 launchDirection, float launchSpeed, int launchDamage, float projectileLifetime, GameObject projectileOwner, float knockbackDistance, float knockbackDuration)
    {
        EnsureComponents();

        if (launchDirection.sqrMagnitude <= 0.001f)
            launchDirection = Vector2.right;

        direction = launchDirection.normalized;
        speed = launchSpeed;
        damage = launchDamage;
        lifeTime = projectileLifetime;
        owner = projectileOwner;
        playerKnockbackDistance = knockbackDistance;
        playerKnockbackDuration = knockbackDuration;
        destroyAtTime = Time.time + lifeTime;
        launched = true;
        alreadyHit = false;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = useContinuousCollisionDetection ? CollisionDetectionMode2D.Continuous : CollisionDetectionMode2D.Discrete;
            rb.linearVelocity = direction * speed;
        }
    }

    private void EnsureComponents()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = useContinuousCollisionDetection ? CollisionDetectionMode2D.Continuous : CollisionDetectionMode2D.Discrete;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.18f;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null)
            HandleHit(collision.collider);
    }

    private void HandleHit(Collider2D other)
    {
        if (alreadyHit || other == null)
            return;

        if (IsOwnerOrOwnerChild(other.gameObject))
            return;

        PlayerHealth2D playerHealth = other.GetComponent<PlayerHealth2D>();
        if (playerHealth == null)
            playerHealth = other.GetComponentInParent<PlayerHealth2D>();

        if (playerHealth != null)
        {
            alreadyHit = true;

            Vector2 knockbackDirection = ((Vector2)playerHealth.transform.position - (Vector2)transform.position).normalized;
            if (knockbackDirection.sqrMagnitude <= 0.001f)
                knockbackDirection = direction;

            playerHealth.TakeDamage(damage, knockbackDirection, playerKnockbackDistance, playerKnockbackDuration);

            if (debugLogs)
                Debug.Log($"FireballProjectile2D: {name} golpea al jugador con {damage} de daño.", this);

            if (destroyOnPlayerHit)
                Destroy(gameObject);

            return;
        }

        if (ShouldDestroyAgainst(other))
        {
            alreadyHit = true;

            if (debugLogs)
                Debug.Log($"FireballProjectile2D: {name} se destruye al tocar pared/obstáculo: {other.name}.", this);

            Destroy(gameObject);
        }
        else if (debugLogs)
        {
            Debug.Log($"FireballProjectile2D: {name} ha tocado {other.name}, pero no es jugador ni pared reconocida.", this);
        }
    }

    private bool IsOwnerOrOwnerChild(GameObject other)
    {
        if (owner == null || other == null)
            return false;

        if (other == owner)
            return true;

        return other.transform.IsChildOf(owner.transform);
    }

    private bool ShouldDestroyAgainst(Collider2D other)
    {
        if (other == null)
            return false;

        if (destroyOnAnyNonOwnerCollision)
            return true;

        int otherLayerMask = 1 << other.gameObject.layer;
        if (obstacleLayers.value != 0 && (obstacleLayers.value & otherLayerMask) != 0)
            return true;

        return destroyOnWallTilemapColliders && LooksLikeWallOrObstacle(other);
    }

    private bool LooksLikeWallOrObstacle(Collider2D other)
    {
        if (other == null)
            return false;

        GameObject obj = other.gameObject;

        if (obj.GetComponent<ProjectileObstacle2D>() != null || obj.GetComponentInParent<ProjectileObstacle2D>() != null)
            return true;

        if (MatchesAnyToken(obj.tag, wallTags))
            return true;

        string layerName = LayerMask.LayerToName(obj.layer);
        if (MatchesAnyToken(layerName, wallNameKeywords))
            return true;

        if (TransformOrParentNameMatches(obj.transform, wallNameKeywords))
            return true;

        bool isTilemapCollider = other.GetComponent<TilemapCollider2D>() != null
                                || other.GetComponent<CompositeCollider2D>() != null
                                || other.GetComponent<Tilemap>() != null
                                || other.GetComponentInParent<Tilemap>() != null;

        // Solo destruimos contra tilemaps si su nombre/layer/tag parece pared, o si tienen ProjectileObstacle2D.
        // Así evitamos que triggers grandes de salas destruyan la bola sin ser pared.
        return false && isTilemapCollider;
    }

    private bool TransformOrParentNameMatches(Transform start, string commaSeparatedKeywords)
    {
        Transform current = start;
        while (current != null)
        {
            if (MatchesAnyToken(current.name, commaSeparatedKeywords))
                return true;

            current = current.parent;
        }

        return false;
    }

    private bool MatchesAnyToken(string value, string commaSeparatedTokens)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(commaSeparatedTokens))
            return false;

        string lowerValue = value.ToLowerInvariant();
        string[] tokens = commaSeparatedTokens.Split(',');

        foreach (string rawToken in tokens)
        {
            string token = rawToken.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(token))
                continue;

            if (lowerValue.Contains(token))
                return true;
        }

        return false;
    }
}
