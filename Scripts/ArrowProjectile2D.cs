using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D))]
public class ArrowProjectile2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float lifeTime = 4f;

    [Header("Damage")]
    [SerializeField] private int damage = 1;

    [Header("Stick to enemy")]
    [Tooltip("Si está activo, la flecha queda pegada al enemigo al impactar.")]
    [SerializeField] private bool stickToEnemyOnHit = true;

    [Tooltip("Si está activo, la flecha se pega al objeto concreto del collider golpeado. Si está desactivado, se pega al objeto que tiene EnemyHealth2D.")]
    [SerializeField] private bool parentToHitCollider = true;

    [Tooltip("Desactiva el collider de la flecha cuando se queda clavada para evitar golpes repetidos.")]
    [SerializeField] private bool disableColliderWhenStuck = true;

    [Tooltip("Destruye la flecha después de quedar clavada. Usa 0 o menos para no destruirla hasta que muera el enemigo.")]
    [SerializeField] private float destroyAfterStuckSeconds = 0f;

    [Header("Precise enemy detection")]
    [Tooltip("ACTIVADO: evita que la flecha haga daño por tocar colliders grandes de salas, triggers o tilemaps. Solo daña si el collider golpeado pertenece realmente a un enemigo.")]
    [SerializeField] private bool requireDirectEnemyColliderHit = true;

    [Tooltip("Opcional. Si lo configuras con la layer Enemy, ayuda a reconocer raíces de enemigos cuyo EnemyHealth2D esté en un hijo.")]
    [SerializeField] private LayerMask enemyLayers;

    [Tooltip("Solo se buscará EnemyHealth2D en hijos si el objeto golpeado parece la raíz real de un enemigo. Evita detectar enemigos lejanos dentro de objetos grandes del mapa.")]
    [SerializeField] private bool searchChildrenOnlyOnEnemyRoots = true;

    [Header("Collision")]
    [Tooltip("Layers que destruyen la flecha, por ejemplo Walls.")]
    [SerializeField] private LayerMask obstacleLayers;

    [Tooltip("Si está activo, se destruye al tocar colliders que parezcan paredes aunque obstacleLayers esté vacío. Detecta WallTilemap, Walls, Pared, Muro, etc.")]
    [SerializeField] private bool destroyOnWallTilemapColliders = true;

    [Tooltip("Palabras usadas para reconocer objetos/layers de paredes. Sepáralas por comas.")]
    [SerializeField] private string wallNameKeywords = "wall,walls,walltilemap,wall tilemap,pared,paredes,muro,muros,obstacle,obstaculo,obstáculo";

    [Tooltip("Tags que destruyen la flecha. Sepáralos por comas.")]
    [SerializeField] private string wallTags = "Wall,Walls,Obstacle";

    [Tooltip("Usa detección continua para reducir que una flecha rápida atraviese colliders.")]
    [SerializeField] private bool useContinuousCollisionDetection = true;

    [Tooltip("Si está activo, la flecha se destruye al tocar cualquier cosa que no sea el dueño ni un enemigo.")]
    [SerializeField] private bool destroyOnAnyNonOwnerCollision = false;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Rigidbody2D rb;
    private Collider2D ownCollider;
    private Vector2 direction = Vector2.right;
    private GameObject owner;
    private float destroyAtTime;
    private bool launched;
    private bool stuck;

    private void Awake()
    {
        EnsureComponents();
    }

    private void OnEnable()
    {
        if (!stuck)
            destroyAtTime = Time.time + lifeTime;
    }

    private void Update()
    {
        if (!stuck && lifeTime > 0f && Time.time >= destroyAtTime)
            Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        if (rb != null && launched && !stuck)
            rb.linearVelocity = direction * speed;
    }

    public void Launch(Vector2 launchDirection, float launchSpeed, int launchDamage, float projectileLifetime, GameObject projectileOwner)
    {
        EnsureComponents();

        if (launchDirection.sqrMagnitude <= 0.001f)
            launchDirection = Vector2.right;

        direction = launchDirection.normalized;
        speed = launchSpeed;
        damage = launchDamage;
        lifeTime = projectileLifetime;
        owner = projectileOwner;
        destroyAtTime = Time.time + lifeTime;
        launched = true;
        stuck = false;

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

        if (ownCollider != null)
            ownCollider.enabled = true;
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

        if (ownCollider == null)
            ownCollider = GetComponent<Collider2D>();

        if (ownCollider == null)
        {
            BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.55f, 0.12f);
            ownCollider = box;
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
        if (stuck || other == null)
            return;

        if (IsOwnerOrOwnerChild(other.gameObject))
            return;

        EnemyHealth2D enemyHealth = FindEnemyHealthFromHitCollider(other);

        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);

            if (debugLogs)
                Debug.Log($"ArrowProjectile2D: {name} golpea a {enemyHealth.name} con {damage} de daño. Collider real golpeado: {other.name}.", this);

            if (stickToEnemyOnHit)
                StickToEnemy(other, enemyHealth);
            else
                Destroy(gameObject);

            return;
        }

        if (debugLogs && requireDirectEnemyColliderHit)
            Debug.Log($"ArrowProjectile2D: {name} ha tocado {other.name}, pero no se considera collider real de enemigo.", this);

        if (ShouldDestroyAgainst(other))
        {
            if (debugLogs)
                Debug.Log($"ArrowProjectile2D: {name} se destruye al tocar {other.name}.", this);

            Destroy(gameObject);
        }
    }

    private EnemyHealth2D FindEnemyHealthFromHitCollider(Collider2D hitCollider)
    {
        if (hitCollider == null)
            return null;

        // Caso correcto habitual: el collider golpeado está en el mismo objeto que la vida.
        EnemyHealth2D enemyHealth = hitCollider.GetComponent<EnemyHealth2D>();
        if (enemyHealth != null)
            return enemyHealth;

        // Caso correcto habitual: el collider está en un hijo del enemigo y la vida está en el padre.
        enemyHealth = hitCollider.GetComponentInParent<EnemyHealth2D>();
        if (enemyHealth != null)
            return enemyHealth;

        if (requireDirectEnemyColliderHit && searchChildrenOnlyOnEnemyRoots && !IsLikelyEnemyRoot(hitCollider))
            return null;

        // Solo como respaldo para prefabs donde el collider está en la raíz y EnemyHealth2D en un hijo.
        // No se usa con colliders genéricos del mapa para evitar daños a distancia.
        enemyHealth = hitCollider.GetComponentInChildren<EnemyHealth2D>();
        return enemyHealth;
    }

    private bool IsLikelyEnemyRoot(Collider2D hitCollider)
    {
        if (hitCollider == null)
            return false;

        GameObject obj = hitCollider.gameObject;

        if (obj.CompareTag("Enemy"))
            return true;

        int layerMask = 1 << obj.layer;
        if (enemyLayers.value != 0 && (enemyLayers.value & layerMask) != 0)
            return true;

        if (obj.GetComponent<EnemyChaser2D>() != null || obj.GetComponent<FireballEnemy2D>() != null || obj.GetComponent<EnemyHealth2D>() != null)
            return true;

        return false;
    }

    private void StickToEnemy(Collider2D hitCollider, EnemyHealth2D enemyHealth)
    {
        stuck = true;
        launched = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        if (disableColliderWhenStuck && ownCollider != null)
            ownCollider.enabled = false;

        Transform parent = null;

        if (parentToHitCollider && hitCollider != null)
            parent = hitCollider.transform;

        if (parent == null && enemyHealth != null)
            parent = enemyHealth.transform;

        if (parent != null)
            transform.SetParent(parent, true);

        if (destroyAfterStuckSeconds > 0f)
            Destroy(gameObject, destroyAfterStuckSeconds);
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
