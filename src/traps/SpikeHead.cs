using Godot;

public partial class SpikeHead : AnimatableBody2D
{
	[Export] public float MoveSpeed = 150f;
	[Export] public bool EnableHorizontal = true;
	[Export] public bool EnableVertical = true;
	[Export] public bool RightFirst = true;
	[Export] public bool DownFirst = true;

	// 动画设置
	[Export] public float BlinkInterval = 3f; // 眨眼间隔

	// 内部变量
	private Vector2 _moveDir;
	private CollisionShape2D _bodyCol;
	private AnimatedSprite2D _animSprite;
	
	// 动画状态
	private bool _isPlayingHitAnim = false;
	private float _blinkTimer = 0f;

	public override void _Ready()
	{
		_bodyCol = GetNode<CollisionShape2D>("CollisionShape2D");
		_animSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

		// 连接动画结束信号
		_animSprite.AnimationFinished += OnAnimationFinished;

		// 初始化方向
		if (EnableHorizontal)
			_moveDir = RightFirst ? Vector2.Right : Vector2.Left;
		else if (EnableVertical)
			_moveDir = DownFirst ? Vector2.Down : Vector2.Up;

		// 初始播放待机动画
		_animSprite.Play("idle");
	}

	public override void _PhysicsProcess(double delta)
	{
		// 如果正在播放碰撞动画，暂停移动逻辑
		if (_isPlayingHitAnim)
			return;

		// 眨眼计时
		_blinkTimer += (float)delta;
		if (_blinkTimer >= BlinkInterval && _animSprite.Animation == "idle")
		{
			_blinkTimer = 0f;
			_animSprite.Play("blink");
		}

		// 物理移动
		Vector2 motion = _moveDir * MoveSpeed * (float)delta;
		KinematicCollision2D collision = MoveAndCollide(motion);

		// 撞墙触发动画
		if (collision != null)
		{
			TriggerHitAnimation();
		}
	}

	#region 动画控制
	private void TriggerHitAnimation()
	{
		_isPlayingHitAnim = true;
		_blinkTimer = 0f; // 重置眨眼计时

		// 根据移动方向播放对应碰撞动画
		bool isHorizontal = Mathf.Abs(_moveDir.X) > 0;
		_animSprite.Play(isHorizontal ? "horizon_hit" : "vertical_hit");
	}

	private void OnAnimationFinished()
	{
		StringName anim = _animSprite.Animation;

		// 碰撞动画播完：转向 + 恢复待机
		if (anim == "horizon_hit" || anim == "vertical_hit")
		{
			SwitchDirection();
			_isPlayingHitAnim = false;
			_animSprite.Play("idle");
		}
		// 眨眼动画播完：回到待机
		else if (anim == "blink")
		{
			_animSprite.Play("idle");
		}
	}
	#endregion

	#region 移动逻辑（永动保证）
	private void SwitchDirection()
	{
		// 单轴模式：直接反弹
		if (EnableHorizontal && !EnableVertical)
		{
			_moveDir = -_moveDir;
			return;
		}
		if (EnableVertical && !EnableHorizontal)
		{
			_moveDir = -_moveDir;
			return;
		}

		// 双轴模式：智能切换
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
