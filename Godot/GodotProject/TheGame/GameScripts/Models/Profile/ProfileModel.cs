using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Equipment;
using GameLogic.Architecture;
using GameLogic.Equipment;

namespace GameLogic.Profile;

/// <summary>
/// 当前档案的权威数据；成长、领取、穿戴、出售与回滚由业务系统执行。
/// </summary>
public sealed class ProfileModel : AbstractModel
{
    private readonly Dictionary<int, BindableProperty<HeroProgressionState>> _heroes = new(); // 每名英雄唯一的成长状态。
    private readonly Dictionary<int, BindableProperty<EquippedItems>> _heroSlots = new(); // 每名英雄唯一的穿戴状态。

    /// <summary>
    /// 当前出战英雄编号。
    /// </summary>
    public int ActiveHeroId { get; }

    /// <summary>
    /// 本档案已拥有的英雄编号。
    /// </summary>
    public IReadOnlyList<int> HeroIds { get; }

    /// <summary>
    /// 出战英雄的完整成长值。
    /// </summary>
    public BindableProperty<HeroProgressionState> Progress => GetProgress(ActiveHeroId);

    /// <summary>
    /// 出战英雄的等级。
    /// </summary>
    public int Level => Progress.Value.Level;

    /// <summary>
    /// 出战英雄累计经验。
    /// </summary>
    public int TotalExperience => Progress.Value.TotalExperience;

    /// <summary>
    /// 本级经验。
    /// </summary>
    public int Experience => Progress.Value.Experience;

    /// <summary>
    /// 本级升级门槛。
    /// </summary>
    public int MaxExperience => Progress.Value.MaxExperience;

    /// <summary>
    /// 出战英雄的完整穿戴。
    /// </summary>
    public BindableProperty<EquippedItems> Slots => GetSlots(ActiveHeroId);

    /// <summary>
    /// 当前武器或空。
    /// </summary>
    public EquipmentData Weapon => Slots.Value.Weapon;

    /// <summary>
    /// 当前防具或空。
    /// </summary>
    public EquipmentData Armor => Slots.Value.Armor;

    /// <summary>
    /// 当前饰品或空；通用饰品仍由实际穿戴英雄独占。
    /// </summary>
    public EquipmentData Accessory => Slots.Value.Accessory;

    /// <summary>
    /// 当前法宝或空；法宝是独立穿戴部位，不占用饰品槽。
    /// </summary>
    public EquipmentData Treasure => Slots.Value.Treasure;

    /// <summary>
    /// 灵魂余额。
    /// </summary>
    public BindableProperty<int> Soul { get; }

    /// <summary>
    /// 已通关的最远关卡；业务系统成功结算时推进，地图观察同一绑定。
    /// </summary>
    public BindableProperty<int> HighestClearedLevelId { get; } = new(0);

    /// <summary>
    /// 东海旗幡的首次保证掉落资格；龙王入场时消耗，出售或丢弃不会恢复。
    /// </summary>
    public bool EastSeaBannerAvailable { get; internal set; } = true;

    /// <summary>
    /// 背包最大装备数量。
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 唯一背包集合；由业务系统执行修改，表现层只读和订阅。
    /// </summary>
    public BindableList<EquipmentData> EquipmentDatas { get; } = new();

