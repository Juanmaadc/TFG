using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public enum AttackHitboxShape
    {
        Circle,
        HorizontalLine,
        AreaCircle
    }

    public float speed = 5f;
    public int facingDirection = 1;

    [Header("References")]
    public Rigidbody2D rb;
    public Animator anim;

    [Header("Wall Sliding / Anti Stuck")]
    [Tooltip("Aplica automaticamente un PhysicsMaterial2D sin friccion a los colliders del jugador para que no se quede pegado a las paredes.")]
    public bool autoApplyFrictionlessMaterial = true;

    [Tooltip("Material fisico opcional. Si se deja vacio, se crea uno temporal con friccion 0 y rebote 0.")]
    public PhysicsMaterial2D frictionlessMaterial;

    [Tooltip("Congela la rotacion Z del Rigidbody2D para evitar que el jugador se gire o se encaje al rozar paredes.")]
    public bool freezeRotationOnStart = true;

    [Tooltip("Mueve al jugador separando el desplazamiento horizontal y vertical. Esto ayuda a que el jugador se deslice por las paredes en lugar de quedarse bloqueado.")]
    public bool useAxisSeparatedWallSlideMovement = true;

    [Tooltip("Pequeña separacion que se deja antes de tocar una pared. Evita vibraciones y bloqueos en esquinas.")]
    public float wallSlideSkinWidth = 0.02f;

    [Tooltip("Si se asigna, solo estas capas bloquearan el movimiento por cast. Si se deja en Nothing, se usan todos los colliders solidos.")]
    public LayerMask movementObstacleLayers;

    [Header("Attack")]
    public float attackDuration = 0.35f;
    public float attackHitDelay = 0.1f;
    public int attackDamage = 1;
    public Transform attackPoint;
    public float attackRadius = 0.7f;

    [Header("Attack Shape")]
    [Tooltip("Circle: ataque normal alrededor del AttackPoint. HorizontalLine: ataque de lanza en una franja horizontal. AreaCircle: hechizo en area alrededor del jugador o AttackPoint.")]
    public AttackHitboxShape attackHitboxShape = AttackHitboxShape.Circle;

    [Header("Horizontal Line / Spear Attack")]
    [Tooltip("Longitud horizontal de la zona de golpeo para personajes con lanza.")]
    public float horizontalAttackLength = 2.2f;

    [Tooltip("Altura de la zona de golpeo para personajes con lanza.")]
    public float horizontalAttackHeight = 0.45f;

    [Tooltip("Distancia desde el centro del personaje hasta el centro de la zona de golpeo horizontal. Para una lanza, normalmente usa la mitad de Horizontal Attack Length.")]
    public float horizontalAttackForwardOffset = 1.1f;

    [Tooltip("Si está activo, la caja horizontal se centra exactamente en AttackPoint. Si está desactivado, se centra usando la posición del jugador + Forward Offset.")]
    public bool horizontalAttackUseAttackPointAsCenter = false;

    [Header("Area Spell Attack")]
    [Tooltip("Radio del ataque en area para personajes que invocan hechizos.")]
    public float areaAttackRadius = 2.5f;

    [Tooltip("Si está activo, el area se centra en el jugador. Si está desactivado, se centra en AttackPoint.")]
    public bool areaAttackCenteredOnPlayer = true;

    [Tooltip("Se mantiene por compatibilidad, pero por defecto el ataque busca EnemyHealth2D aunque el enemigo esté en otra Layer.")]
    public LayerMask enemyLayer;

    [Tooltip("ACTIVADO: el ataque golpea colliders cercanos y solo aplica daño si pertenecen realmente a un enemigo. Evita golpear enemigos lejanos a través de triggers grandes de las salas.")]
    public bool detectEnemiesByComponent = true;

    [Tooltip("Permite buscar EnemyHealth2D en hijos del collider golpeado, pero solo si ese collider parece ser la raíz real de un enemigo. No busca en hijos de triggers grandes del mapa.")]
    public bool searchEnemyHealthInChildrenOnlyOnEnemyRoots = true;

    public float enemyKnockbackDistance = 0.8f;
    public float enemyKnockbackDuration = 0.12f;

    [Header("Debug")]
    public bool debugAttackLogs = false;

    [Header("Hurt Knockback")]
    [Tooltip("Si está activo, el jugador no puede moverse mientras dura el empujón de daño.")]
    public bool blockMovementDuringKnockback = true;

    private Vector2 movement;
    private bool isAttacking;

    private Vector2 playerKnockbackVelocity;
    private float playerKnockbackTimer;

    public bool IsBeingKnockedBack => playerKnockbackTimer > 0f;

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
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

        ConfigureRigidbodyForTopDownMovement();
        ApplyFrictionlessMaterialIfNeeded();
    }

    void Update()
    {
        if (isAttacking)
            return;

        if (blockMovementDuringKnockback && IsBeingKnockedBack)
        {
            movement = Vector2.zero;

            if (anim != null)
                anim.SetFloat("Speed", 0f);

            return;
        }

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
        if (playerKnockbackTimer > 0f)
        {
            rb.linearVelocity = playerKnockbackVelocity;
            playerKnockbackTimer -= Time.fixedDeltaTime;

            if (playerKnockbackTimer <= 0f)
            {
                playerKnockbackTimer = 0f;
                playerKnockbackVelocity = Vector2.zero;
                rb.linearVelocity = Vector2.zero;
            }

            return;
        }

        if (isAttacking)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (useAxisSeparatedWallSlideMovement)
        {
            MoveWithWallSlide(movement * speed * Time.fixedDeltaTime);
        }
        else
        {
            rb.linearVelocity = movement * speed;
        }
    }


    private void ConfigureRigidbodyForTopDownMovement()
    {
        if (rb == null)
            return;

        if (freezeRotationOnStart)
            rb.freezeRotation = true;

        // En un juego top-down no queremos que la gravedad afecte al jugador.
        rb.gravityScale = 0f;
    }

    private void ApplyFrictionlessMaterialIfNeeded()
    {
        if (!autoApplyFrictionlessMaterial)
            return;

        if (frictionlessMaterial == null)
        {
            frictionlessMaterial = new PhysicsMaterial2D("Runtime_Player_Frictionless")
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

    private void MoveWithWallSlide(Vector2 desiredDelta)
    {
        if (rb == null)
            return;

        rb.linearVelocity = Vector2.zero;

        if (desiredDelta.sqrMagnitude <= 0.0000001f)
            return;

        // Primero movemos en X y luego en Y. Si una direccion queda bloqueada por una pared,
        // la otra puede seguir avanzando, lo que crea un deslizamiento suave por los muros.
        MoveSingleAxis(new Vector2(desiredDelta.x, 0f));
        MoveSingleAxis(new Vector2(0f, desiredDelta.y));
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

        rb.position = rb.position + direction * allowedDistance;
    }

    private float GetAllowedMoveDistance(Vector2 direction, float requestedDistance)
    {
        if (requestedDistance <= 0f)
            return 0f;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;

        if (movementObstacleLayers.value != 0)
        {
            filter.useLayerMask = true;
            filter.layerMask = movementObstacleLayers;
        }
        else
        {
            filter.useLayerMask = false;
        }

        RaycastHit2D[] results = new RaycastHit2D[8];
        int hitCount = rb.Cast(direction, filter, results, requestedDistance + wallSlideSkinWidth);

        float allowedDistance = requestedDistance;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = results[i];

            if (hit.collider == null)
                continue;

            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                continue;

            // Distancia maxima antes de tocar la pared.
            allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - wallSlideSkinWidth));
        }

        return allowedDistance;
    }

    public void ApplyKnockback(Vector2 direction, float distance, float duration)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (direction.sqrMagnitude <= 0.001f || distance <= 0f || duration <= 0f || rb == null)
            return;

        isAttacking = false;
        movement = Vector2.zero;
        playerKnockbackVelocity = direction.normalized * (distance / duration);
        playerKnockbackTimer = duration;

        if (anim != null)
            anim.SetFloat("Speed", 0f);
    }

    IEnumerator DoAttack()
    {
        isAttacking = true;

        if (rb != null)
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
        Collider2D[] hits = GetAttackHits();
        HashSet<EnemyHealth2D> damagedEnemies = new HashSet<EnemyHealth2D>();

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
                continue;

            if (IsOwnCollider(hit))
                continue;

            EnemyHealth2D enemyHealth = ResolveEnemyHealthFromHit(hit);

            if (enemyHealth == null || damagedEnemies.Contains(enemyHealth))
                continue;

            Vector2 hitDirection = ((Vector2)enemyHealth.transform.position - (Vector2)transform.position).normalized;

            if (hitDirection.sqrMagnitude <= 0.001f)
                hitDirection = new Vector2(facingDirection, 0f);

            enemyHealth.TakeDamage(
                attackDamage,
                hitDirection,
                enemyKnockbackDistance,
                enemyKnockbackDuration
            );

            if (debugAttackLogs)
                Debug.Log($"PlayerMovement: golpeado {enemyHealth.name} con {attackDamage} de daño usando hitbox {attackHitboxShape}.", enemyHealth);

            damagedEnemies.Add(enemyHealth);
        }

        if (debugAttackLogs && damagedEnemies.Count == 0)
            Debug.Log($"PlayerMovement: ataque {attackHitboxShape} realizado, pero no se encontró ningún EnemyHealth2D dentro de la zona real del ataque.");
    }

    private Collider2D[] GetAttackHits()
    {
        Vector2 center;

        switch (attackHitboxShape)
        {
            case AttackHitboxShape.HorizontalLine:
                center = GetHorizontalAttackCenter();
                Vector2 boxSize = new Vector2(
                    Mathf.Max(0.01f, horizontalAttackLength),
                    Mathf.Max(0.01f, horizontalAttackHeight)
                );

                if (detectEnemiesByComponent)
                    return Physics2D.OverlapBoxAll(center, boxSize, 0f);

                return Physics2D.OverlapBoxAll(center, boxSize, 0f, enemyLayer);

            case AttackHitboxShape.AreaCircle:
                center = GetAreaAttackCenter();
                float areaRadius = Mathf.Max(0.01f, areaAttackRadius);

                if (detectEnemiesByComponent)
                    return Physics2D.OverlapCircleAll(center, areaRadius);

                return Physics2D.OverlapCircleAll(center, areaRadius, enemyLayer);

            case AttackHitboxShape.Circle:
            default:
                center = attackPoint != null ? (Vector2)attackPoint.position : (Vector2)transform.position;
                float radius = Mathf.Max(0.01f, attackRadius);

                if (attackPoint == null && debugAttackLogs)
                    Debug.LogWarning("PlayerMovement: attackPoint no está asignado. Se usará la posición del jugador como centro del ataque circular.", this);

                if (detectEnemiesByComponent)
                    return Physics2D.OverlapCircleAll(center, radius);

                return Physics2D.OverlapCircleAll(center, radius, enemyLayer);
        }
    }

    private Vector2 GetHorizontalAttackCenter()
    {
        if (horizontalAttackUseAttackPointAsCenter && attackPoint != null)
            return attackPoint.position;

        return (Vector2)transform.position + Vector2.right * facingDirection * horizontalAttackForwardOffset;
    }

    private Vector2 GetAreaAttackCenter()
    {
        if (areaAttackCenteredOnPlayer || attackPoint == null)
            return transform.position;

        return attackPoint.position;
    }

    private bool IsOwnCollider(Collider2D hit)
    {
        if (hit == null)
            return true;

        if (hit.transform == transform)
            return true;

        return hit.transform.IsChildOf(transform);
    }

    private EnemyHealth2D ResolveEnemyHealthFromHit(Collider2D hit)
    {
        if (hit == null)
            return null;

        // Caso correcto más común: el collider o alguno de sus padres pertenece al enemigo.
        EnemyHealth2D enemyHealth = hit.GetComponent<EnemyHealth2D>();

        if (enemyHealth == null)
            enemyHealth = hit.GetComponentInParent<EnemyHealth2D>();

        if (enemyHealth != null)
            return enemyHealth;

        // Caso especial: algunos prefabs tienen el collider en la raíz y la vida en un hijo.
        // Esto antes se hacía sobre cualquier collider y podía capturar enemigos lejanos desde
        // triggers enormes de las salas. Ahora solo se permite si el collider parece ser raíz de enemigo.
        if (searchEnemyHealthInChildrenOnlyOnEnemyRoots && LooksLikeEnemyRoot(hit))
            enemyHealth = hit.GetComponentInChildren<EnemyHealth2D>();

        return enemyHealth;
    }

    private bool LooksLikeEnemyRoot(Collider2D hit)
    {
        if (hit == null)
            return false;

        Transform t = hit.transform;

        if (t.GetComponent<EnemyChaser2D>() != null || t.GetComponentInParent<EnemyChaser2D>() != null)
            return true;

        if (t.GetComponent<FireballEnemy2D>() != null || t.GetComponentInParent<FireballEnemy2D>() != null)
            return true;

        if (t.GetComponent<EnemyDeathSpawner2D>() != null || t.GetComponentInParent<EnemyDeathSpawner2D>() != null)
            return true;

        if (hit.attachedRigidbody != null)
        {
            Transform rbTransform = hit.attachedRigidbody.transform;

            if (rbTransform.GetComponent<EnemyChaser2D>() != null)
                return true;

            if (rbTransform.GetComponent<FireballEnemy2D>() != null)
                return true;

            if (rbTransform.GetComponent<EnemyDeathSpawner2D>() != null)
                return true;
        }

        if (enemyLayer.value != 0)
        {
            int layerMask = 1 << hit.gameObject.layer;
            if ((enemyLayer.value & layerMask) != 0)
                return true;
        }

        return false;
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
        Gizmos.color = Color.red;

        switch (attackHitboxShape)
        {
            case AttackHitboxShape.HorizontalLine:
                Vector2 boxCenter = GetHorizontalAttackCenter();
                Vector2 boxSize = new Vector2(
                    Mathf.Max(0.01f, horizontalAttackLength),
                    Mathf.Max(0.01f, horizontalAttackHeight)
                );
                Gizmos.DrawWireCube(boxCenter, boxSize);
                break;

            case AttackHitboxShape.AreaCircle:
                Gizmos.DrawWireSphere(GetAreaAttackCenter(), Mathf.Max(0.01f, areaAttackRadius));
                break;

            case AttackHitboxShape.Circle:
            default:
                Vector2 circleCenter = attackPoint != null ? (Vector2)attackPoint.position : (Vector2)transform.position;
                Gizmos.DrawWireSphere(circleCenter, Mathf.Max(0.01f, attackRadius));
                break;
        }
    }
}
