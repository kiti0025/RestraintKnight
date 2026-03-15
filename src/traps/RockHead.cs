using Godot;

public partial class RockHead : AnimatableBody2D
{
    [ExportGroup("运动模式设置")]
    [Export] public bool EnableHorizontal = true;
    [Export] public bool EnableVertical = true;
    [Export] public bool RightFirst = true;  // 垂直撞墙：优先往右（false则优先往左）
    [Export] public bool DownFirst = true;   // 水平撞墙：优先往下（false则优先往上）

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
    private CollisionShape2D _rockCollision;
    private double _lastDamageTime;
    private const float DamageCooldown = 0.5f;
    private const string PlayerGroupName = "player";
    private const uint StaticBodyLayer = 1 << 0;

    public override void _Ready()
    {
        _animSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _damageZone = GetNode<Area2D>("DamageZone");
        _rockCollision = GetNode<CollisionShape2D>("CollisionShape2D");

        _animSprite.AnimationFinished += OnAnimationFinished;
        _animSprite.Play("idle");

        InitFirstMoveDirection();
        GD.Print("[RockHead] 初始化完成，初始方向: " + _currentMoveDir);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_currentState == MoveState.HitPause)
        {
            _hitAnimTimer += (float)delta;
            if (_hitAnimTimer >= HitAnimTimeout)
            {
                GD.PrintErr("[RockHead] 动画超时强制恢复");
                ForceRecoverFromHit();
            }
            return;
        }

        float moveDistance = MoveSpeed * (float)delta;
        Vector2 moveDelta = _currentMoveDir * moveDistance;

        bool willHitWall = CheckWillHitWall(moveDelta);
        if (willHitWall)
        {
            GD.Print("[RockHead] 检测到撞墙，触发动画");
            TriggerHitAnimation();
            return;
        }

        GlobalPosition += moveDelta;

        if (_animSprite.Animation == "idle")
        {
            _blinkTimer += (float)delta;
            if (_blinkTimer >= BlinkInterval)
            {
                _blinkTimer = 0f;
                _animSprite.Play("blink");
            }
        }

        if (Time.GetTicksMsec() - _lastDamageTime < DamageCooldown * 1000) return;
        foreach (var body in _damageZone.GetOverlappingBodies())
        {
            if (!body.IsInGroup(PlayerGroupName) || body is not Player player) continue;
            if (IsPlayerSqueezed(player))
            {
                player.TakeDamage(1);
                _lastDamageTime = Time.GetTicksMsec();
                break;
            }
        }
    }

    #region 核心方向逻辑（永不卡死）
    private void InitFirstMoveDirection()
    {
        if (EnableHorizontal && EnableVertical)
        {
            // 双轴模式：先按RightFirst选横向，选完立刻检查能不能走，不能走就反向
            _currentMoveDir = RightFirst ? Vector2.Right : Vector2.Left;
            if (CheckWillHitWall(_currentMoveDir * 10f)) // 预判10像素
            {
                _currentMoveDir = -_currentMoveDir; // 优先方向有墙，直接反向
            }
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

    // 【核心修复】撞墙后选方向：先试优先方向，有墙就自动试反方向
    private void SwitchDirectionByRule()
    {
        bool isCurrentlyHorizontal = Mathf.Abs(_currentMoveDir.X) > Mathf.Abs(_currentMoveDir.Y);

        if (EnableHorizontal && EnableVertical)
        {
            Vector2 preferredDir;
            Vector2 fallbackDir;

            if (isCurrentlyHorizontal)
            {
                // 刚才是横向撞墙 → 切换垂直方向
                preferredDir = DownFirst ? Vector2.Down : Vector2.Up;
                fallbackDir = -preferredDir;
            }
            else
            {
                // 刚才是垂直撞墙 → 切换横向方向
                preferredDir = RightFirst ? Vector2.Right : Vector2.Left;
                fallbackDir = -preferredDir;
            }

            // 【关键】先试优先方向，能走就走，不能走就走反方向
            if (!CheckWillHitWall(preferredDir * 10f))
            {
                _currentMoveDir = preferredDir;
            }
            else
            {
                GD.Print("[RockHead] 优先方向有墙，自动走反方向");
                _currentMoveDir = fallbackDir;
            }
        }
        else if (EnableHorizontal)
        {
            _currentMoveDir = -_currentMoveDir;
        }
        else if (EnableVertical)
        {
            _currentMoveDir = -_currentMoveDir;
        }
        
        GD.Print("[RockHead] 方向已切换为: " + _currentMoveDir);
    }
    #endregion

    #region 碰撞检测
    private bool CheckWillHitWall(Vector2 moveDelta)
    {
        if (_rockCollision == null || _rockCollision.Shape == null) return false;

        var spaceState = GetWorld2D().DirectSpaceState;
        Transform2D targetTransform = _rockCollision.GlobalTransform.Translated(moveDelta);

        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = _rockCollision.Shape,
            Transform = targetTransform,
            CollisionMask = StaticBodyLayer,
            Margin = 1f,
            Exclude = new Godot.Collections.Array<Rid> { GetRid() }
        };

        return spaceState.IntersectShape(query).Count > 0;
    }
    #endregion

    #region 动画控制
    private void TriggerHitAnimation()
    {
        _currentState = MoveState.HitPause;
        _hitAnimTimer = 0f;
        _blinkTimer = 0f;

        if (Mathf.Abs(_currentMoveDir.X) > Mathf.Abs(_currentMoveDir.Y))
            _animSprite.Play("horizon_hit");
        else
            _animSprite.Play("vertical_hit");
    }

    private void OnAnimationFinished()
    {
        StringName finishedAnim = _animSprite.Animation;
        if (finishedAnim == "horizon_hit" || finishedAnim == "vertical_hit")
        {
            SwitchDirectionByRule();
            _currentState = MoveState.Normal;
            _animSprite.Play("idle");
        }
        else if (finishedAnim == "blink" || finishedAnim == "idle")
        {
            if (_currentState == MoveState.Normal)
                _animSprite.Play("idle");
        }
    }

    private void ForceRecoverFromHit()
    {
        SwitchDirectionByRule();
        _currentState = MoveState.Normal;
        _animSprite.Play("idle");
    }
    #endregion

    #region 伤害检测
    private bool IsPlayerSqueezed(Player player)
    {
        var playerCol = player.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (playerCol == null || _rockCollision == null) return false;

        var space = GetWorld2D().DirectSpaceState;

        var staticQuery = new PhysicsShapeQueryParameters2D
        {
            Shape = playerCol.Shape,
            Transform = playerCol.GlobalTransform,
            CollisionMask = StaticBodyLayer,
            Margin = 2f,
            Exclude = new Godot.Collections.Array<Rid> { player.GetRid(), GetRid() }
        };
        bool hitStatic = space.IntersectShape(staticQuery).Count > 0;

        var rockQuery = new PhysicsShapeQueryParameters2D
        {
            Shape = _rockCollision.Shape,
            Transform = _rockCollision.GlobalTransform,
            CollisionMask = 1 << 1,
            Margin = 2f,
            Exclude = new Godot.Collections.Array<Rid> { GetRid() }
        };
        bool hitRock = space.IntersectShape(rockQuery).Count > 0;

        return hitStatic && hitRock;
    }
    #endregion
}