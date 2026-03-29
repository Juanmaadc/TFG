using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyChaser2D : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float stopDistance = 0.1f;
    [SerializeField] private bool sleepOnStart = true;

    private Rigidbody2D rb;
    private Transform target;
    private Vector3 spawnPoint;
    private bool isAwake;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.bodyType = RigidbodyType2D.Kinematic;

        spawnPoint = transform.position;
        isAwake = !sleepOnStart;
    }

    public void WakeUp(Transform player)
    {
        target = player;
        isAwake = true;
    }

    public void Sleep()
    {
        isAwake = false;
        target = null;
    }

    public void ReturnToSpawn()
    {
        rb.position = spawnPoint;
    }

    void FixedUpdate()
    {
        if (!isAwake || target == null)
            return;

        Vector2 current = rb.position;
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
}