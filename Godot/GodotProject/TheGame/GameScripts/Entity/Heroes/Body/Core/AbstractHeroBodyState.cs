using GameLogic.Architecture;
using GameLogic.Input;
using GameLogic.Heroes.Body;
using GameFramework.Fsm;
using GameLogic.Body;
using GameLogic.Skills;

namespace GameLogic.Heroes.Body
{
    /// <summary>
    /// 英雄身体状态基类：集中**打断优先级**（死亡 &gt; 受击 &gt; 本状态决策）与共用的移动/跳跃/起手规则。
    /// <code>
    ///   Ground ⇄ Air                离地 / 落地（含起跳）
    ///   Ground/Air → Attack         普攻请求（空中起手的连段不推进）
    ///   Attack → Attack(下一段)     新输入通过冷却判定时立即换段
    ///   Attack → Ground/Air         段时长结束（= 段动画长度）
    ///   任意 → Hurt → Ground/Air    受击立即打断；无双霸体除外
    ///   任意 → Death                死亡（终态；实体回收后整台状态机重建）
    /// </code>
    /// </summary>
    public abstract class AbstractHeroBodyState : AbstractBodyState<IHeroBody>
    {
        private const int SecondJumpCount = 2; // 双跳触发所需的跳跃计数。

        /// <summary>受击是否立即打断本状态；Hurt 自己处理连续受击，Death 不再受击。</summary>
        protected virtual bool HurtInterrupts => true;

        /// <summary>集中处理死亡与可打断受击的状态转换。</summary>
        protected sealed override bool Interrupt(IFsm<IHeroBody> fsm, IHeroBody body)
        {
            if (body.Dead)
            {
                if (this is HeroDeathState)
                {
                    return false;
                }

                ChangeState<HeroDeathState>(fsm);
                return true;
            }

            if (HurtInterrupts && !body.SuperArmor && body.HasPendingHurt)
            {
                ChangeState<HeroHurtState>(fsm);
                return true;
            }

            return false;
        }

        /// <summary>由当前状态开放动作输入窗口并提供匹配所需事实；具体组合归角色。</summary>
        /// <param name="body">当前英雄身体事实。</param>
        /// <param name="attacking">是否处于普通攻击。</param>
        /// <param name="normalProgress">当前普攻从零开始的播放比例。</param>
        /// <param name="airborne">是否处于空中，包括起跳尚未离地的那一帧。</param>
        /// <returns>当前状态允许匹配新动作时为真；否则输出值不参与匹配。</returns>
        public virtual bool TryGetActionInput(IHeroBody body, out bool attacking, out float normalProgress, out bool airborne)
        {
            attacking = false;
            normalProgress = 0;
            airborne = false;
            return false;
        }

        /// <summary>在空中：离地，或已起跳且仍在上升（含起跳那一帧还没离地）。</summary>
        protected static bool IsAirborne(IHeroBody body)
        {
            return !body.OnFloor || (body.ActionData.JumpCount > 0 && body.Velocity.Y < 0f);
        }

        /// <summary>根据已经激活的身体占用切换 GGF 状态，不解析角色输入。</summary>
        /// <param name="fsm">身体状态机。</param>
        /// <param name="body">英雄宿主。</param>
        /// <returns>已经切换到技能状态时为真。</returns>
        protected bool TryStartSkill(IFsm<IHeroBody> fsm, IHeroBody body)
        {
            if (body is not ISkillActor)
                return false;
            var skills = body.GetSystem<SkillSystem>();
            if (skills.BodySkillData == null)
                return false;
            body.SetFacing(body.ActionData.MoveAxis);
            ChangeState(fsm, skills.BodyStateType);
            return true;
        }

        /// <summary>回到地面/空中（按当前是否在空中选）。</summary>
        protected void ChangeToLocomotion(IFsm<IHeroBody> fsm, IHeroBody body)
        {
            if (IsAirborne(body))
            {
                ChangeState<HeroAirState>(fsm);
            }
            else
            {
                ChangeState<HeroGroundState>(fsm);
            }
        }

        /// <summary>消费起跳组合，次数用尽时也耗掉本次输入；跳跃本身不重置地面连段。</summary>
        /// <param name="body">英雄身体。</param>
        /// <returns>本次是否执行起跳。</returns>
        protected static bool TryJump(IHeroBody body)
        {
            if (!body.GetSystem<InputSystem>().ConsumeJump(body.ActionData) ||
                body.ActionData.JumpCount >= body.Params.JumpCountMax)
            {
                return false;
            }

            body.ActionData.JumpCount++;
            body.Velocity = new Godot.Vector2(body.Velocity.X, -body.Params.JumpSpeed * body.JumpSpeedMultiplier);
            return true;
        }

        /// <summary>消费 J＋K 无效组合，并尝试执行 S＋K 的单向平台下落。</summary>
        /// <param name="body">英雄身体。</param>
        protected static void HandleJumpKeyCombinations(IHeroBody body)
        {
            body.GetSystem<InputSystem>().ConsumeJumpAttackCombination(body.ActionData);
            if (body.GetSystem<InputSystem>().ConsumePlatformDrop(body.ActionData))
                body.TryDropThroughPlatform();
        }

        /// <summary>按输入走/跑并随之转向（地面与空中同一套横向控制）。</summary>
        /// <param name="body">提供方向输入和移动参数的英雄身体。</param>
        protected static void ApplyHorizontalMovement(IHeroBody body)
        {
            int axis = body.ActionData.MoveAxis;
            float speed = body.ActionData.Running ? body.Params.RunSpeed : body.Params.WalkSpeed;
            body.Velocity = new Godot.Vector2(axis * speed * body.MoveSpeedMultiplier, body.Velocity.Y);
            body.SetFacing(axis);
        }

        /// <summary>空中动画：上升按跳跃次数选 jump / jump_2，下落播 fall。</summary>
        /// <param name="body">提供竖直速度和跳跃次数的英雄身体。</param>
        /// <returns>上升、二段跳或下落动画名。</returns>
        protected static string SelectAirAnimation(IHeroBody body)
        {
            if (body.Velocity.Y >= 0f)
            {
                return HeroAnims.Fall;
            }

            return body.ActionData.JumpCount >= SecondJumpCount ? HeroAnims.Jump2 : HeroAnims.Jump;
        }
    }
}
