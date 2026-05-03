using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ProfessorNPC2D : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private bool askAutomaticallyOnTouch = false;

    private ProfessorEncounterManager2D encounterManager;
    private bool playerInside;
    private bool resolved;

    void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    public void Configure(ProfessorEncounterManager2D manager)
    {
        encounterManager = manager;
        SetPrompt(false);
    }

    void Update()
    {
        if (resolved || encounterManager == null || !playerInside || askAutomaticallyOnTouch)
            return;

        if (Input.GetKeyDown(interactKey))
            encounterManager.TryStartEncounter(this);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (resolved || !other.CompareTag("Player"))
            return;

        playerInside = true;

        if (askAutomaticallyOnTouch)
        {
            SetPrompt(false);
            encounterManager?.TryStartEncounter(this);
            return;
        }

        SetPrompt(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
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

    private void SetPrompt(bool isVisible)
    {
        if (interactionPrompt != null)
            interactionPrompt.SetActive(isVisible);
    }
}

