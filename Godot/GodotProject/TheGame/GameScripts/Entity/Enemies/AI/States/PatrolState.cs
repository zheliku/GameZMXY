using GameFramework.Fsm;
using GameLogic.Architecture;

namespace GameLogic.Enemies.AI
{
    /// <summary>
    /// 无目标时统一执行巡逻和停步；发现目标即交给本物种的交战状态。
    /// </summary>
    public sealed class PatrolState : AbstractEnemyAiState
    {
        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
        {
            if (agent.HasTarget)
            {
                ChangeState(fsm, agent.AiPreset.EngageStateType);
                return;
            }
            agent.GetSystem<EnemyAiSystem>().Patrol(agent, elapseSeconds);
        }
    }
}
