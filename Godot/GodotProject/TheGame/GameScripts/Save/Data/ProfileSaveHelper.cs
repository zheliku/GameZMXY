using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Equipment;
using GameLogic.Equipment;
using GameLogic.Profile;

namespace GameLogic.Save.Data;

/// <summary>运行时档案与当前 GameData 的唯一转换点；同时负责关卡快照的完整校验与回滚。</summary>
public static class ProfileSaveHelper
{
    /// <summary>捕获独立快照；实例不可变，所有可变集合均重新创建。</summary>
    /// <param name="profile">本槽的权威档案。</param>
    /// <returns>当前格式快照。</returns>
    public static GameData CaptureSaveData(PlayerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        GameData data = new() { ActiveHeroId = profile.ActiveHeroId, Soul = profile.Wallet.Soul };
        foreach (var hero in profile.Heroes.Values.OrderBy(x => x.HeroId))
        {
            var slots = profile.GetEquipment(hero.HeroId);
            data.Heroes.Add(new HeroSaveData
            {
                HeroId = hero.HeroId,
                TotalExperience = hero.TotalExperience,
                WeaponInstanceId = slots.Weapon?.InstanceId ?? Guid.Empty,
                ArmorInstanceId = slots.Armor?.InstanceId ?? Guid.Empty
            });
        }

        // 背包按格子顺序保存；穿戴实例随后保存一次，英雄只引用编号。
        var items = profile.Inventory.Items.Concat(profile.Heroes.Keys.OrderBy(x => x)
            .SelectMany(x => profile.GetEquipment(x).EnumerateItems()));
        foreach (var item in items)
            data.EquipmentItems.Add(new EquipmentSaveData
            { InstanceId = item.InstanceId, EquipmentId = item.EquipmentId, Attributes = item.Attributes });
        return data;
    }

    /// <summary>只恢复当前格式，按当前定义验证实际属性、所属角色和部位，不重抽或迁移。</summary>
    /// <param name="data">GGF 读出的当前格式数据。</param>
    /// <param name="curve">共享经验曲线。</param>
    /// <param name="equipment">已验证的装备定义。</param>
    /// <param name="capacity">背包容量。</param>
    /// <returns>经过完整校验的新档案。</returns>
    /// <exception cref="ArgumentException">英雄、装备实例或穿戴引用无效。</exception>
    /// <exception cref="NotSupportedException">存档版本不一致。</exception>
    public static PlayerProfile RestoreProfile(GameData data, ExperienceCurve curve, TbEquipmentConfig equipment,
        int capacity)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        var profile = RestoreSnapshot(data, curve, capacity);
        foreach (var item in data.EquipmentItems)
            EquipmentModule.RestoreInstance(equipment.GetOrDefault(item.EquipmentId), item.InstanceId, item.Attributes);
        foreach (var hero in profile.Heroes.Values)
        {
            var slots = profile.GetEquipment(hero.HeroId);
            ValidateSlot(slots.Weapon, hero.HeroId, EquipmentSlot.Weapon, equipment);
            ValidateSlot(slots.Armor, hero.HeroId, EquipmentSlot.Armor, equipment);
        }

        return profile;
    }

    /// <summary>先验证完整内存快照，再恢复现有对象；保留订阅者引用，不产生半份回滚。</summary>
    /// <param name="profile">要恢复的权威档案。</param>
    /// <param name="snapshot">同一关卡进场时捕获的快照。</param>
    /// <exception cref="ArgumentException">快照损坏。</exception>
    /// <exception cref="InvalidOperationException">快照不属于该英雄集合。</exception>
    public static void RestoreCheckpoint(PlayerProfile profile, GameData snapshot)
    {
        ArgumentNullException.ThrowIfNull(profile);
        // 曲线不变；用现有成长记录验证所有经验，避免先恢复一名英雄后才发现坏记录。
        ValidateEnvelope(snapshot);
        if (snapshot.ActiveHeroId != profile.ActiveHeroId || snapshot.Heroes.Count != profile.Heroes.Count ||
            snapshot.Heroes.Select(x => x.HeroId).Distinct().Count() != snapshot.Heroes.Count ||
            snapshot.Heroes.Any(x => !profile.Heroes.ContainsKey(x.HeroId) || x.TotalExperience < 0) ||
            snapshot.Soul < 0)
            throw new InvalidOperationException("回滚快照与档案不一致或数值非法。");
        var (bag, slots) = ReadItems(snapshot, profile.Inventory.Capacity);
        foreach (var hero in snapshot.Heroes)
            profile.Heroes[hero.HeroId].Restore(hero.TotalExperience);
        profile.Wallet.Restore(snapshot.Soul);
        profile.Inventory.Restore(bag);
        foreach (var hero in snapshot.Heroes)
            profile.GetEquipment(hero.HeroId).Restore(slots[hero.HeroId].Weapon, slots[hero.HeroId].Armor);
    }

    /// <summary>验证数据形状并重建纯档案；正式读档再按配置验证内容。</summary>
    /// <param name="data">当前格式快照。</param>
    /// <param name="curve">共享经验曲线。</param>
    /// <param name="capacity">背包容量。</param>
    /// <returns>无节点和文件引用的新档案。</returns>
    private static PlayerProfile RestoreSnapshot(GameData data, ExperienceCurve curve, int capacity)
    {
        ValidateEnvelope(data);
        var profile = new PlayerProfile(data.ActiveHeroId,
            data.Heroes.Select(x => new HeroProgression(x.HeroId, curve, x.TotalExperience)), new Wallet(data.Soul),
            new Inventory(capacity));
        var (bag, slots) = ReadItems(data, capacity);
        profile.Inventory.Restore(bag);
        foreach (var hero in data.Heroes)
            profile.GetEquipment(hero.HeroId).Restore(slots[hero.HeroId].Weapon, slots[hero.HeroId].Armor);
        return profile;
    }

    /// <summary>验证格式和必要记录；缺字段不补默认数据。</summary>
    /// <param name="data">待校验的快照。</param>
    /// <exception cref="NotSupportedException">格式不是当前版本。</exception>
    /// <exception cref="ArgumentException">必要集合为空或含空记录。</exception>
    private static void ValidateEnvelope(GameData data)
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
    private static (EquipmentInstance[] Bag, Dictionary<int, (EquipmentInstance Weapon, EquipmentInstance Armor)> Slots)
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
