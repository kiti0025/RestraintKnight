using Godot;

public partial class RockHead : AnimatableBody2D
{
	[Export] public float MoveSpeed = 150f;
	[Export] public bool EnableHorizontal = true;
	[Export] public bool EnableVertical = true;
	[Export] public bool RightFirst = true;
	[Export] public bool DownFirst = true;

	// 动画设置
	[Export] public float BlinkInterval = 3f;

	// 内部变量
	private Vector2 _moveDir;
	private CollisionShape2D _bodyCol;
	private AnimatedSprite2D _animSprite;
	private Area2D _damageZone; // 只需要加一个Area2D检测玩家
	
	// 动画状态
	private bool _isPlayingHitAnim = false;
	private float _blinkTimer = 0f;
	private float _damageCooldown = 0f;
	private const float DamageCooldownTime = 0.5f;
	private const string PlayerGroup = "player";

	public override void _Ready()
	{
		_bodyCol = GetNode<CollisionShape2D>("CollisionShape2D");
		_animSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_damageZone = GetNode<Area2D>("DamageZone"); // 获取伤害区

		_animSprite.AnimationFinished += OnAnimationFinished;

		if (EnableHorizontal)
			_moveDir = RightFirst ? Vector2.Right : Vector2.Left;
		else if (EnableVertical)
			_moveDir = DownFirst ? Vector2.Down : Vector2.Up;

		_animSprite.Play("idle");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_damageCooldown > 0) _damageCooldown -= (float)delta;
		if (_isPlayingHitAnim) return;

		_blinkTimer += (float)delta;
		if (_blinkTimer >= BlinkInterval && _animSprite.Animation == "idle")
		{
			_blinkTimer = 0f;
			_animSprite.Play("blink");
		}

		Vector2 motion = _moveDir * MoveSpeed * (float)delta;
		KinematicCollision2D collision = MoveAndCollide(motion);

		// 【核心逻辑：完全按照你的思路】
		// 如果 collision != null，说明箱子撞墙停下了
		if (collision != null)
		{
			// 撞墙瞬间，检查伤害区内有没有玩家
			CheckDamageOnHitWall();
			TriggerHitAnimation();
		}
	}

	#region 【修复版】加相对位置检查，彻底排除站在箱子顶上的情况
	private void CheckDamageOnHitWall()
	{
		if (_damageCooldown > 0 || _damageZone == null) return;

		foreach (Node2D body in _damageZone.GetOverlappingBodies())
		{
			if (!body.IsInGroup(PlayerGroup) || body is not Player player) continue;

			// 【关键修复】检查玩家是否真的在箱子的移动方向上
			if (!IsPlayerInThePushingDirection(player)) continue;

			player.TakeDamage(1);
			_damageCooldown = DamageCooldownTime;
			GD.Print("[RockHead] 确认玩家被挤压，造成伤害");
			break;
		}
	}

	/// <summary>
	/// 极简判定：玩家是否在箱子的移动方向上（被推的那一侧）
	/// </summary>
	private bool IsPlayerInThePushingDirection(Player player)
	{
		var playerCol = player.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (playerCol?.Shape == null || _bodyCol?.Shape == null) return false;

		// 获取箱子和玩家的世界边界
		Rect2 boxBounds = _bodyCol.Shape.GetRect();
		Rect2 playerBounds = playerCol.Shape.GetRect();
		boxBounds.Position = _bodyCol.GlobalPosition - boxBounds.Size / 2;
		playerBounds.Position = playerCol.GlobalPosition - playerBounds.Size / 2;

		bool isHorizontal = Mathf.Abs(_moveDir.X) > 0;

		if (isHorizontal)
		{
			// 水平撞墙：玩家和箱子的Y轴必须有重叠（说明在左右侧，不是在上下方）
			return boxBounds.Intersects(playerBounds);
		}
		else
		{
			// 垂直撞墙：玩家和箱子的X轴必须有重叠（说明在上下侧，不是在左右侧）
			return boxBounds.Intersects(playerBounds);
		}
	}
	#endregion

	#region 动画控制
	private void TriggerHitAnimation()
	{
		_isPlayingHitAnim = true;
		_blinkTimer = 0f;
		bool isHorizontal = Mathf.Abs(_moveDir.X) > 0;
		_animSprite.Play(isHorizontal ? "horizon_hit" : "vertical_hit");
	}

	private void OnAnimationFinished()
	{
		StringName anim = _animSprite.Animation;
		if (anim == "horizon_hit" || anim == "vertical_hit")
		{
			SwitchDirection();
			_isPlayingHitAnim = false;
			_animSprite.Play("idle");
		}
		else if (anim == "blink")
		{
			_animSprite.Play("idle");
		}
	}
	#endregion

	#region 移动逻辑（永动保证）
	private void SwitchDirection()
	{
		if (EnableHorizontal && !EnableVertical) { _moveDir = -_moveDir; return; }
		if (EnableVertical && !EnableHorizontal) { _moveDir = -_moveDir; return; }

		bool isHorizontal = Mathf.Abs(_moveDir.X) > 0;
		Vector2 preferredDir = isHorizontal ? (DownFirst ? Vector2.Down : Vector2.Up) : (RightFirst ? Vector2.Right : Vector2.Left);
		Vector2 fallbackDir = -preferredDir;
		_moveDir = !CheckWillHitWall(preferredDir * 10f) ? preferredDir : fallbackDir;
	}

	private bool CheckWillHitWall(Vector2 delta)
	{
		if (_bodyCol?.Shape == null) return false;
		var space = GetWorld2D().DirectSpaceState;
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = _bodyCol.Shape,
			Transform = _bodyCol.GlobalTransform.Translated(delta),
			CollisionMask = 1 << 0,
			Exclude = new Godot.Collections.Array<Rid> { GetRid() }
		};
		return space.IntersectShape(query).Count > 0;
	}
	#endregion
}
