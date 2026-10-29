namespace GameLogic.Enemies.AI
{
    /// <summary>
    /// 敌人的巡逻、守卫与追击预设；按行为能力组合，不限定小怪或首领分类。
    /// </summary>
    public static class EnemyAiPresets
    {
        /// <summary>
        /// 无目标时巡逻，发现目标后追击。
        /// </summary>
        public static EnemyAiPreset Patrol { get; } = new(typeof(WanderState), typeof(WalkToTargetState),
            () => new PauseState(), () => new WanderState(), () => new WalkToTargetState(),
            () => new StandAndStrikeState(), () => new PaceBelowTargetState(),
            () => new CcLockedState(), () => new DeathState());

        /// <summary>
        /// 原地守卫，仅向进入范围的目标出招。
        /// </summary>
        public static EnemyAiPreset Guard { get; } = new(typeof(PauseState), typeof(GuardTargetState),
            () => new PauseState(), () => new GuardTargetState(), () => new CcLockedState(), () => new DeathState());

        /// <summary>
        /// 无目标时原地等待，发现目标后追击。
        /// </summary>
        public static EnemyAiPreset Pursuer { get; } = new(typeof(PauseState), typeof(WalkToTargetState),
            () => new PauseState(), () => new WalkToTargetState(), () => new StandAndStrikeState(),
            () => new PaceBelowTargetState(), () => new CcLockedState(), () => new DeathState());

    }
}
