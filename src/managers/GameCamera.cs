using Godot;
using System;
using RestraintKnight.Utils;

namespace RestraintKnight.Common
{
    public partial class GameCamera : Camera2D
    {
        #region 单例
        public static GameCamera Instance { get; private set; }

        public override void _EnterTree()
        {
            if (Instance != null && Instance != this)
            {
                QueueFree();
                return;
            }
            Instance = this;
        }

        public override void _ExitTree()
        {
            if (Instance == this) Instance = null;
        }
        #endregion

        #region 编辑器配置
        [ExportCategory("跟随设置")]
        [Export] public Node2D FollowTarget;
        [Export] public float SmoothTime = 0.12f;
        [Export] public bool LockYAxis = true;
        [Export] public float YFollowThreshold = 200f;

        [ExportCategory("视野偏移设置")]
        [Export] public float MaxLookAheadOffset = 120f;
        [Export] public float OffsetSmoothTime = 0.2f;
        [Export] public bool EnableLookAhead = true;

        [ExportCategory("边界限制设置")]
        [Export] public bool EnableBoundaryLimit = false;
        [Export] public Vector2 LevelMinBounds;
        [Export] public Vector2 LevelMaxBounds;
        [Export] public string MainTileMapLayerName = "Ground";

        [ExportCategory("震动设置")]
        [Export] public float DefaultShakeStrength = 8f;
        [Export] public float DefaultShakeDuration = 0.2f;
        #endregion

        #region 内部状态
        private Vector2 _followVelocity;
        private Vector2 _currentOffset;
        private float _offsetVelocityX;
        private float _shakeTimer;
        private float _currentShakeStrength;
        private FastNoiseLite _shakeNoise;
        private float _noiseTime;
        #endregion

        #region 生命周期
        public override void _Ready()
        {
            _shakeNoise = new FastNoiseLite
            {
                NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
                Frequency = 35f
            };
        }

        public override void _Process(double delta)
        {
            float deltaTime = (float)delta;

            ProcessShake(deltaTime);
            ProcessFollow(deltaTime);
            ProcessLookAheadOffset(deltaTime);
            
            if (EnableBoundaryLimit)
            {
                ProcessBoundaryLimit();
            }
        }
        #endregion

        #region 核心功能
        private void ProcessFollow(float deltaTime)
        {
            if (FollowTarget == null) return;

            Vector2 targetPos = FollowTarget.GlobalPosition;

            if (LockYAxis)
            {
                targetPos.Y = GlobalPosition.Y;
            }
            else
            {
                float yDiff = Mathf.Abs(targetPos.Y - GlobalPosition.Y);
                if (yDiff < YFollowThreshold)
                {
                    targetPos.Y = GlobalPosition.Y;
                }
            }

            GlobalPosition = GlobalPosition.SmoothDamp(
                targetPos + _currentOffset,
                ref _followVelocity,
                SmoothTime,
                float.MaxValue,
                deltaTime
            );
        }

        private void ProcessLookAheadOffset(float deltaTime)
        {
            if (!EnableLookAhead || FollowTarget == null) return;

            float targetOffsetX = 0f;
            
            if (Input.IsActionPressed("move_right"))
            {
                targetOffsetX = MaxLookAheadOffset;
            }
            else if (Input.IsActionPressed("move_left"))
            {
                targetOffsetX = -MaxLookAheadOffset;
            }

            _currentOffset.X = _currentOffset.X.SmoothDamp(
                targetOffsetX,
                ref _offsetVelocityX,
                OffsetSmoothTime,
                float.MaxValue,
                deltaTime
            );
        }

        // 【优化1】抽离通用的边界修正逻辑，消除重复代码
        private void ProcessBoundaryLimit()
        {
            Vector2 viewportHalf = (GetViewportRect().Size / Zoom) / 2f;

            // 自动修正边界顺序
            Vector2 safeMin = new(Mathf.Min(LevelMinBounds.X, LevelMaxBounds.X), Mathf.Min(LevelMinBounds.Y, LevelMaxBounds.Y));
            Vector2 safeMax = new(Mathf.Max(LevelMinBounds.X, LevelMaxBounds.X), Mathf.Max(LevelMinBounds.Y, LevelMaxBounds.Y));

            // 计算摄像机可移动范围
            float minX = safeMin.X + viewportHalf.X;
            float maxX = safeMax.X - viewportHalf.X;
            float minY = safeMin.Y + viewportHalf.Y;
            float maxY = safeMax.Y - viewportHalf.Y;

            // 二次修正Clamp入参
            float finalMinX = Mathf.Min(minX, maxX);
            float finalMaxX = Mathf.Max(minX, maxX);
            float finalMinY = Mathf.Min(minY, maxY);
            float finalMaxY = Mathf.Max(minY, maxY);

            GlobalPosition = new Vector2(
                Mathf.Clamp(GlobalPosition.X, finalMinX, finalMaxX),
                Mathf.Clamp(GlobalPosition.Y, finalMinY, finalMaxY)
            );
        }

