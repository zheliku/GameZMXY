using GameLogic.Architecture;
using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Equipment;
using GameConfig.Loot;
using GameConfig.Attributes;
using GameLogic.Drops;
using GameLogic.Equipment;
using GameLogic.Save.Data;
using Newtonsoft.Json;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>真实配置的装备随机、拾取容量与存档实例回归。</summary>
public sealed class EquipmentTests
{
    private static EquipmentSystem CreateEquipmentSystem(TestGameScope playerModel = null) => new(playerModel == null ? TestTables.Architecture : TestTables.ForPlayer(playerModel));

    /// <summary>系统只修改所属架构注册的玩家模型，装配快照不借用或替换模型。</summary>
    [Fact]
    public void RegisteredModel_EquipmentOperationsStayInTheirOwnScope()
    {
        TestGameScope first = new(1, [(1, 0)], 0, 25);
        TestGameScope second = new(1, [(1, 0)], 0, 25);
        var firstSystem = CreateEquipmentSystem(first);
        var secondSystem = CreateEquipmentSystem(second);
        var weapon = firstSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        first.GetSystem<ProfileSystem>().TryCollectEquipment(weapon);
        Assert.False(secondSystem.TryEquip(weapon.InstanceId));
        Assert.True(firstSystem.TryEquip(weapon.InstanceId));
        var snapshot = first.Hero.CaptureCombatAttributes();
        Assert.Same(weapon, first.Profile.Weapon);
        Assert.Null(second.Profile.Weapon);
        Assert.True(firstSystem.TryUnequip(EquipmentSlot.Weapon));
        Assert.Equal(first.Hero.Power.Value + weapon.Attributes.Power, snapshot.Power);
        Assert.Empty(second.Profile.EquipmentDatas);
        Assert.Null(second.Profile.Weapon);
    }

    /// <summary>跨三页按品质出售，穿戴物和其他品质不参与，各数据源只通知一次。</summary>
    [Fact]
    public void BulkSale_SellsAllPagesAndNotWornEquipment_WithOneNotification()
    {
        TestGameScope playerModel = new(1, [(1, 0)], 7, 75);
        var equipmentSystem = CreateEquipmentSystem(playerModel);
        var worn = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(worn);
        Assert.True(equipmentSystem.TryEquip(worn.InstanceId));
        for (int i = 0; i < 75; i++)
            playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(equipmentSystem.Roll(Guid.NewGuid(), i % 3 == 0
                ? EquipmentId.CommonPineStaff : EquipmentId.RoughPilgrimStaff, () => 0));
        var retained = playerModel.Profile.EquipmentDatas.Where(x => x.EquipmentId == EquipmentId.CommonPineStaff).ToArray();
        int inventoryNotifications = 0;
        int walletNotifications = 0;
        playerModel.Profile.EquipmentDatas.Register(_ => Observe());
        playerModel.Profile.Soul.Register(_ =>
        {
            walletNotifications++;
            ObserveCommittedState();
        });

        Assert.Equal(50, equipmentSystem.SellByQuality(EquipmentQuality.Rough));
        Assert.Equal(1, inventoryNotifications);
        Assert.Equal(1, walletNotifications);
        Assert.Same(worn, playerModel.Profile.Weapon);
        Assert.Equal(0, equipmentSystem.SellByQuality(EquipmentQuality.Rough));
        Assert.Equal(0, equipmentSystem.SellByQuality(EquipmentQuality.Excellent));
        Assert.Equal(0, equipmentSystem.SellByQuality((EquipmentQuality)99));
        Assert.Equal(1, inventoryNotifications);
        Assert.Equal(1, walletNotifications);

        void Observe()
        {
            inventoryNotifications++;
            Assert.Equal(retained, playerModel.Profile.EquipmentDatas);
        }
        void ObserveCommittedState()
        {
            Assert.Equal(retained, playerModel.Profile.EquipmentDatas);
            Assert.Equal(507, playerModel.Profile.Soul.Value);
        }
    }

