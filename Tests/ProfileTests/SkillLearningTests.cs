using System;
using System.Linq;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Level;
using GameLogic.Save.Data;
using GameLogic.Skills;
using Newtonsoft.Json;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 通过正式系统验证学习资格、扣费、可绑定通知、快照和严格存档校验。
/// </summary>
public sealed class SkillLearningTests
{
    /// <summary>
    /// 初始未学；余额不足不变，刚好足够成功一次，重复点击不重复扣费。
    /// </summary>
    [Fact]
    public void Learning_ChargesOnceAndPublishesTheSharedSkillBook()
    {
        var scope = new TestGameScope([(1, 0)], 99, 20);
        SkillSystem system = scope.GetSystem<SkillSystem>();
        BindableList<SkillId> learned = scope.Skills.GetLearnedSkills(1);
        int notifications = 0;
        learned.Register(_ => notifications++);
        Assert.False(system.IsLearned(1, SkillId.RisingDragon));
        Assert.Equal(SkillLearnResult.InsufficientSoul, system.Learn(1, SkillId.RisingDragon));
        Assert.Equal(99, scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value);
        Assert.Empty(learned);
        Assert.Equal(0, notifications);
        scope.GetSystem<ProfileSystem>().AddSoul(1, 1);
        Assert.Equal(SkillLearnResult.Learned, system.Learn(1, SkillId.RisingDragon));
        Assert.Equal(0, scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value);
        Assert.True(system.IsLearned(1, SkillId.RisingDragon));
        Assert.Equal(SkillLearnResult.AlreadyLearned, system.Learn(1, SkillId.RisingDragon));
        Assert.Equal(0, scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value);
        Assert.Equal(1, notifications);
    }

    /// <summary>
    /// 地图没有英雄与关卡模型时仍可购买技能；档案保存后下一局读取主动和被动效果。
    /// </summary>
    [Fact]
    public void MapPurchase_UsesProfileSkillsWithoutBattleModelsAndSurvivesReload()
    {
        var scope = new TestGameScope([(1, 0)], 10000, 20, initializeHero: false);
        // 与离关顺序一致：本局模型仍在时清登记，再进入仅档案的地图作用域。
        scope.GetSystem<SkillSystem>().ClearSkillDefinitions(1);
        scope.EnterMapContext();
        Assert.Null(scope.GetModel<HeroModel>());
        Assert.Null(scope.GetModel<LevelModel>());
        SkillSystem system = scope.GetSystem<SkillSystem>();
        int skillCost = TestTables.Tables.TbSkillConfig.Get(SkillId.RisingDragon).LearnSoul;
        int passiveCost = TestTables.Tables.TbPassiveConfig.Get(PassiveId.Vitality).Levels[0].LearnSoul;

        Assert.False(system.IsAvailable(1, SkillId.RisingDragon));
        Assert.True(system.IsLearnable(1, SkillId.RisingDragon));
        Assert.Equal(SkillLearnResult.Learned, scope.SendCommand(new LearnSkillCommand(1, SkillId.RisingDragon)));
        Assert.Equal(SkillLearnResult.Learned, scope.SendCommand(new UpgradePassiveCommand(1, PassiveId.Vitality)));
        Assert.Equal(10000 - skillCost - passiveCost, scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value);

        GameData save = scope.CaptureSaveData();
        HeroSaveData heroData = save.Heroes.Single(hero => hero.HeroId == 1);
        Assert.Contains(SkillId.RisingDragon, heroData.LearnedSkills);
        Assert.Equal(1, heroData.PassiveLevels[PassiveId.Vitality]);

        var reloaded = TestGameScope.FromSaveData(save, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 20);
        Assert.True(reloaded.GetSystem<SkillSystem>().IsLearned(1, SkillId.RisingDragon));
        Assert.True(reloaded.GetSystem<SkillSystem>().IsAvailable(1, SkillId.RisingDragon));
        Assert.True(reloaded.Hero.MaxHp.Value > TestTables.Tables.TbHeroGrowthConfig.Get(1).Levels[0].BaseAttributes.MaxHp);
    }

