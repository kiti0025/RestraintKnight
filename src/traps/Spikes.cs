using Godot;
using System.Collections.Generic;

public partial class Spikes : Area2D
{
    [Export] private float _damageInterval = 0.5f;
    
    // 改用实例变量 + 静态列表管理所有尖刺，避免静态变量状态残留
    private static List<Spikes> _allSpikes = new List<Spikes>();
    private static Player _player;
    private static float _timer;
    private static bool _isInside;
    private static int _contactCount = 0;

    public override void _Ready()
    {
        // 把自己注册到全局列表
        if (!_allSpikes.Contains(this))
        {
            _allSpikes.Add(this);
        }

        // 只在第一个尖刺初始化时获取玩家引用
        if (_player == null)
        {
            _player = GetTree().GetFirstNodeInGroup("player") as Player;
            _timer = _damageInterval;
        }

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    // 供重新开始逻辑调用的静态重置方法
    public static void ResetAll()
    {
        _player = null;
        _timer = 0;
        _isInside = false;
        _contactCount = 0;
        _allSpikes.Clear();
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body.IsInGroup("player") && body is Player)
        {
            _contactCount++;
            if (_contactCount == 1)
            {
                _isInside = true;
            }
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body.IsInGroup("player") && body is Player)
        {
            _contactCount--;
            if (_contactCount <= 0)
            {
                _contactCount = 0;
                _isInside = false;
                _timer = _damageInterval;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null || !_isInside) return;

        _timer += (float)delta;
        if (_timer >= _damageInterval)
        {
            _player.TakeDamage(1);
            _timer = 0;
        }
    }

    public override void _ExitTree()
    {
        // 从全局列表中移除自己，避免内存泄漏
        if (_allSpikes.Contains(this))
        {
            _allSpikes.Remove(this);
        }
    }
}