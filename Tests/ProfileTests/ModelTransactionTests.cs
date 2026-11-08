using System;
using System.Linq;
using GameConfig.Equipment;
using GameLogic.Architecture;
using GameLogic.Equipment;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 验证集合绑定和跨模型事务的外部可观察行为。
/// </summary>
public sealed class ModelTransactionTests
{
    /// <summary>
    /// 回滚快照不能交换席位；拒绝时双方成长与钱包都保持调用前状态。
    /// </summary>
    [Fact]
    public void RestoreCheckpoint_RejectsReorderedRosterBeforeMutation()
    {
        var scope = new TestGameScope([(2, 0), (1, 0)], 20, 25);
        GameData snapshot = scope.CaptureSaveData();
        snapshot.Heroes.Reverse();
        scope.GetSystem<ProfileSystem>().AddExperience(2, 135);
        scope.GetSystem<ProfileSystem>().AddExperience(1, 281);
        scope.GetSystem<ProfileSystem>().AddSoul(5);

        Assert.Throws<InvalidOperationException>(() => scope.RestoreCheckpoint(snapshot));
        Assert.Equal(new[] { 2, 1 }, scope.Profile.HeroIds);
        Assert.Equal(135, scope.Profile.GetProgress(2).Value.TotalExperience);
        Assert.Equal(281, scope.Profile.GetProgress(1).Value.TotalExperience);
        Assert.Equal(25, scope.Profile.Soul.Value);
    }

    /// <summary>
    /// 档案系统检查背包和全部穿戴，不能再次领取已穿戴的同一实例。
    /// </summary>
    /// <param name="equipmentId">覆盖全部部位及两种法宝。</param>
    /// <param name="slot">实际穿戴部位。</param>
    /// <param name="otherHero">是否把物品放在非出战英雄的穿戴中。</param>
    [Theory]
    [InlineData(EquipmentId.RoughPilgrimStaff, EquipmentSlot.Weapon, false)]
    [InlineData(EquipmentId.EarthFiendApeArmor, EquipmentSlot.Armor, false)]
    [InlineData(EquipmentId.EarthFiendRing, EquipmentSlot.Accessory, false)]
    [InlineData(EquipmentId.DiamondSnare, EquipmentSlot.Treasure, false)]
    [InlineData(EquipmentId.EastSeaBanner, EquipmentSlot.Treasure, false)]
    [InlineData(EquipmentId.DiamondSnare, EquipmentSlot.Treasure, true)]
    [InlineData(EquipmentId.EastSeaBanner, EquipmentSlot.Treasure, true)]
    public void ProfileCollection_RejectsAlreadyEquippedInstance(EquipmentId equipmentId,
        EquipmentSlot slot, bool otherHero)
    {
        var scope = new TestGameScope([(1, 0), (2, 0)], 0, 25);
        ProfileSystem system = scope.GetSystem<ProfileSystem>();
        var equipment = new EquipmentSystem(scope);
        EquipmentData item = equipment.Roll(Guid.NewGuid(), equipmentId, () => 0);
        Assert.True(system.TryCollectEquipment(item));
        Assert.True(equipment.TryEquip(1, item.InstanceId));
        if (otherHero)
        {
            // 测试夹具把完整穿戴交给另一英雄，验证排重不局限于当前出战者。
            scope.Profile.GetSlots(2).Value = scope.Profile.GetSlots(scope.Profile.HeroOf(PlayerSlot.P1)).Value;
            scope.Profile.GetSlots(scope.Profile.HeroOf(PlayerSlot.P1)).Value = default;
        }

        Assert.False(system.TryCollectEquipment(item));
        Assert.Empty(scope.Profile.EquipmentDatas);
        if (otherHero)
        {
            Assert.Same(item, scope.Profile.GetSlots(2).Value.Treasure);
        }
        else
        {
            EquippedItems slots = scope.Profile.GetSlots(1).Value;
            Assert.Same(item, slot switch
            {
                EquipmentSlot.Weapon => slots.Weapon,
                EquipmentSlot.Armor => slots.Armor,
                EquipmentSlot.Accessory => slots.Accessory,
                EquipmentSlot.Treasure => slots.Treasure,
                _ => throw new ArgumentOutOfRangeException(nameof(slot))
            });
        }

        // 被拒绝的请求不得破坏后续保存与重建的不变量。
        var restored = TestGameScope.FromSaveData(scope.CaptureSaveData(), TestTables.Curve,
            TestTables.Tables.TbEquipmentConfig, 25);
        Assert.Empty(restored.Profile.EquipmentDatas);
    }

