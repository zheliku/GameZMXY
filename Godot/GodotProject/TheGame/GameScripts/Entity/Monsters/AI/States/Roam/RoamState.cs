using GameLogic.Monsters.AI;
using GameFramework.Fsm;

namespace GameLogic.Monsters.AI
{
    /// <summary>
    /// 无目标游荡状态的共同规则：一旦有目标立刻转追击；否则交给 <see cref="Roam"/>。
    /// </summary>
    public abstract class RoamState : MonsterAiState
    {
        /// <summary>发现目标时转入追击，否则执行当前无目标决策。</summary>
        protected sealed override void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
        {
            if (agent.TargetBox.HasArea())
            {
                ChangeState<WalkToTargetState>(fsm);
                return;
            }

            Roam(fsm, agent, elapseSeconds);
        }

        /// <summary>无目标时的决策。</summary>
        protected abstract void Roam(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds);
    }
}
