using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class FallingPlatform : StaticBody2D
{
    #region 编辑器参数（按效果阶段分组，方便调手感）
    [ExportGroup("第一阶段：下沉反馈")]
    [Export] public float SinkDistance { get; set; } = 8f;
    [Export] public float SinkDuration { get; set; } = 0.1f;

    [ExportGroup("第二阶段：摇晃预警")]
    [Export] public float ShakeAmount { get; set; } = 6f;
    [Export] public float ShakeSpeed { get; set; } = 30f;
    [Export] public float ShakeDuration { get; set; } = 0.8f;

    [ExportGroup("第三阶段：最终下落")]
    [Export] public float FallSpeed { get; set; } = 800f;
    // 下落消失后，多久重新刷新出来（默认5秒）
    [Export] public float RespawnTime { get; set; } = 5f;

    [ExportGroup("检测配置")]
    [Export] public string PlayerGroup { get; set; } = "player";
    #endregion

    #region 内部状态
    private Vector2 _initialPosition;
    private bool _isTriggered = false;
    private bool _isNodeDestroyed = false;

    private readonly HashSet<PhysicsBody2D> _playersOnPlatform = new HashSet<PhysicsBody2D>();
    private readonly List<PhysicsBody2D> _playerSnapshot = new List<PhysicsBody2D>();

    private Tween _activeTween;
    private bool _isShaking = false;
    private double _shakeElapsed = 0f;
    private bool _isFalling = false;
    #endregion

    #region 节点引用
    private Area2D _triggerArea;
    private AnimatedSprite2D _sprite;
    #endregion

    #region 生命周期
    public override void _Ready()
    {
        _initialPosition = GlobalPosition;

        _triggerArea = GetNodeOrNull<Area2D>("Area2D");
        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

        if (_triggerArea == null)
        {
            GD.PrintErr($"【下落平台】{Name} 缺少 Area2D 子节点！");
            return;
        }

        _triggerArea.BodyEntered += OnPlayerEnter;
        _triggerArea.BodyExited += OnPlayerLeave;
    }

    public override void _ExitTree()
    {
        _isNodeDestroyed = true;
        CleanupTween();
        if (_triggerArea != null)
        {
            _triggerArea.BodyEntered -= OnPlayerEnter;
            _triggerArea.BodyExited -= OnPlayerLeave;
        }
        _playersOnPlatform.Clear();
        _playerSnapshot.Clear();
    }
    #endregion

    #region 玩家检测
    private void OnPlayerEnter(Node body)
    {
        if (body is not PhysicsBody2D player || !player.IsInGroup(PlayerGroup)) return;
        _playersOnPlatform.Add(player);

        if (!_isTriggered)
        {
            _isTriggered = true;
            _ = RunFullSequence();
        }
    }

    private void OnPlayerLeave(Node body)
    {
        if (body is PhysicsBody2D player)
            _playersOnPlatform.Remove(player);
    }
    #endregion

    #region 核心三段式 + 复活流程
    private async Task RunFullSequence()
    {
        // 阶段1：下沉
        await DoSink();
        if (_isNodeDestroyed) return;

        // 阶段2：摇晃
        await DoShake();
        if (_isNodeDestroyed) return;

        // 阶段3：下落
        await DoFall();
        if (_isNodeDestroyed) return;

        // 阶段4：等待5秒 → 复位复活
        await RespawnPlatform();
    }
    #endregion

    #region 各阶段动画实现
    private async Task DoSink()
    {
        Vector2 sinkTarget = _initialPosition + Vector2.Down * SinkDistance;
        await MovePlatformTo(sinkTarget, SinkDuration, Tween.EaseType.Out);
    }

    private async Task DoShake()
    {
        _isShaking = true;
        _shakeElapsed = 0f;

        var shakeTimer = GetTree().CreateTimer(ShakeDuration);
        await ToSignal(shakeTimer, SceneTreeTimer.SignalName.Timeout);

        _isShaking = false;
        GlobalPosition = _initialPosition + Vector2.Down * SinkDistance;
    }

    // 下落逻辑（修改为：下落隐藏，不销毁）
    private async Task DoFall()
    {
        _isFalling = true;
        // 等待平台下落移出屏幕
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await Task.Delay(1000); 
        
        _isFalling = false;
        Visible = false; // 隐藏平台
        _triggerArea.Monitoring = false; // 关闭检测
    }

    // 平台复位复活（核心新增）
    private async Task RespawnPlatform()
    {
        // 等待5秒
        var respawnTimer = GetTree().CreateTimer(RespawnTime);
        await ToSignal(respawnTimer, SceneTreeTimer.SignalName.Timeout);

        // 复位所有状态 + 位置
        GlobalPosition = _initialPosition;
        Visible = true;
        _triggerArea.Monitoring = true;
        _isTriggered = false;
        _playersOnPlatform.Clear();

        GD.Print("平台已刷新复活！");
    }
    #endregion

    #region 帧更新
    public override void _Process(double delta)
    {
        if (_isShaking)
        {
            _shakeElapsed += delta;
            float offset = Mathf.Sin((float)_shakeElapsed * ShakeSpeed) * ShakeAmount;
            Vector2 targetPos = _initialPosition + Vector2.Down * SinkDistance + Vector2.Right * offset;
            MovePlatformAndPlayers(targetPos);
        }

        if (_isFalling)
        {
            Vector2 fallDelta = Vector2.Down * FallSpeed * (float)delta;
            GlobalPosition += fallDelta;
            MovePlayersByDelta(fallDelta);
        }
    }
    #endregion

    #region 工具方法
    private async Task MovePlatformTo(Vector2 targetPos, float duration, Tween.EaseType ease)
    {
        CleanupTween();
        _activeTween = CreateTween();
        _activeTween.SetEase(ease);
        _activeTween.TweenProperty(this, "global_position", targetPos, duration);
        await ToSignal(_activeTween, Tween.SignalName.Finished);
    }

    private void MovePlatformAndPlayers(Vector2 newPos)
    {
        Vector2 delta = newPos - GlobalPosition;
        GlobalPosition = newPos;
        MovePlayersByDelta(delta);
    }

    private void MovePlayersByDelta(Vector2 delta)
    {
        if (_playersOnPlatform.Count == 0) return;

        _playerSnapshot.Clear();
        _playerSnapshot.AddRange(_playersOnPlatform);

        foreach (var player in _playerSnapshot)
        {
            if (IsInstanceValid(player))
                player.GlobalPosition += delta;
            else
                _playersOnPlatform.Remove(player);
        }
    }

    private void CleanupTween()
    {
        if (_activeTween != null && IsInstanceValid(_activeTween))
        {
            _activeTween.Kill();
            _activeTween = null;
        }
    }
    #endregion
}