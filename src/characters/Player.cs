using Godot;

public enum PlayerState
{
	Jump,
	jump_double,
	Fall,
	Running,
	Rolling,
	Idle,
	WallSlide,
	Hit
}

public partial class Player : CharacterBody2D
{
	#region Inspector Config
	[ExportGroup("Movement")]
	[Export] public float MoveSpeed = 150f;
	[Export] public float Gravity = 950f;

	[ExportGroup("Jump")]
	[Export] public float JumpForce = -280f;

	[ExportGroup("Input")]
	[Export] public float InputDeadzone = 0.2f;

	[ExportGroup("Wall Slide")]
	[Export] public float WallSlideSpeed = 60f; 

	[ExportGroup("Node Refs")]
	[Export] public AnimatedSprite2D PlayerSprite;
	
	[ExportGroup("Health")]
	[Export] public int MaxHealth = 3; 
	[Export] public float InvincibleDuration = 1f;
	#endregion

	#region Runtime State
	private PlayerState _currentState = PlayerState.Idle;
	private Vector2 _velocity;
	private Vector2 _moveInput;
	private bool _isOnFloor;
	private bool _isMoveDownPressed;
	private bool _isMoveDownJustPressed;
	private bool _isHorizontalJustPressed;
	private float _faceDirection = 1f;
	private int _currentJumpCount;
	private const int MaxJumpCount = 2;
	private bool _isOnWall;
	private Vector2 _lastWallJumpNormal;
	private string _currentAnimName;
	private int _currentHealth;
	private bool _isInvincible;
	private float _invincibleTimer;
	#endregion

	#region 事件信号（给音效、UI等外部模块监听）
	[Signal] public delegate void HealthChangedEventHandler(int currentHealth, int maxHealth);
	[Signal] public delegate void OnJumpedEventHandler();
	[Signal] public delegate void OnRollTriggeredEventHandler();
	[Signal] public delegate void OnTakeDamageEventHandler();
	[Signal] public delegate void OnStateChangedEventHandler(PlayerState oldState, PlayerState newState);
	#endregion

	public override void _Ready()
	{
		if (PlayerSprite == null)
			PlayerSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		
		_currentHealth = MaxHealth; 
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
	}

	public override void _PhysicsProcess(double delta)
	{
		float deltaTime = (float)delta;

		// 无敌时间处理
		if (_isInvincible)
		{
			_invincibleTimer -= deltaTime;
			if (_invincibleTimer <= 0)
			{
				_isInvincible = false;
			}
		}

		_isOnFloor = IsOnFloor();
		_isOnWall = IsOnWall();
		_velocity = Velocity;

		HandleMovementInput();
		HandleGravity(deltaTime);
		HandleJumpInput();

		Velocity = _velocity;
		MoveAndSlide();

		UpdateBaseState();
		UpdateAnimation();
	}

	private void HandleGravity(float delta)
	{
		if (!_isOnFloor)
		{
			if (_currentState == PlayerState.WallSlide)
			{
				_velocity.Y =  WallSlideSpeed;
			}
			else
			{
				_velocity.Y += Gravity * delta;
			}
		}
		else
		{
			if (_velocity.Y > 0)
			{
				_velocity.Y = 0;
			}
			_currentJumpCount = 0;
			_lastWallJumpNormal = Vector2.Zero;
		}
	}

	private void HandleMovementInput()
	{
		_isMoveDownPressed = Input.IsActionPressed("move_down");
		_isMoveDownJustPressed = Input.IsActionJustPressed("move_down");
		_isHorizontalJustPressed = Input.IsActionJustPressed("move_left") || Input.IsActionJustPressed("move_right");

		_moveInput.X = Input.GetAxis("move_left", "move_right");

		bool isRolling = _currentState == PlayerState.Rolling && PlayerSprite.IsPlaying();

		if (Mathf.Abs(_moveInput.X) > InputDeadzone)
		{
			_faceDirection = _moveInput.X;
			PlayerSprite.FlipH = _faceDirection < 0;
		}
		
		if (isRolling)
		{
			float speed = PlayerSprite.Frame < 8 ? 2.5f : 0f;
			speed = PlayerSprite.Frame < 3 ? -0.15f : speed; 
			_velocity.X = _faceDirection * MoveSpeed * speed;
		}
		else
		{
			_velocity.X = _moveInput.X * MoveSpeed;
		}
	}

