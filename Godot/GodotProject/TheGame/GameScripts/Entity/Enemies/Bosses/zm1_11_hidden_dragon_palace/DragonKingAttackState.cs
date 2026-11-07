using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies;

/// <summary>
/// 龙王落地施法不位移，冰环另固定全部速度；六次攻击编号由原生动画维护。
/// </summary>
internal sealed class DragonKingAttackState : AbstractEnemyBodyState
{
    private int _index; // 当前招式在同一身体动作库中的序号。
    private float _left; // 原生complete标记前的剩余秒数。

    /// <inheritdoc />
    protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
    {
        _index = fsm.GetData<VarInt32>(AttackIndexKey).Value;
        fsm.RemoveData(AttackIndexKey);
        body.BeginAttack(_index);
        _left = body.Params.AttackTimes[_index];
        if (_index == 1)
        {
            body.Velocity = Vector2.Zero;
        }
        else if (body.OnFloor)
        {
            Stand(body);
        }
        body.RestartAnim(body.Params.AttackAnims[_index]);
    }

    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
    {
        if (_index == 1)
        {
            body.Velocity = Vector2.Zero;
        }
        else
        {
            ApplyGravity(body, dt);
            if (body.OnFloor)
            {
                // BaseObject.move仅在走跑、空中或受击时推进X；落地施法保留原地。
                Stand(body);
            }
        }

        if (Elapsed(_left))
        {
            ChangeState(fsm, body.MovementStateType);
            return;
        }
        _left -= dt;
    }

    /// <inheritdoc />
    protected override void Leave(IFsm<IEnemyBody> fsm, IEnemyBody body, bool isShutdown)
    {
        body.EndAttack();
        body.MoveIntent = 0;
    }
}
