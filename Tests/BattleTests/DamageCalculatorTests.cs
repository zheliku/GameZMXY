using GameLogic.Battle.Damage;
using GameLogic.Battle.Attacks;
using System;
using System.IO;
using GameConfig.Battle;
using GameConfig.Sound;
using GameFramework;
using Godot;
using Luban;
using Xunit;

namespace GameLogic.Battle.Tests
{
    /// <summary>
    /// DamageResult 回归：常数取自真实表产物（battle_tbbattleconfig.bytes），期望值为手算。
    /// 手算前提（当前表值）：DefK 英雄250/怪物100，暴击与闪避直接使用 0..1 概率，
    /// LuckyK 英雄攻击100/怪物攻击50，CritBase 2，等级系数 幸运0.07、伤害0.05，
    /// 封顶 幸运0.7、伤害级数5/2。表值变更导致失败时，先确认是改表而非改坏公式。
    /// </summary>
    public class DamageCalculatorTests
    {
        private static readonly BattleConfig Config = LoadConfig(); // 从真实战斗表加载的测试配置。

        private const float NoProc = 0.9999f; // 必不闪避、必不暴击的随机数（取上界附近）。

        private static BattleConfig LoadConfig() // 读取测试输出目录中的战斗配置表。
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Data", "battle_tbbattleconfig.bytes");
            return new TbBattleConfig(new ByteBuf(File.ReadAllBytes(path))).Data;
        }

        private static CombatantAttributes Hero(int level = 1, int power = 8, int def = 10, int mdef = 10, float crit = 0,
            float miss = 0, int lucky = 0, int toughness = 0, float htarget = 0, float critReduce = 0, int ar = 0, int sp = 0) // 创建指定属性的英雄战斗数据。
        {
            return new CombatantAttributes(CombatSide.Hero, level, power, def, mdef, crit, miss, lucky, toughness, htarget,
                critReduce, ar, sp);
        }

        private static CombatantAttributes Monster(int level = 5, int power = 0, int def = 50, int mdef = 80, float crit = 0,
            float miss = 0, int lucky = 0, int toughness = 0, float htarget = 0, float critReduce = 0, int ar = 0, int sp = 0) // 创建指定属性的怪物战斗数据。
        {
            return new CombatantAttributes(CombatSide.Monster, level, power, def, mdef, crit, miss, lucky, toughness,
                htarget, critReduce, ar, sp);
        }

        private static DamageResult Hit(in CombatantAttributes attacker, float power, DamageKind kind,
            in CombatantAttributes defender, float missRoll, float critRoll) // 结算并立即归还攻击包。
        {
            // 从对象池创建一次性攻击包，确保测试覆盖与游戏内相同的生命周期。
            AttackData data = AttackData.Create(0, attacker, power, kind, Vector2.Zero, 1, 0);
            try
            {
                return new CombatSystem(BattleArchitecture.Instance).Calculate(data, defender, missRoll, critRoll);
            }
            finally
            {
                // 无论结算成功或抛错都归还攻击包，避免污染后续测试。
                ReferencePool.Release(data);
            }
        }

        /// <summary>验证测试配置来自真实表数据。</summary>
        [Fact]
        public void Config_LoadsFromTableBytes()
        {
            Assert.Equal(250f, Config.DefKHero);
            Assert.Equal(100f, Config.DefKMonster);
            Assert.Equal(5, Config.LvDamageCapLvHeroDef);
            Assert.Equal(2, Config.LvDamageCapLvMonsterDef);
        }

        /// <summary>验证英雄攻击低等级怪物时的物理伤害。</summary>
        [Fact]
        public void M4Case_Wukong1_HitsMonkey5_PhysicsNoCrit()
        {
            // 悟空 1 级(攻 8) 普攻1 倍率 1.0 → 威力 8；猴子 5 级 物防 50。
            // 等级压制（怪物防守，封顶 2 级）：8 × (1 − 2×0.05) = 7.2 → 7
            // 物防减伤：50/(50+100) = 0.333 → 7 × 0.667 = 4.669 → 4
            DamageResult r = Hit(Hero(), 8f, DamageKind.Physics, Monster(), NoProc, NoProc);
            Assert.False(r.IsMiss);
            Assert.False(r.IsCrit);
            Assert.Equal(4, r.Damage);
        }

