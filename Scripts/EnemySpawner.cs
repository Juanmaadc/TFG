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
        EnsureRuntimeRoot();

        foreach (RectInt room in dungeon.Rooms)
        {
            if (skipOriginRoom && dungeon.HasOriginRoom && room.Equals(dungeon.OriginRoom))
                continue;

            CreateRoomRuntime(room);
        }
    }

    public void SpawnExtraEnemiesInRoom(RectInt room, int amount, Transform initialTarget = null)
    {
        if (dungeon == null || enemyPrefab == null)
        {
            Debug.LogWarning("DungeonEnemySpawner2D: faltan referencias para generar enemigos extra.");
            return;
        }

        if (amount <= 0)
            return;

        RoomTrigger2D roomTrigger = CreateRoomRuntimeIfMissing(room);
        if (roomTrigger == null)
            return;

        for (int i = 0; i < amount; i++)
        {
            EnemyChaser2D enemy = SpawnEnemy(room, roomTrigger.transform);
            roomTrigger.AddEnemy(enemy);

            if (enemy != null && initialTarget != null)
                enemy.WakeUp(initialTarget);
        }
    }

    void CreateRoomRuntime(RectInt room)
    {
        RoomTrigger2D roomTrigger = CreateRoomRuntimeIfMissing(room);
        if (roomTrigger == null)
            return;

        int enemyCount = Random.Range(minEnemiesPerRoom, maxEnemiesPerRoom + 1);

        for (int i = 0; i < enemyCount; i++)
        {
            EnemyChaser2D enemy = SpawnEnemy(room, roomTrigger.transform);
            roomTrigger.AddEnemy(enemy);
        }
    }

    RoomTrigger2D CreateRoomRuntimeIfMissing(RectInt room)
    {
        EnsureRuntimeRoot();

        string roomObjectName = GetRoomObjectName(room);
        Transform existing = runtimeRoot.Find(roomObjectName);

        if (existing != null)
        {
            RoomTrigger2D existingTrigger = existing.GetComponent<RoomTrigger2D>();
            if (existingTrigger != null)
                return existingTrigger;
        }

        GameObject roomGO = existing != null ? existing.gameObject : new GameObject(roomObjectName);
        roomGO.transform.SetParent(runtimeRoot);
        roomGO.transform.position = dungeon.GetRoomTriggerCenterWorld(room);

        BoxCollider2D trigger = roomGO.GetComponent<BoxCollider2D>();
        if (trigger == null)
            trigger = roomGO.AddComponent<BoxCollider2D>();

        trigger.isTrigger = true;

        Vector2 size = dungeon.GetRoomSizeWorld(room);
        trigger.size = new Vector2(
            Mathf.Max(0.1f, size.x - triggerShrink),
            Mathf.Max(0.1f, size.y - triggerShrink)
        );

        RoomTrigger2D roomTrigger = roomGO.GetComponent<RoomTrigger2D>();
        if (roomTrigger == null)
        {
            roomTrigger = roomGO.AddComponent<RoomTrigger2D>();
            roomTrigger.Configure(new List<EnemyChaser2D>());
        }

        return roomTrigger;
    }

    EnemyChaser2D SpawnEnemy(RectInt room, Transform parent)
    {
        Vector3 spawnPos = dungeon.GetRandomWorldPositionInRoom(room, spawnMarginInsideRoom);

        GameObject enemyGO = Instantiate(
            enemyPrefab,
            spawnPos,
            Quaternion.identity,
            parent
        );

        EnemyChaser2D enemy = enemyGO.GetComponent<EnemyChaser2D>();
        if (enemy == null)
            enemy = enemyGO.AddComponent<EnemyChaser2D>();

        return enemy;
    }

    void EnsureRuntimeRoot()
    {
        if (runtimeRoot != null)
            return;

        GameObject root = new GameObject("GeneratedRoomsRuntime");
        runtimeRoot = root.transform;
    }

    string GetRoomObjectName(RectInt room)
    {
        return $"Room_{room.xMin}_{room.yMin}";
    }

    void ClearPrevious()
    {
        if (runtimeRoot == null)
            return;

        for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
            Destroy(runtimeRoot.GetChild(i).gameObject);
    }
}
