using GameFramework.Fsm;
using System;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies.Body
{
    /// <summary>
    /// 自由游动保持双轴速度和垂直加速；出招保留入射速度，具体攻击状态由配方选择。
    /// </summary>
    /// <param name="verticalAcceleration">垂直加速度，px/s²。</param>
    /// <param name="verticalSpeed">垂直速度上限，px/s。</param>
    /// <param name="directSpeed">直接上/下游阶段的速度，px/s。</param>
    /// <param name="attackStateType">本配方使用的攻击状态。</param>
    public sealed class EnemySwimState(float verticalAcceleration, float verticalSpeed, float directSpeed,
        Type attackStateType) : AbstractEnemyBodyState
    {
        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            int request = body.TakeAttackRequest();
            if (request >= 0 && request < body.Params.AttackCount)
            {
                fsm.SetData(AttackIndexKey, (VarInt32)request);
                ChangeState(fsm, attackStateType);
                return;
            }
            ApplyFreeMovement((IEnemyFreeMoveBody)body, dt, verticalAcceleration, verticalSpeed, directSpeed);
        }
    }
}