    /// <summary>
    /// 唯一奖励以背包和穿戴为事实源，重复时不消耗随机数，回滚后可重新领取。
    /// </summary>
    [Fact]
    public void UniqueEquipmentReward_UsesOwnershipAndRollsBackWithProfile()
    {
        var scope = new TestGameScope([(1, 0)], 0, 25);
        ProfileSystem system = scope.GetSystem<ProfileSystem>();
        GameData snapshot = scope.CaptureSaveData();
        int samples = 0;
        Assert.Equal(EquipmentGrantResult.Collected, system.GrantUniqueEquipment(EquipmentId.DiamondSnare,
            () => { samples++; return .37f; }));
        EquipmentData item = Assert.Single(scope.Profile.EquipmentDatas);
        Assert.True(samples > 0);
        int rolled = samples;

        Assert.True(scope.GetSystem<EquipmentSystem>().TryEquip(1, item.InstanceId));
        Assert.Equal(EquipmentGrantResult.AlreadyOwned, system.GrantUniqueEquipment(EquipmentId.DiamondSnare,
            () => { samples++; return .99f; }));
        Assert.Equal(rolled, samples);
        Assert.Same(item, scope.Profile.GetSlots(scope.Profile.HeroOf(PlayerSlot.P1)).Value.Treasure);
        Assert.Empty(scope.Profile.EquipmentDatas);

        scope.RestoreCheckpoint(snapshot);
        Assert.False(scope.Profile.HasEquipment(EquipmentId.DiamondSnare));
        Assert.Equal(EquipmentGrantResult.Collected, system.GrantUniqueEquipment(EquipmentId.DiamondSnare, () => .5f));
        Assert.NotEqual(item.InstanceId, Assert.Single(scope.Profile.EquipmentDatas).InstanceId);
    }

    /// <summary>
    /// 满包奖励不创建属性实例，失败不修改已有背包，也不留下独立领取标志。
    /// </summary>
    [Fact]
    public void UniqueEquipmentReward_FullBagDoesNotRollOrConsumeEligibility()
    {
        var scope = new TestGameScope([(1, 0)], 0, 1);
        ProfileSystem system = scope.GetSystem<ProfileSystem>();
        EquipmentSystem equipment = scope.GetSystem<EquipmentSystem>();
        EquipmentData item = equipment.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        Assert.True(system.TryCollectEquipment(item));
        Assert.Equal(EquipmentGrantResult.InventoryFull, system.GrantUniqueEquipment(EquipmentId.DiamondSnare,
            () => throw new InvalidOperationException("满包不应抽取属性。")));
        Assert.Same(item, Assert.Single(scope.Profile.EquipmentDatas));

        Assert.True(equipment.TryEquip(1, item.InstanceId));
        Assert.Equal(EquipmentGrantResult.Collected, system.GrantUniqueEquipment(EquipmentId.DiamondSnare, () => .5f));
        Assert.Equal(EquipmentId.DiamondSnare, Assert.Single(scope.Profile.EquipmentDatas).EquipmentId);
    }

    /// <summary>
    /// 数量不变的换装仍通知，观察者始终持有同一个实时只读集合。
    /// </summary>
    [Fact]
    public void EquipmentSwap_PublishesReplacementWithoutReplacingBindableList()
    {
        var scope = new TestGameScope([(1, 0)], 0, 25);
        var equipment = new EquipmentSystem(scope);
        EquipmentData first = equipment.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        EquipmentData second = equipment.Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimStaff, () => 0);
        BindableList<EquipmentData> items = scope.Profile.EquipmentDatas;
        scope.GetSystem<ProfileSystem>().TryCollectEquipment(first);
        Assert.True(equipment.TryEquip(1, first.InstanceId));
        scope.GetSystem<ProfileSystem>().TryCollectEquipment(second);
        int notifications = 0;
        items.Register(newItems =>
        {
            notifications++;
            Assert.Same(first, Assert.Single(newItems));
            Assert.Same(first, Assert.Single(items));
        });

        Assert.True(equipment.TryEquip(1, second.InstanceId));
        Assert.Same(second, scope.Profile.GetSlots(scope.Profile.HeroOf(PlayerSlot.P1)).Value.Weapon);
        Assert.Same(items, scope.Profile.EquipmentDatas);
        Assert.Equal(1, notifications);
    }

    /// <summary>
    /// 回滚通过正常属性与集合入口恢复全部数据，绑定身份保持不变。
    /// </summary>
    [Fact]
    public void CheckpointRestore_RestoresModelsAndKeepsBindings()
    {
        var scope = new TestGameScope([(1, 0)], 20, 25);
        var equipment = new EquipmentSystem(scope);
        EquipmentData staff = equipment.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        scope.GetSystem<ProfileSystem>().TryCollectEquipment(staff);
        GameData snapshot = scope.CaptureSaveData();
        Assert.True(equipment.TryEquip(1, staff.InstanceId));
        scope.GetSystem<ProfileSystem>().TryCollectEquipment(equipment.Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimStaff, () => 0));
        scope.GetSystem<ProfileSystem>().AddSoul(100);
        scope.GetSystem<ProfileSystem>().AddExperience(1, 281);
        BindableProperty<HeroProgressionState> progress = scope.Profile.GetProgress(scope.Profile.HeroOf(PlayerSlot.P1));
        BindableList<EquipmentData> items = scope.Profile.EquipmentDatas;
        BindableProperty<EquippedItems> slots = scope.Profile.GetSlots(scope.Profile.HeroOf(PlayerSlot.P1));
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
        Assert.Equal(TestTables.Tables.TbHeroGrowthConfig.Get(1).Levels[0].BaseAttributes.Power, scope.Hero.Power.Value);
        Assert.Equal(1, scope.Hero.Level.Value);
        Assert.Same(progress, scope.Profile.GetProgress(scope.Profile.HeroOf(PlayerSlot.P1)));
        Assert.Same(items, scope.Profile.EquipmentDatas);
        Assert.Same(slots, scope.Profile.GetSlots(scope.Profile.HeroOf(PlayerSlot.P1)));
    }
}
