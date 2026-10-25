using GameLogic.Enemies.AI;

namespace GameLogic.Enemies;

/// <summary>
/// 大猩猩：采用普通敌人行为；守关数值由关卡配方倍率提供。
/// </summary>
public partial class GorillaEntity : AbstractGroundMonsterEntity
{
    /// <summary>
    /// Monster3 使用近战巡逻与追击；守关身份不改变行为，生命和经验由出生配方决定。
    /// </summary>
    protected override EnemyAiPreset AiPreset
    {
        get
        {
            return MonsterAiPresets.Patrol;
        }
    }
}
