using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Attributes;
using GameConfig.Enemy;
using GameConfig.Equipment;
using GameConfig.Level;
using GameConfig.Loot;
using GameLogic.Architecture;
using GameLogic.Battle.Damage;
using GameLogic.Drops;
using GameLogic.Equipment;
using GameLogic.Save.Data;
using Newtonsoft.Json;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 真实配置的装备随机、拾取容量与存档实例回归。
/// </summary>
public sealed class EquipmentTests
{
    /// <summary>
    /// 背包容量和出售收益各归本人，同一实例不能跨背包重复领取或被另一人使用。
    /// </summary>
    [Fact]
    public void PersonalAssets_KeepCapacityOwnershipAndSaleProceedsSeparate()
    {
        var scope = new TestGameScope([(2, 0), (1, 0)], 10, 1);
        ProfileSystem profile = scope.GetSystem<ProfileSystem>();
        EquipmentSystem equipment = scope.GetSystem<EquipmentSystem>();
        EquipmentData first = equipment.Roll(Guid.NewGuid(), EquipmentId.EarthFiendRing, () => 0);
        EquipmentData second = equipment.Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimStaff, () => 0);

        // 各有一个格子；全档唯一归属检查不能退化为只检查接收方。
        Assert.True(profile.TryCollectEquipment(2, first));
        Assert.False(profile.TryCollectEquipment(1, first));
        Assert.True(profile.TryCollectEquipment(1, second));
        Assert.False(equipment.TryEquip(1, first.InstanceId));
        Assert.False(equipment.TrySell(1, first.InstanceId));
        Assert.NotSame(scope.Profile.GetBag(1), scope.Profile.GetBag(2));
        Assert.True(scope.Profile.HasEquipment(EquipmentId.EarthFiendRing));
        Assert.False(profile.TryCollectEquipment(2, equipment.Roll(Guid.NewGuid(), EquipmentId.DiamondSnare, () => 0)));

