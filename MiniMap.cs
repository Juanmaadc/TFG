using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class DungeonMinimapUI : MonoBehaviour
{
    public DungeonRoomsCorridors2D dungeon;
    public Transform player;

    [Header("Display")]
    [Min(1)] public int pixelsPerCell = 4;
    [Min(1)] public int playerMarkerPixels = 3;
    public bool showWalls = true;

    [Header("Colors")]
    public Color emptyColor = new Color(0f, 0f, 0f, 0.65f);
    public Color floorColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    public Color wallColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    public Color playerColor = Color.red;

    private RawImage rawImage;
    private Texture2D minimapTexture;
    private Color[] basePixels;
    private Color[] currentPixels;
    private Vector2Int lastPlayerCell = new Vector2Int(int.MinValue, int.MinValue);

    void Awake()
    {
        rawImage = GetComponent<RawImage>();
    }

    void OnEnable()
    {
        if (dungeon != null)
            dungeon.OnDungeonGenerated += RebuildMinimap;
    }

    void OnDisable()
    {
        if (dungeon != null)
            dungeon.OnDungeonGenerated -= RebuildMinimap;
    }

    void Start()
    {
        if (dungeon != null && dungeon.HasGeneratedMap)
            RebuildMinimap();
    }

    void LateUpdate()
    {
        if (dungeon == null || player == null || minimapTexture == null)
            return;

        Vector2Int playerCell = dungeon.WorldToMapCell(player.position);
        playerCell.x = Mathf.Clamp(playerCell.x, 0, dungeon.MapWidth - 1);
        playerCell.y = Mathf.Clamp(playerCell.y, 0, dungeon.MapHeight - 1);

        if (playerCell != lastPlayerCell)
            Redraw(playerCell);
    }

    public void RebuildMinimap()
    {
        if (dungeon == null || !dungeon.HasGeneratedMap)
            return;

        int texWidth = dungeon.MapWidth * pixelsPerCell;
        int texHeight = dungeon.MapHeight * pixelsPerCell;

        minimapTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        minimapTexture.filterMode = FilterMode.Point;
        minimapTexture.wrapMode = TextureWrapMode.Clamp;

        basePixels = new Color[texWidth * texHeight];
        currentPixels = new Color[basePixels.Length];

        for (int x = 0; x < dungeon.MapWidth; x++)
        {
            for (int y = 0; y < dungeon.MapHeight; y++)
            {
                Color c = emptyColor;

                if (dungeon.IsFloorCell(x, y))
                    c = floorColor;
                else if (showWalls && dungeon.IsWallCellForMinimap(x, y))
                    c = wallColor;

                PaintCell(basePixels, texWidth, x, y, c);
            }
        }

        rawImage.texture = minimapTexture;
        lastPlayerCell = new Vector2Int(int.MinValue, int.MinValue);

        Vector2Int startCell = player != null
            ? dungeon.WorldToMapCell(player.position)
            : new Vector2Int(0, 0);

        startCell.x = Mathf.Clamp(startCell.x, 0, dungeon.MapWidth - 1);
        startCell.y = Mathf.Clamp(startCell.y, 0, dungeon.MapHeight - 1);

        Redraw(startCell);
    }

    void Redraw(Vector2Int playerCell)
    {
        System.Array.Copy(basePixels, currentPixels, basePixels.Length);

        DrawSquare(
            currentPixels,
            minimapTexture.width,
            minimapTexture.height,
            playerCell,
            playerColor,
            playerMarkerPixels
        );

        minimapTexture.SetPixels(currentPixels);
        minimapTexture.Apply(false);

        lastPlayerCell = playerCell;
    }

    void PaintCell(Color[] pixels, int texWidth, int cellX, int cellY, Color color)
    {
        int startX = cellX * pixelsPerCell;
        int startY = cellY * pixelsPerCell;

        for (int px = 0; px < pixelsPerCell; px++)
        {
            for (int py = 0; py < pixelsPerCell; py++)
            {
                int x = startX + px;
                int y = startY + py;
                pixels[y * texWidth + x] = color;
            }
        }
    }

    void DrawSquare(Color[] pixels, int texWidth, int texHeight, Vector2Int cell, Color color, int sizeInPixels)
    {
        int centerX = cell.x * pixelsPerCell + pixelsPerCell / 2;
        int centerY = cell.y * pixelsPerCell + pixelsPerCell / 2;
        int half = sizeInPixels / 2;

        for (int x = centerX - half; x <= centerX + half; x++)
        {
            for (int y = centerY - half; y <= centerY + half; y++)
            {
                if (x < 0 || x >= texWidth || y < 0 || y >= texHeight)
                    continue;

                pixels[y * texWidth + x] = color;
            }
        }
    }
}