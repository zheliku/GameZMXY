using System;
using System.Collections.Generic;
using GameConfig.Equipment;
using GameLogic.Equipment;

namespace GameLogic.Profile;

/// <summary>一名英雄已穿戴的武器和防具；实例从背包移入，卸下时归还背包。</summary>
public sealed class HeroEquipment
{
    /// <summary>当前武器，空表示未穿戴。</summary>
    public EquipmentInstance Weapon { get; private set; }

    /// <summary>当前防具，空表示未穿戴。</summary>
    public EquipmentInstance Armor { get; private set; }

    /// <summary>背包与装备栏完成交换后通知。</summary>
    public event Action Changed;

    /// <summary>读取指定部位。</summary>
    /// <param name="slot">武器或防具。</param>
    /// <returns>该部位的实例或空。</returns>
    /// <exception cref="ArgumentOutOfRangeException">部位无效。</exception>
    public EquipmentInstance Get(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Weapon => Weapon,
        EquipmentSlot.Armor => Armor,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    /// <summary>枚举实际穿戴的实例。</summary>
    /// <returns>武器、防具，不包含空槽。</returns>
    public IEnumerable<EquipmentInstance> EnumerateItems()
    {
        if (Weapon != null)
            yield return Weapon;
        if (Armor != null)
            yield return Armor;
    }

    /// <summary>事务完成背包校验和修改后，提交单个装备栏。</summary>
    /// <param name="slot">已校验的部位。</param>
    /// <param name="item">穿上的实例；空表示卸下。</param>
    internal void Set(EquipmentSlot slot, EquipmentInstance item)
    {
        if (slot == EquipmentSlot.Weapon)
            Weapon = item;
        else if (slot == EquipmentSlot.Armor)
            Armor = item;
        else
            throw new ArgumentOutOfRangeException(nameof(slot));
        Changed?.Invoke();
    }

    /// <summary>读档或回滚时一次替换两个部位。</summary>
    /// <param name="weapon">已校验的武器或空。</param>
    /// <param name="armor">已校验的防具或空。</param>
    internal void Restore(EquipmentInstance weapon, EquipmentInstance armor)
    {
        Weapon = weapon;
        Armor = armor;
        Changed?.Invoke();
    }
}
