using Godot;

public partial class PlayerAudioComponent : Node2D
{
    [Export] public AudioStreamPlayer2D LoopPlayer;  // 管循环：跑步、滑墙
    [Export] public AudioStreamPlayer2D OneShotPlayer; // 管单次：跳跃、受伤、翻滚
    
    [ExportGroup("音效资源")]
    [Export] public AudioStream JumpSfx;
    [Export] public AudioStream RunningSfx;
    [Export] public AudioStream HurtSfx;
    [Export] public AudioStream RollingSfx;
    [Export] public AudioStream WallSlideSfx;

    private Player _player;

    public override void _Ready()
    {
        _player = GetParent<Player>();
        LoopPlayer ??= GetNodeOrNull<AudioStreamPlayer2D>("LoopSfxPlayer");
        OneShotPlayer ??= GetNodeOrNull<AudioStreamPlayer2D>("OneShotSfxPlayer");

        // 绑定事件，极简写法
        _player.OnJumped += () => PlayOneShot(JumpSfx);
        _player.OnRollTriggered += () => PlayOneShot(RollingSfx);
        _player.OnTakeDamage += () => { StopLoop(); PlayOneShot(HurtSfx); };
        _player.OnStateChanged += OnStateChanged;
    }

    private void OnStateChanged(PlayerState oldState, PlayerState newState)
    {
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

    // 播放一次性音效（不打断循环）
    private void PlayOneShot(AudioStream sfx)
    {
        if (sfx == null || OneShotPlayer == null) return;
        OneShotPlayer.Stop();
        OneShotPlayer.Stream = sfx;
        OneShotPlayer.Play();
    }

    // 播放循环音效（资源已设置好循环）
    private void PlayLoop(AudioStream sfx)
    {
        if (sfx == null || LoopPlayer == null) return;
        if (LoopPlayer.Stream == sfx && LoopPlayer.Playing) return;
        
        LoopPlayer.Stop();
        LoopPlayer.Stream = sfx;
        LoopPlayer.Play();
    }

    // 停止循环音效
    private void StopLoop()
    {
        if (LoopPlayer != null && LoopPlayer.Playing)
        {
            LoopPlayer.Stop();
            LoopPlayer.Stream = null;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _player != null)
        {
            _player.OnJumped -= () => PlayOneShot(JumpSfx);
            _player.OnRollTriggered -= () => PlayOneShot(RollingSfx);
            _player.OnTakeDamage -= () => { StopLoop(); PlayOneShot(HurtSfx); };
            _player.OnStateChanged -= OnStateChanged;
        }
        base.Dispose(disposing);
    }
}