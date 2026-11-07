using System;
using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies;

/// <summary>
/// 彭魔王消费追击意图，地面行走或追近150px飞行高度，并提交自己的招式状态。
/// </summary>
internal sealed class PengDemonKingMoveState : PengDemonKingBodyState
{
    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
    {
        var actor = (PengDemonKingEntity)body;
        int request = body.TakeAttackRequest();
        if (request >= 0 && request < body.Params.AttackCount && (actor.Flying || body.OnFloor))
        {
            fsm.SetData(AttackIndexKey, (VarInt32)request);
            ChangeState<PengDemonKingAttackState>(fsm);
            return;
        }

        float speedY = body.Velocity.Y;
        if (actor.Flying)
        {
            float difference = 150f - actor.GlobalPosition.Y;
            speedY = Math.Clamp(difference / dt, -body.Params.MoveSpeed, body.Params.MoveSpeed);
        }
        else
        {
            ApplyGravity(body, dt);
            speedY = body.Velocity.Y;
        }
        body.Velocity = new Vector2(body.MoveIntent * body.Params.MoveSpeed, speedY);
        body.SetFacing(body.MoveIntent);
        body.PlayAnim(actor.Flying ? PengDemonKingAnims.Fly :
            body.MoveIntent == 0 ? EnemyAnims.Idle : EnemyAnims.Run);
    }
}
