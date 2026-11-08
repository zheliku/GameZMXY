using System;
using System.Linq;
using GameConfig.Equipment;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Equipment;
using GameLogic.Save;
using GameLogic.Save.Data;
using GameLogic.Skills;
using Newtonsoft.Json;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 验证集合绑定和跨模型事务的外部可观察行为。
/// </summary>
public sealed class ModelTransactionTests
{
    /// <summary>
    /// 两个席位分别执行个人业务时，成长、钱包、背包、穿戴和两类技能都只修改本人。
    /// </summary>
    /// <param name="heroId">执行本次业务的英雄。</param>
    /// <param name="otherHeroId">必须保持原样的另一英雄。</param>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public void PersonalData_ChangesOnlyTheRequestedHero(int heroId, int otherHeroId)
    {
        var scope = new TestGameScope([(2, 0), (1, 0)], 10000, 3, initializeHero: false);
        scope.EnterMapContext();
        ProfileSystem profile = scope.GetSystem<ProfileSystem>();
        EquipmentSystem equipment = scope.GetSystem<EquipmentSystem>();
        SkillSystem skills = scope.GetSystem<SkillSystem>();
        string untouched = JsonConvert.SerializeObject(scope.CaptureSaveData().Heroes.Single(hero => hero.HeroId == otherHeroId));
        SkillId skillId = heroId == 1 ? SkillId.RisingDragon : SkillId.IceDragonWave;
        EquipmentData ring = equipment.Roll(Guid.NewGuid(), EquipmentId.EarthFiendRing, () => .5f);
        EquipmentData bagItem = equipment.Roll(Guid.NewGuid(), EquipmentId.DiamondSnare, () => .25f);

        // 通过正式业务入口修改六类个人状态，反向阵容避免把编号当作席位。
        profile.AddExperience(heroId, 135);
        profile.AddSoul(heroId, 21);
        Assert.True(profile.TryCollectEquipment(heroId, ring));
        Assert.True(profile.TryCollectEquipment(heroId, bagItem));
        Assert.True(equipment.TryEquip(heroId, ring.InstanceId));
        Assert.Equal(SkillLearnResult.Learned, skills.Learn(heroId, skillId));
        Assert.Equal(SkillLearnResult.Learned, skills.Upgrade(heroId, PassiveId.Vitality));

        int cost = TestTables.Tables.TbSkillConfig.Get(skillId).LearnSoul +
            TestTables.Tables.TbPassiveConfig.Get(PassiveId.Vitality).Levels[0].LearnSoul;
        Assert.Equal(135, scope.Profile.GetProgress(heroId).Value.TotalExperience);
        Assert.Equal(10021 - cost, scope.Profile.GetSoul(heroId).Value);
        Assert.Same(bagItem, Assert.Single(scope.Profile.GetBag(heroId)));
        Assert.Same(ring, scope.Profile.GetSlots(heroId).Value.Accessory);
        Assert.Equal(skillId, Assert.Single(scope.Skills.GetLearnedSkills(heroId)));
        Assert.Equal(1, scope.Skills.GetPassiveLevels(heroId)[PassiveId.Vitality]);
        Assert.Equal(untouched, JsonConvert.SerializeObject(scope.CaptureSaveData().Heroes.Single(hero => hero.HeroId == otherHeroId)));
        Assert.Empty(scope.Profile.GetBag(otherHeroId));
        Assert.Empty(scope.Skills.GetLearnedSkills(otherHeroId));
        Assert.Equal(0, scope.Skills.GetPassiveLevels(otherHeroId)[PassiveId.Vitality]);
    }

