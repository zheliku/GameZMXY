using System;
using GameLogic.Architecture;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Equipment;
using GameLogic.Equipment;
using GameLogic.Profile;
using GameLogic.Save.Data;

namespace GameLogic.Save;

/// <summary>独立运行时模型与存档 DTO 的纯转换；不持有状态，不访问文件或节点。</summary>
public sealed class SaveDataUtility : IUtility
{
    /// <summary>捕获完整档案快照，所有可变集合独立于运行时。</summary>
    /// <param name="profile">本次要捕获的档案，只在同步转换期间读取。</param>
    /// <returns>当前格式的独立存档数据。</returns>
    public GameData Capture(ProfileModel profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        GameData data = new() { ActiveHeroId = profile.ActiveHeroId, Soul = profile.Soul.Value };
        List<EquipmentInstance> items = new(profile.Items);
        foreach (int heroId in profile.HeroIds)
        {
            var slots = profile.GetSlots(heroId).Value;
            data.Heroes.Add(new HeroSaveData
            {
                HeroId = heroId,
                TotalExperience = profile.GetProgress(heroId).Value.TotalExperience,
                WeaponInstanceId = slots.Weapon?.InstanceId ?? Guid.Empty,
                ArmorInstanceId = slots.Armor?.InstanceId ?? Guid.Empty
            });
            if (slots.Weapon != null)
                items.Add(slots.Weapon);
            if (slots.Armor != null)
                items.Add(slots.Armor);
        }
        foreach (var item in items)
            data.EquipmentItems.Add(new EquipmentSaveData
            { InstanceId = item.InstanceId, EquipmentId = item.EquipmentId, Attributes = item.Attributes });
        return data;
    }

    /// <summary>读档时完整验证 DTO；不创建或拥有任何运行时 Model。</summary>
    /// <param name="data">当前格式数据。</param>
    /// <param name="definitions">装备定义。</param>
    /// <param name="capacity">背包容量。</param>
    /// <exception cref="ArgumentException">英雄、余额、装备或穿戴无效。</exception>
    /// <exception cref="NotSupportedException">版本不一致。</exception>
    public void Validate(GameData data, TbEquipmentConfig definitions, int capacity)
    {
        ValidateEnvelope(data);
        ArgumentNullException.ThrowIfNull(definitions);
        if (data.Soul < 0 || data.Heroes.Any(x => x.HeroId <= 0 || x.TotalExperience < 0) ||
            !data.Heroes.Any(x => x.HeroId == data.ActiveHeroId))
            throw new ArgumentException("存档英雄或余额无效。", nameof(data));
        var (_, slots) = ReadItems(data, capacity);
        foreach (var item in data.EquipmentItems)
            EquipmentInstance.FromSaveData(definitions.GetOrDefault(item.EquipmentId), item.InstanceId, item.Attributes);
        foreach (var hero in data.Heroes)
        {
            ValidateSlot(slots[hero.HeroId].Weapon, hero.HeroId, EquipmentSlot.Weapon, definitions);
            ValidateSlot(slots[hero.HeroId].Armor, hero.HeroId, EquipmentSlot.Armor, definitions);
        }
    }

    /// <summary>验证格式和必要记录；缺字段不补默认数据。</summary>
    /// <param name="data">待校验的快照。</param>
    /// <exception cref="NotSupportedException">格式不是当前版本。</exception>
    /// <exception cref="ArgumentException">必要集合为空或含空记录。</exception>
    internal static void ValidateEnvelope(GameData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.SaveVersion != GameData.CurrentVersion)
            throw new NotSupportedException($"只支持存档格式 v{GameData.CurrentVersion}，当前文件为 v{data.SaveVersion}。");
        if (data.Heroes == null || data.Heroes.Count == 0 || data.Heroes.Any(x => x == null) ||
            data.EquipmentItems == null)
            throw new ArgumentException("存档缺少英雄或装备记录。");
    }

    /// <summary>建立实例索引和穿戴引用，验证唯一所有权与背包容量。</summary>
    /// <param name="data">已验证必要集合的快照。</param>
    /// <param name="capacity">背包容量。</param>
    /// <returns>背包实例及每名英雄的武器、防具。</returns>
    /// <exception cref="ArgumentException">编号重复、引用不存在或背包超限。</exception>
    internal static (EquipmentInstance[] Bag, Dictionary<int, (EquipmentInstance Weapon, EquipmentInstance Armor)> Slots)
        ReadItems(GameData data, int capacity)
    {
        Dictionary<Guid, EquipmentInstance> items = new();
        foreach (var item in data.EquipmentItems)
        {
            if (item?.Attributes == null || !items.TryAdd(item.InstanceId,
                    new EquipmentInstance(item.InstanceId, item.EquipmentId, item.Attributes)))
                throw new ArgumentException("装备记录为空或实例编号重复。");
        }

        HashSet<Guid> equipped = new();
        Dictionary<int, (EquipmentInstance Weapon, EquipmentInstance Armor)> slots = new();
        foreach (var hero in data.Heroes)
            if (!slots.TryAdd(hero.HeroId, (Resolve(hero.WeaponInstanceId), Resolve(hero.ArmorInstanceId))))
                throw new ArgumentException("英雄编号重复。");
        var bag = items.Values.Where(x => !equipped.Contains(x.InstanceId)).ToArray();
        if (bag.Length > capacity)
            throw new ArgumentException("装备背包超出容量。");
        return (bag, slots);

        // 同一实例只能归一个英雄、一个部位所有。
        EquipmentInstance Resolve(Guid id)
        {
            if (id == Guid.Empty)
                return null;
            if (!items.TryGetValue(id, out var item) || !equipped.Add(id))
                throw new ArgumentException("穿戴引用不存在或重复占用实例。");
            return item;
        }
    }

    /// <summary>校验装备定义与保存的英雄、部位一致。</summary>
    /// <param name="item">已穿戴的实例或空。</param>
    /// <param name="heroId">穿戴者编号。</param>
    /// <param name="slot">保存的部位。</param>
    /// <param name="definitions">当前装备定义。</param>
    private static void ValidateSlot(EquipmentInstance item, int heroId, EquipmentSlot slot,
        TbEquipmentConfig definitions)
    {
        if (item == null)
            return;
        var definition = definitions.Get(item.EquipmentId);
        if ((int)definition.Owner != heroId || definition.Slot != slot)
            throw new ArgumentException("装备不属于该英雄或该部位。");
    }
}
