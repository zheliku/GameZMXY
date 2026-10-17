using System;
using System.Linq;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Skills;
using Newtonsoft.Json;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>通过正式系统验证学习资格、扣费、可绑定通知、快照和严格存档校验。</summary>
public sealed class SkillLearningTests
{
    /// <summary>初始未学；余额不足不变，刚好足够成功一次，重复点击不重复扣费。</summary>
    [Fact]
    public void Learning_ChargesOnceAndPublishesTheSharedSkillBook()
    {
        var scope = new TestGameScope(1, [(1, 0)], 99, 20);
        var system = scope.GetSystem<SkillSystem>();
        var learned = scope.Skills.GetLearnedSkills(1);
        int notifications = 0;
        learned.Register(_ => notifications++);
        Assert.False(system.IsLearned(SkillId.RisingDragon));
        Assert.Equal(SkillLearnResult.InsufficientSoul, system.Learn(SkillId.RisingDragon));
        Assert.Equal(99, scope.Profile.Soul.Value);
        Assert.Empty(learned);
        Assert.Equal(0, notifications);
        scope.GetSystem<ProfileSystem>().AddSoul(1);
        Assert.Equal(SkillLearnResult.Learned, system.Learn(SkillId.RisingDragon));
        Assert.Equal(0, scope.Profile.Soul.Value);
        Assert.True(system.IsLearned(SkillId.RisingDragon));
        Assert.Equal(SkillLearnResult.AlreadyLearned, system.Learn(SkillId.RisingDragon));
        Assert.Equal(0, scope.Profile.Soul.Value);
        Assert.Equal(1, notifications);
    }

    /// <summary>其他角色、未知编号和未实现的角色动作不能购买。</summary>
    [Theory]
    [InlineData(SkillId.IceDragonWave)]
    [InlineData((SkillId)999)]
    public void Learning_RejectsUnavailableDefinitions(SkillId id)
    {
        var scope = new TestGameScope(1, [(1, 0)], 9999, 20);
        Assert.Equal(SkillLearnResult.Unavailable, scope.GetSystem<SkillSystem>().Learn(id));
        Assert.Equal(9999, scope.Profile.Soul.Value);
        Assert.Empty(scope.Skills.GetLearnedSkills(1));
    }

    /// <summary>技能书按英雄保存、快照独立；放弃关卡同时回滚扣费和学习，保留绑定身份。</summary>
    [Fact]
    public void SaveAndRollback_PreserveHeroOwnershipAndBindingIdentity()
    {
        var scope = new TestGameScope(1, [(1, 0), (2, 0)], 3000, 20);
        var learned = scope.Skills.GetLearnedSkills(1);
        Assert.Equal(SkillLearnResult.Learned, scope.GetSystem<SkillSystem>().Learn(SkillId.FlameThrust));
        var checkpoint = scope.CaptureSaveData();
        Assert.Equal(new[] { SkillId.FlameThrust }, checkpoint.Heroes.Single(x => x.HeroId == 1).LearnedSkills);
        Assert.Empty(checkpoint.Heroes.Single(x => x.HeroId == 2).LearnedSkills);
        var reloaded = TestGameScope.FromSaveData(JsonConvert.DeserializeObject<GameData>(JsonConvert.SerializeObject(checkpoint)),
            TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 20);
        Assert.True(reloaded.GetSystem<SkillSystem>().IsLearned(SkillId.FlameThrust));
        Assert.Equal(SkillLearnResult.Learned, scope.GetSystem<SkillSystem>().Learn(SkillId.BlazingFlash));
        Assert.DoesNotContain(SkillId.BlazingFlash, checkpoint.Heroes[0].LearnedSkills);
        scope.RestoreCheckpoint(checkpoint);
        Assert.Same(learned, scope.Skills.GetLearnedSkills(1));
        Assert.Equal(new[] { SkillId.FlameThrust }, learned);
        Assert.Equal(2800, scope.Profile.Soul.Value);
    }

    /// <summary>坏学习记录在恢复前失败，原档案数据不变。</summary>
    /// <param name="invalid">重复、其他英雄或未知技能。</param>
    [Theory]
    [InlineData(SkillId.RisingDragon)]
    [InlineData(SkillId.IceDragonWave)]
    [InlineData((SkillId)999)]
    public void Restore_RejectsInvalidSkillRecordsBeforeWriting(SkillId invalid)
    {
        var scope = new TestGameScope(1, [(1, 0)], 3000, 20);
        var snapshot = scope.CaptureSaveData();
        snapshot.Heroes[0].LearnedSkills.AddRange([SkillId.RisingDragon, invalid]);
        snapshot.Soul = 0;
        Assert.Throws<ArgumentException>(() => scope.RestoreCheckpoint(snapshot));
        Assert.Equal(3000, scope.Profile.Soul.Value);
        Assert.Empty(scope.Skills.GetLearnedSkills(1));
    }

    /// <summary>当前格式必须显式提供学习记录，缺字段不能被空列表默认值掩盖。</summary>
    [Fact]
    public void Save_RequiresAnExplicitSkillBook()
    {
        var scope = new TestGameScope(1, [(1, 0)], 0, 20);
        var json = Newtonsoft.Json.Linq.JObject.FromObject(scope.CaptureSaveData());
        ((Newtonsoft.Json.Linq.JObject)json["Heroes"][0]).Remove("LearnedSkills");
        Assert.Throws<JsonSerializationException>(() => json.ToObject<GameData>());
    }
}
