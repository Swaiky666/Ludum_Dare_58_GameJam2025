using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 房间类型枚举
/// </summary>
public enum RoomType
{
    Normal,   // 普通房间
    Treasure, // 宝箱房间
    Boss      // Boss房间
}

/// <summary>
/// 房间类
/// </summary>
public class Room
{
    public int id;                          // 房间ID
    public RoomType type;                   // 房间类型
    public int column;                      // 所在列
    public int row;                         // 所在行（同一列中的索引）
    public List<Room> connectedRooms;       // 连接的下一列房间
    public Vector2 position;                // 用于绘制的位置

    public Room(int id, RoomType type, int column, int row)
    {
        this.id = id;
        this.type = type;
        this.column = column;
        this.row = row;
        this.connectedRooms = new List<Room>();
    }
}

/// <summary>
/// 房间地图管理系统
/// </summary>
public class RoomMapSystem : MonoBehaviour
{
    [Header("Generation Settings")]
    [SerializeField] private int maxRoomHalf = 4;           // 扩展阶段的列数
    [SerializeField] private float treasureChance = 0.3f;   // 宝箱房间概率

    [Header("Display Settings")]
    [SerializeField] private float mapScale = 1f;           // 整个地图的缩放大小
    [SerializeField] private float roomSize = 40f;          // 房间大小
    [SerializeField] private float columnSpacing = 100f;    // 列间距
    [SerializeField] private float rowSpacing = 80f;        // 行间距
    [SerializeField] private float screenPadding = 20f;     // 屏幕边缘的内边距

    // 数据结构
    private List<List<Room>> roomColumns;   // 按列存储的房间
    private Room currentRoom;               // 玩家当前房间
    private Room bossRoom;                  // Boss房间
    private int roomIdCounter = 0;          // 房间ID计数器
    private Material lineMaterial;          // GL绘制用的材质
    private Vector2 lastScreenSize;         // 记录上一次的屏幕大小

    void Start()
    {
        lastScreenSize = new Vector2(Screen.width, Screen.height);
        GenerateMap();
    }

    /// <summary>
    /// 生成整个地图
    /// </summary>
    void GenerateMap()
    {
        roomColumns = new List<List<Room>>();
        roomIdCounter = 0;

        // 第0列：起始房间
        List<Room> startColumn = new List<Room>();
        Room startRoom = new Room(roomIdCounter++, RoomType.Normal, 0, 0);
        startColumn.Add(startRoom);
        roomColumns.Add(startColumn);
        currentRoom = startRoom; // 玩家从起始房间开始

        // 第1列：生成2个房间
        List<Room> secondColumn = GenerateRooms(1, 2);
        roomColumns.Add(secondColumn);
        ConnectRooms(startColumn, secondColumn);

        int currentColumnCount = 2;
        List<Room> previousColumn = secondColumn;

        // 扩展阶段：每个房间连接1-2个下一列房间
        for (int i = 2; i <= maxRoomHalf; i++)
        {
            int nextRoomCount = CalculateNextRoomCount(previousColumn.Count, true);
            List<Room> newColumn = GenerateRooms(i, nextRoomCount);
            roomColumns.Add(newColumn);
            ConnectRoomsExpanding(previousColumn, newColumn);
            previousColumn = newColumn;
            currentColumnCount++;
        }

        // 收束阶段：多个房间连接到少数房间
        while (previousColumn.Count > 3)
        {
            int nextRoomCount = CalculateNextRoomCount(previousColumn.Count, false);
            List<Room> newColumn = GenerateRooms(currentColumnCount, nextRoomCount);
            roomColumns.Add(newColumn);
            ConnectRoomsConverging(previousColumn, newColumn);
            previousColumn = newColumn;
            currentColumnCount++;
        }

        // 最后：生成Boss房间
        List<Room> bossColumn = new List<Room>();
        bossRoom = new Room(roomIdCounter++, RoomType.Boss, currentColumnCount, 0);
        bossColumn.Add(bossRoom);
        roomColumns.Add(bossColumn);

        // 所有剩余房间连接到Boss
        foreach (Room room in previousColumn)
        {
            room.connectedRooms.Add(bossRoom);
        }

        // 计算所有房间的绘制位置
        CalculateRoomPositions();

        // 验证并修复所有房间的连接
        ValidateAndFixConnections();

        Debug.Log($"地图生成完成！共{currentColumnCount + 1}列，总房间数：{roomIdCounter}");
    }

