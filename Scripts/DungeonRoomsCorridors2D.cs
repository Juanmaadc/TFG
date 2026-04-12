using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DungeonRoomsCorridors2D : MonoBehaviour
{
    [Header("Tilemaps")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;

    [Header("Tiles")]
    public TileBase floorTile;
    public TileBase wallTile;

    [Header("Dungeon Size")]
    public int width = 120;
    public int height = 70;

    [Header("Rooms")]
    public int roomCount = 18;
    public Vector2Int roomSizeMin = new Vector2Int(6, 6);
    public Vector2Int roomSizeMax = new Vector2Int(16, 14);
    public int roomPadding = 2;

    [Header("Guaranteed Origin Room")]
    public bool guaranteePlayableOrigin = true;
    public Vector2Int guaranteedOriginRoomSize = new Vector2Int(7, 7);

    [Header("Corridors")]
    public int corridorWidth = 2;
    public bool randomCorridorOrder = true;

    [Header("Seed")]
    public int seed = 0;
    public bool useRandomSeedEachRun = true;

    [Header("Validation")]
    [Min(1)] public int minRoomsRequired = 2;
    [Min(1)] public int maxGenerationAttempts = 25;

    private int[,] map;
    private readonly List<RectInt> rooms = new();

    private int offsetX, offsetY;

    private RectInt originRoom;
    private bool hasOriginRoom;

    public System.Action OnDungeonGenerated;

    public bool HasGeneratedMap => map != null && rooms.Count > 0 && CountFloorTiles() > 0;
    public int MapWidth => width;
    public int MapHeight => height;

    public IReadOnlyList<RectInt> Rooms => rooms;
    public RectInt OriginRoom => originRoom;
    public bool HasOriginRoom => hasOriginRoom;

    void Awake()
    {
        Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        bool valid = false;
        int usedSeed = seed;

        offsetX = -width / 2;
        offsetY = -height / 2;

        for (int attempt = 0; attempt < maxGenerationAttempts; attempt++)
        {
            usedSeed = GetSeedForAttempt(attempt);

            Random.InitState(usedSeed);

            map = new int[width, height];
            rooms.Clear();
            hasOriginRoom = false;
            originRoom = default;

            GenerateRooms();
            ConnectRooms();

            if (IsDungeonValid())
            {
                valid = true;
                seed = usedSeed;
                break;
            }
        }

        PaintTilemaps();

        if (!valid)
        {
            Debug.LogWarning(
                $"DungeonRoomsCorridors2D: no se pudo generar un mapa 100% válido tras {maxGenerationAttempts} intentos. " +
                $"Última seed usada: {usedSeed}. Revisa roomCount, roomSizeMin/Max, width, height y roomPadding."
            );
            seed = usedSeed;
        }

        OnDungeonGenerated?.Invoke();
    }

    int GetSeedForAttempt(int attempt)
    {
        if (useRandomSeedEachRun || seed == 0)
        {
            return Random.Range(int.MinValue, int.MaxValue);
        }

        unchecked
        {
            return seed + attempt * 9973;
        }
    }

    bool IsDungeonValid()
    {
        if (rooms.Count < minRoomsRequired)
            return false;

        if (CountFloorTiles() == 0)
            return false;

        return AllFloorIsReachable();
    }

    void GenerateRooms()
    {
        if (guaranteePlayableOrigin)
        {
            CreateGuaranteedOriginRoom();
        }

        int tries = 0;
        int maxTries = roomCount * 50;

        while (rooms.Count < roomCount && tries < maxTries)
        {
            tries++;

            int rw = Random.Range(roomSizeMin.x, roomSizeMax.x + 1);
            int rh = Random.Range(roomSizeMin.y, roomSizeMax.y + 1);

            if (rw >= width - 2 || rh >= height - 2)
                continue;

            int rx = Random.Range(1, width - rw - 1);
            int ry = Random.Range(1, height - rh - 1);

            var room = new RectInt(rx, ry, rw, rh);

            var padded = new RectInt(
                room.xMin - roomPadding,
                room.yMin - roomPadding,
                room.width + roomPadding * 2,
                room.height + roomPadding * 2
            );

            bool overlaps = false;
            foreach (var r in rooms)
            {
                if (padded.Overlaps(r))
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps) continue;

            rooms.Add(room);
            CarveRoom(room);
        }
    }

    void CreateGuaranteedOriginRoom()
    {
        int centerX = -offsetX;
        int centerY = -offsetY;

        int rw = Mathf.Clamp(guaranteedOriginRoomSize.x, 3, width - 2);
        int rh = Mathf.Clamp(guaranteedOriginRoomSize.y, 3, height - 2);

        int rx = centerX - rw / 2;
        int ry = centerY - rh / 2;

        rx = Mathf.Clamp(rx, 1, width - rw - 1);
        ry = Mathf.Clamp(ry, 1, height - rh - 1);

        originRoom = new RectInt(rx, ry, rw, rh);
        hasOriginRoom = true;

        rooms.Add(originRoom);
        CarveRoom(originRoom);
    }

    void CarveRoom(RectInt room)
    {
        for (int x = room.xMin; x < room.xMax; x++)
        for (int y = room.yMin; y < room.yMax; y++)
            map[x, y] = 1;
    }

    Vector2Int RoomCenter(RectInt r)
    {
        return new Vector2Int(r.xMin + r.width / 2, r.yMin + r.height / 2);
    }

    void ConnectRooms()
    {
        if (rooms.Count <= 1) return;

        rooms.Sort((a, b) => RoomCenter(a).x.CompareTo(RoomCenter(b).x));

        for (int i = 0; i < rooms.Count - 1; i++)
        {
            Vector2Int a = RoomCenter(rooms[i]);
            Vector2Int b = RoomCenter(rooms[i + 1]);
            CarveCorridorL(a, b);
        }

        int extraLinks = Mathf.Max(1, rooms.Count / 6);
        for (int k = 0; k < extraLinks; k++)
        {
            var r1 = rooms[Random.Range(0, rooms.Count)];
            var r2 = rooms[Random.Range(0, rooms.Count)];
            if (r1 == r2) continue;

            CarveCorridorL(RoomCenter(r1), RoomCenter(r2));
        }
    }

    void CarveCorridorL(Vector2Int a, Vector2Int b)
    {
        bool verticalFirst = randomCorridorOrder ? (Random.value > 0.5f) : false;

        if (verticalFirst)
        {
            CarveVertical(a.x, a.y, b.y);
            CarveHorizontal(a.y, a.x, b.x);
        }
        else
        {
            CarveHorizontal(a.y, a.x, b.x);
            CarveVertical(b.x, a.y, b.y);
        }
    }

    void CarveHorizontal(int y, int x1, int x2)
    {
        if (x2 < x1) (x1, x2) = (x2, x1);

        int half = corridorWidth / 2;
        for (int x = x1; x <= x2; x++)
        for (int w = -half; w < corridorWidth - half; w++)
        {
            int yy = y + w;
            if (InBounds(x, yy)) map[x, yy] = 1;
        }
    }

    void CarveVertical(int x, int y1, int y2)
    {
        if (y2 < y1) (y1, y2) = (y2, y1);

        int half = corridorWidth / 2;
        for (int y = y1; y <= y2; y++)
        for (int w = -half; w < corridorWidth - half; w++)
        {
            int xx = x + w;
            if (InBounds(xx, y)) map[xx, y] = 1;
        }
    }

    bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    int CountFloorTiles()
    {
        if (map == null) return 0;

        int count = 0;
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (map[x, y] == 1)
                count++;

        return count;
    }

    bool AllFloorIsReachable()
    {
        if (!TryGetAnyFloorCell(out Vector2Int start))
            return false;

        bool[,] visited = new bool[width, height];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        visited[start.x, start.y] = true;

        int reachableCount = 0;

        Vector2Int[] dirs = new Vector2Int[]
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            reachableCount++;

            foreach (var dir in dirs)
            {
                int nx = current.x + dir.x;
                int ny = current.y + dir.y;

                if (!InBounds(nx, ny)) continue;
                if (visited[nx, ny]) continue;
                if (map[nx, ny] != 1) continue;

                visited[nx, ny] = true;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        return reachableCount == CountFloorTiles();
    }

    bool TryGetAnyFloorCell(out Vector2Int cell)
    {
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (map[x, y] == 1)
            {
                cell = new Vector2Int(x, y);
                return true;
            }
        }

        cell = default;
        return false;
    }

    void PaintTilemaps()
    {
        if (floorTilemap == null || wallTilemap == null)
        {
            Debug.LogError("DungeonRoomsCorridors2D: faltan referencias a floorTilemap o wallTilemap.");
            return;
        }

        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (map[x, y] == 1)
                floorTilemap.SetTile(ToWorldCell(x, y), floorTile);
        }

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (map[x, y] != 1 && HasNeighborFloor(x, y))
                wallTilemap.SetTile(ToWorldCell(x, y), wallTile);
        }

        for (int x = 0; x < width; x++)
        {
            wallTilemap.SetTile(ToWorldCell(x, 0), wallTile);
            wallTilemap.SetTile(ToWorldCell(x, height - 1), wallTile);
        }

        for (int y = 0; y < height; y++)
        {
            wallTilemap.SetTile(ToWorldCell(0, y), wallTile);
            wallTilemap.SetTile(ToWorldCell(width - 1, y), wallTile);
        }
    }

    bool HasNeighborFloor(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;

            int nx = x + dx;
            int ny = y + dy;

            if (!InBounds(nx, ny)) continue;
            if (map[nx, ny] == 1) return true;
        }

        return false;
    }

    Vector3Int ToWorldCell(int x, int y)
    {
        return new Vector3Int(x + offsetX, y + offsetY, 0);
    }

    public bool IsFloorCell(int x, int y)
    {
        return map != null && InBounds(x, y) && map[x, y] == 1;
    }

    public bool IsWallCellForMinimap(int x, int y)
    {
        if (map == null || !InBounds(x, y) || map[x, y] == 1)
            return false;

        if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
            return true;

        return HasNeighborFloor(x, y);
    }

    public Vector2Int WorldToMapCell(Vector3 worldPosition)
    {
        Vector3Int cell = floorTilemap.WorldToCell(worldPosition);
        return new Vector2Int(cell.x - offsetX, cell.y - offsetY);
    }

    public bool TryGetSpawnWorldPositionInFirstRoom(out Vector3 worldPos)
    {
        return TryGetSpawnWorldPositionByRoomIndex(0, out worldPos);
    }

    public bool TryGetSpawnWorldPositionInLastRoom(out Vector3 worldPos)
    {
        return TryGetSpawnWorldPositionByRoomIndex(rooms.Count - 1, out worldPos);
    }

    bool TryGetSpawnWorldPositionByRoomIndex(int roomIndex, out Vector3 worldPos)
    {
        if (rooms.Count == 0 || roomIndex < 0 || roomIndex >= rooms.Count)
            return TryGetAnyFloorWorldPosition(out worldPos);

        Vector2Int cell = RoomCenter(rooms[roomIndex]);

        if (!InBounds(cell.x, cell.y) || map[cell.x, cell.y] != 1)
            return TryGetAnyFloorWorldPosition(out worldPos);

        worldPos = floorTilemap.GetCellCenterWorld(ToWorldCell(cell.x, cell.y));
        return true;
    }

    public bool TryGetSpawnWorldPositionInOriginRoom(out Vector3 worldPos)
    {
        if (hasOriginRoom)
        {
            Vector2Int cell = RoomCenter(originRoom);
            worldPos = floorTilemap.GetCellCenterWorld(ToWorldCell(cell.x, cell.y));
            return true;
        }

        return TryGetAnyFloorWorldPosition(out worldPos);
    }

    public bool TryGetAnyFloorWorldPosition(out Vector3 worldPos)
    {
        if (TryGetAnyFloorCell(out Vector2Int cell))
        {
            worldPos = floorTilemap.GetCellCenterWorld(ToWorldCell(cell.x, cell.y));
            return true;
        }

        worldPos = Vector3.zero;
        return false;
    }

    public Vector3 GetRoomTriggerCenterWorld(RectInt room)
    {
        Vector3 a = floorTilemap.GetCellCenterWorld(ToWorldCell(room.xMin, room.yMin));
        Vector3 b = floorTilemap.GetCellCenterWorld(ToWorldCell(room.xMax - 1, room.yMax - 1));
        return (a + b) * 0.5f;
    }

    public Vector2 GetRoomSizeWorld(RectInt room)
    {
        Vector3 cellSize = floorTilemap.layoutGrid.cellSize;
        return new Vector2(room.width * cellSize.x, room.height * cellSize.y);
    }

    public Vector3 GetRandomWorldPositionInRoom(RectInt room, int margin = 1)
    {
        int minX = room.xMin + margin;
        int maxX = room.xMax - 1 - margin;
        int minY = room.yMin + margin;
        int maxY = room.yMax - 1 - margin;

        if (minX > maxX)
        {
            minX = room.xMin;
            maxX = room.xMax - 1;
        }

        if (minY > maxY)
        {
            minY = room.yMin;
            maxY = room.yMax - 1;
        }

        int x = Random.Range(minX, maxX + 1);
        int y = Random.Range(minY, maxY + 1);

        return floorTilemap.GetCellCenterWorld(ToWorldCell(x, y));
    }
}