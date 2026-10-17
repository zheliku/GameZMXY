using System;
using System.Collections.Generic;
using GameConfig.Skills;
using GameConfig.Sound;
using GameFramework;
using GameLogic.Architecture;
using GameLogic.Audio;
using GameLogic.Battle;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using GameLogic.Config;
using GameLogic.Entity;
using GameLogic.Skills;
using Godot;
using GodotGameFramework;
using GodotGameFramework.NodePool;

namespace GameLogic.Heroes.Effects;

/// <summary>独立于角色身体播放的池化技能特效；拥有自己的攻击编号、总命中预算和局部时间轴。</summary>
public partial class SkillEffectNode : Node2D, IPoolable, IController
{
    // ---- 字段 ----

    private AnimationPlayer m_Player; // 外置动画只驱动显示及几何，规则在技能表。
    private Area2D m_HitBox; // 本特效独立的判定区。
    private CollisionShape2D m_Shape; // 当前动画帧写入的判定开关。
    private SkillEffectConfig m_Definition; // 借用本次特效定义。
    private SkillHitConfig m_Hit; // 借用伤害定义；无伤害特效为空。
    private IReadOnlyList<SkillTimelineConfig> m_Events; // 启动期建立的局部事件序列。
    private CombatantAttributes m_Attacker; // 特效生成时冻结的英雄属性。
    private AttackData m_AttackData; // 当前攻击编号对应的池化攻击包。
    private SkillData m_SkillData; // 验证效果回调仍属于同一次执行。
    private Action m_Completed; // 可选的本次效果完成通知，不保存角色节点。
    private Action<SkillEffectNode> m_Finished; // 英雄释放本次表现所有权。
    private int m_Direction, m_NextEvent, m_RemainingHits; // 朝向、下一个事件、整个特效的剩余命中数。
    private float m_PowerMultiplier; // 生成时的无双倍率。
    private double m_Age, m_RepeatAt; // 局部游戏秒数及下一次可更新攻击编号的时刻。

    // ---- 节点生命周期 ----

    /// <summary>收集固定场景引用并只连接一次判定回调。</summary>
    public override void _Ready()
    {
        m_Player = GetNode<AnimationPlayer>("AnimationPlayer");
        m_HitBox = GetNode<Area2D>("HitBox");
        m_Shape = GetNode<CollisionShape2D>("HitBox/CollisionShape2D");
        m_Player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        m_HitBox.AreaEntered += OnAreaEntered;
        SetPhysicsProcess(false);
    }

    /// <summary>按游戏时间更新动画、攻击编号和命中；暂停时不推进。</summary>
    /// <param name="delta">物理游戏秒数。</param>
    public override void _PhysicsProcess(double delta)
    {
        if (delta <= 0 || m_Definition == null)
            return;
        if (m_Hit != null && m_RemainingHits <= 0)
        {
            Finish();
            return;
        }

        m_Age += delta;
        while (m_NextEvent < m_Events.Count && m_Events[m_NextEvent].AtSeconds <= m_Age + .0001)
        {
            var entry = m_Events[m_NextEvent++];
            if (entry.Kind == SkillTimelineKind.NewAttackId)
                NewAttack();
            else if (entry.Kind is SkillTimelineKind.CompleteEffect or SkillTimelineKind.FinishLandingEffect)
            {
                Finish();
                return;
            }
        }

        if (m_Age >= m_Player.GetAnimation("play").Length)
        {
            Finish();
            return;
        }

        m_Player.Seek(m_Age, true);
        if (m_Hit != null && m_Age >= m_RepeatAt)
            NewAttack();
        // 新编号需要处理仍然重叠的受击盒，AreaEntered 只报告初次进入。
        if (!m_Shape.Disabled && m_RemainingHits > 0)
            foreach (var area in m_HitBox.GetOverlappingAreas())
                OnAreaEntered(area);
    }

    // ---- 接口实现 ----

    /// <summary>借用当前流程拥有的架构。</summary>
    /// <returns>流程 FSM 的架构视图。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>取池后等待正式参数。</summary>
    public void OnGet() => SetPhysicsProcess(false);

