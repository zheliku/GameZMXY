using System;
using System.Collections.Generic;
using GameConfig.Constant;
using GameFramework.Event;
using GameLogic.Event.Combat;
using Godot;
using GodotGameFramework;
using GodotGameFramework.NodePool;

namespace GameLogic.FloatingText;

/// <summary>随战斗场景存在的飘字入口；统一显示伤害、闪避和恢复量，只回收本会话取出的节点。</summary>
public partial class FloatingTextModule : Node2D
{
    private readonly HashSet<FloatingTextNode> m_Texts = new(); // 本会话取出的飘字；所有样式共用同一个池。
    private EventComponent m_Events; // 开始会话时捕获的 GGF 事件服务。
    private NodePool m_Pool; // 开始会话时捕获的节点池，结束时仍使用同一服务。
    private Func<int, bool> m_OwnsTarget; // 拒绝其他战斗场景或旧会话的伤害事件。
    private bool m_SessionActive; // 只在显式开始后接收反馈。

    /// <summary>开始接收本场战斗的伤害事件；初始化和挂树本身不会订阅。</summary>
    /// <param name="ownsTarget">按运行实体编号判断受击方是否属于本会话，结束后不再调用。</param>
    /// <exception cref="ArgumentNullException">所有权判断为空。</exception>
    /// <exception cref="InvalidOperationException">组件未挂树或会话已经开始。</exception>
    public void StartSession(Func<int, bool> ownsTarget)
    {
        ArgumentNullException.ThrowIfNull(ownsTarget);
        if (!IsInsideTree() || m_SessionActive)
            throw new InvalidOperationException("飘字组件必须挂树，且同一时间只能运行一个会话。");

        // 开始边界捕获服务并建立订阅，不在构造或资源加载阶段监听玩法。
        m_Events = GF.Event;
        m_Pool = NodePool.Instance;
        m_OwnsTarget = ownsTarget;
        m_Events.Subscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
        m_SessionActive = true;
    }

    /// <summary>退订并归还本会话仍在显示的全部飘字；可重复调用，不影响其他组件使用的同类池。</summary>
    public void StopSession()
    {
        if (!m_SessionActive)
            return;
        m_SessionActive = false;
        m_Events.Unsubscribe(DamageDealtEventArgs.EventId, OnDamageDealt);

        // 逐个归还拥有的实例；OnRelease 会取消动画和完成回调，不能使用全池 ReleaseAll。
        foreach (FloatingTextNode text in m_Texts)
            m_Pool.Release(text);
        m_Texts.Clear();
        m_OwnsTarget = null;
        m_Events = null;
        m_Pool = null;
    }

    /// <summary>显示一次已经发生的恢复反馈；拾取、技能等调用方负责实际恢复生命或魔法。</summary>
    /// <param name="position">目标角色的世界原点，显示后不跟随角色移动。</param>
    /// <param name="amount">正数显示量；拾取保留官方显示配置量的行为，满值时仍可提示。</param>
    /// <param name="kind">绿色生命或蓝色魔法样式。</param>
    /// <exception cref="InvalidOperationException">会话未开始或节点池资源未注册。</exception>
    public void ShowRecovery(Vector2 position, int amount, RecoveryKind kind)
    {
        if (!m_SessionActive)
            throw new InvalidOperationException("开始飘字会话后才能显示恢复量。");
        if (amount <= 0)
            return;
        TakeText().ShowRecovery(position, amount, kind, OnFinished);
    }

    /// <summary>拥有者正常结束会话之外，场景退出仍保证解除订阅并归还节点。</summary>
    public override void _ExitTree() => StopSession();

    /// <summary>只消费本会话实体的伤害快照，不保留池化参数或实体引用。</summary>
    /// <param name="sender">事件发布方，不使用。</param>
    /// <param name="args">只在当前分发期间有效的伤害结果。</param>
    private void OnDamageDealt(object sender, GameEventArgs args)
    {
        if (!m_SessionActive || args is not DamageDealtEventArgs e || !m_OwnsTarget(e.TargetEntityId))
            return;
        if (!e.Result.IsMiss && e.Result.Damage <= 0)
            return;

        // 命中与闪避复用同一场景，显示节点只报告完成，所有权和回池由本模块维护。
        var pop = TakeText();
        if (e.Result.IsMiss)
            pop.ShowMiss(e.PopPosition, OnFinished);
        else
            pop.ShowDamage(e.PopPosition, e.Result.Damage, e.Result.Kind, e.Result.IsCrit, e.TargetIsHero,
            OnFinished);
    }

    /// <summary>从唯一飘字池取出节点并登记本会话所有权。</summary>
    /// <returns>可以显示任意飘字样式的实例。</returns>
    /// <exception cref="InvalidOperationException">飘字场景未注册。</exception>
    private FloatingTextNode TakeText()
    {
        var text = m_Pool.Get<FloatingTextNode>(ResourcesCollectionConstant.Effects_floating_text, this)
                   ?? throw new InvalidOperationException("通用飘字节点池未注册。");
        m_Texts.Add(text);
        return text;
    }

    /// <summary>显示完成后仅归还本模块仍拥有的提示，避免重复归还。</summary>
    /// <param name="pop">已经停止处理的提示节点。</param>
    private void OnFinished(FloatingTextNode pop)
    {
        if (m_Texts.Remove(pop))
            m_Pool.Release(pop);
    }
}
