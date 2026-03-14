using Godot;

public partial class SpikesTilemap : TileMapLayer
{
    [Export] private float _damageInterval = 0.5f;
    private Player _player;
    private float _timer;
    private bool _isInside;
    private PhysicsShapeQueryParameters2D _query;
    private CollisionShape2D _playerCollider;

    public override void _Ready()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (_player == null) return;

        // 预缓存查询对象和玩家碰撞体，避免每帧新建
        _playerCollider = _player.GetNode<CollisionShape2D>("CollisionShape2D");
        _query = new PhysicsShapeQueryParameters2D();
        _query.Shape = _playerCollider.Shape;
        _query.CollisionMask = 1 << 1; // 只检测第2层（尖刺层）
        _query.CollideWithAreas = false;
        _query.CollideWithBodies = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null) return;

        // 每帧只更新Transform，复用查询对象
        _query.Transform = _player.GlobalTransform;
        bool isTouchingSpike = _player.GetWorld2D().DirectSpaceState.IntersectShape(_query).Count > 0;

        // 状态切换
        if (isTouchingSpike && !_isInside)
            _isInside = true;
        else if (!isTouchingSpike && _isInside)
        {
            _isInside = false;
            _timer = _damageInterval;
        }

        // 扣血逻辑
        if (_isInside)
        {
            _timer += (float)delta;
            if (_timer >= _damageInterval)
            {
                _player.TakeDamage(1);
                _timer = 0;
            }
        }
    }
}