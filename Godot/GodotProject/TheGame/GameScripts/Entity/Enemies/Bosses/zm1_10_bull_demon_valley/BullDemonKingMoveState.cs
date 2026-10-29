using System;
using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 人形走近目标并把选招请求交给专属身体状态，保留真实落地约束。
    /// </summary>
    internal sealed class BullDemonKingMoveState : AbstractEnemyBodyState
    {
        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            int request = body.TakeAttackRequest();
            if (body.OnFloor && request >= 0 && request < body.Params.AttackCount)
            {
                fsm.SetData(AttackIndexKey, (VarInt32)request);
                ChangeState<BullDemonKingAttackState>(fsm);
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
