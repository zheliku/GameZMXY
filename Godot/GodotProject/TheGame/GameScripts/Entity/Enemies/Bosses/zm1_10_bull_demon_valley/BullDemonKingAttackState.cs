using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies;

/// <summary>
/// 人形招式状态拥有冲刺、吞噬中断和收招，时点由本角色原生方法轨道触发。
/// </summary>
internal sealed class BullDemonKingAttackState : AbstractEnemyBodyState
{
    private float _left; // 本次招式原生完成标记前的秒数。
    private int _index; // 进入时固定的攻击下标。
    private float _dashSpeed; // 原生第30源帧开始的冲刺速度。
    private bool _captureMissed; // 第28帧没有抓到目标时，下个身体帧立即收招。

    /// <inheritdoc />
    protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
    {
        _index = fsm.GetData<VarInt32>(AttackIndexKey).Value;
        fsm.RemoveData(AttackIndexKey);
        _dashSpeed = 0f;
        _captureMissed = false;
        body.MoveIntent = 0;
        body.BeginAttack(_index);
        _left = body.Params.AttackTimes[_index];
        body.RestartAnim(body.Params.AttackAnims[_index]);
    }

    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
    {
        ApplyGravity(body, dt);
        body.Velocity = new Vector2(_dashSpeed, body.Velocity.Y);
        if (_captureMissed || Elapsed(_left))
        {
            ChangeState(fsm, body.MovementStateType);
            return;
        }
        _left -= dt;
    }

    /// <inheritdoc />
    protected override void Leave(IFsm<IEnemyBody> fsm, IEnemyBody body, bool isShutdown)
    {
        if (!isShutdown)
        {
            ((BullDemonKingEntity)body).ReleaseCaptive();
        }

        body.MoveIntent = 0;
        body.EndAttack();
    }

    /// <summary>
    /// 冲刺方法键仅登记速度，物理移动仍由正常身体帧执行。
    /// </summary>
    /// <param name="direction">起手时锁定的朝向。</param>
    internal void BeginDash(int direction)
    {
        _dashSpeed = direction * 360f;
    }

    /// <summary>
    /// 吞噬失败只登记收招，不在方法轨道中切换动画或状态。
    /// </summary>
    internal void MissCapture()
    {
        _captureMissed = true;
    }
}