        /// <summary>验证低等级英雄承受高等级怪物物理攻击时的伤害。</summary>
        [Fact]
        public void M4Case_Monkey5_HitsWukong1_PhysicsNoCrit()
        {
            // 猴子固定威力 10；英雄防守压制封顶 5 级：d=4 → 10 × (1 + 4×0.05) = 12
            // 英雄物防 10：10/(10+250) = 0.038 → 12 × 0.962 = 11.544 → 11
            DamageResult r = Hit(Monster(), 10f, DamageKind.Physics, Hero(), NoProc, NoProc);
            Assert.Equal(11, r.Damage);
        }

        /// <summary>验证真实伤害不受防御影响。</summary>
        [Fact]
        public void SameLevel_RealDamage_IgnoresDefense()
        {
            DamageResult r = Hit(Hero(level: 5), 37.9f, DamageKind.Real, Monster(def: 999, mdef: 999), NoProc, NoProc);
            Assert.Equal(37, r.Damage);
        }

        /// <summary>验证魔法伤害使用魔法防御结算。</summary>
        [Fact]
        public void Magic_UsesMdef()
        {
            // 同级 威力 100，魔防 80：80/180 = 0.444 → 100 × 0.556 = 55.6 → 55
            DamageResult r = Hit(Hero(level: 5), 100f, DamageKind.Magic, Monster(), NoProc, NoProc);
            Assert.Equal(55, r.Damage);
        }

        /// <summary>验证破甲降低防御且净防御不会小于零。</summary>
        [Fact]
        public void Penetration_ReducesDefense_ClampedAtZero()
        {
            // 破甲 60 > 物防 50 → 净物防 0，不减伤
            DamageResult r = Hit(Hero(level: 5, ar: 60), 100f, DamageKind.Physics, Monster(), NoProc, NoProc);
            Assert.Equal(100, r.Damage);
        }

        /// <summary>验证闪避时伤害为零并设置闪避标记。</summary>
        [Fact]
        public void Miss_ReturnsZeroAndFlag()
        {
            // 怪物闪避为 0.5；roll 0.49 < 0.5 → 闪避
            DamageResult r = Hit(Hero(level: 5), 100f, DamageKind.Physics, Monster(miss: .5f),
                0.49f, 0f);
            Assert.True(r.IsMiss);
            Assert.Equal(0, r.Damage);
        }

        /// <summary>验证随机值恰等于闪避率时不会闪避。</summary>
        [Fact]
        public void Miss_RollAtRate_DoesNotMiss()
        {
            DamageResult r = Hit(Hero(level: 5), 100f, DamageKind.Real, Monster(miss: .5f), 0.5f, 0.9999f);
            Assert.False(r.IsMiss);
        }

        /// <summary>验证零概率在随机值为零时也不会触发。</summary>
        [Fact]
        public void ZeroRates_NeverProc_EvenWithZeroRolls()
        {
            // 旧项目 <= 判定在率为 0、roll 恰为 0 时仍会触发；新实现率为 0 必不触发
            DamageResult r = Hit(Hero(level: 5), 100f, DamageKind.Real, Monster(), 0f, 0f);
            Assert.False(r.IsMiss);
            Assert.False(r.IsCrit);
            Assert.Equal(100, r.Damage);
        }

        /// <summary>验证命中属性抵消目标闪避值。</summary>
        [Fact]
        public void Htarget_OffsetsMiss()
        {
            // 闪避 50% − 命中 50% = 0 → 闪避率 0
            DamageResult r = Hit(Hero(level: 5, htarget: .5f), 100f, DamageKind.Real, Monster(miss: .5f),
                0f, 0.9999f);
            Assert.False(r.IsMiss);
        }

