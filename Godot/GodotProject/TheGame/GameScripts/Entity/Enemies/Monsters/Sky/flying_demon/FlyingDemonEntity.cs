using GameLogic.Enemies.AI;

namespace GameLogic.Enemies;

/// <summary>
/// 造一飞行魔：使用空中身体，保留自己的高度追踪、延迟折返和双段羽刃攻击。
/// </summary>
public partial class FlyingDemonEntity : AbstractSkyMonsterEntity
{
    private EnemyAiPreset _preset; // 共用追踪规则，注入飞行魔的60px悬停高度和本场景追踪范围。

    /// <inheritdoc />
    protected override EnemyAiPreset AiPreset
    {
        get
        {
            return _preset ??= new(typeof(FlyingPursuitState), typeof(FlyingPursuitState),
                () => new FlyingPursuitState(pursuitHeight: 60f, trackingHeight: TrackingHeight,
                    altitude: () => Altitude));
        }
    }

    /// <inheritdoc />
    protected override float VerticalAcceleration => 288f;

    /// <inheritdoc />
    protected override float VerticalSpeed => 96f;
}
