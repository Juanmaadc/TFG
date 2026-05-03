using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyChaser2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float stopDistance = 0.1f;
    [SerializeField] private bool sleepOnStart = true;

    [Header("Contact damage")]
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private float contactDamageCooldown = 1f;

    private Rigidbody2D rb;
    private Transform target;
    private Vector3 spawnPoint;
    private bool isAwake;

    private Vector2 knockbackVelocity;
    private float knockbackTimer;
    private float lastDamageTime = -999f;

    public bool IsAwake => isAwake;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.bodyType = RigidbodyType2D.Kinematic;

        spawnPoint = transform.position;
        isAwake = !sleepOnStart;
    }

    void Start()
    {
        if (isAwake && target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }
    }

    public void WakeUp(Transform player)
    {
        target = player;
        isAwake = true;
    }

    public void Retarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void Sleep()
    {
        isAwake = false;
        target = null;
        knockbackTimer = 0f;
        knockbackVelocity = Vector2.zero;
    }

    public void ReturnToSpawn()
    {
        rb.position = spawnPoint;
    }

    public void ApplyKnockback(Vector2 direction, float distance, float duration)
    {
        if (direction.sqrMagnitude <= 0.001f || distance <= 0f || duration <= 0f)
            return;

        knockbackVelocity = direction.normalized * (distance / duration);
        knockbackTimer = duration;
    }

    void FixedUpdate()
    {
        Vector2 current = rb.position;

        if (knockbackTimer > 0f)
        {
            float step = Time.fixedDeltaTime;
            rb.MovePosition(current + knockbackVelocity * step);
            knockbackTimer -= step;

            if (knockbackTimer <= 0f)
            {
                knockbackTimer = 0f;
                knockbackVelocity = Vector2.zero;
            }

            return;
        }

        if (!isAwake || target == null)
            return;

        Vector2 targetPos = target.position;

        if (Vector2.Distance(current, targetPos) <= stopDistance)
            return;

        Vector2 next = Vector2.MoveTowards(
            current,
            targetPos,
            moveSpeed * Time.fixedDeltaTime
        );

        rb.MovePosition(next);
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

        playerHealth.TakeDamage(contactDamage);
        lastDamageTime = Time.time;
    }
}
