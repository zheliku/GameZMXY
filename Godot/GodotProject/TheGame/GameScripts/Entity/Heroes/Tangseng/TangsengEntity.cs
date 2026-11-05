using System;
using GameConfig.Skills;
using GameConfig.Sound;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Audio;
using GameLogic.Heroes.Body;
using GameLogic.Input;
using GameLogic.Battle;
using GameLogic.Config;
using GameLogic.Heroes.Tangseng;
using GameLogic.Skills;
using GameLogic.Effects;
using Godot;
using GodotGameFramework;

namespace GameLogic.Heroes
{
    /// <summary>
    /// 唐僧入口，复用英雄移动、受击、死亡、换装和档案生命周期。
    /// </summary>
    public partial class TangsengEntity : AbstractHeroEntity
    {
        [Export] private TangsengWaterCloneNode m_WaterClone; // 本体拥有的水幻影实例。
        [Export] private PackedScene m_DarkIceEffect; // 本体与分身共用的玄冰阵场景。
        [Export] private AnimatedSprite2D m_WaterDemonBall; // 原生身体动画驱动的官方roundMc。
        [Export] private PackedScene m_WaterDemonFallEffect; // 水魔爆追加下落效果。

        private static readonly SkillDefinition[] m_Skills =
        [
            new(SkillId.IceDragonWave, typeof(TangsengIceDragonState), DeferredMana: true),
            new(SkillId.DarkIceFormation, typeof(TangsengDarkIceState), InputSystem.Skill1),
            new(SkillId.WaterClone, typeof(TangsengWaterCloneState), InputSystem.Skill2),
            new(SkillId.HeavenlyDew, typeof(TangsengHeavenlyDewState), InputSystem.Skill3),
            new(SkillId.WaterDemonBurst, typeof(TangsengWaterDemonBurstState), InputSystem.Skill4,
                CanStart: actor => actor.OnFloor)
        ]; // 仅登记本角色的具体状态与起手资格。

        /// <inheritdoc />
        protected override Type InitialBodyStateType => typeof(HeroGroundState);

        /// <summary>
        /// 按唐僧单段普攻配置消费输入。
        /// </summary>
        /// <param name="attacking">当前是否正在普攻。</param>
        /// <param name="airborne">当前是否处于空中。</param>
        protected override void MatchActionInput(bool attacking, bool airborne)
        {
            var input = this.GetSystem<InputSystem>();
            if (!attacking && m_WaterClone.IsAvailable && input.ConsumePress(ActionData, InputSystem.Skill2))
            {
                GF.Sound.PlayConfigured(SoundId.TangsengWaterClone);
                GlobalPosition = m_WaterClone.GlobalPosition;
                m_WaterClone.Dismiss();
                return;
            }

            if (!attacking && this.GetSystem<SkillSystem>().TryActivateInput(this, ActionData))
            {
                return;
            }

            ActionData.AttackRequested = input.TryStartAttack(
                ActionData, ((IHeroBody)this).Params, airborne, attacking);
        }

        /// <inheritdoc />
        public override void OnShow(object userData)
        {
            base.OnShow(userData);
            m_WaterClone.Dismiss();
            this.GetSystem<SkillSystem>().SelectSkills(2, m_Skills);
        }

        /// <summary>
        /// 首次施放水幻影时创建官方 Role2Shadow。
        /// </summary>
        public void CreateWaterClone()
        {
            GF.Sound.PlayConfigured(SoundId.TangsengWaterClone);
            m_WaterClone.Activate(GlobalPosition, Facing);
        }

        /// <summary>
        /// 隐藏或回池时清除分身，避免跨场景保留旧坐标。
        /// </summary>
        public override void OnHide(bool isShutdown, object userData)
        {
            m_WaterClone.Dismiss();
            base.OnHide(isShutdown, userData);
        }

        /// <summary>
        /// 按本次蓄力结果配置官方法弹的移动、攻击与缩放。
        /// </summary>
        /// <param name="scene">原生轨道引用的法弹场景。</param>
        /// <param name="fullCharge">本次是否达到满蓄。</param>
        public void SpawnIceDragonBolt(PackedScene scene, bool fullCharge)
        {
            GF.Sound.PlayConfigured(fullCharge ? SoundId.TangsengIceDragon : SoundId.TangsengAttack);
            var effect = (TangsengBoltNode)RentSkillEffect(scene);
            var hit = fullCharge
                ? this.GetModel<ConfigurationModel>().Tables.TbAttackConfig.Get((int)SkillHitId.IceDragonWave)
                : OwnAttacks[0];
            var attributes = CaptureCombatAttributes();
            int power = (int)((attributes.Power * hit.PowerScale.X + hit.FlatPower.X) *
                this.GetSystem<HeroSystem>().AttackPowerMultiplier);
            effect.ConfigureTravel(fullCharge ? 600f : 400f);
            effect.Show(hit, GlobalPosition, Facing, attributes, power,
                this.GetModel<HeroModel>().WsActive.Value, ReleaseSkillEffect, hitSoundId: HitSoundId);
        }

