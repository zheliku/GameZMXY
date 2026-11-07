using GameLogic.Enemies.AI;

namespace GameLogic.Enemies;

/// <summary>
/// 七星洞近战小怪：复用普通巡逻、追击与攻击状态。
/// </summary>
public partial class CaveBruteEntity : AbstractGroundMonsterEntity
{
    private static readonly EnemyAiPreset s_preset = EnemyAiPresets.Patrol; // Monster7 的原点出招距离。

    /// <summary>
    /// Monster7 使用近战巡逻与追击；攻击前摇和出拳窗口由本角色动画提供。
    /// </summary>
    protected override EnemyAiPreset AiPreset => s_preset;
}
