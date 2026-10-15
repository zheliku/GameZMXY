using System.Linq;
using GameConfig.Equipment;
using GameLogic.Equipment;
using GameLogic.Save.Data;
using System;
using System.Collections.Generic;

namespace GameLogic.Profile;

/// <summary>
/// 一个存档槽的运行时状态：拥有英雄成长、钱包和装备背包。
/// 从读档到返回标题期间保持同一实例；纯 C#，不负责文件读写。
/// </summary>
public sealed class PlayerModel : Architecture.AbstractModel
{
    private readonly Dictionary<int, HeroProgression> m_Heroes = new(); // 已拥有的英雄，按 HeroId 索引。
    private readonly Dictionary<int, HeroEquipment> m_Equipment = new(); // 每名已拥有英雄的穿戴栏。

    /// <summary>当前出战英雄 ID。</summary>
    public int ActiveHeroId { get; }

    /// <summary>拥有：本槽唯一钱包。</summary>
    public Wallet Wallet { get; }

    /// <summary>拥有：本槽共享的装备背包。</summary>
    public Inventory Inventory { get; }

    /// <summary>拥有的英雄记录，按 HeroId 索引；返回索引视图，调用方借用其中的成长对象。</summary>
    public IReadOnlyDictionary<int, HeroProgression> Heroes => m_Heroes;

    /// <summary>借出当前出战英雄的成长记录，不新建副本。</summary>
    public HeroProgression ActiveHero => m_Heroes[ActiveHeroId];

    /// <summary>借出当前出战英雄的穿戴栏，不新建副本。</summary>
    public HeroEquipment ActiveEquipment => m_Equipment[ActiveHeroId];

    /// <summary>读取已拥有英雄的穿戴栏。</summary>
    /// <param name="heroId">已拥有英雄编号。</param>
    /// <returns>该英雄权威的穿戴状态。</returns>
    public HeroEquipment GetEquipment(int heroId) => m_Equipment[heroId];

