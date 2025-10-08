using UnityEngine;

/// <summary>
/// 小地图显示系统（右上角）
/// ⭐ 修复：使用RoomGenerator的坐标转换，正确处理房间偏移
/// </summary>
public class MinimapSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoomGenerator roomGenerator;
    [SerializeField] private RoomMapSystem roomMapSystem;
    [SerializeField] private Transform playerTransform;

    [Header("Minimap Settings")]
    [SerializeField] private float mapScale = 3f;                  // 地图缩放
    [SerializeField] private Vector2 mapOffset = new Vector2(20, 20); // 距离右上角的偏移
    [SerializeField] private Texture2D bossRoomTexture;            // Boss房间显示的图片

    [Header("Colors")]
    [SerializeField] private Color walkableColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
    [SerializeField] private Color unwalkableColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color playerColor = new Color(0, 1, 0, 1);

    private Material lineMaterial;
    private Vector2 mapPosition;  // 地图右上角位置

    void Start()
    {
        if (roomGenerator == null)
        {
            Debug.LogError("MinimapSystem: RoomGenerator reference is missing!");
        }
        if (playerTransform == null)
        {
            Debug.LogError("MinimapSystem: PlayerTransform reference is missing!");
        }
    }

    void OnRenderObject()
    {
        if (roomGenerator == null || roomGenerator.FloorGrid == null) return;

        // 检查是否是Boss房间
        var currentRoom = roomMapSystem?.GetCurrentRoom();
        if (currentRoom != null && currentRoom.type == RoomType.Boss)
        {
            DrawBossRoomIcon();
            return;
        }

        CreateLineMaterial();
        lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.LoadPixelMatrix();

        // 计算地图位置（右上角）
        Vector2 roomSize = roomGenerator.RoomSize;
        float tileSize = roomGenerator.TileSize;
        float mapWidth = roomSize.x * mapScale;
        float mapHeight = roomSize.y * mapScale;

        mapPosition = new Vector2(
            Screen.width - mapWidth - mapOffset.x,
            Screen.height - mapHeight - mapOffset.y
        );

        // 绘制地板网格
        DrawFloorGrid();

        // 绘制玩家位置
        DrawPlayer();

        GL.PopMatrix();
    }

    /// <summary>
    /// 绘制Boss房间图标
    /// </summary>
    void DrawBossRoomIcon()
    {
        if (bossRoomTexture == null) return;

        float iconSize = 200f;
        Vector2 iconPos = new Vector2(
            Screen.width - iconSize - mapOffset.x,
            Screen.height - iconSize - mapOffset.y
        );

        // 使用GUI绘制图片
        GUI.DrawTexture(new Rect(iconPos.x, iconPos.y, iconSize, iconSize), bossRoomTexture);
    }

    /// <summary>
    /// 绘制地板网格
    /// </summary>
    void DrawFloorGrid()
    {
        Floor[,] floorGrid = roomGenerator.FloorGrid;
        Vector2Int roomSize = roomGenerator.RoomSize;

        for (int x = 0; x < roomSize.x; x++)
        {
            for (int y = 0; y < roomSize.y; y++)
            {
                Floor floor = floorGrid[x, y];
                Vector2 pixelPos = GridToScreenPosition(x, y);

                Color tileColor;
                if (floor.type == FloorType.Walkable)
                {
                    tileColor = walkableColor;
                }
                else
                {
                    // 合并两种不可通行类型
                    tileColor = unwalkableColor;
                }

                DrawQuad(pixelPos, Vector2.one * mapScale, tileColor);
            }
        }
    }

    /// <summary>
    /// ⭐ 修复：绘制玩家位置（使用RoomGenerator的坐标转换）
    /// </summary>
    void DrawPlayer()
    {
        if (playerTransform == null || roomGenerator == null) return;

        // ⭐ 使用RoomGenerator的坐标转换（已处理偏移）
        Vector2Int gridPos = roomGenerator.WorldToGridPublic(playerTransform.position);

        // 检查是否在有效范围内
        Vector2Int roomSize = roomGenerator.RoomSize;
        if (gridPos.x < 0 || gridPos.y < 0 || gridPos.x >= roomSize.x || gridPos.y >= roomSize.y)
        {
            // 玩家不在当前房间内，不绘制
            return;
        }

        Vector2 screenPos = GridToScreenPosition(gridPos.x, gridPos.y);

        // 绘制玩家（比格子大一点）
        float playerSize = mapScale * 1.5f;
        DrawQuad(screenPos, Vector2.one * playerSize, playerColor);
    }

    /// <summary>
    /// 网格坐标转屏幕坐标
    /// </summary>
    Vector2 GridToScreenPosition(int gridX, int gridY)
    {
        float x = mapPosition.x + gridX * mapScale;
        float y = mapPosition.y + gridY * mapScale;
        return new Vector2(x, y);
    }

    /// <summary>
    /// 绘制四边形
    /// </summary>
    void DrawQuad(Vector2 center, Vector2 size, Color color)
    {
        float halfW = size.x / 2f;
        float halfH = size.y / 2f;

        GL.Begin(GL.QUADS);
        GL.Color(color);

        GL.Vertex3(center.x - halfW, center.y - halfH, 0);
        GL.Vertex3(center.x + halfW, center.y - halfH, 0);
        GL.Vertex3(center.x + halfW, center.y + halfH, 0);
        GL.Vertex3(center.x - halfW, center.y + halfH, 0);

        GL.End();
    }

    /// <summary>
    /// 创建材质
    /// </summary>
    void CreateLineMaterial()
    {
        if (!lineMaterial)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
        }
    }
}