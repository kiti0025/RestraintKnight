using Godot;

public enum PlayerState
{
    Jump,
    Fall,
    Running,
    Rolling,
    CrouchingIdle,
    Idle
}

public partial class Player : CharacterBody2D
{
    #region Inspector Config
    [ExportGroup("Movement")]
    [Export] public float MoveSpeed = 280f;
    [Export] public float Gravity = 1300f;

    [ExportGroup("Jump")]
    [Export] public float JumpForce = -480f;

    [ExportGroup("Input")]
    [Export] public float InputDeadzone = 0.2f;

    [ExportGroup("Node Refs")]
    [Export] public AnimatedSprite2D PlayerSprite;
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
    private string _currentAnimName;
    #endregion

    public override void _Ready()
    {
        if (PlayerSprite == null)
            PlayerSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
    }

    public override void _PhysicsProcess(double delta)
    {
        float deltaTime = (float)delta;
        _isOnFloor = IsOnFloor();

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
            _velocity.Y += Gravity * delta;
        }
        else
        {
            _velocity.Y = 0;
            _currentJumpCount = 0;
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
            float speed = PlayerSprite.Frame < 4 ? 2.5f : 1.2f;
            _velocity.X = _faceDirection * MoveSpeed * speed;
        }
        else if(_isMoveDownPressed)
        {
            _velocity.X = 0;
        }
        else
        {
            _velocity.X = _moveInput.X * MoveSpeed;
        }
    }

    private void HandleJumpInput()
    {
        if (Input.IsActionJustPressed("jump") && _currentJumpCount < MaxJumpCount)
        {
            _velocity.Y = JumpForce;
            _currentJumpCount++;
        }
    }

    private void UpdateBaseState()
    {
        bool hasHorizontalInput = Mathf.Abs(_moveInput.X) > InputDeadzone;
        bool isRollingTriggered = _currentState == PlayerState.Rolling && PlayerSprite.IsPlaying() 
                                    || _isMoveDownJustPressed && hasHorizontalInput 
                                    || _isMoveDownPressed && _isHorizontalJustPressed;

        if (!_isOnFloor)
        {
            _currentState = Velocity.Y < 0 ? PlayerState.Jump : PlayerState.Fall;
        }
        else if (isRollingTriggered)
        {
            _currentState = PlayerState.Rolling;
        }
        else if (_isMoveDownPressed)
        {
            _currentState = PlayerState.CrouchingIdle;
        }
        else
        {
            _currentState = hasHorizontalInput ? PlayerState.Running : PlayerState.Idle;
        }
    }

    private void UpdateAnimation()
    {
        string targetAnim = _currentState switch
        {
            PlayerState.Idle => "idle",
            PlayerState.Running => "running",
            PlayerState.Rolling => "rolling",
            PlayerState.CrouchingIdle => "crouching_idle",
            PlayerState.Jump => "jump",
            PlayerState.Fall => "fall",
            _ => "idle"
        };

        if (_currentAnimName != targetAnim)
        {
            PlayerSprite.Play(targetAnim);
            _currentAnimName = targetAnim;
        }
    }
}
