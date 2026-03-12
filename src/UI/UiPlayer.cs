using Godot;

public partial class UiPlayer : CanvasLayer 
{
    [Export] public Player Player;                
    [Export] public ColorRect HealthBarBg;        
    [Export] public ColorRect HealthBarFill;      

    public override void _Ready()
    {
        if (Player == null)
        {
            Player = GetNodeOrNull<Player>("/root/Root/Player"); 
        }        

        if (Player == null || HealthBarBg == null || HealthBarFill == null)
        {
            GD.PrintErr("请绑定 Player/HealthBarBg/HealthBarFill 节点！");
            return;
        }

        Player.HealthChanged += OnHealthChanged;
    }

    private void OnHealthChanged(int currentHealth, int maxHealth)
    {
        float healthRatio = Mathf.Clamp((float)currentHealth / maxHealth, 0f, 1f);
        HealthBarFill.Size = new Vector2(HealthBarBg.Size.X * healthRatio, HealthBarFill.Size.Y);
    }

    public override void _ExitTree()
    {
        if (Player != null)
        {
            Player.HealthChanged -= OnHealthChanged;
        }
    }
}