    /// <summary>回池清理攻击包、回调和执行引用，防止复用串招。</summary>
    public void OnRelease()
    {
        SetPhysicsProcess(false);
        if (m_AttackData != null)
            ReferencePool.Release(m_AttackData);
        m_AttackData = null;
        m_Definition = null;
        m_Hit = null;
        m_SkillData = null;
        m_Events = null;
        m_Finished = null;
        m_Completed = null;
        m_Player.Stop();
        m_Shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        Visible = false;
    }

    // ---- 业务方法 ----

    /// <summary>显示一份新的特效；世界位置固定，不跟随角色后续移动。</summary>
    /// <param name="definition">特效定义。</param>
    /// <param name="data">当前施放身份。</param>
    /// <param name="position">世界位置。</param>
    /// <param name="direction">英雄朝向。</param>
    /// <param name="attacker">英雄属性快照。</param>
    /// <param name="powerMultiplier">无双倍率。</param>
    /// <param name="finished">完成后的回收回调。</param>
    /// <param name="completed">本次规则效果的完成通知。</param>
    public void Show(SkillEffectConfig definition, SkillData data, Vector2 position, int direction,
        CombatantAttributes attacker, float powerMultiplier, Action<SkillEffectNode> finished, Action completed = null)
    {
        m_Definition = definition;
        m_SkillData = data;
        m_Attacker = attacker;
        m_Direction = direction;
        m_PowerMultiplier = powerMultiplier;
        m_Finished = finished;
        m_Completed = completed;
        m_Events = this.GetModel<ConfigurationModel>().GetSkillTimeline(definition.SkillId, definition.Id);
        m_Hit = definition.HitId == SkillHitId.None
            ? null
            : this.GetModel<ConfigurationModel>().Tables.TbSkillHitConfig.Get(definition.HitId);
        m_RemainingHits = m_Hit?.HitBudget ?? 0;
        m_Age = m_NextEvent = 0;
        m_RepeatAt = double.PositiveInfinity;
        Vector2 scale = powerMultiplier > 1 ? definition.MusouScale : definition.Scale;
        // 源素材朝左；特效按表指定与角色同向或反向镜像，位移也一同镜像。
        scale.X *= (direction > 0 ? -1 : 1) * (definition.Mirror == SkillEffectMirror.Actor ? 1 : -1);
        Scale = scale;
        GlobalPosition = position + new Vector2(definition.SpawnOffset.X * direction, definition.SpawnOffset.Y);
        m_Player.Stop();
        m_Player.Play("play");
        m_Player.Seek(0, true);
        if (m_Hit != null)
            NewAttack();
        Visible = true;
        SetPhysicsProcess(true);
        GF.Sound.PlayConfigured(definition.SoundId);
    }

    /// <summary>创建独立攻击编号；不重置整个特效的命中预算。</summary>
    private void NewAttack()
    {
        if (m_AttackData != null)
            ReferencePool.Release(m_AttackData);
        m_AttackData = m_Hit == null
            ? null
            : AttackData.Create((int)m_Hit.Id, m_Attacker,
                SkillSystem.CalculatePower(m_Hit, m_Attacker.Power, m_PowerMultiplier), m_Hit.DamageKind,
                m_Hit.Knockback, m_Direction, 0, SoundId.None);
        m_RepeatAt = double.PositiveInfinity;
    }

    /// <summary>按当前编号去重并累计整段特效的有效命中。</summary>
    /// <param name="area">进入或持续重叠的区域。</param>
    private void OnAreaEntered(Area2D area)
    {
        if (m_AttackData == null || m_RemainingHits <= 0 || m_Shape.Disabled ||
            area is not HurtBoxNode { OwnerEntity: { IsAlive: true, Side: CombatSide.Monster } target } ||
            !m_AttackData.TryRegisterHit(target.GetInstanceId()))
            return;
        var result = target.ReceiveHit(m_AttackData, m_Attacker.ActorId.Value);
        if (result.IsMiss)
            return;
        m_RemainingHits--;
        m_RepeatAt = Math.Min(m_RepeatAt, m_Age + m_Hit.RepeatIntervalSeconds);
        this.GetSystem<HeroSystem>().RecordHit(result.Damage, m_Attacker.Power);
    }

    /// <summary>完成后由英雄统一移除并归还对象池。</summary>
    private void Finish()
    {
        if (this.GetSystem<SkillSystem>().IsActive(m_SkillData))
            m_Completed?.Invoke();
        m_Finished(this);
    }
}
