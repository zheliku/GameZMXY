using System;
using System.Collections.Generic;

namespace GameLogic.Architecture;

/// <summary>观察者借用的只读属性；在 GGF 窗口 OnOpen 注册、OnClose 退订。</summary>
/// <typeparam name="T">完整的属性值；关联字段可以使用不可变快照。</typeparam>
public interface IReadonlyBindableProperty<T>
{
    /// <summary>当前已经提交的值。</summary>
    T Value { get; }

    /// <summary>注册变化回调；优先级越大越先执行，同优先级按注册顺序。</summary>
    /// <param name="onValueChanged">接收新值的同步回调。</param>
    /// <param name="priority">默认零。</param>
    void Register(Action<T> onValueChanged, int priority = 0);

    /// <summary>移除最近一次注册的同一个回调。</summary>
    /// <param name="onValueChanged">原回调。</param>
    void UnRegister(Action<T> onValueChanged);

    /// <summary>注册后立即以当前值初始化显示，后续按优先级接收新值。</summary>
    /// <param name="onValueChanged">接收当前新值的同步回调。</param>
    /// <param name="priority">越大越先执行，默认零；相同优先级按注册顺序。</param>
    void RegisterWithInitValue(Action<T> onValueChanged, int priority = 0);
}

/// <summary>借鉴 QFramework BindableProperty 的值比较与绑定契约；独立于 GGF 事件总线。</summary>
/// <typeparam name="T">属性值类型。</typeparam>
/// <param name="initialValue">创建时已经提交的初值，不发送通知。</param>
/// <remarks>
/// 参考 https://github.com/liangxiegame/QFramework/blob/master/QFramework.cs 的 BindableProperty 区域。
/// 本地实现使用 C# 委托与 EqualityComparer，不依赖 EasyEvent、Unity 或另一套 Architecture。
/// 模型拥有属性，业务系统直接写入；表现层只读并订阅，业务系统维护修改规则。
/// </remarks>
public sealed class BindableProperty<T>(T initialValue = default) : IReadonlyBindableProperty<T>
{
    private T m_Value = initialValue; // 唯一权威值。
    private readonly PriorityCallbacks<Action<T>> m_Callbacks = new(); // 只传新值的有序同步通知。

    /// <summary>先提交新值再同步通知；相等值不重复通知，支持 null。</summary>
    public T Value
    {
        get => m_Value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(m_Value, value))
                return;
            m_Value = value;
            foreach (var callback in m_Callbacks.Snapshot)
                callback(value);
        }
    }

    /// <summary>仅设置值而不通知，用于明确不需要刷新的初始化。</summary>
    /// <param name="value">本次已校验的值。</param>
    public void SetValueWithoutEvent(T value) => m_Value = value;

    /// <summary>注册变化回调。</summary>
    /// <param name="onValueChanged">接收新值的主线程回调，调用方负责对称退订。</param>
    /// <param name="priority">越大越先执行，默认零；相同优先级按注册顺序。</param>
    /// <exception cref="ArgumentNullException">回调为空。</exception>
    public void Register(Action<T> onValueChanged, int priority = 0) => m_Callbacks.Register(onValueChanged, priority);

    /// <summary>先注册再以当前值初始化，初始化期间发生的变化也不会漏掉。</summary>
    /// <param name="onValueChanged">接收新值的主线程回调。</param>
    /// <param name="priority">越大越先执行，默认零。</param>
    /// <exception cref="ArgumentNullException">回调为空。</exception>
    public void RegisterWithInitValue(Action<T> onValueChanged, int priority = 0)
    {
        Register(onValueChanged, priority);
        try
        {
            T currentValue = m_Value;
            onValueChanged(currentValue);
        }
        catch
        {
            UnRegister(onValueChanged);
            throw;
        }
    }

    /// <summary>移除注册的回调。</summary>
    /// <param name="onValueChanged">同一个回调。</param>
    public void UnRegister(Action<T> onValueChanged) => m_Callbacks.UnRegister(onValueChanged);
}

/// <summary>绑定系列共用的稳定排序；仅订阅变化时重建数组，分发不排序、不分配。</summary>
/// <typeparam name="T">回调委托类型。</typeparam>
internal sealed class PriorityCallbacks<T> where T : Delegate
{
    private readonly List<(T Callback, int Priority, long Order)> m_Entries = new(); // 优先级与注册序号。
    private long m_NextOrder; // 同优先级的稳定注册次序。

    /// <summary>本轮可安全枚举的订阅数组。</summary>
    internal T[] Snapshot { get; private set; } = [];

    /// <summary>插入订阅并稳定排序。</summary>
    /// <param name="callback">非空回调。</param>
    /// <param name="priority">越大越先执行。</param>
    internal void Register(T callback, int priority)
    {
        ArgumentNullException.ThrowIfNull(callback);
        m_Entries.Add((callback, priority, m_NextOrder++));
        Rebuild();
    }

    /// <summary>按注册序号移除最后一项匹配项，重复订阅的行为与委托退订一致。</summary>
    /// <param name="callback">原回调。</param>
    internal void UnRegister(T callback)
    {
        int index = -1;
        long order = -1;
        for (int i = 0; i < m_Entries.Count; i++)
            if (Equals(m_Entries[i].Callback, callback) && m_Entries[i].Order > order)
            {
                index = i;
                order = m_Entries[i].Order;
            }

        if (index < 0)
            return;
        m_Entries.RemoveAt(index);
        Rebuild();
    }

    /// <summary>先按优先级降序，再按注册序号升序生成分发数组。</summary>
    private void Rebuild()
    {
        m_Entries.Sort((left, right) =>
        {
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0 ? priority : left.Order.CompareTo(right.Order);
        });
        var callbacks = new T[m_Entries.Count];
        for (int i = 0; i < callbacks.Length; i++)
            callbacks[i] = m_Entries[i].Callback;
        Snapshot = callbacks;
    }
}
