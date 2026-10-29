using System;
using System.IO;
using System.Linq;
using GameConfig;
using GameConfig.Level;
using GameLogic.Config;
using Luban;
using Xunit;

namespace GameLogic.Profile.Tests
{
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
            Assert.Equal(System.Linq.Enumerable.Range(1, 13),
                tables.TbEnemyConfig.DataList.Where(config => config.EnemyId < 10000).Select(config => config.EnemyId));
            Assert.Equal(System.Linq.Enumerable.Range(10001, 11),
                tables.TbEnemyConfig.DataList.Where(config => config.EnemyId >= 10000).Select(config => config.EnemyId));
        }

        /// <summary>
        /// 同一怪物实体不再允许使用多条基础配置表达关卡差异。
        /// </summary>
        [Fact]
        public void RejectsDuplicateEnemyBaseConfig()
        {
            Tables tables = LoadTables();
            typeof(GameConfig.Enemy.EnemyConfig).GetField("EntityId")
                .SetValue(tables.TbEnemyConfig.Get(2), GameConfig.Entity.EntityId.Gorilla);
            Assert.Contains("只有一条基础配置", Assert.Throws<InvalidOperationException>(() => ConfigurationSystem.ValidateTables(tables)).Message);
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
}
