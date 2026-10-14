using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameConfig;
using GameConfig.Battle;
using GameConfig.Skills;
using GameConfig.Sound;
using GameLogic.Skills;
using Godot;
using Luban;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>从正式 Luban 产物核对造一技能阶段，以及动作、特效和声音的关联。</summary>
public sealed class SkillCombatConfigTests
{
    /// <summary>原版每个伤害键的威力、舍入顺序、伤害类型与重复命中参数分别保留。</summary>
    /// <param name="id">阶段标识。</param>
    /// <param name="scale">原版攻击倍率。</param>
    /// <param name="flat">原版固定威力。</param>
    /// <param name="rounding">无双倍率之前的舍入顺序。</param>
    /// <param name="kind">伤害类型。</param>
    /// <param name="backX">源水平击退系数。</param>
    /// <param name="backY">源竖直击退系数。</param>
    /// <param name="budget">攻击源的总命中预算。</param>
    /// <param name="sourceInterval">24 fps 源时间轴的编号更新间隔。</param>
    [Theory]
    [InlineData(SkillHitId.RisingDragon, 2f, 0f, SkillPowerRounding.None, DamageKind.Magic, 2f, -22f, 100, 2)]
    [InlineData(SkillHitId.FlameThrust, .5f, 0f, SkillPowerRounding.AfterScale, DamageKind.Magic, 35f, -2f, 1, 5)]
    [InlineData(SkillHitId.BlazingFlash, 0f, 100f, SkillPowerRounding.None, DamageKind.Magic, 0f, -2f, 1, 4)]
    [InlineData(SkillHitId.FlameStorm, 1.2f, 0f, SkillPowerRounding.AfterScale, DamageKind.Magic, 20f, -2f, 100, 2)]
    [InlineData(SkillHitId.FireDemonCombo, 1f, 0f, SkillPowerRounding.BeforeScale, DamageKind.Physics, 2f, -3f, 100, 999)]
    [InlineData(SkillHitId.FireDemonDescent, 1f, 0f, SkillPowerRounding.BeforeScale, DamageKind.Magic, 40f, 25f, 100, 999)]
    [InlineData(SkillHitId.FireDemonLanding, 3f, 0f, SkillPowerRounding.BeforeScale, DamageKind.Magic, 40f, -15f, 100, 999)]
    [InlineData(SkillHitId.IceDragonWave, .2f, 0f, SkillPowerRounding.None, DamageKind.Magic, 20f, -5f, 200, 4)]
    [InlineData(SkillHitId.DarkIceFormation, .8f, 0f, SkillPowerRounding.None, DamageKind.Magic, 6f, -7f, 200, 999)]
    [InlineData(SkillHitId.WaterDemonPillar, 2f, 0f, SkillPowerRounding.None, DamageKind.Magic, 25f, -5f, 200, 999)]
    [InlineData(SkillHitId.WaterDemonFall, 8f, 0f, SkillPowerRounding.None, DamageKind.Magic, 25f, -5f, 200, 999)]
    public void DamageStages_PreserveOfficialRules(SkillHitId id, float scale, float flat,
        SkillPowerRounding rounding, DamageKind kind, float backX, float backY, int budget, int sourceInterval)
    {
        var hit = TestTables.Tables.TbSkillHitConfig.Get(id);
        Assert.Equal(scale, hit.PowerScale);
        Assert.Equal(flat, hit.FlatPower);
        Assert.Equal(rounding, hit.PowerRounding);
        Assert.Equal(kind, hit.DamageKind);
        Assert.Equal(new Vector2(backX, backY), hit.Knockback);
        Assert.Equal(budget, hit.HitBudget);
        Assert.Equal(sourceInterval / 24f, hit.RepeatIntervalSeconds, .000001f);
    }

