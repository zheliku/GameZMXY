using System;
using System.Linq;
using GameLogic.Heroes;
using GameLogic.Input;
using Godot;

namespace GameLogic.Level;

/// <summary>
/// 清场后开放的出口；英雄落地靠近并按本人交互键才提交通关请求，节点随关卡卸载。
/// </summary>
public partial class LevelExitNode : Area2D
{
    private static readonly StringName s_exitAnimation = "exit"; // 出口资源中的循环光标动画。
    [Export] private AnimatedSprite2D _marker; // 外置资源中的循环出口光标。
    [Export] private Label _prompt; // 靠近时显示的按键提示。

    /// <summary>
    /// 报告实际接触并交互的英雄；关卡接受请求后停用出口。
    /// </summary>
    public event Action<AbstractHeroEntity> Entered;

    /// <summary>
    /// 加载期禁用出口及输入。
    /// </summary>
    public override void _Ready() => Disable();

    /// <summary>
    /// 玩家靠近时显示提示；暂停时由场景树一起暂停。
    /// </summary>
    /// <param name="delta">物理帧秒数。</param>
    public override void _PhysicsProcess(double delta) => _prompt.Visible = Monitoring &&
        GetOverlappingBodies().Any(body => body is AbstractHeroEntity { IsAlive: true, OnFloor: true });

    /// <summary>
    /// 只接受靠近、落地、存活英雄的本人单独交互键，拒绝同席位动作组合与按住自动重复。
    /// </summary>
    /// <param name="input">尚未被界面消费的输入。</param>
    public override void _UnhandledInput(InputEvent input)
    {
        if (!Monitoring || input.IsEcho())
        {
            return;
        }

        // 逐个检查实际交互者，组合键必须与请求者属于同一席位。
        foreach (Node2D body in GetOverlappingBodies())
        {
            if (body is not AbstractHeroEntity { IsAlive: true, OnFloor: true } hero ||
                !input.IsActionPressed(InputSystem.ActionOf(InputSystem.Interact, hero.Slot)))
            {
                continue;
            }

            if (Godot.Input.IsActionPressed(InputSystem.ActionOf(InputSystem.Attack, hero.Slot)) ||
                Godot.Input.IsActionPressed(InputSystem.ActionOf(InputSystem.Jump, hero.Slot)) ||
                Godot.Input.IsActionPressed(InputSystem.ActionOf(InputSystem.MoveDown, hero.Slot)))
            {
                continue;
            }

            GetViewport().SetInputAsHandled();
            Entered?.Invoke(hero);
            return;
        }
    }

    /// <summary>
    /// 最后一波清场后显示光标并启用局部交互检测。
    /// </summary>
    public void Enable()
    {
        SetDeferred(Area2D.PropertyName.Monitoring, true);
        Visible = true;
        _marker.Play(s_exitAnimation);
        SetPhysicsProcess(true);
        SetProcessUnhandledInput(true);
    }

    /// <summary>
    /// 清理会话或已提交通关时停止光标与输入。
    /// </summary>
    public void Disable()
    {
        SetDeferred(Area2D.PropertyName.Monitoring, false);
        Visible = false;
        _marker.Stop();
        _prompt.Visible = false;
        SetPhysicsProcess(false);
        SetProcessUnhandledInput(false);
    }

}
