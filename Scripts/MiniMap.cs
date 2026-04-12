using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class DungeonMinimapUI : MonoBehaviour
{
    public DungeonRoomsCorridors2D dungeon;

    [Header("Display")]
    [Min(1)] public int pixelsPerCell = 4;
    public bool showWalls = true;

    [Header("Colors")]
    public Color emptyColor = new Color(0f, 0f, 0f, 0.65f);
    public Color floorColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    public Color wallColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    private RawImage rawImage;
    private Texture2D minimapTexture;
    private Color[] basePixels;

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

        minimapTexture.SetPixels(basePixels);
        minimapTexture.Apply(false);
        rawImage.texture = minimapTexture;
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
}