        /// <summary>验证英雄攻击使用英雄幸运系数计算暴击伤害。</summary>
        [Fact]
        public void Crit_HeroAttacks_UsesHeroLuckyK()
        {
            // 同级，暴击率 50%；幸运 100、英雄攻击幸运K 100 → 倍率 2 + 0.5 = 2.5
            // 真实伤害：100 × 2.5 = 250
            DamageResult r = Hit(Hero(level: 5, crit: .5f, lucky: 100), 100f, DamageKind.Real, Monster(),
                0.9999f, 0.49f);
            Assert.True(r.IsCrit);
            Assert.Equal(250, r.Damage);
        }

        /// <summary>验证怪物攻击使用怪物幸运系数计算暴击伤害。</summary>
        [Fact]
        public void Crit_MonsterAttacks_UsesMonsterLuckyK()
        {
            // 同级，幸运 50、怪物攻击幸运K 50 → 倍率 2.5；真实 100 → 250
            DamageResult r = Hit(Monster(level: 1, crit: .5f, lucky: 50), 100f, DamageKind.Real, Hero(level: 1),
                0.9999f, 0.49f);
            Assert.True(r.IsCrit);
            Assert.Equal(250, r.Damage);
        }

        /// <summary>验证韧性抵消幸运且暴抗抵消暴击值。</summary>
        [Fact]
        public void Crit_ToughnessOffsetsLucky_CritReduceOffsetsCrit()
        {
            // 暴击 50% − 暴抗 50% = 0 → 不暴击
            DamageResult none = Hit(Hero(level: 5, crit: .5f), 100f, DamageKind.Real, Monster(critReduce: .5f),
                0.9999f, 0f);
            Assert.False(none.IsCrit);

            // 幸运 100 − 韧性 100 = 0 → 倍率仅基数 2
            DamageResult plain = Hit(Hero(level: 5, crit: .5f, lucky: 100), 100f, DamageKind.Real,
                Monster(toughness: 100), 0.9999f, 0f);
            Assert.True(plain.IsCrit);
            Assert.Equal(200, plain.Damage);
        }

        /// <summary>验证暴击先于防御减伤结算。</summary>
        [Fact]
        public void Crit_ThenDefense_OrderMatchesLegacy()
        {
            // 同级 威力 100，暴击倍率 2（幸运 0），物防 50 → 200 × 0.667 = 133.4 → 133
            DamageResult r = Hit(Hero(level: 5, crit: .5f), 100f, DamageKind.Physics, Monster(),
                0.9999f, 0f);
            Assert.Equal(133, r.Damage);
        }

        /// <summary>验证怪物防守时等级伤害修正最多计两级。</summary>
        [Fact]
        public void LevelSuppression_MonsterDefends_CapsAtTwoLevels()
        {
            // 英雄 10 级打 1 级怪：d=9 但封顶 2 级 → 100 × 1.1 = 110（真实伤害，隔离减伤）
            DamageResult r = Hit(Hero(level: 10), 100f, DamageKind.Real, Monster(level: 1), NoProc, NoProc);
            Assert.Equal(110, r.Damage);
        }

        /// <summary>验证英雄防守时等级伤害修正最多计五级。</summary>
        [Fact]
        public void LevelSuppression_HeroDefends_CapsAtFiveLevels()
        {
            // 1 级英雄被 10 级怪打：封顶 5 级，攻击方(怪)等级高 → ×1.25
            // （回归：表系数是 float，×0.75 一侧曾因 74.9999996 被截成 74，见 DamageResult.TruncateEpsilon）
            DamageResult up = Hit(Monster(level: 10), 100f, DamageKind.Real, Hero(level: 1), NoProc, NoProc);
            Assert.Equal(125, up.Damage);

            // 10 级英雄被 1 级怪打 → ×0.75
            DamageResult down = Hit(Monster(level: 1), 100f, DamageKind.Real, Hero(level: 10), NoProc, NoProc);
            Assert.Equal(75, down.Damage);
        }

