using GameFramework.Fsm;

namespace GameLogic.Heroes.Body
{
    /// <summary>地面走、跑与完整待机循环；攻击、跳跃和离地立即打断。</summary>
    public sealed class HeroGroundState : HeroBodyState
    {
        /// <summary>处理地面攻击、跳跃、移动和待机动画。</summary>
        /// <param name="fsm">所属身体状态机。</param>
        /// <param name="body">英雄宿主。</param>
        /// <param name="dt">游戏物理帧秒数。</param>
        protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
        {
            HandleJumpKeyCombinations(body);
            if (body.NormalAttack.TryAdvance(body.Input, IsAirborne(body), isAttacking: false))
            {
                ChangeState<HeroAttackState>(fsm);
                return;
            }
            if (TryJump(body) || IsAirborne(body))
            {
                ChangeState<HeroAirState>(fsm);
                return;
            }

            ApplyGravity(body, dt);
            ApplyHorizontalMovement(body);
            // 包括立棍张望的时序归动画，不另设随机小动作计时。
            body.PlayAnim(body.Input.MoveAxis == 0 ? HeroAnims.Idle :
                body.Input.Running ? HeroAnims.Run : HeroAnims.Walk);
        }
    }
}
