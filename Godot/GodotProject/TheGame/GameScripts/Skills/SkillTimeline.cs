using System;
using System.Collections.Generic;
using GameConfig.Skills;

namespace GameLogic.Skills;

/// <summary>秒制事件游标，只处理时间、冻结与跳转；不解释移动、攻击或特效。</summary>
/// <param name="events">已按局部时间排序的只读事件。</param>
public sealed class SkillTimeline(IReadOnlyList<SkillTimelineConfig> events)
{
    private int m_NextEvent; // 下一条尚未交给状态处理的事件。
    private double m_FreezeRemaining; // 剩余冻结秒数。

    /// <summary>当前动画局部秒数。</summary>
    public double Elapsed { get; private set; }
    /// <summary>具体状态等待外部效果时暂停游标。</summary>
    public bool Paused { get; set; }

    /// <summary>产出本帧到期事件；处理者可在事件之间暂停、冻结或跳转。</summary>
    /// <param name="dt">本次游戏秒数。</param>
    /// <param name="onFloor">本帧落地事实，用于有截止时间的落地事件。</param>
    /// <returns>到期事件，由招式状态解释。</returns>
    public IEnumerable<SkillTimelineConfig> Advance(float dt, bool onFloor)
    {
        double remaining = dt;
        while (m_NextEvent < events.Count && !Paused)
        {
            var entry = events[m_NextEvent];
            // 同时点先全部产出，再消耗冻结时间；低帧率也保留事件顺序。
            if (entry.AtSeconds > Elapsed + .0001)
            {
                if (m_FreezeRemaining > 0)
                {
                    double frozen = Math.Min(remaining, m_FreezeRemaining);
                    m_FreezeRemaining -= frozen;
                    remaining -= frozen;
                }
                if (remaining <= 0)
                    yield break;
                double step = Math.Min(remaining, entry.AtSeconds - Elapsed);
                Elapsed += step;
                remaining -= step;
                if (entry.AtSeconds > Elapsed + .0001)
                    yield break;
            }
            if (entry.Condition == SkillTimelineCondition.OnFloor && !onFloor && Elapsed <= entry.UntilSeconds + .0001)
            {
                Elapsed += remaining;
                remaining = 0;
                if (Elapsed <= entry.UntilSeconds + .0001)
                    yield break;
            }
            m_NextEvent++;
            yield return entry;
        }
    }

    /// <summary>冻结后续时间，同时点其他事件仍会产出。</summary>
    /// <param name="seconds">冻结的游戏秒数。</param>
    public void Freeze(float seconds) => m_FreezeRemaining = seconds;

    /// <summary>跳过目标时点之前的事件，保留目标时点自身的事件。</summary>
    /// <param name="seconds">目标局部秒数。</param>
    public void Seek(float seconds)
    {
        Elapsed = seconds;
        while (m_NextEvent < events.Count && events[m_NextEvent].AtSeconds < seconds - .0001)
            m_NextEvent++;
    }
}