        /// <summary>装备的 3% 直接参与判定，等级差不改变概率；边界值不触发。</summary>
        /// <param name="attackerLevel">攻击方等级。</param>
        /// <param name="defenderLevel">防守方等级。</param>
        [Theory]
        [InlineData(1, 30)]
        [InlineData(10, 10)]
        [InlineData(30, 1)]
        public void DirectProbabilities_KeepThreePercentAcrossLevelDifferences(int attackerLevel, int defenderLevel)
        {
            var hero = Hero(level: attackerLevel, crit: .03f);
            var monster = Monster(level: defenderLevel, miss: .03f);
            Assert.True(Hit(hero, 100f, DamageKind.Real, monster, .029f, NoProc).IsMiss);
            Assert.False(Hit(hero, 100f, DamageKind.Real, monster, .03f, NoProc).IsMiss);
            Assert.True(Hit(hero, 100f, DamageKind.Real, monster, NoProc, .029f).IsCrit);
            Assert.False(Hit(hero, 100f, DamageKind.Real, monster, NoProc, .03f).IsCrit);
            // 人怪结算方向相反时也不引入另一条概率曲线。
            Assert.True(Hit(Monster(level: attackerLevel, crit: .03f), 100f, DamageKind.Real,
                Hero(level: defenderLevel), NoProc, .029f).IsCrit);
            Assert.True(Hit(Monster(level: attackerLevel), 100f, DamageKind.Real,
                Hero(level: defenderLevel, miss: .03f), .029f, NoProc).IsMiss);
        }

        /// <summary>验证伤害结算结果不会为负数。</summary>
        [Fact]
        public void Damage_NeverNegative()
        {
            DamageResult r = Hit(Hero(level: 5), 0f, DamageKind.Physics, Monster(), NoProc, NoProc);
            Assert.Equal(0, r.Damage);
        }

        /// <summary>击退数值已经使用 px/s，结算只处理朝向，不重复乘源帧率。</summary>
        [Fact]
        public void Knockback_AppliesDirectionWithoutRescaling()
        {
            AttackData data = AttackData.Create(1004, Hero(), 10f, DamageKind.Physics, new Vector2(144, -120), -1, 0);
            try
            {
                Assert.Equal(new Vector2(-144, -120), new CombatSystem(BattleArchitecture.Instance).KnockbackVelocity(data));
            }
            finally
            {
                ReferencePool.Release(data);
            }
        }

        /// <summary>中心型攻击按目标所在一侧击退，不受角色朝向影响；中心相等时向右。</summary>
        /// <param name="targetX">受击方世界横坐标。</param>
        /// <param name="expectedX">应得到的世界横向速度。</param>
        [Theory]
        [InlineData(150, -480)]
        [InlineData(200, 480)]
        [InlineData(250, 480)]
        public void Knockback_FromCenter(float targetX, float expectedX)
        {
            var data = AttackData.Create(0, Hero(), 10f, DamageKind.Magic, new Vector2(480, -48), -1, 0, 200f);
            try
            {
                Assert.Equal(new Vector2(expectedX, -48), new CombatSystem(BattleArchitecture.Instance)
                    .KnockbackVelocity(data, targetX));
            }
            finally
            {
                ReferencePool.Release(data);
            }
            Assert.Null(data.KnockbackOriginX);
        }

        /// <summary>验证攻击包每目标只命中一次且归还后清除命中记录。</summary>
        [Fact]
        public void AttackData_OneHitPerTarget_AndClearedOnRelease()
        {
            AttackData attackData = AttackData.Create(1001, Hero(), 8f, DamageKind.Physics, Vector2.Zero, 1, 0);
            Assert.True(attackData.TryRegisterHit(42));
            Assert.False(attackData.TryRegisterHit(42));
            Assert.True(attackData.TryRegisterHit(43));
            ReferencePool.Release(attackData);

            // 同类型复用池内对象：必须是干净的
            AttackData reusedData = AttackData.Create(1002, Hero(), 9f, DamageKind.Magic, Vector2.Zero, 1, 0);
            Assert.True(reusedData.TryRegisterHit(42));
            Assert.Equal(1002, reusedData.AttackId);
            Assert.Equal(DamageKind.Magic, reusedData.Kind);
            ReferencePool.Release(reusedData);
        }
    }
}
