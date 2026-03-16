using Godot;

public partial class UiGameOver : CanvasLayer
{
	[Export] private ColorRect _background;
	[Export] private Label _gameOverLabel;
	[Export] private Button _restartButton;

	public override void _Ready()
	{
		Hide();
		_restartButton.Pressed += OnRestartButtonPressed;
	}

public void ShowGameOver()
{
	Show();
}

	private void OnRestartButtonPressed()
	{
		Spikes.ResetAll();//重载场景前先重置所有尖刺的静态状态
		GetTree().ReloadCurrentScene();
	}
}
