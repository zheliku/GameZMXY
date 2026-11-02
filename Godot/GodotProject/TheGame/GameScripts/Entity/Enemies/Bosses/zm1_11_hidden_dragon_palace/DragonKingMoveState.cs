using System;
using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 龙王地面游荡和追击；有效请求交给负责停止落地施法位移的攻击状态。
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
