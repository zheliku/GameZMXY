using GameLogic.Architecture;
using System;
using GameFramework.Fsm;

namespace GameLogic.Enemies.AI
{
    /// <summary>
    /// 水平走向目标（旧 have_target）；水平到位后，可命中则站定出招，高度不可达则直接踱步。
    /// </summary>
    public class WalkToTargetState : AbstractEngageState
    {
        /// <summary>进入追击当帧就给出方向，避免沿用上个状态的停步意图。</summary>
        /// <param name="agent">提供当前目标与移动意图的怪物。</param>
        protected override void Enter(IEnemyAiAgent agent)
        {
            base.Enter(agent);
            if (agent.TargetBox.HasArea())
                agent.Move(Math.Sign(agent.TargetBox.GetCenter().X));
        }

        /// <summary>未进入普通攻击水平范围时追向目标。</summary>
        protected override void Engage(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, int dir, float elapseSeconds)
        {
            if (agent.GetSystem<EnemyAiSystem>().GetBasicAttackHorizontalGap(agent.Params, agent.TargetBox, dir) <= -EnemyAiSystem.ReachMargin)
            {
                // 不经过会停步的中间状态，避免平台目标导致一帧举棒再走动。
                if (agent.GetSystem<EnemyAiSystem>().IsBasicAttackInReach(agent.Params, agent.TargetBox, dir))
                {
                    ChangeState<StandAndStrikeState>(fsm);
                }
                else
                {
                    ChangeState<PaceBelowTargetState>(fsm);
                }
            }
            else
            {
                agent.Move(dir);
            }
        }
    }
}
