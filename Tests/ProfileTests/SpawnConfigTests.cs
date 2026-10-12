using System;
using System.IO;
using GameConfig;
using GameConfig.Level;
using GameLogic.Config;
using Luban;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>刷怪配方的输入边界；用独立真实表实例验证非法倍率和重复基础配置会在启动期被拒绝。</summary>
public sealed class SpawnConfigTests
{
    /// <summary>生命必须为正，物防与攻击不能为负，所有倍率都必须是有限数。</summary>
    /// <param name="field">通过反射修改的生成字段，仅作用于本次测试表。</param>
    /// <param name="value">非法配置值。</param>
    [Theory]
    [InlineData("HealthMultiplier", 0f)]
    [InlineData("HealthMultiplier", -1f)]
    [InlineData("HealthMultiplier", float.NaN)]
    [InlineData("DefenseMultiplier", -1f)]
    [InlineData("DefenseMultiplier", float.PositiveInfinity)]
    [InlineData("AttackMultiplier", -1f)]
    [InlineData("AttackMultiplier", float.NaN)]
    public void RejectsInvalidMultiplier(string field, float value)
    {
        Tables tables = LoadTables();
        LevelSpawnRecipe recipe = tables.TbLevelConfig.Get(1).Stages[0].Recipes[0];
        typeof(LevelSpawnRecipe).GetField(field).SetValue(recipe, value);
        Assert.Contains("倍率", Assert.Throws<InvalidOperationException>(() => ConfigHelper.ValidateAll(tables)).Message);
    }

    /// <summary>经验覆盖只接受 -1 或非负整数。</summary>
    [Fact]
    public void RejectsInvalidExperienceOverride()
    {
        Tables tables = LoadTables();
        LevelSpawnRecipe recipe = tables.TbLevelConfig.Get(1).Stages[0].Recipes[0];
        typeof(LevelSpawnRecipe).GetField("ExperienceOverride").SetValue(recipe, -2);
        Assert.Throws<InvalidOperationException>(() => ConfigHelper.ValidateAll(tables));
    }

    /// <summary>同一怪物实体不再允许使用多条基础配置表达关卡差异。</summary>
    [Fact]
    public void RejectsDuplicateMonsterBaseConfig()
    {
        Tables tables = LoadTables();
        typeof(GameConfig.Monster.MonsterConfig).GetField("EntityId")
            .SetValue(tables.TbMonsterConfig.Get(2), GameConfig.Entity.EntityId.Gorilla);
        Assert.Contains("只有一条基础配置", Assert.Throws<InvalidOperationException>(() => ConfigHelper.ValidateAll(tables)).Message);
    }

    /// <summary>每次读取独立表实例，避免篡改共享测试配置或影响并行用例。</summary>
    /// <returns>由正式二进制加载的独立配置总表。</returns>
    private static Tables LoadTables() => new(name =>
        new ByteBuf(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", name + ".bytes"))));
}
