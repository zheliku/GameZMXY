using GameFramework.Fsm;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies;

/// <summary>
/// 四招身体状态：原生帧控制判定和生火，第17帧起的火鸟冲刺保留水平运动。
/// </summary>
internal sealed class PengDemonKingAttackState : PengDemonKingBodyState
{
    private int _index; // 本次招式下标。
    private float _left; // 按原生完成标记计时的剩余秒数。
    private bool _dashing; // 当前冲刺已越过原生起跑事件。

    /// <inheritdoc />
    protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
    {
        _index = fsm.GetData<VarInt32>(AttackIndexKey).Value;
        fsm.RemoveData(AttackIndexKey);
        body.MoveIntent = 0;
        body.Velocity = Vector2.Zero;
        _dashing = false;
        body.BeginAttack(_index);
        if (_index is 0 or 2)
        {
            EnemyAiData data = ((IEnemyAiAgent)body).AiData;
            data.Cooldowns[2 - _index] = data.Cooldowns[_index];
        }
        _left = body.Params.AttackTimes[_index];
        body.RestartAnim(body.Params.AttackAnims[_index]);
    }

    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
    {
        ApplyGravity(body, dt);
        if (!_dashing)
        {
            Stand(body);
        }

        if (Elapsed(_left))
        {
            body.Velocity = Vector2.Zero;
            ChangeState<PengDemonKingMoveState>(fsm);
            return;
        }
        _left -= dt;
    }

    /// <inheritdoc />
    protected override void Leave(IFsm<IEnemyBody> fsm, IEnemyBody body, bool isShutdown)
    {
        body.MoveIntent = 0;
        body.EndAttack();
    }

    /// <summary>
    /// 动画事件开始本次火鸟冲刺；原版25px/源帧换算为600px/s。
    /// </summary>
    /// <param name="actor">正在出招的具体首领。</param>
    internal void BeginDash(PengDemonKingEntity actor)
    {
        if (_index != 1)
        {
            return;
        }

        _dashing = true;
        actor.Velocity = new Vector2(actor.Facing * 600f, actor.Velocity.Y);
    }
}