        Assert.True(equipment.TrySell(2, first.InstanceId));
        Assert.Equal(10 + TestTables.Tables.TbEquipmentConfig.Get(first.EquipmentId).SellPrice,
            scope.Profile.GetSoul(2).Value);
        Assert.Equal(10, scope.Profile.GetSoul(1).Value);
        Assert.Same(second, Assert.Single(scope.Profile.GetBag(1)));
        Assert.Empty(scope.Profile.GetBag(2));
    }

    /// <summary>
    /// 归属编号可排序保存，但两人的钱包、穿戴和原始背包格子顺序都必须往返保留。
    /// </summary>
    [Fact]
    public void PersonalAssets_RoundTripSortedReferencesWithoutReorderingBags()
    {
        var scope = new TestGameScope([(2, 0), (1, 0)], 10, 3);
        ProfileSystem profile = scope.GetSystem<ProfileSystem>();
        EquipmentSystem equipment = scope.GetSystem<EquipmentSystem>();
        EquipmentData high = equipment.Roll(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), EquipmentId.CommonPineStaff, () => .5f);
        EquipmentData low = equipment.Roll(Guid.Parse("00000000-0000-0000-0000-000000000001"), EquipmentId.CommonPineStaff, () => .2f);
        EquipmentData ring = equipment.Roll(Guid.NewGuid(), EquipmentId.EarthFiendRing, () => 0);
        Assert.True(profile.TryCollectEquipment(2, high));
        Assert.True(profile.TryCollectEquipment(2, low));
        Assert.True(profile.TryCollectEquipment(1, ring));
        Assert.True(equipment.TryEquip(1, ring.InstanceId));
        profile.AddSoul(2, 27);
        profile.AddSoul(1, 1);

        // 故意让格子顺序与编号顺序相反，验证归属列表不成为显示排序。
        GameData snapshot = scope.CaptureSaveData();
        Assert.Equal(new[] { low.InstanceId, high.InstanceId }, snapshot.Heroes[0].BagInstanceIds);
        Assert.Empty(snapshot.Heroes[1].BagInstanceIds);
        GameData loaded = JsonConvert.DeserializeObject<GameData>(JsonConvert.SerializeObject(snapshot));
        var restored = TestGameScope.FromSaveData(loaded, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 3);
        Assert.Equal(new[] { 2, 1 }, restored.Profile.HeroIds);
        Assert.Equal(new[] { high.InstanceId, low.InstanceId }, restored.Profile.GetBag(2).Select(item => item.InstanceId));
        Assert.Equal(high.Attributes, restored.Profile.GetBag(2)[0].Attributes);
        Assert.Equal(37, restored.Profile.GetSoul(2).Value);
        Assert.Equal(11, restored.Profile.GetSoul(1).Value);
        Assert.Empty(restored.Profile.GetBag(1));
        Assert.Equal(ring.InstanceId, restored.Profile.GetSlots(1).Value.Accessory.InstanceId);
    }

    /// <summary>
    /// 实例引用必须存在、唯一、完整且不超过各人的容量。
    /// </summary>
    /// <param name="invalidCase">模拟的外部存档错误类型。</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void PersonalAssets_RejectInvalidInstanceOwnership(int invalidCase)
    {
        var scope = new TestGameScope([(1, 0), (2, 0)], 0, 2);
        EquipmentSystem equipment = scope.GetSystem<EquipmentSystem>();
        ProfileSystem profile = scope.GetSystem<ProfileSystem>();
        EquipmentData first = equipment.Roll(Guid.NewGuid(), EquipmentId.EarthFiendRing, () => 0);
        EquipmentData second = equipment.Roll(Guid.NewGuid(), EquipmentId.EarthFiendRing, () => 0);
        Assert.True(profile.TryCollectEquipment(1, first));
        Assert.True(profile.TryCollectEquipment(2, second));
        GameData snapshot = scope.CaptureSaveData();
        int capacity = 2;
        switch (invalidCase)
        {
            case 0:
                snapshot.Heroes[0].BagInstanceIds.Add(first.InstanceId);
                break;
            case 1:
                snapshot.Heroes[1].BagInstanceIds.Add(first.InstanceId);
                break;
            case 2:
                snapshot.Heroes[0].AccessoryInstanceId = first.InstanceId;
                break;
            case 3:
                snapshot.Heroes[0].BagInstanceIds[0] = Guid.NewGuid();
                break;
            case 4:
                snapshot.Heroes[0].BagInstanceIds.Clear();
                break;
            case 5:
                snapshot.Heroes[1].BagInstanceIds.Clear();
                snapshot.Heroes[0].BagInstanceIds.Add(second.InstanceId);
                capacity = 1;
                break;
            case 6:
                snapshot.Heroes[1].AccessoryInstanceId = first.InstanceId;
                break;
        }

        Assert.Throws<ArgumentException>(() => GameLogic.Save.SaveDataUtility.ReadItems(snapshot, capacity));
    }

    /// <summary>
    /// 法宝作为独立部位穿戴，属性和实例引用均可保存、重建与卸下。
    /// </summary>
    /// <param name="id">当前可穿戴的法宝。</param>
    [Theory]
    [InlineData(EquipmentId.DiamondSnare)]
    [InlineData(EquipmentId.EastSeaBanner)]
    public void Treasure_CollectsEquipsAndSaves(EquipmentId id)
    {
        var scope = new TestGameScope([(1, 0)], 0, 25);
        EquipmentSystem system = CreateEquipmentSystem(scope);
        EquipmentData data = system.Roll(Guid.NewGuid(), id, () => .37f);
        Assert.Equal(EquipmentSlot.Treasure, TestTables.Tables.TbEquipmentConfig.Get(id).Slot);
        Assert.True(scope.GetSystem<ProfileSystem>().TryCollectEquipment(1, data));
        Assert.True(scope.Profile.HasEquipment(id));
        CombatantAttributes original = scope.Hero.CaptureCombatAttributes();
        Assert.True(system.TryEquip(1, data.InstanceId));
        Assert.Same(data, scope.Profile.GetSlots(scope.Profile.HeroOf(PlayerSlot.P1)).Value.Treasure);
        Assert.NotEqual(original, scope.Hero.CaptureCombatAttributes());
        GameData saved = JsonConvert.DeserializeObject<GameData>(JsonConvert.SerializeObject(scope.CaptureSaveData()));
        var restored = TestGameScope.FromSaveData(saved, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
        Assert.Empty(restored.Profile.GetBag(restored.Profile.HeroOf(PlayerSlot.P1)));
        EquipmentData actual = restored.Profile.GetSlots(restored.Profile.HeroOf(PlayerSlot.P1)).Value.Treasure;
        Assert.NotNull(actual);
        Assert.Equal(data.InstanceId, actual.InstanceId);
        Assert.Equal(data.Attributes, actual.Attributes);
        Assert.True(restored.Profile.HasEquipment(id));
        Assert.True(restored.GetSystem<EquipmentSystem>().TryUnequip(1, EquipmentSlot.Treasure));
        Assert.Null(restored.Profile.GetSlots(restored.Profile.HeroOf(PlayerSlot.P1)).Value.Treasure);
        Assert.Single(restored.Profile.GetBag(restored.Profile.HeroOf(PlayerSlot.P1)));
    }

    /// <summary>
    /// 其他部位穿戴、替换和卸下后，法宝及其存档引用仍保持原实例。
    /// </summary>
    /// <param name="treasureId">穿戴中的法宝定义。</param>
    /// <param name="equipmentId">执行穿卸的其他部位装备。</param>
    /// <param name="slot">本次变更的部位。</param>
    [Theory]
    [InlineData(EquipmentId.DiamondSnare, EquipmentId.CommonPilgrimStaff, EquipmentSlot.Weapon)]
    [InlineData(EquipmentId.DiamondSnare, EquipmentId.EarthFiendApeArmor, EquipmentSlot.Armor)]
    [InlineData(EquipmentId.DiamondSnare, EquipmentId.EarthFiendRing, EquipmentSlot.Accessory)]
    [InlineData(EquipmentId.EastSeaBanner, EquipmentId.CommonPilgrimStaff, EquipmentSlot.Weapon)]
    [InlineData(EquipmentId.EastSeaBanner, EquipmentId.EarthFiendApeArmor, EquipmentSlot.Armor)]
    [InlineData(EquipmentId.EastSeaBanner, EquipmentId.EarthFiendRing, EquipmentSlot.Accessory)]
    public void Treasure_PersistsAcrossOtherEquipmentChanges(EquipmentId treasureId,
        EquipmentId equipmentId, EquipmentSlot slot)
    {
        var scope = new TestGameScope([(1, 0)], 0, 25);
        EquipmentSystem system = CreateEquipmentSystem(scope);
        ProfileSystem profileSystem = scope.GetSystem<ProfileSystem>();
        EquipmentData treasure = system.Roll(Guid.NewGuid(), treasureId, () => .37f);
        Assert.True(profileSystem.TryCollectEquipment(1, treasure));
        Assert.True(system.TryEquip(1, treasure.InstanceId));
        CombatantAttributes equippedAttributes = scope.Hero.CaptureCombatAttributes();

        // 首次穿戴、同部位替换和卸下均只改变请求的部位。
        foreach (float sample in new[] { 0f, .99f })
        {
            EquipmentData item = system.Roll(Guid.NewGuid(), equipmentId, () => sample);
            Assert.True(profileSystem.TryCollectEquipment(1, item));
            Assert.True(system.TryEquip(1, item.InstanceId));
            EquippedItems slots = scope.Profile.GetSlots(1).Value;
            Assert.Same(item, slot switch
            {
                EquipmentSlot.Weapon => slots.Weapon,
                EquipmentSlot.Armor => slots.Armor,
                EquipmentSlot.Accessory => slots.Accessory,
                EquipmentSlot.Treasure => slots.Treasure,
                _ => throw new ArgumentOutOfRangeException(nameof(slot))
            });
            Assert.Same(treasure, scope.Profile.GetSlots(scope.Profile.HeroOf(PlayerSlot.P1)).Value.Treasure);
        }
        Assert.True(system.TryUnequip(1, slot));
        Assert.Same(treasure, scope.Profile.GetSlots(scope.Profile.HeroOf(PlayerSlot.P1)).Value.Treasure);
        Assert.Equal(equippedAttributes, scope.Hero.CaptureCombatAttributes());

        // 保存及重建仍保留全部实例，不因其他部位交换丢失法宝。
        GameData saved = JsonConvert.DeserializeObject<GameData>(JsonConvert.SerializeObject(scope.CaptureSaveData()));
        Assert.Equal(3, saved.EquipmentItems.Count);
        Assert.Equal(treasure.InstanceId, saved.Heroes[0].TreasureInstanceId);
        var restored = TestGameScope.FromSaveData(saved, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
        Assert.Equal(treasure.InstanceId, restored.Profile.GetSlots(restored.Profile.HeroOf(PlayerSlot.P1)).Value.Treasure.InstanceId);
        Assert.Equal(equippedAttributes, restored.Hero.CaptureCombatAttributes());
        Assert.Equal(2, restored.Profile.GetBag(restored.Profile.HeroOf(PlayerSlot.P1)).Count);
    }

    /// <summary>
    /// 金钢琢闪避按原文件保留整百分比阶梯，其他随机属性保留各自端点。
    /// </summary>
    [Fact]
    public void DiamondSnare_RollsOfficialRoundedProbability()
    {
        EquipmentSystem system = CreateEquipmentSystem();
        EquipmentData low = system.Roll(Guid.NewGuid(), EquipmentId.DiamondSnare, () => 0f);
        EquipmentData high = system.Roll(Guid.NewGuid(), EquipmentId.DiamondSnare, () => .999999f);
        Assert.Equal((50, .02f, 2), (low.Attributes.Power, low.Attributes.Miss, low.Attributes.HpRegen));
        Assert.Equal(80, high.Attributes.Power);
        Assert.Equal(.05f, high.Attributes.Miss, 4);
        Assert.Equal(5, high.Attributes.HpRegen);
        foreach (float sample in new[] { .1f, .25f, .45f, .6f, .9f })
        {
            EquipmentData item = system.Roll(Guid.NewGuid(), EquipmentId.DiamondSnare, () => sample);
            Assert.Equal((float)Math.Round(item.Attributes.Miss * 100), item.Attributes.Miss * 100, 4);
        }
    }

    /// <summary>
    /// 龙宫武器的15%属于闪避，不可误接暴击或经属性点曲线换算。
    /// </summary>
    [Fact]
    public void RuyiStaff_AppliesOfficialDodgeAndPower()
    {
        var scope = new TestGameScope([(1, 0)], 0, 25);
        EquipmentSystem system = CreateEquipmentSystem(scope);
        int power = scope.Hero.Power.Value;
        EquipmentData item = system.Roll(Guid.NewGuid(), EquipmentId.RuyiGoldenStaff, () => 0);
        Assert.True(scope.GetSystem<ProfileSystem>().TryCollectEquipment(1, item));
        Assert.True(system.TryEquip(1, item.InstanceId));
        Assert.Equal((power + 120, .15f, 0f), (scope.Hero.Power.Value, scope.Hero.Miss.Value, scope.Hero.Crit.Value));
    }

    /// <summary>
    /// 血海魔甲的负防御和负回血经过穿卸与存档往返仍是同一份实际属性。
    /// </summary>
    [Fact]
    public void BloodSeaArmor_PreservesCostsThroughWearingAndSaveReload()
    {
        var scope = new TestGameScope([(1, 0)], 0, 25);
        EquipmentSystem system = CreateEquipmentSystem(scope);
        EquipmentData armor = system.Roll(Guid.NewGuid(), EquipmentId.BloodSeaDemonArmor, () => .37f);
        int basePower = scope.Hero.Power.Value;
        int baseDefense = scope.Hero.Def.Value;
        Assert.Equal((80, -10, -2, .05f, .05f),
            (armor.Attributes.Power, armor.Attributes.Def, armor.Attributes.HpRegen,
                armor.Attributes.Crit, armor.Attributes.Miss));
        Assert.True(scope.GetSystem<ProfileSystem>().TryCollectEquipment(1, armor));
        Assert.True(system.TryEquip(1, armor.InstanceId));
        Assert.Equal((basePower + 80, Math.Max(0, baseDefense - 10), -2, .05f, .05f),
            (scope.Hero.Power.Value, scope.Hero.Def.Value, scope.Hero.HpRegen.Value,
                scope.Hero.Crit.Value, scope.Hero.Miss.Value));

        // 正式序列化、校验和重建不得重抽、抹去代价或恢复成裸身属性。
        GameData data = JsonConvert.DeserializeObject<GameData>(JsonConvert.SerializeObject(scope.CaptureSaveData()));
        var restored = TestGameScope.FromSaveData(data, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
        Assert.Equal(armor.InstanceId, restored.Profile.GetSlots(restored.Profile.HeroOf(PlayerSlot.P1)).Value.Armor.InstanceId);
        Assert.Equal(armor.Attributes, restored.Profile.GetSlots(restored.Profile.HeroOf(PlayerSlot.P1)).Value.Armor.Attributes);
        Assert.Equal(-2, restored.Hero.HpRegen.Value);
        Assert.True(restored.GetSystem<EquipmentSystem>().TryUnequip(1, EquipmentSlot.Armor));
        Assert.Equal((basePower, baseDefense, 0),
            (restored.Hero.Power.Value, restored.Hero.Def.Value, restored.Hero.HpRegen.Value));
    }

    /// <summary>
    /// 在指定或新建的档案测试作用域中取得装备规则服务。
    /// </summary>
    /// <param name="playerModel">借用的测试档案作用域；允许为空的方法会新建作用域。</param>
    /// <returns>指定或新建测试作用域拥有的装备规则服务。</returns>
    private static EquipmentSystem CreateEquipmentSystem(TestGameScope playerModel = null) => new(playerModel == null ? TestTables.Architecture : TestTables.ForPlayer(playerModel));

    /// <summary>
    /// 系统只修改所属架构注册的玩家模型，装配快照不借用或替换模型。
    /// </summary>
    [Fact]
    public void RegisteredModel_EquipmentOperationsStayInTheirOwnScope()
    {
        TestGameScope first = new([(1, 0)], 0, 25);
        TestGameScope second = new([(1, 0)], 0, 25);
        EquipmentSystem firstSystem = CreateEquipmentSystem(first);
        EquipmentSystem secondSystem = CreateEquipmentSystem(second);
        EquipmentData weapon = firstSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        first.GetSystem<ProfileSystem>().TryCollectEquipment(1, weapon);
        Assert.False(secondSystem.TryEquip(1, weapon.InstanceId));
        Assert.True(firstSystem.TryEquip(1, weapon.InstanceId));
        CombatantAttributes snapshot = first.Hero.CaptureCombatAttributes();
        Assert.Same(weapon, first.Profile.GetSlots(first.Profile.HeroOf(PlayerSlot.P1)).Value.Weapon);
        Assert.Null(second.Profile.GetSlots(second.Profile.HeroOf(PlayerSlot.P1)).Value.Weapon);
        Assert.True(firstSystem.TryUnequip(1, EquipmentSlot.Weapon));
        Assert.Equal(first.Hero.Power.Value + weapon.Attributes.Power, snapshot.Power);
        Assert.Empty(second.Profile.GetBag(second.Profile.HeroOf(PlayerSlot.P1)));
        Assert.Null(second.Profile.GetSlots(second.Profile.HeroOf(PlayerSlot.P1)).Value.Weapon);
    }

    /// <summary>
    /// 跨三页按品质出售，穿戴物和其他品质不参与，各数据源只通知一次。
    /// </summary>
    [Fact]
    public void BulkSale_SellsAllPagesAndNotWornEquipment_WithOneNotification()
    {
        TestGameScope playerModel = new([(1, 0)], 7, 75);
        EquipmentSystem equipmentSystem = CreateEquipmentSystem(playerModel);
        EquipmentData worn = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(1, worn);
        Assert.True(equipmentSystem.TryEquip(1, worn.InstanceId));
        for (int i = 0; i < 75; i++)
        {
            playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(1, equipmentSystem.Roll(Guid.NewGuid(), i % 3 == 0
                        ? EquipmentId.CommonPineStaff : EquipmentId.RoughPilgrimStaff, () => 0));
        }

        EquipmentData[] retained = playerModel.Profile.GetBag(playerModel.Profile.HeroOf(PlayerSlot.P1)).Where(x => x.EquipmentId == EquipmentId.CommonPineStaff).ToArray();
        int inventoryNotifications = 0;
        int walletNotifications = 0;
        playerModel.Profile.GetBag(playerModel.Profile.HeroOf(PlayerSlot.P1)).Register(_ => Observe());
        playerModel.Profile.GetSoul(playerModel.Profile.HeroOf(PlayerSlot.P1)).Register(_ =>
        {
            walletNotifications++;
            ObserveCommittedState();
        });

        Assert.Equal(50, equipmentSystem.SellByQuality(1, EquipmentQuality.Rough));
        Assert.Equal(1, inventoryNotifications);
        Assert.Equal(1, walletNotifications);
        Assert.Same(worn, playerModel.Profile.GetSlots(playerModel.Profile.HeroOf(PlayerSlot.P1)).Value.Weapon);
        Assert.Equal(0, equipmentSystem.SellByQuality(1, EquipmentQuality.Rough));
        Assert.Equal(0, equipmentSystem.SellByQuality(1, EquipmentQuality.Excellent));
        Assert.Equal(0, equipmentSystem.SellByQuality(1, (EquipmentQuality)99));
        Assert.Equal(1, inventoryNotifications);
        Assert.Equal(1, walletNotifications);

        void Observe()
        {
            inventoryNotifications++;
            Assert.Equal(retained, playerModel.Profile.GetBag(playerModel.Profile.HeroOf(PlayerSlot.P1)));
        }
        void ObserveCommittedState()
        {
            Assert.Equal(retained, playerModel.Profile.GetBag(playerModel.Profile.HeroOf(PlayerSlot.P1)));
            Assert.Equal(507, playerModel.Profile.GetSoul(playerModel.Profile.HeroOf(PlayerSlot.P1)).Value);
        }
    }

    /// <summary>
    /// 整批价格超过灵魂余额上限时，一件也不卖且不发送变更。
    /// </summary>
    [Fact]
    public void BulkSale_OverflowLeavesEntireTransactionUnchanged()
    {
        TestGameScope playerModel = new([(1, 0)],
            int.MaxValue - 15, 75);
        EquipmentSystem equipmentSystem = CreateEquipmentSystem(playerModel);
        for (int i = 0; i < 2; i++)
        {
            playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(1, equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0));
        }

        EquipmentData[] before = playerModel.Profile.GetBag(playerModel.Profile.HeroOf(PlayerSlot.P1)).ToArray();
        int notifications = 0;
        playerModel.Profile.GetBag(playerModel.Profile.HeroOf(PlayerSlot.P1)).Register(_ => notifications++);
        playerModel.Profile.GetSoul(playerModel.Profile.HeroOf(PlayerSlot.P1)).Register(_ => notifications++);
        Assert.Equal(0, equipmentSystem.SellByQuality(1, EquipmentQuality.Rough));
        Assert.Equal(before, playerModel.Profile.GetBag(playerModel.Profile.HeroOf(PlayerSlot.P1)));
        Assert.Equal(int.MaxValue - 15, playerModel.Profile.GetSoul(playerModel.Profile.HeroOf(PlayerSlot.P1)).Value);
        Assert.Equal(0, notifications);
    }

    /// <summary>
    /// 出售只消费背包实例一次，入账表内价格，溢出或已穿戴时保持完整状态。
    /// </summary>
    [Fact]
    public void Selling_UsesConfiguredPriceAndRejectsStaleWornOrOverflow()
    {
        TestGameScope playerModel = new([(1, 0)], 0, 25);
        EquipmentSystem equipmentSystem = CreateEquipmentSystem(playerModel);
        EquipmentData rough = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        EquipmentData common = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.CommonPineStaff, () => 0);
        playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(1, rough);
        playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(1, common);
        Assert.True(equipmentSystem.TryEquip(1, rough.InstanceId));
        Assert.False(equipmentSystem.TrySell(1, rough.InstanceId));
        Assert.True(equipmentSystem.TrySell(1, common.InstanceId));
        Assert.Equal(20, playerModel.Profile.GetSoul(playerModel.Profile.HeroOf(PlayerSlot.P1)).Value);
        Assert.False(equipmentSystem.TrySell(1, common.InstanceId));
        Assert.True(equipmentSystem.TryUnequip(1, EquipmentSlot.Weapon));
        playerModel.Profile.RestoreSoul(1, int.MaxValue - 9);
        Assert.False(equipmentSystem.TrySell(1, rough.InstanceId));
        Assert.Same(rough, Assert.Single(playerModel.Profile.GetBag(playerModel.Profile.HeroOf(PlayerSlot.P1))));
        Assert.Equal(int.MaxValue - 9, playerModel.Profile.GetSoul(playerModel.Profile.HeroOf(PlayerSlot.P1)).Value);
        playerModel.Profile.RestoreSoul(1, 0);
        Assert.True(equipmentSystem.TrySell(1, rough.InstanceId));
        Assert.Equal(10, playerModel.Profile.GetSoul(playerModel.Profile.HeroOf(PlayerSlot.P1)).Value);
        Assert.Empty(playerModel.Profile.GetBag(playerModel.Profile.HeroOf(PlayerSlot.P1)));
    }

    /// <summary>
    /// 八件初级装备的每条实际属性在两端精确匹配原版范围。
    /// </summary>
    /// <param name="id">本次验证的装备配置编号。</param>
    /// <param name="minimum">原版第一条装备属性的最小值。</param>
    /// <param name="maximum">原版第一条装备属性的最大值。</param>
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
        EquipmentData low = CreateEquipmentSystem().Roll(Guid.NewGuid(), id, () => 0);
        EquipmentData high = CreateEquipmentSystem().Roll(Guid.NewGuid(), id, () => .999999f);
        EquipmentConfig definition = TestTables.Tables.TbEquipmentConfig.Get(id);
        Assert.Equal(minimum, low.Attributes.Get(definition.AttributeRanges[0].Attribute));
        Assert.Equal(maximum, high.Attributes.Get(definition.AttributeRanges[0].Attribute));
        Assert.Equal(definition.AttributeRanges.Select(x => (float)x.Minimum), definition.AttributeRanges.Select(x => (float)low.Attributes.Get(x.Attribute)));
        Assert.Equal(definition.AttributeRanges.Select(x => (float)x.Maximum), definition.AttributeRanges.Select(x => (float)high.Attributes.Get(x.Attribute)));
        Assert.Equal(0, low.Attributes.Mdef);
    }

    /// <summary>
    /// 保留 round 随机的端点半权重，生命与魔法分别抽取。
    /// </summary>
    [Fact]
    public void RandomAttributes_UseRoundDistributionAndIndependentRolls()
    {
        var values = Enumerable.Range(0, 600).Select(i => CreateEquipmentSystem().Roll(Guid.NewGuid(),
            EquipmentId.RoughPilgrimStaff, () => (i + .5f) / 600).Attributes.Power).ToArray();
        Assert.Equal(new[] { 100, 200, 200, 100 }, new[] { 2f, 3f, 4f, 5f }.Select(x => values.Count(v => v == x)));
        Queue<float> random = new(new[] { 0f, .999999f });
        EquipmentData armor = CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimClothes, random.Dequeue);
        Assert.Equal((20, 40, 3), (armor.Attributes.MaxHp, armor.Attributes.MaxMp, armor.Attributes.Def));
        Assert.Empty(random);
    }

    /// <summary>
    /// 一万组分层采样精确锁定恢复品概率、互斥关系与装备独立判定。
    /// </summary>
    [Fact]
    public void GroundSelection_MatchesOfficialProbabilitiesAndPoolWeights()
    {
        IReadOnlyList<PickupConfig> pickups = TestTables.Tables.TbPickupConfig.DataList;
        EquipmentDropPoolConfig pool = TestTables.Tables.TbEquipmentDropPoolConfig.Get(EquipmentDropPoolId.Rough);
        PickupId[] results = Enumerable.Range(0, 10000).Select(i =>
            new LootSystem(TestTables.Architecture).RollGround(0, EquipmentDropPoolId.None, () => (i + .5f) / 10000).Pickup).ToArray();
        Assert.Equal(new[] { 375, 125, 500, 9000 }, new[] { PickupId.SmallPeach, PickupId.LargePeach,
        PickupId.ManaBottle, PickupId.None }.Select(x => results.Count(v => v == x)));
        EquipmentId[] equipment = Enumerable.Range(0, 600).Select(i =>
        {
            Queue<float> samples = new(new[] { 0f, 0f, (i + .5f) / 600 });
            GroundLootSelection result = new LootSystem(TestTables.Architecture).RollGround(.15f, pool.Id, samples.Dequeue);
            Assert.Equal(PickupId.SmallPeach, result.Pickup);
            return result.Equipment;
        }).ToArray();
        Assert.Equal(new[] { 100, 200, 200, 100 }, pool.Entries.Select(x => equipment.Count(v => v == x.EquipmentId)));
        Queue<float> upperEndpoint = new(new[] { 0f, 0f, 1f });
        Assert.Equal(pool.Entries.Last().EquipmentId,
            new LootSystem(TestTables.Architecture).RollGround(.15f, pool.Id, upperEndpoint.Dequeue).Equipment);
        Queue<float> noGear = new(new[] { 0f, .15f });
        Assert.Equal(EquipmentId.None, new LootSystem(TestTables.Architecture).RollGround(.15f, pool.Id, noGear.Dequeue).Equipment);
        LevelSpawnRecipe boss = TestTables.Tables.TbLevelConfig.Get(1)
            .Stages[^1].Recipes.Single(x => x.EnemyEntityId == GameConfig.Entity.EntityId.Gorilla);
        EnemyConfig monster = TestTables.Tables.TbEnemyConfig.DataList.Single(x => x.EntityId == boss.EnemyEntityId);
        Assert.Equal(EquipmentDropPoolId.None, monster.EquipmentDropPoolId);
        Assert.Equal(EquipmentId.None,
            new LootSystem(TestTables.Architecture).RollGround(monster.EquipmentDropChance,
                monster.EquipmentDropPoolId, () => 0f).Equipment);
    }

    /// <summary>
    /// 满包和重复领取没有副作用，坏快照不能覆盖现有背包。
    /// </summary>
    [Fact]
    public void Inventory_IsAtomicAndRejectsDuplicateOrFullPickup()
    {
        ProfileModel bag = new([(1, 0)], TestTables.Curve, 1);
        int changes = 0;
        bag.GetBag(bag.HeroOf(PlayerSlot.P1)).Register(_ => changes++);
        EquipmentData item = CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.CommonPineStaff, () => .5f);
        Assert.True(TestTables.ForProfile(bag).TryCollectEquipment(1, item));
        Assert.False(TestTables.ForProfile(bag).TryCollectEquipment(1, item));
        Assert.False(TestTables.ForProfile(bag).TryCollectEquipment(1, CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.RoughCassock, () => 0)));
        var scope = new TestGameScope([(1, 0)], 0, 1);
        GameData malformed = scope.CaptureSaveData();
        var data = new GameLogic.Save.Data.EquipmentSaveData
        {
            InstanceId = item.InstanceId,
            EquipmentId = item.EquipmentId,
            Attributes = item.Attributes
        };
        malformed.EquipmentItems.Add(data);
        malformed.EquipmentItems.Add(data);
        Assert.Throws<ArgumentException>(() => scope.RestoreCheckpoint(malformed));
        Assert.Empty(scope.Profile.GetBag(scope.Profile.HeroOf(PlayerSlot.P1)));
        Assert.Same(item, Assert.Single(bag.GetBag(bag.HeroOf(PlayerSlot.P1))));
        Assert.Equal(1, changes);
    }

    /// <summary>
    /// 保存实际属性及稳定顺序，JSON 读回和关卡回滚都不重抽。
    /// </summary>
    [Fact]
    public void Equipment_RoundTripsAndRollsBackWithoutRerolling()
    {
        TestGameScope playerModel = new(new[] { (1, 0) }, 0, 25);
        EquipmentData first = CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimClothes, () => .73f);
        playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(1, first);
        GameData snapshot = playerModel.CaptureSaveData();
        playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(1, CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.CommonPineStaff, () => .2f));
        var json = JsonConvert.SerializeObject(snapshot);
        var restored = TestGameScope.FromSaveData(JsonConvert.DeserializeObject<GameData>(json),
            TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
        EquipmentData actual = Assert.Single(restored.Profile.GetBag(restored.Profile.HeroOf(PlayerSlot.P1)));
        Assert.Equal(first.InstanceId, actual.InstanceId);
        Assert.Equal(first.Attributes, actual.Attributes);
        ProfileModel bag = playerModel.Profile;
        playerModel.RestoreCheckpoint(snapshot);
        Assert.Same(bag, playerModel.Profile);
        Assert.Equal(first.InstanceId, Assert.Single(bag.GetBag(bag.HeroOf(PlayerSlot.P1))).InstanceId);
        snapshot.EquipmentItems[0].Attributes = first.Attributes with
        {
            MaxHp = 999
        };
        Assert.Throws<ArgumentException>(() => TestGameScope.FromSaveData(snapshot, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
        Assert.Equal(35, first.Attributes.MaxHp);
        var missing = Newtonsoft.Json.Linq.JObject.Parse(json);
        ((Newtonsoft.Json.Linq.JObject)missing["EquipmentItems"][0]["Attributes"]).Remove("MaxHp");
        Assert.Throws<JsonSerializationException>(() => missing.ToObject<GameData>());
    }

    /// <summary>
    /// 穿戴交换保持实例所有权，异角色和满包卸装均不改变状态。
    /// </summary>
    [Fact]
    public void Wearing_ExchangesItemsAtomicallyAndRejectsWrongHeroOrFullBag()
    {
        TestGameScope playerModel = new([(1, 0)], 0, 1);
        EquipmentSystem equipmentSystem = CreateEquipmentSystem(playerModel);
        EquipmentData rough = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        EquipmentData common = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimStaff, () => .999999f);
        Assert.True(playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(1, rough));
        Assert.True(equipmentSystem.TryEquip(1, rough.InstanceId));
        Assert.Same(rough, playerModel.Profile.GetSlots(playerModel.Profile.HeroOf(PlayerSlot.P1)).Value.Weapon);
        Assert.Empty(playerModel.Profile.GetBag(playerModel.Profile.HeroOf(PlayerSlot.P1)));
        Assert.True(playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(1, common));
        Assert.True(equipmentSystem.TryEquip(1, common.InstanceId));
        Assert.Same(common, playerModel.Profile.GetSlots(playerModel.Profile.HeroOf(PlayerSlot.P1)).Value.Weapon);
        Assert.Same(rough, Assert.Single(playerModel.Profile.GetBag(playerModel.Profile.HeroOf(PlayerSlot.P1))));
        Assert.False(equipmentSystem.TryUnequip(1, EquipmentSlot.Weapon));
        Assert.Same(common, playerModel.Profile.GetSlots(playerModel.Profile.HeroOf(PlayerSlot.P1)).Value.Weapon);
        EquipmentData tang = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.RoughPineStaff, () => 0);
        playerModel.Profile.RestoreItems(1, [tang]);
        Assert.False(equipmentSystem.TryEquip(1, tang.InstanceId));
        Assert.Same(common, playerModel.Profile.GetSlots(playerModel.Profile.HeroOf(PlayerSlot.P1)).Value.Weapon);
        Assert.Same(tang, Assert.Single(playerModel.Profile.GetBag(playerModel.Profile.HeroOf(PlayerSlot.P1))));
        playerModel.Profile.RestoreItems(1, []);
        Assert.True(equipmentSystem.TryUnequip(1, EquipmentSlot.Weapon));
        Assert.Null(playerModel.Profile.GetSlots(playerModel.Profile.HeroOf(PlayerSlot.P1)).Value.Weapon);
        Assert.Same(common, Assert.Single(playerModel.Profile.GetBag(playerModel.Profile.HeroOf(PlayerSlot.P1))));
    }

    /// <summary>
    /// 具名属性和穿戴引用经过 JSON、检查点回滚后完全保留，字段顺序不影响读档。
    /// </summary>
    [Fact]
    public void Wearing_RoundTripsNamedAttributesAndRestoresCheckpoint()
    {
        TestGameScope playerModel = new([(1, 0)], 0, 25);
        EquipmentSystem equipmentSystem = CreateEquipmentSystem(playerModel);
        EquipmentData armor = equipmentSystem.Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimClothes, () => .5f);
        playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(1, armor);
        equipmentSystem.TryEquip(1, armor.InstanceId);
        GameData snapshot = playerModel.CaptureSaveData();
        var json = Newtonsoft.Json.Linq.JObject.FromObject(snapshot);
        var attrs = (Newtonsoft.Json.Linq.JObject)json["EquipmentItems"][0]["Attributes"];
        json["EquipmentItems"][0]["Attributes"] = new Newtonsoft.Json.Linq.JObject(attrs.Properties().Reverse());
        var restored = TestGameScope.FromSaveData(json.ToObject<GameData>(), TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
        Assert.Empty(restored.Profile.GetBag(restored.Profile.HeroOf(PlayerSlot.P1)));
        Assert.Equal(armor.Attributes, restored.Profile.GetSlots(restored.Profile.HeroOf(PlayerSlot.P1)).Value.Armor.Attributes);
        Assert.Equal((30, 30, 3), (restored.Profile.GetSlots(restored.Profile.HeroOf(PlayerSlot.P1)).Value.Armor.Attributes.MaxHp,
            restored.Profile.GetSlots(restored.Profile.HeroOf(PlayerSlot.P1)).Value.Armor.Attributes.MaxMp, restored.Profile.GetSlots(restored.Profile.HeroOf(PlayerSlot.P1)).Value.Armor.Attributes.Def));
        Assert.True(equipmentSystem.TryUnequip(1, EquipmentSlot.Armor));
        playerModel.RestoreCheckpoint(snapshot);
        Assert.Empty(playerModel.Profile.GetBag(playerModel.Profile.HeroOf(PlayerSlot.P1)));
        Assert.Equal(armor.InstanceId, playerModel.Profile.GetSlots(playerModel.Profile.HeroOf(PlayerSlot.P1)).Value.Armor.InstanceId);
        CombatantAttributes frozen = playerModel.Hero.CaptureCombatAttributes();
        Assert.True(equipmentSystem.TryUnequip(1, EquipmentSlot.Armor));
        Assert.Null(playerModel.Profile.GetSlots(playerModel.Profile.HeroOf(PlayerSlot.P1)).Value.Armor);
        Assert.Equal(TestTables.Tables.TbHeroGrowthConfig.Get(1).Levels[0].BaseAttributes.Def + armor.Attributes.Def, frozen.Def);
    }

    /// <summary>
    /// 错误部位、跨角色引用和一物多穿都被正式读档拒绝。
    /// </summary>
    [Fact]
    public void Wearing_RejectsInvalidSavedReferences()
    {
        TestGameScope playerModel = new([(1, 0)], 0, 25);
        EquipmentData item = CreateEquipmentSystem().Roll(Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0);
        playerModel.GetSystem<ProfileSystem>().TryCollectEquipment(1, item);
        GameData data = playerModel.CaptureSaveData();
        data.Heroes[0].ArmorInstanceId = item.InstanceId;
        Assert.Throws<ArgumentException>(() => TestGameScope.FromSaveData(data, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
        data.Heroes[0].WeaponInstanceId = item.InstanceId;
        Assert.Throws<ArgumentException>(() => TestGameScope.FromSaveData(data, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
        data.Heroes[0].ArmorInstanceId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => TestGameScope.FromSaveData(data, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
    }
}