    /// <summary>只有悟空动作已迁入；无敌时间按秒存储，火魔主体不重复产生伤害。</summary>
    [Fact]
    public void PreparedActions_UseSecondsAndSeparateBodyFromDetachedDamage()
    {
        var table = TestTables.Tables.TbSkillActionConfig;
        Assert.Equal(5, table.DataList.Count);
        Assert.All(table.DataList, action =>
        {
            Assert.Equal(SkillHero.Wukong, TestTables.Tables.TbSkillConfig.Get(action.SkillId).Hero);
            Assert.StartsWith("skills/", action.Animation);
            Assert.Equal("res://TheGame/Entitys/Animations/Skills/wukong_skill_library.tres", action.AnimationLibraryPath);
        });
        Assert.Equal(new[] { SkillId.FlameThrust }, table.DataList.Where(x => x.GroundOnly).Select(x => x.SkillId));
        var flash = table.Get(SkillId.BlazingFlash);
        Assert.Equal(2f / 24, flash.InvincibleDelaySeconds, .000001f);
        Assert.Equal(.5f, flash.InvincibleDurationSeconds);
        var fire = table.Get(SkillId.FireDemonSlash);
        Assert.Equal(SkillHitId.None, fire.BodyHitId);
        Assert.Equal(70f / 24, fire.InvincibleDurationSeconds, .000001f);
        Assert.Equal(SoundId.WukongFireDemonStart, fire.SoundId);
    }

    /// <summary>落地光效缩放包含原文件横向修正；九连、下冲与落地引用各自阶段。</summary>
    [Fact]
    public void DetachedEffects_PreserveActualScaleAndStageSounds()
    {
        var effects = TestTables.Tables.TbSkillEffectConfig;
        Assert.Equal(5, effects.DataList.Count);
        var landing = effects.Get(SkillEffectId.FireDemonLanding);
        Assert.Equal(new Vector2(1.4f, 2), landing.Scale);
        Assert.Equal(new Vector2(2.1f, 3), landing.MusouScale);
        Assert.Equal(SkillHitId.FireDemonLanding, landing.HitId);
        Assert.Equal(SoundId.WukongFireDemonLanding, landing.SoundId);
        Assert.Equal(SkillEffectMirror.Actor, landing.Mirror);
        var combo = effects.Get(SkillEffectId.FireDemonCombo);
        Assert.Equal(SkillHitId.FireDemonCombo, combo.HitId);
        Assert.Equal(SoundId.WukongFireDemonCombo, combo.SoundId);
        var descent = effects.Get(SkillEffectId.FireDemonDescent);
        Assert.Equal(SkillHitId.FireDemonDescent, descent.HitId);
        Assert.Equal(SkillEffectMirror.OppositeActor, descent.Mirror);
        Assert.Equal(new Vector2(0, 20), effects.Get(SkillEffectId.FireDemonPrelude).SpawnOffset);
    }

    /// <summary>位移速度按像素/秒存储；技能专属无双速度不能套通用移速倍率。</summary>
    [Fact]
    public void MotionEvents_PreserveSkillSpecificMusouAndRepeatedAttackIds()
    {
        var timeline = TestTables.Tables.TbSkillTimelineConfig;
        var thrust = timeline.Get("FlameThrustStartMotion");
        Assert.Equal(new Vector2(360, 0), thrust.Velocity);
        Assert.Equal(new Vector2(720, 0), thrust.MusouVelocity);
        Assert.Equal(SkillMotionDirection.ExistingVelocity, thrust.Direction);
        var flash = timeline.Get("BlazingFlashMotion");
        Assert.Equal(new Vector2(1080, 0), flash.Velocity);
        Assert.Equal(flash.Velocity, flash.MusouVelocity);
        Assert.Equal(2f / 24, flash.AtSeconds, .000001f);
        var hits = timeline.DataList.Where(x => x.SkillId == SkillId.FlameThrust && x.Kind == SkillTimelineKind.MultiplyVelocityX);
        Assert.Equal(new[] { 4f / 24, 7f / 24, 10f / 24 }, hits.Select(x => x.AtSeconds));
        Assert.All(hits, hit => Assert.Equal(.8f, hit.XMultiplier));
        Assert.Equal(.7f, TestTables.Tables.TbSkillActionConfig.Get(SkillId.RisingDragon).CancelNormalAttackFrameRatio);
        Assert.Equal(new[] { SkillId.RisingDragon, SkillId.FlameStorm }, TestTables.Tables.TbSkillActionConfig.DataList
            .Where(x => x.ConsumesSecondAirJump).Select(x => x.SkillId));
    }

