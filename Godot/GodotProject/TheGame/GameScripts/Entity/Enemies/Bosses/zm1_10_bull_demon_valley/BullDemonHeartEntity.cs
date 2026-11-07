using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Battle;
using GameFramework;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using GameLogic.Entity;
using Godot;
using GodotGameFramework.NodePool;

namespace GameLogic.Enemies;

/// <summary>
/// 牛魔王最终形态固定在场中，心脏显隐控制受伤，双掌与火柱共用父攻击编号。
/// </summary>
public partial class BullDemonHeartEntity : AbstractBossEntity
{
    private static readonly EnemyBodyPreset s_bodyPreset = new(typeof(BullDemonHeartState),
        typeof(BullDemonHeartDeathState),
        () => new BullDemonHeartState(), () => new BullDemonHeartDeathState()); // 本首领的状态组合；运行实例由每次入场独立创建。
    [Export] private AnimatedSprite2D _heart; // 保留父级柔光和原点的23帧独立心脏。
    [Export] private AnimationPlayer _heartFade; // 原生show/hide动画负责两秒淡入淡出。
    [Export] private AnimationPlayer _presentation; // 原生intro与death负责背景进入和完整退场。
    [Export] private BullDemonHandNode _leftHand; // 本首领拥有的左手局部运动。
    [Export] private BullDemonHandNode _rightHand; // 本首领拥有的镜像右手。
    [Export] private PackedScene _fireScene; // 通过GGF节点池显示的官方火柱。
    private readonly HashSet<BullDemonFireNode> _fires = new(); // 本次显示拥有的火柱，离场逐个回收。
    private readonly HashSet<ulong> _hitTargets = new(); // 两只手和全部火柱共同使用的当前攻击目标去重。
    private AttackData _handAttackData; // 本次父攻击编号的物理掌击快照。
    private AttackData _fireAttackData; // 与掌击共用目标去重的魔法火柱快照。

    /// <inheritdoc />
    protected override EnemyBodyPreset BodyPreset => s_bodyPreset;

    /// <inheritdoc />
    protected override EnemyAiPreset AiPreset => null;

    /// <inheritdoc />
    protected override bool Invulnerable => _heart.Modulate.A < 1f;

    /// <summary>
    /// 当前淡入或淡出是否仍由原生播放器执行。
    /// </summary>
    internal bool HeartFading => _heartFade.IsPlaying();

    /// <summary>
    /// 原生背景进入时长，身体状态不另写一套视觉秒数。
    /// </summary>
    internal float IntroDuration => (float)_presentation.GetAnimation("intro").Length;

    /// <summary>
    /// 原生整体退场时长。
    /// </summary>
    internal float DeathDuration => (float)_presentation.GetAnimation("death").Length;

    /// <summary>
    /// 入场启动背景和心脏，双手等背景完整出现后开始。
    /// </summary>
    internal void BeginPresentation()
    {
        _leftHand.Stop();
        _rightHand.Stop();
        _presentation.Play("RESET");
        _presentation.Advance(0);
        _presentation.Stop();
        // Stop会恢复旧淡出动画的起点alpha=1，须先停旧周期，再设置本次初始隐藏。
        _heartFade.Stop();
        _heart.Modulate = new Color(1, 1, 1, 0);
        _heart.Play("play");
        _presentation.Play("intro");
        RefreshSharedAttack();
    }

    /// <summary>
    /// 背景进入完成后在源位置启动两只独立手掌。
    /// </summary>
    internal void StartHands()
    {
        _leftHand.Start(-300, HitWithHand, RefreshSharedAttack);
        _rightHand.Start(300, HitWithHand, RefreshSharedAttack);
    }

    /// <summary>
    /// 随机显隐阶段只选择原生动画，透明度插值不写在规则代码中。
    /// </summary>
    /// <param name="visible">本次是否淡入心脏。</param>
    internal void FadeHeart(bool visible) => _heartFade.Play(visible ? "show" : "hide");

