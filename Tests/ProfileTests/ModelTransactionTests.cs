using System;
using System.Linq;
using GameConfig.Equipment;
using GameLogic.Equipment;
using Xunit;

namespace GameLogic.Profile.Tests
{
    /// <summary>
    /// 验证集合绑定和跨模型事务的外部可观察行为。
    /// </summary>
    public sealed class ModelTransactionTests
    {
        /// <summary>
        /// 档案系统检查背包和全部穿戴，不能再次领取已穿戴的同一实例。
        /// </summary>
        /// <param name="equipmentId">覆盖全部部位及两种法宝。</param>
        /// <param name="slot">实际穿戴部位。</param>
        /// <param name="otherHero">是否把物品放在非出战英雄的穿戴中。</param>
        [Theory]
        [InlineData(EEquipmentId.RoughPilgrimStaff, EEquipmentSlot.Weapon, false)]
        [InlineData(EEquipmentId.EarthFiendApeArmor, EEquipmentSlot.Armor, false)]
        [InlineData(EEquipmentId.EarthFiendRing, EEquipmentSlot.Accessory, false)]
        [InlineData(EEquipmentId.DiamondSnare, EEquipmentSlot.Treasure, false)]
        [InlineData(EEquipmentId.EastSeaBanner, EEquipmentSlot.Treasure, false)]
        [InlineData(EEquipmentId.DiamondSnare, EEquipmentSlot.Treasure, true)]
        [InlineData(EEquipmentId.EastSeaBanner, EEquipmentSlot.Treasure, true)]
        public void ProfileCollection_RejectsAlreadyEquippedInstance(EEquipmentId equipmentId,
            EEquipmentSlot slot, bool otherHero)
        {
            var scope = new TestGameScope(1, [(1, 0), (2, 0)], 0, 25);
            var system = scope.GetSystem<ProfileSystem>();
            var equipment = new EquipmentSystem(scope);
            var item = equipment.Roll(Guid.NewGuid(), equipmentId, () => 0);
            Assert.True(system.TryCollectEquipment(item));
            Assert.True(equipment.TryEquip(item.InstanceId));
            if (otherHero)
            {
                // 测试夹具把完整穿戴交给另一英雄，验证排重不局限于当前出战者。
                scope.Profile.GetSlots(2).Value = scope.Profile.Slots.Value;
                scope.Profile.Slots.Value = default;
            }

            Assert.False(system.TryCollectEquipment(item));
            Assert.Empty(scope.Profile.EquipmentDatas);
            if (otherHero)
            {
                Assert.Same(item, scope.Profile.GetSlots(2).Value.Treasure);
            }
            else
            {
                Assert.Same(item, scope.Profile.GetEquippedItem(slot));
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
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var system = scope.GetSystem<ProfileSystem>();
            var snapshot = scope.CaptureSaveData();
            int samples = 0;
            Assert.Equal(EEquipmentGrantResult.Collected, system.GrantUniqueEquipment(EEquipmentId.DiamondSnare,
                () => { samples++; return .37f; }));
            var item = Assert.Single(scope.Profile.EquipmentDatas);
            Assert.True(samples > 0);
            int rolled = samples;

            Assert.True(scope.GetSystem<EquipmentSystem>().TryEquip(item.InstanceId));
            Assert.Equal(EEquipmentGrantResult.AlreadyOwned, system.GrantUniqueEquipment(EEquipmentId.DiamondSnare,
                () => { samples++; return .99f; }));
            Assert.Equal(rolled, samples);
            Assert.Same(item, scope.Profile.Treasure);
            Assert.Empty(scope.Profile.EquipmentDatas);

            scope.RestoreCheckpoint(snapshot);
            Assert.False(scope.Profile.HasEquipment(EEquipmentId.DiamondSnare));
            Assert.Equal(EEquipmentGrantResult.Collected, system.GrantUniqueEquipment(EEquipmentId.DiamondSnare, () => .5f));
            Assert.NotEqual(item.InstanceId, Assert.Single(scope.Profile.EquipmentDatas).InstanceId);
        }

        /// <summary>
        /// 满包奖励不创建属性实例，失败不修改已有背包，也不留下独立领取标志。
        /// </summary>
        [Fact]
        public void UniqueEquipmentReward_FullBagDoesNotRollOrConsumeEligibility()
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 1);
            var system = scope.GetSystem<ProfileSystem>();
            var equipment = scope.GetSystem<EquipmentSystem>();
            var item = equipment.Roll(Guid.NewGuid(), EEquipmentId.RoughPilgrimStaff, () => 0);
            Assert.True(system.TryCollectEquipment(item));
            Assert.Equal(EEquipmentGrantResult.InventoryFull, system.GrantUniqueEquipment(EEquipmentId.DiamondSnare,
                () => throw new InvalidOperationException("满包不应抽取属性。")));
            Assert.Same(item, Assert.Single(scope.Profile.EquipmentDatas));

            Assert.True(equipment.TryEquip(item.InstanceId));
            Assert.Equal(EEquipmentGrantResult.Collected, system.GrantUniqueEquipment(EEquipmentId.DiamondSnare, () => .5f));
            Assert.Equal(EEquipmentId.DiamondSnare, Assert.Single(scope.Profile.EquipmentDatas).EquipmentId);
        }

        /// <summary>
        /// 数量不变的换装仍通知，观察者始终持有同一个实时只读集合。
        /// </summary>
        [Fact]
        public void EquipmentSwap_PublishesReplacementWithoutReplacingBindableList()
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var equipment = new EquipmentSystem(scope);
            var first = equipment.Roll(Guid.NewGuid(), EEquipmentId.RoughPilgrimStaff, () => 0);
            var second = equipment.Roll(Guid.NewGuid(), EEquipmentId.CommonPilgrimStaff, () => 0);
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

        /// <summary>
        /// 回滚通过正常属性与集合入口恢复全部数据，绑定身份保持不变。
        /// </summary>
        [Fact]
        public void CheckpointRestore_RestoresModelsAndKeepsBindings()
        {
            var scope = new TestGameScope(1, [(1, 0)], 20, 25);
            var equipment = new EquipmentSystem(scope);
            var staff = equipment.Roll(Guid.NewGuid(), EEquipmentId.RoughPilgrimStaff, () => 0);
            scope.GetSystem<ProfileSystem>().TryCollectEquipment(staff);
            var snapshot = scope.CaptureSaveData();
            Assert.True(equipment.TryEquip(staff.InstanceId));
            scope.GetSystem<ProfileSystem>().TryCollectEquipment(equipment.Roll(Guid.NewGuid(), EEquipmentId.CommonPilgrimStaff, () => 0));
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
            Assert.Equal(TestTables.Tables.TbHeroGrowthConfig.Get(1).Levels[0].BaseAttributes.Power, scope.Hero.Power.Value);
            Assert.Equal(1, scope.Hero.Level.Value);
            Assert.Same(progress, scope.Profile.Progress);
            Assert.Same(items, scope.Profile.EquipmentDatas);
            Assert.Same(slots, scope.Profile.Slots);
        }
    }
}
