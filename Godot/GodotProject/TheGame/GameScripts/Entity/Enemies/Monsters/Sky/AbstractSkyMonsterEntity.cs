using System;
using GameFramework.Fsm;
using GameLogic.Enemies.Body;

namespace GameLogic.Enemies;

/// <summary>
/// 飞行小怪：拥有垂直意图、空中出招和身体状态；具体寻敌及退避归角色AI。
/// </summary>
public abstract partial class AbstractSkyMonsterEntity : AbstractMonsterEntity
{
    /// <summary>
    /// AI写入、身体消费的垂直意图：-1上升、0悬停、1下降。
    /// </summary>
    public int VerticalIntent { get; internal set; }

    /// <summary>
    /// 直接采用移动速度而不经过垂直加速，由具体AI按运动阶段选择。
    /// </summary>
    public bool DirectVerticalMovement { get; internal set; }

    /// <summary>
    /// 供具体飞行AI读取的世界高度。
    /// </summary>
    public float Altitude
    {
        get
        {
            return GlobalPosition.Y;
        }
    }

    /// <summary>
    /// 本物种追踪时的垂直加速度，px/s²。
    /// </summary>
    protected abstract float VerticalAcceleration { get; }

    /// <summary>
    /// 本物种追踪时的垂直速度上限，px/s。
    /// </summary>
    protected abstract float VerticalSpeed { get; }

    /// <inheritdoc />
    protected override Type InitialBodyStateType
    {
        get
        {
            return typeof(SkyMonsterMoveState);
        }
    }

    /// <inheritdoc />
    protected override FsmState<IEnemyBody>[] CreateBodyStates()
    {
        return [new SkyMonsterMoveState(VerticalAcceleration, VerticalSpeed), .. base.CreateBodyStates()];
    }

    /// <inheritdoc />
    public override void OnShow(object userData)
    {
        VerticalIntent = 0;
        DirectVerticalMovement = false;
        base.OnShow(userData);
    }

    /// <summary>
    /// 空中移动时允许出招；受击、死亡、已有请求和其他身体阶段仍拒绝。
    /// </summary>
    /// <param name="index">本物种的招式下标。</param>
    /// <returns>是否已登记新攻击请求。</returns>
    protected override bool TryRequestAttack(int index)
    {
        if (m_BodyFsm?.CurrentState is not SkyMonsterMoveState || !base.TryRequestAttack(index))
            return false;
        VerticalIntent = 0;
        return true;
    }
}
