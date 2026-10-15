using System;
using GameLogic.Heroes;
using GameLogic.Input;
using Godot;

namespace GameLogic.Level;

/// <summary>清场后开放的出口；英雄落地靠近并按 W 才提交通关请求，节点随关卡卸载。</summary>
public partial class LevelExitNode : Area2D
{
    [Export] private AnimatedSprite2D m_Marker; // 外置资源中的循环出口光标。
    [Export] private Label m_Prompt; // 靠近时显示的按键提示。

    /// <summary>报告实际接触并交互的英雄；关卡接受请求后停用出口。</summary>
    public event Action<AbstractHeroEntity> Entered;

    /// <summary>加载期禁用出口及输入。</summary>
    public override void _Ready() => Disable();

    /// <summary>最后一波清场后显示光标并启用局部交互检测。</summary>
    public void Enable()
    {
        SetDeferred(Area2D.PropertyName.Monitoring, true);
        Visible = true;
        m_Marker.Play("exit");
        SetPhysicsProcess(true);
        SetProcessUnhandledInput(true);
    }

    /// <summary>清理会话或已提交通关时停止光标与输入。</summary>
    public void Disable()
    {
        SetDeferred(Area2D.PropertyName.Monitoring, false);
        Visible = false;
        m_Marker.Stop();
        m_Prompt.Visible = false;
        SetPhysicsProcess(false);
        SetProcessUnhandledInput(false);
    }

    /// <summary>玩家靠近时显示提示；暂停时由场景树一起暂停。</summary>
    /// <param name="delta">物理帧秒数。</param>
    public override void _PhysicsProcess(double delta) => m_Prompt.Visible = FindEligibleHero() != null;

    /// <summary>只接受靠近、落地、存活玩家的单独 W，拒绝动作组合与按住自动重复。</summary>
    /// <param name="input">尚未被界面消费的输入。</param>
    public override void _UnhandledInput(InputEvent input)
    {
        if (!input.IsActionPressed(InputSystem.Interact) || input.IsEcho())
            return;
        var hero = FindEligibleHero();
        if (hero == null)
            return;
        // 造一只把 S/J/K/W 中的单独 W 当作出口输入，W＋J 等组合属于动作输入。
        if (Godot.Input.IsActionPressed(InputSystem.Attack) || Godot.Input.IsActionPressed(InputSystem.Jump) ||
            Godot.Input.IsActionPressed(InputSystem.MoveDown))
            return;
        GetViewport().SetInputAsHandled();
        Entered?.Invoke(hero);
    }

    /// <summary>从真实物理重叠和角色状态判断交互条件。</summary>
    /// <returns>范围内存活且落地的英雄，没有则返回空。</returns>
    private AbstractHeroEntity FindEligibleHero()
    {
        if (!Monitoring)
            return null;
        foreach (Node2D body in GetOverlappingBodies())
            if (body is AbstractHeroEntity { IsAlive: true, OnFloor: true } hero)
                return hero;
        return null;
    }
}
