using Godot;

public partial class SpikedBall : Area2D
{
    #region 可配置参数（Inspector面板调整）
    [Export] public NodePath RopeLine2DPath;       // 绑定的Line2D绳体节点
    [Export] public MovementMode CurrentMode = MovementMode.Circular; // 运动模式
    [Export] public float MoveSpeed = 90f;         // 速度（圆周：度/秒；单摆：摆动频率）
    [Export] public float MaxPendulumAngle = 80f;  // 单摆最大摆角（度）
    [Export] public bool IsClockwise = true;        // 圆周是否顺时针
    [Export] public bool AutoStart = true;          // 自动开始运动
    #endregion

    #region 内部变量
    private Line2D _ropeLine;
    private Vector2 _pivotGlobalPos; // 悬挂点/圆心（Line2D第一个顶点的全局坐标）
    private float _ropeLength;       // 绳长（运动半径）
    private float _currentAngleRad;  // 当前角度（弧度）
    private float _balanceAngleRad;  // 单摆平衡角度（初始线段方向）
    private float _timeAccumulator;  // 单摆时间累加器
    private bool _isMoving;
    #endregion

    public enum MovementMode
    {
        Circular,    // 匀速圆周运动
        Pendulum     // 单摆摆动运动
    }

    public override void _Ready()
    {
        // 1. 获取并校验Line2D节点
        if (RopeLine2DPath == null)
        {
            GD.PrintErr($"[{Name}] 错误：未绑定Line2D绳体节点！");
            SetPhysicsProcess(false);
            return;
        }

        _ropeLine = GetNode<Line2D>(RopeLine2DPath);

        // 2. 校验Line2D顶点数量（至少2个：悬挂点 + 球初始位置）
        if (_ropeLine == null || _ropeLine.Points.Length < 2) 
        {
			GD.PrintErr($"[{Name}] 错误：找不到Line2D节点！");
            SetPhysicsProcess(false);
            return;
        }

        // 3. 初始化运动基础参数
        _pivotGlobalPos = _ropeLine.GlobalPosition + _ropeLine.Points[0];
        _ropeLength = _ropeLine.Points[0].DistanceTo(_ropeLine.Points[1]);
        
        // 初始方向角度（以Line2D初始线段为基准）
        Vector2 initialDir = (_ropeLine.Points[1] - _ropeLine.Points[0]).Normalized();
        _currentAngleRad = initialDir.Angle();
        _balanceAngleRad = _currentAngleRad; // 单摆平衡位置 = 初始线段方向

        _isMoving = AutoStart;

        BodyEntered += OnBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_isMoving || _ropeLine == null) return;

        float deltaF = (float)delta;

        // 根据模式更新角度
        switch (CurrentMode)
        {
            case MovementMode.Circular:
                // 匀速圆周运动：角速度 = 角度转弧度 * 方向（顺时针+1/逆时针-1）
                float angularVel = Mathf.DegToRad(MoveSpeed) * (IsClockwise ? 1 : -1);
                _currentAngleRad += angularVel * deltaF;
                break;

            case MovementMode.Pendulum:
                // 单摆简谐运动：围绕平衡位置左右摆动
                _timeAccumulator += deltaF;
                float maxAngleRad = Mathf.DegToRad(MaxPendulumAngle);
                float omega = Mathf.DegToRad(MoveSpeed); // 摆动频率
                _currentAngleRad = _balanceAngleRad + maxAngleRad * Mathf.Sin(_timeAccumulator * omega);
                break;
        }

        // 计算球的新位置 + 同步Line2D绳体末端
        Vector2 currentDir = Vector2.FromAngle(_currentAngleRad);
        Vector2 newBallPos = _pivotGlobalPos + currentDir * _ropeLength;
        
        GlobalPosition = newBallPos;
        _ropeLine.SetPointPosition(1, _ropeLine.ToLocal(newBallPos)); // 同步Line2D第二个顶点位置
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player player)
        {
            player.TakeDamage(1);
        }
    }
}