using System;
using GameConfig.Hero;
using GameConfig.Equipment;
using System.Collections.Generic;
using GameLogic.Battle.Attributes;
using GameLogic.Profile;
using GameConfig.Attributes;
using System.Linq;

namespace GameLogic.Equipment;

/// <summary>装备定义与随机实例规则、英雄装配入口；纯 C#，不持有节点或玩家档案。</summary>
public sealed class EquipmentModule
{
    private readonly TbHeroGrowthConfig m_Growth; // 已在启动阶段验证的成长配置。
    private readonly TbEquipmentConfig m_Definitions; // 装备身份与随机属性范围。

    /// <summary>创建档案作用域内的装配模块。</summary>
    /// <param name="growth">完整的英雄成长表。</param>
    /// <param name="definitions">启动期已验证的装备表。</param>
    /// <exception cref="ArgumentNullException">成长表或装备表为空。</exception>
    public EquipmentModule(TbHeroGrowthConfig growth, TbEquipmentConfig definitions)
    {
        m_Growth = growth ?? throw new ArgumentNullException(nameof(growth));
        m_Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
    }

    /// <summary>为一次掉落抽取实际属性；复现造一 round(random × 范围) 的端点半权重分布。</summary>
    /// <param name="instanceId">本次掉落的唯一编号。</param>
    /// <param name="equipmentId">已校验的装备定义编号。</param>
    /// <param name="random">返回 [0,1) 的随机采样器。</param>
    /// <returns>以后查看、拾取和保存均复用的不可变实例。</returns>
    /// <exception cref="ArgumentNullException">采样器为空。</exception>
    public EquipmentInstance Roll(Guid instanceId, EquipmentId equipmentId, Func<float> random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var definition = m_Definitions.Get(equipmentId);
        EquipmentAttributes attributes = new();
        foreach (var roll in definition.AttributeRanges)
        {
            int value = roll.Minimum == roll.Maximum
                ? roll.Minimum
                : roll.Minimum + (int)Math.Floor(random() * (roll.Maximum - roll.Minimum) + .5);
            attributes = attributes.WithValue(roll.Attribute, value);
        }

        return new EquipmentInstance(instanceId, equipmentId, attributes);
    }

    /// <summary>校验存档中实际属性仍对应当前定义，不在读档时重抽或悄悄更换装备。</summary>
    /// <param name="definition">当前装备定义。</param>
    /// <param name="instanceId">保存的实例编号。</param>
    /// <param name="attributes">保存的实际属性。</param>
    /// <returns>已经验证的装备实例。</returns>
    /// <exception cref="ArgumentException">定义不存在或属性不符合定义。</exception>
    public static EquipmentInstance RestoreInstance(EquipmentConfig definition, Guid instanceId,
        EquipmentAttributes attributes)
    {
        if (definition == null || attributes == null)
            throw new ArgumentException("装备定义或实际属性不存在。");
        foreach (AttributeType attribute in Enum.GetValues<AttributeType>())
        {
            var expected = definition.AttributeRanges.Find(x => x.Attribute == attribute);
            int value = attributes.Get(attribute);
            if (expected == null ? value != 0 : value < expected.Minimum || value > expected.Maximum)
                throw new ArgumentException("装备实例属性不符合当前配置。");
        }

        return new EquipmentInstance(instanceId, definition.Id, attributes);
    }

    /// <summary>按英雄当前成长构建独立于后续档案变化的战斗输入。</summary>
    /// <param name="hero">档案拥有的英雄成长。</param>
    /// <returns>身份、等级与成长属性的不可变输入。</returns>
    /// <exception cref="ArgumentNullException">英雄为空。</exception>
    /// <exception cref="InvalidOperationException">配置缺少对应成长行。</exception>
    public HeroLoadout CreateLoadout(HeroProgression hero)
    {
        ArgumentNullException.ThrowIfNull(hero);
        HeroGrowthConfig row = m_Growth.Get(hero.HeroId, hero.Level)
                               ?? throw new InvalidOperationException($"成长表缺少英雄 {hero.HeroId} 的 {hero.Level} 级。");
        return new HeroLoadout(hero.HeroId, hero.Level, row.BaseAttributes);
    }

