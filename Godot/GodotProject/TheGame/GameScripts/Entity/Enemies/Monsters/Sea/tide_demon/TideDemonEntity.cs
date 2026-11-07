using GameLogic.Enemies.AI;

namespace GameLogic.Enemies;

/// <summary>
/// 潮汐妖：普通斩击和冷却重击；本版源时间轴没有调用其旧投刀方法。
/// </summary>
public partial class TideDemonEntity : AbstractSeaMonsterEntity
{
    private static readonly EnemyAiPreset s_preset = new(typeof(PatrolState), typeof(EngageState),
        () => new PatrolState(), () => new EngageState(cooldownAttackIndex: 1)); // 巡逻和本物种交战共用实例时钟。

    /// <inheritdoc />
    protected override EnemyAiPreset AiPreset => s_preset;
}
