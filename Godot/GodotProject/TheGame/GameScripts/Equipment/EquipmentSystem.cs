using GameLogic.Config;
using GameLogic.Architecture;
using System;
using GameConfig.Hero;
using GameConfig.Equipment;
using System.Collections.Generic;
using GameLogic.Battle.Attributes;
using GameLogic.Profile;
using System.Linq;

namespace GameLogic.Equipment;

/// <summary>装备定义与随机实例规则、英雄装配入口；纯 C#，不持有节点或玩家档案。</summary>
/// <remarks>ProcedureLoadGame 装配后注册到流程 FSM；配置和当前档案经 GetModel 读取。HeroLoadout 是战斗输入快照，实体不借用模型。</remarks>
public sealed class EquipmentSystem(IArchitecture architecture) : AbstractSystem(architecture)
{
    /// <summary>为一次掉落抽取实际属性；复现造一 round(random × 范围) 的端点半权重分布。</summary>
    /// <param name="instanceId">本次掉落的唯一编号。</param>
    /// <param name="equipmentId">已校验的装备定义编号。</param>
    /// <param name="random">返回 [0,1) 的随机采样器。</param>
    /// <returns>以后查看、拾取和保存均复用的不可变实例。</returns>
    /// <exception cref="ArgumentNullException">采样器为空。</exception>
    public EquipmentInstance Roll(Guid instanceId, EquipmentId equipmentId, Func<float> random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var definition = this.GetModel<ConfigurationModel>().Tables.TbEquipmentConfig.Get(equipmentId);
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

    /// <summary>按英雄当前成长构建独立于后续档案变化的战斗输入。</summary>
    /// <param name="hero">档案拥有的英雄成长。</param>
    /// <param name="weapon">本次装配的不可变武器；独立测试场地可省略。</param>
    /// <param name="armor">本次装配的不可变防具；独立测试场地可省略。</param>
    /// <returns>身份、等级与成长属性的不可变输入。</returns>
    /// <exception cref="ArgumentNullException">英雄为空。</exception>
    /// <exception cref="InvalidOperationException">配置缺少对应成长行。</exception>
    public HeroLoadout CreateLoadout(HeroProgression hero, EquipmentInstance weapon = null, EquipmentInstance armor = null)
    {
        ArgumentNullException.ThrowIfNull(hero);
        HeroGrowthConfig row = this.GetModel<ConfigurationModel>().Tables.TbHeroGrowthConfig.Get(hero.HeroId, hero.Level)
                               ?? throw new InvalidOperationException($"成长表缺少英雄 {hero.HeroId} 的 {hero.Level} 级。");
        return new HeroLoadout(hero.HeroId, hero.Level, row.BaseAttributes, weapon, armor);
    }

    /// <summary>为出战英雄冻结成长与当前装备；升级、换装和进关共用此入口。</summary>
    /// <returns>不借用档案集合的不可变输入。</returns>
    public HeroLoadout CreateLoadout()
    {
        var playerModel = this.GetModel<PlayerModel>();
        return CreateLoadout(playerModel.ActiveHero,
            playerModel.ActiveEquipment.Weapon, playerModel.ActiveEquipment.Armor);
    }

    /// <summary>把背包中的装备穿到出战英雄；替换件原位放回，异角色装备没有副作用。</summary>
    /// <param name="instanceId">背包中要穿上的实例。</param>
    /// <returns>成功完成一次交换时为 true。</returns>
    public bool TryEquip(Guid instanceId)
    {
        var playerModel = this.GetModel<PlayerModel>();
        var item = playerModel.Inventory.Items.FirstOrDefault(x => x.InstanceId == instanceId);
        if (item == null)
            return false;
        var definition = this.GetModel<ConfigurationModel>().Tables.TbEquipmentConfig.Get(item.EquipmentId);
        if ((int)definition.Owner != playerModel.ActiveHeroId)
            return false;
        if (!playerModel.Inventory.Exchange(instanceId, playerModel.ActiveEquipment.Get(definition.Slot)))
            return false;
        playerModel.ActiveEquipment.Set(definition.Slot, item);
        playerModel.Inventory.NotifyChanged();
        return true;
    }

    /// <summary>卸下指定部位；背包满时保留当前装备，不丢弃物品。</summary>
    /// <param name="slot">要卸下的部位。</param>
    /// <returns>实际卸下时为 true。</returns>
    public bool TryUnequip(EquipmentSlot slot)
    {
        var playerModel = this.GetModel<PlayerModel>();
        var item = playerModel.ActiveEquipment.Get(slot);
        if (item == null || !playerModel.Inventory.Exchange(Guid.Empty, item))
            return false;
        playerModel.ActiveEquipment.Set(slot, null);
        playerModel.Inventory.NotifyChanged();
        return true;
    }

    /// <summary>出售背包中的一件装备；过期选择、已穿戴物或余额溢出时不修改档案。</summary>
    /// <param name="instanceId">要出售的背包实例。</param>
    /// <returns>移除装备并入账配置售价时为 true。</returns>
    public bool TrySell(Guid instanceId)
    {
        var playerModel = this.GetModel<PlayerModel>();
        var item = playerModel.Inventory.Items.FirstOrDefault(x => x.InstanceId == instanceId);
        if (item == null)
            return false;
        int price = this.GetModel<ConfigurationModel>().Tables.TbEquipmentConfig.Get(item.EquipmentId).SellPrice;
        if (price > int.MaxValue - playerModel.Wallet.Soul)
            return false;
        if (!playerModel.Inventory.Exchange(instanceId, null))
            return false;
        // 两份状态已提交再通知；钱包观察者看到的背包也已移除此物。
        playerModel.Wallet.AddSoul(price);
        playerModel.Inventory.NotifyChanged();
        return true;
    }

    /// <summary>一次出售背包内指定品质的全部装备；穿戴物不在背包内，余额溢出时整批不变。</summary>
    /// <param name="quality">要出售的品质。</param>
    /// <returns>本次实际出售件数；没有匹配物或无法入账时为零。</returns>
    public int SellByQuality(EquipmentQuality quality)
    {
        var playerModel = this.GetModel<PlayerModel>();
        if (!Enum.IsDefined(quality))
            return 0;
        HashSet<Guid> ids = new();
        long price = 0;
        var definitions = this.GetModel<ConfigurationModel>().Tables.TbEquipmentConfig;
        foreach (var item in playerModel.Inventory.Items)
        {
            var definition = definitions.Get(item.EquipmentId);
            if (definition.Quality != quality)
                continue;
            ids.Add(item.InstanceId);
            price += definition.SellPrice;
        }

        if (ids.Count == 0 || price > int.MaxValue - playerModel.Wallet.Soul || !playerModel.Inventory.RemoveAll(ids))
            return 0;
        playerModel.Wallet.AddSoul((int)price);
        playerModel.Inventory.NotifyChanged();
        return ids.Count;
    }
}
