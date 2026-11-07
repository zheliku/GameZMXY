using System;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies;

/// <summary>
/// 彭魔王按地面/空中、原点距离和原版一秒节拍选择近身刺击、生火或火鸟冲刺。
/// </summary>
/// <param name="trackingHeight">允许追踪的高差，像素。</param>
/// <param name="meleeDistance">近身刺击半径，像素。</param>
/// <param name="flying">读取当前地面/飞行阶段；不复制运行状态。</param>
/// <param name="onFloor">读取本次物理移动后的真实落地事实。</param>
internal sealed class PengDemonKingEngageState(float trackingHeight, float meleeDistance,
    Func<bool> flying, Func<bool> onFloor) : AbstractEnemyAiState
{
    private long _turnSlot = -1; // 两秒决策格只消费一次，避免在整个偶数秒内每帧折返。

    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float delta)
    {
        if (agent.IsAttacking)
        {
            return;
        }
        if (!agent.HasTarget || Math.Abs(agent.TargetOffset.Y) > trackingHeight)
        {
            agent.GetSystem<EnemyAiSystem>().Patrol(agent, delta);
            return;
        }
        Engage(agent, agent.GetSystem<EnemyAiSystem>().IsDecisionDue(agent.AiData, 1));
    }

    /// <summary>
    /// 按圆形距离和飞行状态执行本首领选招，追踪高度由独立原生范围拥有。
    /// </summary>
    /// <param name="agent">当前首领实例。</param>
    /// <param name="decision">是否跨过一秒选招节拍。</param>
    private void Engage(IEnemyAiAgent agent, bool decision)
    {
        bool airborne = flying();
        float distance = agent.TargetOffset.Length();
        if (!airborne && !onFloor())
        {
            return;
        }

        if (!agent.IsInEngageRange)
        {
            if (!airborne)
            {
                agent.Move(Math.Sign(agent.TargetOffset.X));
            }
            else if (decision && agent.NextRandom() < .5f)
            {
                agent.RequestAttack(0);
            }

            return;
        }
        if (!decision)
        {
            // 飞行时只每两秒修正一次朝向，仍持续朝150px高度运动。
            if (!airborne)
            {
                agent.Move(Math.Sign(agent.TargetOffset.X));
            }
            else
            {
                long slot = (long)(agent.AiData.ElapsedSeconds / 2);
                if (slot != _turnSlot)
                {
                    _turnSlot = slot;
                    if (Math.Abs(agent.TargetOffset.X) > meleeDistance)
                    {
                        agent.Move(Math.Sign(agent.TargetOffset.X));
                    }
                }
            }
            return;
        }
        agent.Move(0);
        if (distance <= meleeDistance)
        {
            if (!airborne)
            {
                agent.RequestAttack(3);
            }

            return;
        }
        if (agent.NextRandom() < .3f)
        {
            if (agent.AiData.Cooldowns[1] <= 0f)
            {
                agent.RequestAttack(1);
            }
        }
        else
        {
            int fire = airborne ? 0 : 2;
            if (agent.AiData.Cooldowns[fire] <= 0f)
            {
                agent.RequestAttack(fire);
            }
        }
    }
}
