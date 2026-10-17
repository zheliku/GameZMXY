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
/// <remarks>ProcedureLoadGame 装配后注册到流程 FSM；配置和当前档案经 GetModel 读取。英雄属性由本局 HeroSystem 观察成长和穿戴后更新。</remarks>
public sealed class EquipmentSystem(IArchitecture architecture) : AbstractSystem(architecture)
{
    /// <summary>为一次掉落抽取实际属性；复现造一 round(random × 范围) 的端点半权重分布。</summary>
    /// <param name="instanceId">本次掉落的唯一编号。</param>
    /// <param name="equipmentId">已校验的装备定义编号。</param>
    /// <param name="random">返回 [0,1) 的随机采样器。</param>
    /// <returns>以后查看、拾取和保存均复用的不可变实例。</returns>
    /// <exception cref="ArgumentNullException">采样器为空。</exception>
    public EquipmentData Roll(Guid instanceId, EquipmentId equipmentId, Func<float> random)
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

        return new EquipmentData(instanceId, equipmentId, attributes);
    }

    /// <summary>把背包中的装备穿到出战英雄；替换件原位放回，异角色装备没有副作用。</summary>
    /// <param name="instanceId">背包中要穿上的实例。</param>
    /// <returns>成功完成一次交换时为 true。</returns>
    public bool TryEquip(Guid instanceId)
    {
        var profile = this.GetModel<ProfileModel>();
        var item = profile.EquipmentDatas.FirstOrDefault(x => x.InstanceId == instanceId);
        if (item == null)
            return false;
        var definition = this.GetModel<ConfigurationModel>().Tables.TbEquipmentConfig.Get(item.EquipmentId);
        if ((int)definition.Owner != profile.ActiveHeroId)
            return false;
        if (!TryExchange(instanceId, profile.GetEquippedItem(definition.Slot)))
            return false;
        profile.Slots.Value = definition.Slot switch
        {
            EquipmentSlot.Weapon => new(item, profile.Armor),
            EquipmentSlot.Armor => new(profile.Weapon, item),
            _ => throw new ArgumentOutOfRangeException(nameof(definition.Slot))
        };
        return true;
    }

    /// <summary>卸下指定部位；背包满时保留当前装备，不丢弃物品。</summary>
    /// <param name="slot">要卸下的部位。</param>
    /// <returns>实际卸下时为 true。</returns>
    public bool TryUnequip(EquipmentSlot slot)
    {
        var profile = this.GetModel<ProfileModel>();
        var item = profile.GetEquippedItem(slot);
        if (item == null || !TryExchange(Guid.Empty, item))
            return false;
        profile.Slots.Value = slot switch
        {
            EquipmentSlot.Weapon => new(null, profile.Armor),
            EquipmentSlot.Armor => new(profile.Weapon, null),
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };
        return true;
    }

    /// <summary>出售背包中的一件装备；过期选择、已穿戴物或余额溢出时不修改档案。</summary>
    /// <param name="instanceId">要出售的背包实例。</param>
    /// <returns>移除装备并入账配置售价时为 true。</returns>
    public bool TrySell(Guid instanceId)
    {
        var profile = this.GetModel<ProfileModel>();
        var item = profile.EquipmentDatas.FirstOrDefault(x => x.InstanceId == instanceId);
        if (item == null)
            return false;
        int price = this.GetModel<ConfigurationModel>().Tables.TbEquipmentConfig.Get(item.EquipmentId).SellPrice;
        if (price > int.MaxValue - profile.Soul.Value)
            return false;
        if (!TryExchange(instanceId, null))
            return false;
        // 背包移除完成后入账。
        profile.Soul.Value += price;
        return true;
    }

    /// <summary>一次出售背包内指定品质的全部装备；穿戴物不在背包内，余额溢出时整批不变。</summary>
    /// <param name="quality">要出售的品质。</param>
    /// <returns>本次实际出售件数；没有匹配物或无法入账时为零。</returns>
    public int SellByQuality(EquipmentQuality quality)
    {
        var profile = this.GetModel<ProfileModel>();
        if (!Enum.IsDefined(quality))
            return 0;
        HashSet<Guid> ids = new();
        long price = 0;
        var definitions = this.GetModel<ConfigurationModel>().Tables.TbEquipmentConfig;
        foreach (var item in profile.EquipmentDatas)
        {
            var definition = definitions.Get(item.EquipmentId);
            if (definition.Quality != quality)
                continue;
            ids.Add(item.InstanceId);
            price += definition.SellPrice;
        }

        if (ids.Count == 0 || price > int.MaxValue - profile.Soul.Value)
            return 0;
        profile.EquipmentDatas.ReplaceAll(profile.EquipmentDatas.Where(item => !ids.Contains(item.InstanceId)));
        profile.Soul.Value += (int)price;
        return ids.Count;
    }

    /// <summary>交换背包中的一件物品；先验证，再经正常集合入口修改。</summary>
    /// <param name="removeId">移出背包的实例；空编号表示只放回。</param>
    /// <param name="data">放回背包的实例；空表示只移出。</param>
    /// <returns>容量、编号均有效时完成交换。</returns>
    private bool TryExchange(Guid removeId, EquipmentData data)
    {
        var profile = this.GetModel<ProfileModel>();
        int index = removeId == Guid.Empty
            ? -1
            : Enumerable.Range(0, profile.EquipmentDatas.Count).FirstOrDefault(i => profile.EquipmentDatas[i].InstanceId == removeId, -1);
        if (removeId != Guid.Empty && index < 0)
            return false;
        if (data != null && (profile.EquipmentDatas.Any(x => x.InstanceId == data.InstanceId) ||
                            (index < 0 && profile.EquipmentDatas.Count == profile.Capacity)))
            return false;
        if (index >= 0)
        {
            if (data == null)
                profile.EquipmentDatas.RemoveAt(index);
            else
                profile.EquipmentDatas[index] = data;
        }
        else if (data != null)
            profile.EquipmentDatas.Add(data);
        return true;
    }
}
