using Godot;

public partial class StartUi : CanvasLayer
{
    // 节点引用
    private AudioStreamPlayer _bgmPlayer;
    private AudioStreamPlayer _sfxPlayer;
    private Button _startButton;
    private Button _settingButton;
    private Button _exitButton;

    // 游戏场景路径（保持你原有的路径不变）
    private const string GameScenePath = "res://scenes/levels/Root.tscn";

    public override void _Ready()
    {
        // 获取节点
        _bgmPlayer = GetNode<AudioStreamPlayer>("BGMPlayer");
        _sfxPlayer = GetNode<AudioStreamPlayer>("SFXPlayer");
        _startButton = GetNode<Button>("Background/UILayout/StartButton");
        _settingButton = GetNode<Button>("Background/UILayout/SettingButton");
        _exitButton = GetNode<Button>("Background/UILayout/ExitButton");

        // 自动播放背景音乐
        if (_bgmPlayer.Stream != null)
        {
            _bgmPlayer.Play();
        }

        // 绑定按钮事件
        _startButton.Pressed += OnStartGame;
        _settingButton.Pressed += OnSettingGame;
        _exitButton.Pressed += OnExitGame;
    }

    /// <summary>
    /// 封装：播放点击音效（复用代码，告别重复）
    /// </summary>
    private void PlayClickSound()
    {
        if (_sfxPlayer.Stream != null)
        {
            _sfxPlayer.Play();
        }
    }

    /// <summary>
    /// 开始游戏（完整保留你原有的所有逻辑）
    /// </summary>
    private async void OnStartGame()
    {
        PlayClickSound();
        
        await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
        _bgmPlayer.Stop();
        GetTree().ChangeSceneToFile(GameScenePath);
    }

    /// <summary>
    /// 设置按钮（仅播放音效，修复冗余async）
    /// </summary>
    private void OnSettingGame()
    {
        PlayClickSound();
    }

    /// <summary>
    /// 退出游戏（补全逻辑，保留音效+延迟）
    /// </summary>
    private async void OnExitGame()
    {
        PlayClickSound();
        
        await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
        GetTree().Quit();
    }
}