	private void HandleJumpInput()
	{
		if (Input.IsActionJustPressed("jump"))
		{
			if (_currentState == PlayerState.WallSlide)
			{
				// 墙跳逻辑
				_velocity.X = -_faceDirection * 800f; 
				_velocity.Y = JumpForce; 
				_currentJumpCount = MaxJumpCount; 
				PlayerSprite.FlipH = -_faceDirection < 0;
				_lastWallJumpNormal = GetWallNormal();
				
				// 只发事件，不处理音效
				EmitSignal(SignalName.OnJumped);
			}
			else if (_currentJumpCount < MaxJumpCount)
			{
				// 普通跳跃/二段跳
				_velocity.Y = JumpForce;
				_currentJumpCount++;
				
				// 只发事件，不处理音效
				EmitSignal(SignalName.OnJumped);
			}
		}
	}

	private void UpdateBaseState()
	{
		// 受伤硬直期间不更新状态
		if (_currentState == PlayerState.Hit && PlayerSprite.IsPlaying())
			return;

		bool hasHorizontalInput = Mathf.Abs(_moveInput.X) > InputDeadzone;
		bool isRollingTriggered = _currentState == PlayerState.Rolling && PlayerSprite.IsPlaying() 
								|| _isMoveDownJustPressed && hasHorizontalInput 
								|| _isMoveDownPressed && _isHorizontalJustPressed;

		// 记录旧状态，计算新状态
		PlayerState oldState = _currentState;
		PlayerState newState;

		// 状态机核心逻辑
		if (!_isOnFloor) 
		{
			if (Velocity.Y > 0)
			{
				Vector2 currentWallNormal = GetWallNormal();
				bool isSameWallAsLastJump = _lastWallJumpNormal != Vector2.Zero && currentWallNormal == _lastWallJumpNormal;
				bool isPressingTowardsWall = Mathf.Abs(_moveInput.X) > InputDeadzone && (currentWallNormal.X * _moveInput.X < 0);
				newState = (_isOnWall && !isSameWallAsLastJump && isPressingTowardsWall) ? PlayerState.WallSlide : PlayerState.Fall;
			}
			else
			{
				newState = _currentJumpCount == 1 ?  PlayerState.Jump : PlayerState.jump_double;
			}
		}
		else if (isRollingTriggered)
		{
			newState = PlayerState.Rolling;
		}
		else
		{
			newState = hasHorizontalInput ? PlayerState.Running : PlayerState.Idle;
		}

		// 状态变化时，更新状态+发射事件
		if (oldState != newState)
		{
			_currentState = newState;
			EmitSignal(SignalName.OnStateChanged, Variant.From(oldState), Variant.From(newState));

			// 翻滚触发事件
			if (newState == PlayerState.Rolling)
			{
				EmitSignal(SignalName.OnRollTriggered);
			}
		}
	}

	public void TakeDamage(int damage)
	{
		if (_isInvincible || _currentState == PlayerState.Hit) return;

		_currentHealth -= damage;
		_isInvincible = true;
		_invincibleTimer = InvincibleDuration;
		_currentState = PlayerState.Hit;

		// ✅【修复核心】强制立即播放受伤动画
		PlayerSprite.Play("hit");
		_currentAnimName = "hit";


		// 只发事件，不处理音效
		EmitSignal(SignalName.OnTakeDamage);
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);

		if (_currentHealth <= 0)
		{
			HandleDeath();
		}
	}

	private void HandleDeath()
	{
		UiGameOver uiGameOver = GetNodeOrNull<UiGameOver>("/root/Root/PlayerUI/UiGameOver");
		uiGameOver?.ShowGameOver();
	}

	private void UpdateAnimation()
	{
		string targetAnim = _currentState switch
		{
			PlayerState.Hit => "hit", 
			PlayerState.Idle => "idle",
			PlayerState.Running => "running",
			PlayerState.Rolling => "rolling",
			PlayerState.Jump => "jump",
			PlayerState.jump_double => "jump_double",
			PlayerState.Fall => "fall",
			PlayerState.WallSlide => "wall_slide",
			_ => "idle"
		};

		if (_currentAnimName != targetAnim)
		{
			PlayerSprite.Play(targetAnim);
			_currentAnimName = targetAnim;
		}
	}
}