    /// <summary>
    /// 生成本轮2/4/6根火柱，保留源文件固定世界范围和底边。
    /// </summary>
    /// <param name="count">本轮火柱数量。</param>
    internal void SpawnFire(int count)
    {
        for (int index = 0; index < count; index++)
        {
            BullDemonFireNode fire = NodePool.Instance.Get<BullDemonFireNode>(_fireScene.ResourcePath, GetParent()) ??
                throw new InvalidOperationException("牛魔王火柱未登记到GGF节点池。");
            _fires.Add(fire);
            fire.Show(new Vector2(900 + GD.Randf() * 800, 520), HitWithFire, ReleaseFire);
        }
    }

    /// <summary>
    /// 死亡停止全部伤害，再由原生death统一淡出身体、心脏、双手和背景。
    /// </summary>
    internal void BeginDeathPresentation()
    {
        _leftHand.Stop();
        _rightHand.Stop();
        foreach (BullDemonFireNode fire in _fires.ToArray())
        {
            ReleaseFire(fire);
        }

        _heartFade.Stop();
        _presentation.Play("death");
    }

    /// <inheritdoc />
    public override void OnHide(bool isShutdown, object userData)
    {
        if (!isShutdown)
        {
            _leftHand.Stop();
            _rightHand.Stop();
            _heart.Stop();
            _heartFade.Stop();
            _presentation.Stop();
            foreach (BullDemonFireNode fire in _fires.ToArray())
            {
                ReleaseFire(fire);
            }
        }
        _fires.Clear();
        ReleaseSharedAttacks();
        base.OnHide(isShutdown, userData);
    }

    /// <summary>
    /// 任一手掌复位时换父攻击编号；同时更新两个伤害快照和共同去重集合。
    /// </summary>
    private void RefreshSharedAttack()
    {
        ReleaseSharedAttacks();
        _hitTargets.Clear();
        CombatantAttributes attributes = CaptureCombatAttributes();
        AttackConfig hand = OwnAttacks[0];
        AttackConfig fire = OwnAttacks[1];
        _handAttackData = AttackData.Create(hand.Id, attributes,
            Mathf.RoundToInt(hand.FlatPower.X), hand.DamageKind, hand.Knockback, -1, hand.HitProtect,
            isEffect: true, source: CaptureAttackSource());
        _fireAttackData = AttackData.Create(fire.Id, attributes,
            Mathf.RoundToInt(fire.FlatPower.X), fire.DamageKind, fire.Knockback, -1, fire.HitProtect,
            isEffect: true, source: CaptureAttackSource());
    }

    /// <summary>
    /// 归还首领拥有的两个攻击包，不将它们交给火柱回收。
    /// </summary>
    private void ReleaseSharedAttacks()
    {
        if (_handAttackData != null)
        {
            ReferencePool.Release(_handAttackData);
        }

        if (_fireAttackData != null)
        {
            ReferencePool.Release(_fireAttackData);
        }

        _handAttackData = null;
        _fireAttackData = null;
    }

    /// <summary>
    /// 巨掌使用父编号的物理伤害快照。
    /// </summary>
    /// <param name="target">当前重叠的真实目标。</param>
    private void HitWithHand(AbstractActorEntity target) => HitTarget(target, _handAttackData);

    /// <summary>
    /// 火柱使用魔法伤害，目标去重与双掌完全相同。
    /// </summary>
    /// <param name="target">当前火焰范围内的目标。</param>
    private void HitWithFire(AbstractActorEntity target) => HitTarget(target, _fireAttackData);

    /// <summary>
    /// 只在成功命中后登记共享目标，免疫期间不会消耗本次父攻击编号。
    /// </summary>
    /// <param name="target">实际物理重叠目标。</param>
    /// <param name="data">父形态拥有的攻击包。</param>
    private void HitTarget(AbstractActorEntity target, AttackData data)
    {
        if (!IsAlive || data == null || !target.IsAlive || target.Side == Side || _hitTargets.Contains(target.GetInstanceId()))
        {
            return;
        }

        if (!target.ReceiveHit(data).IsMiss)
        {
            _hitTargets.Add(target.GetInstanceId());
        }
    }

    /// <summary>
    /// 只归还本次显示仍拥有的火柱，完成和清理可重复调用。
    /// </summary>
    /// <param name="fire">已结束的火柱。</param>
    private void ReleaseFire(BullDemonFireNode fire)
    {
        if (_fires.Remove(fire))
        {
            NodePool.Instance.Release(fire);
        }
    }
}