    /// <summary>
    /// v14 双人档案经捕获、JSON、完整校验和原地恢复后，席位与全部个人状态一致。
    /// </summary>
    /// <param name="firstHeroId">P1 英雄编号。</param>
    /// <param name="secondHeroId">P2 英雄编号。</param>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public void TwoHeroCheckpoint_RoundTripsAllPersonalData(int firstHeroId, int secondHeroId)
    {
        var scope = new TestGameScope([(firstHeroId, 0), (secondHeroId, 0)], 1000000, 3, initializeHero: false);
        scope.EnterMapContext();
        ProfileSystem profile = scope.GetSystem<ProfileSystem>();
        EquipmentSystem equipment = scope.GetSystem<EquipmentSystem>();
        SkillSystem skills = scope.GetSystem<SkillSystem>();
        foreach (int heroId in scope.Profile.HeroIds)
        {
            profile.AddExperience(heroId, TestTables.Curve.MaxTotalExperience - heroId);
            profile.AddSoul(heroId, heroId * 17);
            EquipmentId[] items =
            [
                heroId == 1 ? EquipmentId.RoughPilgrimStaff : EquipmentId.RoughPineStaff,
                heroId == 1 ? EquipmentId.RoughPilgrimClothes : EquipmentId.RoughCassock,
                EquipmentId.EarthFiendRing,
                heroId == 1 ? EquipmentId.DiamondSnare : EquipmentId.EastSeaBanner,
                EquipmentId.EarthFiendRing
            ];
            for (int index = 0; index < items.Length; index++)
            {
                EquipmentData item = equipment.Roll(Guid.NewGuid(), items[index], () => heroId * .25f);
                Assert.True(profile.TryCollectEquipment(heroId, item));
                if (index < 4)
                {
                    Assert.True(equipment.TryEquip(heroId, item.InstanceId));
                }
            }
            Assert.Equal(SkillLearnResult.Learned, skills.Learn(heroId,
                heroId == 1 ? SkillId.RisingDragon : SkillId.IceDragonWave));
            for (int level = 0; level < heroId; level++)
            {
                Assert.Equal(SkillLearnResult.Learned, skills.Upgrade(heroId, PassiveId.Vitality));
            }
        }
        Assert.Equal(SkillLearnResult.Learned, skills.Learn(2, SkillId.DarkIceFormation));
        profile.RecordLevelClear(1);
        Assert.True(profile.TryClaimEastSeaBanner());

        // 显式走完整转换链，不通过只比较单一英雄的便捷断言替代双人往返。
        GameData snapshot = scope.CaptureSaveData();
        string json = JsonConvert.SerializeObject(snapshot);
        GameData loaded = JsonConvert.DeserializeObject<GameData>(json);
        var mapper = new SaveDataUtility();
        mapper.Validate(loaded, TestTables.Tables.TbEquipmentConfig, 3, TestTables.Tables.TbSkillConfig,
            TestTables.Tables.TbLevelConfig, TestTables.Tables.TbPassiveConfig);
        var restored = new TestGameScope([(firstHeroId, 0), (secondHeroId, 0)], 0, 3, initializeHero: false);
        restored.RestoreCheckpoint(loaded);

        Assert.Equal(14, loaded.SaveVersion);
        Assert.Equal(new[] { firstHeroId, secondHeroId }, restored.Profile.HeroIds);
        Assert.Equal(firstHeroId, restored.Profile.HeroOf(PlayerSlot.P1));
        Assert.Equal(secondHeroId, restored.Profile.HeroOf(PlayerSlot.P2));
        Assert.Equal(json, JsonConvert.SerializeObject(restored.CaptureSaveData()));
        foreach (int heroId in restored.Profile.HeroIds)
        {
            Assert.Equal(scope.Profile.GetProgress(heroId).Value, restored.Profile.GetProgress(heroId).Value);
            Assert.Equal(scope.Profile.GetSoul(heroId).Value, restored.Profile.GetSoul(heroId).Value);
            Assert.Single(restored.Profile.GetBag(heroId));
            EquippedItems slots = restored.Profile.GetSlots(heroId).Value;
            Assert.NotNull(slots.Weapon);
            Assert.NotNull(slots.Armor);
            Assert.NotNull(slots.Accessory);
            Assert.NotNull(slots.Treasure);
            Assert.Equal(heroId, restored.Skills.GetPassiveLevels(heroId)[PassiveId.Vitality]);
        }
    }

