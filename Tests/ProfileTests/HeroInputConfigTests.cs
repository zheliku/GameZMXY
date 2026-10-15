using System;
using System.IO;
using GameConfig;
using GameConfig.Hero;
using GameLogic.Config;
using Luban;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>按独立正式表验证英雄动作参数的启动边界。</summary>
public sealed class HeroInputConfigTests
{
    /// <summary>冷却必须覆盖全部普攻段，不能含非有限或非正数。</summary>
    /// <param name="invalid">注入的冷却值，零也用于覆盖缺段情况。</param>
    [Theory, InlineData(0f), InlineData(-1f), InlineData(float.NaN), InlineData(float.PositiveInfinity)]
    public void RejectsInvalidCooldown(float invalid)
    {
        Tables tables = Load();
        HeroConfig hero = tables.TbHeroConfig.Get(1);
        float[] values = invalid == 0 ? [0.1f] : [0.1f, 0.1f, invalid, 0.1f, 0.1f];
        typeof(HeroConfig).GetField("AttackInputCooldowns").SetValue(hero, values);
        Assert.Contains("普攻冷却", Assert.Throws<InvalidOperationException>(() => ConfigurationSystem.ValidateTables(tables)).Message);
    }

    /// <summary>时序和穿平台距离拒绝非正或非有限配置。</summary>
    /// <param name="field">动作配置字段。</param>
    /// <param name="invalid">非法值。</param>
    [Theory, InlineData("AirAttackCooldown", 0f), InlineData("ComboResetTime", float.NaN), InlineData("PlatformDropDistance", -1f)]
    public void RejectsInvalidActionParameter(string field, float invalid)
    {
        Tables tables = Load();
        typeof(HeroConfig).GetField(field).SetValue(tables.TbHeroConfig.Get(1), invalid);
        Assert.Contains("普攻冷却", Assert.Throws<InvalidOperationException>(() => ConfigurationSystem.ValidateTables(tables)).Message);
    }

    /// <summary>每例加载独立正式表，反射修改不会污染其他用例。</summary>
    /// <returns>独立配置实例。</returns>
    private static Tables Load() => new(name => new ByteBuf(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", name + ".bytes"))));
}