    /// <summary>
    /// 验证并修复房间连接，确保每个房间都有入口
    /// </summary>
    void ValidateAndFixConnections()
    {
        int fixedCount = 0;

        // 从第2列开始检查（第1列是起始房间，肯定有入口）
        for (int col = 1; col < roomColumns.Count; col++)
        {
            List<Room> currentColumn = roomColumns[col];
            List<Room> previousColumn = roomColumns[col - 1];

            foreach (Room room in currentColumn)
            {
                // 检查是否有任何上一列的房间连接到这个房间
                bool hasIncomingConnection = false;

                foreach (Room prevRoom in previousColumn)
                {
                    if (prevRoom.connectedRooms.Contains(room))
                    {
                        hasIncomingConnection = true;
                        break;
                    }
                }

                // 如果没有入口，随机从上一列选一个房间连接到它
                if (!hasIncomingConnection)
                {
                    Room randomPrevRoom = previousColumn[Random.Range(0, previousColumn.Count)];
                    randomPrevRoom.connectedRooms.Add(room);
                    fixedCount++;
                    Debug.LogWarning($"修复连接：房间{randomPrevRoom.id}(列{col - 1}) -> 房间{room.id}(列{col})");
                }
            }
        }

        if (fixedCount > 0)
        {
            Debug.Log($"<color=yellow>修复了 {fixedCount} 个缺失的房间连接</color>");
        }
        else
        {
            Debug.Log("<color=green>所有房间连接正常✓</color>");
        }
    }

    /// <summary>
    /// 生成指定数量的房间
    /// </summary>
    List<Room> GenerateRooms(int column, int count)
    {
        List<Room> rooms = new List<Room>();
        for (int i = 0; i < count; i++)
        {
            RoomType type = (Random.value < treasureChance) ? RoomType.Treasure : RoomType.Normal;
            Room room = new Room(roomIdCounter++, type, column, i);
            rooms.Add(room);
        }
        return rooms;
    }

    /// <summary>
    /// 计算下一列应该生成的房间数
    /// </summary>
    int CalculateNextRoomCount(int currentCount, bool isExpanding)
    {
        if (isExpanding)
        {
            // 扩展阶段：增加1-2个房间
            return currentCount + Random.Range(1, 3);
        }
        else
        {
            // 收束阶段：根据奇偶性决定
            if (currentCount % 2 == 0)
            {
                return currentCount / 2; // 偶数：减半
            }
            else
            {
                return (currentCount + 1) / 2; // 奇数：向上取整减半
            }
        }
    }

    /// <summary>
    /// 简单连接（用于起始房间）
    /// </summary>
    void ConnectRooms(List<Room> fromColumn, List<Room> toColumn)
    {
        foreach (Room toRoom in toColumn)
        {
            fromColumn[0].connectedRooms.Add(toRoom);
        }
    }

