using Godot;

public partial class Root : Node2D
{
    [Export] private TileMapLayer _tileMapLayer;
    [Export] private Camera2D _camera2d;

    public override void _Ready()
    {
        // 如果没有手动绑定，自动从场景中获取
        if (_tileMapLayer == null)
            _tileMapLayer = GetNode<TileMapLayer>("TileMapLayer");
        if (_camera2d == null)
            _camera2d = GetNode<Camera2D>("Player/Camera2D");

        // 获取 TileMap 使用的矩形区域
        Rect2 used = _tileMapLayer.GetUsedRect().Grow(-1); // 缩小1个单元以确保边界正确
        Vector2 tileSize = _tileMapLayer.TileSet.TileSize;

        // 设置相机边界
        _camera2d.LimitTop = (int)(used.Position.Y * tileSize.Y);
        _camera2d.LimitRight = (int)(used.End.X * tileSize.X);
        _camera2d.LimitBottom = (int)(used.End.Y * tileSize.Y);
        _camera2d.LimitLeft = (int)(used.Position.X * tileSize.X);

        // 重置相机平滑
        _camera2d.ResetSmoothing();
    }
}