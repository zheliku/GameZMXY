using GameFramework.Fsm;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies.Body;

/// <summary>
/// 飞行身体：消费水平与垂直意图，在空中提交攻击；受击和死亡沿用敌方打断。
/// </summary>
/// <param name="verticalAcceleration">垂直加速度，px/s²。</param>
/// <param name="verticalSpeed">垂直速度上限，px/s。</param>
public sealed class EnemyFlyState(float verticalAcceleration, float verticalSpeed) : AbstractEnemyBodyState
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
        ApplyFreeMovement((IEnemyFreeMoveBody)body, dt, verticalAcceleration, verticalSpeed, body.Params.MoveSpeed);
    }
}