    /// <summary>
    /// 扩展阶段连接：每个房间连接1-2个下一列房间，并确保下一列每个房间都有入口
    /// </summary>
    void ConnectRoomsExpanding(List<Room> fromColumn, List<Room> toColumn)
    {
        // 第一步：每个房间连接1-2个下一列房间
        for (int i = 0; i < fromColumn.Count; i++)
        {
            Room fromRoom = fromColumn[i];
            int connectCount = Random.Range(1, 3); // 连接1或2个房间

            // 计算合理的连接范围
            float ratio = (float)i / Mathf.Max(1, fromColumn.Count - 1);
            int targetIndex = Mathf.RoundToInt(ratio * (toColumn.Count - 1));

            for (int j = 0; j < connectCount; j++)
            {
                int toIndex = Mathf.Clamp(targetIndex + j - (connectCount - 1) / 2, 0, toColumn.Count - 1);
                if (!fromRoom.connectedRooms.Contains(toColumn[toIndex]))
                {
                    fromRoom.connectedRooms.Add(toColumn[toIndex]);
                }
            }

            // 确保至少有一个连接
            if (fromRoom.connectedRooms.Count == 0)
            {
                fromRoom.connectedRooms.Add(toColumn[Mathf.Min(i, toColumn.Count - 1)]);
            }
        }

        // 第二步：检查下一列是否有房间没被连接，如果有就补连接
        foreach (Room toRoom in toColumn)
        {
            bool hasConnection = false;
            foreach (Room fromRoom in fromColumn)
            {
                if (fromRoom.connectedRooms.Contains(toRoom))
                {
                    hasConnection = true;
                    break;
                }
            }

            // 如果没有连接，随机从上一列选一个房间连接过来
            if (!hasConnection)
            {
                Room randomFromRoom = fromColumn[Random.Range(0, fromColumn.Count)];
                randomFromRoom.connectedRooms.Add(toRoom);
                Debug.Log($"扩展阶段补充连接：房间{randomFromRoom.id} -> 房间{toRoom.id}");
            }
        }
    }

    /// <summary>
    /// 收束阶段连接：多个房间连接到一个房间，确保每个房间都有连接
    /// </summary>
    void ConnectRoomsConverging(List<Room> fromColumn, List<Room> toColumn)
    {
        int roomsPerTarget = (fromColumn.Count % 2 == 0) ? 2 : 3;

        for (int i = 0; i < fromColumn.Count; i++)
        {
            int targetIndex = i / roomsPerTarget;
            targetIndex = Mathf.Min(targetIndex, toColumn.Count - 1);
            fromColumn[i].connectedRooms.Add(toColumn[targetIndex]);
        }

        // 验证下一列每个房间都有入口
        foreach (Room toRoom in toColumn)
        {
            bool hasConnection = false;
            foreach (Room fromRoom in fromColumn)
            {
                if (fromRoom.connectedRooms.Contains(toRoom))
                {
                    hasConnection = true;
                    break;
                }
            }

            // 如果没有连接，随机选一个上一列房间连接过来
            if (!hasConnection)
            {
                Room randomFromRoom = fromColumn[Random.Range(0, fromColumn.Count)];
                randomFromRoom.connectedRooms.Add(toRoom);
                Debug.Log($"收束阶段补充连接：房间{randomFromRoom.id} -> 房间{toRoom.id}");
            }
        }
    }

    /// <summary>
    /// 计算所有房间的绘制位置（左上角对齐，适应屏幕分辨率，每列竖直居中）
    /// </summary>
    void CalculateRoomPositions()
    {
        // 左上角起始位置（GL坐标系左下角是原点，所以Y要从屏幕高度减去）
        Vector2 startPosition = new Vector2(
            screenPadding + roomSize * mapScale / 2,
            Screen.height - screenPadding - roomSize * mapScale / 2
        );

        // 找到最大列高度（用于居中参考）
        float maxColumnHeight = 0;
        foreach (var column in roomColumns)
        {
            float columnHeight = (column.Count - 1) * rowSpacing * mapScale;
            if (columnHeight > maxColumnHeight)
            {
                maxColumnHeight = columnHeight;
            }
        }

        for (int col = 0; col < roomColumns.Count; col++)
        {
            List<Room> column = roomColumns[col];

            // 计算这一列的总高度
            float columnHeight = (column.Count - 1) * rowSpacing * mapScale;

            // 计算这一列的起始Y位置（居中）
            float columnStartY = startPosition.y - (maxColumnHeight - columnHeight) / 2;

            for (int row = 0; row < column.Count; row++)
            {
                float x = startPosition.x + col * columnSpacing * mapScale;
                float y = columnStartY - row * rowSpacing * mapScale;
                column[row].position = new Vector2(x, y);
            }
        }
    }

