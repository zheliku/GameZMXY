using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameConfig;
using GameConfig.Battle;
using GameConfig.Entity;
using GameConfig.Equipment;
using GameConfig.Level;
using GameConfig.Loot;
using GameConfig.Sound;
using GameLogic.Config;
using Luban;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 生成配置的输入边界；专属数值与重复基础身份在启动时集中校验。
/// </summary>
public sealed class SpawnConfigTests
{
    /// <summary>
    /// 小怪和首领使用不同编号区间，且各区间保持从首次出场顺序升序。
    /// </summary>
    [Fact]
    public void EnemyIdsUseSeparateOrderedRanges()
    {
        Tables tables = LoadTables();
        Assert.Equal(System.Linq.Enumerable.Range(10000, 13),
            tables.TbEnemyConfig.DataList.Where(config => TestTables.ConfigIds.IsMonster(config.EntityId)).Select(config => config.EnemyId));
        Assert.Equal(System.Linq.Enumerable.Range(100000, 11),
            tables.TbEnemyConfig.DataList.Where(config => TestTables.ConfigIds.IsBoss(config.EntityId)).Select(config => config.EnemyId));
        Assert.All(tables.TbEnemyConfig.DataList, config => Assert.Equal((int)config.EntityId, config.EnemyId));
    }

    /// <summary>
    /// 资源按实体身份直接索引，英雄和敌人不维护第二套资源编号。
    /// </summary>
    [Fact]
    public void EntityResourcesUseSharedIdentityKeys()
    {
        Tables tables = LoadTables();
        TbEntityConfig resources = tables.TbEntityConfig;
        Assert.Equal(resources.DataList.OrderBy(config => (int)config.EntityId).Select(config => config.EntityId),
            resources.DataList.Select(config => config.EntityId));
        Assert.All(tables.TbHeroConfig.DataList, hero =>
        {
            Assert.Equal(hero.Id, (int)hero.EntityId);
            Assert.InRange(hero.Id, 1, 9999);
            Assert.Same(resources.DataList.Single(config => config.EntityId == hero.EntityId), resources.Get(hero.EntityId));
        });
        Assert.All(tables.TbEnemyConfig.DataList, enemy => Assert.NotNull(resources.Get(enemy.EntityId)));
    }

    /// <summary>
    /// 声音分组使用独立预留区间，数值和可读键引用同一枚举。
    /// </summary>
    [Fact]
    public void SoundIdsUseOrderedGroupRanges()
    {
        IReadOnlyList<SoundConfig> sounds = LoadTables().TbSoundConfig.DataList;
        Assert.Equal(sounds.Select(sound => sound.Id).OrderBy(id => id), sounds.Select(sound => sound.Id));
        Assert.All(sounds, sound =>
        {
            Assert.Equal(sound.Id, (int)Enum.Parse<GameConfig.Sound.SoundId>(sound.Key));
            if (sound.Group == "Music")
            {
                Assert.InRange(sound.Id, 1000, 1999);
            }
            else if (sound.Group == "UI")
            {
                Assert.InRange(sound.Id, 2000, 2999);
            }
            else
            {
                Assert.InRange(sound.Id, 10000, 999999);
            }
        });
    }

    /// <summary>
    /// 攻击编码保留实体、动作、段号，技能不会混入本体普攻。
    /// </summary>
    [Fact]
    public void AttackIdsEncodeOwnerActionAndStage()
    {
        Tables tables = LoadTables();
        Assert.All(tables.TbAttackConfig.DataList.Where(hit => hit.OwnerId != GameConfig.Entity.EntityId.None), hit =>
        {
            Assert.True(TestTables.ConfigIds.IsAttackIdForOwner(hit.Id, hit.OwnerId));
        });
        Assert.Equal(1011, (int)GameConfig.Skills.SkillHitId.RisingDragon);
        Assert.Equal(new[] { 1051, 1052, 1053 }, new[] {
            (int)GameConfig.Skills.SkillHitId.FireDemonCombo,
            (int)GameConfig.Skills.SkillHitId.FireDemonDescent,
            (int)GameConfig.Skills.SkillHitId.FireDemonLanding });
        Assert.Equal(2051, (int)GameConfig.Skills.SkillHitId.WaterDemonPillar);
        Assert.Equal(2052, (int)GameConfig.Skills.SkillHitId.WaterDemonFall);
        Assert.Equal(5, new ConfigurationModel(tables).GetAttacks(GameConfig.Entity.EntityId.Wukong).Count);
        Assert.Single(new ConfigurationModel(tables).GetAttacks(GameConfig.Entity.EntityId.Tangseng));
    }

    /// <summary>
    /// 通用品质和Boss变体使用明确公式，不保留历史流水号。
    /// </summary>
    [Fact]
    public void DropPoolIdsEncodeQualityOrBossVariant()
    {
        Assert.Equal(EquipmentDropPoolId.Rough, TestTables.ConfigIds.GetCommonEquipmentDropPoolId(EquipmentQuality.Rough));
        Assert.Equal(EquipmentDropPoolId.Common, TestTables.ConfigIds.GetCommonEquipmentDropPoolId(EquipmentQuality.Common));
        Assert.Equal(EquipmentDropPoolId.Excellent, TestTables.ConfigIds.GetCommonEquipmentDropPoolId(EquipmentQuality.Excellent));
        Assert.Equal(EquipmentDropPoolId.Fine, TestTables.ConfigIds.GetCommonEquipmentDropPoolId(EquipmentQuality.Fine));
        Assert.Equal(EquipmentDropPoolId.GarudaCave, TestTables.ConfigIds.GetBossEquipmentDropPoolId(EntityId.PengDemonKing));
        Assert.Equal(EquipmentDropPoolId.DragonKing, TestTables.ConfigIds.GetBossEquipmentDropPoolId(EntityId.DragonKing));
        Assert.Equal(EquipmentDropPoolId.DragonKingHigh, TestTables.ConfigIds.GetBossEquipmentDropPoolId(EntityId.DragonKing, 1));
        Assert.Equal(EquipmentDropPoolId.EastSeaBanner, TestTables.ConfigIds.GetBossEquipmentDropPoolId(EntityId.DragonKing, 2));
    }

