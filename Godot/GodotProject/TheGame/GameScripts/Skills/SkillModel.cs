using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Skills;
using GameLogic.Architecture;

namespace GameLogic.Skills;

/// <summary>档案作用域的技能书；只保存各英雄的学习记录，本局施放状态归 HeroModel。</summary>
public sealed class SkillModel : AbstractModel
{
    // ---- 字段 ----

    private readonly Dictionary<int, BindableList<SkillId>> m_LearnedSkills; // 已拥有英雄的学习记录，不含动作计时或配置副本。

    // ---- 构造 ----

    /// <summary>为已验证的档案英雄建立空技能书，读档随后由 ProfileSystem 原地恢复。</summary>
    /// <param name="heroIds">本档案已拥有的英雄编号。</param>
    public SkillModel(IEnumerable<int> heroIds)
    {
        ArgumentNullException.ThrowIfNull(heroIds);
        m_LearnedSkills = heroIds.ToDictionary(id => id, _ => new BindableList<SkillId>());
    }

    // ---- 业务方法 ----

    /// <summary>借用指定英雄的唯一学习记录，正常集合写入自动通知。</summary>
    /// <param name="heroId">本档案已经拥有的英雄编号。</param>
    /// <returns>该英雄的可绑定技能书。</returns>
    public BindableList<SkillId> GetLearnedSkills(int heroId) => m_LearnedSkills[heroId];
}
