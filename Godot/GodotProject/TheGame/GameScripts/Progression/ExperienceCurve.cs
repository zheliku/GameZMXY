using System;
using System.Collections.Generic;
using GameConfig.Hero;

namespace GameLogic.Progression;

/// <summary>验证等级表并将累计经验映射到等级与本级进度；不持有运行时玩家状态。</summary>
public sealed class ExperienceCurve
{
    private readonly int[] m_Thresholds; // 每个等级的累计经验起点，索引为等级减一。

    /// <summary>表中最高等级。</summary>
    public int MaxLevel => m_Thresholds.Length;

    /// <summary>达到最高等级的累计经验上限。</summary>
    public int MaxTotalExperience => m_Thresholds[^1];

    /// <summary>按连续等级验证表，并预计算累计经验门槛。</summary>
    /// <param name="levels">Luban 等级配置，可按任意顺序提供。</param>
    /// <exception cref="ArgumentNullException">配置列表为空引用。</exception>
    /// <exception cref="ArgumentException">等级缺失、重复、阈值非法或累计经验超出整数范围。</exception>
    public ExperienceCurve(IReadOnlyList<HeroLevelConfig> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        if (levels.Count == 0)
        {
            throw new ArgumentException("等级表不能为空。", nameof(levels));
        }

        // 初始化边界只接受从 1 连续编号，末级零阈值是唯一终止标记。
        int[] requirements = new int[levels.Count];
        bool[] found = new bool[levels.Count];
        foreach (HeroLevelConfig level in levels)
        {
            if (level == null || level.Id < 1 || level.Id > levels.Count || found[level.Id - 1] ||
                (level.Id == levels.Count ? level.MaxExp != 0 : level.MaxExp <= 0))
            {
                throw new ArgumentException("等级必须从 1 连续且唯一，非末级经验为正，末级经验为零。", nameof(levels));
            }
            found[level.Id - 1] = true;
            requirements[level.Id - 1] = level.MaxExp;
        }

        m_Thresholds = new int[levels.Count];
        long total = 0;
        for (int index = 1; index < levels.Count; index++)
        {
            total += requirements[index - 1];
            if (total > int.MaxValue)
            {
                throw new ArgumentException("等级累计经验超出整数范围。", nameof(levels));
            }
            m_Thresholds[index] = (int)total;
        }
    }

    /// <summary>兼容历史独立等级字段，将累计经验限制到可用范围并保留已取得的等级。</summary>
    /// <param name="totalExperience">存档中的累计经验。</param>
    /// <param name="savedLevel">存档中的等级；超出表范围时限制到首末等级。</param>
    /// <returns>归一化后的累计经验。</returns>
    public int Restore(int totalExperience, int savedLevel)
    {
        int level = Math.Clamp(savedLevel, 1, MaxLevel);
        return Math.Clamp(totalExperience, m_Thresholds[level - 1], MaxTotalExperience);
    }

    /// <summary>累计奖励并限制满级上限，使用宽整数避免奖励加法溢出。</summary>
    /// <param name="totalExperience">当前累计经验。</param>
    /// <param name="amount">非负奖励经验。</param>
    /// <returns>奖励后的累计经验。</returns>
    /// <exception cref="ArgumentOutOfRangeException">奖励为负。</exception>
    public int Add(int totalExperience, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        return (int)Math.Min((long)Math.Clamp(totalExperience, 0, MaxTotalExperience) + amount, MaxTotalExperience);
    }

    /// <summary>由累计经验解析最终等级及本级进度，精确门槛立即升级。</summary>
    /// <param name="totalExperience">累计经验，消费前限制到首末门槛。</param>
    /// <returns>最终等级、本级经验和升下一级所需经验；满级进度为零。</returns>
    public (int Level, int Experience, int MaxExperience) Evaluate(int totalExperience)
    {
        int total = Math.Clamp(totalExperience, 0, MaxTotalExperience);
        int index = Array.BinarySearch(m_Thresholds, total);
        if (index < 0)
        {
            index = ~index - 1;
        }
        return index == MaxLevel - 1
            ? (MaxLevel, 0, 0)
            : (index + 1, total - m_Thresholds[index], m_Thresholds[index + 1] - m_Thresholds[index]);
    }
}