    /// <summary>
    /// 外部调用：移动玩家到指定房间
    /// </summary>
    public void MovePlayerToRoom(int roomId)
    {
        Room targetRoom = FindRoomById(roomId);
        if (targetRoom != null)
        {
            currentRoom = targetRoom;
            Debug.Log($"玩家移动到房间 {roomId} (类型: {targetRoom.type})");
        }
        else
        {
            Debug.LogWarning($"未找到ID为{roomId}的房间！");
        }
    }

    /// <summary>
    /// 外部调用：移动到当前房间连接的某个房间
    /// </summary>
    public void MoveToConnectedRoom(int connectionIndex)
    {
        if (currentRoom == null || currentRoom.connectedRooms.Count == 0)
        {
            Debug.LogWarning("当前房间没有可连接的房间！");
            return;
        }

        if (connectionIndex >= 0 && connectionIndex < currentRoom.connectedRooms.Count)
        {
            currentRoom = currentRoom.connectedRooms[connectionIndex];
            Debug.Log($"玩家移动到房间 {currentRoom.id} (类型: {currentRoom.type})");
        }
        else
        {
            Debug.LogWarning("连接索引超出范围！");
        }
    }

    /// <summary>
    /// 获取当前房间连接的所有房间（供外部调用）
    /// </summary>
    public List<Room> GetCurrentRoomConnections()
    {
        if (currentRoom == null)
            return new List<Room>();

        return new List<Room>(currentRoom.connectedRooms);
    }

    /// <summary>
    /// 获取当前房间信息（供外部调用）
    /// </summary>
    public Room GetCurrentRoom()
    {
        return currentRoom;
    }

    /// <summary>
    /// 根据ID查找房间
    /// </summary>
    Room FindRoomById(int id)
    {
        foreach (var column in roomColumns)
        {
            foreach (var room in column)
            {
                if (room.id == id) return room;
            }
        }
        return null;
    }

    /// <summary>
    /// 获取下一列的所有房间
    /// </summary>
    List<Room> GetNextColumnRooms()
    {
        if (currentRoom == null || currentRoom.column >= roomColumns.Count - 1)
            return new List<Room>();

        return roomColumns[currentRoom.column + 1];
    }

    // ==================== Gizmos绘制（编辑器Scene视图） ====================
    void OnDrawGizmos()
    {
        if (roomColumns == null || roomColumns.Count == 0) return;

        // 绘制所有房间
        foreach (var column in roomColumns)
        {
            foreach (var room in column)
            {
                DrawRoomGizmos(room);
            }
        }

        // 绘制连接线
        foreach (var column in roomColumns)
        {
            foreach (var room in column)
            {
                foreach (var connectedRoom in room.connectedRooms)
                {
                    Gizmos.color = Color.gray;
                    Vector3 start = ScreenToWorld(room.position);
                    Vector3 end = ScreenToWorld(connectedRoom.position);
                    Gizmos.DrawLine(start, end);
                }
            }
        }
    }

    void DrawRoomGizmos(Room room)
    {
        Vector3 worldPos = ScreenToWorld(room.position);
        float size = roomSize * mapScale * 0.01f; // 转换为世界单位并应用缩放

        // Scene视图：显示所有宝箱位置方便debug
        if (room == currentRoom)
        {
            Gizmos.color = Color.green; // 玩家当前位置
        }
        else if (room.type == RoomType.Boss)
        {
            Gizmos.color = Color.red; // Boss房间
        }
        else if (room.type == RoomType.Treasure)
        {
            Gizmos.color = Color.yellow; // 所有宝箱房间都显示为黄色（方便debug）
        }
        else
        {
            Gizmos.color = Color.white; // 普通房间
        }

        Gizmos.DrawCube(worldPos, Vector3.one * size);
    }