        /// <summary>
        /// 以独立池化效果提交本体玄冰阵，避免覆盖英雄身体攻击包。
        /// </summary>
        /// <param name="scene">原生轨道引用的玄冰阵场景。</param>
        /// <param name="position">施法者世界位置。</param>
        /// <param name="facing">本体或分身自身的出招朝向。</param>
        /// <param name="fromClone">分身使用本体无双缩放与威力，但不取得本体的额外两次攻击刷新。</param>
        public void SpawnDarkIceEffect(PackedScene scene, Vector2 position, int facing, bool fromClone = false)
        {
            if (!fromClone)
            {
                GF.Sound.PlayConfigured(SoundId.TangsengDarkIce);
            }
            var effect = RentSkillEffect(scene);
            var hit = this.GetModel<ConfigurationModel>().Tables.TbAttackConfig.Get((int)SkillHitId.DarkIceFormation);
            var attributes = CaptureCombatAttributes();
            bool musou = this.GetModel<HeroModel>().WsActive.Value;
            int power = (int)((attributes.Power * hit.PowerScale.X + hit.FlatPower.X) *
                this.GetSystem<HeroSystem>().AttackPowerMultiplier);
            effect.Show(hit, position, facing, attributes, power, musou && !fromClone,
                ReleaseSkillEffect, hitSoundId: HitSoundId,
                visualScaleMultiplier: musou && fromClone ? new Vector2(1.26f, 1.8f) : Vector2.One);
        }

        /// <summary>
        /// 分身复用同一官方 Bullet3 场景，但拥有独立攻击包。
        /// </summary>
        public void PlayShadowDarkIce()
        {
            if (m_WaterClone.IsAvailable)
            {
                m_WaterClone.PlayDarkIce(() => SpawnDarkIceEffect(m_DarkIceEffect,
                    m_WaterClone.GlobalPosition, m_WaterClone.Facing, fromClone: true));
            }
        }

        /// <summary>
        /// 起手播放完整甘露，并同时安排分身hit5，治疗仍由各自末帧提交。
        /// </summary>
        /// <param name="scene">原生轨道引用的治疗表现。</param>
        public void BeginHeavenlyDew(PackedScene scene)
        {
            SpawnCureEffect(scene, GlobalPosition);
            GF.Sound.PlayConfigured(SoundId.TangsengHeavenlyDew);
            if (m_WaterClone.IsAvailable)
            {
                GF.Sound.PlayConfigured(SoundId.TangsengHeavenlyDew);
                m_WaterClone.PlayHeavenlyDew(() =>
                {
                    Vector2 delta = m_WaterClone.GlobalPosition - GlobalPosition;
                    if (Mathf.Abs(delta.X) <= 200f && Mathf.Abs(delta.Y) <= 160f)
                    {
                        ApplyHeavenlyDew();
                    }
                });
            }
        }

        /// <summary>
        /// roundMc仍在身体中时允许二次输入，落点按当前局部坐标计算。
        /// </summary>
        public bool CanReleaseWaterDemon => m_WaterDemonBall.Visible;

        /// <summary>
        /// 第二次 L 触发官方 hit4FallDown，并提交追加伤害。
        /// </summary>
        public void ReleaseWaterDemonFollowUp()
        {
            Vector2 local = m_WaterDemonBall.Position;
            Vector2 position = GlobalPosition + new Vector2(local.X * (Facing > 0 ? -1 : 1), local.Y - 300);
            ((ISkillActor)this).PlaySkillEffect(m_WaterDemonFallEffect, position,
                SkillHitId.WaterDemonFall, PowerFor(SkillHitId.WaterDemonFall));
            m_WaterDemonBall.Stop();
            m_WaterDemonBall.Hide();
        }

        /// <summary>
        /// 读取指定伤害阶段在应用无双前的威力。
        /// </summary>
        /// <param name="hit">统一攻击定义。</param>
        /// <returns>无双倍率前的威力。</returns>
        private double PowerFor(SkillHitId hit)
        {
            var definition = this.GetModel<ConfigurationModel>().Tables.TbAttackConfig.Get((int)hit);
            return this.GetModel<HeroModel>().Power.Value * definition.PowerScale.X + definition.FlatPower.X;
        }

        /// <summary>
        /// 在本体或分身的官方治疗帧结算一次生命恢复。
        /// </summary>
        public void ApplyHeavenlyDew()
        {
            double amount = this.GetModel<HeroModel>().MaxMp.Value / 8d + 8;
            if (this.GetModel<HeroModel>().WsActive.Value)
            {
                amount *= 1.5;
            }

            this.GetSystem<HeroSystem>().Heal((int)amount);
        }

        /// <summary>
        /// 在官方偏移处播放甘露，大小随本次无双状态确定。
        /// </summary>
        /// <param name="scene">官方治疗场景。</param>
        /// <param name="position">施法者世界位置。</param>
        private void SpawnCureEffect(PackedScene scene, Vector2 position)
        {
            var effect = RentSkillEffect(scene);
            effect.Show(null, position + new Vector2(0, -60), Facing,
                CaptureCombatAttributes(), 0, this.GetModel<HeroModel>().WsActive.Value,
                ReleaseSkillEffect, visualScaleMultiplier: this.GetModel<HeroModel>().WsActive.Value
                    ? new Vector2(1.6f, 1.6f) : Vector2.One);
        }

        /// <summary>
        /// 注册共用身体状态，唐僧无双仍接受硬直和状态效果。
        /// </summary>
        /// <returns>由本角色实例拥有的身体状态集合。</returns>
        protected override FsmState<IHeroBody>[] CreateBodyStates()
        {
            return
            [
                new HeroGroundState(), new HeroAirState(), new HeroAttackState(),
                new HeroHurtState(), new HeroDeathState(), new HeroCapturedState(),
                new TangsengIceDragonState(), new TangsengDarkIceState(), new TangsengWaterCloneState(),
                new TangsengHeavenlyDewState(), new TangsengWaterDemonBurstState()
            ];
        }

    }
}
