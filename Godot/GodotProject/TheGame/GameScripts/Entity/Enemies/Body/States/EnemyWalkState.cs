using System;
using GameFramework.Fsm;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies.Body
{
    /// <summary>
    /// 行走身体：执行水平意图与重力，只在落地时接受出招请求。
    /// </summary>
    public sealed class EnemyWalkState : AbstractEnemyBodyState
    {
        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            int request = body.TakeAttackRequest();
            if (body.OnFloor && request >= 0 && request < body.Params.AttackCount)
            {
                // 切换参数归 FSM 拥有，不提前取得并修改另一个状态实例。
                fsm.SetData(AttackIndexKey, (VarInt32)request);
                ChangeState<EnemyAttackState>(fsm);
                return;
            }

            // 已落地时清除上一物理步的坡面纵向分量，实际离地后继续正常重力。
            if (body.OnFloor)
            {
                body.Velocity = new Vector2(body.Velocity.X, 0f);
            }

            ApplyGravity(body, dt);
            int move = Math.Clamp(body.MoveIntent, -1, 1);
            body.Velocity = new Vector2(move * body.Params.MoveSpeed, body.Velocity.Y);
            body.SetFacing(move);
            body.PlayAnim(move == 0 ? EnemyAnims.Idle : EnemyAnims.Run);
        }
    }
}
