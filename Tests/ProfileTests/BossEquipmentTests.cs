using System;
using GameConfig.Equipment;
using GameConfig.Loot;
using GameLogic.Architecture;
using GameLogic.Equipment;
using GameLogic.Save;
using Newtonsoft.Json;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>造一第二、第三关装备的原始数值、通用饰品所有权和直接概率存档回归。</summary>
public sealed class BossEquipmentTests
{
    /// <summary>灵戒经穿戴、其他部位交换、升级、保存、读档和重开本局仍保持同一件及 3% 概率。</summary>
    [Fact]
    public void Ring_StaysEquippedAcrossOtherSlotsAndPersistsDirectProbabilities()
    {
        var scope = new TestGameScope(1, [(1, 0)], 0, 25);
        var equipment = scope.GetSystem<EquipmentSystem>();
        var ring = equipment.Roll(Guid.NewGuid(), EquipmentId.EarthFiendRing, () => .5f);
        Assert.Equal((15, 4, .03f, .03f),
            (ring.Attributes.Power, ring.Attributes.Def, ring.Attributes.Crit, ring.Attributes.Miss));
        Assert.True(scope.GetSystem<ProfileSystem>().TryCollectEquipment(ring));
        Assert.True(equipment.TryEquip(ring.InstanceId));
        Assert.False(scope.GetSystem<ProfileSystem>().TryCollectEquipment(ring));
        Assert.Equal((25, 6, .03f, .03f),
            (scope.Hero.Power.Value, scope.Hero.Def.Value, scope.Hero.Crit.Value, scope.Hero.Miss.Value));
        foreach (var id in new[] { EquipmentId.CommonPilgrimStaff, EquipmentId.EarthFiendApeArmor })
        {
            var item = equipment.Roll(Guid.NewGuid(), id, () => 0);
            Assert.True(scope.GetSystem<ProfileSystem>().TryCollectEquipment(item));
            Assert.True(equipment.TryEquip(item.InstanceId));
            Assert.Same(ring, scope.Profile.Accessory);
            Assert.Equal(.03f, scope.Hero.Crit.Value);
        }
        scope.GetSystem<ProfileSystem>().AddExperience(140);
        Assert.Same(ring, scope.Profile.Accessory);
        Assert.Equal(.03f, scope.Hero.Miss.Value);
        var data = JsonConvert.DeserializeObject<GameData>(JsonConvert.SerializeObject(scope.CaptureSaveData()));
        Assert.Equal(ring.InstanceId, data.Heroes[0].AccessoryInstanceId);
        var restored = TestGameScope.FromSaveData(data, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
        Assert.Equal(ring.InstanceId, restored.Profile.Accessory.InstanceId);
        Assert.Equal((scope.Hero.Power.Value, scope.Hero.Def.Value, .03f, .03f),
            (restored.Hero.Power.Value, restored.Hero.Def.Value, restored.Hero.Crit.Value, restored.Hero.Miss.Value));
        restored.RestartHero();
        Assert.Equal(.03f, restored.Hero.Crit.Value);
        Assert.True(restored.GetSystem<EquipmentSystem>().TryUnequip(EquipmentSlot.Weapon));
        Assert.True(restored.GetSystem<EquipmentSystem>().TryUnequip(EquipmentSlot.Armor));
        Assert.Equal(ring.InstanceId, restored.Profile.Accessory.InstanceId);
        Assert.True(restored.GetSystem<EquipmentSystem>().TryUnequip(EquipmentSlot.Accessory));
        Assert.Null(restored.Profile.Accessory);
        Assert.Equal((0f, 0f), (restored.Hero.Crit.Value, restored.Hero.Miss.Value));
    }

    /// <summary>通用灵戒允许唐僧穿戴；悟空专属猿甲拒绝交换，已有饰品不会丢失。</summary>
    [Fact]
    public void UniversalAccessory_DoesNotRequireAPlayableHeroOrChangeOtherOwners()
    {
        var scope = new TestGameScope(2, [(2, 0)], 0, 25, initializeHero: false);
        var equipment = scope.GetSystem<EquipmentSystem>();
        var ring = equipment.Roll(Guid.NewGuid(), EquipmentId.EarthFiendRing, () => 0);
        var armor = equipment.Roll(Guid.NewGuid(), EquipmentId.EarthFiendApeArmor, () => 0);
        Assert.True(scope.GetSystem<ProfileSystem>().TryCollectEquipment(ring));
        Assert.True(scope.GetSystem<ProfileSystem>().TryCollectEquipment(armor));
        Assert.True(equipment.TryEquip(ring.InstanceId));
        Assert.False(equipment.TryEquip(armor.InstanceId));
        Assert.Same(ring, scope.Profile.Accessory);
        Assert.Null(scope.Profile.Armor);
    }

    /// <summary>猿甲保持原版整数随机端点；权杖固定加魔法 100、攻击 20，优秀品质售价 40。</summary>
    [Fact]
    public void OriginalEquipment_RetainsRollRangesAndSalePrices()
    {
        var equipment = new EquipmentSystem(TestTables.Architecture);
        var low = equipment.Roll(Guid.NewGuid(), EquipmentId.EarthFiendApeArmor, () => 0);
        var high = equipment.Roll(Guid.NewGuid(), EquipmentId.EarthFiendApeArmor, () => .999999f);
        Assert.Equal((80, 50, 4), (low.Attributes.MaxHp, low.Attributes.MaxMp, low.Attributes.Def));
        Assert.Equal((110, 70, 4), (high.Attributes.MaxHp, high.Attributes.MaxMp, high.Attributes.Def));
        var staff = equipment.Roll(Guid.NewGuid(), EquipmentId.EarthFiendScepter, () => 0);
        Assert.Equal((100, 20), (staff.Attributes.MaxMp, staff.Attributes.Power));
        foreach (var id in new[] { EquipmentId.EarthFiendRing, EquipmentId.EarthFiendApeArmor, EquipmentId.EarthFiendScepter })
        {
            var definition = TestTables.Tables.TbEquipmentConfig.Get(id);
            Assert.Equal(EquipmentQuality.Excellent, definition.Quality);
            Assert.Equal(40, definition.SellPrice);
        }
        Assert.Single(TestTables.Tables.TbEquipmentDropPoolConfig.Get(EquipmentDropPoolId.MacaqueKing).Entries);
        Assert.Equal(2, TestTables.Tables.TbEquipmentDropPoolConfig.Get(EquipmentDropPoolId.TamarinKing).Entries.Count);
        Assert.Equal((.4f, EquipmentDropPoolId.MacaqueKing),
            (TestTables.Tables.TbMonsterConfig.Get(4).EquipmentDropChance, TestTables.Tables.TbMonsterConfig.Get(4).EquipmentDropPoolId));
        Assert.Equal((.35f, EquipmentDropPoolId.TamarinKing),
            (TestTables.Tables.TbMonsterConfig.Get(6).EquipmentDropChance, TestTables.Tables.TbMonsterConfig.Get(6).EquipmentDropPoolId));
    }

    /// <summary>读档不把概率重抽、取整或接受非有限值，跨部位和重复所有权也必须拒绝。</summary>
    [Fact]
    public void Save_RejectsAlteredProbabilitiesAndInvalidAccessoryOwnership()
    {
        var scope = new TestGameScope(1, [(1, 0)], 0, 25);
        var ring = scope.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), EquipmentId.EarthFiendRing, () => 0);
        Assert.True(scope.GetSystem<ProfileSystem>().TryCollectEquipment(ring));
        var utility = new SaveDataUtility();
        foreach (float invalid in new[] { .031f, 3f, float.NaN, float.PositiveInfinity })
        {
            var data = scope.CaptureSaveData();
            data.EquipmentItems[0].Attributes = ring.Attributes with { Crit = invalid };
            Assert.Throws<ArgumentException>(() => utility.Validate(data, TestTables.Tables.TbEquipmentConfig, 25, TestTables.Tables.TbSkillConfig, TestTables.Tables.TbLevelConfig));
        }
        var valid = scope.CaptureSaveData();
        valid.Heroes[0].AccessoryInstanceId = ring.InstanceId;
        utility.Validate(valid, TestTables.Tables.TbEquipmentConfig, 25, TestTables.Tables.TbSkillConfig, TestTables.Tables.TbLevelConfig);
        valid.Heroes[0].WeaponInstanceId = ring.InstanceId;
        Assert.Throws<ArgumentException>(() => utility.Validate(valid, TestTables.Tables.TbEquipmentConfig, 25, TestTables.Tables.TbSkillConfig, TestTables.Tables.TbLevelConfig));
        valid.Heroes[0].AccessoryInstanceId = Guid.Empty;
        Assert.Throws<ArgumentException>(() => utility.Validate(valid, TestTables.Tables.TbEquipmentConfig, 25, TestTables.Tables.TbSkillConfig, TestTables.Tables.TbLevelConfig));
    }
}
