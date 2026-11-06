using System.Linq;
using GameLogic.Drops;
using Xunit;

namespace GameLogic.Profile.Tests
{
    /// <summary>
    /// 真实配置驱动的灵魂、生命、魔法与无双球掉落边界回归。
    /// </summary>
    public sealed class LootTests
    {
        /// <summary>
        /// 直接核对游戏使用的导表产物，没有在代码中复制另一套正式配置。
        /// </summary>
        [Fact]
        public void OfficialMusouTable_UsesDamageRatioAndTimedDecay()
        {
            var battle = TestTables.Tables.TbBattleConfig.Data;
            Assert.Equal(100, battle.WsMax);
            Assert.Equal(2, battle.WsGainPerDamageRatio);
            Assert.Equal(1, battle.WsDecayPerSecond);
            Assert.Equal(6, battle.WsDrainPerSecond);
            Assert.Equal(1.5f, battle.WsAttackMultiplier);
            Assert.Equal(1.5f, battle.WsMoveSpeedMultiplier);
            Assert.Equal(1.5f, battle.WsJumpSpeedMultiplier);
            Assert.Equal(2.5f, battle.WsGravityMultiplier);
            Assert.Equal(5, TestTables.Tables.TbBattleConfig.Data.MusouPerOrb);
        }

        /// <summary>
        /// 四种球总量有界；红、蓝球分别使用掉落时的生命、魔法上限。
        /// </summary>
        [Fact]
        public void Rewards_RespectOfficialRanges_AndHaveBoundedCount()
        {
            var drop = TestTables.Tables.TbBattleConfig.Data;
            foreach (var monster in TestTables.Tables.TbEnemyConfig.DataList)
            {
                foreach (float random in new[] { 0f, .34f, .67f, .99999f, 1f })
                {
                    var loot = new LootSystem(TestTables.Architecture).Roll(monster, monster.SoulPerOrb, 1039, 2000, () => random);
                    Assert.InRange(loot.Count, 2, 13);
                    Assert.All(loot.Where(x => x.Kind == LootKind.Health), x => Assert.Equal(51, x.Amount));
                    Assert.All(loot.Where(x => x.Kind == LootKind.Mana), x => Assert.Equal(100, x.Amount));
                    Assert.All(loot.Where(x => x.Kind == LootKind.Musou), x => Assert.Equal(5, x.Amount));
                    Assert.InRange(loot.Count(x => x.Kind == LootKind.Soul), monster.SoulCount.X, monster.SoulCount.Y);
                    Assert.All(loot.Where(x => x.Kind == LootKind.Soul), x => Assert.Equal(monster.SoulPerOrb, x.Amount));
                }
            }
        }

        /// <summary>
        /// 12% 概率边界不多发蓝球，触发时数量可覆盖区间两端。
        /// </summary>
        /// <param name="chance">本次掉落概率判定的0..1随机样本。</param>
        /// <param name="count">用于抽取掉落数量的0..1随机样本。</param>
        /// <param name="expected">本次参数组合对应的预期结果。</param>
        [Theory]
        [InlineData(.11999f, 1f, 3)]
        [InlineData(.12f, 0f, 0)]
        [InlineData(0f, 0f, 1)]
        public void Mana_UsesIndependentChance(float chance, float count, int expected)
        {
            int calls = 0;
            float Random() => ++calls == 2 ? chance : count;
            var monster = TestTables.Tables.TbEnemyConfig.DataList[0];
            var loot = new LootSystem(TestTables.Architecture).Roll(monster, monster.SoulPerOrb, 50, 50, Random);
            Assert.Equal(expected, loot.Count(x => x.Kind == LootKind.Mana));
        }

        /// <summary>
        /// 黄球独立采用 12% 掉落概率，每颗加 5 无双，蓝球未触发也可触发黄球。
        /// </summary>
        /// <param name="chance">本次掉落概率判定的0..1随机样本。</param>
        /// <param name="count">用于抽取掉落数量的0..1随机样本。</param>
        /// <param name="expected">本次参数组合对应的预期结果。</param>
        [Theory]
        [InlineData(.11999f, 0f, 1)]
        [InlineData(.11999f, .99999f, 3)]
        [InlineData(.12f, 0f, 0)]
        public void Musou_HasIndependentChanceAndFivePoints(float chance, float count, int expected)
        {
            int calls = 0;
            float Random() => ++calls switch { 1 => 0f, 2 => .99f, 3 => chance, _ => count };
            var monster = TestTables.Tables.TbEnemyConfig.DataList[0];
            var loot = new LootSystem(TestTables.Architecture).Roll(monster, monster.SoulPerOrb, 50, 50, Random);
            Assert.DoesNotContain(loot, x => x.Kind == LootKind.Mana);
            Assert.Equal(expected, loot.Count(x => x.Kind == LootKind.Musou));
            Assert.All(loot.Where(x => x.Kind == LootKind.Musou), x => Assert.Equal(5, x.Amount));
        }

        /// <summary>
        /// 蓝球最低恢复 10，超过最低量后按魔法上限 5% 向下取整。
        /// </summary>
        /// <param name="maximum">拾取者当前的魔法上限。</param>
        /// <param name="expected">本次参数组合对应的预期结果。</param>
        [Theory]
        [InlineData(50, 10)]
        [InlineData(1000, 50)]
        [InlineData(1039, 51)]
        public void Mana_UsesCurrentMaximum(int maximum, int expected)
        {
            Assert.Equal(expected, new LootSystem(TestTables.Architecture).ManaAmount(maximum));
        }

        /// <summary>
        /// 红球独立于蓝/黄球；两次采样的联合分布保持官方1、2、3颗各4%。
        /// </summary>
        [Fact]
        public void Health_HasOfficialDistributionAndIndependentSamples()
        {
            var system = new LootSystem(TestTables.Architecture);
            var monster = TestTables.Tables.TbEnemyConfig.DataList[0];
            int[] outcomes = new int[4];
            for (int sample = 0; sample < 300; sample++)
            {
                for (int count = 0; count < 3; count++)
                {
                    int calls = 0;
                    float Random() => ++calls switch { 1 => 0f, 2 or 3 => 1f, 4 => sample / 300f, _ => count / 3f };
                    var loot = system.Roll(monster, monster.SoulPerOrb, 1039, 50, Random);
                    outcomes[loot.Count(x => x.Kind == LootKind.Health)]++;
                    Assert.DoesNotContain(loot, x => x.Kind is LootKind.Mana or LootKind.Musou);
                }
            }

            Assert.Equal(new[] { 792, 36, 36, 36 }, outcomes);
        }

        /// <summary>
        /// 红球最低10点；上限较高时向下取整恢复5%。
        /// </summary>
        /// <param name="maximum">生成时的生命上限。</param>
        /// <param name="expected">官方恢复量。</param>
        [Theory]
        [InlineData(1, 10)]
        [InlineData(199, 10)]
        [InlineData(1039, 51)]
        [InlineData(2000, 100)]
        public void Health_UsesOfficialAmount(int maximum, int expected)
        {
            Assert.Equal(expected, new LootSystem(TestTables.Architecture).HealthAmount(maximum));
        }
    }
}
