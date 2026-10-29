using GameLogic.Architecture;
using GameFramework.Fsm;

namespace GameLogic.Enemies.AI
{
    /// <summary>
    /// 无目标游荡状态的共同规则：一旦有目标立刻转追击；否则交给 <see cref="Roam"/>。
    /// </summary>
    public abstract class AbstractRoamState : AbstractEnemyAiState
    {
        /// <summary>
        /// 发现目标时转入追击，否则执行当前无目标决策。
        /// </summary>
        /// <param name="fsm">当前宿主拥有的状态机。</param>
        /// <param name="agent">当前状态借用的敌人AI宿主。</param>
        /// <param name="elapseSeconds">逻辑流逝时间，单位为秒。</param>
        protected sealed override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
        {
            if (agent.TargetBox.HasArea())
            {
                ChangeState(fsm, agent.AiPreset.EngageStateType);
                return;
            }

            Roam(fsm, agent, elapseSeconds);
        }

        /// <summary>
        /// 无目标时的决策。
        /// </summary>
        /// <param name="fsm">当前宿主拥有的状态机。</param>
        /// <param name="agent">当前状态借用的敌人AI宿主。</param>
        /// <param name="elapseSeconds">逻辑流逝时间，单位为秒。</param>
        protected abstract void Roam(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds);
    }
}
