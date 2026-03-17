using Godot;

public partial class Root : Node2D
{
    [Export] private TileMapLayer _tileMapLayerBorder; 
    [Export] private Camera2D _camera2d;

    // ====================== 新增：BGM配置 ======================
    [ExportGroup("音频配置")]
    [Export] public AudioStream BgmStream;          // BGM音频资源
    [Export] public AudioStreamPlayer2D BgmPlayer;  // 静态BGM播放器节点
    // ==========================================================

    public override void _Ready()
    {
        if (_tileMapLayerBorder == null)
            _tileMapLayerBorder = GetNode<TileMapLayer>("TileMapLayer_border");
        if (_camera2d == null)
            _camera2d = GetNode<Camera2D>("Player/Camera2D");

        // ========== 核心修改：实现 Grow(-0.5) 效果 ==========
        Rect2 usedTileRect = _tileMapLayerBorder.GetUsedRect(); // 获取原始网格范围
        float shrinkValue = 0.5f; // 要收缩的单位（对应 Grow(-0.5)）
        // 1. 位置向右、向下各偏移0.5个网格单位（收缩内侧）
        usedTileRect.Position += new Vector2(shrinkValue, shrinkValue);
        // 2. 尺寸宽、高各减少1个网格单位（2*0.5，对应四周各收缩0.5）
        usedTileRect.Size -= new Vector2(2 * shrinkValue, 2 * shrinkValue);
        // ==================================================

        Vector2 tilePixelSize = _tileMapLayerBorder.TileSet.TileSize; // 单瓦片像素尺寸
        Vector2 layerWorldPos = _tileMapLayerBorder.GlobalPosition; // 图层世界位置（避免偏移）

        float mapLeft = layerWorldPos.X + usedTileRect.Position.X * tilePixelSize.X;
        float mapTop = layerWorldPos.Y + usedTileRect.Position.Y * tilePixelSize.Y;
        float mapRight = layerWorldPos.X + (usedTileRect.End.X) * tilePixelSize.X;
        float mapBottom = layerWorldPos.Y + (usedTileRect.End.Y) * tilePixelSize.Y; 

        // 给相机设置精准边界（强制转int避免像素浮点误差）
        _camera2d.LimitLeft = (int)mapLeft;
        _camera2d.LimitTop = (int)mapTop;
        _camera2d.LimitRight = (int)mapRight;
        _camera2d.LimitBottom = (int)mapBottom;
        _camera2d.ResetSmoothing();

        // ====================== 新增：初始化并播放BGM ======================
        // 自动获取场景中的BGM播放器节点（如果没手动拖拽）
        BgmPlayer ??= GetNodeOrNull<AudioStreamPlayer2D>("BGM");
        
        if (BgmStream != null && BgmPlayer != null)
        {
            BgmPlayer.Stream = BgmStream;
            BgmPlayer.Play();             // 开始播放
        }
        // ==================================================================
    }
}