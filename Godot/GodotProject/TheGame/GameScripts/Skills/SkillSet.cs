using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Skills;
using GameLogic.Architecture;

namespace GameLogic.Skills;

/// <summary>角色当前可用的不可变技能组；武器变化时由具体角色选择另一组，系统不识别武器种类。</summary>
public sealed class SkillSet
{
    private readonly Dictionary<SkillId, SkillDefinition> m_Definitions; // 复制登记项，调用者不能修改已选技能组。

    /// <summary>所属英雄编号。</summary>
    public int HeroId { get; }
    /// <summary>组内技能定义，只借用不可变登记项。</summary>
    public IEnumerable<SkillDefinition> Definitions => m_Definitions.Values;

    /// <summary>创建角色或武器的一套技能。</summary>
    /// <param name="heroId">所属英雄。</param>
    /// <param name="definitions">技能工厂与可选直接输入槽；不能重复编号或槽位。</param>
    /// <exception cref="ArgumentException">编号或输入槽重复、工厂为空，或槽位超出 1..3。</exception>
    public SkillSet(int heroId, params SkillDefinition[] definitions)
    {
        HeroId = heroId;
        m_Definitions = definitions.ToDictionary(item => item.Id);
        if (definitions.Where(item => item.Slot.HasValue).GroupBy(item => item.Slot).Any(group => group.Count() > 1) ||
            definitions.Any(item => item.Create == null || item.Slot is < 1 or > 3))
            throw new ArgumentException("技能工厂不能为空，输入槽必须唯一且为 1..3。", nameof(definitions));
    }

    /// <summary>读取技能槽绑定，组合键仍由具体角色裁定。</summary>
    /// <param name="slot">从 1 开始的直接技能槽。</param>
    /// <returns>绑定的技能，没有则为空。</returns>
    public SkillId? GetSkill(int slot) => m_Definitions.Values.FirstOrDefault(item => item.Slot == slot)?.Id;

    /// <summary>读取本组技能定义。</summary>
    /// <param name="id">技能编号。</param>
    /// <returns>定义，不在本组则为空。</returns>
    internal SkillDefinition GetDefinition(SkillId id) => m_Definitions.GetValueOrDefault(id);
}

/// <summary>技能行为登记；费用、名称和归属仍从配置表读取，不在代码重复数值。</summary>
/// <param name="Id">技能编号。</param>
/// <param name="Create">每次开始创建独立执行，不能返回共享可变实例。</param>
/// <param name="Slot">直接输入槽；组合技能可以不绑定。</param>
/// <param name="BodyState">占用身体时进入的 GGF 状态类型；独立效果可以不指定。</param>
public sealed record SkillDefinition(SkillId Id, Func<IArchitecture, SkillExecution> Create, int? Slot = null,
    Type BodyState = null);