    /// <summary>整批价格超过灵魂余额上限时，一件也不卖且不发送变更。</summary>
    [Fact]
    public void BulkSale_OverflowLeavesEntireTransactionUnchanged()
    {
        TestGameScope playerModel = new(1, [(1, 0)],
            int.MaxValue - 15, 75);
        var equipmentSystem = CreateEquipmentSystem(playerModel);
        for (int i = 0; i < 2; i++)
            playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0));
        var before = playerModel.Profile.EquipmentDatas.ToArray();
        int notifications = 0;
        playerModel.Profile.EquipmentDatas.Register(_ => notifications++);
        playerModel.Profile.Soul.Register(_ => notifications++);
        Assert.Equal(0, equipmentSystem.SellByQuality(EquipmentQuality.Rough));
        Assert.Equal(before, playerModel.Profile.EquipmentDatas);
        Assert.Equal(int.MaxValue - 15, playerModel.Profile.Soul.Value);
        Assert.Equal(0, notifications);
    }

    /// <summary>出售只消费背包实例一次，入账表内价格，溢出或已穿戴时保持完整状态。</summary>
    [Fact]
    public void Selling_UsesConfiguredPriceAndRejectsStaleWornOrOverflow()
    {
        TestGameScope playerModel = new(1, [(1, 0)], 0, 25);
        var equipmentSystem = CreateEquipmentSystem(playerModel);
        var rough = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        var common = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.CommonPineStaff, () => 0);
        playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(rough);
        playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(common);
        Assert.True(equipmentSystem.TryEquip(rough.InstanceId));
        Assert.False(equipmentSystem.TrySell(rough.InstanceId));
        Assert.True(equipmentSystem.TrySell(common.InstanceId));
        Assert.Equal(20, playerModel.Profile.Soul.Value);
        Assert.False(equipmentSystem.TrySell(common.InstanceId));
        Assert.True(equipmentSystem.TryUnequip(EquipmentSlot.Weapon));
        playerModel.Profile.RestoreSoul(int.MaxValue - 9);
        Assert.False(equipmentSystem.TrySell(rough.InstanceId));
        Assert.Same(rough, Assert.Single(playerModel.Profile.EquipmentDatas));
        Assert.Equal(int.MaxValue - 9, playerModel.Profile.Soul.Value);
        playerModel.Profile.RestoreSoul(0);
        Assert.True(equipmentSystem.TrySell(rough.InstanceId));
        Assert.Equal(10, playerModel.Profile.Soul.Value);
        Assert.Empty(playerModel.Profile.EquipmentDatas);
    }

    /// <summary>八件初级装备的每条实际属性在两端精确匹配原版范围。</summary>
    [Theory]
    [InlineData(EquipmentId.RoughPilgrimClothes, 1, 1)]
    [InlineData(EquipmentId.RoughPilgrimStaff, 2, 5)]
    [InlineData(EquipmentId.RoughCassock, 1, 1)]
    [InlineData(EquipmentId.RoughPineStaff, 2, 5)]
    [InlineData(EquipmentId.CommonPilgrimClothes, 20, 40)]
    [InlineData(EquipmentId.CommonPilgrimStaff, 5, 15)]
    [InlineData(EquipmentId.CommonCassock, 20, 40)]
    [InlineData(EquipmentId.CommonPineStaff, 8, 16)]
    public void RandomAttributes_MatchOfficialEndpoints(EquipmentId id, int minimum, int maximum)
    {
        var low = CreateEquipmentSystem().Roll(Guid.NewGuid(), id, () => 0);
        var high = CreateEquipmentSystem().Roll(Guid.NewGuid(), id, () => .999999f);
        var definition = TestTables.Tables.TbEquipmentConfig.Get(id);
        Assert.Equal(minimum, low.Attributes.Get(definition.AttributeRanges[0].Attribute));
        Assert.Equal(maximum, high.Attributes.Get(definition.AttributeRanges[0].Attribute));
        Assert.Equal(definition.AttributeRanges.Select(x => (float)x.Minimum), definition.AttributeRanges.Select(x => (float)low.Attributes.Get(x.Attribute)));
        Assert.Equal(definition.AttributeRanges.Select(x => (float)x.Maximum), definition.AttributeRanges.Select(x => (float)high.Attributes.Get(x.Attribute)));
        Assert.Equal(0, low.Attributes.Mdef);
    }

    /// <summary>保留 round 随机的端点半权重，生命与魔法分别抽取。</summary>
    [Fact]
    public void RandomAttributes_UseRoundDistributionAndIndependentRolls()
    {
        var values = Enumerable.Range(0, 600).Select(i => CreateEquipmentSystem().Roll(Guid.NewGuid(),
            EquipmentId.RoughPilgrimStaff, () => (i + .5f) / 600).Attributes.Power).ToArray();
        Assert.Equal(new[] { 100, 200, 200, 100 }, new[] { 2f, 3f, 4f, 5f }.Select(x => values.Count(v => v == x)));
        Queue<float> random = new(new[] { 0f, .999999f });
        var armor = CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimClothes, random.Dequeue);
        Assert.Equal((20, 40, 3), (armor.Attributes.MaxHp, armor.Attributes.MaxMp, armor.Attributes.Def));
        Assert.Empty(random);
    }

    /// <summary>一万组分层采样精确锁定恢复品概率、互斥关系与装备独立判定。</summary>
    [Fact]
    public void GroundSelection_MatchesOfficialProbabilitiesAndPoolWeights()
    {
        var pickups = TestTables.Tables.TbPickupConfig.DataList;
        var pool = TestTables.Tables.TbEquipmentDropPoolConfig.Get(EquipmentDropPoolId.Rough);
        var results = Enumerable.Range(0, 10000).Select(i =>
            new LootSystem(TestTables.Architecture).RollGround(0, EquipmentDropPoolId.None, () => (i + .5f) / 10000).Pickup).ToArray();
        Assert.Equal(new[] { 375, 125, 500, 9000 }, new[] { PickupId.SmallPeach, PickupId.LargePeach,
            PickupId.ManaBottle, PickupId.None }.Select(x => results.Count(v => v == x)));
        var equipment = Enumerable.Range(0, 600).Select(i =>
        {
            Queue<float> samples = new(new[] { 0f, 0f, (i + .5f) / 600 });
            var result = new LootSystem(TestTables.Architecture).RollGround(.15f, pool.Id, samples.Dequeue);
            Assert.Equal(PickupId.SmallPeach, result.Pickup);
            return result.Equipment;
        }).ToArray();
        Assert.Equal(new[] { 100, 200, 200, 100 }, pool.Entries.Select(x => equipment.Count(v => v == x.EquipmentId)));
        Queue<float> upperEndpoint = new(new[] { 0f, 0f, 1f });
        Assert.Equal(pool.Entries.Last().EquipmentId,
            new LootSystem(TestTables.Architecture).RollGround(.15f, pool.Id, upperEndpoint.Dequeue).Equipment);
        Queue<float> noGear = new(new[] { 0f, .15f });
        Assert.Equal(EquipmentId.None, new LootSystem(TestTables.Architecture).RollGround(.15f, pool.Id, noGear.Dequeue).Equipment);
        var boss = TestTables.Tables.TbLevelConfig.DataList.SelectMany(x => x.Stages).SelectMany(x => x.Recipes)
            .Single(x => x.ExperienceOverride == 25);
        var monster = TestTables.Tables.TbMonsterConfig.DataList.Single(x => x.EntityId == boss.MonsterEntityId);
        Assert.Equal(EquipmentDropPoolId.None, monster.EquipmentDropPoolId);
        Assert.Equal(EquipmentId.None,
            new LootSystem(TestTables.Architecture).RollGround(monster.EquipmentDropChance,
                monster.EquipmentDropPoolId, () => 0f).Equipment);
    }

    /// <summary>满包和重复领取没有副作用，坏快照不能覆盖现有背包。</summary>
    [Fact]
    public void Inventory_IsAtomicAndRejectsDuplicateOrFullPickup()
    {
        ProfileModel bag = new(1, [(1, 0)], TestTables.Curve, 1);
        int changes = 0;
        bag.EquipmentDatas.Register(_ => changes++);
        var item = CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.CommonPineStaff, () => .5f);
        Assert.True(TestTables.ForProfile(bag).TryCollectEquipment(item));
        Assert.False(TestTables.ForProfile(bag).TryCollectEquipment(item));
        Assert.False(TestTables.ForProfile(bag).TryCollectEquipment(CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.RoughCassock, () => 0)));
        var scope = new TestGameScope(1, [(1, 0)], 0, 1);
        var malformed = scope.CaptureSaveData();
        var data = new GameLogic.Save.Data.EquipmentSaveData
        {
            InstanceId = item.InstanceId,
            EquipmentId = item.EquipmentId,
            Attributes = item.Attributes
        };
        malformed.EquipmentItems.Add(data);
        malformed.EquipmentItems.Add(data);
        Assert.Throws<ArgumentException>(() => scope.RestoreCheckpoint(malformed));
        Assert.Empty(scope.Profile.EquipmentDatas);
        Assert.Same(item, Assert.Single(bag.EquipmentDatas));
        Assert.Equal(1, changes);
    }

    /// <summary>保存实际属性及稳定顺序，JSON 读回和关卡回滚都不重抽。</summary>
    [Fact]
    public void Equipment_RoundTripsAndRollsBackWithoutRerolling()
    {
        TestGameScope playerModel = new(1, new[] { (1, 0) }, 0, 25);
        var first = CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimClothes, () => .73f);
        playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(first);
        var snapshot = playerModel.CaptureSaveData();
        playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.CommonPineStaff, () => .2f));
        var json = JsonConvert.SerializeObject(snapshot);
        var restored = TestGameScope.FromSaveData(JsonConvert.DeserializeObject<GameData>(json),
            TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
        var actual = Assert.Single(restored.Profile.EquipmentDatas);
        Assert.Equal(first.InstanceId, actual.InstanceId);
        Assert.Equal(first.Attributes, actual.Attributes);
        var bag = playerModel.Profile;
        playerModel.RestoreCheckpoint(snapshot);
        Assert.Same(bag, playerModel.Profile);
        Assert.Equal(first.InstanceId, Assert.Single(bag.EquipmentDatas).InstanceId);
        snapshot.EquipmentItems[0].Attributes = first.Attributes with { MaxHp = 999 };
        Assert.Throws<ArgumentException>(() => TestGameScope.FromSaveData(snapshot, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
        Assert.Equal(35, first.Attributes.MaxHp);
        var missing = Newtonsoft.Json.Linq.JObject.Parse(json);
        ((Newtonsoft.Json.Linq.JObject)missing["EquipmentItems"][0]["Attributes"]).Remove("MaxHp");
        Assert.Throws<JsonSerializationException>(() => missing.ToObject<GameData>());
    }

    /// <summary>穿戴交换保持实例所有权，异角色和满包卸装均不改变状态。</summary>
    [Fact]
    public void Wearing_ExchangesItemsAtomicallyAndRejectsWrongHeroOrFullBag()
    {
        TestGameScope playerModel = new(1, [(1, 0)], 0, 1);
        var equipmentSystem = CreateEquipmentSystem(playerModel);
        var rough = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        var common = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimStaff, () => .999999f);
        Assert.True(playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(rough));
        Assert.True(equipmentSystem.TryEquip(rough.InstanceId));
        Assert.Same(rough, playerModel.Profile.Weapon);
        Assert.Empty(playerModel.Profile.EquipmentDatas);
        Assert.True(playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(common));
        Assert.True(equipmentSystem.TryEquip(common.InstanceId));
        Assert.Same(common, playerModel.Profile.Weapon);
        Assert.Same(rough, Assert.Single(playerModel.Profile.EquipmentDatas));
        Assert.False(equipmentSystem.TryUnequip(EquipmentSlot.Weapon));
        Assert.Same(common, playerModel.Profile.Weapon);
        var tang = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPineStaff, () => 0);
        playerModel.Profile.RestoreItems([tang]);
        Assert.False(equipmentSystem.TryEquip(tang.InstanceId));
        Assert.Same(common, playerModel.Profile.Weapon);
        Assert.Same(tang, Assert.Single(playerModel.Profile.EquipmentDatas));
        playerModel.Profile.RestoreItems([]);
        Assert.True(equipmentSystem.TryUnequip(EquipmentSlot.Weapon));
        Assert.Null(playerModel.Profile.Weapon);
        Assert.Same(common, Assert.Single(playerModel.Profile.EquipmentDatas));
    }

    /// <summary>具名属性和穿戴引用经过 JSON、检查点回滚后完全保留，字段顺序不影响读档。</summary>
    [Fact]
    public void Wearing_RoundTripsNamedAttributesAndRestoresCheckpoint()
    {
        TestGameScope playerModel = new(1, [(1, 0)], 0, 25);
        var equipmentSystem = CreateEquipmentSystem(playerModel);
        var armor = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimClothes, () => .5f);
        playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(armor);
        equipmentSystem.TryEquip(armor.InstanceId);
        var snapshot = playerModel.CaptureSaveData();
        var json = Newtonsoft.Json.Linq.JObject.FromObject(snapshot);
        var attrs = (Newtonsoft.Json.Linq.JObject)json["EquipmentItems"][0]["Attributes"];
        json["EquipmentItems"][0]["Attributes"] = new Newtonsoft.Json.Linq.JObject(attrs.Properties().Reverse());
        var restored = TestGameScope.FromSaveData(json.ToObject<GameData>(), TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
        Assert.Empty(restored.Profile.EquipmentDatas);
        Assert.Equal(armor.Attributes, restored.Profile.Armor.Attributes);
        Assert.Equal((30, 30, 3), (restored.Profile.Armor.Attributes.MaxHp,
            restored.Profile.Armor.Attributes.MaxMp, restored.Profile.Armor.Attributes.Def));
        Assert.True(equipmentSystem.TryUnequip(EquipmentSlot.Armor));
        playerModel.RestoreCheckpoint(snapshot);
        Assert.Empty(playerModel.Profile.EquipmentDatas);
        Assert.Equal(armor.InstanceId, playerModel.Profile.Armor.InstanceId);
        var frozen = playerModel.Hero.CaptureCombatAttributes();
        Assert.True(equipmentSystem.TryUnequip(EquipmentSlot.Armor));
        Assert.Null(playerModel.Profile.Armor);
        Assert.Equal(TestTables.Tables.TbHeroGrowthConfig.Get(1, 1).BaseAttributes.Def + armor.Attributes.Def, frozen.Def);
    }

    /// <summary>错误部位、跨角色引用和一物多穿都被正式读档拒绝。</summary>
    [Fact]
    public void Wearing_RejectsInvalidSavedReferences()
    {
        TestGameScope playerModel = new(1, [(1, 0)], 0, 25);
        var item = CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(item);
        var data = playerModel.CaptureSaveData();
        data.Heroes[0].ArmorInstanceId = item.InstanceId;
        Assert.Throws<ArgumentException>(() => TestGameScope.FromSaveData(data, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
        data.Heroes[0].WeaponInstanceId = item.InstanceId;
        Assert.Throws<ArgumentException>(() => TestGameScope.FromSaveData(data, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
        data.Heroes[0].ArmorInstanceId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => TestGameScope.FromSaveData(data, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
    }
}
