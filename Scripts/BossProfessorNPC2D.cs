using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BossProfessorNPC2D : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private bool askAutomaticallyOnTouch = false;
    [SerializeField] private string playerTag = "Player";

    private BossArenaScene2D bossArena;
    private bool playerInside;
    private bool resolved;
    private bool interactionEnabled = true;
    private Collider2D ownCollider;

    void Reset()
    {
        EnsureColliderIsTrigger();
    }

    void Awake()
    {
        EnsureColliderIsTrigger();
        SetPrompt(false);
    }

    public void Configure(BossArenaScene2D arena, bool automaticOnTouch, string requiredPlayerTag)
    {
        bossArena = arena;
        askAutomaticallyOnTouch = automaticOnTouch;

        if (!string.IsNullOrWhiteSpace(requiredPlayerTag))
            playerTag = requiredPlayerTag;

        resolved = false;
        playerInside = false;
        interactionEnabled = true;
        EnsureColliderIsTrigger();
        SetPrompt(false);
    }

    void Update()
    {
        if (resolved || !interactionEnabled)
            return;

        ResolveArenaIfNeeded();

        if (bossArena == null || !playerInside || askAutomaticallyOnTouch)
            return;

        if (Input.GetKeyDown(interactKey))
            bossArena.OpenProfessorStory(this);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (resolved || !interactionEnabled || !IsPlayerCollider(other))
            return;

        playerInside = true;
        ResolveArenaIfNeeded();

        if (askAutomaticallyOnTouch)
        {
            SetPrompt(false);
            bossArena?.OpenProfessorStory(this);
            return;
        }

        SetPrompt(true);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (resolved || !interactionEnabled || playerInside || !IsPlayerCollider(other))
            return;

        playerInside = true;
        ResolveArenaIfNeeded();

        if (askAutomaticallyOnTouch)
        {
            SetPrompt(false);
            bossArena?.OpenProfessorStory(this);
        }
        else
        {
            SetPrompt(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInside = false;
        SetPrompt(false);
    }

    public void MarkResolved()
    {
        resolved = true;
        playerInside = false;
        SetPrompt(false);
    }

    public void SetInteractionEnabled(bool isEnabled)
    {
        interactionEnabled = isEnabled;
        playerInside = false;
        EnsureColliderIsTrigger();

        if (ownCollider != null)
            ownCollider.enabled = isEnabled;

        SetPrompt(false);
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

    private void ResolveArenaIfNeeded()
    {
        if (bossArena != null)
            return;

        bossArena = FindFirstObjectByType<BossArenaScene2D>();
    }

    private void EnsureColliderIsTrigger()
    {
        ownCollider = GetComponent<Collider2D>();
        if (ownCollider != null)
            ownCollider.isTrigger = true;
    }

    private void SetPrompt(bool isVisible)
    {
        if (interactionPrompt != null)
            interactionPrompt.SetActive(isVisible);
    }
}
