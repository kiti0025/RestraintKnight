using Godot;

public partial class Trampoline : StaticBody2D
{
    [Export] public float BounceForce = -800f;
    [Export] private Area2D _bounceTriggerArea;
    [Export] private AnimatedSprite2D _animatedSprite;
    
    // 加个标记，避免动画信号重复绑定
    private bool _isAnimConnected = false;

    public override void _Ready()
    {
        _bounceTriggerArea ??= GetNode<Area2D>("Area2D");
        _animatedSprite ??= GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        _bounceTriggerArea.BodyEntered += OnBounceTriggerEntered;

        if (_animatedSprite.SpriteFrames.HasAnimation("idle"))
        {
            _animatedSprite.Play("idle");
        }
    }

    private void OnBounceTriggerEntered(Node2D body)
    {
        if (body is not CharacterBody2D player || !player.IsInGroup("player"))
        {
            return;
        }

        // 强制弹跳
        player.Velocity = new Vector2(player.Velocity.X, BounceForce);

        // 播放动画，只在未绑定时绑定信号
        if (_animatedSprite.SpriteFrames.HasAnimation("jump") && !_isAnimConnected)
        {
            _animatedSprite.Play("jump");
            _animatedSprite.AnimationFinished += OnJumpAnimFinished;
            _isAnimConnected = true;
        }
    }

    private void OnJumpAnimFinished()
    {
        if (_animatedSprite.SpriteFrames.HasAnimation("idle"))
        {
            _animatedSprite.Play("idle");
        }
        // 解绑信号并重置标记
        _animatedSprite.AnimationFinished -= OnJumpAnimFinished;
        _isAnimConnected = false;
    }

    public override void _ExitTree()
    {
        if (_bounceTriggerArea != null)
        {
            _bounceTriggerArea.BodyEntered -= OnBounceTriggerEntered;
        }
        if (_animatedSprite != null && _isAnimConnected)
        {
            _animatedSprite.AnimationFinished -= OnJumpAnimFinished;
        }
        base._ExitTree();
    }
}