using Godot;
using System.Collections.Generic;

public partial class Saw : Area2D
{
    [Export] public Line2D PathLine;       
    [Export] public float MoveSpeed = 80f; // 移动速度（像素/秒）

    private readonly List<float> _segmentLengths = new List<float>();
    private float _totalPathLength = 0f;
    private float _currentProgress = 0f;
    private int _moveDirection = 1;
    private bool _isClosedPath = false;

    public override void _Ready()
    {
        CalculatePathInfo();
        BodyEntered += OnBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (PathLine == null || PathLine.Points.Length < 2) return;

        float moveDelta = MoveSpeed * (float)delta * _moveDirection;
        _currentProgress += moveDelta;

        if (_isClosedPath)
        {
            if (_currentProgress >= _totalPathLength)
                _currentProgress = 0;
        }
        else
        {
            if (_currentProgress >= _totalPathLength)
            {
                _currentProgress = _totalPathLength;
                _moveDirection *= -1;
            }
            else if (_currentProgress <= 0)
            {
                _currentProgress = 0;
                _moveDirection *= -1;
            }
        }

        GlobalPosition = GetPositionByProgress(_currentProgress);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player player)
        {
            player.TakeDamage(1);
        }
    }

    private void CalculatePathInfo()
    {
        _segmentLengths.Clear();
        _totalPathLength = 0f;
        _isClosedPath = PathLine.Closed;
        _moveDirection = 1;

        Vector2[] points = PathLine.Points;
        int pointCount = points.Length;

        // 闭合路径额外加一段：终点→起点
        int segmentCount = _isClosedPath ? pointCount : pointCount - 1;

        for (int i = 0; i < segmentCount; i++)
        {
            Vector2 p1 = PathLine.ToGlobal(points[i]);
            Vector2 p2 = PathLine.ToGlobal(points[(i + 1) % pointCount]);
            float length = p1.DistanceTo(p2);
            _segmentLengths.Add(length);
            _totalPathLength += length;
        }
    }

    // 根据进度计算路径上的坐标
    private Vector2 GetPositionByProgress(float progress)
    {
        progress = Mathf.Clamp(progress, 0f, _totalPathLength);
        float accumulatedLength = 0f;
        Vector2[] points = PathLine.Points;
        int pointCount = points.Length;

        for (int i = 0; i < _segmentLengths.Count; i++)
        {
            float segLength = _segmentLengths[i];
            if (progress <= accumulatedLength + segLength)
            {
                float t = (progress - accumulatedLength) / segLength;
                Vector2 p1 = PathLine.ToGlobal(points[i]);
                Vector2 p2 = PathLine.ToGlobal(points[(i + 1) % pointCount]);
                return p1.Lerp(p2, t);
            }
            accumulatedLength += segLength;
        }

        // 兜底返回最后一个点
        return PathLine.ToGlobal(points[^1]);
    }
}