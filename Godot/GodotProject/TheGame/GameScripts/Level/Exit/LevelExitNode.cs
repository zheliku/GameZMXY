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
    private HeroEntity m_Hero; // 由关卡注入的本会话英雄，停用时释放。

    /// <summary>有效交互触发一次；关卡控制器负责状态迁移。</summary>
    public event Action Entered;

    /// <summary>加载期禁用出口及输入。</summary>
    public override void _Ready() => Disable();

    /// <summary>最后一波清场后显示光标并允许本会话英雄使用。</summary>
    /// <param name="hero">本关玩家。</param>
    public void Enable(HeroEntity hero)
    {
        m_Hero = hero;
        Visible = true;
        m_Marker.Play("exit");
        SetPhysicsProcess(true);
        SetProcessUnhandledInput(true);
    }

    /// <summary>清理会话或已提交通关时停止光标与输入。</summary>
    public void Disable()
    {
        m_Hero = null;
        Visible = false;
        m_Marker.Stop();
        m_Prompt.Visible = false;
        SetPhysicsProcess(false);
        SetProcessUnhandledInput(false);
    }

    /// <summary>玩家靠近时显示提示；暂停时由场景树一起暂停。</summary>
    /// <param name="delta">物理帧秒数。</param>
    public override void _PhysicsProcess(double delta) => m_Prompt.Visible = CanEnter();

    /// <summary>只接受靠近、落地、存活玩家的新按键，拒绝按住自动重复。</summary>
    /// <param name="input">尚未被界面消费的输入。</param>
    public override void _UnhandledInput(InputEvent input)
    {
        if (!input.IsActionPressed(InputActions.Interact) || input.IsEcho() || !CanEnter()) return;
        GetViewport().SetInputAsHandled();
        Disable();
        Entered?.Invoke();
    }

    /// <summary>从真实物理重叠和角色状态判断交互条件。</summary>
    /// <returns>玩家是否可使用当前出口。</returns>
    private bool CanEnter() => IsInstanceValid(m_Hero) && m_Hero.IsAlive && m_Hero.OnFloor &&
                               GetOverlappingBodies().Contains(m_Hero);
}
