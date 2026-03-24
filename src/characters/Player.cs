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
	Hit,
	// 新增：攻击/防御状态
	Attack1,
	Attack2,
	Attack3,
	Attack4,
	Attack5,
	Attack6,
	Defend,
	Walk_defend
}

public partial class Player : CharacterBody2D
{
	#region Inspector Config
	// ... 保持原有配置不变 ...
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

	[ExportGroup("Skill Effects")]
	[Export] public PackedScene RollingFireEffect; 
	#endregion

	#region Runtime State
	// ... 保持原有变量不变 ...
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

	// 新增：攻击/防御相关变量
	private int _comboCount = 0; // 连击计数 (1-3)
	private float _comboTimer = 0f; // 连击时间窗口计时器
	private const float ComboWindow = 1.5f; // 连击时间窗口0.5秒
	private float _attackHoldTimer = 0f; // 长按attack计时器
	private const float HoldThreshold = 0.3f; // 长按判定阈值

	// 输入状态缓存
	private bool _isAttackJustPressed;
	private bool _isAttackJustReleased;
	private bool _isAttackPressed;
	private bool _isDefendPressed;
	// 新增：确保一次按键只触发一种攻击
	private bool _hasTriggeredAnyAttack = false;
	#endregion

	#region 事件信号
	// ... 保持原有信号不变 ...
	[Signal] public delegate void HealthChangedEventHandler(int currentHealth, int maxHealth);
	[Signal] public delegate void OnJumpedEventHandler();
	[Signal] public delegate void OnRollTriggeredEventHandler();
	[Signal] public delegate void OnTakeDamageEventHandler();
	[Signal] public delegate void OnStateChangedEventHandler(PlayerState oldState, PlayerState newState);
	// 可选：新增攻击信号
	// [Signal] public delegate void OnAttackTriggeredEventHandler(PlayerState attackState);
	#endregion

	public override void _Ready()
	{
		if (PlayerSprite == null)
			PlayerSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

		 PlayerSprite.FrameChanged += OnRollingFrameChanged;
		
		_currentHealth = MaxHealth; 
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
	}

	public override void _PhysicsProcess(double delta)
	{
		float deltaTime = (float)delta;

		// 1. 无敌时间处理
		if (_isInvincible)
		{
			_invincibleTimer -= deltaTime;
			if (_invincibleTimer <= 0) _isInvincible = false;
		}

		// 2. 缓存输入状态
		CacheInputState(deltaTime);

		// 3. 原有逻辑
		_isOnFloor = IsOnFloor();
		_isOnWall = IsOnWall();
		_velocity = Velocity;

		HandleMovementInput();
		HandleGravity(deltaTime);
		HandleJumpInput();

		Velocity = _velocity;
		MoveAndSlide();

		// 4. 更新状态机（包含攻击/防御逻辑）
		UpdateBaseState();
		UpdateAnimation();
	}

	private void CacheInputState(float delta)
	{
		// 缓存输入状态
		_isAttackJustPressed = Input.IsActionJustPressed("attack");
		_isAttackJustReleased = Input.IsActionJustReleased("attack");
		_isAttackPressed = Input.IsActionPressed("attack");
		_isDefendPressed = Input.IsActionPressed("defend");

		// 连击计时器
		if (_comboTimer > 0)
		{
			_comboTimer -= delta;
			if (_comboTimer <= 0) _comboCount = 0;
		}

		// 长按计时器 & 状态重置
		if (_isAttackPressed)
		{
			_attackHoldTimer += delta;
		}
		else
		{
			_attackHoldTimer = 0f;
		}

		// 【修改2】把松开重置，改成「新攻击按下时重置」，开启新的按键周期
		if (_isAttackJustPressed)
		{
			_hasTriggeredAnyAttack = false;
		}
	}

	private bool IsAttackState()
	{
		return _currentState is PlayerState.Attack1 or PlayerState.Attack2 
			or PlayerState.Attack3 or PlayerState.Attack4 
			or PlayerState.Attack5 or PlayerState.Attack6;
	}


	#region 原有方法（保持不变）
	private void HandleGravity(float delta)
	{
		if (!_isOnFloor)
		{
			if (_currentState == PlayerState.WallSlide)
				_velocity.Y = WallSlideSpeed;
			else
				_velocity.Y += Gravity * delta;
		}
		else
		{
			if (_velocity.Y > 0) _velocity.Y = 0;
			_currentJumpCount = 0;
			_lastWallJumpNormal = Vector2.Zero;
		}
	}

	private void HandleMovementInput()
	{
		if (IsAttackState())
		{
			_velocity.X = 0;//攻击时不准移动
			return;
		}

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
		if (IsAttackState())
        return;

		if (Input.IsActionJustPressed("jump"))
		{
			if (_currentState == PlayerState.WallSlide)
			{
				_velocity.X = -_faceDirection * 800f; 
				_velocity.Y = JumpForce; 
				_currentJumpCount = MaxJumpCount; 
				PlayerSprite.FlipH = -_faceDirection < 0;
				_lastWallJumpNormal = GetWallNormal();
				EmitSignal(SignalName.OnJumped);
			}
			else if (_currentJumpCount < MaxJumpCount)
			{
				_velocity.Y = JumpForce;
				_currentJumpCount++;
				EmitSignal(SignalName.OnJumped);
			}
		}
	}
	#endregion

