using Godot;

public partial class PlayerAudioComponent : Node2D
{
    [Export] public AudioStreamPlayer2D LoopSfxPlayer;   // 循环：跑步、滑墙
    [Export] public AudioStreamPlayer2D OneShotSfxPlayer; // 普通单次：跳跃、翻滚
    [Export] public AudioStreamPlayer2D HurtSfxPlayer;    // 专属受伤：不被打断
    
    [ExportGroup("音效资源")]
    [Export] public AudioStream JumpSfx;
    [Export] public AudioStream RunningSfx;
    [Export] public AudioStream HurtSfx;
    [Export] public AudioStream RollingSfx;
    [Export] public AudioStream WallSlideSfx;

    private Player _player;
    private bool _isHurtPlaying; // 标记：受伤音效是否正在播放

    public override void _Ready()
    {
        _player = GetParent<Player>();
        
        // 自动获取所有节点
        LoopSfxPlayer ??= GetNodeOrNull<AudioStreamPlayer2D>("LoopSfxPlayer");
        OneShotSfxPlayer ??= GetNodeOrNull<AudioStreamPlayer2D>("OneShotSfxPlayer");
        HurtSfxPlayer ??= GetNodeOrNull<AudioStreamPlayer2D>("HurtSfxPlayer");

        // 绑定受伤音效的完成事件，解锁标记
        if (HurtSfxPlayer != null)
        {
            HurtSfxPlayer.Finished += () => _isHurtPlaying = false;
        }

        // 绑定玩家事件
        _player.OnJumped += OnPlayerJumped;
        _player.OnRollTriggered += OnPlayerRolled;
        _player.OnTakeDamage += OnPlayerHurt;
        _player.OnStateChanged += OnStateChanged;
    }

    private void OnPlayerJumped()
    {
        // 核心逻辑：受伤音效正在播放时，普通音效不播放
        if (_isHurtPlaying) return;
        PlayNormalOneShot(JumpSfx);
    }

    private void OnPlayerRolled()
    {
        // 核心逻辑：受伤音效正在播放时，普通音效不播放
        if (_isHurtPlaying) return;
        PlayNormalOneShot(RollingSfx);
    }

    private void OnPlayerHurt()
    {
        // 受伤音效优先级最高：停掉所有其他音效，强制播放
        StopLoop();
        if (OneShotSfxPlayer != null) OneShotSfxPlayer.Stop();
        
        if (HurtSfx != null && HurtSfxPlayer != null)
        {
            _isHurtPlaying = true; // 锁定标记
            HurtSfxPlayer.Stop();
            HurtSfxPlayer.Stream = HurtSfx;
            HurtSfxPlayer.Play();
        }
    }

    private void OnStateChanged(PlayerState oldState, PlayerState newState)
    {
        // 核心逻辑：受伤音效正在播放时，循环音效也不播放
        if (_isHurtPlaying) return;

        switch (newState)
        {
            case PlayerState.Running:
                PlayLoop(RunningSfx);
                break;
            case PlayerState.WallSlide:
                PlayLoop(WallSlideSfx);
                break;
            default:
                StopLoop();
                break;
        }
    }

    // 播放普通一次性音效
    private void PlayNormalOneShot(AudioStream sfx)
    {
        if (sfx == null || OneShotSfxPlayer == null) return;
        OneShotSfxPlayer.Stop();
        OneShotSfxPlayer.Stream = sfx;
        OneShotSfxPlayer.Play();
    }

    // 播放循环音效
    private void PlayLoop(AudioStream sfx)
    {
        if (sfx == null || LoopSfxPlayer == null) return;
        if (LoopSfxPlayer.Stream == sfx && LoopSfxPlayer.Playing) return;
        
        LoopSfxPlayer.Stop();
        LoopSfxPlayer.Stream = sfx;
        LoopSfxPlayer.Play();
    }

    // 停止循环音效
    private void StopLoop()
    {
        if (LoopSfxPlayer != null && LoopSfxPlayer.Playing)
        {
            LoopSfxPlayer.Stop();
            LoopSfxPlayer.Stream = null;
        }
    }

    // 销毁时解绑事件
    protected override void Dispose(bool disposing)
    {
        if (disposing && _player != null)
        {
            _player.OnJumped -= OnPlayerJumped;
            _player.OnRollTriggered -= OnPlayerRolled;
            _player.OnTakeDamage -= OnPlayerHurt;
            _player.OnStateChanged -= OnStateChanged;
        }
        base.Dispose(disposing);
    }
}