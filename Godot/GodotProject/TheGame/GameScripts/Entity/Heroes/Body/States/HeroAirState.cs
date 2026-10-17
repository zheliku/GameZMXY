using GameLogic.Architecture;
using GameLogic.Input;
using GameLogic.Heroes.Body;
using GameFramework.Fsm;

namespace GameLogic.Heroes.Body
{
    /// <summary>
    /// 空中：起跳上升 / 二段跳 / 下落。空中保留横向控制、可二段跳、可起手角色指定的空中普攻；
    /// 落地进 Ground；仅空中普攻清空地面连段，普通跳跃不清空。
    /// jump/jump_2 播完停在末帧（不循环），Air 不计时——下落姿势由纵向速度切换。
    /// </summary>
    public sealed class HeroAirState : AbstractHeroBodyState
    {
        /// <summary>处理空中攻击、二段跳、转向与跳跃/下落动画。</summary>
        /// <param name="fsm">所属身体状态机。</param>
        /// <param name="body">英雄宿主。</param>
        /// <param name="dt">游戏物理帧秒数。</param>
        protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
        {
            if (TryStartSkill(fsm, body))
                return;
            HandleJumpKeyCombinations(body);
            if (body.ActionData.AttackRequested)
            {
                body.ActionData.AttackRequested = false;
                ChangeState<HeroAttackState>(fsm);
                return;
            }

            TryJump(body);
            if (!IsAirborne(body))
            {
                ChangeState<HeroGroundState>(fsm);
                return;
            }

            ApplyGravity(body, dt);
            ApplyHorizontalMovement(body);
            body.PlayAnim(SelectAirAnimation(body));
        }

        /// <inheritdoc />
        public override bool TryGetActionInput(IHeroBody body, out bool attacking, out float normalProgress, out bool airborne)
        {
            attacking = false;
            normalProgress = 0;
            airborne = IsAirborne(body);
            return true;
        }
    }
}
