using GameLogic.Architecture;
using System;
using GameFramework.Fsm;

namespace GameLogic.Enemies.AI
{
    /// <summary>
    /// 在目标下方守候：攻击重新够得着（目标落回判定高度）→ 站定出招，水平走远 → 继续追击；
    /// 否则以目标 x 为中心、<see cref="GameConfig.Enemy.EnemyConfig.PaceRange"/> 为半幅左右往返（到一端掉头；半幅 0 = 原地面向目标等）。
    /// 半幅取 EnemyConfig.PaceRange。
    /// </summary>
    public class PaceBelowTargetState : AbstractEngageState
    {
        private int m_Dir; // 当前平台下踱步方向；0 表示尚未选向。

        /// <summary>
        /// 初始化目标下方的踱步方向，并立即提交移动意图。
        /// </summary>
        /// <param name="agent">当前状态借用的敌人AI宿主。</param>
        protected override void Enter(IEnemyAiAgent agent)
        {
            base.Enter(agent);
            // 踱步与追击一样在进入时提交方向，站定等待只用于半幅为零的配置。
            m_Dir = agent.Config.PaceRange > 0f ? Math.Sign(agent.TargetBox.GetCenter().X) : 0;
            if (m_Dir == 0 && agent.Config.PaceRange > 0f)
            {
                m_Dir = 1;
            }

            agent.Move(m_Dir);
        }

        /// <summary>
        /// 目标高度暂不可达时在其下方踱步或转入追击。
        /// </summary>
        /// <param name="fsm">当前宿主拥有的状态机。</param>
        /// <param name="agent">当前状态借用的敌人AI宿主。</param>
        /// <param name="dir">方向符号：-1向左、0保持或停止、1向右。</param>
        /// <param name="elapseSeconds">逻辑流逝时间，单位为秒。</param>
        protected override void Engage(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, int dir, float elapseSeconds)
        {
            if (agent.GetSystem<EnemyAiSystem>().IsBasicAttackInReach(agent.Params, agent.TargetBox, dir))
            {
                ChangeState<StandAndStrikeState>(fsm);
                return;
            }

            // 目标在平台上走远（普攻水平间隙超出踱步半幅 + 滞回）：交给接近
            float range = agent.Config.PaceRange;
            if (agent.GetSystem<EnemyAiSystem>().GetBasicAttackHorizontalGap(agent.Params, agent.TargetBox, dir) > range + agent.Config.AttackRangeSlack)
            {
                ChangeState<WalkToTargetState>(fsm);
                return;
            }

            if (range <= 0f)
            {
                agent.Move(0);
                agent.Face(dir);
                return;
            }

            // 走到半幅外（且还在往外走）就掉头
            float fromTarget = -agent.TargetBox.GetCenter().X;   // 自己相对目标的水平偏移
            if (m_Dir == 0 || (Math.Abs(fromTarget) >= range && Math.Sign(fromTarget) == m_Dir))
            {
                m_Dir = fromTarget > 0f ? -1 : 1;
            }

            agent.Move(m_Dir);
        }
    }
}
