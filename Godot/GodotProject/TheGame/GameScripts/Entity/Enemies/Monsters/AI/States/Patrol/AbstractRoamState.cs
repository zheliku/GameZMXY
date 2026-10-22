using GameLogic.Architecture;
using GameFramework.Fsm;

namespace GameLogic.Enemies.AI
{
    /// <summary>
    /// 无目标游荡状态的共同规则：一旦有目标立刻转追击；否则交给 <see cref="Roam"/>。
    /// </summary>
    public abstract class AbstractRoamState : AbstractMonsterAiState
    {
        /// <summary>发现目标时转入追击，否则执行当前无目标决策。</summary>
        protected sealed override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
        {
            if (agent.TargetBox.HasArea())
            {
                ChangeState(fsm, agent.AiPreset.EngageStateType);
                return;
            }

            Roam(fsm, agent, elapseSeconds);
        }

        /// <summary>无目标时的决策。</summary>
        protected abstract void Roam(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds);
    }
}
