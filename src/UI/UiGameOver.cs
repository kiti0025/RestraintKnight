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
		GetTree().ReloadCurrentScene();
	}
}
