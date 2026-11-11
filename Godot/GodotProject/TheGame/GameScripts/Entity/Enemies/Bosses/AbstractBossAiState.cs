using System;
using GameFramework.Fsm;
using GameLogic.Architecture;

namespace GameLogic.Enemies.AI;

/// <summary>
/// 造一 BOSS 共用的行为时钟与原版游荡；具体距离、追击和选招由各自状态决定。
/// </summary>
public abstract class AbstractBossAiState : AbstractEnemyAiState
{
    /// <summary>
    /// 近身选招的游戏秒间隔；默认一秒，特殊节拍由具体首领决定。
    /// </summary>
    /// <param name="agent">本次决策的目标距离与实例状态。</param>
    /// <returns>正数游戏秒数。</returns>
    protected virtual double GetDecisionPeriod(IEnemyAiAgent agent)
    {
        return 1;
    }

    /// <summary>
    /// 有目标时不套用小怪概率选招；无目标或高度超限时按官方游荡。
    /// </summary>
    /// <param name="fsm">本实例 AI 状态机。</param>
    /// <param name="agent">感知与意图宿主。</param>
    /// <param name="elapseSeconds">本次游戏秒数。</param>
    protected sealed override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
    {
        bool decision = agent.GetSystem<EnemyAiSystem>().IsDecisionDue(agent.AiData, GetDecisionPeriod(agent));
        if (agent.IsAttacking)
        {
            return;
        }

        if (!agent.HasTarget || agent.TargetOffset.Y < agent.EngageBounds.Position.Y || agent.TargetOffset.Y > agent.EngageBounds.End.Y)
        {
            Patrol(agent, elapseSeconds);
            return;
        }
        Engage(agent, decision, elapseSeconds);
    }

    /// <summary>
    /// 具体 BOSS 按原点距离与自身招式冷却选择追击或出招。
    /// </summary>
    /// <param name="agent">当前怪物。</param>
    /// <param name="decision">本帧是否跨过当前首领的近身选招节拍。</param>
    /// <param name="delta">本帧游戏秒数，具体首领可继续采用相同的原版游荡。</param>
    protected abstract void Engage(IEnemyAiAgent agent, bool decision, float delta);

    /// <summary>
    /// 保留官方半秒起步、3/4 秒停步及相机两侧 400 像素折返规则。
    /// </summary>
    /// <param name="agent">本实例感知及随机源。</param>
    /// <param name="dt">本次游戏秒数。</param>
    protected void Patrol(IEnemyAiAgent agent, float dt)
    {
        agent.GetSystem<EnemyAiSystem>().Patrol(agent, dt);
    }
}