	#region 修改：状态机核心逻辑（整合攻击/防御）
	private void UpdateBaseState()
	{
		// 【优先级0】动画锁定：受伤/攻击/翻滚动画播放中不切换状态
		if ((_currentState == PlayerState.Hit || IsAttackState() 
			|| _currentState == PlayerState.Rolling) 
			&& PlayerSprite.IsPlaying())
			return;

		PlayerState oldState = _currentState;
		PlayerState newState;

		// 【优先级1】防御输入
		if (_isDefendPressed)
		{
			bool hasHorizontalInput = Mathf.Abs(_moveInput.X) > InputDeadzone;
			newState = hasHorizontalInput ? PlayerState.Walk_defend : PlayerState.Defend;
		}
		// 【优先级2】上+攻击 (Attack5)
		else if (_isAttackJustPressed && Input.IsActionPressed("move_up") && !_hasTriggeredAnyAttack)
		{
			newState = PlayerState.Attack5;
			_hasTriggeredAnyAttack = true; // 锁定：本次按键不再触发其他攻击
		}
		// 【优先级3】下+攻击 (Attack6)
		else if (_isAttackJustPressed && Input.IsActionPressed("move_down") && !_hasTriggeredAnyAttack)
		{
			newState = PlayerState.Attack6;
			_hasTriggeredAnyAttack = true;
		}
		// 【优先级4】长按攻击 (Attack4)
		else if (_isAttackPressed && !_hasTriggeredAnyAttack && _attackHoldTimer >= HoldThreshold)
		{
			newState = PlayerState.Attack4;
			_hasTriggeredAnyAttack = true; // 锁定：本次按键不再触发其他攻击
		}
		// 【优先级5】短按连击 (Attack1-3)
		else if (_isAttackJustReleased && !_hasTriggeredAnyAttack && _attackHoldTimer < HoldThreshold) 
		{
			_comboCount = _comboCount >= 3 ? 1 : _comboCount + 1;
			newState = _comboCount switch
			{
				1 => PlayerState.Attack1,
				2 => PlayerState.Attack2,
				3 => PlayerState.Attack3,
				_ => PlayerState.Attack1
			};
			_hasTriggeredAnyAttack = true; // 锁定：本次按键不再触发其他攻击
			_comboTimer = ComboWindow;
		}
		// 【原有逻辑】移动/跳跃/翻滚
		else
		{
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
		}

		// 状态变化通知
		if (oldState != newState)
		{
			_currentState = newState;
			EmitSignal(SignalName.OnStateChanged, Variant.From(oldState), Variant.From(newState));
			
			if (newState == PlayerState.Rolling)
				EmitSignal(SignalName.OnRollTriggered);
			
			// 可选：攻击触发信号
			// if (IsAttackState())
			// 	EmitSignal(SignalName.OnAttackTriggered, newState);
		}
	}
	#endregion

	#region 原有方法（保持不变，除了动画更新）
	public void TakeDamage(int damage)
	{
		if (_isInvincible || _currentState == PlayerState.Hit) return;

		_currentHealth -= damage;
		_isInvincible = true;
		_invincibleTimer = InvincibleDuration;
		_currentState = PlayerState.Hit;

		PlayerSprite.Play("hit");
		_currentAnimName = "hit";

		EmitSignal(SignalName.OnTakeDamage);
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);

		if (_currentHealth <= 0)
			HandleDeath();
	}

	private void HandleDeath()
	{
		UiGameOver uiGameOver = GetNodeOrNull<UiGameOver>("/root/Root/PlayerUI/UiGameOver");
		uiGameOver?.ShowGameOver();
	}
	#endregion

	// --------------------------
	// 【正确】监听帧变化，在rolling第20帧触发特效
	// --------------------------
	private void OnRollingFrameChanged()
	{
		// 1. 只处理rolling动画，过滤其他所有动画
		if (PlayerSprite.Animation != "rolling")
			return;

		// 2. 只在第20帧触发（⚠️ Godot动画帧是0-based：编辑器里的第20帧 = 代码里的19）
		// 如果触发时机不对，把19改成20即可（对应编辑器里的第21帧）
		if (PlayerSprite.Frame != 15)
			return;

		// 3. 防止空引用报错
		if (RollingFireEffect == null)
			return;

		// 4. 实例化特效
		var fireEffect = RollingFireEffect.Instantiate<FireAttack>();
		
		// 5. 把特效加到Player的父节点（和Player同层级，不跟着角色移动）
		GetParent().AddChild(fireEffect);
		
		// 6. 初始化特效（传入角色位置和朝向）
		fireEffect.Init(GlobalPosition, _faceDirection);
	}

	#region 修改：动画更新
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
			// 新增动画映射
			PlayerState.Attack1 => "attack_1",
			PlayerState.Attack2 => "attack_2",
			PlayerState.Attack3 => "attack_3",
			PlayerState.Attack4 => "attack_4",
			PlayerState.Attack5 => "attack_5",
			PlayerState.Attack6 => "attack_6",
			PlayerState.Defend => "idle_defend",
			PlayerState.Walk_defend => "walk_defend",
			_ => "idle"
		};

		if (_currentAnimName != targetAnim)
		{
			PlayerSprite.Play(targetAnim);
			_currentAnimName = targetAnim;
		}
	}
	#endregion
}