    /// <summary>
    /// 其他角色、未知编号和未实现的角色动作不能购买。
    /// </summary>
    /// <param name="id">应被拒绝学习的技能配置编号。</param>
    [Theory]
    [InlineData(SkillId.IceDragonWave)]
    [InlineData((SkillId)999)]
    public void Learning_RejectsUnavailableDefinitions(SkillId id)
    {
        var scope = new TestGameScope([(1, 0)], 9999, 20);
        Assert.Equal(SkillLearnResult.Unavailable, scope.GetSystem<SkillSystem>().Learn(1, id));
        Assert.Equal(9999, scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value);
        Assert.Empty(scope.Skills.GetLearnedSkills(1));
    }

    /// <summary>
    /// 技能书按英雄保存、快照独立；放弃关卡同时回滚扣费和学习，保留绑定身份。
    /// </summary>
    [Fact]
    public void SaveAndRollback_PreserveHeroOwnershipAndBindingIdentity()
    {
        var scope = new TestGameScope([(1, 0), (2, 0)], 3000, 20);
        BindableList<SkillId> learned = scope.Skills.GetLearnedSkills(1);
        Assert.Equal(SkillLearnResult.Learned, scope.GetSystem<SkillSystem>().Learn(1, SkillId.FlameThrust));
        GameData checkpoint = scope.CaptureSaveData();
        Assert.Equal(new[] { SkillId.FlameThrust }, checkpoint.Heroes.Single(x => x.HeroId == 1).LearnedSkills);
        Assert.Empty(checkpoint.Heroes.Single(x => x.HeroId == 2).LearnedSkills);
        var reloaded = TestGameScope.FromSaveData(JsonConvert.DeserializeObject<GameData>(JsonConvert.SerializeObject(checkpoint)),
            TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 20);
        Assert.True(reloaded.GetSystem<SkillSystem>().IsLearned(1, SkillId.FlameThrust));
        Assert.Equal(SkillLearnResult.Learned, scope.GetSystem<SkillSystem>().Learn(1, SkillId.BlazingFlash));
        Assert.DoesNotContain(SkillId.BlazingFlash, checkpoint.Heroes[0].LearnedSkills);
        scope.RestoreCheckpoint(checkpoint);
        Assert.Same(learned, scope.Skills.GetLearnedSkills(1));
        Assert.Equal(new[] { SkillId.FlameThrust }, learned);
        Assert.Equal(2800, scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value);
    }

    /// <summary>
    /// 坏学习记录在恢复前失败，原档案数据不变。
    /// </summary>
    /// <param name="invalid">重复、其他英雄或未知技能。</param>
    [Theory]
    [InlineData(SkillId.RisingDragon)]
    [InlineData(SkillId.IceDragonWave)]
    [InlineData((SkillId)999)]
    public void Restore_RejectsInvalidSkillRecordsBeforeWriting(SkillId invalid)
    {
        var scope = new TestGameScope([(1, 0)], 3000, 20);
        GameData snapshot = scope.CaptureSaveData();
        snapshot.Heroes[0].LearnedSkills.AddRange([SkillId.RisingDragon, invalid]);
        snapshot.Heroes[0].Soul = 0;
        Assert.Throws<ArgumentException>(() => scope.RestoreCheckpoint(snapshot));
        Assert.Equal(3000, scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value);
        Assert.Empty(scope.Skills.GetLearnedSkills(1));
    }

    /// <summary>
    /// 当前格式必须显式提供学习记录，缺字段不能被空列表默认值掩盖。
    /// </summary>
    [Fact]
    public void Save_RequiresAnExplicitSkillBook()
    {
        var scope = new TestGameScope([(1, 0)], 0, 20);
        var json = Newtonsoft.Json.Linq.JObject.FromObject(scope.CaptureSaveData());
        ((Newtonsoft.Json.Linq.JObject)json["Heroes"][0]).Remove("LearnedSkills");
        Assert.Throws<JsonSerializationException>(() => json.ToObject<GameData>());
    }
}
