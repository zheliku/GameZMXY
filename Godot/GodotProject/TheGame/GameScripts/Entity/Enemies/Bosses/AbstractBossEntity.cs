using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Effects;
using Godot;
using GodotGameFramework.NodePool;

namespace GameLogic.Enemies;

/// <summary>首领分支拥有独立攻击效果的生命周期；隐藏时统一回收，专属选招与无敌由具体首领实现。</summary>
public abstract partial class AbstractBossEntity : AbstractEnemyEntity
{
    private readonly HashSet<SkillEffectNode> m_SkillEffects = new(); // 本次显示借出的独立攻击效果。

    /// <summary>取得并登记原生动画引用的独立效果；之后由首领提供该招数值与位置。</summary>
    /// <param name="scene">已登记到 GGF 节点池的场景。</param>
    /// <returns>本首领负责归还的效果。</returns>
    /// <exception cref="InvalidOperationException">场景未登记到节点池。</exception>
    protected SkillEffectNode CreateSkillEffect(PackedScene scene)
    {
        var effect = NodePool.Instance.Get<SkillEffectNode>(scene.ResourcePath, GetParent()) ??
            throw new InvalidOperationException($"首领技能未登记到节点池：{scene.ResourcePath}");
        m_SkillEffects.Add(effect);
        return effect;
    }

    /// <summary>效果完成时解除所有权并归还，只处理本首领仍拥有的实例。</summary>
    /// <param name="effect">已完成效果。</param>
    protected void ReleaseSkillEffect(SkillEffectNode effect)
    {
        if (m_SkillEffects.Remove(effect))
            NodePool.Instance.Release(effect);
    }

    /// <summary>首领回收时清理独立效果，关停时由节点池统一释放。</summary>
    /// <param name="isShutdown">框架是否关停。</param>
    /// <param name="userData">GGF 隐藏参数。</param>
    public override void OnHide(bool isShutdown, object userData)
    {
        if (!isShutdown)
            foreach (var effect in m_SkillEffects.ToArray())
                ReleaseSkillEffect(effect);
        m_SkillEffects.Clear();
        base.OnHide(isShutdown, userData);
    }
}