    /// <summary>接管传入的成长、钱包和背包，创建各英雄穿戴栏；校验出战英雄已拥有。</summary>
    /// <param name="activeHeroId">出战英雄 ID。</param>
    /// <param name="heroes">已拥有的英雄记录，HeroId 不能重复。</param>
    /// <param name="wallet">钱包。</param>
    /// <param name="inventory">本槽拥有的装备背包。</param>
    /// <exception cref="ArgumentNullException">参数为空。</exception>
    /// <exception cref="ArgumentException">英雄重复或出战英雄不在列表中。</exception>
    public PlayerModel(int activeHeroId, IEnumerable<HeroProgression> heroes, Wallet wallet, Inventory inventory)
    {
        ArgumentNullException.ThrowIfNull(heroes);
        Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));

        // 建立英雄索引；重复 ID 说明存档或建档规则有误，直接拒绝。
        foreach (HeroProgression hero in heroes)
        {
            ArgumentNullException.ThrowIfNull(hero, nameof(heroes));
            if (!m_Heroes.TryAdd(hero.HeroId, hero))
            {
                throw new ArgumentException($"英雄 {hero.HeroId} 重复。", nameof(heroes));
            }

            m_Equipment.Add(hero.HeroId, new HeroEquipment());
        }

        if (!m_Heroes.ContainsKey(activeHeroId))
        {
            throw new ArgumentException($"出战英雄 {activeHeroId} 不在已拥有列表中。", nameof(activeHeroId));
        }

        ActiveHeroId = activeHeroId;
    }

    /// <summary>捕获独立快照；实例不可变，所有可变集合均重新创建。</summary>
    /// <returns>当前格式快照。</returns>
    public GameData CaptureSaveData()
    {
        GameData data = new() { ActiveHeroId = ActiveHeroId, Soul = Wallet.Soul };
        foreach (var hero in Heroes.Values.OrderBy(x => x.HeroId))
        {
            var slots = GetEquipment(hero.HeroId);
            data.Heroes.Add(new HeroSaveData
            {
                HeroId = hero.HeroId,
                TotalExperience = hero.TotalExperience,
                WeaponInstanceId = slots.Weapon?.InstanceId ?? Guid.Empty,
                ArmorInstanceId = slots.Armor?.InstanceId ?? Guid.Empty
            });
        }

        // 背包按格子顺序保存；穿戴实例随后保存一次，英雄只引用编号。
        var items = Inventory.Items.Concat(Heroes.Keys.OrderBy(x => x)
            .SelectMany(x => GetEquipment(x).EnumerateItems()));
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
    public static PlayerModel FromSaveData(GameData data, ExperienceCurve curve, TbEquipmentConfig equipment,
        int capacity)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        var playerModel = RestoreSnapshot(data, curve, capacity);
        foreach (var item in data.EquipmentItems)
            EquipmentInstance.FromSaveData(equipment.GetOrDefault(item.EquipmentId), item.InstanceId, item.Attributes);
        foreach (var hero in playerModel.Heroes.Values)
        {
            var slots = playerModel.GetEquipment(hero.HeroId);
            ValidateSlot(slots.Weapon, hero.HeroId, EquipmentSlot.Weapon, equipment);
            ValidateSlot(slots.Armor, hero.HeroId, EquipmentSlot.Armor, equipment);
        }

        return playerModel;
    }

    /// <summary>先验证完整内存快照，再恢复现有对象；保留订阅者引用，不产生半份回滚。</summary>
    /// <param name="snapshot">同一关卡进场时捕获的快照。</param>
    /// <exception cref="ArgumentException">快照损坏。</exception>
    /// <exception cref="InvalidOperationException">快照不属于该英雄集合。</exception>
    public void RestoreCheckpoint(GameData snapshot)
    {
        // 曲线不变；用现有成长记录验证所有经验，避免先恢复一名英雄后才发现坏记录。
        ValidateEnvelope(snapshot);
        if (snapshot.ActiveHeroId != ActiveHeroId || snapshot.Heroes.Count != Heroes.Count ||
            snapshot.Heroes.Select(x => x.HeroId).Distinct().Count() != snapshot.Heroes.Count ||
            snapshot.Heroes.Any(x => !Heroes.ContainsKey(x.HeroId) || x.TotalExperience < 0) ||
            snapshot.Soul < 0)
            throw new InvalidOperationException("回滚快照与档案不一致或数值非法。");
        var (bag, slots) = ReadItems(snapshot, Inventory.Capacity);
        foreach (var hero in snapshot.Heroes)
            Heroes[hero.HeroId].Restore(hero.TotalExperience);
        Wallet.Restore(snapshot.Soul);
        Inventory.Restore(bag);
        foreach (var hero in snapshot.Heroes)
            GetEquipment(hero.HeroId).Restore(slots[hero.HeroId].Weapon, slots[hero.HeroId].Armor);
    }

    /// <summary>验证数据形状并重建纯档案；正式读档再按配置验证内容。</summary>
    /// <param name="data">当前格式快照。</param>
    /// <param name="curve">共享经验曲线。</param>
    /// <param name="capacity">背包容量。</param>
    /// <returns>无节点和文件引用的新档案。</returns>
    private static PlayerModel RestoreSnapshot(GameData data, ExperienceCurve curve, int capacity)
    {
        ValidateEnvelope(data);
        var playerModel = new PlayerModel(data.ActiveHeroId,
            data.Heroes.Select(x => new HeroProgression(x.HeroId, curve, x.TotalExperience)), new Wallet(data.Soul),
            new Inventory(capacity));
        var (bag, slots) = ReadItems(data, capacity);
        playerModel.Inventory.Restore(bag);
        foreach (var hero in data.Heroes)
            playerModel.GetEquipment(hero.HeroId).Restore(slots[hero.HeroId].Weapon, slots[hero.HeroId].Armor);
        return playerModel;
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
