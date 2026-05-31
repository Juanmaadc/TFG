using UnityEngine;

public class CharacterChangeItemSpawner2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DungeonRoomsCorridors2D dungeon;
    [SerializeField] private GameObject characterChangeItemPrefab;

    [Header("Random spawn mode")]
    [Tooltip("Desactívalo si el libro de cambio de personaje debe aparecer al matar enemigos en vez de aparecer aleatoriamente en la dungeon.")]
    [SerializeField] private bool spawnRandomlyOnDungeonGenerated = false;

    [Header("Spawn")]
    [SerializeField] private int spawnMarginInsideRoom = 1;

    private Transform runtimeRoot;
    private GameObject currentItem;

    void OnEnable()
    {
        if (dungeon != null)
            dungeon.OnDungeonGenerated += RebuildItem;
    }

    void OnDisable()
    {
        if (dungeon != null)
            dungeon.OnDungeonGenerated -= RebuildItem;
    }

    void Start()
    {
        if (dungeon != null && dungeon.HasGeneratedMap)
            RebuildItem();
    }

    public void SetRandomSpawnEnabled(bool enabled)
    {
        spawnRandomlyOnDungeonGenerated = enabled;

        if (!spawnRandomlyOnDungeonGenerated)
            ClearCurrentItem();
    }

    public void RebuildItem()
    {
        if (!spawnRandomlyOnDungeonGenerated)
        {
            ClearCurrentItem();
            return;
        }

        if (dungeon == null || characterChangeItemPrefab == null)
        {
            Debug.LogWarning("CharacterChangeItemSpawner2D: faltan referencias.");
            return;
        }

        ClearCurrentItem();

        if (runtimeRoot == null)
        {
            GameObject root = new GameObject("GeneratedSpecialItems");
            runtimeRoot = root.transform;
        }

        if (dungeon.Rooms == null || dungeon.Rooms.Count < 3)
        {
            Debug.LogWarning("CharacterChangeItemSpawner2D: no hay suficientes salas para excluir la primera y la última.");
            return;
        }

        int selectedIndex = Random.Range(1, dungeon.Rooms.Count - 1);
        RectInt selectedRoom = dungeon.Rooms[selectedIndex];

        Vector3 spawnPos = dungeon.GetRandomWorldPositionInRoom(selectedRoom, spawnMarginInsideRoom);

        currentItem = Instantiate(
            characterChangeItemPrefab,
            spawnPos,
            Quaternion.identity,
            runtimeRoot
        );
    }

    public void ClearCurrentItem()
    {
        if (currentItem != null)
        {
            Destroy(currentItem);
            currentItem = null;
        }
    }
}
