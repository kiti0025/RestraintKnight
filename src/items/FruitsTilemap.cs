using Godot;
using System.Collections.Generic;

public partial class FruitsTilemap : TileMapLayer
{
    [ExportGroup("全局配置")]
    [Export] public AudioStream CollectSound;
    [Export] public Label ScoreLabel;

    // 不同水果不同分数 —— 在这里统一改！
    private Dictionary<string, int> _fruitScoreMap = new Dictionary<string, int>()
    {
        { "Apple", 100 },
        { "Banana", 200 },
        { "Kiwi", 500 },
        { "Cherries", 300 }
    };

    private List<Area2D> _spawnedFruits = new List<Area2D>();
    private int _totalScore;
    private AudioStreamPlayer2D _soundPlayer;

    public override void _Ready()
    {
		if (ScoreLabel == null)
		{
			ScoreLabel = GetNodeOrNull<Label>("/root/Root/PlayerUI/UiPlayer/ScoreLabel"); 
		}

        _soundPlayer = new AudioStreamPlayer2D();
        AddChild(_soundPlayer);

        UpdateScoreUI();

        foreach (Node child in GetChildren())
        {
            if (child is Area2D fruit)
                RegisterFruit(fruit);
        }

        ChildEnteredTree += OnChildEntered;
    }

    private void RegisterFruit(Area2D fruit)
    {
        if (_spawnedFruits.Contains(fruit)) return;
        fruit.BodyEntered += (body) => OnFruitTouched(fruit, body);
        _spawnedFruits.Add(fruit);
    }

    private void OnChildEntered(Node node)
    {
        if (node is Area2D fruit)
            CallDeferred(nameof(RegisterFruit), fruit);
    }

    private void OnFruitTouched(Area2D fruit, Node2D body)
    {
        if (!body.IsInGroup("player")) return;
        if (!_spawnedFruits.Contains(fruit)) return;

        CollectFruit(fruit);
    }

    private void CollectFruit(Area2D fruit)
    {
        _spawnedFruits.Remove(fruit);

        // 延迟关闭碰撞，不报错
        fruit.SetDeferred("monitoring", false);
        fruit.SetDeferred("monitorable", false);

        if (CollectSound != null) _soundPlayer.Play();

        // ✅ 关键：根据水果名字自动给不同分数
        // 优先读取水果场景里的自定义分数，没有则用默认100
int score = fruit.HasMeta("FruitScore") ? (int)fruit.GetMeta("FruitScore") : 100;
        _totalScore += score;
        UpdateScoreUI();

        // 消失逻辑
        AnimatedSprite2D anim = fruit.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (anim != null && anim.SpriteFrames != null && anim.SpriteFrames.HasAnimation("collected"))
        {
            anim.Play("collected");
            void OnAnimEnd()
            {
                anim.AnimationFinished -= OnAnimEnd;
                fruit.QueueFree();
            }
            anim.AnimationFinished += OnAnimEnd;
        }
        else
        {
            double delay = CollectSound != null ? CollectSound.GetLength() : 0.05;
            GetTree().CreateTimer(delay).Timeout += fruit.QueueFree;
        }
    }

    private void UpdateScoreUI()
    {
        if (ScoreLabel != null)
            ScoreLabel.Text = $"Score: {_totalScore:D6}";
    }
}