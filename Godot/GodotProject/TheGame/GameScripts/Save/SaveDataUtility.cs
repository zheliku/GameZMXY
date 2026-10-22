using System;
using GameLogic.Architecture;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Attributes;
using GameConfig.Equipment;
using GameLogic.Equipment;
using GameLogic.Profile;
using GameLogic.Skills;
using GameConfig.Skills;
using GameLogic.Save.Data;

namespace GameLogic.Save;

/// <summary>独立运行时模型与存档 DTO 的纯转换；不持有状态，不访问文件或节点。</summary>
public sealed class SaveDataUtility : IUtility
{
    /// <summary>捕获完整档案快照，所有可变集合独立于运行时。</summary>
    /// <param name="profile">本次要捕获的档案，只在同步转换期间读取。</param>
    /// <param name="skills">同一档案唯一的技能书。</param>
    /// <returns>当前格式的独立存档数据。</returns>
    public GameData Capture(ProfileModel profile, SkillModel skills)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(skills);
        GameData data = new()
        {
            ActiveHeroId = profile.ActiveHeroId, Soul = profile.Soul.Value,
            HighestClearedLevelId = profile.HighestClearedLevelId.Value
        };
        List<EquipmentData> datas = new(profile.EquipmentDatas);
        foreach (int heroId in profile.HeroIds)
        {
            var slots = profile.GetSlots(heroId).Value;
            data.Heroes.Add(new HeroSaveData
            {
                HeroId = heroId,
                TotalExperience = profile.GetProgress(heroId).Value.TotalExperience,
                LearnedSkills = skills.GetLearnedSkills(heroId).OrderBy(id => id).ToList(),
                WeaponInstanceId = slots.Weapon?.InstanceId ?? Guid.Empty,
                ArmorInstanceId = slots.Armor?.InstanceId ?? Guid.Empty,
                AccessoryInstanceId = slots.Accessory?.InstanceId ?? Guid.Empty
            });
            if (slots.Weapon != null)
                datas.Add(slots.Weapon);
            if (slots.Armor != null)
                datas.Add(slots.Armor);
            if (slots.Accessory != null)
                datas.Add(slots.Accessory);
        }
        foreach (var item in datas)
            data.EquipmentItems.Add(new EquipmentSaveData
            { InstanceId = item.InstanceId, EquipmentId = item.EquipmentId, Attributes = item.Attributes });
        return data;
    }

    /// <summary>读档时完整验证 DTO；不创建或拥有任何运行时 Model。</summary>
    /// <param name="data">当前格式数据。</param>
    /// <param name="definitions">装备定义。</param>
    /// <param name="capacity">背包容量。</param>
    /// <param name="skills">主动技能定义。</param>
    /// <param name="levels">已实现的关卡定义，用于校验通关记录。</param>
    /// <exception cref="ArgumentException">英雄、余额、装备、穿戴或技能记录无效。</exception>
    /// <exception cref="NotSupportedException">版本不一致。</exception>
    public void Validate(GameData data, TbEquipmentConfig definitions, int capacity, TbSkillConfig skills,
        GameConfig.Level.TbLevelConfig levels)
    {
        ValidateEnvelope(data);
        ValidateSkills(data, skills);
        ValidateLevelProgress(data, levels);
        ArgumentNullException.ThrowIfNull(definitions);
        if (data.Soul < 0 || data.Heroes.Any(x => x.HeroId <= 0 || x.TotalExperience < 0) ||
            !data.Heroes.Any(x => x.HeroId == data.ActiveHeroId))
            throw new ArgumentException("存档英雄或余额无效。", nameof(data));
        var (_, slots) = ReadItems(data, capacity);
        foreach (var item in data.EquipmentItems)
            ValidateEquipment(item, definitions.GetOrDefault(item.EquipmentId));
        foreach (var hero in data.Heroes)
        {
            ValidateSlot(slots[hero.HeroId].Weapon, hero.HeroId, EquipmentSlot.Weapon, definitions);
            ValidateSlot(slots[hero.HeroId].Armor, hero.HeroId, EquipmentSlot.Armor, definitions);
            ValidateSlot(slots[hero.HeroId].Accessory, hero.HeroId, EquipmentSlot.Accessory, definitions);
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
        if (data.HighestClearedLevelId < 0 || data.Heroes == null || data.Heroes.Count == 0 || data.Heroes.Any(x => x == null) ||
            data.EquipmentItems == null || data.Heroes.Any(x => x.LearnedSkills == null))
            throw new ArgumentException("存档缺少英雄或装备记录。");
    }

    /// <summary>最远通关记录必须为空进度或指向已实现关卡，拒绝越界档案。</summary>
    /// <param name="data">已校验版本的档案。</param>
    /// <param name="levels">当前关卡定义。</param>
    /// <exception cref="ArgumentException">通关主键无效。</exception>
    internal static void ValidateLevelProgress(GameData data, GameConfig.Level.TbLevelConfig levels)
    {
        if (data.HighestClearedLevelId < 0 || data.HighestClearedLevelId != 0 &&
            levels.GetOrDefault(data.HighestClearedLevelId) == null)
            throw new ArgumentException("最远通关记录不存在。");
    }

    /// <summary>校验技能书的唯一性、定义存在及英雄归属；不静默丢弃坏记录。</summary>
    /// <param name="data">必要字段已通过校验的档案快照。</param>
    /// <param name="definitions">主动技能定义。</param>
    /// <exception cref="ArgumentException">技能重复、不存在或不属于该英雄。</exception>
    internal static void ValidateSkills(GameData data, TbSkillConfig definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        foreach (var hero in data.Heroes)
            if (hero.LearnedSkills.Distinct().Count() != hero.LearnedSkills.Count ||
                hero.LearnedSkills.Any(id => definitions.GetOrDefault(id) is not { } skill || (int)skill.Hero != hero.HeroId))
                throw new ArgumentException($"英雄 {hero.HeroId} 的技能记录无效。");
    }

    /// <summary>建立实例索引和穿戴引用，验证唯一所有权与背包容量。</summary>
    /// <param name="data">已验证必要集合的快照。</param>
    /// <param name="capacity">背包容量。</param>
    /// <returns>背包实例及每名英雄的完整穿戴。</returns>
    /// <exception cref="ArgumentException">编号重复、引用不存在或背包超限。</exception>
    internal static (EquipmentData[] Bag, Dictionary<int, EquippedItems> Slots)
        ReadItems(GameData data, int capacity)
    {
        Dictionary<Guid, EquipmentData> datas = new();
        foreach (var item in data.EquipmentItems)
        {
            if (item?.Attributes == null || !datas.TryAdd(item.InstanceId,
                    new EquipmentData(item.InstanceId, item.EquipmentId, item.Attributes)))
                throw new ArgumentException("装备记录为空或实例编号重复。");
        }

        HashSet<Guid> equipped = new();
        Dictionary<int, EquippedItems> slots = new();
        foreach (var hero in data.Heroes)
            if (!slots.TryAdd(hero.HeroId, new(Resolve(hero.WeaponInstanceId), Resolve(hero.ArmorInstanceId), Resolve(hero.AccessoryInstanceId))))
                throw new ArgumentException("英雄编号重复。");
        var bag = datas.Values.Where(x => !equipped.Contains(x.InstanceId)).ToArray();
        if (bag.Length > capacity)
            throw new ArgumentException("装备背包超出容量。");
        return (bag, slots);

        // 同一实例只能归一个英雄、一个部位所有。
        EquipmentData Resolve(Guid id)
        {
            if (id == Guid.Empty)
                return null;
            if (!datas.TryGetValue(id, out var item) || !equipped.Add(id))
                throw new ArgumentException("穿戴引用不存在或重复占用实例。");
            return item;
        }
    }

    /// <summary>校验装备实际属性符合当前定义；读档不重抽或悄悄更换装备。</summary>
    /// <param name="data">已检查编号与必要字段的装备记录。</param>
    /// <param name="definition">当前装备定义。</param>
    /// <exception cref="ArgumentException">定义不存在或属性不符合定义。</exception>
    private static void ValidateEquipment(EquipmentSaveData data, EquipmentConfig definition)
    {
        if (definition == null)
            throw new ArgumentException("装备定义不存在。");

        // 未配置的属性只能为零，已配置的属性必须处于原抽取范围内。
        foreach (AttributeType attribute in Enum.GetValues<AttributeType>())
        {
            var expected = definition.AttributeRanges.Find(x => x.Attribute == attribute);
            float value = data.Attributes.Get(attribute);
            if (!float.IsFinite(value) || (expected == null ? value != 0 : value < expected.Minimum || value > expected.Maximum))
                throw new ArgumentException("装备实际属性不符合当前配置。");
        }
    }

    /// <summary>校验装备定义与保存的英雄、部位一致。</summary>
    /// <param name="data">已穿戴的实例或空。</param>
    /// <param name="heroId">穿戴者编号。</param>
    /// <param name="slot">保存的部位。</param>
    /// <param name="definitions">当前装备定义。</param>
    private static void ValidateSlot(EquipmentData data, int heroId, EquipmentSlot slot,
        TbEquipmentConfig definitions)
    {
        if (data == null)
            return;
        var definition = definitions.Get(data.EquipmentId);
        if ((definition.Owner != EquipmentOwner.Universal && (int)definition.Owner != heroId) || definition.Slot != slot)
            throw new ArgumentException("装备不属于该英雄或该部位。");
    }
}
