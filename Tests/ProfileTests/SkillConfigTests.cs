using GameLogic.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Skills;
using GameConfig.Sound;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>读取正式技能表，锁定造一的目录、学习费用与被动总加成语义。</summary>
public sealed class SkillConfigTests
{
    /// <summary>两个角色各五项主动，费用和耗蓝直接对应原版 Role1/Role2。</summary>
    [Fact]
    public void ActiveSkills_PreserveOfficialOrderCostAndMana()
    {
        var tables = TestTables.Tables;
        var configuration = new ConfigurationModel(tables);
        var wukong = configuration.GetActive(SkillHero.Wukong);
        var tangseng = configuration.GetActive(SkillHero.Tangseng);
        Assert.Equal(new[] { "升龙斩", "火炎突击", "烈焰闪", "烈焰风暴", "火魔斩" }, wukong.Select(x => x.NameCn));
        Assert.Equal(new[] { "冰龙波", "玄冰阵", "水幻影", "天降甘露", "水魔爆" }, tangseng.Select(x => x.NameCn));
        foreach (var skills in new[] { wukong, tangseng })
        {
            Assert.Equal(new[] { 100, 200, 500, 1000, 2000 }, skills.Select(x => x.LearnSoul));
            Assert.Equal(Enumerable.Range(1, 5), skills.Select(x => x.Order));
        }
        Assert.Equal(new[] { 10, 20, 30, 40, 50 }, wukong.Select(x => x.ManaCost));
        Assert.Equal(new[] { 20, 40, 30, 60, 100 }, tangseng.Select(x => x.ManaCost));
    }

    /// <summary>被动各级记录总加成；暴击为直接概率，不能误作项目中的属性值。</summary>
    [Fact]
    public void Passives_PreserveFiveLevelsAndAbsoluteChance()
    {
        var table = TestTables.Tables.TbPassiveConfig;
        Assert.Equal(25, table.DataList.Count);
        foreach (PassiveId id in Enum.GetValues<PassiveId>())
        {
            var levels = table.DataList.Where(x => x.PassiveId == id).OrderBy(x => x.Level).ToArray();
            Assert.Equal(new[] { 5000, 10000, 15000, 20000, 25000 }, levels.Select(x => x.LearnSoul));
            for (int i = 0; i < levels.Length; i++)
            {
                float expected = id switch
                {
                    PassiveId.Vitality or PassiveId.ManaSpring => (i + 2) * 100,
                    PassiveId.Frenzy => (i + 2) / 100f,
                    _ => i + 2
                };
                Assert.Equal(expected, levels[i].Value);
            }
        }
        Assert.All(table.DataList.Where(x => x.PassiveId == PassiveId.Frenzy),
            level => Assert.Equal(PassiveEffect.CriticalChance, level.Effect));
    }

    /// <summary>完整正式目录通过同一套启动校验。</summary>
    [Fact]
    public void PreparedCatalog_PassesStartupValidation()
    {
        List<string> errors = new();
        ConfigurationSystem.ValidateSkillCatalog(TestTables.Tables.TbSkillConfig, TestTables.Tables.TbPassiveConfig, errors);
        Assert.Empty(errors);
    }

    /// <summary>原版七段技能音效都登记到 GGF SFX，保留来源并使用独立文件。</summary>
    [Fact]
    public void SkillSounds_UseGgfSfxGroupAndDistinctOriginalFiles()
    {
        var sounds = Enumerable.Range(7, 7).Select(id => TestTables.Tables.TbSoundConfig.Get(id)).ToArray();
        Assert.All(sounds, sound =>
        {
            Assert.Equal("SFX", sound.Group);
            Assert.StartsWith("official.Music.Role1_hit", sound.LegacyId);
            Assert.StartsWith("res://TheGame/Audios/SFX/wukong/skills/", sound.Path);
            Assert.Equal(((SoundId)sound.Id).ToString(), sound.Key);
        });
        Assert.Equal(7, sounds.Select(sound => sound.Path).Distinct().Count());
    }
}
