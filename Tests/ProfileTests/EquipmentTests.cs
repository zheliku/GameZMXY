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
    private static EquipmentModule Module() => new(TestTables.Tables.TbHeroGrowthConfig, TestTables.Tables.TbEquipmentConfig);

    /// <summary>跨三页按品质出售，穿戴物和其他品质不参与，观察者只看到完整交易。</summary>
    [Fact]
    public void BulkSale_SellsAllPagesAndNotWornEquipment_WithOneNotification()
    {
        var module = Module();
        PlayerProfile profile = new(1, [new HeroProgression(1, TestTables.Curve, 0)], new Wallet(7), new Inventory(75));
        var worn = module.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        profile.Inventory.TryAdd(worn);
        Assert.True(module.TryEquip(profile, worn.InstanceId));
        for (int i = 0; i < 75; i++)
            profile.Inventory.TryAdd(module.Roll(Guid.NewGuid(), i % 3 == 0
                ? EquipmentId.CommonPineStaff : EquipmentId.RoughPilgrimStaff, () => 0));
        var retained = profile.Inventory.Items.Where(x => x.EquipmentId == EquipmentId.CommonPineStaff).ToArray();
        int inventoryNotifications = 0;
        int walletNotifications = 0;
        profile.Inventory.Changed += Observe;
        profile.Wallet.Changed += () =>
        {
            walletNotifications++;
            ObserveCommittedState();
        };

        Assert.Equal(50, module.SellByQuality(profile, EquipmentQuality.Rough));
        Assert.Equal(1, inventoryNotifications);
        Assert.Equal(1, walletNotifications);
        Assert.Same(worn, profile.ActiveEquipment.Weapon);
        Assert.Equal(0, module.SellByQuality(profile, EquipmentQuality.Rough));
        Assert.Equal(0, module.SellByQuality(profile, EquipmentQuality.Excellent));
        Assert.Equal(0, module.SellByQuality(profile, (EquipmentQuality)99));
        Assert.Equal(1, inventoryNotifications);
        Assert.Equal(1, walletNotifications);

        void Observe()
        {
            inventoryNotifications++;
            ObserveCommittedState();
        }
        void ObserveCommittedState()
        {
            Assert.Equal(retained, profile.Inventory.Items);
            Assert.Equal(507, profile.Wallet.Soul);
        }
    }

    /// <summary>整批价格超过灵魂余额上限时，一件也不卖且不发送变更。</summary>
    [Fact]
    public void BulkSale_OverflowLeavesEntireTransactionUnchanged()
    {
        var module = Module();
        PlayerProfile profile = new(1, [new HeroProgression(1, TestTables.Curve, 0)],
            new Wallet(int.MaxValue - 15), new Inventory(75));
        for (int i = 0; i < 2; i++)
            profile.Inventory.TryAdd(module.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0));
        var before = profile.Inventory.Items.ToArray();
        int notifications = 0;
        profile.Inventory.Changed += () => notifications++;
        profile.Wallet.Changed += () => notifications++;
        Assert.Equal(0, module.SellByQuality(profile, EquipmentQuality.Rough));
        Assert.Equal(before, profile.Inventory.Items);
        Assert.Equal(int.MaxValue - 15, profile.Wallet.Soul);
        Assert.Equal(0, notifications);
    }

    /// <summary>出售只消费背包实例一次，入账表内价格，溢出或已穿戴时保持完整状态。</summary>
    [Fact]
    public void Selling_UsesConfiguredPriceAndRejectsStaleWornOrOverflow()
    {
        PlayerProfile profile = new(1, [new HeroProgression(1, TestTables.Curve, 0)], new Wallet(0), new Inventory(25));
        var module = Module();
        var rough = module.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        var common = module.Roll(Guid.NewGuid(), EquipmentId.CommonPineStaff, () => 0);
        profile.Inventory.TryAdd(rough);
        profile.Inventory.TryAdd(common);
        Assert.True(module.TryEquip(profile, rough.InstanceId));
        Assert.False(module.TrySell(profile, rough.InstanceId));
        Assert.True(module.TrySell(profile, common.InstanceId));
        Assert.Equal(20, profile.Wallet.Soul);
        Assert.False(module.TrySell(profile, common.InstanceId));
        Assert.True(module.TryUnequip(profile, EquipmentSlot.Weapon));
        profile.Wallet.Restore(int.MaxValue - 9);
        Assert.False(module.TrySell(profile, rough.InstanceId));
        Assert.Same(rough, Assert.Single(profile.Inventory.Items));
        Assert.Equal(int.MaxValue - 9, profile.Wallet.Soul);
        profile.Wallet.Restore(0);
        Assert.True(module.TrySell(profile, rough.InstanceId));
        Assert.Equal(10, profile.Wallet.Soul);
        Assert.Empty(profile.Inventory.Items);
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
        var low = Module().Roll(Guid.NewGuid(), id, () => 0);
        var high = Module().Roll(Guid.NewGuid(), id, () => .999999f);
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
        var values = Enumerable.Range(0, 600).Select(i => Module().Roll(Guid.NewGuid(),
            EquipmentId.RoughPilgrimStaff, () => (i + .5f) / 600).Attributes.Power).ToArray();
        Assert.Equal(new[] { 100, 200, 200, 100 }, new[] { 2f, 3f, 4f, 5f }.Select(x => values.Count(v => v == x)));
        Queue<float> random = new(new[] { 0f, .999999f });
        var armor = Module().Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimClothes, random.Dequeue);
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
            LootHelper.RollGround(pickups, 0, null, () => (i + .5f) / 10000).Pickup).ToArray();
        Assert.Equal(new[] { 375, 125, 500, 9000 }, new[] { PickupId.SmallPeach, PickupId.LargePeach,
            PickupId.ManaBottle, PickupId.None }.Select(x => results.Count(v => v == x)));
        var equipment = Enumerable.Range(0, 600).Select(i =>
        {
            Queue<float> samples = new(new[] { 0f, 0f, (i + .5f) / 600 });
            var result = LootHelper.RollGround(pickups, .15f, pool, samples.Dequeue);
            Assert.Equal(PickupId.SmallPeach, result.Pickup);
            return result.Equipment;
        }).ToArray();
        Assert.Equal(new[] { 100, 200, 200, 100 }, pool.Entries.Select(x => equipment.Count(v => v == x.EquipmentId)));
        Queue<float> upperEndpoint = new(new[] { 0f, 0f, 1f });
        Assert.Equal(pool.Entries.Last().EquipmentId,
            LootHelper.RollGround(pickups, .15f, pool, upperEndpoint.Dequeue).Equipment);
        Queue<float> noGear = new(new[] { 0f, .15f });
        Assert.Equal(EquipmentId.None, LootHelper.RollGround(pickups, .15f, pool, noGear.Dequeue).Equipment);
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
        var item = Module().Roll(Guid.NewGuid(), EquipmentId.CommonPineStaff, () => .5f);
        Assert.True(bag.TryAdd(item));
        Assert.False(bag.TryAdd(item));
        Assert.False(bag.TryAdd(Module().Roll(Guid.NewGuid(), EquipmentId.RoughCassock, () => 0)));
        Assert.Throws<ArgumentException>(() => bag.Restore(new[] { item, item }));
        Assert.Same(item, Assert.Single(bag.Items));
        Assert.Equal(1, changes);
    }

    /// <summary>保存实际属性及稳定顺序，JSON 读回和关卡回滚都不重抽。</summary>
    [Fact]
    public void Equipment_RoundTripsAndRollsBackWithoutRerolling()
    {
        PlayerProfile profile = new(1, new[] { new HeroProgression(1, TestTables.Curve, 0) }, new Wallet(0), new Inventory(25));
        var first = Module().Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimClothes, () => .73f);
        profile.Inventory.TryAdd(first);
        var snapshot = ProfileSaveHelper.CaptureSaveData(profile);
        profile.Inventory.TryAdd(Module().Roll(Guid.NewGuid(), EquipmentId.CommonPineStaff, () => .2f));
        var json = JsonConvert.SerializeObject(snapshot);
        var restored = ProfileSaveHelper.RestoreProfile(JsonConvert.DeserializeObject<GameData>(json),
            TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
        var actual = Assert.Single(restored.Inventory.Items);
        Assert.Equal(first.InstanceId, actual.InstanceId);
        Assert.Equal(first.Attributes, actual.Attributes);
        var bag = profile.Inventory;
        ProfileSaveHelper.RestoreCheckpoint(profile, snapshot);
        Assert.Same(bag, profile.Inventory);
        Assert.Equal(first.InstanceId, Assert.Single(bag.Items).InstanceId);
        snapshot.EquipmentItems[0].Attributes = first.Attributes with { MaxHp = 999 };
        Assert.Throws<ArgumentException>(() => ProfileSaveHelper.RestoreProfile(snapshot, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
        Assert.Equal(35, first.Attributes.MaxHp);
        var missing = Newtonsoft.Json.Linq.JObject.Parse(json);
        ((Newtonsoft.Json.Linq.JObject)missing["EquipmentItems"][0]["Attributes"]).Remove("MaxHp");
        Assert.Throws<JsonSerializationException>(() => missing.ToObject<GameData>());
    }

    /// <summary>穿戴交换保持实例所有权，异角色和满包卸装均不改变状态。</summary>
    [Fact]
    public void Wearing_ExchangesItemsAtomicallyAndRejectsWrongHeroOrFullBag()
    {
        PlayerProfile profile = new(1, [new HeroProgression(1, TestTables.Curve, 0)], new Wallet(0), new Inventory(1));
        var module = Module();
        var rough = module.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        var common = module.Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimStaff, () => .999999f);
        Assert.True(profile.Inventory.TryAdd(rough));
        Assert.True(module.TryEquip(profile, rough.InstanceId));
        Assert.Same(rough, profile.ActiveEquipment.Weapon);
        Assert.Empty(profile.Inventory.Items);
        Assert.True(profile.Inventory.TryAdd(common));
        Assert.True(module.TryEquip(profile, common.InstanceId));
        Assert.Same(common, profile.ActiveEquipment.Weapon);
        Assert.Same(rough, Assert.Single(profile.Inventory.Items));
        Assert.False(module.TryUnequip(profile, EquipmentSlot.Weapon));
        Assert.Same(common, profile.ActiveEquipment.Weapon);
        var tang = module.Roll(Guid.NewGuid(), EquipmentId.RoughPineStaff, () => 0);
        profile.Inventory.Restore([tang]);
        Assert.False(module.TryEquip(profile, tang.InstanceId));
        Assert.Same(common, profile.ActiveEquipment.Weapon);
        Assert.Same(tang, Assert.Single(profile.Inventory.Items));
        profile.Inventory.Restore([]);
        Assert.True(module.TryUnequip(profile, EquipmentSlot.Weapon));
        Assert.Null(profile.ActiveEquipment.Weapon);
        Assert.Same(common, Assert.Single(profile.Inventory.Items));
    }

    /// <summary>具名属性和穿戴引用经过 JSON、检查点回滚后完全保留，字段顺序不影响读档。</summary>
    [Fact]
    public void Wearing_RoundTripsNamedAttributesAndRestoresCheckpoint()
    {
        PlayerProfile profile = new(1, [new HeroProgression(1, TestTables.Curve, 0)], new Wallet(0), new Inventory(25));
        var module = Module();
        var armor = module.Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimClothes, () => .5f);
        profile.Inventory.TryAdd(armor);
        module.TryEquip(profile, armor.InstanceId);
        var snapshot = ProfileSaveHelper.CaptureSaveData(profile);
        var json = Newtonsoft.Json.Linq.JObject.FromObject(snapshot);
        var attrs = (Newtonsoft.Json.Linq.JObject)json["EquipmentItems"][0]["Attributes"];
        json["EquipmentItems"][0]["Attributes"] = new Newtonsoft.Json.Linq.JObject(attrs.Properties().Reverse());
        var restored = ProfileSaveHelper.RestoreProfile(json.ToObject<GameData>(), TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
        Assert.Empty(restored.Inventory.Items);
        Assert.Equal(armor.Attributes, restored.ActiveEquipment.Armor.Attributes);
        Assert.Equal((30, 30, 3), (restored.ActiveEquipment.Armor.Attributes.MaxHp,
            restored.ActiveEquipment.Armor.Attributes.MaxMp, restored.ActiveEquipment.Armor.Attributes.Def));
        Assert.True(module.TryUnequip(profile, EquipmentSlot.Armor));
        ProfileSaveHelper.RestoreCheckpoint(profile, snapshot);
        Assert.Empty(profile.Inventory.Items);
        Assert.Equal(armor.InstanceId, profile.ActiveEquipment.Armor.InstanceId);
        var frozen = module.CreateLoadout(profile);
        Assert.True(module.TryUnequip(profile, EquipmentSlot.Armor));
        Assert.Equal(armor.InstanceId, frozen.Armor.InstanceId);
        Assert.Equal(armor.Attributes, frozen.Armor.Attributes);
    }

    /// <summary>错误部位、跨角色引用和一物多穿都被正式读档拒绝。</summary>
    [Fact]
    public void Wearing_RejectsInvalidSavedReferences()
    {
        PlayerProfile profile = new(1, [new HeroProgression(1, TestTables.Curve, 0)], new Wallet(0), new Inventory(25));
        var item = Module().Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        profile.Inventory.TryAdd(item);
        var data = ProfileSaveHelper.CaptureSaveData(profile);
        data.Heroes[0].ArmorInstanceId = item.InstanceId;
        Assert.Throws<ArgumentException>(() => ProfileSaveHelper.RestoreProfile(data, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
        data.Heroes[0].WeaponInstanceId = item.InstanceId;
        Assert.Throws<ArgumentException>(() => ProfileSaveHelper.RestoreProfile(data, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
        data.Heroes[0].ArmorInstanceId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => ProfileSaveHelper.RestoreProfile(data, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
    }
}

