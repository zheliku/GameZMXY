using GameLogic.Architecture;
using GameLogic.Enemies;
using GameFramework.Fsm;

namespace GameLogic.Enemies.AI
{
    /// <summary>
    /// 受控期间不做决策（击退速度由实体物理保持）；解除后再僵直 BehitCalmTime 秒（旧 behit_calmtime），
    /// 然后有目标 → WalkToTarget，否则 → Wander。被打会锁定攻击者（AbstractEnemyEntity.OnHurt），所以挨打后通常直接反击。
    /// </summary>
    public class CcLockedState : AbstractEnemyAiState
    {
        private float m_CalmElapsed; // 解除控制后已累计的平静时间（秒）。

        /// <summary>
        /// 进入受控状态时清零解除后的恢复计时。
        /// </summary>
        /// <param name="agent">当前状态借用的敌人AI宿主。</param>
        protected override void Enter(IEnemyAiAgent agent)
        {
            base.Enter(agent);
            m_CalmElapsed = 0f;
        }

        /// <summary>
        /// 等待控制与平静计时结束，再按目标是否存在恢复 AI。
        /// </summary>
        /// <param name="fsm">当前宿主拥有的状态机。</param>
        /// <param name="agent">当前状态借用的敌人AI宿主。</param>
        /// <param name="elapseSeconds">逻辑流逝时间，单位为秒。</param>
        protected override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
        {
            if (agent.IsCcLocked)
            {
                m_CalmElapsed = 0f;
                return;
            }

            m_CalmElapsed += elapseSeconds;
            if (m_CalmElapsed >= agent.Config.BehitCalmTime)
            {
                if (!agent.TargetBox.HasArea())
                {
                    ChangeState(fsm, agent.AiPreset.IdleStateType);
                }
                else
                {
                    ChangeState(fsm, agent.AiPreset.EngageStateType);
                }
            }
        }
    }
}