    /// <summary>
    /// 从已验证的档案记录建立数据，不缓存共享经验曲线。
    /// </summary>
    /// <param name="activeHeroId">已拥有的出战英雄。</param>
    /// <param name="heroes">英雄编号与累计经验。</param>
    /// <param name="curve">仅构造期间使用的共享经验规则。</param>
    /// <param name="equipmentCapacity">正数背包容量。</param>
    /// <param name="soul">初始余额，负数按零处理。</param>
    /// <exception cref="ArgumentException">英雄重复或出战英雄未拥有。</exception>
    /// <exception cref="ArgumentNullException">英雄列表或曲线为空。</exception>
    /// <exception cref="ArgumentOutOfRangeException">容量不是正数。</exception>
    public ProfileModel(int activeHeroId, IEnumerable<(int HeroId, int TotalExperience)> heroes,
        ExperienceCurveUtility curve, int equipmentCapacity, int soul = 0)
    {
        ArgumentNullException.ThrowIfNull(heroes);
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(equipmentCapacity);
        // 只在构造边界建立数据与固定身份，运行期不更换绑定对象。
        foreach ((int HeroId, int TotalExperience) hero in heroes)
        {
            if (!_heroes.TryAdd(hero.HeroId, new(curve.GetProgress(hero.TotalExperience))))
            {
                throw new ArgumentException($"英雄 {hero.HeroId} 重复。", nameof(heroes));
            }
        }

        if (!_heroes.ContainsKey(activeHeroId))
        {
            throw new ArgumentException("出战英雄不在已拥有列表中。", nameof(activeHeroId));
        }

        ActiveHeroId = activeHeroId;
        HeroIds = Array.AsReadOnly(_heroes.Keys.OrderBy(x => x).ToArray());
        foreach (int heroId in HeroIds)
        {
            _heroSlots.Add(heroId, new());
        }

        Capacity = equipmentCapacity;
        Soul = new(Math.Max(0, soul));
    }

    /// <summary>
    /// 读取指定英雄的同一成长属性。
    /// </summary>
    /// <param name="heroId">已拥有英雄。</param>
    /// <returns>该英雄的成长绑定。</returns>
    public BindableProperty<HeroProgressionState> GetProgress(int heroId) => _heroes[heroId];

    /// <summary>
    /// 读取指定英雄的同一穿戴属性。
    /// </summary>
    /// <param name="heroId">已拥有英雄。</param>
    /// <returns>该英雄的穿戴绑定。</returns>
    public BindableProperty<EquippedItems> GetSlots(int heroId) => _heroSlots[heroId];

    /// <summary>
    /// 查询整个档案的背包和所有英雄穿戴，供一次性奖励判断是否已拥有该物品。
    /// </summary>
    /// <param name="equipmentId">装备或法宝定义编号。</param>
    /// <returns>任一位置拥有相同定义时为 true。</returns>
    public bool HasEquipment(EquipmentId equipmentId)
    {
        if (EquipmentDatas.Any(data => data.EquipmentId == equipmentId))
        {
            return true;
        }

        foreach (int heroId in HeroIds)
        {
            EquippedItems slots = GetSlots(heroId).Value;
            if (slots.Weapon?.EquipmentId == equipmentId || slots.Armor?.EquipmentId == equipmentId ||
                slots.Accessory?.EquipmentId == equipmentId || slots.Treasure?.EquipmentId == equipmentId)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 读取出战英雄的指定部位。
    /// </summary>
    /// <param name="slot">武器、防具、饰品或法宝。</param>
    /// <returns>该部位的实例或空。</returns>
    /// <exception cref="ArgumentOutOfRangeException">部位无效。</exception>
    public EquipmentData GetEquippedItem(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Weapon => Weapon,
            EquipmentSlot.Armor => Armor,
            EquipmentSlot.Accessory => Accessory,
            EquipmentSlot.Treasure => Treasure,
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };
    }
}

/// <summary>
/// 同一次经验曲线求值的不可变结果。
/// </summary>
/// <param name="TotalExperience">累计经验。</param>
/// <param name="Level">当前等级。</param>
/// <param name="Experience">本级经验。</param>
/// <param name="MaxExperience">本级上限，满级为零。</param>
public readonly record struct HeroProgressionState(int TotalExperience, int Level, int Experience, int MaxExperience);

/// <summary>
/// 一名英雄完整的穿戴快照，实例不可变。
/// </summary>
/// <param name="Weapon">武器或空。</param>
/// <param name="Armor">防具或空。</param>
/// <param name="Accessory">饰品或空。</param>
/// <param name="Treasure">法宝或空。</param>
public readonly record struct EquippedItems(EquipmentData Weapon, EquipmentData Armor,
    EquipmentData Accessory = null, EquipmentData Treasure = null);
