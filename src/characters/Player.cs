using Godot;

// 精简版状态枚举（只保留核心移动状态）
public enum PlayerState
{
    Jump,    // 起跳/上升
    Fall,    // 下落
    Running, // 跑步
    Idle     // 站立
}

public partial class Player : CharacterBody2D
{
    #region 编辑器可配置参数
    [ExportGroup("基础属性")]
    [Export] public float MoveSpeed = 280f;
    [Export] public float Gravity = 1300f;

    [ExportGroup("跳跃")]
    [Export] public float JumpForce = -480f;
    // 二段跳，不需要在编辑器修改
    private const int MaxJumpCount = 2;

    [ExportGroup("节点引用")]
    [Export] public AnimatedSprite2D PlayerSprite;
    #endregion

    #region 内部状态变量
    // 核心状态
    private PlayerState _currentState = PlayerState.Idle;
    private Vector2 _velocity;
    private Vector2 _moveInput;
    private bool _isOnFloor;
    private float _faceDirection = 1f; // 1=右，-1=左

    // 跳跃
    private int _currentJumpCount = 0;

    // 动画
    private string _currentAnimName;
    #endregion

    #region 初始化
    public override void _Ready()
    {
        // 自动获取节点
        if (PlayerSprite == null)
            PlayerSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
    }
    #endregion

    #region 物理帧更新
    public override void _PhysicsProcess(double delta)
    {
        float deltaTime = (float)delta;
        _isOnFloor = IsOnFloor();

        _velocity = Velocity;//同步当前速度，确保下面的函数处理的_velocity是上一帧的物理状态
        // 1. 核心逻辑处理
        HandleMovementInput();
        HandleGravity(deltaTime);
        HandleJumpInput();

        // 2. 执行移动
        Velocity = _velocity;
        MoveAndSlide();

        UpdateBaseState();
        // 3. 更新动画
        UpdateAnimation();
    }
    #endregion

    #region 核心逻辑
    private void HandleGravity(float delta)
    {
        if (!_isOnFloor)
        {
            _velocity.Y += Gravity * delta;
        }
        else
        {
            _velocity.Y = 0;
            _currentJumpCount = 0; // 落地重置跳跃计数
        }
    }

    private void HandleMovementInput()
    {
        _moveInput = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        _velocity.X = _moveInput.X * MoveSpeed;

        // 更新朝向
        if (Mathf.Abs(_moveInput.X) > 0.2f)//死区阈值
        {
            _faceDirection = _moveInput.X;
            PlayerSprite.FlipH = _faceDirection < 0;
        }
    }

    private void HandleJumpInput()
    {
        if (Input.IsActionJustPressed("jump"))
        {
            if (_currentJumpCount < MaxJumpCount)
            {
                _velocity.Y = JumpForce;
                _currentJumpCount++;
            }
        }
    }

    private void UpdateBaseState()
    {
        if (!_isOnFloor)
        {
            // 在空中：根据Y轴速度判断是上升还是下落
            _currentState = Velocity.Y < 0 ? PlayerState.Jump : PlayerState.Fall;//用 Velocity.Y（MoveAndSlide 后的真实值）判断
        }
        else
        {
            // 在地面：根据输入判断是站立还是跑步
            _currentState = Mathf.Abs(_moveInput.X) > 0.2f ? PlayerState.Running : PlayerState.Idle;
        }
    }
    #endregion

    #region 动画更新
    private void UpdateAnimation()
    {
        string targetAnim = _currentState switch
        {
            PlayerState.Idle => "idle",
            PlayerState.Running => "running",
            PlayerState.Jump => "jump",
            PlayerState.Fall => "fall",
            _ => "idle"
        };

        // 只有动画名变化时才触发播放，避免打断当前动画
        if (_currentAnimName != targetAnim)
        {
            PlayerSprite.Play(targetAnim);
            _currentAnimName = targetAnim;
        }
    }
    #endregion
}