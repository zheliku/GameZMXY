using System;
using System.Collections.Generic;
using GameLogic.Architecture;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>泛型字典各写入路径的通知、比较规则与订阅优先级。</summary>
public sealed class BindableDictionaryTests
{
    /// <summary>索引新增、替换、接口新增、条件删除和清空都经过通知。</summary>
    [Fact]
    public void Dictionary_AllGenericWritePathsNotify()
    {
        var dictionary = new BindableDictionary<string, int>();
        IDictionary<string, int> writable = dictionary;
        List<string> events = [];
        dictionary.OnAdd += item => events.Add($"add:{item.Key}:{item.Value}");
        dictionary.OnReplace += item => events.Add($"replace:{item.Key}:{item.Value}");
        dictionary.OnRemove += item => events.Add($"remove:{item.Key}:{item.Value}");
        dictionary.OnClear += () => events.Add("clear");
        dictionary.OnCountChanged += count => events.Add($"count:{count}");
        writable["a"] = 1;
        writable["a"] = 1;
        writable.Add(new("b", 2));
        Assert.Throws<ArgumentException>(() => writable.Add("a", 9));
        Assert.False(writable.Remove(new KeyValuePair<string, int>("a", 9)));
        Assert.True(writable.Remove(new KeyValuePair<string, int>("a", 1)));
        Assert.False(writable.Remove("missing"));
        writable.Clear();
        writable.Clear();
        Assert.Equal(new[] { "add:a:1", "count:1", "replace:a:1", "add:b:2", "count:2", "remove:a:1", "count:1", "clear", "count:0", "clear" }, events);
    }

    /// <summary>构造和转换复制输入，同时保留键比较器。</summary>
    [Fact]
    public void Dictionary_CopiesInputAndPreservesComparer()
    {
        Dictionary<string, int> source = new(StringComparer.OrdinalIgnoreCase) { ["Hero"] = 1 };
        var dictionary = source.ToBindableDictionary();
        source["Hero"] = 2;
        Assert.Equal(1, dictionary["hero"]);
        Assert.True(dictionary.TryGetValue("HERO", out int value));
        Assert.Equal(1, value);
        Assert.True(dictionary.ContainsKey("hero"));
        KeyValuePair<string, int>[] copy = new KeyValuePair<string, int>[1];
        dictionary.CopyTo(copy, 0);
        Assert.Equal("Hero", copy[0].Key);
        Assert.Single(dictionary.Keys);
        Assert.Single(dictionary.Values);
        Assert.False(dictionary.IsReadOnly);
    }

    /// <summary>整表绑定为单参数、实时只读视图，优先级默认零。</summary>
    [Fact]
    public void Dictionary_BindingOrdersPrioritiesAndProtectsView()
    {
        var dictionary = new BindableDictionary<int, int>();
        IReadonlyBindableDictionary<int, int> binding = dictionary;
        IReadOnlyDictionary<int, int> view = null;
        List<string> calls = [];
        void Observe(IReadOnlyDictionary<int, int> value)
        { view = value; calls.Add("ui1"); }
        binding.RegisterWithInitValue(Observe);
        binding.Register(_ => calls.Add("system"), 100);
        binding.Register(_ => calls.Add("ui2"));
        calls.Clear();
        dictionary.Add(1, 10);
        Assert.Equal(new[] { "system", "ui1", "ui2" }, calls);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<int, int>)view).Add(2, 20));
        dictionary[1] = 11;
        Assert.Equal(11, view[1]);
        binding.UnRegister(Observe);
        calls.Clear();
        dictionary.Remove(1);
        Assert.Equal(new[] { "system", "ui2" }, calls);
    }

    /// <summary>失败的初始化不会遗留订阅。</summary>
    [Fact]
    public void Dictionary_FailedInitializationRemovesCallback()
    {
        var dictionary = new BindableDictionary<int, int>();
        int calls = 0;
        void Failing(IReadOnlyDictionary<int, int> _)
        { calls++; throw new InvalidOperationException(); }
        Assert.Throws<InvalidOperationException>(() => dictionary.RegisterWithInitValue(Failing));
        dictionary.Add(1, 1);
        Assert.Equal(1, calls);
    }
}
