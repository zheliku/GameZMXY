namespace GameLogic.Enemies.AI;

/// <summary>
/// 普通敌人的巡逻与交战配方；只组合行为类型，不携带空间或数值配置。
/// </summary>
public static class EnemyAiPresets
{
    /// <summary>
    /// 无目标时巡逻，发现目标后按场景出招范围交战。
    /// </summary>
    public static EnemyAiPreset Patrol { get; } = new(typeof(PatrolState), typeof(EngageState),
        () => new PatrolState(), () => new EngageState());
}
