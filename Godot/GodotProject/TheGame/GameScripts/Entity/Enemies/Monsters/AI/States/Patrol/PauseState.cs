using GameFramework.Fsm;

namespace GameLogic.Enemies.AI
{
    /// <summary>按巡逻停留概率原地暂停，到点恢复游荡。</summary>
    public class PauseState : AbstractRoamState
    {
        private float m_Timer; // 当前巡逻停留剩余时间（秒）。

        /// <summary>重置暂停时长为巡逻间隔。</summary>
        protected override void Enter(IEnemyAiAgent agent)
        {
            base.Enter(agent);
            m_Timer = agent.Config.PatrolInterval;
        }

        /// <summary>倒计时结束后恢复游荡。</summary>
        protected override void Roam(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
        {
            m_Timer -= elapseSeconds;
            if (m_Timer <= 0f && agent.AiPreset.IdleStateType != typeof(PauseState))
            {
                ChangeState(fsm, agent.AiPreset.IdleStateType);
            }
        }
    }
}
