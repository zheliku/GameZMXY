using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using GodotGameFramework;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 龙王仅冰环定身，其余攻击保留起手水平运动；六次攻击编号由原生动画维护。
    /// </summary>
    internal sealed class DragonKingAttackState : AbstractEnemyBodyState
    {
        private int m_Index; // 当前招式在同一身体动作库中的序号。
        private float m_Left; // 原生complete标记前的剩余秒数。

        /// <inheritdoc />
        protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
        {
            m_Index = fsm.GetData<VarInt32>(AttackIndexKey).Value;
            fsm.RemoveData(AttackIndexKey);
            body.BeginAttack(m_Index);
            m_Left = body.Params.AttackTimes[m_Index];
            body.RestartAnim(body.Params.AttackAnims[m_Index]);
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            ApplyGravity(body, dt);
            if (m_Index == 1)
            {
                Stand(body);
            }

            if (Elapsed(m_Left))
            {
                ChangeState<DragonKingMoveState>(fsm);
                return;
            }
            m_Left -= dt;
        }

        /// <inheritdoc />
        protected override void Leave(IFsm<IEnemyBody> fsm, IEnemyBody body, bool isShutdown)
        {
            body.EndAttack();
            body.MoveIntent = 0;
        }
    }
}
