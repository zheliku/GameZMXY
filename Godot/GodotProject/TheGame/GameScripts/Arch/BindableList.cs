using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GameLogic.Architecture;

/// <summary>
/// 参考 QFramework 的 Collection 实现，提供标准列表操作、逐项事件和当前集合绑定。
/// </summary>
/// <typeparam name="T">元素类型。</typeparam>
/// <remarks>Model 拥有数据，System 修改；逐项事件采用 C# 事件，整表 Register 支持整数优先级。</remarks>
public sealed class BindableList<T> : Collection<T>, IReadOnlyList<T>
{
    private readonly ReadOnlyCollection<T> _view; // 同一个实时只读视图。
    private readonly PriorityCallbacks<Action<IReadOnlyList<T>>> _callbacks = new(); // 有序刷新订阅。

    /// <summary>
    /// 新增项的位置与内容。
    /// </summary>
    public event Action<(int Index, T Item)> OnAdd;

    /// <summary>
    /// 移除项原位置与内容。
    /// </summary>
    public event Action<(int Index, T Item)> OnRemove;

    /// <summary>
    /// 替换后的项；数量不变，不传旧值。
    /// </summary>
    public event Action<(int Index, T Item)> OnReplace;

    /// <summary>
    /// 移动项的前后位置与内容。
    /// </summary>
    public event Action<(int OldIndex, int NewIndex, T Item)> OnMove;

    /// <summary>
    /// 清空操作；原本为空也触发。
    /// </summary>
    public event Action OnClear;

    /// <summary>
    /// 增删后的数量；替换与移动不触发。
    /// </summary>
    public event Action<int> OnCountChanged;

    /// <summary>
    /// 整批替换后的集合，不伪装成逐项增删。
    /// </summary>
    public event Action<IReadOnlyList<T>> OnReset;

    /// <summary>
    /// 建立空集合。
    /// </summary>
    public BindableList()
    {
        _view = new(this);
    }

    /// <summary>
    /// 复制输入元素，外部列表修改不会绕过通知。
    /// </summary>
    /// <param name="values">初始元素。</param>
    public BindableList(IEnumerable<T> values) : base(new List<T>(values))
    {
        _view = new(this);
    }

    /// <summary>
    /// 注册当前集合的同步刷新；调用方负责在同一生命周期退订。
    /// </summary>
    /// <param name="callback">接收实时只读视图的回调。</param>
    /// <param name="priority">越大越先执行，同优先级按注册顺序。</param>
    public void Register(Action<IReadOnlyList<T>> callback, int priority = 0)
    {
        _callbacks.Register(callback, priority);
    }

    /// <summary>
    /// 移除最近一次注册的同一回调。
    /// </summary>
    /// <param name="callback">原回调。</param>
    public void UnRegister(Action<IReadOnlyList<T>> callback)
    {
        _callbacks.UnRegister(callback);
    }

    /// <summary>
    /// 注册并立即初始化；初始化失败时撤销本次订阅，再原样抛出异常。
    /// </summary>
    /// <param name="callback">接收实时只读视图的回调。</param>
    /// <param name="priority">越大越先执行，默认零。</param>
    public void RegisterWithInitValue(Action<IReadOnlyList<T>> callback, int priority = 0)
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
    /// 插入后通知位置、数量与当前集合。
    /// </summary>
    /// <param name="index">插入位置。</param>
    /// <param name="item">新元素。</param>
    protected override void InsertItem(int index, T item)
    {
        base.InsertItem(index, item);
        int count = Count;
        OnAdd?.Invoke((index, item));
        OnCountChanged?.Invoke(count);
        Publish();
    }

    /// <summary>
    /// 移除后通知原位置、内容与新数量。
    /// </summary>
    /// <param name="index">移除位置。</param>
    protected override void RemoveItem(int index)
    {
        T item = this[index];
        base.RemoveItem(index);
        int count = Count;
        OnRemove?.Invoke((index, item));
        OnCountChanged?.Invoke(count);
        Publish();
    }

    /// <summary>
    /// 原位替换也通知，与 QFramework 一样不以数量判断变化。
    /// </summary>
    /// <param name="index">替换位置。</param>
    /// <param name="item">新内容。</param>
    protected override void SetItem(int index, T item)
    {
        base.SetItem(index, item);
        OnReplace?.Invoke((index, item));
        Publish();
    }

    /// <summary>
    /// 清空后通知；原本非空时才通知数量变化。
    /// </summary>
    protected override void ClearItems()
    {
        int count = Count;
        base.ClearItems();
        OnClear?.Invoke();
        if (count > 0)
        {
            OnCountChanged?.Invoke(0);
        }

        Publish();
    }

    /// <summary>
    /// 移动元素；先验证两个索引，失败时不破坏集合。
    /// </summary>
    /// <param name="oldIndex">原位置。</param>
    /// <param name="newIndex">移动后的最终位置。</param>
    public void Move(int oldIndex, int newIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(oldIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(oldIndex, Count);
        ArgumentOutOfRangeException.ThrowIfNegative(newIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(newIndex, Count);
        T item = this[oldIndex];
        base.RemoveItem(oldIndex);
        base.InsertItem(newIndex, item);
        OnMove?.Invoke((oldIndex, newIndex, item));
        Publish();
    }

    /// <summary>
    /// 一次替换整表；先复制输入，失败时保持原集合，相同内容不通知。
    /// </summary>
    /// <param name="values">新元素。</param>
    public void ReplaceAll(IEnumerable<T> values)
    {
        var copy = new List<T>(values);
        if (this.SequenceEqual(copy))
        {
            return;
        }

        int previousCount = Count;
        base.ClearItems();
        foreach (T item in copy)
        {
            base.InsertItem(Count, item);
        }

        int count = Count;
        OnReset?.Invoke(_view);
        if (previousCount != count)
        {
            OnCountChanged?.Invoke(count);
        }

        Publish();
    }

    /// <summary>
    /// 由集合修改入口自动刷新，外部不提供手动通知 API。
    /// </summary>
    private void Publish()
    {
        foreach (Action<IReadOnlyList<T>> callback in _callbacks.Snapshot)
        {
            callback(_view);
        }
    }
}
