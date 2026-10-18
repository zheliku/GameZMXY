using System;
using System.Linq;
using GameConfig.Skills;
using GameConfig.Sound;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attacks;
using GameLogic.Config;
using GameLogic.Heroes.Body;
using GameLogic.Heroes.Effects;
using GameLogic.Monsters;
using GameLogic.Skills;
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

    /// <summary>身体状态机当前真正执行的技能，供具体角色连接动画方法。</summary>
    protected AbstractHeroSkillState CurrentSkillState => m_BodyFsm?.CurrentState as AbstractHeroSkillState;

    // ---- 技能输入 ----

    /// <summary>角色在身体允许的输入窗口匹配技能和普攻；只登记请求，实际转换归状态机。</summary>
    /// <param name="attacking">当前是否处于普攻。</param>
    /// <param name="normalProgress">本段普攻的播放比例。</param>
    /// <param name="airborne">当前是否处于空中。</param>
    protected abstract void MatchActionInput(bool attacking, float normalProgress, bool airborne);

    // ---- 技能表现接口 ----

    /// <summary>播放原生技能动画及方法轨道，跳阶段使用动画自身的命名标记。</summary>
    /// <param name="animation">完整动画名。</param>
    /// <param name="startMarker">可选的阶段标记。</param>
    public void BeginSkillAnimation(string animation, string startMarker = null)
    {
        ReleaseAttack();
        AnimPlayer.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        AnimPlayer.CallbackModeMethod = AnimationMixer.AnimationCallbackModeMethod.Immediate;
        AnimPlayer.Stop();
        if (startMarker == null)
            AnimPlayer.Play(animation);
        else
            AnimPlayer.PlaySectionWithMarkers(animation, startMarker);
        AnimPlayer.Advance(0);
    }

    /// <summary>原生推进负责方法键顺序，判定仍重叠时补齐新攻击包的命中。</summary>
    /// <param name="dt">游戏秒数。</param>
    void ISkillActor.AdvanceSkillAnimation(float dt)
    {
        AnimPlayer.Advance(dt);
        PollAttackOverlaps();
    }

    /// <summary>收招或中断后释放判定并恢复常规动画，不销毁独立效果。</summary>
    public void EndSkillAnimation()
    {
        ReleaseAttack();
        RestartAnim(HeroAnims.Reset);
        AnimPlayer.Advance(0);
        AnimPlayer.Stop();
        AnimPlayer.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Physics;
    }

    /// <summary>具体角色的原生方法轨道请求收招；状态在轨道遍历返回后执行迁移。</summary>
    protected void CompleteSkillAnimationAnimEvent() => CurrentSkillState?.CompleteAnimation();

    /// <summary>接收具体技能已计算的基础威力，再应用本次无双倍率。</summary>
    /// <param name="hit">统一伤害表的技能编号。</param>
    /// <param name="power">无双前威力。</param>
    /// <param name="knockbackOriginX">具体招式指定的击退中心。</param>
    void ISkillActor.NewSkillAttack(SkillHitId hit, double power, float? knockbackOriginX)
    {
        var definition = this.GetModel<ConfigurationModel>().Tables.TbAttackConfig.Get((int)hit);
        SetAttack(AttackData.Create((int)hit, CaptureCombatAttributes(),
            (int)(power * this.GetSystem<HeroSystem>().AttackPowerMultiplier),
            definition.DamageKind, definition.Knockback, Facing, definition.HitProtect,
            SoundId.None, knockbackOriginX));
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

    /// <summary>从动画引用的正式场景取池，表现参数留在可预览的场景中。</summary>
    /// <param name="scene">原生方法键直接引用的场景。</param>
    /// <param name="position">世界位置。</param>
    /// <param name="hit">可选的伤害阶段。</param>
    /// <param name="power">已按本招规则取整的威力。</param>
    /// <param name="completed">本次效果完成通知。</param>
    /// <param name="knockbackOriginX">本次击退中心。</param>
    /// <exception cref="InvalidOperationException">场景未登记到框架节点池。</exception>
    void ISkillActor.SpawnSkillEffect(PackedScene scene, Vector2 position,
        SkillHitId hit, double power, Action completed, float? knockbackOriginX)
    {
        var node = NodePool.Instance.Get<SkillEffectNode>(scene.ResourcePath, GetParent()) ??
            throw new InvalidOperationException($"技能特效未登记到节点池：{scene.ResourcePath}");
        m_SkillEffects.Add(node);
        float multiplier = this.GetSystem<HeroSystem>().AttackPowerMultiplier;
        node.Show(hit == SkillHitId.None ? null :
                this.GetModel<ConfigurationModel>().Tables.TbAttackConfig.Get((int)hit),
            position, Facing, CaptureCombatAttributes(), (int)(power * multiplier),
            this.GetModel<HeroModel>().WsActive.Value, ReleaseSkillEffect, completed, knockbackOriginX);
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
            this.GetSystem<SkillSystem>().CancelSkill();
        }

        m_SkillEffects.Clear();
    }
}
