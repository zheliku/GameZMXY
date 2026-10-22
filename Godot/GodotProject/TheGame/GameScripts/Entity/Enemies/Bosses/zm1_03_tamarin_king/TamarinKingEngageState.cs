using System;
using GameFramework.Fsm;

namespace GameLogic.Enemies.AI;

/// <summary>禺狨王：追至原点距离 250 像素以内，每秒优先释放落雷，冷却期间杖击。</summary>
public sealed class TamarinKingEngageState : AbstractBossAiState
{
    private const float AttackDistance = 250f; // Monster6.hasAttackTarget 的原版水平阈值。
    /// <summary>原版 attackRange 450 × 2 的纵向范围。</summary>
    protected override float HeightRange => 900f;

    /// <summary>按距离追击或停步选招，不套用小怪的出手概率和攻击盒追击距离。</summary>
    /// <param name="agent">本怪物感知及请求接口。</param>
    /// <param name="decision">是否跨过一秒近身选招时点。</param>
    protected override void Engage(IEnemyAiAgent agent, bool decision)
    {
        var offset = agent.TargetOffset;
        if (Math.Abs(offset.X) > AttackDistance)
        {
            agent.Move(Math.Sign(offset.X));
            return;
        }
        agent.Move(0);
        agent.Face(Math.Sign(offset.X));
        if (decision)
            agent.RequestAttack(agent.AiData.Cooldowns[1] <= 0f ? 1 : 0);
    }
}
