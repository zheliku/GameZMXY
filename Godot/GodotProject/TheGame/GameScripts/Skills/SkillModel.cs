using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Skills;
using GameLogic.Architecture;

namespace GameLogic.Skills;

/// <summary>档案作用域的技能数据所有者：持有各英雄唯一的技能书、当前技能组与本场活动执行。</summary>
public sealed class SkillModel : AbstractModel
{
    // ---- 字段 ----

    private readonly Dictionary<int, BindableList<SkillId>> m_LearnedSkills; // 已拥有英雄的学习记录，不含动作计时或配置副本。

    // ---- 属性 ----

    /// <summary>当前占用身体的执行；持续效果释放身体后仍留在活动集合中。</summary>
    public SkillExecution BodySkill { get; internal set; }

    /// <summary>具体角色选择的技能组；只是不可变定义的引用。</summary>
    public SkillSet SelectedSet { get; internal set; }

    /// <summary>本英雄唯一的活动执行集合，不保存场景节点或存档数据。</summary>
    internal Dictionary<SkillId, SkillExecution> ActiveExecutions { get; } = new();

    /// <summary>技能无敌开始的游戏时刻，秒。</summary>
    internal double InvulnerableFrom { get; set; } = double.PositiveInfinity;

    /// <summary>技能无敌结束的游戏时刻，秒；独立于角色动画跳帧。</summary>
    internal double InvulnerableUntil { get; set; }

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
