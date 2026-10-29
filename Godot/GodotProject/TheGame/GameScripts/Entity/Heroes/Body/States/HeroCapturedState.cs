using GameFramework.Fsm;
using Godot;

namespace GameLogic.Heroes.Body
{
    /// <summary>
    /// 被捕获期间保留生命与状态计时，身体不移动、不出招，释放后回到实际地空动作。
    /// </summary>
    public sealed class HeroCapturedState : AbstractHeroBodyState
    {
        /// <inheritdoc />
        protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
        {
            body.ActionData.InputLocked = true;
            body.ActionData.AttackRequested = false;
            body.Velocity = Vector2.Zero;
            body.EndAttack();
            body.TakePendingHurt();
            body.RestartAnim(HeroAnims.Idle);
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
        {
            body.Velocity = Vector2.Zero;
            if (!body.ActionData.Captured)
            {
                ChangeToLocomotion(fsm, body);
            }
        }

        /// <inheritdoc />
        protected override void Leave(IFsm<IHeroBody> fsm, IHeroBody body, bool isShutdown)
        {
            body.ActionData.InputLocked = false;
        }
    }
}