    /// <summary>
    /// 独立固定样本锁定既有编号，防止工具和调用方一起改错仍能通过相互比较。
    /// </summary>
    [Fact]
    public void EncodedIdsKeepPublishedValues()
    {
        Assert.Equal(1001, TestTables.ConfigIds.GetAttackId(EntityId.Wukong, 0));
        Assert.Equal(1053, TestTables.ConfigIds.GetAttackId(EntityId.Wukong, 5, 3));
        Assert.Equal(2052, TestTables.ConfigIds.GetAttackId(EntityId.Tangseng, 5, 2));
        Assert.Equal(100006022, TestTables.ConfigIds.GetAttackId(EntityId.SharkDemonKing, 2, 2));
        Assert.Equal(100006032, TestTables.ConfigIds.GetAttackId(EntityId.SharkDemonKing, 3, 2));
        Assert.Equal(10011012, TestTables.ConfigIds.GetAttackId(EntityId.SharkSpawn, 1, 2));
        Assert.Equal(100003032, TestTables.ConfigIds.GetAttackId(EntityId.LionCamelKing, 3, 2));
        Assert.Equal(900004001, TestTables.ConfigIds.GetEnvironmentAttackId(4, 1));
        Assert.Equal(1000, (int)TestTables.ConfigIds.GetCommonEquipmentDropPoolId(EquipmentQuality.Rough));
        Assert.Equal(1300, (int)TestTables.ConfigIds.GetCommonEquipmentDropPoolId(EquipmentQuality.Fine));
        Assert.Equal(10001002, (int)TestTables.ConfigIds.GetBossEquipmentDropPoolId(EntityId.DragonKing, 2));
    }

    /// <summary>
    /// 段号为零、跨实体及环境编号误归属必须在正式启动校验处拒绝。
    /// </summary>
    /// <param name="attackId">错误的攻击主键。</param>
    /// <param name="ownerId">声明的所属身份。</param>
    [Theory]
    [InlineData(1000, EntityId.Wukong)]
    [InlineData(2011, EntityId.Wukong)]
    [InlineData(1011, EntityId.None)]
    [InlineData(900004001, EntityId.Wukong)]
    public void RejectsAttackIdsOutsideOwnerEncoding(int attackId, EntityId ownerId)
    {
        Tables tables = LoadTables();
        AttackConfig attack = tables.TbAttackConfig.Get(1011);
        typeof(GameConfig.Battle.AttackConfig).GetField("Id").SetValue(attack, attackId);
        typeof(GameConfig.Battle.AttackConfig).GetField("OwnerId").SetValue(attack, ownerId);
        Assert.Contains("编号必须", Assert.Throws<InvalidOperationException>(() => TestTables.CreateConfigurationScope(tables)).Message);
    }

    /// <summary>
    /// 实体区间在交界处互斥，None和区间之外的值不误归类。
    /// </summary>
    /// <param name="id">区间边界样本。</param>
    /// <param name="hero">是否为英雄。</param>
    /// <param name="monster">是否为小怪。</param>
    /// <param name="boss">是否为首领。</param>
    [Theory]
    [InlineData(-1, false, false, false)]
    [InlineData(0, false, false, false)]
    [InlineData(1, true, false, false)]
    [InlineData(9999, true, false, false)]
    [InlineData(10000, false, true, false)]
    [InlineData(99999, false, true, false)]
    [InlineData(100000, false, false, true)]
    [InlineData(199999, false, false, true)]
    [InlineData(200000, false, false, false)]
    public void EntityRangesRemainDisjoint(int id, bool hero, bool monster, bool boss)
    {
        Assert.Equal(hero, TestTables.ConfigIds.IsHero((EntityId)id));
        Assert.Equal(monster, TestTables.ConfigIds.IsMonster((EntityId)id));
        Assert.Equal(boss, TestTables.ConfigIds.IsBoss((EntityId)id));
    }

    /// <summary>
    /// 同一怪物实体不再允许使用多条基础配置表达关卡差异。
    /// </summary>
    [Fact]
    public void RejectsDuplicateEnemyBaseConfig()
    {
        Tables tables = LoadTables();
        typeof(GameConfig.Enemy.EnemyConfig).GetField("EntityId")
            .SetValue(tables.TbEnemyConfig.Get((int)GameConfig.Entity.EntityId.DemonMonkey), GameConfig.Entity.EntityId.Gorilla);
        Assert.Contains("只有一条基础配置", Assert.Throws<InvalidOperationException>(() => TestTables.CreateConfigurationScope(tables)).Message);
    }

    /// <summary>
    /// 每次读取独立表实例，避免篡改共享测试配置或影响并行用例。
    /// </summary>
    /// <returns>由正式二进制加载的独立配置总表。</returns>
    private static Tables LoadTables()
    {
        return new(name =>
            new ByteBuf(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", name + ".bytes"))));
    }
}
