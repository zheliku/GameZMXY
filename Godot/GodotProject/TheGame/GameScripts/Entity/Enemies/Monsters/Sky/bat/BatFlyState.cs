using System;
using GameFramework.Fsm;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies;

/// <summary>
/// 蝙蝠原版飞行决策：延迟折返、追踪英雄上方、近身尝试攻击及攻击后升空。
/// </summary>
internal sealed class BatFlyState : AbstractMonsterAiState
{
    private float m_TurnLeft; // 距离下一次允许折返的游戏秒数。
    private float m_TurnInterval; // 本次显示的原版随机折返间隔，1～3 秒。
    private float m_AttackLeft; // 近身攻击尝试的剩余秒数。
    private float m_RetreatLeft; // 攻击后的原版三秒回避窗口。
    private float m_PatrolLeft; // 无目标时下一次巡逻决策剩余秒数。
    private int m_RetreatDirection; // 出招面向，攻击结束后沿该方向升空。

    /// <inheritdoc />
    protected override void Enter(IEnemyAiAgent agent)
    {
        base.Enter(agent);
        ((AbstractSkyMonsterEntity)agent).VerticalIntent = 0;
        ((AbstractSkyMonsterEntity)agent).DirectVerticalMovement = false;
        m_TurnInterval = 1f + 2f * agent.NextRandom();
        m_TurnLeft = m_AttackLeft = m_RetreatLeft = m_PatrolLeft = 0f;
    }

    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
    {
        var flight = (AbstractSkyMonsterEntity)agent;
        m_TurnLeft -= elapseSeconds;
        m_AttackLeft -= elapseSeconds;
        m_RetreatLeft -= elapseSeconds;
        m_PatrolLeft -= elapseSeconds;
        flight.DirectVerticalMovement = m_RetreatLeft > 0f;
        if (agent.IsAttacking)
            return;
        if (m_RetreatLeft > 0f)
        {
            agent.Move(m_RetreatDirection);
            flight.VerticalIntent = flight.Altitude > 150f ? -1 : 0;
            return;
        }
        if (!agent.TargetBox.HasArea())
        {
            flight.VerticalIntent = 0;
            if (m_PatrolLeft <= 0f)
            {
                agent.Move(Math.Abs(agent.PatrolOffsetX) > 400f ? -Math.Sign(agent.PatrolOffsetX) :
                    (agent.NextRandom() < 0.5f ? -1 : 1));
                m_PatrolLeft = agent.NextRandom() < 0.5f ? 3f : 4f;
            }
            return;
        }
        var offset = agent.TargetOffset;
        if (Math.Abs(offset.X) > 140f || Math.Abs(offset.Y) > 90f)
        {
            if (m_TurnLeft <= 0f)
            {
                agent.Move(Math.Sign(offset.X));
                m_TurnLeft = m_TurnInterval;
            }
            flight.VerticalIntent = offset.Y > 90f ? 1 : -1;
            return;
        }
        agent.Move(0);
        flight.VerticalIntent = 0;
        if (m_AttackLeft <= 0f)
        {
            m_AttackLeft = agent.Config.AttackInterval;
            if (Chance(agent, agent.Config.AttackDesire) && agent.RequestAttack(0))
            {
                m_RetreatLeft = 3f;
                m_RetreatDirection = Math.Sign(offset.X) is 0 ? 1 : Math.Sign(offset.X);
            }
        }
    }
}
