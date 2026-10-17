using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Skills;
using GameConfig.Sound;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using GameLogic.Config;
using GameLogic.Heroes.Body;
using GameLogic.Heroes.Effects;
using GameLogic.Monsters;
using GameLogic.Skills;
using GameLogic.Input;
using Godot;
using GodotGameFramework;
using GodotGameFramework.NodePool;

namespace GameLogic.Heroes;

/// <summary>英雄技能表现：手动动画、身体攻击包和独立特效的所有权。</summary>
public abstract partial class AbstractHeroEntity : ISkillActor
{
    // ---- 属性 ----

    /// <summary>世界位置是物理表现，不进入技能存档。</summary>
    Vector2 ISkillActor.SkillPosition { get => GlobalPosition; set => GlobalPosition = value; }

    /// <summary>具体英雄匹配自己的技能组合，不向公共输入或身体状态增加专属分支。</summary>
    /// <param name="attacking">是否正在普攻。</param>
    /// <param name="normalRatio">当前普攻帧比例。</param>
    /// <param name="airborne">是否处于空中。</param>
    public abstract void ProcessSkillInput(bool attacking, float normalRatio, bool airborne);

    /// <summary>默认按共用普攻输入和冷却起手；特殊组合由具体角色处理。</summary>
    /// <param name="attacking">是否正在普攻。</param>
    /// <param name="normalRatio">当前普攻帧比例。</param>
    /// <param name="airborne">是否处于空中。</param>
    /// <returns>成功选择普攻段时为真。</returns>
    public virtual bool TryStartAttack(bool attacking, float normalRatio, bool airborne) =>
        this.GetSystem<InputSystem>().TryStartAttack(ActionData, ((IHeroBody)this).Params, airborne, attacking);

    // ---- 技能表现接口 ----

    /// <summary>接管动画的局部时间，起手声音由动作表配置。</summary>
    /// <param name="action">借用动作定义。</param>
    public void BeginSkillAnimation(SkillActionConfig action)
    {
        ReleaseAttack();
        AnimPlayer.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        RestartAnim(action.Animation);
        PlaySound(action.SoundId);
    }

    /// <summary>应用角色时间轴并补齐持续重叠的多段命中。</summary>
    /// <param name="seconds">技能局部秒数。</param>
    void ISkillActor.SetSkillAnimationTime(double seconds)
    {
        AnimPlayer.Seek(seconds, true);
        PollAttackOverlaps();
    }

    /// <summary>收招或中断后恢复物理帧驱动，不销毁仍播放中的独立特效。</summary>
    public virtual void EndSkillAnimation()
    {
        ReleaseAttack();
        AnimPlayer.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Physics;
    }

    /// <summary>按技能配置生成新攻击编号，威力经原版取整及无双倍率计算。</summary>
    /// <param name="hit">伤害定义编号。</param>
    void ISkillActor.NewSkillAttack(SkillHitId hit)
    {
        if (hit == SkillHitId.None)
            return;
        var definition = this.GetModel<ConfigurationModel>().Tables.TbSkillHitConfig.Get(hit);
        var attributes = CaptureCombatAttributes();
        SetAttack(AttackInstance.Create((int)hit, attributes,
            SkillSystem.CalculatePower(definition, attributes.Power,
                this.GetSystem<HeroSystem>().AttackPowerMultiplier),
            definition.DamageKind, definition.Knockback, Facing, 0, SoundId.None));
    }

    /// <summary>从框架当前怪物组查找首个可见目标，不穿透场景树。</summary>
    /// <param name="range">最大欧氏距离，像素。</param>
    /// <param name="inset">视口左右留白，像素。</param>
    /// <returns>满足条件的世界位置；没有则为空。</returns>
    Vector2? ISkillActor.FindSkillTarget(float range, float inset)
    {
        Transform2D canvas = GetViewport().GetCanvasTransform();
        float width = GetViewportRect().Size.X;
        foreach (var group in GF.Entity.GetAllEntityGroups())
            foreach (var monster in group.GetAllEntities().OfType<AbstractMonsterEntity>())
            {
                if (!monster.IsAlive || GlobalPosition.DistanceTo(monster.GlobalPosition) > range)
                    continue;
                var bounds = monster.HurtBox.GetGlobalBounds();
                float left = (canvas * bounds.Position).X;
                if (left > inset && left < width - inset)
                    return monster.GlobalPosition;
            }

        return null;
    }

    /// <summary>从正式对象池创建独立特效，与角色节点相邻放置以保持世界坐标。</summary>
    /// <param name="effect">特效编号。</param>
    /// <param name="execution">施放身份。</param>
    /// <param name="position">世界位置。</param>
    /// <param name="completed">本次效果完成后通知所属技能。</param>
    /// <exception cref="InvalidOperationException">技能场景未登记到对象池。</exception>
    void ISkillActor.SpawnSkillEffect(SkillEffectId effect, SkillExecution execution, Vector2 position, Action completed)
    {
        var definition = this.GetModel<ConfigurationModel>().Tables.TbSkillEffectConfig.Get(effect);
        var node = NodePool.Instance.Get<SkillEffectNode>(definition.ScenePath, GetParent()) ??
                   throw new InvalidOperationException($"技能特效未能取池：{effect}");
        m_SkillEffects.Add(node);
        node.Show(definition, execution, position, Facing, CaptureCombatAttributes(),
            this.GetSystem<HeroSystem>().AttackPowerMultiplier, ReleaseSkillEffect, completed);
    }

    // ---- 私有回收方法 ----

    /// <summary>移除本英雄的表现所有权后归还对象池。</summary>
    /// <param name="effect">已经完成的活动特效。</param>
    private void ReleaseSkillEffect(SkillEffectNode effect)
    {
        if (m_SkillEffects.Remove(effect))
            NodePool.Instance.Release(effect);
    }

    /// <summary>隐藏英雄时回收所有活动特效；关停不访问已释放的节点池。</summary>
    /// <param name="shutdown">是否框架关停。</param>
    private void ClearSkillEffects(bool shutdown)
    {
        if (!shutdown)
        {
            foreach (var effect in m_SkillEffects.ToArray())
                ReleaseSkillEffect(effect);
            this.GetSystem<SkillSystem>().StopAll(this);
        }

        m_SkillEffects.Clear();
    }
}