    /// <summary>为出战英雄冻结成长与当前装备；升级、换装和进关共用此入口。</summary>
    /// <param name="profile">本槽的权威档案。</param>
    /// <returns>不借用档案集合的不可变输入。</returns>
    public HeroLoadout CreateLoadout(PlayerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var growth = CreateLoadout(profile.ActiveHero);
        return new HeroLoadout(growth.HeroId, growth.Level, growth.BaseAttributes,
            profile.ActiveEquipment.Weapon, profile.ActiveEquipment.Armor);
    }

    /// <summary>把背包中的装备穿到出战英雄；替换件原位放回，异角色装备没有副作用。</summary>
    /// <param name="profile">本槽档案。</param>
    /// <param name="instanceId">背包中要穿上的实例。</param>
    /// <returns>成功完成一次交换时为 true。</returns>
    public bool TryEquip(PlayerProfile profile, Guid instanceId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var item = profile.Inventory.Items.FirstOrDefault(x => x.InstanceId == instanceId);
        if (item == null) return false;
        var definition = m_Definitions.Get(item.EquipmentId);
        if ((int)definition.Owner != profile.ActiveHeroId) return false;
        if (!profile.Inventory.Exchange(instanceId, profile.ActiveEquipment.Get(definition.Slot))) return false;
        profile.ActiveEquipment.Set(definition.Slot, item);
        profile.Inventory.NotifyChanged();
        return true;
    }

    /// <summary>卸下指定部位；背包满时保留当前装备，不丢弃物品。</summary>
    /// <param name="profile">本槽档案。</param>
    /// <param name="slot">要卸下的部位。</param>
    /// <returns>实际卸下时为 true。</returns>
    public bool TryUnequip(PlayerProfile profile, EquipmentSlot slot)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var item = profile.ActiveEquipment.Get(slot);
        if (item == null || !profile.Inventory.Exchange(Guid.Empty, item)) return false;
        profile.ActiveEquipment.Set(slot, null);
        profile.Inventory.NotifyChanged();
        return true;
    }

    /// <summary>出售背包中的一件装备；过期选择、已穿戴物或余额溢出时不修改档案。</summary>
    /// <param name="profile">本槽档案。</param>
    /// <param name="instanceId">要出售的背包实例。</param>
    /// <returns>移除装备并入账配置售价时为 true。</returns>
    public bool TrySell(PlayerProfile profile, Guid instanceId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var item = profile.Inventory.Items.FirstOrDefault(x => x.InstanceId == instanceId);
        if (item == null) return false;
        int price = m_Definitions.Get(item.EquipmentId).SellPrice;
        if (price > int.MaxValue - profile.Wallet.Soul) return false;
        if (!profile.Inventory.Exchange(instanceId, null)) return false;
        // 两份状态已提交再通知；钱包观察者看到的背包也已移除此物。
        profile.Wallet.AddSoul(price);
        profile.Inventory.NotifyChanged();
        return true;
    }

    /// <summary>一次出售背包内指定品质的全部装备；穿戴物不在背包内，余额溢出时整批不变。</summary>
    /// <param name="profile">本槽档案。</param>
    /// <param name="quality">要出售的品质。</param>
    /// <returns>本次实际出售件数；没有匹配物或无法入账时为零。</returns>
    public int SellByQuality(PlayerProfile profile, EquipmentQuality quality)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!Enum.IsDefined(quality)) return 0;
        HashSet<Guid> ids = new();
        long price = 0;
        foreach (var item in profile.Inventory.Items)
        {
            var definition = m_Definitions.Get(item.EquipmentId);
            if (definition.Quality != quality) continue;
            ids.Add(item.InstanceId);
            price += definition.SellPrice;
        }

        if (ids.Count == 0 || price > int.MaxValue - profile.Wallet.Soul || !profile.Inventory.RemoveAll(ids)) return 0;
        profile.Wallet.AddSoul((int)price);
        profile.Inventory.NotifyChanged();
        return ids.Count;
    }
}