    /// <summary>火魔的目标缺失、地面提前落地和特效局部九次命中均有独立秒制配置。</summary>
    [Fact]
    public void FireDemonBranches_KeepSearchSeekLandingAndDetachedComboSeparate()
    {
        var table = TestTables.Tables.TbSkillTimelineConfig;
        var search = table.Get("FireDemonFindTarget");
        Assert.Equal(.25f, search.AtSeconds);
        Assert.Equal(130, search.TargetRange);
        Assert.Equal(20, search.ViewportInset);
        var missing = table.Get("FireDemonSkipCombo");
        Assert.Equal(SkillTimelineCondition.TargetMissing, missing.Condition);
        Assert.Equal(46f / 24, missing.SeekSeconds, .000001f);
        var combo = table.Get("FireDemonCombo");
        Assert.Equal(SkillTimelineCondition.TargetFound, combo.Condition);
        Assert.Equal(SkillEffectAnchor.SelectedTarget, combo.Anchor);
        Assert.Equal(40f / 24, table.Get("FireDemonTeleport").ActionLockSeconds, .000001f);
        var landing = table.Get("FireDemonLand");
        Assert.Equal(SkillTimelineCondition.OnFloor, landing.Condition);
        Assert.Equal(2, landing.AtSeconds);
        Assert.Equal(68f / 24, landing.UntilSeconds, .000001f);
        Assert.Equal(SkillTimelineCondition.LandingMissing, table.Get("FireDemonEnsureLanding").Condition);
        var detachedHits = table.DataList.Where(x => x.SourceEffectId == SkillEffectId.FireDemonCombo && x.Kind == SkillTimelineKind.NewAttackId);
        Assert.Equal(new[] { 3, 7, 13, 19, 23, 27, 33, 37, 41 }.Select(frame => (frame - 1) / 24f), detachedHits.Select(x => x.AtSeconds));
        Assert.Equal(69f / 24, table.Get("FireDemonLandingFinish").SeekSeconds, .000001f);
    }

    /// <summary>跨技能特效、倒序时间与不属于当前事件职责的参数必须在启动时拒绝。</summary>
    [Fact]
    public void StartupValidation_RejectsForeignEffectAndInvalidTimelineParameters()
    {
        var tables = new Tables(name => new ByteBuf(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", name + ".bytes"))));
        var motion = tables.TbSkillTimelineConfig.Get("BlazingFlashMotion");
        typeof(SkillTimelineConfig).GetField(nameof(motion.EffectId)).SetValue(motion, SkillEffectId.FireDemonPrelude);
        typeof(SkillTimelineConfig).GetField(nameof(motion.AtSeconds)).SetValue(motion, -1f);
        List<string> errors = new();
        SkillCombatConfigHelper.Validate(tables, errors);
        Assert.Contains(errors, error => error.Contains("不属于该技能"));
        Assert.Contains(errors, error => error.Contains("局部秒数"));
        Assert.Contains(errors, error => error.Contains("事件职责"));
    }

    /// <summary>正式表通过同一套启动校验。</summary>
    [Fact]
    public void PreparedCombatTables_PassStartupValidation()
    {
        List<string> errors = new();
        SkillCombatConfigHelper.Validate(TestTables.Tables, errors);
        Assert.Empty(errors);
    }

    /// <summary>损坏的阶段外键和负无敌时间必须在启动时报告，而不是战斗中静默兜底。</summary>
    [Fact]
    public void StartupValidation_RejectsForeignHitAndInvalidTime()
    {
        var tables = new Tables(name => new ByteBuf(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Data", name + ".bytes"))));
        var action = tables.TbSkillActionConfig.Get(SkillId.BlazingFlash);
        typeof(SkillActionConfig).GetField(nameof(action.BodyHitId)).SetValue(action, SkillHitId.WaterDemonFall);
        typeof(SkillActionConfig).GetField(nameof(action.InvincibleDurationSeconds)).SetValue(action, -1f);
        List<string> errors = new();
        SkillCombatConfigHelper.Validate(tables, errors);
        Assert.Contains(errors, error => error.Contains("无敌时间"));
        Assert.Contains(errors, error => error.Contains("不属于该技能"));
    }
}
