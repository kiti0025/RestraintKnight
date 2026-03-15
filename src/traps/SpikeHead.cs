using Godot;

public partial class SpikeHead : AnimatableBody2D
{
    [ExportGroup("运动模式设置")]
    [Export] public bool EnableHorizontal = true;
    [Export] public bool EnableVertical = true;
    [Export] public bool RightFirst = true;
    [Export] public bool DownFirst = true;

    [ExportGroup("基础设置")]
    [Export] public float MoveSpeed = 120f;
    [Export] public float HitAnimTimeout = 1.0f;

    // 内部状态
    private enum MoveState
    {
        Normal,
        HitPause
    }
    private MoveState _currentState = MoveState.Normal;
    private AnimatedSprite2D _animSprite;
    private float _blinkTimer = 0f;
    private float _hitAnimTimer = 0f;
    private const float BlinkInterval = 3f;
    private Vector2 _currentMoveDir;

    // 碰撞与伤害
    private Area2D _damageZone;
    private CollisionShape2D _bodyCollision;
    private double _lastDamageTime;
    private const float DamageCooldown = 0.5f;
    private const string PlayerGroupName = "player";
    private const uint StaticBodyLayer = 1 << 0;

    public override void _Ready()
    {
        // 节点获取
        _animSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _damageZone = GetNode<Area2D>("DamageZone");
        _bodyCollision = GetNode<CollisionShape2D>("CollisionShape2D");

        // 【修复1：所有信号绑定严格匹配签名】
        _damageZone.BodyEntered += OnDamageZoneHitPlayer;
        _animSprite.AnimationFinished += OnAnimationFinished;

        // 初始化
        _animSprite.Play("idle");
        InitFirstMoveDirection();
        GD.Print("[SpikeHead] 初始化完成，无编译错误");
    }

    public override void _PhysicsProcess(double delta)
    {
        // 动画暂停状态
        if (_currentState == MoveState.HitPause)
        {
            _hitAnimTimer += (float)delta;
            if (_hitAnimTimer >= HitAnimTimeout)
            {
                ForceRecoverFromHit();
            }
            return;
        }

        // 移动与撞墙检测
        float moveDistance = MoveSpeed * (float)delta;
        Vector2 moveDelta = _currentMoveDir * moveDistance;

        bool willHitWall = CheckWillHitStaticWall(moveDelta);
        if (willHitWall)
        {
            TriggerHitAnimation();
            return;
        }

        // 正常移动
        GlobalPosition += moveDelta;

        // 自动眨眼
        if (_animSprite.Animation == "idle")
        {
            _blinkTimer += (float)delta;
            if (_blinkTimer >= BlinkInterval)
            {
                _blinkTimer = 0f;
                _animSprite.Play("blink");
            }
        }
    }

    #region 伤害判定（DamageZone生效）
    /// <summary>
    /// 玩家进入伤害区触发伤害
    /// </summary>
    private void OnDamageZoneHitPlayer(Node2D body)
    {
        // 冷却时间跳过
        if (Time.GetTicksMsec() - _lastDamageTime < DamageCooldown * 1000)
            return;

        // 只处理玩家分组
        if (!body.IsInGroup(PlayerGroupName) || body is not Player player)
            return;

        GD.Print("[SpikeHead] 尖刺对玩家造成伤害");
        player.TakeDamage(1);
        _lastDamageTime = Time.GetTicksMsec();
    }
    #endregion

    #region 移动与方向逻辑
    private void InitFirstMoveDirection()
    {
        if (EnableHorizontal && EnableVertical)
        {
            _currentMoveDir = RightFirst ? Vector2.Right : Vector2.Left;
            if (CheckWillHitStaticWall(_currentMoveDir * 10f))
                _currentMoveDir = -_currentMoveDir;
        }
        else if (EnableHorizontal)
        {
            _currentMoveDir = RightFirst ? Vector2.Right : Vector2.Left;
        }
        else if (EnableVertical)
        {
            _currentMoveDir = DownFirst ? Vector2.Down : Vector2.Up;
        }
    }

    private void SwitchDirectionByRule()
    {
        bool isHorizontalMove = Mathf.Abs(_currentMoveDir.X) > Mathf.Abs(_currentMoveDir.Y);

        if (EnableHorizontal && EnableVertical)
        {
            Vector2 preferredDir;
            Vector2 fallbackDir;

            if (isHorizontalMove)
            {
                // 横向撞墙，切换垂直方向
                preferredDir = DownFirst ? Vector2.Down : Vector2.Up;
            }
            else
            {
                // 垂直撞墙，切换横向方向
                preferredDir = RightFirst ? Vector2.Right : Vector2.Left;
            }
            fallbackDir = -preferredDir;

            // 优先方向能走就走，不能走就反向
            _currentMoveDir = !CheckWillHitStaticWall(preferredDir * 10f) ? preferredDir : fallbackDir;
        }
        else if (EnableHorizontal)
        {
            // 单轴横向往返
            _currentMoveDir = -_currentMoveDir;
        }
        else if (EnableVertical)
        {
            // 单轴垂直往返
            _currentMoveDir = -_currentMoveDir;
        }
    }
    #endregion

    #region 碰撞与动画控制
    /// <summary>
    /// 检测是否会撞到静态墙体
    /// </summary>
    private bool CheckWillHitStaticWall(Vector2 moveDelta)
    {
        if (_bodyCollision == null || _bodyCollision.Shape == null)
            return false;

        var space = GetWorld2D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = _bodyCollision.Shape,
            Transform = _bodyCollision.GlobalTransform.Translated(moveDelta),
            CollisionMask = StaticBodyLayer,
            Exclude = new Godot.Collections.Array<Rid> { GetRid() }
        };

        return space.IntersectShape(query).Count > 0;
    }

    /// <summary>
    /// 触发撞墙动画
    /// </summary>
    private void TriggerHitAnimation()
    {
        _currentState = MoveState.HitPause;
        _hitAnimTimer = 0f;
        _blinkTimer = 0f;

        // 按方向播动画
        if (Mathf.Abs(_currentMoveDir.X) > Mathf.Abs(_currentMoveDir.Y))
            _animSprite.Play("horizon_hit");
        else
            _animSprite.Play("vertical_hit");
    }

    /// <summary>
    /// 【修复2：动画完成回调，无参匹配Action委托】
    /// </summary>
    private void OnAnimationFinished()
    {
        StringName currentAnim = _animSprite.Animation;

        if (currentAnim == "horizon_hit" || currentAnim == "vertical_hit")
        {
            // 撞墙动画结束，切换方向恢复移动
            SwitchDirectionByRule();
            _currentState = MoveState.Normal;
            _animSprite.Play("idle");
        }
        else if (currentAnim == "blink")
        {
            _animSprite.Play("idle");
        }
    }

    /// <summary>
    /// 超时强制恢复
    /// </summary>
    private void ForceRecoverFromHit()
    {
        SwitchDirectionByRule();
        _currentState = MoveState.Normal;
        _animSprite.Play("idle");
    }
    #endregion
}