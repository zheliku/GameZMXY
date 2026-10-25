using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies;

/// <summary>
/// 飞行身体：消费水平与垂直意图，在空中提交攻击；受击和死亡沿用敌方打断。
/// </summary>
internal sealed class SkyMonsterMoveState(float verticalAcceleration, float verticalSpeed) : AbstractEnemyBodyState
{

    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
    {
        int request = body.TakeAttackRequest();
        if (request >= 0 && request < body.Params.AttackCount)
        {
            body.Velocity = Vector2.Zero;
            fsm.SetData(AttackIndexKey, (VarInt32)request);
            ChangeState<EnemyAttackState>(fsm);
            return;
        }
        var flight = (AbstractSkyMonsterEntity)body;
        int vertical = flight.VerticalIntent;
        float speedY = vertical == 0 ? 0f : flight.DirectVerticalMovement ? vertical * body.Params.MoveSpeed :
            Mathf.Clamp(body.Velocity.Y + vertical * verticalAcceleration * dt, -verticalSpeed, verticalSpeed);
        body.Velocity = new Vector2(body.MoveIntent * body.Params.MoveSpeed, speedY);
        body.SetFacing(body.MoveIntent);
        body.PlayAnim(body.MoveIntent == 0 ? EnemyAnims.Idle : EnemyAnims.Run);
    }
}
