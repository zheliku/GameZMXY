using System;
using System.Collections.Generic;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Level;
using GameLogic.Save;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>单值绑定、整数优先级与真实业务依赖顺序。</summary>
public sealed class BindablePropertyTests
{
    /// <summary>初始化、新值、相等抑制与退订。</summary>
    [Fact]
    public void Binding_InitializesAndPublishesOnlyNewValues()
    {
        var property = new BindableProperty<int>(3);
        IReadonlyBindableProperty<int> view = property;
        List<int> values = [];
        void Observe(int value) => values.Add(value);
        view.RegisterWithInitValue(Observe);
        property.Value = 3;
        property.Value = 5;
        view.UnRegister(Observe);
        view.UnRegister(Observe);
        property.Value = 6;
        view.RegisterWithInitValue(Observe);
        Assert.Equal(new[] { 3, 5, 6 }, values);
    }

    /// <summary>引用值支持 null；明确静默设置不会补发通知。</summary>
    [Fact]
    public void Binding_HandlesEqualityNullAndSilentInitialization()
    {
        var property = new BindableProperty<string>();
        List<string> values = [];
        property.Register(values.Add);
        property.Value = null;
        property.Value = "hello";
        property.Value = new string("hello".ToCharArray());
        property.SetValueWithoutEvent("silent");
        property.Value = "silent";
        property.Value = null;
        Assert.Equal(new[] { "hello", null }, values);
    }

    /// <summary>数值越大越先执行，同优先级保持注册次序，默认优先级为零。</summary>
    [Fact]
    public void Binding_OrdersIntegerPrioritiesStably()
    {
        var property = new BindableProperty<int>();
        List<string> calls = [];
        property.Register(_ => calls.Add("default1"));
        property.Register(_ => calls.Add("low"), int.MinValue);
        property.Register(_ => calls.Add("high1"), 100);
        property.Register(_ => calls.Add("default2"));
        property.Register(_ => calls.Add("high2"), 100);
        property.Register(_ => calls.Add("highest"), int.MaxValue);
        property.Value = 1;
        Assert.Equal(new[] { "highest", "high1", "high2", "default1", "default2", "low" }, calls);
    }

    /// <summary>重复委托按最后注册项退订，即使其优先级不同。</summary>
    [Fact]
    public void Binding_UnregistersMostRecentlyRegisteredDuplicate()
    {
        var property = new BindableProperty<int>();
        List<string> calls = [];
        void Observe(int value) => calls.Add("observe");
        property.Register(Observe, 100);
        property.Register(_ => calls.Add("middle"));
        property.Register(Observe, -100);
        property.UnRegister(Observe);
        property.Value = 1;
        Assert.Equal(new[] { "observe", "middle" }, calls);
    }

    /// <summary>本轮使用订阅快照；回调中注册、退订从下一轮生效。</summary>
    [Fact]
    public void Binding_SubscriptionChangesApplyOnNextDispatch()
    {
        var property = new BindableProperty<int>();
        List<string> calls = [];
        void Later(int value) => calls.Add("later");
        void Removed(int value) => calls.Add("removed");
        property.Register(value =>
        {
            calls.Add("first");
            if (value == 1)
            {
                property.UnRegister(Removed);
                property.Register(Later, 200);
            }
        }, 100);
        property.Register(Removed);
        property.Value = 1;
        property.Value = 2;
        Assert.Equal(new[] { "first", "removed", "later", "first" }, calls);
    }

    /// <summary>初始化先注册，期间的新变化不遗漏；失败时清理订阅。</summary>
    [Fact]
    public void Binding_InitializationHandlesReentryAndFailure()
    {
        var property = new BindableProperty<int>();
        List<int> values = [];
        property.RegisterWithInitValue(value =>
        {
            values.Add(value);
            if (value == 0)
                property.Value = 1;
        }, 100);
        Assert.Equal(new[] { 0, 1 }, values);
        int calls = 0;
        void Failing(int value)
        { calls++; throw new InvalidOperationException(); }
        Assert.Throws<InvalidOperationException>(() => property.RegisterWithInitValue(Failing));
        property.Value = 2;
        Assert.Equal(1, calls);
    }

    /// <summary>同步嵌套赋值保留各轮的新值，不将后一次的新值混入前一轮参数。</summary>
    [Fact]
    public void Binding_ReentrantAssignmentKeepsEachDispatchValue()
    {
        var property = new BindableProperty<int>();
        List<int> values = [];
        property.Register(value => { if (value == 1) property.Value = 2; }, 100);
        property.Register(values.Add);
        property.Value = 1;
        Assert.Equal(new[] { 2, 1 }, values);
        Assert.Equal(2, property.Value);
    }

    /// <summary>即使 UI 先注册，升级的系统同步仍先执行，无需依赖装配时偶然的订阅顺序。</summary>
    [Fact]
    public void ProgressBinding_SynchronizesHeroBeforeEarlierUiSubscriber()
    {
        var scope = new TestGameScope(1, [(1, 0)], 0, 25);
        var system = scope.GetSystem<HeroSystem>();
        ((ICanInit)system).Deinit();
        int calls = 0;
        scope.Profile.Progress.Register(progress =>
        {
            calls++;
            Assert.Equal(progress.Level, scope.Hero.Level.Value);
            Assert.Equal(scope.Hero.MaxHp.Value, scope.Hero.Hp.Value);
        });
        ((ICanInit)system).Init();
        scope.GetSystem<ProfileSystem>().AddExperience(281);
        Assert.Equal(1, calls);
    }

    /// <summary>单个目录状态始终携带完整的阶段、格子与错误信息。</summary>
    [Fact]
    public void SaveDirectory_BindsOneCompleteState()
    {
        var model = new SaveModel();
        int calls = 0;
        model.Directory.Register(state =>
        {
            calls++;
            Assert.Equal(model.Phase, state.Phase);
            Assert.Same(model.Slots, state.Slots);
            Assert.Equal(model.ErrorMessage, state.ErrorMessage);
        });
        model.Directory.Value = new(SaveListState.Failed, Array.Empty<GameLogic.Save.Slots.SaveSlotInfo>(), "failure");
        Assert.Equal(1, calls);
    }

    /// <summary>结局本身依旧在成绩冻结后发布，不依赖手动通知 API。</summary>
    [Fact]
    public void LevelOutcome_PublishesAfterResultIsFrozen()
    {
        var scope = new TestGameScope(1, [(1, 0)], 0, 25);
        var system = scope.GetSystem<LevelSystem>();
        system.Prepare(1);
        system.StartSession(1);
        int calls = 0;
        scope.Level.Outcome.Register(outcome =>
        {
            calls++;
            Assert.Equal(outcome, scope.Level.Result.Outcome);
            Assert.Null(scope.Level.EntrySnapshot);
            Assert.False(scope.Level.TravelAvailable.Value);
        });
        system.Complete(LevelOutcome.Defeated, 100);
        Assert.Equal(1, calls);
    }
}
