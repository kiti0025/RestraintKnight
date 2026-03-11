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
	[Export] public float InvincibleDuration = 1f; // 受伤后无敌时间（秒）
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
	private Vector2 _lastWallJumpNormal; // 记录最近一次蹬墙的墙法线
	private string _currentAnimName;
	private int _currentHealth;
	private bool _isInvincible;
	private float _invincibleTimer;
	#endregion

	[Signal]
    public delegate void HealthChangedEventHandler(int currentHealth, int maxHealth);


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
			_velocity.Y = 0;
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
			float speed = PlayerSprite.Frame == 8 ? 8f : 0f;
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
				_velocity.X = -_faceDirection * 800f; 
				_velocity.Y = JumpForce; 
				_currentJumpCount = MaxJumpCount; 
				PlayerSprite.FlipH = -_faceDirection < 0;
				_lastWallJumpNormal = GetWallNormal();
			}
			else if (_currentJumpCount < MaxJumpCount)
			{
				_velocity.Y = JumpForce;
				_currentJumpCount++;
			}
		}
	}

	private void UpdateBaseState()
	{
		if (_currentState == PlayerState.Hit && PlayerSprite.IsPlaying())
            return;

		bool hasHorizontalInput = Mathf.Abs(_moveInput.X) > InputDeadzone;
		bool isRollingTriggered = _currentState == PlayerState.Rolling && PlayerSprite.IsPlaying() 
								|| _isMoveDownJustPressed && hasHorizontalInput 
								|| _isMoveDownPressed && _isHorizontalJustPressed;

		if (!_isOnFloor) 
		{
			if (Velocity.Y > 0)
			{
				Vector2 currentWallNormal = GetWallNormal();
				bool isSameWallAsLastJump = _lastWallJumpNormal != Vector2.Zero && currentWallNormal == _lastWallJumpNormal;
				_currentState = (_isOnWall && !isSameWallAsLastJump) ? PlayerState.WallSlide : PlayerState.Fall;
			}
			else
			{
				_currentState = _currentJumpCount == 1 ?  PlayerState.Jump : PlayerState.jump_double;
			}
		}
		else if (isRollingTriggered)
		{
			_currentState = PlayerState.Rolling;
		}
		else
		{
			_currentState = hasHorizontalInput ? PlayerState.Running : PlayerState.Idle;
		}
	}

	public void TakeDamage(int damage)
	{
		if (_isInvincible || _currentState == PlayerState.Hit) return;

		_currentHealth -= damage;
		_isInvincible = true;
		_invincibleTimer = InvincibleDuration;
		_currentState = PlayerState.Hit;

		GD.Print($"Hurt！: {_currentHealth}");
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);

		if (_currentHealth <= 0)
		{
			HandleDeath();
		}
	}

	private void HandleDeath()
	{
		GD.Print("Game Over!");
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
