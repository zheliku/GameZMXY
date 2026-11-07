using GameLogic.Enemies.AI;

namespace GameLogic.Enemies;

/// <summary>
/// 妖猴：采用普通敌人的巡逻和拳击，攻击前摇由原生动画提供。
/// </summary>
public partial class DemonMonkeyEntity : AbstractGroundMonsterEntity
{
    private static readonly EnemyAiPreset s_preset = EnemyAiPresets.Patrol; // Monster2 的原点出招距离。

    /// <summary>
    /// Monster2 主动索敌并追近出拳，不使用猴子的受击激怒规则。
    /// </summary>
    protected override EnemyAiPreset AiPreset => s_preset;
}
