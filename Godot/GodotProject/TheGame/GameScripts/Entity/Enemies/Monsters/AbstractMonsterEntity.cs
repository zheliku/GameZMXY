using GameLogic.Enemies.AI;

namespace GameLogic.Enemies;

/// <summary>普通敌人分支：采用巡逻、感知和按配置概率选招；身体、生命和受击机制继承 Enemy。</summary>
public abstract partial class AbstractMonsterEntity : AbstractEnemyEntity
{
    /// <summary>普通地面怪默认巡逻预设，特殊小怪可替换为守卫或追击集合。</summary>
    protected override EnemyAiPreset AiPreset => MonsterAiPresets.Patrol;
}
