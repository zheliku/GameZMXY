using GameFramework.Fsm;
using GameLogic.Enemies.Body;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 凤凰蛋保留受击体，七秒内接收破蛋命中；每秒生成火焰，超时进入真实复生动作。
    /// </summary>
    internal sealed class PengDemonKingEggState : PengDemonKingBodyState
    {
        private float m_Left; // 本次凤凰蛋存续剩余秒数。
        private float m_FireLeft; // 下次蛋火焰的剩余游戏秒数。

        /// <inheritdoc />
        protected override bool HurtInterrupts
        {
            get
            {
                return false;
            }
        }

        /// <inheritdoc />
        protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
        {
            ((PengDemonKingEntity)body).BeginEgg();
            m_Left = 7f;
            m_FireLeft = 1f;
            body.RestartAnim(PengDemonKingAnims.Egg);
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            body.TakeAttackRequest();
            ApplyGravity(body, dt);
            Stand(body);
            m_Left -= dt;
            m_FireLeft -= dt;
            if (Elapsed(m_Left))
            {
                ChangeState<PengDemonKingRebirthState>(fsm);
                return;
            }
            if (Elapsed(m_FireLeft))
            {
                ((PengDemonKingEntity)body).SpawnFire();
                m_FireLeft += 1f;
            }
        }
    }
}
