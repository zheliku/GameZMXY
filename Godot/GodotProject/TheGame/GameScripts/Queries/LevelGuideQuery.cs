using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig;
using GameConfig.Enemy;
using GameConfig.Entity;
using GameConfig.Equipment;
using GameConfig.Level;
using GameConfig.Loot;
using GameLogic.Architecture;
using GameLogic.Config;

namespace GameLogic.Level;

/// <summary>
/// 地图关卡图鉴的一次只读结果；定义借用配置，不拥有战斗或存档状态。
/// </summary>
/// <param name="Level">共享关卡定义。</param>
/// <param name="Enemies">守关敌人在前、普通小怪在后的共享定义，同名形态只显示一次。</param>
/// <param name="Drops">本关可能获得的装备，不表示本次一定掉落。</param>
public sealed record LevelGuideData(LevelConfig Level, IReadOnlyList<EnemyConfig> Enemies, IReadOnlyList<EquipmentConfig> Drops);

/// <summary>
/// 从关卡配方、敌人与掉落池组合关卡图鉴；不启动场景或修改档案。
/// </summary>
/// <param name="levelId">地图展示的主线关卡主键。</param>
public sealed class LevelGuideQuery(int levelId) : AbstractQuery<LevelGuideData>, ICanGetUtility
{
    /// <summary>
    /// 查询实际配方并合并装备身份，不创建运行时敌人或装备。
    /// </summary>
    /// <returns>本次查询的独立集合与共享配置定义。</returns>
    protected override LevelGuideData OnDo()
    {
        ConfigurationModel configuration = this.GetModel<ConfigurationModel>();
        Tables tables = configuration.Tables;
        ConfigIdUtility ids = this.GetUtility<ConfigIdUtility>();
        LevelConfig level = tables.TbLevelConfig.Get(levelId);
        EnemyConfig[] enemies = level.Stages.SelectMany(stage => stage.Recipes)
            .Select(recipe => configuration.GetEnemy(recipe.EnemyEntityId))
            .DistinctBy(enemy => enemy.EnemyId)
            .ToArray();
        EnemyConfig[] guardians = enemies.Where(enemy => ids.IsBoss(enemy.EntityId)).ToArray();
        if (guardians.Length == 0)
        {
            // 末段可有多条并行流，末行不是守关身份；这两关复用具体普通物种。
            EntityId guardian = levelId switch
            {
                1 => EntityId.Gorilla,
                7 => EntityId.TurtleMinister,
                _ => throw new InvalidOperationException("关卡图鉴缺少守关身份。")
            };
            guardians = [configuration.GetEnemy(guardian)];
        }
        var drops = new HashSet<EquipmentId>();
        foreach (EnemyConfig enemy in enemies)
        {
            // 浅岸守关龟丞相明确不掉装备，不能把后续海域的基础掉落放到这里。
            bool guardianWithoutEquipment = levelId == 7 && enemy.EntityId == EntityId.TurtleMinister;
            if (enemy.EquipmentDropChance > 0 && !guardianWithoutEquipment)
            {
                AddEquipmentFromPool(tables, enemy.EquipmentDropPoolId, drops);
            }
        }
        if (levelId == 10)
        {
            drops.Add(EquipmentId.DiamondSnare);
        }
        // 召唤物不在刷怪配方里，但会在本关出现；仅补图鉴名单，不改变掉落合并规则。
        EnemyConfig[] summoned = levelId switch
        {
            4 => new[] { configuration.GetEnemy(EntityId.Bat) },
            9 => new[] { configuration.GetEnemy(EntityId.SharkSpawn) },
            _ => Array.Empty<EnemyConfig>()
        };
        EnemyConfig[] displayEnemies = guardians.Concat(enemies).Concat(summoned)
            .DistinctBy(enemy => enemy.NameCn)
            .ToArray();
        EquipmentConfig[] displayEquipment = drops.Select(tables.TbEquipmentConfig.Get)
            .OrderByDescending(item => item.Quality)
            .ThenBy(item => item.Id)
            .ToArray();
        return new LevelGuideData(level, Array.AsReadOnly(displayEnemies), Array.AsReadOnly(displayEquipment));
    }

    /// <summary>
    /// 将掉落池中的装备身份加入图鉴；概率与权重仍由正式掉落系统处理。
    /// </summary>
    /// <param name="tables">本次查询借用的配置总表。</param>
    /// <param name="poolId">敌人配置的掉落池；None不贡献装备。</param>
    /// <param name="equipmentIds">本次查询拥有的去重集合。</param>
    private static void AddEquipmentFromPool(Tables tables, EquipmentDropPoolId poolId,
        HashSet<EquipmentId> equipmentIds)
    {
        if (poolId == EquipmentDropPoolId.None)
        {
            return;
        }

        foreach (EquipmentDropEntry entry in tables.TbEquipmentDropPoolConfig.Get(poolId).Entries)
        {
            equipmentIds.Add(entry.EquipmentId);
        }
    }
}
