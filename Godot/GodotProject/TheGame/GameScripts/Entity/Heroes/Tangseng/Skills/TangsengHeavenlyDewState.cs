using GameFramework.Fsm;
using GameLogic.Heroes.Body;
using Godot;

namespace GameLogic.Heroes.Tangseng
{
    /// <summary>
    /// 天降甘露由官方末帧方法键治疗本体一次。
    /// </summary>
    public sealed class TangsengHeavenlyDewState : AbstractHeroSkillState
    {
        private bool m_Healed; // 本次末帧治疗已提交。

        /// <inheritdoc />
        protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
        {
            base.Enter(fsm, body);
            GravitySuspended = true;
            body.Velocity = Vector2.Zero;
            m_Healed = false;
            BeginAnimation(body, "skills/heavenly_dew");
        }

        /// <summary>
        /// 本体末帧治疗并收招，分身由自身时间轴负责。
        /// </summary>
        /// <param name="body">唐僧身体。</param>
        public void Heal(IHeroBody body)
        {
            if (m_Healed)
            {
                return;
            }

            m_Healed = true;
            ((TangsengEntity)body).ApplyHeavenlyDew();
            CompleteAnimation();
        }
    }
}
