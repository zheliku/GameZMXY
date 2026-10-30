using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 本物种在场景出招范围内优先释放已冷却的第二招，其余节拍使用普攻。
    /// </summary>
    internal sealed class KnifeDemonEngageState : EngageState
    {
        /// <inheritdoc />
        protected override void Attack(IEnemyAiAgent agent)
        {
            agent.RequestAttack(agent.AiData.Cooldowns[1] <= 0f ? 1 : 0);
        }
    }
}
