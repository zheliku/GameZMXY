using GameLogic.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameConfig;
using GameConfig.Battle;
using GameConfig.Skills;
using GameConfig.Sound;
using Godot;
using Luban;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>从正式 Luban 产物核对统一伤害数据与技能平衡参数。</summary>
public sealed class SkillCombatConfigTests
{
    /// <summary>原版每个伤害键的威力、伤害类型、秒制击退与重复命中参数分别保留。</summary>
    /// <param name="id">阶段标识。</param>
    /// <param name="scale">原版攻击倍率。</param>
    /// <param name="flat">原版固定威力。</param>
    /// <param name="kind">伤害类型。</param>
    /// <param name="backX">源水平击退系数。</param>
    /// <param name="backY">源竖直击退系数。</param>
    /// <param name="budget">攻击源的总命中预算。</param>
    /// <param name="sourceInterval">24 fps 源时间轴的编号更新间隔。</param>
    [Theory]
    [InlineData(SkillHitId.RisingDragon, 2f, 0f, DamageKind.Magic, 2f, -22f, 100, 2)]
    [InlineData(SkillHitId.FlameThrust, .5f, 0f, DamageKind.Magic, 35f, -2f, 1, 5)]
    [InlineData(SkillHitId.BlazingFlash, 0f, 100f, DamageKind.Magic, 0f, -2f, 1, 4)]
    [InlineData(SkillHitId.FlameStorm, 1.2f, 0f, DamageKind.Magic, 20f, -2f, 100, 2)]
    [InlineData(SkillHitId.FireDemonCombo, 1f, 0f, DamageKind.Physics, 2f, -3f, 100, 999)]
    [InlineData(SkillHitId.FireDemonDescent, 1f, 0f, DamageKind.Magic, 40f, 25f, 100, 999)]
    [InlineData(SkillHitId.FireDemonLanding, 3f, 0f, DamageKind.Magic, 40f, -15f, 100, 999)]
    [InlineData(SkillHitId.IceDragonWave, .2f, 0f, DamageKind.Magic, 20f, -5f, 200, 4)]
    [InlineData(SkillHitId.DarkIceFormation, .8f, 0f, DamageKind.Magic, 6f, -7f, 200, 999)]
    [InlineData(SkillHitId.WaterDemonPillar, 2f, 0f, DamageKind.Magic, 25f, -5f, 200, 999)]
    [InlineData(SkillHitId.WaterDemonFall, 8f, 0f, DamageKind.Magic, 25f, -5f, 200, 999)]
    public void DamageStages_PreserveOfficialRules(SkillHitId id, float scale, float flat,
        DamageKind kind, float backX, float backY, int budget, int sourceInterval)
    {
        var hit = TestTables.Tables.TbAttackConfig.Get((int)id);
        Assert.Equal(new Vector2(scale, scale), hit.PowerScale);
        Assert.Equal(new Vector2(flat, flat), hit.FlatPower);
        Assert.Equal(kind, hit.DamageKind);
        Assert.Equal(new Vector2(backX * 24, backY * 24), hit.Knockback);
        Assert.Equal(budget, hit.HitBudget);
        Assert.Equal(sourceInterval / 24f, hit.RepeatIntervalSeconds, .000001f);
    }

    /// <summary>技能伤害不能混进实体的普通攻击段列表。</summary>
    [Fact]
    public void NormalAttackIndex_ExcludesSkillDamageStages()
    {
        Assert.Empty(TestTables.Configuration.GetAttacks(GameConfig.Entity.EntityId.None));
        foreach (var owner in TestTables.Tables.TbAttackConfig.DataList.Where(x => x.ComboIndex >= 0).Select(x => x.OwnerId).Distinct())
            Assert.All(TestTables.Configuration.GetAttacks(owner), attack => Assert.True(attack.ComboIndex >= 0));
    }

    /// <summary>保留实际平衡参数；无敌开始时点归原生动画。</summary>
    [Fact]
    public void SkillBalance_ContainsDurationsAndCancelRatio()
    {
        var table = TestTables.Tables.TbSkillConfig;
        Assert.Equal(.5f, table.Get(SkillId.BlazingFlash).InvincibleDurationSeconds);
        Assert.Equal(70f / 24, table.Get(SkillId.FireDemonSlash).InvincibleDurationSeconds, .000001f);
        Assert.Equal(.7f, table.Get(SkillId.RisingDragon).CancelNormalAttackFrameRatio);
    }

    /// <summary>正式表通过同一套启动校验。</summary>
    [Fact]
    public void PreparedCombatTables_PassStartupValidation()
    {
        List<string> errors = new();
        ConfigurationSystem.ValidateSkillCombat(TestTables.Tables, errors);
        Assert.Empty(errors);
    }

    /// <summary>无效攻击数值与技能平衡参数须在预加载拒绝。</summary>
    [Fact]
    public void StartupValidation_RejectsInvalidHitAndDuration()
    {
        var tables = new Tables(name => new ByteBuf(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Data", name + ".bytes"))));
        var skill = tables.TbSkillConfig.Get(SkillId.BlazingFlash);
        typeof(SkillConfig).GetField(nameof(skill.InvincibleDurationSeconds)).SetValue(skill, -1f);
        var attack = tables.TbAttackConfig.Get((int)SkillHitId.FlameStorm);
        typeof(AttackConfig).GetField(nameof(attack.HitBudget)).SetValue(attack, 0);
        List<string> errors = new();
        ConfigurationSystem.ValidateSkillCombat(tables, errors);
        Assert.Contains(errors, error => error.Contains("无敌"));
        Assert.Contains(errors, error => error.Contains("命中预算"));
    }
}
