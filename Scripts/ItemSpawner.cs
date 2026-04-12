using UnityEngine;

public class CharacterChangeItemSpawner2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DungeonRoomsCorridors2D dungeon;
    [SerializeField] private GameObject characterChangeItemPrefab;

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

    public void RebuildItem()
    {
        if (dungeon == null || characterChangeItemPrefab == null)
        {
            Debug.LogWarning("CharacterChangeItemSpawner2D: faltan referencias.");
            return;
        }

        ClearPrevious();

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

    void ClearPrevious()
    {
        if (currentItem != null)
            Destroy(currentItem);
    }
}