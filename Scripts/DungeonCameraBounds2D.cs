using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Generates a PolygonCollider2D that matches the bounds of a procedural dungeon.
/// Use this collider as the Bounding Shape 2D of a Cinemachine Confiner 2D.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(PolygonCollider2D))]
public class DungeonCameraBounds2D : MonoBehaviour
{
    [Header("Dungeon")]
    [SerializeField] private DungeonRoomsCorridors2D dungeon;
    [Tooltip("Optional. If empty, the script uses dungeon.floorTilemap first, then dungeon.wallTilemap.")]
    [SerializeField] private Tilemap boundsTilemapOverride;

    [Header("Bounds")]
    [Tooltip("Recommended ON for procedural maps. Uses dungeon width/height instead of the current Tilemap compressed bounds, avoiding tiny/empty confiner shapes during scene loading.")]
    [SerializeField] private bool useDungeonSizeInsteadOfTilemapBounds = true;
    [Tooltip("Positive values make the camera boundary bigger. Negative values shrink it.")]
    [SerializeField] private float paddingWorldUnits = 0f;
    [SerializeField] private bool rebuildOnStart = true;
    [SerializeField] private bool rebuildWhenDungeonGenerated = true;
    [SerializeField] private bool colliderIsTrigger = true;

    [Header("Late Rebuild")]
    [Tooltip("Rebuild the collider for several frames after scene load. This prevents Cinemachine Confiner 2D from caching an empty/old shape.")]
    [SerializeField] private bool rebuildDuringFirstFrames = true;
    [SerializeField] private int firstFramesToRebuild = 10;

    [Header("Optional Cinemachine Refresh")]
    [Tooltip("Optional. Drag the Cinemachine Confiner 2D component here if you want this script to invalidate its cache automatically.")]
    [SerializeField] private MonoBehaviour cinemachineConfiner2D;
    [SerializeField] private bool invalidateConfinerCacheAfterRebuild = true;
    [Tooltip("If enabled, the script also searches Cinemachine Confiner 2D components in the scene and refreshes them.")]
    [SerializeField] private bool refreshAllCinemachineConfinersInScene = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private PolygonCollider2D polygonCollider;

    public PolygonCollider2D BoundsCollider
    {
        get
        {
            EnsureReferences();
            return polygonCollider;
        }
    }

    private void Reset()
    {
        polygonCollider = GetComponent<PolygonCollider2D>();
        polygonCollider.isTrigger = true;

#if UNITY_2023_1_OR_NEWER
        dungeon = FindFirstObjectByType<DungeonRoomsCorridors2D>();
#else
        dungeon = FindObjectOfType<DungeonRoomsCorridors2D>();
#endif
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        EnsureReferences();
        SubscribeToDungeon();
    }

    private void OnDisable()
    {
        UnsubscribeFromDungeon();
    }

    private IEnumerator Start()
    {
        if (!rebuildOnStart)
            yield break;

        // Wait one frame so DungeonRoomsCorridors2D has time to Generate() in Awake.
        yield return null;
        RebuildBounds();

        if (!rebuildDuringFirstFrames)
            yield break;

        int frames = Mathf.Max(0, firstFramesToRebuild);
        for (int i = 0; i < frames; i++)
        {
            yield return null;
            RebuildBounds();
        }
    }

    private void SubscribeToDungeon()
    {
        if (!rebuildWhenDungeonGenerated || dungeon == null)
            return;

        dungeon.OnDungeonGenerated -= RebuildBounds;
        dungeon.OnDungeonGenerated += RebuildBounds;
    }

    private void UnsubscribeFromDungeon()
    {
        if (dungeon == null)
            return;

        dungeon.OnDungeonGenerated -= RebuildBounds;
    }

    private void EnsureReferences()
    {
        if (polygonCollider == null)
            polygonCollider = GetComponent<PolygonCollider2D>();

        if (polygonCollider != null)
            polygonCollider.isTrigger = colliderIsTrigger;

        if (dungeon == null)
        {
#if UNITY_2023_1_OR_NEWER
            dungeon = FindFirstObjectByType<DungeonRoomsCorridors2D>();
#else
            dungeon = FindObjectOfType<DungeonRoomsCorridors2D>();
#endif
        }
    }

    [ContextMenu("Rebuild Camera Bounds")]
    public void RebuildBounds()
    {
        EnsureReferences();

        if (polygonCollider == null)
        {
            Debug.LogWarning("DungeonCameraBounds2D: no hay PolygonCollider2D.", this);
            return;
        }

        if (!TryGetWorldBounds(out float minX, out float minY, out float maxX, out float maxY, out string sourceName))
        {
            Debug.LogWarning("DungeonCameraBounds2D: no se pudieron calcular los límites de cámara.", this);
            return;
        }

        minX -= paddingWorldUnits;
        minY -= paddingWorldUnits;
        maxX += paddingWorldUnits;
        maxY += paddingWorldUnits;

        if (maxX <= minX || maxY <= minY)
        {
            Debug.LogWarning($"DungeonCameraBounds2D: límites inválidos min=({minX}, {minY}) max=({maxX}, {maxY}).", this);
            return;
        }

        Vector2[] path =
        {
            transform.InverseTransformPoint(new Vector3(minX, minY, 0f)),
            transform.InverseTransformPoint(new Vector3(minX, maxY, 0f)),
            transform.InverseTransformPoint(new Vector3(maxX, maxY, 0f)),
            transform.InverseTransformPoint(new Vector3(maxX, minY, 0f))
        };

        polygonCollider.pathCount = 1;
        polygonCollider.SetPath(0, path);
        polygonCollider.isTrigger = colliderIsTrigger;

        TryRefreshCinemachineConfiners();

        if (debugLogs)
        {
            Debug.Log(
                $"DungeonCameraBounds2D: límites reconstruidos usando {sourceName}. " +
                $"World min=({minX:0.##}, {minY:0.##}) max=({maxX:0.##}, {maxY:0.##}).",
                this
            );
        }
    }

