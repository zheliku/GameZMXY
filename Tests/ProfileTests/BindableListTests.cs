using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Architecture;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>标准列表操作、逐项事件、当前只读视图与优先级。</summary>
public sealed class BindableListTests
{
    /// <summary>经 IList 写入也能通知；替换、移动不冒充数量变化。</summary>
    [Fact]
    public void List_StandardOperationsPublishMatchingEvents()
    {
        var list = new BindableList<int>();
        IList<int> writable = list;
        List<string> events = [];
        list.OnAdd += change => events.Add($"add:{change.Index}:{change.Item}");
        list.OnReplace += change => events.Add($"replace:{change.Index}:{change.Item}");
        list.OnMove += change => events.Add($"move:{change.OldIndex}:{change.NewIndex}:{change.Item}");
        list.OnRemove += change => events.Add($"remove:{change.Index}:{change.Item}");
        list.OnClear += () => events.Add("clear");
        list.OnCountChanged += count => events.Add($"count:{count}");
        writable.Add(1);
        writable.Insert(0, 2);
        writable[1] = 3;
        list.Move(1, 0);
        Assert.True(writable.Contains(3));
        int[] copy = new int[2];
        writable.CopyTo(copy, 0);
        Assert.Equal(new[] { 3, 2 }, copy);
        Assert.False(writable.Remove(99));
        writable.RemoveAt(0);
        writable.Clear();
        writable.Clear();
        Assert.Equal(new[] { "add:0:1", "count:1", "add:0:2", "count:2", "replace:1:3", "move:1:0:3", "remove:0:3", "count:1", "clear", "count:0", "clear" }, events);
    }

    /// <summary>相等元素原位替换仍是替换操作，与 QFramework 的列表语义一致。</summary>
    [Fact]
    public void List_ReplacementNotifiesEvenWhenEqual()
    {
        var list = new BindableList<int>([1]);
        int calls = 0;
        list.OnReplace += _ => calls++;
        list[0] = 1;
        Assert.Equal(1, calls);
    }

    /// <summary>整表回调使用同一个实时只读视图；初始化、默认优先级与退订可一起使用。</summary>
    [Fact]
    public void List_BindingOrdersPrioritiesAndProtectsView()
    {
        var list = new BindableList<int>();
        IReadonlyBindableList<int> binding = list;
        IReadOnlyList<int> observed = null;
        List<string> calls = [];
        void Observe(IReadOnlyList<int> value)
        { observed = value; calls.Add("ui"); }
        binding.RegisterWithInitValue(Observe);
        binding.Register(_ => calls.Add("system"), 100);
        binding.Register(_ => calls.Add("ui2"));
        calls.Clear();
        list.Add(1);
        Assert.Equal(new[] { "system", "ui", "ui2" }, calls);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)observed).Add(3));
        list.Add(2);
        Assert.Equal(new[] { 1, 2 }, observed);
        binding.UnRegister(Observe);
        calls.Clear();
        list.Add(3);
        Assert.Equal(new[] { "system", "ui2" }, calls);
    }

    /// <summary>非法目标索引不会先移除原项，也不产生事件。</summary>
    [Fact]
    public void List_InvalidMovePreservesItems()
    {
        var list = new BindableList<int>([1, 2]);
        int calls = 0;
        list.Register(_ => calls++);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.Move(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.Move(-1, 0));
        Assert.Equal(new[] { 1, 2 }, list);
        Assert.Equal(0, calls);
    }

    /// <summary>整批替换只发重置通知；输入枚举失败和相同内容均不改动集合。</summary>
    [Fact]
    public void List_ReplaceAllValidatesBeforeCommitAndPublishesOnce()
    {
        var list = new BindableList<int>([1, 2]);
        int calls = 0;
        list.OnReset += _ => calls++;
        list.ReplaceAll(list);
        Assert.Throws<InvalidOperationException>(() => list.ReplaceAll(Broken()));
        Assert.Equal(new[] { 1, 2 }, list);
        Assert.Equal(0, calls);
        list.ReplaceAll([2, 3]);
        Assert.Equal(new[] { 2, 3 }, list);
        Assert.Equal(1, calls);
    }

    /// <summary>输入列表被复制，初始化失败也不会遗留刷新订阅。</summary>
    [Fact]
    public void List_CopiesInputAndCleansFailedInitialization()
    {
        List<int> original = [1];
        var list = original.ToBindableList();
        original.Add(2);
        Assert.Equal(new[] { 1 }, list);
        int calls = 0;
        void Failing(IReadOnlyList<int> _)
        { calls++; throw new InvalidOperationException(); }
        Assert.Throws<InvalidOperationException>(() => list.RegisterWithInitValue(Failing, 100));
        list.Add(3);
        Assert.Equal(1, calls);
    }

    /// <summary>构造失败的输入枚举。</summary>
    private static IEnumerable<int> Broken()
    {
        yield return 9;
        throw new InvalidOperationException();
    }
}
