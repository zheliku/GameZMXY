using System;
using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 龙王保留地面行走速度，再把有效请求交给允许部分攻击继续运动的身体状态。
    /// </summary>
    internal sealed class DragonKingMoveState : AbstractEnemyBodyState
    {
        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            int request = body.TakeAttackRequest();
            if (body.OnFloor && request >= 0 && request < body.Params.AttackCount)
            {
                fsm.SetData(AttackIndexKey, (VarInt32)request);
                ChangeState<DragonKingAttackState>(fsm);
                return;
            }
            if (body.OnFloor)
            {
                body.Velocity = new Vector2(body.Velocity.X, 0);
            }

            ApplyGravity(body, dt);
            int move = Math.Clamp(body.MoveIntent, -1, 1);
            body.Velocity = new Vector2(move * body.Params.MoveSpeed, body.Velocity.Y);
            body.SetFacing(move);
            body.PlayAnim(move == 0 ? EnemyAnims.Idle : EnemyAnims.Run);
        }
    }
}