    Vector3 ScreenToWorld(Vector2 screenPos)
    {
        // 简单转换，用于Gizmos显示
        return new Vector3(screenPos.x * 0.01f, screenPos.y * 0.01f, 0);
    }

    // ==================== GL绘制（运行时Game视图） ====================
    void OnRenderObject()
    {
        if (roomColumns == null || roomColumns.Count == 0) return;

        // 检测屏幕分辨率变化，重新计算房间位置
        Vector2 currentScreenSize = new Vector2(Screen.width, Screen.height);
        if (currentScreenSize != lastScreenSize)
        {
            lastScreenSize = currentScreenSize;
            CalculateRoomPositions();
        }

        CreateLineMaterial();
        lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.LoadPixelMatrix();

        // 绘制连接线
        DrawConnectionsGL();

        // 绘制所有房间
        foreach (var column in roomColumns)
        {
            foreach (var room in column)
            {
                DrawRoomGL(room);
            }
        }

        GL.PopMatrix();
    }

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

    void DrawConnectionsGL()
    {
        GL.Begin(GL.LINES);
        GL.Color(new Color(0.5f, 0.5f, 0.5f, 0.8f));

        foreach (var column in roomColumns)
        {
            foreach (var room in column)
            {
                foreach (var connectedRoom in room.connectedRooms)
                {
                    GL.Vertex3(room.position.x, room.position.y, 0);
                    GL.Vertex3(connectedRoom.position.x, connectedRoom.position.y, 0);
                }
            }
        }

        GL.End();
    }

    void DrawRoomGL(Room room)
    {
        Color roomColor;

        // Game视图：只显示当前房间、Boss房间和下一列连接的宝箱
        if (room == currentRoom)
        {
            roomColor = new Color(0, 1, 0, 1); // 绿色：玩家位置
        }
        else if (room.type == RoomType.Boss)
        {
            roomColor = new Color(1, 0, 0, 1); // 红色：Boss房间（始终显示）
        }
        else
        {
            // 检查是否是下一列连接的房间
            bool isNextConnectedRoom = currentRoom != null && currentRoom.connectedRooms.Contains(room);

            if (isNextConnectedRoom && room.type == RoomType.Treasure)
            {
                roomColor = new Color(1, 1, 0, 1); // 黄色：下一列连接的宝箱房间
            }
            else
            {
                roomColor = new Color(1, 1, 1, 0.8f); // 白色：所有其他房间（隐藏宝箱信息）
            }
        }

        // 绘制房间方块
        DrawQuadGL(room.position, roomSize * mapScale, roomColor);
    }

    void DrawQuadGL(Vector2 center, float size, Color color)
    {
        float halfSize = size / 2f;

        GL.Begin(GL.QUADS);
        GL.Color(color);

        GL.Vertex3(center.x - halfSize, center.y - halfSize, 0);
        GL.Vertex3(center.x + halfSize, center.y - halfSize, 0);
        GL.Vertex3(center.x + halfSize, center.y + halfSize, 0);
        GL.Vertex3(center.x - halfSize, center.y + halfSize, 0);

        GL.End();

        // 绘制边框
        GL.Begin(GL.LINE_STRIP);
        GL.Color(Color.black);

        GL.Vertex3(center.x - halfSize, center.y - halfSize, 0);
        GL.Vertex3(center.x + halfSize, center.y - halfSize, 0);
        GL.Vertex3(center.x + halfSize, center.y + halfSize, 0);
        GL.Vertex3(center.x - halfSize, center.y + halfSize, 0);
        GL.Vertex3(center.x - halfSize, center.y - halfSize, 0);

        GL.End();
    }

    // ==================== Inspector参数变化时重新计算位置 ====================
    void OnValidate()
    {
        // 当在Inspector中修改参数时，重新计算房间位置
        if (Application.isPlaying && roomColumns != null && roomColumns.Count > 0)
        {
            CalculateRoomPositions();
        }
    }
}