        private void ProcessShake(float deltaTime)
        {
            if (_shakeTimer <= 0f)
            {
                if (Offset != Vector2.Zero) Offset = Vector2.Zero;
                return;
            }

            _shakeTimer -= deltaTime;
            _noiseTime += deltaTime * 60f;

            float currentStrength = _currentShakeStrength * (_shakeTimer / DefaultShakeDuration);
            float shakeX = _shakeNoise.GetNoise2D(_noiseTime, 0) * currentStrength;
            float shakeY = _shakeNoise.GetNoise2D(0, _noiseTime) * currentStrength;

            Offset = new Vector2(shakeX, shakeY);

            if (_shakeTimer <= 0f)
            {
                Offset = Vector2.Zero;
                _currentShakeStrength = 0f;
            }
        }
        #endregion

        #region 对外API（保持完全兼容，之前的调用不用改）
        public void TriggerShake(float? strength = null, float? duration = null)
        {
            _currentShakeStrength = strength ?? DefaultShakeStrength;
            _shakeTimer = duration ?? DefaultShakeDuration;
            _noiseTime = 0f;
        }

        public void SetZoom(Vector2 targetZoom, float smoothTime = 0.3f)
        {
            Tween tween = CreateTween();
            tween.SetEase(Tween.EaseType.Out);
            tween.SetTrans(Tween.TransitionType.Quad);
            tween.TweenProperty(this, "zoom", targetZoom, smoothTime);
        }

        public void SetFollowTarget(Node2D newTarget) => FollowTarget = newTarget;
        public void SetLevelBounds(Vector2 minBounds, Vector2 maxBounds) => (LevelMinBounds, LevelMaxBounds) = (minBounds, maxBounds);

        public void TriggerLightShake() => TriggerShake(4f, 0.1f);
        public void TriggerMediumShake() => TriggerShake(12f, 0.25f);
        public void TriggerHeavyShake() => TriggerShake(25f, 0.4f);
        #endregion

        // 【优化2】自动边界方法保留，但默认不调用，按需开启
        public void TryAutoSetBounds()
        {
            TileMap rootTileMap = GetTree().Root.GetChild(0).FindChild("TileMap", true, false) as TileMap;
            if (rootTileMap == null || rootTileMap.TileSet == null)
            {
                GD.Print("自动边界设置失败：未找到有效TileMap或TileSet");
                return;
            }

            TileMapLayer mainLayer = rootTileMap.GetNodeOrNull<TileMapLayer>(MainTileMapLayerName);
            if (mainLayer == null && rootTileMap.GetChildCount() > 0)
            {
                mainLayer = rootTileMap.GetChild<TileMapLayer>(0);
            }

            if (mainLayer == null)
            {
                GD.Print("自动边界设置失败：未找到有效TileMapLayer");
                return;
            }

            Rect2 mapBounds = mainLayer.GetUsedRect();
            if (mapBounds.Size == Vector2.Zero)
            {
                GD.Print("自动边界设置失败：TileMapLayer无有效瓦片");
                return;
            }

            Vector2 cellSize = rootTileMap.TileSet.TileSize;
            Vector2 bottomRight = (mapBounds.Position + mapBounds.Size) * cellSize;
            Vector2 topLeft = mapBounds.Position * cellSize;

            LevelMinBounds = new Vector2(Mathf.Min(topLeft.X, bottomRight.X), Mathf.Min(topLeft.Y, bottomRight.Y));
            LevelMaxBounds = new Vector2(Mathf.Max(topLeft.X, bottomRight.X), Mathf.Max(topLeft.Y, bottomRight.Y));

            GD.Print($"自动边界设置成功：Min={LevelMinBounds}, Max={LevelMaxBounds}");
        }
    }
}