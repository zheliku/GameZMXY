using System;
using GameFramework.Entity;
using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;

namespace GameLogic.Enemies;

/// <summary>
/// 飞行小怪：拥有垂直意图、空中出招和身体状态；具体寻敌及退避归角色AI。
/// </summary>
public abstract partial class AbstractSkyMonsterEntity : AbstractMonsterEntity, IEnemyFreeMoveBody
{
    [Export] private CollisionShape2D _trackingRange; // 空中物种的追踪高度上限，独立于近身选招范围。

    private EnemyBodyPreset _bodyPreset; // 本物种的只读飞行配方，状态实例由每次显示创建。

    /// <summary>
    /// 目标高差允许追踪的半幅，像素；来自本物种原生场景。
    /// </summary>
    internal float TrackingHeight => _trackingRange.Shape.GetRect().End.Y;

    /// <summary>
    /// AI写入、身体消费的垂直意图：-1上升、0悬停、1下降。
    /// </summary>
    public int VerticalIntent
    {
        get => AiData.VerticalIntent;
        internal set => AiData.VerticalIntent = value;
    }

    /// <summary>
    /// 直接采用移动速度而不经过垂直加速，由具体AI按运动阶段选择。
    /// </summary>
    public bool DirectVerticalMovement
    {
        get => AiData.DirectVerticalMovement;
        internal set => AiData.DirectVerticalMovement = value;
    }

    /// <summary>
    /// 供具体飞行AI读取的世界高度。
    /// </summary>
    public float Altitude => GlobalPosition.Y;

    /// <summary>
    /// 本物种追踪时的垂直加速度，px/s²。
    /// </summary>
    protected abstract float VerticalAcceleration { get; }

    /// <summary>
    /// 本物种追踪时的垂直速度上限，px/s。
    /// </summary>
    protected abstract float VerticalSpeed { get; }

    /// <inheritdoc />
    protected override EnemyBodyPreset BodyPreset => _bodyPreset ??= EnemyBodyPresets.Fly(VerticalAcceleration, VerticalSpeed);

    /// <inheritdoc />
    public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
    {
        base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
        if (isNewInstance && (_trackingRange?.Shape is not SegmentShape2D || !_trackingRange.Disabled))
        {
            throw new InvalidOperationException("飞行怪物必须绑定独立的追踪高度范围。");
        }
    }

    /// <summary>
    /// 空中移动时允许出招；受击、死亡、已有请求和其他身体阶段仍拒绝。
    /// </summary>
    /// <param name="index">本物种的招式下标。</param>
    /// <returns>是否已登记新攻击请求。</returns>
    protected override bool TryRequestAttack(int index)
    {
        if (BodyFsm?.CurrentState is not { } current || current.GetType() != BodyPreset.MovementStateType ||
            !base.TryRequestAttack(index))
        {
            return false;
        }

        VerticalIntent = 0;
        return true;
    }
}
