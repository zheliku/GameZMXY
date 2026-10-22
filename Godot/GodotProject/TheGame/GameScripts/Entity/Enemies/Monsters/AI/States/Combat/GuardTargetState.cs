using GameLogic.Architecture;
using GameLogic.Utility;
using GameFramework.Fsm;

namespace GameLogic.Enemies.AI;

/// <summary>原地守卫：不追击或踱步，按配置节奏攻击进入范围的目标。</summary>
public sealed class GuardTargetState : AbstractEngageState
{
    private float m_RollTimer; // 距下次普通攻击意愿判定的游戏秒数。

    /// <inheritdoc />
    protected override void Enter(IEnemyAiAgent agent)
    {
        base.Enter(agent);
        agent.Move(0);
        m_RollTimer = (agent.Config.AttackFirstDelay.X, agent.Config.AttackFirstDelay.Y).Sample(agent.NextRandom());
    }

    /// <inheritdoc />
    protected override void Engage(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, int dir, float elapseSeconds)
    {
        agent.Move(0);
        agent.Face(dir);
        m_RollTimer -= elapseSeconds;
        if (m_RollTimer > 0f || !agent.GetSystem<EnemyAiSystem>().IsBasicAttackInReach(agent.Params, agent.TargetBox, dir))
            return;
        m_RollTimer = agent.Config.AttackInterval;
        if (Chance(agent, agent.Config.AttackDesire))
            agent.RequestAttack(agent.GetSystem<EnemyAiSystem>().SelectBasicAttack(agent.AiData, agent.Params, agent.TargetBox, dir, agent.NextRandom));
    }
}
