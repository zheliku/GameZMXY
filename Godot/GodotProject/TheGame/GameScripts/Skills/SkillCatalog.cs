using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Skills;

namespace GameLogic.Skills;

/// <summary>造一技能的只读目录；负责面板排序与定义查找，不持有学习记录或执行购买。</summary>
public sealed class SkillCatalog
{
    private readonly TbSkillConfig m_Active; // 正式主动技能表。
    private readonly TbPassiveConfig m_Passive; // 正式被动技能等级表。

    /// <summary>借用已经过启动校验的配置。</summary>
    /// <param name="active">主动技能表。</param>
    /// <param name="passive">被动技能等级表。</param>
    /// <exception cref="ArgumentNullException">任一表为空。</exception>
    public SkillCatalog(TbSkillConfig active, TbPassiveConfig passive)
    {
        m_Active = active ?? throw new ArgumentNullException(nameof(active));
        m_Passive = passive ?? throw new ArgumentNullException(nameof(passive));
    }

    /// <summary>按官方面板行序读取一名角色的主动技能。</summary>
    /// <param name="hero">查看的角色；不表示当前可以出战。</param>
    /// <returns>排好序的只读定义列表。</returns>
    public IReadOnlyList<SkillConfig> GetActive(SkillHero hero) =>
        m_Active.DataList.Where(x => x.Hero == hero).OrderBy(x => x.Order).ToArray();

    /// <summary>读取五项被动的指定等级，面板初期以第一级作为下一级效果。</summary>
    /// <param name="level">配置等级，从 1 开始。</param>
    /// <returns>按官方被动顺序排列的定义。</returns>
    public IReadOnlyList<PassiveConfig> GetPassives(int level) =>
        m_Passive.DataList.Where(x => x.Level == level).OrderBy(x => x.PassiveId).ToArray();

    /// <summary>集中检查面板身份、行序、费用、资源路径和被动等级覆盖。</summary>
    /// <param name="active">主动技能表。</param>
    /// <param name="passive">被动技能等级表。</param>
    /// <param name="errors">启动校验汇总错误。</param>
    public static void Validate(TbSkillConfig active, TbPassiveConfig passive, ICollection<string> errors)
    {
        foreach (SkillHero hero in Enum.GetValues<SkillHero>())
        {
            var skills = active.DataList.Where(x => x.Hero == hero).OrderBy(x => x.Order).ToArray();
            if (skills.Length != 5 || !skills.Select(x => x.Order).SequenceEqual(Enumerable.Range(1, 5)))
                errors.Add($"技能 {hero}：造一面板必须有连续的五行主动技能。");
        }
        foreach (var skill in active.DataList)
        {
            if (!Enum.IsDefined(skill.Hero) || skill.LearnSoul < 0 || skill.ManaCost < 0 ||
                string.IsNullOrWhiteSpace(skill.NameCn) || string.IsNullOrWhiteSpace(skill.Description) ||
                string.IsNullOrWhiteSpace(skill.LegacyId) || string.IsNullOrWhiteSpace(skill.KeyHint) ||
                !skill.UnlearnedIconPath.StartsWith("res://", StringComparison.Ordinal) ||
                !skill.LearnedIconPath.StartsWith("res://", StringComparison.Ordinal))
                errors.Add($"技能 {skill.Id}：身份、费用、文字或图标路径无效。");
        }
        foreach (PassiveId id in Enum.GetValues<PassiveId>())
        {
            var levels = passive.DataList.Where(x => x.PassiveId == id).OrderBy(x => x.Level).ToArray();
            if (levels.Length != 5 || !levels.Select(x => x.Level).SequenceEqual(Enumerable.Range(1, 5)))
                errors.Add($"被动 {id}：必须完整覆盖 1..5 级。");
            if (levels.Select(x => (x.NameCn, x.Effect)).Distinct().Count() != 1)
                errors.Add($"被动 {id}：各级名称和效果类型必须一致。");
        }
        foreach (var level in passive.DataList)
        {
            if (!Enum.IsDefined(level.PassiveId) || !Enum.IsDefined(level.Effect) || level.LearnSoul < 0 ||
                !float.IsFinite(level.Value) || level.Value < 0 ||
                (level.Effect == PassiveEffect.CriticalChance && level.Value > 1) ||
                string.IsNullOrWhiteSpace(level.NameCn) || string.IsNullOrWhiteSpace(level.Description))
                errors.Add($"被动 {level.PassiveId}/{level.Level}：费用、效果或文字无效。");
        }
    }
}
