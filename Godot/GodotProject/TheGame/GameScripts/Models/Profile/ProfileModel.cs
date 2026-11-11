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

    private readonly Dictionary<int, BindableProperty<int>> _heroSouls = new(); // 每名英雄唯一的灵魂余额。
    private readonly Dictionary<int, BindableList<EquipmentData>> _heroBags = new(); // 每名英雄唯一的背包集合。

    /// <summary>
    /// 按玩家席位排列的参战英雄编号；下标 0 为 P1，下标 1 为 P2。
    /// </summary>
    public IReadOnlyList<int> HeroIds { get; }

    /// <summary>
    /// 已通关的最远关卡；业务系统成功结算时推进，地图观察同一绑定。
    /// </summary>
    public BindableProperty<int> HighestClearedLevelId { get; } = new(0);

    /// <summary>
    /// 东海旗幡的首次保证掉落资格；龙王入场时消耗，出售或丢弃不会恢复。
    /// </summary>
    public bool EastSeaBannerAvailable { get; internal set; } = true;

    /// <summary>
    /// 每名英雄的背包最大装备数量。
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 从已验证的档案记录建立数据，不缓存共享经验曲线。
    /// </summary>
    /// <param name="roster">按席位排列的英雄编号与累计经验，包含一至两名不重复的英雄。</param>
    /// <param name="curve">仅构造期间使用的共享经验规则。</param>
    /// <param name="equipmentCapacity">正数背包容量。</param>
    /// <exception cref="ArgumentException">阵容为空、超过两人或英雄编号重复。</exception>
    /// <exception cref="ArgumentNullException">阵容或曲线为空。</exception>
    /// <exception cref="ArgumentOutOfRangeException">容量或英雄编号不是正数。</exception>
    public ProfileModel(IEnumerable<(int HeroId, int TotalExperience)> roster,
        ExperienceCurveUtility curve, int equipmentCapacity)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(equipmentCapacity);
        // 构造时固定阵容与席位顺序，运行期不更换绑定对象。
        var heroIds = new List<int>(2);
        foreach ((int HeroId, int TotalExperience) hero in roster)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hero.HeroId, nameof(roster));
            if (heroIds.Count == 2 || !_heroes.TryAdd(hero.HeroId, new(curve.GetProgress(hero.TotalExperience))))
            {
                throw new ArgumentException("阵容最多两人，且英雄编号不得重复。", nameof(roster));
            }

            heroIds.Add(hero.HeroId);
            _heroSlots.Add(hero.HeroId, new());
            _heroSouls.Add(hero.HeroId, new());
            _heroBags.Add(hero.HeroId, new());
        }

        if (heroIds.Count == 0)
        {
            throw new ArgumentException("阵容不能为空。", nameof(roster));
        }

        HeroIds = heroIds.AsReadOnly();
        Capacity = equipmentCapacity;
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
    /// 读取指定英雄唯一的灵魂余额。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <returns>该英雄的灵魂绑定。</returns>
    public BindableProperty<int> GetSoul(int heroId) => _heroSouls[heroId];

    /// <summary>
    /// 读取指定英雄唯一的背包集合。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <returns>该英雄的背包集合。</returns>
    public BindableList<EquipmentData> GetBag(int heroId) => _heroBags[heroId];

    /// <summary>
    /// 查询整个档案的背包和所有英雄穿戴，供一次性奖励判断是否已拥有该物品。
    /// </summary>
    /// <param name="equipmentId">装备或法宝定义编号。</param>
    /// <returns>任一位置拥有相同定义时为 true。</returns>
    public bool HasEquipment(EquipmentId equipmentId)
    {
        foreach (int heroId in HeroIds)
        {
            if (GetBag(heroId).Any(data => data.EquipmentId == equipmentId))
            {
                return true;
            }

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
    /// 按席位取参战英雄编号。
    /// </summary>
    /// <param name="slot">当前阵容中的玩家席位。</param>
    /// <returns>该席位的英雄编号。</returns>
    /// <exception cref="ArgumentOutOfRangeException">席位不在当前阵容中。</exception>
    public int HeroOf(PlayerSlot slot)
    {
        int index = (int)slot - 1;
        if (index < 0 || index >= HeroIds.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        return HeroIds[index];
    }

    /// <summary>
    /// 按英雄编号取其席位。
    /// </summary>
    /// <param name="heroId">当前阵容中的英雄编号。</param>
    /// <returns>该英雄占据的玩家席位。</returns>
    /// <exception cref="ArgumentOutOfRangeException">英雄不在当前阵容中。</exception>
    public PlayerSlot SlotOf(int heroId)
    {
        int index = ((IList<int>)HeroIds).IndexOf(heroId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(heroId));
        }

        return (PlayerSlot)(index + 1);
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
