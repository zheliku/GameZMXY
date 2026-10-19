using System;
using GameConfig.Equipment;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Equipment;
using GameLogic.Level;
using GameLogic.Skills;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>通过正式命令验证玩家请求的作用域、会话资格、重复点击及检查点边界。</summary>
public sealed class CommandTests
{
    /// <summary>控制器在返回前已修改自己的档案，另一架构完全隔离；命令不注册为系统。</summary>
    [Fact]
    public void ControllerCommand_ExecutesSynchronouslyInItsOwnScope()
    {
        var first = Ready();
        var second = Ready();
        var firstItem = AddItem(first, EquipmentId.RoughPilgrimStaff);
        var secondItem = AddItem(second, EquipmentId.CommonPilgrimStaff);
        var controller = new Controller(first);
        Assert.False(controller.SendCommand(new EquipEquipmentCommand(secondItem.InstanceId)));
        Assert.True(controller.SendCommand(new EquipEquipmentCommand(firstItem.InstanceId)));
        Assert.Same(firstItem, first.Profile.Weapon);
        Assert.Empty(first.Profile.EquipmentDatas);
        Assert.Null(second.Profile.Weapon);
        Assert.Same(secondItem, Assert.Single(second.Profile.EquipmentDatas));
        Assert.True(controller.SendCommand(new UnequipEquipmentCommand(EquipmentSlot.Weapon)));
        Assert.Same(firstItem, Assert.Single(first.Profile.EquipmentDatas));
    }

    /// <summary>出生前、结局后或停止后的请求不修改装备、余额及技能。</summary>
    /// <param name="state">0 未开始、1 已失败、2 已停止。</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InactiveSession_RejectsAllPlayerMutations(int state)
    {
        var scope = state == 0 ? new TestGameScope(1, [(1, 0)], 100, 25) : Ready();
        var item = AddItem(scope, EquipmentId.RoughPilgrimStaff);
        var system = scope.GetSystem<LevelSystem>();
        if (state == 1)
            system.Complete(LevelOutcome.Defeated, 0);
        else if (state == 2)
            system.EndSession();
        int soul = scope.Profile.Soul.Value;
        var items = scope.Profile.EquipmentDatas.Count;
        Assert.False(scope.SendCommand(new EquipEquipmentCommand(item.InstanceId)));
        Assert.False(scope.SendCommand(new UnequipEquipmentCommand(EquipmentSlot.Weapon)));
        Assert.False(scope.SendCommand(new SellEquipmentCommand(item.InstanceId)));
        Assert.Equal(0, scope.SendCommand(new SellEquipmentByQualityCommand(EquipmentQuality.Rough)));
        Assert.Equal(SkillLearnResult.Unavailable, scope.SendCommand(new LearnSkillCommand(SkillId.RisingDragon)));
        Assert.Null(scope.Profile.Weapon);
        Assert.Equal(items, scope.Profile.EquipmentDatas.Count);
        Assert.Equal(soul, scope.Profile.Soul.Value);
        Assert.Empty(scope.Skills.GetLearnedSkills(1));
    }

    /// <summary>不存在关卡模型时命令返回拒绝结果，不尝试创建作用域或访问玩法系统。</summary>
    [Fact]
    public void MissingLevel_RejectsWithoutAutoCreatingState()
    {
        var scope = new EmptyArchitecture();
        Assert.False(scope.SendCommand(new EquipEquipmentCommand(Guid.NewGuid())));
        Assert.False(scope.SendCommand(new UnequipEquipmentCommand(EquipmentSlot.Weapon)));
        Assert.False(scope.SendCommand(new SellEquipmentCommand(Guid.NewGuid())));
        Assert.Equal(0, scope.SendCommand(new SellEquipmentByQualityCommand(EquipmentQuality.Rough)));
        Assert.Equal(SkillLearnResult.Unavailable, scope.SendCommand(new LearnSkillCommand(SkillId.RisingDragon)));
    }

    /// <summary>重复出售只入账一次，批量出售排除已穿戴物；放弃沿用原有检查点回滚。</summary>
    [Fact]
    public void SaleCommands_PreserveDeduplicationAndCheckpointRollback()
    {
        var scope = Ready();
        var first = AddItem(scope, EquipmentId.RoughPilgrimStaff);
        var second = AddItem(scope, EquipmentId.RoughPilgrimClothes);
        var third = AddItem(scope, EquipmentId.RoughPilgrimStaff);
        int before = scope.Profile.Soul.Value;
        Assert.True(scope.SendCommand(new EquipEquipmentCommand(first.InstanceId)));
        Assert.True(scope.SendCommand(new SellEquipmentCommand(second.InstanceId)));
        Assert.False(scope.SendCommand(new SellEquipmentCommand(second.InstanceId)));
        Assert.Equal(1, scope.SendCommand(new SellEquipmentByQualityCommand(EquipmentQuality.Rough)));
        Assert.Equal(0, scope.SendCommand(new SellEquipmentByQualityCommand(EquipmentQuality.Rough)));
        Assert.Equal(before + 20, scope.Profile.Soul.Value);
        Assert.Same(first, scope.Profile.Weapon);
        scope.GetSystem<LevelSystem>().EndSession();
        Assert.Equal(before, scope.Profile.Soul.Value);
        Assert.Null(scope.Profile.Weapon);
        Assert.Empty(scope.Profile.EquipmentDatas);
    }

