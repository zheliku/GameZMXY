using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 人形招式状态拥有冲刺、吞噬中断和收招，时点由本角色原生方法轨道触发。
    /// </summary>
    internal sealed class BullDemonKingAttackState : AbstractEnemyBodyState
    {
        private float m_Left; // 本次招式原生完成标记前的秒数。
        private int m_Index; // 进入时固定的攻击下标。
        private float m_DashSpeed; // 原生第30源帧开始的冲刺速度。
        private bool m_CaptureMissed; // 第28帧没有抓到目标时，下个身体帧立即收招。

        /// <inheritdoc />
        protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
        {
            m_Index = fsm.GetData<VarInt32>(AttackIndexKey).Value;
            fsm.RemoveData(AttackIndexKey);
            m_DashSpeed = 0f;
            m_CaptureMissed = false;
            body.MoveIntent = 0;
            body.BeginAttack(m_Index);
            m_Left = body.Params.AttackTimes[m_Index];
            body.RestartAnim(body.Params.AttackAnims[m_Index]);
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            ApplyGravity(body, dt);
            body.Velocity = new Vector2(m_DashSpeed, body.Velocity.Y);
            if (m_CaptureMissed || Elapsed(m_Left))
            {
                ChangeState(fsm, body.MovementStateType);
                return;
            }
            m_Left -= dt;
        }

        /// <inheritdoc />
        protected override void Leave(IFsm<IEnemyBody> fsm, IEnemyBody body, bool isShutdown)
        {
            if (!isShutdown)
            {
                ((BullDemonKingEntity)body).ReleaseCaptive();
            }

            body.MoveIntent = 0;
            body.EndAttack();
        }

        /// <summary>
        /// 冲刺方法键仅登记速度，物理移动仍由正常身体帧执行。
        /// </summary>
        /// <param name="direction">起手时锁定的朝向。</param>
        internal void BeginDash(int direction)
        {
            m_DashSpeed = direction * 360f;
        }

        /// <summary>
        /// 吞噬失败只登记收招，不在方法轨道中切换动画或状态。
        /// </summary>
        internal void MissCapture()
        {
            m_CaptureMissed = true;
        }
    }
}
