using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class StatBook2D : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool alsoCheckByRadius = true;
    [SerializeField, Min(0.05f)] private float fallbackDetectionRadius = 0.85f;

    [Header("Debug")]
    [SerializeField] private bool logDebugMessages = false;

    private StatBookEncounterManager2D encounterManager;
    private Collider2D ownCollider;
    private bool resolved;

    void Reset()
    {
        EnsureColliderIsTrigger();
    }

    void Awake()
    {
        EnsureColliderIsTrigger();
    }

    void Start()
    {
        ResolveManagerIfNeeded();
    }

    void Update()
    {
        // Fallback por si OnTriggerEnter2D no salta por capas, collider en hijo, o porque el libro
        // ya estaba solapando al jugador al aparecer.
        if (resolved || !alsoCheckByRadius)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, fallbackDetectionRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsPlayerCollider(hits[i]))
            {
                TryStart(hits[i]);
                break;
            }
        }
    }

    public void Configure(StatBookEncounterManager2D manager)
    {
        encounterManager = manager;
        resolved = false;
        EnsureColliderIsTrigger();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryStart(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryStart(other);
    }

    private void TryStart(Collider2D other)
    {
        if (resolved || !IsPlayerCollider(other))
            return;

        ResolveManagerIfNeeded();

        if (encounterManager == null)
        {
            if (logDebugMessages)
                Debug.LogWarning("StatBook2D: el jugador tocó el StatBook, pero no se encontró StatBookEncounterManager2D en la escena.", this);
            return;
        }

        if (logDebugMessages)
            Debug.Log("StatBook2D: jugador detectado, intentando abrir pregunta.", this);

        encounterManager.TryStartStatBook(this);
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
            return false;

        if (other.CompareTag(playerTag))
            return true;

        Transform root = other.transform.root;
        if (root != null && root.CompareTag(playerTag))
            return true;

        if (other.GetComponentInParent<PlayerMovement>() != null)
            return true;

        if (other.GetComponentInParent<PlayerHealth2D>() != null)
            return true;

        return false;
    }

    private void ResolveManagerIfNeeded()
    {
        if (encounterManager != null)
            return;

        encounterManager = FindObjectOfType<StatBookEncounterManager2D>();
    }

    private void EnsureColliderIsTrigger()
    {
        ownCollider = GetComponent<Collider2D>();
        if (ownCollider != null)
            ownCollider.isTrigger = true;
    }

    public void MarkResolved()
    {
        resolved = true;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!alsoCheckByRadius)
            return;

        Gizmos.DrawWireSphere(transform.position, fallbackDetectionRadius);
    }
#endif
}
