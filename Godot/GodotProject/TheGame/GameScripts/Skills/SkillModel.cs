using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Skills;
using GameLogic.Architecture;

namespace GameLogic.Skills;

/// <summary>档案作用域的技能数据所有者：持有各英雄唯一的技能书、当前技能组与当前激活编号。</summary>
public sealed class SkillModel : AbstractModel
{
    // ---- 字段 ----

    private readonly Dictionary<int, BindableList<SkillId>> m_LearnedSkills; // 已拥有英雄的学习记录，不含动作计时或配置副本。

    // ---- 属性 ----

    /// <summary>当前激活的技能编号；招式执行与旧回调的有效性归所属 GGF 状态。</summary>
    public SkillId? ActiveSkillId { get; internal set; }

    /// <summary>本次选定的角色或武器技能登记，由技能系统校验和替换；不保存角色节点。</summary>
    internal Dictionary<SkillId, SkillDefinition> Definitions { get; } = new();

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

/// <summary>模型中的一条技能登记；具体行为由登记的 GGF 状态实现，不再创建第二套技能对象。</summary>
/// <param name="Id">技能编号。</param>
/// <param name="BodyState">本技能的 GGF 身体状态。</param>
/// <param name="InputAction">可选直接动作名；组合技能由具体角色匹配。</param>
/// <param name="CanStart">可选起手条件；在扣费前检查。</param>
public sealed record SkillDefinition(SkillId Id, Type BodyState,
    string InputAction = null, Func<ISkillActor, bool> CanStart = null);
