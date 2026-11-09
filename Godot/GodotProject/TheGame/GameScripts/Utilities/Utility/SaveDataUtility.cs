using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Attributes;
using GameConfig.Equipment;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Equipment;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.Save.Data;
using GameLogic.Skills;

namespace GameLogic.Save;

/// <summary>
/// 独立运行时模型与存档 DTO 的纯转换；不持有状态，不访问文件或节点。
/// </summary>
public sealed class SaveDataUtility : IUtility
{
    /// <summary>
    /// 捕获完整档案快照，所有可变集合独立于运行时。
    /// </summary>
    /// <param name="profile">本次要捕获的档案，只在同步转换期间读取。</param>
    /// <param name="skills">同一档案唯一的技能书。</param>
    /// <returns>当前格式的独立存档数据。</returns>
    public GameData Capture(ProfileModel profile, SkillModel skills)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(skills);
        GameData data = new()
        {
            HighestClearedLevelId = profile.HighestClearedLevelId.Value,
            EastSeaBannerAvailable = profile.EastSeaBannerAvailable
        };
        List<EquipmentData> datas = new();
        foreach (int heroId in profile.HeroIds)
        {
            EquippedItems slots = profile.GetSlots(heroId).Value;
            // 归属编号稳定排序；实例池沿用背包顺序，恢复时不改变格子位置。
            datas.AddRange(profile.GetBag(heroId));
            data.Heroes.Add(new HeroSaveData
            {
                HeroId = heroId,
                TotalExperience = profile.GetProgress(heroId).Value.TotalExperience,
                Soul = profile.GetSoul(heroId).Value,
                BagInstanceIds = profile.GetBag(heroId).Select(item => item.InstanceId).OrderBy(id => id).ToList(),
                LearnedSkills = skills.GetLearnedSkills(heroId).OrderBy(id => id).ToList(),
                PassiveLevels = skills.GetPassiveLevels(heroId).OrderBy(pair => pair.Key)
                    .ToDictionary(pair => pair.Key, pair => pair.Value),
                WeaponInstanceId = slots.Weapon?.InstanceId ?? Guid.Empty,
                ArmorInstanceId = slots.Armor?.InstanceId ?? Guid.Empty,
                AccessoryInstanceId = slots.Accessory?.InstanceId ?? Guid.Empty,
                TreasureInstanceId = slots.Treasure?.InstanceId ?? Guid.Empty
            });
            if (slots.Weapon != null)
            {
                datas.Add(slots.Weapon);
            }

            if (slots.Armor != null)
            {
                datas.Add(slots.Armor);
            }

            if (slots.Accessory != null)
            {
                datas.Add(slots.Accessory);
            }

            if (slots.Treasure != null)
            {
                datas.Add(slots.Treasure);
            }
        }
        foreach (EquipmentData item in datas)
        {
            data.EquipmentItems.Add(new EquipmentSaveData
            {
                InstanceId = item.InstanceId,
                EquipmentId = item.EquipmentId,
                Attributes = item.Attributes
            });
        }

