using System;
using System.Collections.Generic;
using GameLogic.Equipment;

namespace GameLogic.Profile;

/// <summary>档案拥有的装备背包；以实例为单位占格，拾取、回滚后通知界面读取完整状态。</summary>
public sealed class Inventory
{
    private readonly List<EquipmentInstance> m_Items = new(); // 按拾取顺序排列的装备格。
    private readonly IReadOnlyList<EquipmentInstance> m_View; // 不向调用方开放可变列表。

    /// <summary>当前存档规则允许的最大装备数量。</summary>
    public int Capacity { get; }

    /// <summary>已经领取的装备，地面未拾取物不在此处。</summary>
    public IReadOnlyList<EquipmentInstance> Items => m_View;

    /// <summary>集合实际提交变化后通知一次。</summary>
    public event Action Changed;

    /// <summary>创建指定容量的空背包。</summary>
    /// <param name="capacity">正数容量，由档案配置注入。</param>
    /// <exception cref="ArgumentOutOfRangeException">容量不是正数。</exception>
    public Inventory(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
        m_View = m_Items.AsReadOnly();
    }

    /// <summary>领取一件地面装备；满包或同一实例重复领取时不修改状态。</summary>
    /// <param name="item">已确定属性的装备。</param>
    /// <returns>实际进入背包时为 true。</returns>
    /// <exception cref="ArgumentNullException">装备为空。</exception>
    public bool TryAdd(EquipmentInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (m_Items.Count == Capacity || m_Items.Exists(x => x.InstanceId == item.InstanceId))
            return false;
        m_Items.Add(item);
        Changed?.Invoke();
        return true;
    }

    /// <summary>穿戴事务交换一件物品；先验证，再修改，通知由事务提交者统一发出。</summary>
    /// <param name="removeId">移出背包的实例；空编号表示只放回。</param>
    /// <param name="add">放回背包的实例；空表示只移出。</param>
    /// <returns>容量、编号均有效时完成交换。</returns>
    internal bool Exchange(Guid removeId, EquipmentInstance add)
    {
        int index = removeId == Guid.Empty ? -1 : m_Items.FindIndex(x => x.InstanceId == removeId);
        if (removeId != Guid.Empty && index < 0)
            return false;
        if (add != null && (m_Items.Exists(x => x.InstanceId == add.InstanceId) ||
                            (index < 0 && m_Items.Count == Capacity)))
            return false;
        if (index >= 0)
        {
            if (add == null)
                m_Items.RemoveAt(index);
            else
                m_Items[index] = add;
        }
        else if (add != null)
            m_Items.Add(add);

        return true;
    }

    /// <summary>背包与装备栏全部提交后发出一次通知。</summary>
    internal void NotifyChanged() => Changed?.Invoke();

    /// <summary>批量交易先核对全部实例再移除；由交易提交者统一通知。</summary>
    /// <param name="instanceIds">本次全部要移出的背包实例。</param>
    /// <returns>全部存在并完成移除时为 true。</returns>
    internal bool RemoveAll(HashSet<Guid> instanceIds)
    {
        if (!instanceIds.IsSubsetOf(new HashSet<Guid>(m_Items.ConvertAll(x => x.InstanceId))))
            return false;
        m_Items.RemoveAll(x => instanceIds.Contains(x.InstanceId));
        return true;
    }

    /// <summary>读档或关卡回滚时原子替换内容，非法集合不影响原背包。</summary>
    /// <param name="items">已校验属性的完整实例集合。</param>
    /// <exception cref="ArgumentNullException">集合为空。</exception>
    /// <exception cref="ArgumentException">实例为空、编号重复或超出容量。</exception>
    public void Restore(IEnumerable<EquipmentInstance> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        List<EquipmentInstance> restored = new();
        HashSet<Guid> ids = new();
        // 先检查全部记录，再修改权威集合，避免半份存档进入内存。
        foreach (var item in items)
        {
            if (item == null || !ids.Add(item.InstanceId) || restored.Count == Capacity)
                throw new ArgumentException("背包包含空实例、重复编号或数量超限。", nameof(items));
            restored.Add(item);
        }

        m_Items.Clear();
        m_Items.AddRange(restored);
        Changed?.Invoke();
    }
}
