using Godot;

public partial class SpikeHead : AnimatableBody2D
{
	[Export] public float MoveSpeed = 150f;
	[Export] public bool EnableHorizontal = true;
	[Export] public bool EnableVertical = true;
	[Export] public bool RightFirst = true;
	[Export] public bool DownFirst = true;

	[Export] public float BlinkInterval = 3f;

	// 持续掉血设置
	[Export] public float DamageInterval = 0.4f;   // 每隔多久掉一次血
	[Export] public int DamagePerHit = 1;         // 每次掉多少血

	private Vector2 _moveDir;
	private CollisionShape2D _bodyCol;
	private AnimatedSprite2D _animSprite;
	private Area2D _damageZone;

	private bool _isPlayingHitAnim = false;
	private float _blinkTimer = 0f;
	private float _damageTimer = 0f;  // 持续掉血计时器
	private Player _currentPlayer;    // 正在接触的玩家

	private const string PlayerGroup = "player";

	public override void _Ready()
	{
		_bodyCol = GetNode<CollisionShape2D>("CollisionShape2D");
		_animSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_damageZone = GetNode<Area2D>("DamageZone");

		// 接触开始 / 接触结束
		_damageZone.BodyEntered += OnPlayerEnter;
		_damageZone.BodyExited += OnPlayerExit;

		_animSprite.AnimationFinished += OnAnimationFinished;

		if (EnableHorizontal)
			_moveDir = RightFirst ? Vector2.Right : Vector2.Left;
		else if (EnableVertical)
			_moveDir = DownFirst ? Vector2.Down : Vector2.Up;

		_animSprite.Play("idle");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isPlayingHitAnim)
			return;

		// 眨眼逻辑
		_blinkTimer += (float)delta;
		if (_blinkTimer >= BlinkInterval && _animSprite.Animation == "idle")
		{
			_blinkTimer = 0f;
			_animSprite.Play("blink");
		}

		// 【核心】持续掉血
		if (_currentPlayer != null)
		{
			_damageTimer += (float)delta;
			if (_damageTimer >= DamageInterval)
			{
				_currentPlayer.TakeDamage(DamagePerHit);
				_damageTimer = 0;
			}
		}

		// 移动
		Vector2 motion = _moveDir * MoveSpeed * (float)delta;
		KinematicCollision2D collision = MoveAndCollide(motion);

		if (collision != null)
		{
			TriggerHitAnimation();
		}
	}

	#region 接触开始 / 结束
	private void OnPlayerEnter(Node2D body)
	{
		if (body.IsInGroup(PlayerGroup) && body is Player player)
		{
			_currentPlayer = player;
			_damageTimer = 0; // 刚碰到立刻掉一次血
			_currentPlayer.TakeDamage(DamagePerHit);
		}
	}

	private void OnPlayerExit(Node2D body)
	{
		if (body == _currentPlayer)
		{
			_currentPlayer = null;
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

	#region 移动逻辑
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