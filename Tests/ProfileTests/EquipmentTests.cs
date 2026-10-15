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
    private static EquipmentSystem CreateEquipmentSystem(PlayerModel playerModel = null) => new(playerModel == null ? TestTables.Architecture : TestTables.ForPlayer(playerModel));

    /// <summary>系统只修改所属架构注册的玩家模型，装配快照不借用或替换模型。</summary>
    [Fact]
    public void RegisteredModel_EquipmentOperationsStayInTheirOwnScope()
    {
        PlayerModel first = new(1, [new HeroProgression(1, TestTables.Curve, 0)], new Wallet(0), new Inventory(25));
        PlayerModel second = new(1, [new HeroProgression(1, TestTables.Curve, 0)], new Wallet(0), new Inventory(25));
        var firstSystem = CreateEquipmentSystem(first);
        var secondSystem = CreateEquipmentSystem(second);
        var weapon = firstSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        first.Inventory.TryAdd(weapon);
        Assert.False(secondSystem.TryEquip(weapon.InstanceId));
        Assert.True(firstSystem.TryEquip(weapon.InstanceId));
        var snapshot = firstSystem.CreateLoadout();
        Assert.Same(weapon, snapshot.Weapon);
        Assert.Null(secondSystem.CreateLoadout().Weapon);
        Assert.True(firstSystem.TryUnequip(EquipmentSlot.Weapon));
        Assert.Same(weapon, snapshot.Weapon);
        Assert.Empty(second.Inventory.Items);
        Assert.Null(second.ActiveEquipment.Weapon);
    }

    /// <summary>跨三页按品质出售，穿戴物和其他品质不参与，观察者只看到完整交易。</summary>
    [Fact]
    public void BulkSale_SellsAllPagesAndNotWornEquipment_WithOneNotification()
    {
        PlayerModel playerModel = new(1, [new HeroProgression(1, TestTables.Curve, 0)], new Wallet(7), new Inventory(75));
        var equipmentSystem = CreateEquipmentSystem(playerModel);
        var worn = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        playerModel.Inventory.TryAdd(worn);
        Assert.True(equipmentSystem.TryEquip(worn.InstanceId));
        for (int i = 0; i < 75; i++)
            playerModel.Inventory.TryAdd(equipmentSystem.Roll(Guid.NewGuid(), i % 3 == 0
                ? EquipmentId.CommonPineStaff : EquipmentId.RoughPilgrimStaff, () => 0));
        var retained = playerModel.Inventory.Items.Where(x => x.EquipmentId == EquipmentId.CommonPineStaff).ToArray();
        int inventoryNotifications = 0;
        int walletNotifications = 0;
        playerModel.Inventory.Changed += Observe;
        playerModel.Wallet.Changed += () =>
        {
            walletNotifications++;
            ObserveCommittedState();
        };

        Assert.Equal(50, equipmentSystem.SellByQuality(EquipmentQuality.Rough));
        Assert.Equal(1, inventoryNotifications);
        Assert.Equal(1, walletNotifications);
        Assert.Same(worn, playerModel.ActiveEquipment.Weapon);
        Assert.Equal(0, equipmentSystem.SellByQuality(EquipmentQuality.Rough));
        Assert.Equal(0, equipmentSystem.SellByQuality(EquipmentQuality.Excellent));
        Assert.Equal(0, equipmentSystem.SellByQuality((EquipmentQuality)99));
        Assert.Equal(1, inventoryNotifications);
        Assert.Equal(1, walletNotifications);

        void Observe()
        {
            inventoryNotifications++;
            ObserveCommittedState();
        }
        void ObserveCommittedState()
        {
            Assert.Equal(retained, playerModel.Inventory.Items);
            Assert.Equal(507, playerModel.Wallet.Soul);
        }
    }

    /// <summary>整批价格超过灵魂余额上限时，一件也不卖且不发送变更。</summary>
    [Fact]
    public void BulkSale_OverflowLeavesEntireTransactionUnchanged()
    {
        PlayerModel playerModel = new(1, [new HeroProgression(1, TestTables.Curve, 0)],
            new Wallet(int.MaxValue - 15), new Inventory(75));
        var equipmentSystem = CreateEquipmentSystem(playerModel);
        for (int i = 0; i < 2; i++)
            playerModel.Inventory.TryAdd(equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0));
        var before = playerModel.Inventory.Items.ToArray();
        int notifications = 0;
        playerModel.Inventory.Changed += () => notifications++;
        playerModel.Wallet.Changed += () => notifications++;
        Assert.Equal(0, equipmentSystem.SellByQuality(EquipmentQuality.Rough));
        Assert.Equal(before, playerModel.Inventory.Items);
        Assert.Equal(int.MaxValue - 15, playerModel.Wallet.Soul);
        Assert.Equal(0, notifications);
    }

    /// <summary>出售只消费背包实例一次，入账表内价格，溢出或已穿戴时保持完整状态。</summary>
    [Fact]
    public void Selling_UsesConfiguredPriceAndRejectsStaleWornOrOverflow()
    {
        PlayerModel playerModel = new(1, [new HeroProgression(1, TestTables.Curve, 0)], new Wallet(0), new Inventory(25));
        var equipmentSystem = CreateEquipmentSystem(playerModel);
        var rough = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        var common = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.CommonPineStaff, () => 0);
        playerModel.Inventory.TryAdd(rough);
        playerModel.Inventory.TryAdd(common);
        Assert.True(equipmentSystem.TryEquip(rough.InstanceId));
        Assert.False(equipmentSystem.TrySell(rough.InstanceId));
        Assert.True(equipmentSystem.TrySell(common.InstanceId));
        Assert.Equal(20, playerModel.Wallet.Soul);
        Assert.False(equipmentSystem.TrySell(common.InstanceId));
        Assert.True(equipmentSystem.TryUnequip(EquipmentSlot.Weapon));
        playerModel.Wallet.Restore(int.MaxValue - 9);
        Assert.False(equipmentSystem.TrySell(rough.InstanceId));
        Assert.Same(rough, Assert.Single(playerModel.Inventory.Items));
        Assert.Equal(int.MaxValue - 9, playerModel.Wallet.Soul);
        playerModel.Wallet.Restore(0);
        Assert.True(equipmentSystem.TrySell(rough.InstanceId));
        Assert.Equal(10, playerModel.Wallet.Soul);
        Assert.Empty(playerModel.Inventory.Items);
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
        Assert.Equal(EquipmentDropPoolId.None, boss.EquipmentDropPoolOverride);
    }

    /// <summary>满包和重复领取没有副作用，坏快照不能覆盖现有背包。</summary>
    [Fact]
    public void Inventory_IsAtomicAndRejectsDuplicateOrFullPickup()
    {
        Inventory bag = new(1);
        int changes = 0;
        bag.Changed += () => changes++;
        var item = CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.CommonPineStaff, () => .5f);
        Assert.True(bag.TryAdd(item));
        Assert.False(bag.TryAdd(item));
        Assert.False(bag.TryAdd(CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.RoughCassock, () => 0)));
        Assert.Throws<ArgumentException>(() => bag.Restore(new[] { item, item }));
        Assert.Same(item, Assert.Single(bag.Items));
        Assert.Equal(1, changes);
    }

    /// <summary>保存实际属性及稳定顺序，JSON 读回和关卡回滚都不重抽。</summary>
    [Fact]
    public void Equipment_RoundTripsAndRollsBackWithoutRerolling()
    {
        PlayerModel playerModel = new(1, new[] { new HeroProgression(1, TestTables.Curve, 0) }, new Wallet(0), new Inventory(25));
        var first = CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimClothes, () => .73f);
        playerModel.Inventory.TryAdd(first);
        var snapshot = playerModel.CaptureSaveData();
        playerModel.Inventory.TryAdd(CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.CommonPineStaff, () => .2f));
        var json = JsonConvert.SerializeObject(snapshot);
        var restored = PlayerModel.FromSaveData(JsonConvert.DeserializeObject<GameData>(json),
            TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
        var actual = Assert.Single(restored.Inventory.Items);
        Assert.Equal(first.InstanceId, actual.InstanceId);
        Assert.Equal(first.Attributes, actual.Attributes);
        var bag = playerModel.Inventory;
        playerModel.RestoreCheckpoint(snapshot);
        Assert.Same(bag, playerModel.Inventory);
        Assert.Equal(first.InstanceId, Assert.Single(bag.Items).InstanceId);
        snapshot.EquipmentItems[0].Attributes = first.Attributes with { MaxHp = 999 };
        Assert.Throws<ArgumentException>(() => PlayerModel.FromSaveData(snapshot, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
        Assert.Equal(35, first.Attributes.MaxHp);
        var missing = Newtonsoft.Json.Linq.JObject.Parse(json);
        ((Newtonsoft.Json.Linq.JObject)missing["EquipmentItems"][0]["Attributes"]).Remove("MaxHp");
        Assert.Throws<JsonSerializationException>(() => missing.ToObject<GameData>());
    }

    /// <summary>穿戴交换保持实例所有权，异角色和满包卸装均不改变状态。</summary>
    [Fact]
    public void Wearing_ExchangesItemsAtomicallyAndRejectsWrongHeroOrFullBag()
    {
        PlayerModel playerModel = new(1, [new HeroProgression(1, TestTables.Curve, 0)], new Wallet(0), new Inventory(1));
        var equipmentSystem = CreateEquipmentSystem(playerModel);
        var rough = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        var common = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimStaff, () => .999999f);
        Assert.True(playerModel.Inventory.TryAdd(rough));
        Assert.True(equipmentSystem.TryEquip(rough.InstanceId));
        Assert.Same(rough, playerModel.ActiveEquipment.Weapon);
        Assert.Empty(playerModel.Inventory.Items);
        Assert.True(playerModel.Inventory.TryAdd(common));
        Assert.True(equipmentSystem.TryEquip(common.InstanceId));
        Assert.Same(common, playerModel.ActiveEquipment.Weapon);
        Assert.Same(rough, Assert.Single(playerModel.Inventory.Items));
        Assert.False(equipmentSystem.TryUnequip(EquipmentSlot.Weapon));
        Assert.Same(common, playerModel.ActiveEquipment.Weapon);
        var tang = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPineStaff, () => 0);
        playerModel.Inventory.Restore([tang]);
        Assert.False(equipmentSystem.TryEquip(tang.InstanceId));
        Assert.Same(common, playerModel.ActiveEquipment.Weapon);
        Assert.Same(tang, Assert.Single(playerModel.Inventory.Items));
        playerModel.Inventory.Restore([]);
        Assert.True(equipmentSystem.TryUnequip(EquipmentSlot.Weapon));
        Assert.Null(playerModel.ActiveEquipment.Weapon);
        Assert.Same(common, Assert.Single(playerModel.Inventory.Items));
    }

    /// <summary>具名属性和穿戴引用经过 JSON、检查点回滚后完全保留，字段顺序不影响读档。</summary>
    [Fact]
    public void Wearing_RoundTripsNamedAttributesAndRestoresCheckpoint()
    {
        PlayerModel playerModel = new(1, [new HeroProgression(1, TestTables.Curve, 0)], new Wallet(0), new Inventory(25));
        var equipmentSystem = CreateEquipmentSystem(playerModel);
        var armor = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimClothes, () => .5f);
        playerModel.Inventory.TryAdd(armor);
        equipmentSystem.TryEquip(armor.InstanceId);
        var snapshot = playerModel.CaptureSaveData();
        var json = Newtonsoft.Json.Linq.JObject.FromObject(snapshot);
        var attrs = (Newtonsoft.Json.Linq.JObject)json["EquipmentItems"][0]["Attributes"];
        json["EquipmentItems"][0]["Attributes"] = new Newtonsoft.Json.Linq.JObject(attrs.Properties().Reverse());
        var restored = PlayerModel.FromSaveData(json.ToObject<GameData>(), TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
        Assert.Empty(restored.Inventory.Items);
        Assert.Equal(armor.Attributes, restored.ActiveEquipment.Armor.Attributes);
        Assert.Equal((30, 30, 3), (restored.ActiveEquipment.Armor.Attributes.MaxHp,
            restored.ActiveEquipment.Armor.Attributes.MaxMp, restored.ActiveEquipment.Armor.Attributes.Def));
        Assert.True(equipmentSystem.TryUnequip(EquipmentSlot.Armor));
        playerModel.RestoreCheckpoint(snapshot);
        Assert.Empty(playerModel.Inventory.Items);
        Assert.Equal(armor.InstanceId, playerModel.ActiveEquipment.Armor.InstanceId);
        var frozen = equipmentSystem.CreateLoadout();
        Assert.True(equipmentSystem.TryUnequip(EquipmentSlot.Armor));
        Assert.Equal(armor.InstanceId, frozen.Armor.InstanceId);
        Assert.Equal(armor.Attributes, frozen.Armor.Attributes);
    }

    /// <summary>错误部位、跨角色引用和一物多穿都被正式读档拒绝。</summary>
    [Fact]
    public void Wearing_RejectsInvalidSavedReferences()
    {
        PlayerModel playerModel = new(1, [new HeroProgression(1, TestTables.Curve, 0)], new Wallet(0), new Inventory(25));
        var item = CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        playerModel.Inventory.TryAdd(item);
        var data = playerModel.CaptureSaveData();
        data.Heroes[0].ArmorInstanceId = item.InstanceId;
        Assert.Throws<ArgumentException>(() => PlayerModel.FromSaveData(data, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
        data.Heroes[0].WeaponInstanceId = item.InstanceId;
        Assert.Throws<ArgumentException>(() => PlayerModel.FromSaveData(data, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
        data.Heroes[0].ArmorInstanceId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => PlayerModel.FromSaveData(data, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
    }
}
