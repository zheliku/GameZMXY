using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;

namespace GameLogic.Enemies;

/// <summary>
/// 兽形身体让冲撞判定、无敌和运动同启停，每次折返重新装填攻击编号。
/// </summary>
internal sealed class BullDemonChargeState : AbstractEnemyBodyState
{
    private int _direction; // 上次身体帧实际使用的方向，零表示停步暴露。

    /// <inheritdoc />
    protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
    {
        _direction = 0;
    }

    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
    {
        int direction = body.MoveIntent;
        if (direction != _direction)
        {
            body.EndAttack();
            if (direction != 0)
            {
                body.BeginAttack(0);
            }

            _direction = direction;
            body.RestartAnim(direction == 0 ? EnemyAnims.Idle : "charge");
        }
        if (body.OnFloor)
        {
            body.Velocity = new Vector2(body.Velocity.X, 0);
        }

        ApplyGravity(body, dt);
        body.SetFacing(direction);
        body.Velocity = new Vector2(direction * body.Params.MoveSpeed, body.Velocity.Y);
    }

    /// <inheritdoc />
    protected override void Leave(IFsm<IEnemyBody> fsm, IEnemyBody body, bool isShutdown)
    {
        body.EndAttack();
    }
}
