using System;
using System.Collections.Generic;
using GameConfig.Skills;
using GameLogic.Architecture;

namespace GameLogic.Skills;

/// <summary>
/// 档案作用域的技能书；只保存各英雄的学习记录，本局施放状态归 HeroModel。
/// </summary>
public sealed class SkillModel : AbstractModel
{
    // ---- 字段 ----

    private readonly Dictionary<int, BindableList<SkillId>> _learnedSkills; // 已拥有英雄的学习记录，不含动作计时或配置副本。
    private readonly Dictionary<int, BindableDictionary<PassiveId, int>> _passiveLevels; // 各英雄的被动等级，0表示未学习。

    // ---- 构造 ----

    /// <summary>
    /// 为已验证的档案英雄建立空技能书，读档随后由 ProfileSystem 原地恢复。
    /// </summary>
    /// <param name="heroIds">本档案已拥有的英雄编号。</param>
    public SkillModel(IEnumerable<int> heroIds)
    {
        ArgumentNullException.ThrowIfNull(heroIds);
        _learnedSkills = new Dictionary<int, BindableList<SkillId>>();
        _passiveLevels = new Dictionary<int, BindableDictionary<PassiveId, int>>();
        foreach (int heroId in heroIds)
        {
            _learnedSkills.Add(heroId, new BindableList<SkillId>());

            // 每名英雄拥有独立的零级被动记录，不共享可变字典。
            var passiveLevels = new Dictionary<PassiveId, int>();
            foreach (PassiveId passiveId in Enum.GetValues<PassiveId>())
            {
                passiveLevels.Add(passiveId, 0);
            }
            _passiveLevels.Add(heroId, new BindableDictionary<PassiveId, int>(passiveLevels));
        }
    }

    // ---- 业务方法 ----

    /// <summary>
    /// 借用指定英雄的唯一学习记录，正常集合写入自动通知。
    /// </summary>
    /// <param name="heroId">本档案已经拥有的英雄编号。</param>
    /// <returns>该英雄的可绑定技能书。</returns>
    public BindableList<SkillId> GetLearnedSkills(int heroId)
    {
        return _learnedSkills[heroId];
    }

    /// <summary>
    /// 借用指定英雄唯一的被动等级字典，升级与回滚直接修改此容器。
    /// </summary>
    /// <param name="heroId">本档案已拥有的英雄。</param>
    /// <returns>被动编号到已学习等级的绑定。</returns>
    public BindableDictionary<PassiveId, int> GetPassiveLevels(int heroId)
    {
        return _passiveLevels[heroId];
    }
}
