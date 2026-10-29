using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using GameLogic.Enemies.AI;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 四招身体状态：原生帧控制判定和生火，第17帧起的火鸟冲刺保留水平运动。
    /// </summary>
    internal sealed class PengDemonKingAttackState : PengDemonKingBodyState
    {
        private int m_Index; // 本次招式下标。
        private float m_Left; // 按原生完成标记计时的剩余秒数。
        private bool m_Dashing; // 当前冲刺已越过原生起跑事件。

        /// <inheritdoc />
        protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
        {
            m_Index = fsm.GetData<VarInt32>(AttackIndexKey).Value;
            fsm.RemoveData(AttackIndexKey);
            body.MoveIntent = 0;
            body.Velocity = Vector2.Zero;
            m_Dashing = false;
            body.BeginAttack(m_Index);
            if (m_Index is 0 or 2)
            {
                var data = ((IEnemyAiAgent)body).AiData;
                data.Cooldowns[2 - m_Index] = data.Cooldowns[m_Index];
            }
            m_Left = body.Params.AttackTimes[m_Index];
            body.RestartAnim(body.Params.AttackAnims[m_Index]);
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            ApplyGravity(body, dt);
            if (!m_Dashing)
            {
                Stand(body);
            }

            if (Elapsed(m_Left))
            {
                body.Velocity = Vector2.Zero;
                ChangeState<PengDemonKingMoveState>(fsm);
                return;
            }
            m_Left -= dt;
        }

        /// <inheritdoc />
        protected override void Leave(IFsm<IEnemyBody> fsm, IEnemyBody body, bool isShutdown)
        {
            body.MoveIntent = 0;
            body.EndAttack();
        }

        /// <summary>
        /// 动画事件开始本次火鸟冲刺；原版25px/源帧换算为600px/s。
        /// </summary>
        /// <param name="actor">正在出招的具体首领。</param>
        internal void BeginDash(PengDemonKingEntity actor)
        {
            if (m_Index != 1)
            {
                return;
            }

            m_Dashing = true;
            actor.Velocity = new Vector2(actor.Facing * 600f, actor.Velocity.Y);
        }
    }
}
