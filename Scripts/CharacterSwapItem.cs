using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CharacterChangeItem : MonoBehaviour
{
    [SerializeField] private CharacterSwapManager swapManager;
    [SerializeField] private bool destroyOnUse = true;

    private bool used;

    void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (used)
            return;

        if (!other.CompareTag("Player"))
            return;

        if (swapManager == null)
            swapManager = FindObjectOfType<CharacterSwapManager>();

        if (swapManager == null)
        {
            Debug.LogWarning("CharacterChangeItem: no se encontró CharacterSwapManager.");
            return;
        }

        used = true;
        swapManager.SwapToRandomDifferent(other.gameObject);

        if (destroyOnUse)
            Destroy(gameObject);
    }
}