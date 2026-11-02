using System;
using System.Collections.Generic;
using System.Linq;
using GameFramework;
using GameLogic.Battle.Attacks;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using GameLogic.Entity;
using Godot;
using GodotGameFramework.NodePool;
using GameConfig.Equipment;
using GameLogic.Architecture;
using GameLogic.Equipment;
using GameLogic.Profile;
using GameLogic.UI;
using GodotGameFramework;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 牛魔王最终形态固定在场中，心脏显隐控制受伤，双掌与火柱共用父攻击编号。
    /// </summary>
    public partial class BullDemonHeartEntity : AbstractBossEntity
    {
        private static readonly EnemyBodyPreset m_BodyPreset = new(typeof(BullDemonHeartState),
            typeof(BullDemonHeartDeathState),
            () => new BullDemonHeartState(), () => new BullDemonHeartDeathState()); // 本首领的状态组合；运行实例由每次入场独立创建。
        [Export] private AnimatedSprite2D m_Heart; // 保留父级柔光和原点的23帧独立心脏。
        [Export] private AnimationPlayer m_HeartFade; // 原生show/hide动画负责两秒淡入淡出。
        [Export] private AnimationPlayer m_Presentation; // 原生intro与death负责背景进入和完整退场。
        [Export] private BullDemonHandNode m_LeftHand; // 本首领拥有的左手局部运动。
        [Export] private BullDemonHandNode m_RightHand; // 本首领拥有的镜像右手。
        [Export] private PackedScene m_FireScene; // 通过GGF节点池显示的官方火柱。
        private readonly HashSet<BullDemonFireNode> m_Fires = new(); // 本次显示拥有的火柱，离场逐个回收。
        private readonly HashSet<ulong> m_HitTargets = new(); // 两只手和全部火柱共同使用的当前攻击目标去重。
        private AttackData m_HandAttackData; // 本次父攻击编号的物理掌击快照。
        private AttackData m_FireAttackData; // 与掌击共用目标去重的魔法火柱快照。

        /// <inheritdoc />
        protected override EnemyBodyPreset BodyPreset
        {
            get
            {
                return m_BodyPreset;
            }
        }

        /// <inheritdoc />
        protected override EnemyAiPreset AiPreset
        {
            get
            {
                return null;
            }
        }

        /// <inheritdoc />
        protected override bool Invulnerable
        {
            get
            {
                return m_Heart.Modulate.A < 1f;
            }
        }

        /// <summary>
        /// 当前淡入或淡出是否仍由原生播放器执行。
        /// </summary>
        internal bool HeartFading
        {
            get
            {
                return m_HeartFade.IsPlaying();
            }
        }

        /// <summary>
        /// 原生背景进入时长，身体状态不另写一套视觉秒数。
        /// </summary>
        internal float IntroDuration
        {
            get
            {
                return (float)m_Presentation.GetAnimation("intro").Length;
            }
        }

        /// <summary>
        /// 原生整体退场时长。
        /// </summary>
        internal float DeathDuration
        {
            get
            {
                return (float)m_Presentation.GetAnimation("death").Length;
            }
        }

        /// <summary>
        /// 入场启动背景和心脏，双手等背景完整出现后开始。
        /// </summary>
        internal void BeginPresentation()
        {
            m_LeftHand.Stop();
            m_RightHand.Stop();
            m_Presentation.Play("RESET");
            m_Presentation.Advance(0);
            m_Presentation.Stop();
            // Stop会恢复旧淡出动画的起点alpha=1，须先停旧周期，再设置本次初始隐藏。
            m_HeartFade.Stop();
            m_Heart.Modulate = new Color(1, 1, 1, 0);
            m_Heart.Play("play");
            m_Presentation.Play("intro");
            RefreshSharedAttack();
        }

        /// <summary>
        /// 背景进入完成后在源位置启动两只独立手掌。
        /// </summary>
        internal void StartHands()
        {
            m_LeftHand.Start(-300, HitWithHand, RefreshSharedAttack);
            m_RightHand.Start(300, HitWithHand, RefreshSharedAttack);
        }

        /// <summary>
        /// 随机显隐阶段只选择原生动画，透明度插值不写在规则代码中。
        /// </summary>
        /// <param name="visible">本次是否淡入心脏。</param>
        internal void FadeHeart(bool visible)
        {
            m_HeartFade.Play(visible ? "show" : "hide");
        }

        /// <summary>
        /// 生成本轮2/4/6根火柱，保留源文件固定世界范围和底边。
        /// </summary>
        /// <param name="count">本轮火柱数量。</param>
        internal void SpawnFire(int count)
        {
            for (int index = 0; index < count; index++)
            {
                var fire = NodePool.Instance.Get<BullDemonFireNode>(m_FireScene.ResourcePath, GetParent()) ??
                    throw new InvalidOperationException("牛魔王火柱未登记到GGF节点池。");
                m_Fires.Add(fire);
                fire.Show(new Vector2(900 + GD.Randf() * 800, 520), HitWithFire, ReleaseFire);
            }
        }

        /// <summary>
        /// 死亡停止全部伤害，再由原生death统一淡出身体、心脏、双手和背景。
        /// </summary>
        internal void BeginDeathPresentation()
        {
            m_LeftHand.Stop();
            m_RightHand.Stop();
            foreach (var fire in m_Fires.ToArray())
            {
                ReleaseFire(fire);
            }

            m_HeartFade.Stop();
            m_Presentation.Play("death");
            // 原版直接赠入背包，已有同名法宝时不重复生成；满包不丢到地上。
            var profile = this.GetModel<ProfileModel>();
            if (!profile.HasEquipment(EquipmentId.DiamondSnare))
            {
                bool collected = false;
                if (profile.EquipmentDatas.Count < profile.Capacity)
                {
                    var data = this.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), EquipmentId.DiamondSnare, GD.Randf);
                    collected = this.GetSystem<ProfileSystem>().TryCollectEquipment(data);
                }
                GF.UI.GetAllLoadedUIForms().OfType<SaveNoticeForm>().Single().ShowMessage(
                    collected ? "获得 金钢琢" : "背包已满，无法获得 金钢琢");
            }
        }

        /// <inheritdoc />
        public override void OnHide(bool isShutdown, object userData)
        {
            if (!isShutdown)
            {
                m_LeftHand.Stop();
                m_RightHand.Stop();
                m_Heart.Stop();
                m_HeartFade.Stop();
                m_Presentation.Stop();
                foreach (var fire in m_Fires.ToArray())
                {
                    ReleaseFire(fire);
                }
            }
            m_Fires.Clear();
            ReleaseSharedAttacks();
            base.OnHide(isShutdown, userData);
        }

        /// <summary>
        /// 任一手掌复位时换父攻击编号；同时更新两个伤害快照和共同去重集合。
        /// </summary>
        private void RefreshSharedAttack()
        {
            ReleaseSharedAttacks();
            m_HitTargets.Clear();
            var attributes = CaptureCombatAttributes();
            var hand = OwnAttacks[0];
            var fire = OwnAttacks[1];
            m_HandAttackData = AttackData.Create(hand.Id, attributes,
                Mathf.RoundToInt(hand.FlatPower.X), hand.DamageKind, hand.Knockback, -1, hand.HitProtect, isEffect: true);
            m_FireAttackData = AttackData.Create(fire.Id, attributes,
                Mathf.RoundToInt(fire.FlatPower.X), fire.DamageKind, fire.Knockback, -1, fire.HitProtect, isEffect: true);
        }

        /// <summary>
        /// 归还首领拥有的两个攻击包，不将它们交给火柱回收。
        /// </summary>
        private void ReleaseSharedAttacks()
        {
            if (m_HandAttackData != null)
            {
                ReferencePool.Release(m_HandAttackData);
            }

            if (m_FireAttackData != null)
            {
                ReferencePool.Release(m_FireAttackData);
            }

            m_HandAttackData = null;
            m_FireAttackData = null;
        }

        /// <summary>
        /// 巨掌使用父编号的物理伤害快照。
        /// </summary>
        /// <param name="target">当前重叠的真实目标。</param>
        private void HitWithHand(AbstractActorEntity target)
        {
            HitTarget(target, m_HandAttackData);
        }

        /// <summary>
        /// 火柱使用魔法伤害，目标去重与双掌完全相同。
        /// </summary>
        /// <param name="target">当前火焰范围内的目标。</param>
        private void HitWithFire(AbstractActorEntity target)
        {
            HitTarget(target, m_FireAttackData);
        }

        /// <summary>
        /// 只在成功命中后登记共享目标，免疫期间不会消耗本次父攻击编号。
        /// </summary>
        /// <param name="target">实际物理重叠目标。</param>
        /// <param name="data">父形态拥有的攻击包。</param>
        private void HitTarget(AbstractActorEntity target, AttackData data)
        {
            if (!IsAlive || data == null || !target.IsAlive || target.Side == Side || m_HitTargets.Contains(target.GetInstanceId()))
            {
                return;
            }

            if (!target.ReceiveHit(data, Id).IsMiss)
            {
                m_HitTargets.Add(target.GetInstanceId());
            }
        }

        /// <summary>
        /// 只归还本次显示仍拥有的火柱，完成和清理可重复调用。
        /// </summary>
        /// <param name="fire">已结束的火柱。</param>
        private void ReleaseFire(BullDemonFireNode fire)
        {
            if (m_Fires.Remove(fire))
            {
                NodePool.Instance.Release(fire);
            }
        }
    }
}
