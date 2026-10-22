namespace GameLogic.Enemies.AI;

/// <summary>小怪决策的概率规则；受控与死亡优先级继承 Enemy，BOSS 不继承小怪出手概率。</summary>
public abstract class AbstractMonsterAiState : AbstractEnemyAiState
{
    /// <summary>使用该敌人的随机源判断百分比，0 必不中、100 必中。</summary>
    /// <param name="agent">只读感知和写入意图的宿主。</param>
    /// <param name="percent">0..100 的概率。</param>
    /// <returns>本次是否命中概率。</returns>
    protected static bool Chance(IEnemyAiAgent agent, int percent) => agent.NextRandom() * 100f < percent;
}