    /// <summary>
    /// 第二人任一关键数据非法时，恢复在发布任何第一人状态前拒绝，且保留原模型与绑定。
    /// </summary>
    /// <param name="invalidField">第二人的非法快照字段。</param>
    [Theory]
    [InlineData("Soul")]
    [InlineData("Experience")]
    [InlineData("MissingItem")]
    [InlineData("DuplicateOwner")]
    [InlineData("Skill")]
    [InlineData("Passive")]
    public void RestoreCheckpoint_RejectsInvalidSecondHeroWithoutMutation(string invalidField)
    {
        var scope = new TestGameScope([(1, 0), (2, 0)], 10000, 3, initializeHero: false);
        ProfileSystem profile = scope.GetSystem<ProfileSystem>();
        EquipmentSystem equipment = scope.GetSystem<EquipmentSystem>();
        SkillSystem skills = scope.GetSystem<SkillSystem>();
        foreach (int heroId in scope.Profile.HeroIds)
        {
            EquipmentData ring = equipment.Roll(Guid.NewGuid(), EquipmentId.EarthFiendRing, () => .5f);
            Assert.True(profile.TryCollectEquipment(heroId, ring));
        }
        GameData snapshot = scope.CaptureSaveData();

        // 调用前状态故意不同于快照；若逐人提交后才发现第二人错误，第一人的变化就会被观察到。
        profile.AddExperience(1, 472);
        profile.AddSoul(1, 31);
        Assert.True(equipment.TryEquip(1, scope.Profile.GetBag(1)[0].InstanceId));
        Assert.Equal(SkillLearnResult.Learned, skills.Learn(1, SkillId.RisingDragon));
        Assert.Equal(SkillLearnResult.Learned, skills.Upgrade(1, PassiveId.Vitality));
        string before = JsonConvert.SerializeObject(scope.CaptureSaveData());
        BindableList<EquipmentData> bag = scope.Profile.GetBag(1);
        BindableProperty<int> soul = scope.Profile.GetSoul(1);
        int notifications = 0;
        scope.Profile.GetProgress(1).Register(_ => notifications++);
        soul.Register(_ => notifications++);
        bag.Register(_ => notifications++);
        scope.Profile.GetSlots(1).Register(_ => notifications++);
        scope.Skills.GetLearnedSkills(1).Register(_ => notifications++);

        HeroSaveData second = snapshot.Heroes[1];
        switch (invalidField)
        {
            case "Soul":
                second.Soul = -1;
                break;
            case "Experience":
                second.TotalExperience = -1;
                break;
            case "MissingItem":
                second.BagInstanceIds[0] = Guid.NewGuid();
                break;
            case "DuplicateOwner":
                second.BagInstanceIds.Add(snapshot.Heroes[0].BagInstanceIds[0]);
                break;
            case "Skill":
                second.LearnedSkills.Add(SkillId.RisingDragon);
                break;
            case "Passive":
                second.PassiveLevels[PassiveId.Vitality] = int.MaxValue;
                break;
        }
        if (invalidField is "Soul" or "Experience")
        {
            Assert.Throws<InvalidOperationException>(() => scope.RestoreCheckpoint(snapshot));
        }
        else
        {
            Assert.Throws<ArgumentException>(() => scope.RestoreCheckpoint(snapshot));
        }
        Assert.Equal(before, JsonConvert.SerializeObject(scope.CaptureSaveData()));
        Assert.Equal(0, notifications);
        Assert.Same(bag, scope.Profile.GetBag(1));
        Assert.Same(soul, scope.Profile.GetSoul(1));
    }

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
        scope.GetSystem<ProfileSystem>().AddSoul(2, 5);

        Assert.Throws<InvalidOperationException>(() => scope.RestoreCheckpoint(snapshot));
        Assert.Equal(new[] { 2, 1 }, scope.Profile.HeroIds);
        Assert.Equal(135, scope.Profile.GetProgress(2).Value.TotalExperience);
        Assert.Equal(281, scope.Profile.GetProgress(1).Value.TotalExperience);
        Assert.Equal(25, scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value);
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
        Assert.True(system.TryCollectEquipment(1, item));
        Assert.True(equipment.TryEquip(1, item.InstanceId));
        if (otherHero)
        {
            // 测试夹具把完整穿戴交给另一英雄，验证排重不局限于当前出战者。
            scope.Profile.GetSlots(2).Value = scope.Profile.GetSlots(scope.Profile.HeroOf(PlayerSlot.P1)).Value;
            scope.Profile.GetSlots(scope.Profile.HeroOf(PlayerSlot.P1)).Value = default;
        }

        Assert.False(system.TryCollectEquipment(1, item));
        Assert.Empty(scope.Profile.GetBag(scope.Profile.HeroOf(PlayerSlot.P1)));
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
        Assert.Empty(restored.Profile.GetBag(restored.Profile.HeroOf(PlayerSlot.P1)));
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
        Assert.Equal(EquipmentGrantResult.Collected, system.GrantUniqueEquipment(1, EquipmentId.DiamondSnare,
            () => { samples++; return .37f; }));
        EquipmentData item = Assert.Single(scope.Profile.GetBag(scope.Profile.HeroOf(PlayerSlot.P1)));
        Assert.True(samples > 0);
        int rolled = samples;

        Assert.True(scope.GetSystem<EquipmentSystem>().TryEquip(1, item.InstanceId));
        Assert.Equal(EquipmentGrantResult.AlreadyOwned, system.GrantUniqueEquipment(1, EquipmentId.DiamondSnare,
            () => { samples++; return .99f; }));
        Assert.Equal(rolled, samples);
        Assert.Same(item, scope.Profile.GetSlots(scope.Profile.HeroOf(PlayerSlot.P1)).Value.Treasure);
        Assert.Empty(scope.Profile.GetBag(scope.Profile.HeroOf(PlayerSlot.P1)));

