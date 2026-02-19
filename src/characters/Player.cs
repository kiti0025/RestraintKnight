using Godot;
using System;

namespace RestraintKnight.Characters
{
    public partial class Player : CharacterBody2D
    {
        #region 编辑器配置
        [ExportCategory("基础移动参数")]
        [Export] public float WalkSpeed = 150f;
        [Export] public float RunSpeed = 320f;
        [Export] public float Gravity = 1800f;
        [Export] public float JumpForce = -600f;

        [ExportCategory("双击检测参数")]
        [Export] public float DoubleClickWindow = 0.3f;

        [ExportCategory("节点引用")]
        [Export] public AnimatedSprite2D AnimatedSprite;
        [Export] public AnimationPlayer AnimationPlayer;
        #endregion

        #region 内部状态
        private enum WeaponState { Sheathed = 1, Drawn = 2 }
        private WeaponState _currentWeaponState;
        private Vector2 _velocity;
        private bool _isRunning;
        private float _lastLeftPressTime;
        private float _lastRightPressTime;

        private const string ANIM_IDLE = "idle";
        private const string ANIM_WALK = "walking";
        private const string ANIM_RUN = "running";
        private const string ANIM_JUMP = "jump";
        private const string ANIM_FALL = "fall";
        #endregion

        #region 生命周期
        public override void _Ready()
        {
            _currentWeaponState = WeaponState.Sheathed;
            _isRunning = false;

            if (AnimatedSprite == null)
            {
                GD.PrintErr("请在编辑器中给Player脚本赋值AnimatedSprite2D节点！");
            }
            if (AnimationPlayer == null)
            {
                GD.PrintErr("请在编辑器中给Player脚本赋值AnimationPlayer节点！");
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            float deltaTime = (float)delta;

            // 1. 重力
            _velocity.Y = IsOnFloor() ? 0f : _velocity.Y + Gravity * deltaTime;

            // 2. 武器切换
            HandleWeaponToggle();

            // 3. 移动输入
            float inputDir = HandleMovementInput();

            // 4. 跳跃
            HandleJump();

            // 5. 移动执行
            _velocity.X = inputDir * (_isRunning ? RunSpeed : WalkSpeed);
            Velocity = _velocity;
            MoveAndSlide();

            // 6. 动画更新
            UpdateAnimation(inputDir);
        }
        #endregion

        #region 核心功能
        private void HandleWeaponToggle()
        {
            if (Input.IsActionJustPressed("sheathe"))
            {
                _currentWeaponState = _currentWeaponState == WeaponState.Sheathed 
                    ? WeaponState.Drawn 
                    : WeaponState.Sheathed;
                _isRunning = false;
            }
        }

        // 【优化1】把左右键重复的双击逻辑抽成通用方法
        private float HandleMovementInput()
        {
            float inputDir = 0f;
            bool isLeftPressed = Input.IsActionPressed("move_left");
            bool isRightPressed = Input.IsActionPressed("move_right");
            bool isOnGround = IsOnFloor();

            if (isOnGround)
            {
                // 通用双击检测
                CheckDoubleClick("move_left", ref _lastLeftPressTime);
                CheckDoubleClick("move_right", ref _lastRightPressTime);

                // 【优化2】修正松开重置逻辑：原逻辑里IsActionJustReleased时IsPressed已经是false，判断永远不成立
                if (Input.IsActionJustReleased("move_left") || Input.IsActionJustReleased("move_right"))
                {
                    // 直接检查当前是否还有方向键按下，没有就重置
                    if (!Input.IsActionPressed("move_left") && !Input.IsActionPressed("move_right"))
                    {
                        _isRunning = false;
                    }
                }
            }

            // 计算方向
            if (isLeftPressed) inputDir = -1f;
            else if (isRightPressed) inputDir = 1f;

            // 【优化3】统一翻转逻辑，避免重复判断
            if (AnimatedSprite != null && Mathf.Abs(inputDir) > 0.1f)
            {
                AnimatedSprite.FlipH = inputDir < 0f;
            }

            return inputDir;
        }

        // 通用双击检测方法，消除左右键重复代码
        private void CheckDoubleClick(string actionName, ref float lastPressTime)
        {
            if (Input.IsActionJustPressed(actionName))
            {
                float currentTime = (float)Time.GetTicksMsec() / 1000f;
                _isRunning = currentTime - lastPressTime <= DoubleClickWindow;
                lastPressTime = currentTime;
            }
        }

        private void HandleJump()
        {
            if (IsOnFloor() && Input.IsActionJustPressed("move_jump"))
            {
                _velocity.Y = JumpForce;
            }
        }

        private void UpdateAnimation(float inputDir)
        {
            if (AnimatedSprite == null) return;

            string targetAnimBase;
            bool isOnGround = IsOnFloor();

            if (!isOnGround)
            {
                targetAnimBase = _velocity.Y < 0 ? ANIM_JUMP : ANIM_FALL;
            }
            else
            {
                if (Mathf.Abs(inputDir) > 0.1f)
                {
                    targetAnimBase = _isRunning ? ANIM_RUN : ANIM_WALK;
                }
                else
                {
                    targetAnimBase = ANIM_IDLE;
                }
            }

            string finalAnimName = $"{targetAnimBase}_{(int)_currentWeaponState}";
            
            // 【优化4】增加动画存在性检查，避免报错
            if (AnimatedSprite.SpriteFrames.HasAnimation(finalAnimName) 
                && AnimatedSprite.Animation != finalAnimName)
            {
                AnimatedSprite.Play(finalAnimName);
            }
        }
        #endregion
    }
}