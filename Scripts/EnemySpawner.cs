using System.Collections.Generic;
using UnityEngine;

public class DungeonEnemySpawner2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DungeonRoomsCorridors2D dungeon;
    [SerializeField] private GameObject enemyPrefab;

    [Header("Spawn")]
    [SerializeField] private int minEnemiesPerRoom = 1;
    [SerializeField] private int maxEnemiesPerRoom = 3;
    [SerializeField] private bool skipOriginRoom = true;
    [SerializeField] private int spawnMarginInsideRoom = 1;

    [Header("Trigger")]
    [SerializeField] private float triggerShrink = 0.1f;

    private Transform runtimeRoot;

    void OnEnable()
    {
        if (dungeon != null)
            dungeon.OnDungeonGenerated += RebuildRoomsAndEnemies;
    }

    void OnDisable()
    {
        if (dungeon != null)
            dungeon.OnDungeonGenerated -= RebuildRoomsAndEnemies;
    }

    void Start()
    {
        if (dungeon != null && dungeon.HasGeneratedMap)
            RebuildRoomsAndEnemies();
    }

    public void RebuildRoomsAndEnemies()
    {
        if (dungeon == null || enemyPrefab == null)
        {
            Debug.LogWarning("DungeonEnemySpawner2D: faltan referencias.");
            return;
        }

        ClearPrevious();

        if (runtimeRoot == null)
        {
            GameObject root = new GameObject("GeneratedRoomsRuntime");
            runtimeRoot = root.transform;
        }

        foreach (RectInt room in dungeon.Rooms)
        {
            if (skipOriginRoom && dungeon.HasOriginRoom && room.Equals(dungeon.OriginRoom))
                continue;

            CreateRoomRuntime(room);
        }
    }

    void CreateRoomRuntime(RectInt room)
    {
        GameObject roomGO = new GameObject($"Room_{room.xMin}_{room.yMin}");
        roomGO.transform.SetParent(runtimeRoot);
        roomGO.transform.position = dungeon.GetRoomTriggerCenterWorld(room);

        BoxCollider2D trigger = roomGO.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;

        Vector2 size = dungeon.GetRoomSizeWorld(room);
        trigger.size = new Vector2(
            Mathf.Max(0.1f, size.x - triggerShrink),
            Mathf.Max(0.1f, size.y - triggerShrink)
        );

        RoomTrigger2D roomTrigger = roomGO.AddComponent<RoomTrigger2D>();

        int enemyCount = Random.Range(minEnemiesPerRoom, maxEnemiesPerRoom + 1);
        List<EnemyChaser2D> roomEnemies = new();

        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 spawnPos = dungeon.GetRandomWorldPositionInRoom(room, spawnMarginInsideRoom);

            GameObject enemyGO = Instantiate(
                enemyPrefab,
                spawnPos,
                Quaternion.identity,
                roomGO.transform
            );

            EnemyChaser2D enemy = enemyGO.GetComponent<EnemyChaser2D>();
            if (enemy == null)
                enemy = enemyGO.AddComponent<EnemyChaser2D>();

            roomEnemies.Add(enemy);
        }

        roomTrigger.Configure(roomEnemies);
    }

    void ClearPrevious()
    {
        if (runtimeRoot == null)
            return;

        for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(runtimeRoot.GetChild(i).gameObject);
        }
    }
}