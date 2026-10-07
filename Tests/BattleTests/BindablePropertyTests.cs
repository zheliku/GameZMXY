using System;
using System.Collections.Generic;
using GameLogic.Bindable;
using Xunit;

namespace GameLogic.Battle.Tests;

/// <summary>可观察属性的公开契约：写入先于通知，通知只属于具体实例，订阅可对称解除。</summary>
public class BindablePropertyTests
{
    /// <summary>订阅本身不触发通知，初始值可立即读取。</summary>
    [Fact]
    public void Subscription_LeavesInitialValueAvailableWithoutNotification()
    {
        BindableProperty<int> property = new(5);
        int calls = 0;
        property.Changed += _ => calls++;
        Assert.Equal(5, property.Value);
        Assert.Equal(0, calls);
    }

    /// <summary>直接赋值及加减在通知前完成保存，重复赋相同值不通知。</summary>
    [Fact]
    public void AssignmentAndArithmetic_NotifyAfterSavingOnlyChangedValues()
    {
        BindableProperty<int> property = new(5);
        List<int> observed = new();
        property.Changed += value =>
        {
            Assert.Equal(value, property.Value);
            observed.Add(value);
        };
        property.Value = 5;
        property.Value += 3;
        property.Value -= 2;
        property.Value = 6;
        Assert.Equal(new[] { 8, 6 }, observed);
    }

    /// <summary>多个订阅者分别收到通知，解除其中一个不影响另一个。</summary>
    [Fact]
    public void Unsubscription_StopsOneObserverAndKeepsOthers()
    {
        BindableProperty<int> property = new();
        int firstCalls = 0;
        int secondCalls = 0;
        Action<int> first = _ => firstCalls++;
        Action<int> second = _ => secondCalls++;
        property.Changed += first;
        property.Changed += second;
        property.Value = 1;
        property.Changed -= first;
        property.Changed -= first;
        property.Value = 2;
        Assert.Equal(1, firstCalls);
        Assert.Equal(2, secondCalls);
    }

    /// <summary>相同泛型类型的不同属性实例互不分发。</summary>
    [Fact]
    public void Instances_KeepValuesAndObserversIsolated()
    {
        BindableProperty<int> first = new();
        BindableProperty<int> second = new();
        int calls = 0;
        first.Changed += _ => calls++;
        second.Value = 10;
        Assert.Equal(0, first.Value);
        Assert.Equal(0, calls);
        first.Value = 3;
        Assert.Equal(10, second.Value);
        Assert.Equal(1, calls);
    }

    /// <summary>默认比较器按值比较字符串并允许 null，未订阅时也可正常赋值。</summary>
    [Fact]
    public void DefaultEquality_HandlesEquivalentStringsAndNull()
    {
        BindableProperty<string> property = new("value");
        int calls = 0;
        Action<string> observer = _ => calls++;
        property.Changed += observer;
        property.Value = new string("value".ToCharArray());
        property.Value = null;
        property.Value = null;
        Assert.Equal(1, calls);
        property.Changed -= observer;
        property.Value = "next";
        Assert.Equal("next", property.Value);
    }
}
