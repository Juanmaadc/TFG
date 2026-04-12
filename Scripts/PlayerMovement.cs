using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public int facingDirection = 1;

    [Header("References")]
    public Rigidbody2D rb;
    public Animator anim;

    [Header("Attack")]
    public float attackDuration = 0.35f;
    public float attackHitDelay = 0.1f;
    public int attackDamage = 1;
    public Transform attackPoint;
    public float attackRadius = 0.7f;
    public LayerMask enemyLayer;

    private Vector2 movement;
    private bool isAttacking;

    void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (anim == null)
            anim = GetComponent<Animator>();

        if (attackPoint == null)
        {
            Transform found = transform.Find("AttackPoint");
            if (found != null)
                attackPoint = found;
        }
    }

    void OnEnable()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (anim == null)
            anim = GetComponent<Animator>();

        if (attackPoint == null)
        {
            Transform found = transform.Find("AttackPoint");
            if (found != null)
                attackPoint = found;
        }
    }

    void Update()
    {
        if (isAttacking)
            return;

        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");
        movement = movement.normalized;

        if ((movement.x > 0 && transform.localScale.x < 0) ||
            (movement.x < 0 && transform.localScale.x > 0))
        {
            Flip();
        }

        if (anim != null)
            anim.SetFloat("Speed", movement.sqrMagnitude);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(DoAttack());
        }
    }

    void FixedUpdate()
    {
        if (isAttacking)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = movement * speed;
    }

    IEnumerator DoAttack()
    {
        isAttacking = true;
        rb.linearVelocity = Vector2.zero;

        if (anim != null)
        {
            anim.SetFloat("Speed", 0f);
            anim.SetTrigger("Attack");
        }

        yield return new WaitForSeconds(attackHitDelay);

        DealDamage();

        float remainingTime = attackDuration - attackHitDelay;
        if (remainingTime > 0f)
            yield return new WaitForSeconds(remainingTime);

        isAttacking = false;
    }

    void DealDamage()
    {
        if (attackPoint == null)
        {
            Debug.LogWarning("PlayerMovement: attackPoint no está asignado.");
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            attackPoint.position,
            attackRadius,
            enemyLayer
        );

        HashSet<EnemyHealth2D> damagedEnemies = new HashSet<EnemyHealth2D>();

        foreach (Collider2D hit in hits)
        {
            EnemyHealth2D enemyHealth = hit.GetComponent<EnemyHealth2D>();

            if (enemyHealth == null)
                enemyHealth = hit.GetComponentInParent<EnemyHealth2D>();

            if (enemyHealth != null && !damagedEnemies.Contains(enemyHealth))
            {
                enemyHealth.TakeDamage(attackDamage);
                damagedEnemies.Add(enemyHealth);
            }
        }
    }

    void Flip()
    {
        facingDirection *= -1;

        transform.localScale = new Vector3(
            transform.localScale.x * -1,
            transform.localScale.y,
            transform.localScale.z
        );

        if (attackPoint != null)
        {
            Vector3 localPos = attackPoint.localPosition;
            localPos.x = Mathf.Abs(localPos.x) * facingDirection;
            attackPoint.localPosition = localPos;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }
}