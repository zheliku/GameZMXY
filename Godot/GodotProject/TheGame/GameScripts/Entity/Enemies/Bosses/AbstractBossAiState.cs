using System;
using GameFramework.Fsm;

namespace GameLogic.Enemies.AI;

/// <summary>造一两位 BOSS 共用的行为时钟与原版游荡；具体距离、追击和选招由各自状态决定。</summary>
public abstract class AbstractBossAiState : AbstractEnemyAiState
{
    private const double ClockPeriod = 242d / 24d; // BaseMonster.count 自 0 至 241，再归零；换算成游戏秒。
    private const double DirectionTime = .5; // 原版 count == 12 时选择游荡方向。
    private const double ResetSpread = 1.25; // 原版随机延迟 0..30 帧，对应 0..1.25 秒。
    private double m_ClockOrigin; // normalWalk 提前归零时的本实例游戏时钟。
    private double m_StopTime = 3; // 原版默认 72 帧；选方向时在 3、4 秒间随机。

    /// <summary>目标原点允许的纵向距离，具体 BOSS 使用原版范围。</summary>
    protected abstract float HeightRange { get; }

    /// <summary>有目标时不套用小怪概率选招；无目标或高度超限时按官方游荡。</summary>
    /// <param name="fsm">本实例 AI 状态机。</param>
    /// <param name="agent">感知与意图宿主。</param>
    /// <param name="elapseSeconds">本次游戏秒数。</param>
    protected sealed override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
    {
        double age = (agent.AiData.ElapsedSeconds - m_ClockOrigin) % ClockPeriod;
        double previous = Math.Max(0, agent.AiData.ElapsedSeconds - elapseSeconds - m_ClockOrigin) % ClockPeriod;
        bool decision = age < previous || (int)(age + 1e-6) != (int)(previous + 1e-6);
        if (agent.IsAttacking)
            return;
        if (!agent.TargetBox.HasArea() || Math.Abs(agent.TargetOffset.Y) > HeightRange)
        {
            Patrol(agent, age, previous, elapseSeconds);
            return;
        }
        Engage(agent, decision);
    }

    /// <summary>具体 BOSS 按原点距离与自身招式冷却选择追击或出招。</summary>
    /// <param name="agent">当前怪物。</param>
    /// <param name="decision">本帧是否跨过一秒近身选招节拍。</param>
    protected abstract void Engage(IEnemyAiAgent agent, bool decision);

    /// <summary>保留官方半秒起步、3/4 秒停步及相机两侧 400 像素折返规则。</summary>
    /// <param name="agent">本实例感知及随机源。</param>
    /// <param name="age">本轮行为秒数。</param>
    /// <param name="previous">上一步行为秒数。</param>
    /// <param name="dt">本次游戏秒数。</param>
    private void Patrol(IEnemyAiAgent agent, double age, double previous, float dt)
    {
        if (previous < DirectionTime && age >= DirectionTime)
        {
            int direction = agent.PatrolOffsetX < -400 ? 1 : agent.PatrolOffsetX > 400 ? -1
                : agent.NextRandom() < .5f ? -1 : 1;
            agent.Move(direction);
            m_StopTime = agent.NextRandom() < .5f ? 3 : 4;
        }
        if (age < m_StopTime)
            return;
        // 将原版每源帧的随机判定换成按秒的概率，避免随实际渲染帧率变快。
        if (agent.NextRandom() < 1 - Math.Pow(.5, dt * 24))
            agent.Move(0);
        double resetChance = Math.Clamp((age - m_StopTime) / ResetSpread, 0, 1);
        if (agent.NextRandom() < 1 - Math.Pow(1 - resetChance, dt * 24))
            m_ClockOrigin = agent.AiData.ElapsedSeconds;
    }
}
