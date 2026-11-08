using System;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Skills;
using Newtonsoft.Json;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 被动购买的费用、总加成、英雄归属、绑定及完整保存/回滚。
/// </summary>
public sealed class PassiveSkillTests
{
    /// <summary>
    /// 当前等级贡献为总值，升2级只增加100上限并保留原有生命缺损。
    /// </summary>
    [Fact]
    public void Upgrade_ChargesNextLevelAndReplacesTotalBonus()
    {
        var scope = new TestGameScope([(1, 0)], 15000, 25);
        SkillSystem skill = scope.GetSystem<SkillSystem>();
        scope.GetSystem<HeroSystem>().Damage(30);
        Assert.Equal(SkillLearnResult.Learned, skill.Upgrade(1, PassiveId.Vitality));
        Assert.Equal((280, 250, 10000, 1), (scope.Hero.MaxHp.Value, scope.Hero.Hp.Value,
            scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value, scope.Skills.GetPassiveLevels(1)[PassiveId.Vitality]));
        Assert.Equal(SkillLearnResult.Learned, skill.Upgrade(1, PassiveId.Vitality));
        Assert.Equal((380, 350, 0, 2), (scope.Hero.MaxHp.Value, scope.Hero.Hp.Value,
            scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value, scope.Skills.GetPassiveLevels(1)[PassiveId.Vitality]));
    }

    /// <summary>
    /// 余额不足、满级与未知编号拒绝扣费，五级总价按官方每级递增。
    /// </summary>
    [Fact]
    public void Upgrade_RejectsInsufficientBalanceMaximumAndUnknownId()
    {
        var scope = new TestGameScope([(1, 0)], 4999, 25);
        SkillSystem skill = scope.GetSystem<SkillSystem>();
        Assert.Equal(SkillLearnResult.InsufficientSoul, skill.Upgrade(1, PassiveId.Vitality));
        Assert.Equal((4999, 0, 80), (scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value,
            scope.Skills.GetPassiveLevels(1)[PassiveId.Vitality], scope.Hero.MaxHp.Value));
        scope.GetSystem<ProfileSystem>().AddSoul(1, 75000 - 4999);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(SkillLearnResult.Learned, skill.Upgrade(1, PassiveId.Vitality));
        }

        Assert.Equal((0, 680, 5), (scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value, scope.Hero.MaxHp.Value,
                    scope.Skills.GetPassiveLevels(1)[PassiveId.Vitality]));
        Assert.Equal(SkillLearnResult.AlreadyLearned, skill.Upgrade(1, PassiveId.Vitality));
        Assert.Equal(SkillLearnResult.Unavailable, skill.Upgrade(1, (PassiveId)999));
        Assert.Equal(0, scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value);
    }

    /// <summary>
    /// 五种效果进入同一英雄属性汇总，保存重读和关卡重开不丢失、不叠加。
    /// </summary>
    [Fact]
    public void AllEffects_SurviveSaveRestartAndRemainHeroOwned()
    {
        var scope = new TestGameScope([(1, 0), (2, 0)], 25000, 25);
        foreach (PassiveId id in Enum.GetValues<PassiveId>())
        {
            Assert.Equal(SkillLearnResult.Learned, scope.GetSystem<SkillSystem>().Upgrade(1, id));
        }

        Assert.Equal((280, 250, .02f, 2, 2), (scope.Hero.MaxHp.Value, scope.Hero.MaxMp.Value,
                    scope.Hero.Crit.Value, scope.Hero.HpRegen.Value, scope.Hero.MpRegen.Value));
        Assert.All(scope.Skills.GetPassiveLevels(2).Values, level => Assert.Equal(0, level));
        scope.RestartHero();
        Assert.Equal(280, scope.Hero.MaxHp.Value);
        GameData saved = JsonConvert.DeserializeObject<GameData>(JsonConvert.SerializeObject(scope.CaptureSaveData()));
        var loaded = TestGameScope.FromSaveData(saved, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
        Assert.Equal((280, 250, .02f, 2, 2), (loaded.Hero.MaxHp.Value, loaded.Hero.MaxMp.Value,
            loaded.Hero.Crit.Value, loaded.Hero.HpRegen.Value, loaded.Hero.MpRegen.Value));
    }

    /// <summary>
    /// 回滚原地恢复等级与扣费，原字典仍被订阅，快照不会跟随之后的升级变化。
    /// </summary>
    [Fact]
    public void Rollback_RestoresLevelsAndBalanceUsingSameBinding()
    {
        var scope = new TestGameScope([(1, 0)], 15000, 25);
        BindableDictionary<PassiveId, int> levels = scope.Skills.GetPassiveLevels(1);
        GameData checkpoint = scope.CaptureSaveData();
        scope.GetSystem<SkillSystem>().Upgrade(1, PassiveId.Vitality);
        Assert.Equal(0, checkpoint.Heroes[0].PassiveLevels[PassiveId.Vitality]);
        scope.RestoreCheckpoint(checkpoint);
        Assert.Same(levels, scope.Skills.GetPassiveLevels(1));
        Assert.Equal((0, 15000, 80), (levels[PassiveId.Vitality], scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value, scope.Hero.MaxHp.Value));
    }

    /// <summary>
    /// 坏被动记录在任何写入前失败；玩家命令还必须满足当前关卡资格。
    /// </summary>
    /// <param name="level">本次测试使用的等级或被动技能等级。</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void InvalidLevels_RejectBeforeWriting(int level)
    {
        var scope = new TestGameScope([(1, 0)], 10000, 25);
        GameData data = scope.CaptureSaveData();
        data.Heroes[0].PassiveLevels[PassiveId.Vitality] = level;
        Assert.Throws<ArgumentException>(() => scope.RestoreCheckpoint(data));
        Assert.Equal(0, scope.Skills.GetPassiveLevels(1)[PassiveId.Vitality]);
        Assert.Equal(SkillLearnResult.Unavailable, scope.SendCommand(new UpgradePassiveCommand(1, PassiveId.Vitality)));
        Assert.Equal(10000, scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value);
    }
}
