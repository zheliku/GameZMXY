using System;
using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 鲨魔王移动时随生命阶段选择walk1/walk2，攻击请求交给本首领身体状态。
    /// </summary>
    internal sealed class SharkDemonKingMoveState : AbstractEnemyBodyState
    {
        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            int request = body.TakeAttackRequest();
            if (body.OnFloor && request >= 0 && request < body.Params.AttackCount)
            {
                fsm.SetData(AttackIndexKey, (VarInt32)request);
                ChangeState<SharkDemonKingAttackState>(fsm);
                return;
            }
            if (body.OnFloor)
            {
                body.Velocity = new Vector2(body.Velocity.X, 0f);
            }

            ApplyGravity(body, dt);
            int move = Math.Clamp(body.MoveIntent, -1, 1);
            body.Velocity = new Vector2(move * body.Params.MoveSpeed, body.Velocity.Y);
            body.SetFacing(move);
            bool low = ((SharkDemonKingEntity)body).LowPhase;
            if (move == 0)
            {
                body.PlayAnim(low ? "idle_low" : "idle");
            }
            else
            {
                body.PlayAnim(low ? "run_low" : "run");
            }
        }
    }
}