        scope.RestoreCheckpoint(snapshot);
        Assert.False(scope.Profile.HasEquipment(EquipmentId.DiamondSnare));
        Assert.Equal(EquipmentGrantResult.Collected, system.GrantUniqueEquipment(1, EquipmentId.DiamondSnare, () => .5f));
        Assert.NotEqual(item.InstanceId, Assert.Single(scope.Profile.GetBag(scope.Profile.HeroOf(PlayerSlot.P1))).InstanceId);
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
        Assert.True(system.TryCollectEquipment(1, item));
        Assert.Equal(EquipmentGrantResult.InventoryFull, system.GrantUniqueEquipment(1, EquipmentId.DiamondSnare,
            () => throw new InvalidOperationException("满包不应抽取属性。")));
        Assert.Same(item, Assert.Single(scope.Profile.GetBag(scope.Profile.HeroOf(PlayerSlot.P1))));

        Assert.True(equipment.TryEquip(1, item.InstanceId));
        Assert.Equal(EquipmentGrantResult.Collected, system.GrantUniqueEquipment(1, EquipmentId.DiamondSnare, () => .5f));
        Assert.Equal(EquipmentId.DiamondSnare, Assert.Single(scope.Profile.GetBag(scope.Profile.HeroOf(PlayerSlot.P1))).EquipmentId);
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
        BindableList<EquipmentData> items = scope.Profile.GetBag(scope.Profile.HeroOf(PlayerSlot.P1));
        scope.GetSystem<ProfileSystem>().TryCollectEquipment(1, first);
        Assert.True(equipment.TryEquip(1, first.InstanceId));
        scope.GetSystem<ProfileSystem>().TryCollectEquipment(1, second);
        int notifications = 0;
        items.Register(newItems =>
        {
            notifications++;
            Assert.Same(first, Assert.Single(newItems));
            Assert.Same(first, Assert.Single(items));
        });

        Assert.True(equipment.TryEquip(1, second.InstanceId));
        Assert.Same(second, scope.Profile.GetSlots(scope.Profile.HeroOf(PlayerSlot.P1)).Value.Weapon);
        Assert.Same(items, scope.Profile.GetBag(scope.Profile.HeroOf(PlayerSlot.P1)));
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
        scope.GetSystem<ProfileSystem>().TryCollectEquipment(1, staff);
        GameData snapshot = scope.CaptureSaveData();
        Assert.True(equipment.TryEquip(1, staff.InstanceId));
        scope.GetSystem<ProfileSystem>().TryCollectEquipment(1, equipment.Roll(Guid.NewGuid(), EquipmentId.CommonPilgrimStaff, () => 0));
        scope.GetSystem<ProfileSystem>().AddSoul(1, 100);
        scope.GetSystem<ProfileSystem>().AddExperience(1, 281);
        BindableProperty<HeroProgressionState> progress = scope.Profile.GetProgress(scope.Profile.HeroOf(PlayerSlot.P1));
        BindableList<EquipmentData> items = scope.Profile.GetBag(scope.Profile.HeroOf(PlayerSlot.P1));
        BindableProperty<EquippedItems> slots = scope.Profile.GetSlots(scope.Profile.HeroOf(PlayerSlot.P1));
        int notifications = 0;
        progress.Register(value => { notifications++; Assert.Equal(0, value.TotalExperience); });
        slots.Register(value => { notifications++; Assert.Null(value.Weapon); Assert.Null(value.Armor); });
        scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Register(value => { notifications++; Assert.Equal(20, value); });
        items.Register(value => { notifications++; Assert.Equal(staff.InstanceId, Assert.Single(value).InstanceId); });

        scope.RestoreCheckpoint(snapshot);
        Assert.Equal(4, notifications);
        Assert.Equal(0, progress.Value.TotalExperience);
        Assert.Equal(20, scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value);
        Assert.Equal(staff.InstanceId, Assert.Single(items).InstanceId);
        Assert.Null(slots.Value.Weapon);
        Assert.Equal(TestTables.Tables.TbHeroGrowthConfig.Get(1).Levels[0].BaseAttributes.Power, scope.Hero.Power.Value);
        Assert.Equal(1, scope.Hero.Level.Value);
        Assert.Same(progress, scope.Profile.GetProgress(scope.Profile.HeroOf(PlayerSlot.P1)));
        Assert.Same(items, scope.Profile.GetBag(scope.Profile.HeroOf(PlayerSlot.P1)));
        Assert.Same(slots, scope.Profile.GetSlots(scope.Profile.HeroOf(PlayerSlot.P1)));
    }
}
