using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 最终形态的身体状态拥有心脏显隐、火柱轮次和入场，不创建没有选招职责的AI。
    /// </summary>
    internal sealed class BullDemonHeartState : AbstractEnemyBodyState
    {
        private HeartPhase m_HeartPhase; // 当前心脏显隐阶段。
        private float m_HeartLeft; // 隐藏或完整显现的随机停留秒数。
        private float m_IntroLeft; // 原生背景进入剩余时长。
        private float m_FireLeft; // 下一轮火柱前的随机秒数。
        private int m_FireCount; // 依次2、4、6根，再从2根开始。

        /// <inheritdoc />
        protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
        {
            var owner = (BullDemonHeartEntity)body;
            body.MoveIntent = 0;
            body.Velocity = Vector2.Zero;
            body.RestartAnim(EnemyAnims.Idle);
            owner.BeginPresentation();
            m_IntroLeft = owner.IntroDuration;
            m_HeartPhase = HeartPhase.Hidden;
            m_HeartLeft = GD.RandRange(5, 9);
            m_FireLeft = GD.RandRange(2, 6);
            m_FireCount = 2;
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            var owner = (BullDemonHeartEntity)body;
            body.Velocity = Vector2.Zero;
            if (m_IntroLeft > 0)
            {
                m_IntroLeft -= dt;
                if (m_IntroLeft <= 0)
                {
                    owner.StartHands();
                }
            }
            m_FireLeft -= dt;
            if (m_FireLeft <= 0)
            {
                owner.SpawnFire(m_FireCount);
                m_FireCount = m_FireCount == 6 ? 2 : m_FireCount + 2;
                m_FireLeft = GD.RandRange(2, 6);
            }
            if (m_HeartPhase is HeartPhase.Showing or HeartPhase.Hiding)
            {
                if (owner.HeartFading)
                {
                    return;
                }

                m_HeartLeft = m_HeartPhase == HeartPhase.Showing ? GD.RandRange(2, 5) : GD.RandRange(5, 11);
                m_HeartPhase = m_HeartPhase == HeartPhase.Showing ? HeartPhase.Shown : HeartPhase.Hidden;
                return;
            }
            m_HeartLeft -= dt;
            if (m_HeartLeft <= 0)
            {
                bool show = m_HeartPhase == HeartPhase.Hidden;
                owner.FadeHeart(show);
                m_HeartPhase = show ? HeartPhase.Showing : HeartPhase.Hiding;
            }
        }

        /// <summary>
        /// 每个阶段只维护自己的下一次转换。
        /// </summary>
        private enum HeartPhase { Hidden, Showing, Shown, Hiding }
    }
}