    private bool TryGetWorldBounds(out float minX, out float minY, out float maxX, out float maxY, out string sourceName)
    {
        minX = minY = maxX = maxY = 0f;
        sourceName = "none";

        Tilemap tilemap = GetReferenceTilemap();

        if (useDungeonSizeInsteadOfTilemapBounds && dungeon != null && tilemap != null && dungeon.MapWidth > 0 && dungeon.MapHeight > 0)
        {
            // Must match DungeonRoomsCorridors2D.ToWorldCell(): offsetX = -width / 2, offsetY = -height / 2.
            int minCellX = -dungeon.MapWidth / 2;
            int minCellY = -dungeon.MapHeight / 2;
            int maxCellX = minCellX + dungeon.MapWidth;
            int maxCellY = minCellY + dungeon.MapHeight;

            Vector3 worldA = tilemap.CellToWorld(new Vector3Int(minCellX, minCellY, 0));
            Vector3 worldB = tilemap.CellToWorld(new Vector3Int(maxCellX, maxCellY, 0));

            minX = Mathf.Min(worldA.x, worldB.x);
            maxX = Mathf.Max(worldA.x, worldB.x);
            minY = Mathf.Min(worldA.y, worldB.y);
            maxY = Mathf.Max(worldA.y, worldB.y);
            sourceName = $"dungeon size {dungeon.MapWidth}x{dungeon.MapHeight}";
            return true;
        }

        if (tilemap == null)
            return false;

        BoundsInt cellBounds = tilemap.cellBounds;
        if (cellBounds.size.x <= 0 || cellBounds.size.y <= 0)
        {
            tilemap.CompressBounds();
            cellBounds = tilemap.cellBounds;
        }

        if (cellBounds.size.x <= 0 || cellBounds.size.y <= 0)
            return false;

        Vector3 worldMin = tilemap.CellToWorld(cellBounds.min);
        Vector3 worldMax = tilemap.CellToWorld(cellBounds.max);

        minX = Mathf.Min(worldMin.x, worldMax.x);
        maxX = Mathf.Max(worldMin.x, worldMax.x);
        minY = Mathf.Min(worldMin.y, worldMax.y);
        maxY = Mathf.Max(worldMin.y, worldMax.y);
        sourceName = tilemap.name;
        return true;
    }

    private Tilemap GetReferenceTilemap()
    {
        if (boundsTilemapOverride != null)
            return boundsTilemapOverride;

        if (dungeon == null)
            return null;

        if (dungeon.floorTilemap != null)
            return dungeon.floorTilemap;

        return dungeon.wallTilemap;
    }

    private void TryRefreshCinemachineConfiners()
    {
        if (!invalidateConfinerCacheAfterRebuild)
            return;

        if (cinemachineConfiner2D != null)
            RefreshOneConfiner(cinemachineConfiner2D);

        if (!refreshAllCinemachineConfinersInScene)
            return;

#if UNITY_2023_1_OR_NEWER
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
#else
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>();
#endif
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            if (behaviour == cinemachineConfiner2D)
                continue;

            string typeName = behaviour.GetType().Name;
            if (typeName.Contains("Confiner2D"))
                RefreshOneConfiner(behaviour);
        }
    }

    private void RefreshOneConfiner(MonoBehaviour confiner)
    {
        if (confiner == null)
            return;

        AssignBoundingShapeIfPossible(confiner, polygonCollider);
        InvokeIfExists(confiner, "InvalidateBoundingShapeCache");
        InvokeIfExists(confiner, "InvalidateLensCache");
    }

    private void AssignBoundingShapeIfPossible(MonoBehaviour confiner, Collider2D collider)
    {
        if (confiner == null || collider == null)
            return;

        System.Type type = confiner.GetType();

        PropertyInfo property = type.GetProperty(
            "BoundingShape2D",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (property != null && property.CanWrite && property.PropertyType.IsAssignableFrom(typeof(Collider2D)))
        {
            property.SetValue(confiner, collider);
            return;
        }

        FieldInfo field = type.GetField(
            "m_BoundingShape2D",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (field != null && field.FieldType.IsAssignableFrom(typeof(Collider2D)))
        {
            field.SetValue(confiner, collider);
            return;
        }

        field = type.GetField(
            "BoundingShape2D",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (field != null && field.FieldType.IsAssignableFrom(typeof(Collider2D)))
            field.SetValue(confiner, collider);
    }

    private void InvokeIfExists(MonoBehaviour target, string methodName)
    {
        if (target == null)
            return;

        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (method != null)
            method.Invoke(target, null);
    }
}