    /// <summary>穿戴资格、满包和学习费用继续由正式系统判定，命令不会复制这些规则。</summary>
    [Fact]
    public void PlayerCommands_KeepEquipmentAndLearningRules()
    {
        var scope = Ready(capacity: 1);
        var first = AddItem(scope, EquipmentId.RoughPilgrimStaff);
        Assert.True(scope.SendCommand(new EquipEquipmentCommand(first.InstanceId)));
        AddItem(scope, EquipmentId.RoughPineStaff);
        Assert.False(scope.SendCommand(new UnequipEquipmentCommand(EquipmentSlot.Weapon)));
        Assert.Same(first, scope.Profile.Weapon);
        Assert.Equal(SkillLearnResult.Learned, scope.SendCommand(new LearnSkillCommand(SkillId.RisingDragon)));
        Assert.Equal(0, scope.Profile.Soul.Value);
        Assert.Equal(SkillLearnResult.AlreadyLearned, scope.SendCommand(new LearnSkillCommand(SkillId.RisingDragon)));
        Assert.Equal(SkillLearnResult.InsufficientSoul, scope.SendCommand(new LearnSkillCommand(SkillId.FlameThrust)));
        Assert.Equal(SkillLearnResult.Unavailable, scope.SendCommand(new LearnSkillCommand(SkillId.IceDragonWave)));
        Assert.Equal(SkillId.RisingDragon, Assert.Single(scope.Skills.GetLearnedSkills(1)));
    }

    /// <summary>命令没有生命周期依赖，使用当前绑定的架构；空请求在执行前拒绝。</summary>
    [Fact]
    public void Dispatch_BindsCurrentArchitectureAndRejectsNull()
    {
        var first = Ready();
        var second = Ready();
        var item = AddItem(second, EquipmentId.RoughPilgrimStaff);
        var command = new EquipEquipmentCommand(item.InstanceId);
        Assert.False(first.SendCommand(command));
        Assert.True(second.SendCommand(command));
        Assert.Null(first.Profile.Weapon);
        Assert.Same(item, second.Profile.Weapon);
        Assert.Throws<ArgumentNullException>(() => first.SendCommand<bool>(null));
    }

    /// <summary>在正式规则上建立已开始的测试会话。</summary>
    /// <param name="capacity">本测试背包容量。</param>
    /// <returns>独立测试架构。</returns>
    private static TestGameScope Ready(int capacity = 25)
    {
        var scope = new TestGameScope(1, [(1, 0)], 100, capacity);
        scope.GetSystem<LevelSystem>().Prepare(1);
        scope.GetSystem<LevelSystem>().StartSession(1);
        return scope;
    }

    /// <summary>测试准备使用正式装备抽取与领取入口。</summary>
    /// <param name="scope">所属测试架构。</param>
    /// <param name="id">实际装备定义。</param>
    /// <returns>本次加入的同一实例。</returns>
    private static EquipmentData AddItem(TestGameScope scope, EquipmentId id)
    {
        var item = scope.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), id, () => 0);
        Assert.True(scope.GetSystem<ProfileSystem>().TryCollectEquipment(item));
        return item;
    }

    /// <summary>以正式控制器能力发送，避免只测试直接调用命令方法。</summary>
    private sealed class Controller(IArchitecture architecture) : IController
    {
        /// <inheritdoc />
        IArchitecture IBelongToArchitecture.GetArchitecture() => architecture;
    }

    /// <summary>无关卡的作用域，任何越过资格校验的访问都会使测试失败。</summary>
    private sealed class EmptyArchitecture : IArchitecture
    {
        /// <inheritdoc />
        public TModel GetModel<TModel>() where TModel : class, IModel =>
            typeof(TModel) == typeof(LevelModel) ? null : throw new InvalidOperationException();
        /// <inheritdoc />
        public TSystem GetSystem<TSystem>() where TSystem : class, ISystem => throw new InvalidOperationException();
        /// <inheritdoc />
        public TUtility GetUtility<TUtility>() where TUtility : class, IUtility => throw new InvalidOperationException();
    }
}
