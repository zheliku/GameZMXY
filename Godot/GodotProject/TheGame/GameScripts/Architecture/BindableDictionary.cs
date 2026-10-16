using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameLogic.Architecture;

/// <summary>字典的实时只读视图与当前值绑定。</summary>
/// <typeparam name="TKey">键类型。</typeparam>
/// <typeparam name="TValue">值类型。</typeparam>
public interface IReadonlyBindableDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
{
    /// <summary>注册刷新回调；优先级越大越先执行，同优先级按注册顺序。</summary>
    /// <param name="callback">接收当前字典的同步回调。</param>
    /// <param name="priority">默认零。</param>
    void Register(Action<IReadOnlyDictionary<TKey, TValue>> callback, int priority = 0);
    /// <summary>注册并立即初始化。</summary>
    /// <param name="callback">接收当前字典的同步回调。</param>
    /// <param name="priority">默认零。</param>
    void RegisterWithInitValue(Action<IReadOnlyDictionary<TKey, TValue>> callback, int priority = 0);
    /// <summary>移除最近注册的同一个回调。</summary>
    /// <param name="callback">原回调。</param>
    void UnRegister(Action<IReadOnlyDictionary<TKey, TValue>> callback);
}

/// <summary>参考 QFramework 的可绑定字典，标准泛型字典写入均经过通知入口。</summary>
/// <typeparam name="TKey">键类型。</typeparam>
/// <typeparam name="TValue">值类型。</typeparam>
/// <remarks>只保留泛型集合 API，不引入 Unity、EasyEvent 或旧的二进制序列化接口；存档仍使用 GameData。</remarks>
public sealed class BindableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadonlyBindableDictionary<TKey, TValue>
{
    private readonly Dictionary<TKey, TValue> m_Items; // 唯一键值存储，不借用外部可写字典。
    private readonly ReadOnlyDictionary<TKey, TValue> m_View; // 稳定的实时只读视图。
    private readonly PriorityCallbacks<Action<IReadOnlyDictionary<TKey, TValue>>> m_Callbacks = new(); // 有序刷新订阅。

    /// <summary>新增的键值。</summary>
    public event Action<KeyValuePair<TKey, TValue>> OnAdd;
    /// <summary>移除的键值。</summary>
    public event Action<KeyValuePair<TKey, TValue>> OnRemove;
    /// <summary>替换后的键值，不传旧值。</summary>
    public event Action<KeyValuePair<TKey, TValue>> OnReplace;
    /// <summary>清空操作，原本为空也触发。</summary>
    public event Action OnClear;
    /// <summary>增删后的数量，替换不触发。</summary>
    public event Action<int> OnCountChanged;

    /// <summary>建立空字典。</summary>
    public BindableDictionary() : this((IEqualityComparer<TKey>)null) { }
    /// <summary>按指定键比较规则建立空字典。</summary>
    /// <param name="comparer">键比较器，空表示默认规则。</param>
    public BindableDictionary(IEqualityComparer<TKey> comparer)
    {
        m_Items = new(comparer);
        m_View = new(m_Items);
    }
    /// <summary>复制输入字典，保留其键比较器，避免外部修改绕过通知。</summary>
    /// <param name="values">初始键值。</param>
    public BindableDictionary(Dictionary<TKey, TValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        m_Items = new(values, values.Comparer);
        m_View = new(m_Items);
    }

    /// <inheritdoc />
    public int Count => m_Items.Count;
    /// <inheritdoc />
    public bool IsReadOnly => false;
    /// <inheritdoc />
    public ICollection<TKey> Keys => m_Items.Keys;
    /// <inheritdoc />
    public ICollection<TValue> Values => m_Items.Values;
    /// <inheritdoc />
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => m_Items.Keys;
    /// <inheritdoc />
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => m_Items.Values;
    /// <inheritdoc />
    public TValue this[TKey key]
    {
        get => m_Items[key];
        set
        {
            bool replacing = m_Items.ContainsKey(key);
            m_Items[key] = value;
            int count = Count;
            if (replacing)
                OnReplace?.Invoke(new(key, value));
            else
            {
                OnAdd?.Invoke(new(key, value));
                OnCountChanged?.Invoke(count);
            }
            Publish();
        }
    }

    /// <inheritdoc />
    public void Add(TKey key, TValue value)
    {
        m_Items.Add(key, value);
        int count = Count;
        OnAdd?.Invoke(new(key, value));
        OnCountChanged?.Invoke(count);
        Publish();
    }
    /// <inheritdoc />
    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
    /// <inheritdoc />
    public bool Remove(TKey key)
    {
        if (!m_Items.Remove(key, out TValue value))
            return false;
        int count = Count;
        OnRemove?.Invoke(new(key, value));
        OnCountChanged?.Invoke(count);
        Publish();
        return true;
    }
    /// <inheritdoc />
    public bool Remove(KeyValuePair<TKey, TValue> item) => Contains(item) && Remove(item.Key);
    /// <inheritdoc />
    public void Clear()
    {
        int count = Count;
        m_Items.Clear();
        OnClear?.Invoke();
        if (count > 0)
            OnCountChanged?.Invoke(0);
        Publish();
    }
    /// <inheritdoc />
    public bool ContainsKey(TKey key) => m_Items.ContainsKey(key);
    /// <inheritdoc />
    public bool TryGetValue(TKey key, out TValue value) => m_Items.TryGetValue(key, out value);
    /// <inheritdoc />
    public bool Contains(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)m_Items).Contains(item);
    /// <inheritdoc />
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((ICollection<KeyValuePair<TKey, TValue>>)m_Items).CopyTo(array, arrayIndex);
    /// <inheritdoc />
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => m_Items.GetEnumerator();
    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public void Register(Action<IReadOnlyDictionary<TKey, TValue>> callback, int priority = 0) => m_Callbacks.Register(callback, priority);
    /// <inheritdoc />
    public void UnRegister(Action<IReadOnlyDictionary<TKey, TValue>> callback) => m_Callbacks.UnRegister(callback);
    /// <inheritdoc />
    public void RegisterWithInitValue(Action<IReadOnlyDictionary<TKey, TValue>> callback, int priority = 0)
    {
        Register(callback, priority);
        try
        { callback(m_View); }
        catch { UnRegister(callback); throw; }
    }

    /// <summary>由字典写入入口自动同步刷新，不开放手动通知 API。</summary>
    private void Publish()
    {
        foreach (var callback in m_Callbacks.Snapshot)
            callback(m_View);
    }
}

/// <summary>普通字典到可绑定字典的转换。</summary>
public static class BindableDictionaryExtensions
{
    /// <summary>复制键值并保留键比较器。</summary>
    /// <typeparam name="TKey">键类型。</typeparam>
    /// <typeparam name="TValue">值类型。</typeparam>
    /// <param name="values">原字典。</param>
    /// <returns>独立的新字典。</returns>
    public static BindableDictionary<TKey, TValue> ToBindableDictionary<TKey, TValue>(this Dictionary<TKey, TValue> values) => new(values);
}
