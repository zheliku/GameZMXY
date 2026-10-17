using System;
using System.Linq;
using GameConfig.Equipment;
using GameLogic.Equipment;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>验证集合绑定和跨模型事务的外部可观察行为。</summary>
public sealed class ModelTransactionTests
{
    /// <summary>档案系统检查背包和全部穿戴，不能再次领取已穿戴的同一实例。</summary>
    [Fact]
    public void ProfileCollection_RejectsAlreadyEquippedInstance()
    {
        var scope = new TestGameScope(1, [(1, 0)], 0, 25);
        var system = scope.GetSystem<ProfileSystem>();
        var equipment = new EquipmentSystem(scope);
        var item = equipment.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        Assert.True(system.TryCollectEquipment(item));
        Assert.True(equipment.TryEquip(item.InstanceId));
        Assert.False(system.TryCollectEquipment(item));
        Assert.Empty(scope.Profile.EquipmentDatas);
        Assert.Same(item, scope.Profile.Weapon);
    }

    /// <summary>数量不变的换装仍通知，观察者始终持有同一个实时只读集合。</summary>
    [Fact]
    public void EquipmentSwap_PublishesReplacementWithoutReplacingBindableList()
    {
        var scope = new TestGameScope(1, [(1, 0)], 0, 25);
        var equipment = new EquipmentSystem(scope);
        var first = equipment.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        var second = equipment.Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimStaff, () => 0);
        var items = scope.Profile.EquipmentDatas;
        scope.GetSystem<ProfileSystem>().TryCollectEquipment(first);
        Assert.True(equipment.TryEquip(first.InstanceId));
        scope.GetSystem<ProfileSystem>().TryCollectEquipment(second);
        int notifications = 0;
        items.Register(newItems =>
        {
            notifications++;
            Assert.Same(first, Assert.Single(newItems));
            Assert.Same(first, Assert.Single(items));
        });

        Assert.True(equipment.TryEquip(second.InstanceId));
        Assert.Same(second, scope.Profile.Weapon);
        Assert.Same(items, scope.Profile.EquipmentDatas);
        Assert.Equal(1, notifications);
    }

    /// <summary>回滚通过正常属性与集合入口恢复全部数据，绑定身份保持不变。</summary>
    [Fact]
    public void CheckpointRestore_RestoresModelsAndKeepsBindings()
    {
        var scope = new TestGameScope(1, [(1, 0)], 20, 25);
        var equipment = new EquipmentSystem(scope);
        var staff = equipment.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        scope.GetSystem<ProfileSystem>().TryCollectEquipment(staff);
        var snapshot = scope.CaptureSaveData();
        Assert.True(equipment.TryEquip(staff.InstanceId));
        scope.GetSystem<ProfileSystem>().TryCollectEquipment(equipment.Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimStaff, () => 0));
        scope.GetSystem<ProfileSystem>().AddSoul(100);
        scope.GetSystem<ProfileSystem>().AddExperience(281);
        var progress = scope.Profile.Progress;
        var items = scope.Profile.EquipmentDatas;
        var slots = scope.Profile.Slots;
        int notifications = 0;
        progress.Register(value => { notifications++; Assert.Equal(0, value.TotalExperience); });
        slots.Register(value => { notifications++; Assert.Null(value.Weapon); Assert.Null(value.Armor); });
        scope.Profile.Soul.Register(value => { notifications++; Assert.Equal(20, value); });
        items.Register(value => { notifications++; Assert.Equal(staff.InstanceId, Assert.Single(value).InstanceId); });

        scope.RestoreCheckpoint(snapshot);
        Assert.Equal(4, notifications);
        Assert.Equal(0, progress.Value.TotalExperience);
        Assert.Equal(20, scope.Profile.Soul.Value);
        Assert.Equal(staff.InstanceId, Assert.Single(items).InstanceId);
        Assert.Null(slots.Value.Weapon);
        Assert.Equal(TestTables.Tables.TbHeroGrowthConfig.Get(1, 1).BaseAttributes.Power, scope.Hero.Power.Value);
        Assert.Equal(1, scope.Hero.Level.Value);
        Assert.Same(progress, scope.Profile.Progress);
        Assert.Same(items, scope.Profile.EquipmentDatas);
        Assert.Same(slots, scope.Profile.Slots);
    }
}
