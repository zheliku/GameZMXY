using System.Linq;
using GameConfig.Equipment;
using GameConfig.Entity;
using GameLogic.Config;
using GameLogic.Architecture;
using GameLogic.Level;
using Xunit;

namespace GameLogic.Profile.Tests
{
    /// <summary>
    /// 主线图鉴关联真实配置，覆盖守关复用和直接奖励，不抽取或修改装备。
    /// </summary>
    public sealed class LevelGuideTests
    {
        /// <summary>
        /// 十个主线关卡具有非空守关信息和去重装备集合。
        /// </summary>
        [Fact]
        public void AllConfiguredLevels_HaveGuardiansAndDistinctDrops()
        {
            var configuration = TestTables.Architecture.GetModel<ConfigurationModel>();
            foreach (var level in TestTables.Tables.TbLevelConfig.DataList.Where(level => level.LevelId != DragonPalaceRules.LevelId))
            {
                var data = TestTables.Architecture.SendQuery(new LevelGuideQuery(level.LevelId));
                Assert.Same(level, data.Level);
                var expected = level.Stages.SelectMany(stage => stage.Recipes)
                    .Select(recipe => configuration.GetEnemy(recipe.EnemyEntityId).NameCn).Distinct();
                Assert.All(expected, name => Assert.Contains(data.Enemies, enemy => enemy.NameCn == name));
                Assert.Equal(data.Enemies.Count, data.Enemies.Select(enemy => enemy.NameCn).Distinct().Count());
                Assert.False(string.IsNullOrWhiteSpace(data.Enemies[0].NameCn));
                Assert.Equal(data.Drops.Count, data.Drops.Select(item => item.Id).Distinct().Count());
            }
        }

        /// <summary>
        /// 普通物种守关不能从首领编号或配方最后一行机械推断。
        /// </summary>
        [Fact]
        public void SharedSpeciesAndCaveGuardian_MatchActualLevels()
        {
            Assert.Equal("大猩猩", TestTables.Architecture.SendQuery(new LevelGuideQuery(1)).Enemies[0].NameCn);
            Assert.Equal("龟丞相", TestTables.Architecture.SendQuery(new LevelGuideQuery(7)).Enemies[0].NameCn);
            var cave = TestTables.Architecture.SendQuery(new LevelGuideQuery(2));
            Assert.Equal("猕猴王", cave.Enemies[0].NameCn);
        }

        /// <summary>
        /// 牛魔王图鉴展示实际直接法宝奖励。
        /// </summary>
        [Fact]
        public void BullDemon_ListsSingleBossAndDirectReward()
        {
            var data = TestTables.Architecture.SendQuery(new LevelGuideQuery(10));
            Assert.Equal("牛魔王", data.Enemies[0].NameCn);
            Assert.Equal(EEntityId.BullDemonBeast, data.Enemies[0].EntityId);
            Assert.Single(data.Enemies, enemy => enemy.NameCn == "牛魔王");
            Assert.Contains(data.Drops, item => item.Id == EEquipmentId.DiamondSnare);
        }

        /// <summary>
        /// 专属召唤物也属于本关图鉴，补充名单不改变关卡掉落来源。
        /// </summary>
        [Fact]
        public void SummonedEnemies_AppearAfterGuardians()
        {
            var cave = TestTables.Architecture.SendQuery(new LevelGuideQuery(4));
            Assert.Equal(EEntityId.BatCaveStatue, cave.Enemies[0].EntityId);
            Assert.Contains(cave.Enemies.Skip(1), enemy => enemy.EntityId == EEntityId.Bat);
            var whirlpool = TestTables.Architecture.SendQuery(new LevelGuideQuery(9));
            Assert.Equal(EEntityId.SharkDemonKing, whirlpool.Enemies[0].EntityId);
            Assert.Contains(whirlpool.Enemies.Skip(1), enemy => enemy.EntityId == EEntityId.SharkSpawn);
        }
    }
}
