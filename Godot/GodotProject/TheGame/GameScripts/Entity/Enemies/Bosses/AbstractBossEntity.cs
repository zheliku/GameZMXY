using System;
using GameFramework.Fsm;
using GameLogic.Enemies.Body;

namespace GameLogic.Enemies;

/// <summary>
/// 首领分支装配当前首领的地面身体；独立效果继承 Enemy，专属选招与无敌由具体首领实现。
/// </summary>
public abstract partial class AbstractBossEntity : AbstractEnemyEntity
{
    /// <summary>
    /// 官方首领进入死亡动作后立即停止受击，避免守关条与尸体重复结算。
    /// </summary>
    protected override bool KeepHurtBoxDuringDeath
    {
        get
        {
            return false;
        }
    }

    /// <summary>
    /// 现有首领采用地面身体，具体首领可按真实运动方式替换。
    /// </summary>
    protected override Type InitialBodyStateType
    {
        get
        {
            return typeof(EnemyMoveState);
        }
    }

    /// <inheritdoc />
    protected override FsmState<IEnemyBody>[] CreateBodyStates()
    {
        return [new EnemyMoveState(), .. base.CreateBodyStates()];
    }

    /// <summary>
    /// 当前地面首领落地后才可出招，通用忙碌/死亡校验仍归Enemy。
    /// </summary>
    /// <param name="index">首领招式下标。</param>
    /// <returns>是否接受请求。</returns>
    protected override bool TryRequestAttack(int index)
    {
        return OnFloor && base.TryRequestAttack(index);
    }
}
