using GameLogic.Enemies.AI;

namespace GameLogic.Enemies;

/// <summary>造一禺狨王：专属追击和落雷选择，施法前两秒无敌，后段可以受击打断。</summary>
public partial class TamarinKingEntity : AbstractBossEntity
{
    private const double InvulnerabilitySeconds = 48.0 / 24.0; // Monster6.setYourFather(48)，按源 24fps 换算。
    private static readonly EnemyAiPreset m_Preset = new(typeof(TamarinKingEngageState), typeof(TamarinKingEngageState),
        () => new TamarinKingEngageState(),
        () => new CcLockedState(), () => new DeathState()); // 各实例独立的选择和受控状态。

    /// <inheritdoc />
    protected override EnemyAiPreset AiPreset => m_Preset;

    /// <summary>无敌只覆盖官方 48 帧窗口，动画剩余部分保持正常受击。</summary>
    protected override bool Invulnerable => AttackSegment == 1 && AnimPlayer.CurrentAnimationPosition < InvulnerabilitySeconds;

}
