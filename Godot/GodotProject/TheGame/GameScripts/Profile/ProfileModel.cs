using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Equipment;
using GameLogic.Architecture;
using GameLogic.Equipment;

namespace GameLogic.Profile;

/// <summary>当前档案的权威数据；成长、领取、穿戴、出售与回滚由业务系统执行。</summary>
public sealed class ProfileModel : AbstractModel
{
    private readonly Dictionary<int, BindableProperty<HeroProgressionState>> m_Heroes = new(); // 每名英雄唯一的成长状态。
    private readonly Dictionary<int, BindableProperty<EquippedItems>> m_HeroSlots = new(); // 每名英雄唯一的穿戴状态。

    /// <summary>当前出战英雄编号。</summary>
    public int ActiveHeroId { get; }

    /// <summary>本档案已拥有的英雄编号。</summary>
    public IReadOnlyList<int> HeroIds { get; }

    /// <summary>出战英雄的完整成长值。</summary>
    public BindableProperty<HeroProgressionState> Progress => GetProgress(ActiveHeroId);

    /// <summary>出战英雄的等级。</summary>
    public int Level => Progress.Value.Level;

    /// <summary>出战英雄累计经验。</summary>
    public int TotalExperience => Progress.Value.TotalExperience;

    /// <summary>本级经验。</summary>
    public int Experience => Progress.Value.Experience;

    /// <summary>本级升级门槛。</summary>
    public int MaxExperience => Progress.Value.MaxExperience;

    /// <summary>出战英雄的完整穿戴。</summary>
    public BindableProperty<EquippedItems> Slots => GetSlots(ActiveHeroId);

    /// <summary>当前武器或空。</summary>
    public EquipmentData Weapon => Slots.Value.Weapon;

    /// <summary>当前防具或空。</summary>
    public EquipmentData Armor => Slots.Value.Armor;

    /// <summary>灵魂余额。</summary>
    public BindableProperty<int> Soul { get; }

    /// <summary>背包最大装备数量。</summary>
    public int Capacity { get; }

    /// <summary>唯一背包集合；由业务系统执行修改，表现层只读和订阅。</summary>
    public BindableList<EquipmentData> EquipmentDatas { get; } = new();

    /// <summary>从已验证的档案记录建立数据，不缓存共享经验曲线。</summary>
    /// <param name="activeHeroId">已拥有的出战英雄。</param>
    /// <param name="heroes">英雄编号与累计经验。</param>
    /// <param name="curve">仅构造期间使用的共享经验规则。</param>
    /// <param name="equipmentCapacity">正数背包容量。</param>
    /// <param name="soul">初始余额，负数按零处理。</param>
    /// <exception cref="ArgumentException">英雄重复或出战英雄未拥有。</exception>
    /// <exception cref="ArgumentNullException">英雄列表或曲线为空。</exception>
    /// <exception cref="ArgumentOutOfRangeException">容量不是正数。</exception>
    public ProfileModel(int activeHeroId, IEnumerable<(int HeroId, int TotalExperience)> heroes,
        ExperienceCurve curve, int equipmentCapacity, int soul = 0)
    {
        ArgumentNullException.ThrowIfNull(heroes);
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(equipmentCapacity);
        // 只在构造边界建立数据与固定身份，运行期不更换绑定对象。
        foreach (var hero in heroes)
            if (!m_Heroes.TryAdd(hero.HeroId, new(curve.GetProgress(hero.TotalExperience))))
                throw new ArgumentException($"英雄 {hero.HeroId} 重复。", nameof(heroes));
        if (!m_Heroes.ContainsKey(activeHeroId))
            throw new ArgumentException("出战英雄不在已拥有列表中。", nameof(activeHeroId));
        ActiveHeroId = activeHeroId;
        HeroIds = Array.AsReadOnly(m_Heroes.Keys.OrderBy(x => x).ToArray());
        foreach (int heroId in HeroIds)
            m_HeroSlots.Add(heroId, new());
        Capacity = equipmentCapacity;
        Soul = new(Math.Max(0, soul));
    }

    /// <summary>读取指定英雄的同一成长属性。</summary>
    /// <param name="heroId">已拥有英雄。</param>
    /// <returns>该英雄的成长绑定。</returns>
    public BindableProperty<HeroProgressionState> GetProgress(int heroId) => m_Heroes[heroId];

    /// <summary>读取指定英雄的同一穿戴属性。</summary>
    /// <param name="heroId">已拥有英雄。</param>
    /// <returns>该英雄的穿戴绑定。</returns>
    public BindableProperty<EquippedItems> GetSlots(int heroId) => m_HeroSlots[heroId];

    /// <summary>读取出战英雄的指定部位。</summary>
    /// <param name="slot">武器或防具。</param>
    /// <returns>该部位的实例或空。</returns>
    /// <exception cref="ArgumentOutOfRangeException">部位无效。</exception>
    public EquipmentData GetEquippedItem(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Weapon => Weapon,
        EquipmentSlot.Armor => Armor,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };
}

/// <summary>同一次经验曲线求值的不可变结果。</summary>
/// <param name="TotalExperience">累计经验。</param>
/// <param name="Level">当前等级。</param>
/// <param name="Experience">本级经验。</param>
/// <param name="MaxExperience">本级上限，满级为零。</param>
public readonly record struct HeroProgressionState(int TotalExperience, int Level, int Experience, int MaxExperience);

/// <summary>一名英雄完整的穿戴快照，实例不可变。</summary>
/// <param name="Weapon">武器或空。</param>
/// <param name="Armor">防具或空。</param>
public readonly record struct EquippedItems(EquipmentData Weapon, EquipmentData Armor);
