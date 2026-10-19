using GameLogic.Heroes.Body;
using GameLogic.Heroes.Wukong;
using GameLogic.Entity;
using System;
using GameFramework.Fsm;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Input;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes
{
    /// <summary>
    /// 悟空入口：匹配专属输入、选择技能组并声明身体状态；各招流程由对应的技能状态负责。
    /// 通用宿主机制（输入采样、物理、结算、状态机推进）在 <see cref="AbstractHeroEntity"/>。
    ///
    /// 场景绑定（Entitys/wukong_entity.tscn）：
    ///  * HeroId —— 对应的 HeroConfig 行；
    ///  * m_Body / m_Weapon —— 独立身体、武器 Sprite2D，共用图集网格、帧号与定位；
    ///  * m_AnimPlayer —— 悟空自己的动画库（Entitys/Animations/wukong_anim_library.tres，
    ///    帧、特效、判定盒值轨道与具名方法轨道），由 AbstractHeroEntity 经 AbstractActorEntity 直接驱动；
    ///  * m_HurtBox / m_HitBox —— 判定区（层见 Entity/AGENTS.md）。
    ///
    /// 角色专属动画名只允许出现在三个地方：该角色的动画库资源、本角色状态与场景动作列表。
    /// 换装通过共用的 wukong_appearance.tres 独立切换身体与武器图集，背包预览使用相同资源。
    /// 唐僧（远程）、八戒、沙僧（双武器）等新角色各自继承 <see cref="AbstractHeroEntity"/>，在本类同样的位置声明自己的状态集。
    /// </summary>
    public partial class WukongEntity : AbstractHeroEntity
    {
        private const double SourceFrameSeconds = 1d / 24; // 造一悟空源动画一帧的秒数，仅用于取消窗口换算。
        private static readonly SkillDefinition[] m_Skills =
        [
            new(SkillId.RisingDragon, typeof(WukongRisingDragonState)),
            new(SkillId.FlameThrust, typeof(WukongFlameThrustState), CanStart: actor => actor.OnFloor),
            new(SkillId.BlazingFlash, typeof(WukongBlazingFlashState), InputSystem.Skill1),
            new(SkillId.FlameStorm, typeof(WukongFlameStormState), InputSystem.Skill2),
            new(SkillId.FireDemonSlash, typeof(WukongFireDemonState), InputSystem.Skill3)
        ]; // 悟空只提供不可变登记项，本局登记容器归 HeroModel。

        /// <summary>悟空从地面状态起步。</summary>
        protected override Type InitialBodyStateType => typeof(HeroGroundState);

        /// <summary>起手前选定悟空的技能组；具体招式不会进入共享输入或身体状态。</summary>
        /// <param name="userData">框架显示参数。</param>
        public override void OnShow(object userData)
        {
            base.OnShow(userData);
            this.GetSystem<SkillSystem>().SelectSkills(1, m_Skills);
        }

        /// <summary>悟空匹配直接技能、升龙取消、缺蓝普攻与奔跑突刺；身体接口不承担这些规则。</summary>
        /// <param name="attacking">当前是否处于普攻。</param>
        /// <param name="normalProgress">本段普攻的播放比例。</param>
        /// <param name="airborne">当前是否处于空中。</param>
        protected override void MatchActionInput(bool attacking, float normalProgress, bool airborne)
        {
            var input = this.GetSystem<InputSystem>();
            var skills = this.GetSystem<SkillSystem>();
            var body = (IHeroBody)this;
            if (!attacking && skills.TryActivateInput(this, ActionData))
                return;
            if (IsUpAttackRequested())
            {
                if (!CanCancelNormalAttack(attacking, normalProgress))
                    return;
                if (skills.IsLearned(SkillId.RisingDragon) && !skills.CanAfford(SkillId.RisingDragon))
                    ActionData.AttackRequested = input.ConsumeAction(ActionData, true) &&
                        input.TrySelectAttack(ActionData, body.Params, airborne, attacking);
                else
                    skills.TryActivate(SkillId.RisingDragon, this);
                return;
            }

            // 奔跑普攻先尝试突刺；不可用时保留输入供普通攻击消费。
            if (ActionData.Running && !airborne && !attacking && ActionData.CanAct &&
                ActionData.AttackHeld && !ActionData.JumpHeld && !ActionData.UpHeld &&
                input.IsAttackReady(ActionData, airborne, attacking) &&
                skills.TryActivate(SkillId.FlameThrust, this))
            {
                input.ConsumeAttack(ActionData);
                ActionData.LastAttackAttempt = ActionData.Time;
                Velocity = new Vector2(ActionData.MoveAxis * body.Params.RunSpeed * body.MoveSpeedMultiplier, Velocity.Y);
                return;
            }
            ActionData.AttackRequested = input.TryStartAttack(ActionData, body.Params,
                airborne, attacking);
        }

        /// <summary>造一悟空空中使用第三段下劈，地面使用完整五段普攻。</summary>
        /// <returns>包含空中下劈段号的身体参数。</returns>
        protected override HeroBodyParams BuildBodyParams() => base.BuildBodyParams() with { AirAttackIndex = 2 };

        /// <summary>悟空的身体状态与五个具名技能状态；进入、退出与打断统一使用 GGF 生命周期。</summary>
        /// <returns>本角色使用的身体状态集。</returns>
        protected override FsmState<IHeroBody>[] CreateBodyStates()
        {
            return
            [
                new HeroGroundState(), new HeroAirState(), new HeroAttackState(), new HeroHurtState(),
                new HeroDeathState(), new WukongRisingDragonState(), new WukongFlameThrustState(),
                new WukongBlazingFlashState(), new WukongFlameStormState(), new WukongFireDemonState()
            ];
        }

        /// <summary>悟空的升龙组合；不让公共输入系统识别具体招式。</summary>
        /// <returns>本帧组合与动作闩锁均允许时为真。</returns>
        private bool IsUpAttackRequested() => ActionData.CanAct && ActionData.UpHeld && ActionData.AttackHeld &&
            !ActionData.JumpHeld && !ActionData.DownHeld;

        /// <summary>只允许前四段普攻后 30% 取消；不在普攻时直接允许。</summary>
        /// <param name="attacking">是否正在普攻。</param>
        /// <param name="normalRatio">当前帧比例。</param>
        /// <returns>允许升龙或缺蓝回退时为真。</returns>
        private bool CanCancelNormalAttack(bool attacking, float normalRatio) => !attacking ||
            (ActionData.AttackSegment >= 0 && ActionData.AttackSegment < ((IHeroBody)this).Params.ComboLength - 1 &&
             (normalRatio + SourceFrameSeconds / ((IHeroBody)this).Params.AttackTimes[ActionData.AttackSegment]) >=
             this.GetModel<ConfigurationModel>().Tables.TbSkillConfig
                 .Get(SkillId.RisingDragon).CancelNormalAttackFrameRatio);
    }
}
