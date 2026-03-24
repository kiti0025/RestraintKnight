using Godot;

public partial class FireAttack : Node2D
{
    // --------------------------
    // 【可配置】在编辑器里直接调，不用改代码
    // --------------------------
    [ExportGroup("特效设置")]
    [Export] public float MoveSpeed = 800f; // 特效向前飞的速度
    [Export] public Vector2 OffsetFromPlayer = new Vector2(20, 0); // 相对于Player的生成位置偏移
	[Export] public AnimatedSprite2D _animSprite;

    // --------------------------
    // 内部变量
    // -----------------------
    // 移动方向（1=向右，-1=向左，由Player传入）
    private float _moveDirection = 1f;

    public override void _Ready()
    {
        // 自动获取子节点AnimatedSprite2D，不用在编辑器手动拖
        _animSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        
        // 自动播放动画
        _animSprite.Play("default");
        // 【专业必备】动画播完自动销毁，避免内存泄漏
        _animSprite.AnimationFinished += QueueFree;
    }

    // --------------------------
    // 【Player调用的唯一接口】初始化特效
    // --------------------------
    public void Init(Vector2 playerGlobalPos, float faceDirection)
    {
        _moveDirection = faceDirection;

        // 1. 设置特效位置：Player位置 + 偏移 + 朝向修正
        GlobalPosition = playerGlobalPos + new Vector2(faceDirection * OffsetFromPlayer.X, OffsetFromPlayer.Y);
        
        // 2. 翻转特效朝向（如果朝左）
        if (faceDirection < 0)
        {
            Scale = new Vector2(-1, 1);
        }
    }

    public override void _Process(double delta)
    {
        // 特效自己管移动，Player完全不用管
        Position += new Vector2(_moveDirection * MoveSpeed * (float)delta, 0);
    }
}