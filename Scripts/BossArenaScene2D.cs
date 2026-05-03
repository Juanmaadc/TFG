using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BossArenaScene2D : MonoBehaviour
{
    [Header("Arena tiles")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private TileBase floorTile;
    [SerializeField] private TileBase wallTile;

    [Header("Arena shape")]
    [SerializeField, Min(5)] private int arenaSize = 24;
    [SerializeField, Min(1)] private int wallThickness = 1;
    [SerializeField] private Vector3Int arenaCenterCell = Vector3Int.zero;
    [SerializeField] private bool clearTilemapsBeforeBuild = true;
    [SerializeField] private bool buildOnAwake = true;

    [Header("Boss activation")]
    [SerializeField] private EnemyChaser2D bossEnemy;
    [SerializeField] private List<EnemyChaser2D> additionalEnemiesToWake = new List<EnemyChaser2D>();
    [SerializeField] private bool autoFindEnemiesIfListIsEmpty = false;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool wakeEnemiesOnStart = true;
    [SerializeField, Min(0f)] private float playerLookupRetryDuration = 2f;
    [SerializeField, Min(0.05f)] private float playerLookupRetryInterval = 0.2f;

    private Coroutine wakeRoutine;

    void Awake()
    {
        if (buildOnAwake)
            BuildArena();
    }

    void Start()
    {
        if (wakeEnemiesOnStart)
            wakeRoutine = StartCoroutine(WakeEnemiesWhenPlayerIsAvailable());
    }

    [ContextMenu("Build boss arena")]
    public void BuildArena()
    {
        if (floorTilemap == null || wallTilemap == null)
        {
            Debug.LogWarning("BossArenaScene2D: faltan floorTilemap o wallTilemap.");
            return;
        }

        if (floorTile == null || wallTile == null)
        {
            Debug.LogWarning("BossArenaScene2D: faltan floorTile o wallTile.");
            return;
        }

        if (clearTilemapsBeforeBuild)
        {
            floorTilemap.ClearAllTiles();
            wallTilemap.ClearAllTiles();
        }

        int innerHalf = arenaSize / 2;
        int minX = arenaCenterCell.x - innerHalf;
        int minY = arenaCenterCell.y - innerHalf;
        int maxX = minX + arenaSize - 1;
        int maxY = minY + arenaSize - 1;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                floorTilemap.SetTile(new Vector3Int(x, y, 0), floorTile);
            }
        }

        int outerMinX = minX - wallThickness;
        int outerMinY = minY - wallThickness;
        int outerMaxX = maxX + wallThickness;
        int outerMaxY = maxY + wallThickness;

        for (int x = outerMinX; x <= outerMaxX; x++)
        {
            for (int y = outerMinY; y <= outerMaxY; y++)
            {
                bool insideFloor = x >= minX && x <= maxX && y >= minY && y <= maxY;
                if (insideFloor)
                    continue;

                bool withinWallBand =
                    x >= minX - wallThickness && x <= maxX + wallThickness &&
                    y >= minY - wallThickness && y <= maxY + wallThickness;

                if (withinWallBand)
                    wallTilemap.SetTile(new Vector3Int(x, y, 0), wallTile);
            }
        }
    }

    private IEnumerator WakeEnemiesWhenPlayerIsAvailable()
    {
        float deadline = Time.unscaledTime + playerLookupRetryDuration;

        while (true)
        {
            Transform player = FindPlayerTransform();
            if (player != null)
            {
                WakeConfiguredEnemies(player);
                yield break;
            }

            if (Time.unscaledTime >= deadline)
            {
                Debug.LogWarning("BossArenaScene2D: no se encontró al jugador para activar al jefe.");
                yield break;
            }

            yield return new WaitForSecondsRealtime(playerLookupRetryInterval);
        }
    }

    private Transform FindPlayerTransform()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        return player != null ? player.transform : null;
    }

    private void WakeConfiguredEnemies(Transform player)
    {
        HashSet<EnemyChaser2D> uniqueEnemies = new HashSet<EnemyChaser2D>();

        if (bossEnemy != null)
            uniqueEnemies.Add(bossEnemy);

        foreach (EnemyChaser2D enemy in additionalEnemiesToWake)
        {
            if (enemy != null)
                uniqueEnemies.Add(enemy);
        }

        if (uniqueEnemies.Count == 0 && autoFindEnemiesIfListIsEmpty)
        {
            EnemyChaser2D[] sceneEnemies = FindObjectsByType<EnemyChaser2D>(FindObjectsSortMode.None);
            foreach (EnemyChaser2D enemy in sceneEnemies)
            {
                if (enemy != null)
                    uniqueEnemies.Add(enemy);
            }
        }

        foreach (EnemyChaser2D enemy in uniqueEnemies)
        {
            enemy.WakeUp(player);
        }
    }
}
