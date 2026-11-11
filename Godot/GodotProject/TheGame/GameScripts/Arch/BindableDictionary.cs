using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameLogic.Architecture;

/// <summary>
/// 参考 QFramework 的可绑定字典，标准泛型字典写入均经过通知入口。
/// </summary>
/// <typeparam name="TKey">键类型。</typeparam>
/// <typeparam name="TValue">值类型。</typeparam>
/// <remarks>只保留泛型集合 API，不引入 Unity、EasyEvent 或旧的二进制序列化接口；存档仍使用 GameData。</remarks>
public sealed class BindableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
{
    private readonly Dictionary<TKey, TValue> _items; // 唯一键值存储，不借用外部可写字典。
    private readonly ReadOnlyDictionary<TKey, TValue> _view; // 稳定的实时只读视图。
    private readonly PriorityCallbacks<Action<IReadOnlyDictionary<TKey, TValue>>> _callbacks = new(); // 有序刷新订阅。

    /// <summary>
    /// 新增的键值。
    /// </summary>
    public event Action<KeyValuePair<TKey, TValue>> OnAdd;

    /// <summary>
    /// 移除的键值。
    /// </summary>
    public event Action<KeyValuePair<TKey, TValue>> OnRemove;

    /// <summary>
    /// 替换后的键值，不传旧值。
    /// </summary>
    public event Action<KeyValuePair<TKey, TValue>> OnReplace;

    /// <summary>
    /// 清空操作，原本为空也触发。
    /// </summary>
    public event Action OnClear;

    /// <summary>
    /// 增删后的数量，替换不触发。
    /// </summary>
    public event Action<int> OnCountChanged;

    /// <summary>
    /// 建立空字典。
    /// </summary>
    public BindableDictionary() : this((IEqualityComparer<TKey>)null)
    {
    }

    /// <summary>
    /// 按指定键比较规则建立空字典。
    /// </summary>
    /// <param name="comparer">键比较器，空表示默认规则。</param>
    public BindableDictionary(IEqualityComparer<TKey> comparer)
    {
        _items = new(comparer);
        _view = new(_items);
    }

    /// <summary>
    /// 复制输入字典，保留其键比较器，避免外部修改绕过通知。
    /// </summary>
    /// <param name="values">初始键值。</param>
    public BindableDictionary(Dictionary<TKey, TValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _items = new(values, values.Comparer);
        _view = new(_items);
    }

    /// <inheritdoc />
    public int Count => _items.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public ICollection<TKey> Keys => _items.Keys;

    /// <inheritdoc />
    public ICollection<TValue> Values => _items.Values;

    /// <inheritdoc />
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _items.Keys;

    /// <inheritdoc />
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _items.Values;

    /// <inheritdoc />
    public TValue this[TKey key]
    {
        get
        {
            return _items[key];
        }

        set
        {
            bool replacing = _items.ContainsKey(key);
            _items[key] = value;
            int count = Count;
            if (replacing)
            {
                OnReplace?.Invoke(new(key, value));
            }
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
        _items.Add(key, value);
        int count = Count;
        OnAdd?.Invoke(new(key, value));
        OnCountChanged?.Invoke(count);
        Publish();
    }

    /// <inheritdoc />
    public void Add(KeyValuePair<TKey, TValue> item)
    {
        Add(item.Key, item.Value);
    }

    /// <inheritdoc />
    public bool Remove(TKey key)
    {
        if (!_items.Remove(key, out TValue value))
        {
            return false;
        }

        int count = Count;
        OnRemove?.Invoke(new(key, value));
        OnCountChanged?.Invoke(count);
        Publish();
        return true;
    }

    /// <inheritdoc />
    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        return Contains(item) && Remove(item.Key);
    }

    /// <inheritdoc />
    public void Clear()
    {
        int count = Count;
        _items.Clear();
        OnClear?.Invoke();
        if (count > 0)
        {
            OnCountChanged?.Invoke(0);
        }

        Publish();
    }

    /// <inheritdoc />
    public bool ContainsKey(TKey key)
    {
        return _items.ContainsKey(key);
    }

    /// <inheritdoc />
    public bool TryGetValue(TKey key, out TValue value)
    {
        return _items.TryGetValue(key, out value);
    }

    /// <inheritdoc />
    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        return ((ICollection<KeyValuePair<TKey, TValue>>)_items).Contains(item);
    }

    /// <inheritdoc />
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        ((ICollection<KeyValuePair<TKey, TValue>>)_items).CopyTo(array, arrayIndex);
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// 注册当前集合的同步刷新；调用方负责在同一生命周期退订。
    /// </summary>
    /// <param name="callback">接收实时只读视图的回调。</param>
    /// <param name="priority">越大越先执行，同优先级按注册顺序。</param>
    public void Register(Action<IReadOnlyDictionary<TKey, TValue>> callback, int priority = 0)
    {
        _callbacks.Register(callback, priority);
    }

    /// <summary>
    /// 移除最近一次注册的同一回调。
    /// </summary>
    /// <param name="callback">原回调。</param>
    public void UnRegister(Action<IReadOnlyDictionary<TKey, TValue>> callback)
    {
        _callbacks.UnRegister(callback);
    }

    /// <summary>
    /// 注册并立即初始化；初始化失败时撤销本次订阅，再原样抛出异常。
    /// </summary>
    /// <param name="callback">接收实时只读视图的回调。</param>
    /// <param name="priority">越大越先执行，默认零。</param>
    public void RegisterWithInitValue(Action<IReadOnlyDictionary<TKey, TValue>> callback, int priority = 0)
    {
        Register(callback, priority);
        try
        {
            callback(_view);
        }
        catch
        {
            UnRegister(callback);
            throw;
        }
    }

    /// <summary>
    /// 由字典写入入口自动同步刷新，不开放手动通知 API。
    /// </summary>
    private void Publish()
    {
        foreach (Action<IReadOnlyDictionary<TKey, TValue>> callback in _callbacks.Snapshot)
        {
            callback(_view);
        }
    }
}
