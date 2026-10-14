using GameLogic.Heroes.Input;

namespace GameLogic.Heroes.Body;

/// <summary>英雄拥有的普攻序列：保存跨身体状态的连段和冷却，不切状态或播放动画。</summary>
public sealed class HeroAttackSequence
{
    private const double TimeEpsilon = 1e-4; // 物理时钟浮点误差容差，单位秒。
    private readonly HeroBodyParams m_Parameters; // 初始化时已校验的攻击参数。
    private int m_LastGroundSegment = -1; // 上一次地面普攻段号，空中普攻后归零序列。
    private double m_LastAttempt = double.NegativeInfinity; // 上次尝试时刻，冷却拒绝也更新。
    private double m_ReadyAt; // 下一次允许起手的游戏时刻，单位秒。

    /// <summary>使用英雄配置及动画参数创建普攻序列。</summary>
    /// <param name="parameters">初始化时已校验的身体参数快照。</param>
    public HeroAttackSequence(HeroBodyParams parameters) => m_Parameters = parameters;

    /// <summary>最近成功选定的攻击段下标；初始为 -1，身体状态用于装填攻击和播放动画。</summary>
    public int Segment { get; private set; } = -1;

    /// <summary>消费一次普攻尝试；冷却允许时选定下一段，拒绝的输入不留到后续帧。</summary>
    /// <param name="input">本英雄的输入组合和游戏时钟。</param>
    /// <param name="airborne">本次是否从空中出招。</param>
    /// <param name="isAttacking">当前是否已经处于攻击动作。</param>
    /// <returns>已选定本次攻击段时返回 true。</returns>
    public bool TryAdvance(HeroInput input, bool airborne, bool isAttacking)
    {
        if (m_Parameters.ComboLength == 0 || !input.ConsumeAttack())
        {
            return false;
        }

        double now = input.Time;
        bool reset = now - m_LastAttempt > m_Parameters.ComboResetTime + TimeEpsilon;
        m_LastAttempt = now;
        if (now + TimeEpsilon < m_ReadyAt || (!airborne && input.Running && isAttacking))
        {
            return false;
        }

        // 原版先按上一段设置本次起手后的冷却，再推进段号；与动画收招时间独立。
        float cooldown;
        if (airborne)
        {
            cooldown = m_Parameters.AirAttackCooldown;
            m_LastGroundSegment = -1;
            Segment = m_Parameters.AirAttackIndex;
        }
        else
        {
            cooldown = m_Parameters.AttackInputCooldowns[System.Math.Max(m_LastGroundSegment, 0)];
            m_LastGroundSegment = reset ? 0 : (m_LastGroundSegment + 1) % m_Parameters.ComboLength;
            Segment = m_LastGroundSegment;
            input.StopRunning();
        }

        m_ReadyAt = now + cooldown;
        return true;
    }

    /// <summary>实体重新显示时清空连段和冷却；受击、跳跃与收招不调用。</summary>
    public void Reset()
    {
        Segment = m_LastGroundSegment = -1;
        m_LastAttempt = double.NegativeInfinity;
        m_ReadyAt = 0;
    }
}
