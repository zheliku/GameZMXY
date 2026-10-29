using GameFramework.Fsm;
using GameLogic.Enemies.Body;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 复生开始即补满本次配方生命；原生动画结束恢复行动，受击仍可正常打断。
    /// </summary>
    internal sealed class PengDemonKingRebirthState : PengDemonKingBodyState
    {
        private float m_Left; // 原生30帧复生动作的剩余时间。

        /// <inheritdoc />
        protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
        {
            var actor = (PengDemonKingEntity)body;
            actor.BeginRebirth();
            var animation = actor.AnimPlayer.GetAnimation(PengDemonKingAnims.Rebirth);
            m_Left = (float)(animation.HasMarker("complete") ? animation.GetMarkerTime("complete") : animation.Length);
            body.RestartAnim(PengDemonKingAnims.Rebirth);
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            body.TakeAttackRequest();
            ApplyGravity(body, dt);
            Stand(body);
            if (Elapsed(m_Left))
            {
                ChangeState<PengDemonKingMoveState>(fsm);
                return;
            }
            m_Left -= dt;
        }
    }
}