        return data;
    }

    /// <summary>
    /// 读档时完整验证 DTO；不创建或拥有任何运行时 Model。
    /// </summary>
    /// <param name="data">当前格式数据。</param>
    /// <param name="definitions">装备定义。</param>
    /// <param name="capacity">背包容量。</param>
    /// <param name="skills">主动技能定义。</param>
    /// <param name="levels">已实现的关卡定义，用于校验通关记录。</param>
    /// <param name="passives">被动定义，用于校验等级上限。</param>
    /// <exception cref="ArgumentException">英雄、余额、装备、穿戴或技能记录无效。</exception>
    /// <exception cref="NotSupportedException">版本不一致。</exception>
    public void Validate(GameData data, TbEquipmentConfig definitions, int capacity, TbSkillConfig skills,
        GameConfig.Level.TbLevelConfig levels,
        TbPassiveConfig passives)
    {
        ValidateEnvelope(data);
        ValidateSkills(data, skills, passives);
        ValidateLevelProgress(data, levels);
        ArgumentNullException.ThrowIfNull(definitions);
        if (data.Heroes.Any(x => x.HeroId <= 0 || x.TotalExperience < 0 || x.Soul < 0))
        {
            throw new ArgumentException("存档英雄或余额无效。", nameof(data));
        }

        (Dictionary<int, EquipmentData[]> _, Dictionary<int, EquippedItems> slots) = ReadItems(data, capacity);
        foreach (EquipmentSaveData item in data.EquipmentItems)
        {
            ValidateEquipment(item, definitions.GetOrDefault(item.EquipmentId));
        }

        foreach (HeroSaveData hero in data.Heroes)
        {
            ValidateSlot(slots[hero.HeroId].Weapon, hero.HeroId, EquipmentSlot.Weapon, definitions);
            ValidateSlot(slots[hero.HeroId].Armor, hero.HeroId, EquipmentSlot.Armor, definitions);
            ValidateSlot(slots[hero.HeroId].Accessory, hero.HeroId, EquipmentSlot.Accessory, definitions);
            ValidateSlot(slots[hero.HeroId].Treasure, hero.HeroId, EquipmentSlot.Treasure, definitions);
        }
    }

    /// <summary>
    /// 验证格式和必要记录；缺字段不补默认数据。
    /// </summary>
    /// <param name="data">待校验的快照。</param>
    /// <exception cref="NotSupportedException">格式不是当前版本。</exception>
    /// <exception cref="ArgumentException">阵容人数不在一至两人之间、英雄重复、必要集合为空或含空记录。</exception>
    internal static void ValidateEnvelope(GameData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.SaveVersion != GameData.CurrentVersion)
        {
            throw new NotSupportedException($"只支持存档格式 v{GameData.CurrentVersion}，当前文件为 v{data.SaveVersion}。");
        }

        if (data.HighestClearedLevelId < 0 || data.Heroes == null || data.Heroes.Count is < 1 or > 2 || data.EquipmentItems == null)
        {
            throw new ArgumentException("存档缺少英雄或装备记录。");
        }

        foreach (HeroSaveData hero in data.Heroes)
        {
            if (hero == null || hero.LearnedSkills == null || hero.PassiveLevels == null || hero.BagInstanceIds == null)
            {
                throw new ArgumentException("存档缺少英雄或装备记录。");
            }
        }

        if (data.Heroes.Select(hero => hero.HeroId).Distinct().Count() != data.Heroes.Count)
        {
            throw new ArgumentException("存档阵容的英雄编号不得重复。", nameof(data));
        }
    }

    /// <summary>
    /// 最远通关记录必须为空进度或指向已实现关卡，拒绝越界档案。
    /// </summary>
    /// <param name="data">已校验版本的档案。</param>
    /// <param name="levels">当前关卡定义。</param>
    /// <exception cref="ArgumentException">通关主键无效。</exception>
    internal static void ValidateLevelProgress(GameData data, GameConfig.Level.TbLevelConfig levels)
    {
        if (data.HighestClearedLevelId < 0 || data.HighestClearedLevelId != 0 &&
            (levels.GetOrDefault(data.HighestClearedLevelId) == null ||
             data.HighestClearedLevelId == DragonPalaceRules.LevelId))
        {
            throw new ArgumentException("最远通关记录不存在。");
        }
    }

    /// <summary>
    /// 校验技能书的唯一性、定义存在及英雄归属；不静默丢弃坏记录。
    /// </summary>
    /// <param name="data">必要字段已通过校验的档案快照。</param>
    /// <param name="definitions">主动技能定义。</param>
    /// <param name="passives">被动定义及各自等级上限。</param>
    /// <exception cref="ArgumentException">技能重复、不存在或不属于该英雄。</exception>
    internal static void ValidateSkills(GameData data, TbSkillConfig definitions, TbPassiveConfig passives)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        foreach (HeroSaveData hero in data.Heroes)
        {
            if (hero.LearnedSkills.Distinct().Count() != hero.LearnedSkills.Count ||
                hero.LearnedSkills.Any(id => definitions.GetOrDefault(id) is not { } skill || (int)skill.Hero != hero.HeroId))
            {
                throw new ArgumentException($"英雄 {hero.HeroId} 的技能记录无效。");
            }

            if (hero.PassiveLevels.Count != passives.DataList.Count ||
                            passives.DataList.Any(definition => !hero.PassiveLevels.TryGetValue(definition.PassiveId, out int level) ||
                                level < 0 || level > definition.Levels.Count))
            {
                throw new ArgumentException($"英雄 {hero.HeroId} 的被动等级记录无效。");
            }
        }
    }

    /// <summary>
    /// 建立实例索引和穿戴引用，验证唯一所有权与背包容量。
    /// </summary>
    /// <param name="data">已验证必要集合的快照。</param>
    /// <param name="capacity">背包容量。</param>
    /// <returns>按英雄保存的背包实例及完整穿戴，背包顺序保持实例池中的原始格子顺序。</returns>
    /// <exception cref="ArgumentException">编号重复、引用不存在、实例归属重复或缺失、背包超限。</exception>
    internal static (Dictionary<int, EquipmentData[]> Bags, Dictionary<int, EquippedItems> Slots)
        ReadItems(GameData data, int capacity)
    {
        Dictionary<Guid, EquipmentData> equipmentById = new();
        foreach (EquipmentSaveData item in data.EquipmentItems)
        {
            if (item?.Attributes == null || !equipmentById.TryAdd(item.InstanceId,
                    new EquipmentData(item.InstanceId, item.EquipmentId, item.Attributes)))
            {
                throw new ArgumentException("装备记录为空或实例编号重复。");
            }
        }

        HashSet<Guid> ownedIds = new();
        Dictionary<int, EquipmentData[]> bags = new();
        Dictionary<int, EquippedItems> slots = new();
        // 先验证全部归属；调用方只有在完整转换成功后才提交模型。
        foreach (HeroSaveData hero in data.Heroes)
        {
            if (hero.BagInstanceIds.Count > capacity)
            {
                throw new ArgumentException("装备背包超出容量。");
            }

            HashSet<Guid> bagIds = new();
            foreach (Guid id in hero.BagInstanceIds)
            {
                if (!equipmentById.ContainsKey(id) || !ownedIds.Add(id))
                {
                    throw new ArgumentException("背包引用不存在或重复占用实例。");
                }

                bagIds.Add(id);
            }

            var equipped = new EquippedItems(
                Weapon: ResolveEquippedItem(hero.WeaponInstanceId, equipmentById, ownedIds),
                Armor: ResolveEquippedItem(hero.ArmorInstanceId, equipmentById, ownedIds),
                Accessory: ResolveEquippedItem(hero.AccessoryInstanceId, equipmentById, ownedIds),
                Treasure: ResolveEquippedItem(hero.TreasureInstanceId, equipmentById, ownedIds));
            if (!slots.TryAdd(hero.HeroId, equipped))
            {
                throw new ArgumentException("英雄编号重复。");
            }

            bags.Add(hero.HeroId, equipmentById.Values.Where(item => bagIds.Contains(item.InstanceId)).ToArray());
        }

        if (ownedIds.Count != equipmentById.Count)
        {
            throw new ArgumentException("装备实例池包含未归属任何背包或部位的记录。");
        }

        return (bags, slots);
    }

    /// <summary>
    /// 解析穿戴引用，并与全部背包和穿戴共同校验实例唯一归属。
    /// </summary>
    /// <param name="id">保存的实例编号；空编号表示未穿戴。</param>
    /// <param name="equipmentById">本次读档已建立的装备实例索引。</param>
    /// <param name="ownedIds">本次读档中已经被背包或穿戴引用占用的实例。</param>
    /// <returns>引用的装备实例；未穿戴时为空。</returns>
    /// <exception cref="ArgumentException">引用不存在或实例已被其他部位占用。</exception>
    private static EquipmentData ResolveEquippedItem(Guid id,
        IReadOnlyDictionary<Guid, EquipmentData> equipmentById, HashSet<Guid> ownedIds)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        if (!equipmentById.TryGetValue(id, out EquipmentData item) || !ownedIds.Add(id))
        {
            throw new ArgumentException("穿戴引用不存在或重复占用实例。");
        }

        return item;
    }

    /// <summary>
    /// 校验装备实际属性符合当前定义；读档不重抽或悄悄更换装备。
    /// </summary>
    /// <param name="data">已检查编号与必要字段的装备记录。</param>
    /// <param name="definition">当前装备定义。</param>
    /// <exception cref="ArgumentException">定义不存在或属性不符合定义。</exception>
    private static void ValidateEquipment(EquipmentSaveData data, EquipmentConfig definition)
    {
        if (definition == null)
        {
            throw new ArgumentException("装备定义不存在。");
        }

        // 未配置的属性只能为零，已配置的属性必须处于原抽取范围内。
        foreach (AttributeType attribute in Enum.GetValues<AttributeType>())
        {
            EquipmentAttributeRange expected = definition.AttributeRanges.Find(x => x.Attribute == attribute);
            float value = data.Attributes.GetValue(attribute);
            if (!float.IsFinite(value) || (expected == null ? value != 0 : value < expected.Minimum || value > expected.Maximum))
            {
                throw new ArgumentException("装备实际属性不符合当前配置。");
            }
        }
    }

    /// <summary>
    /// 校验装备定义与保存的英雄、部位一致。
    /// </summary>
    /// <param name="data">已穿戴的实例或空。</param>
    /// <param name="heroId">穿戴者编号。</param>
    /// <param name="slot">保存的部位。</param>
    /// <param name="definitions">当前装备定义。</param>
    private static void ValidateSlot(EquipmentData data, int heroId, EquipmentSlot slot,
        TbEquipmentConfig definitions)
    {
        if (data == null)
        {
            return;
        }

        EquipmentConfig definition = definitions.Get(data.EquipmentId);
        if ((definition.Owner != EquipmentOwner.Universal && (int)definition.Owner != heroId) || definition.Slot != slot)
        {
            throw new ArgumentException("装备不属于该英雄或该部位。");
        }
    }
}
