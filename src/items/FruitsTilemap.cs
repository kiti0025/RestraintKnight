using Godot;
using System.Collections.Generic;

public partial class FruitsTilemap : TileMapLayer
{
    [ExportGroup("全局配置")]
    [Export] public AudioStream CollectSound;
    [Export] public Label ScoreLabel;

    // 配置常量：统一管理元数据键名和默认值，避免硬编码
    private const string MetaKeyScore = "FruitScore";
    private const int DefaultScore = 100;
    private const string CollectAnimName = "collected";

    private readonly List<Area2D> _spawnedFruits = new List<Area2D>();
    private int _totalScore;
    private AudioStreamPlayer2D _soundPlayer;

    public override void _Ready()
    {
        // 1. 自动获取分数UI（如果未手动赋值）
        if (ScoreLabel == null)
        {
            ScoreLabel = GetNodeOrNull<Label>("/root/Root/PlayerUI/UiPlayer/ScoreLabel");
        }

        // 2. 初始化音效播放器
        InitializeSoundPlayer();

        // 3. 初始化分数显示
        UpdateScoreUI();

        // 4. 注册编辑器中已放置的水果
        RegisterExistingFruits();

        // 5. 监听运行时动态生成的水果
        ChildEnteredTree += OnChildEnteredTree;
    }

    private void InitializeSoundPlayer()
    {
        _soundPlayer = new AudioStreamPlayer2D();
        AddChild(_soundPlayer);
        if (CollectSound != null)
        {
            _soundPlayer.Stream = CollectSound;
        }
    }

    private void RegisterExistingFruits()
    {
        foreach (Node child in GetChildren())
        {
            if (child is Area2D fruit)
            {
                RegisterFruit(fruit);
            }
        }
    }

    private void RegisterFruit(Area2D fruit)
    {
        if (_spawnedFruits.Contains(fruit))
        {
            return;
        }

        fruit.BodyEntered += (body) => OnFruitTouched(fruit, body);
        _spawnedFruits.Add(fruit);
    }

    private void OnChildEnteredTree(Node node)
    {
        if (node is Area2D fruit)
        {
            CallDeferred(nameof(RegisterFruit), fruit);
        }
    }

    private void OnFruitTouched(Area2D fruit, Node2D body)
    {
        // 严格判定玩家分组
        if (!body.IsInGroup("player"))
        {
            return;
        }

        // 防止重复收集
        if (!_spawnedFruits.Contains(fruit))
        {
            return;
        }

        CollectFruit(fruit);
    }

    private void CollectFruit(Area2D fruit)
    {
        // 1. 先从列表移除，彻底杜绝重复触发
        _spawnedFruits.Remove(fruit);

        // 2. 延迟关闭碰撞，避免引擎信号锁定报错
        fruit.SetDeferred("monitoring", false);
        fruit.SetDeferred("monitorable", false);

        // 3. 播放收集音效
        if (CollectSound != null)
        {
            _soundPlayer.Play();
        }

        // 4. 读取水果专属分数（完全基于元数据）
        int score = fruit.HasMeta(MetaKeyScore) ? (int)fruit.GetMeta(MetaKeyScore) : DefaultScore;
        _totalScore += score;
        UpdateScoreUI();

        // 5. 处理收集动画和销毁
        HandleFruitDestruction(fruit);
    }

    private void HandleFruitDestruction(Area2D fruit)
    {
        AnimatedSprite2D anim = fruit.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        
        if (anim != null && anim.SpriteFrames != null && anim.SpriteFrames.HasAnimation(CollectAnimName))
        {
            // 播放收集动画，播完销毁
            anim.Play(CollectAnimName);
            void OnAnimationFinished()
            {
                anim.AnimationFinished -= OnAnimationFinished;
                fruit.QueueFree();
            }
            anim.AnimationFinished += OnAnimationFinished;
        }
        else
        {
            // 没有收集动画，等音效播完销毁
            double delay = CollectSound != null ? CollectSound.GetLength() : 0.05;
            GetTree().CreateTimer(delay).Timeout += fruit.QueueFree;
        }
    }

    private void UpdateScoreUI()
    {
        if (ScoreLabel != null)
        {
            ScoreLabel.Text = $"Score: {_totalScore}";
        }
    }
}