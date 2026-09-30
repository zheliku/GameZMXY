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
	/// DamageCalculator 回归：常数取自真实表产物（battle_tbbattleconfig.bytes），期望值为手算。
	/// 手算前提（当前表值）：DefK 英雄250/怪物100，MissK 英雄100/怪物70，CritK 100，
	/// LuckyK 英雄攻击100/怪物攻击50，CritBase 2，等级系数 闪避0.03/0.04、暴击0.07/0.11、幸运0.07、伤害0.05，
	/// 封顶 闪避1/0.9、暴击1、幸运0.7、伤害级数5/2。表值变更导致失败时，先确认是改表而非改坏公式。
	/// </summary>
	public class DamageCalculatorTests
	{
		private static readonly BattleConfig Config = LoadConfig();

		/// <summary>必不闪避、必不暴击的随机数（roll 取上界附近）</summary>
		private const float NoProc = 0.9999f;

		private static BattleConfig LoadConfig()
		{
			string path = Path.Combine(AppContext.BaseDirectory, "Data", "battle_tbbattleconfig.bytes");
			return new TbBattleConfig(new ByteBuf(File.ReadAllBytes(path))).Data;
		}

		private static CombatantStats Hero(int level = 1, int power = 8, int def = 10, int mdef = 10, int crit = 0,
			int miss = 0, int lucky = 0, int toughness = 0, int htarget = 0, int critReduce = 0, int ar = 0, int sp = 0)
		{
			return new CombatantStats(CombatSide.Hero, level, power, def, mdef, crit, miss, lucky, toughness, htarget,
				critReduce, ar, sp);
		}

		private static CombatantStats Monster(int level = 5, int power = 0, int def = 50, int mdef = 80, int crit = 0,
			int miss = 0, int lucky = 0, int toughness = 0, int htarget = 0, int critReduce = 0, int ar = 0, int sp = 0)
		{
			return new CombatantStats(CombatSide.Monster, level, power, def, mdef, crit, miss, lucky, toughness,
				htarget, critReduce, ar, sp);
		}

		/// <summary>结算并立即归还攻击包（与游戏内"一招一包"用法一致）</summary>
		private static DamageResult Hit(in CombatantStats attacker, float power, DamageKind kind,
			in CombatantStats defender, float missRoll, float critRoll)
		{
			AttackData attack = AttackData.Create(0, attacker, power, kind, Vector2.Zero, 1, 0, SoundId.None);
			try
			{
				return DamageCalculator.Calculate(Config, attack, defender, missRoll, critRoll);
			}
			finally
			{
				ReferencePool.Release(attack);
			}
		}

		[Fact]
		public void Config_LoadsFromTableBytes()
		{
			Assert.Equal(250f, Config.DefKHero);
			Assert.Equal(100f, Config.DefKMonster);
			Assert.Equal(0.9f, Config.LvMissCapMonsterDef, 4);
			Assert.Equal(5, Config.LvDamageCapLvHeroDef);
			Assert.Equal(2, Config.LvDamageCapLvMonsterDef);
		}

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

		[Fact]
		public void M4Case_Monkey5_HitsWukong1_PhysicsNoCrit()
		{
			// 猴子固定威力 10；英雄防守压制封顶 5 级：d=4 → 10 × (1 + 4×0.05) = 12
			// 英雄物防 10：10/(10+250) = 0.038 → 12 × 0.962 = 11.544 → 11
			DamageResult r = Hit(Monster(), 10f, DamageKind.Physics, Hero(), NoProc, NoProc);
			Assert.Equal(11, r.Damage);
		}

		[Fact]
		public void SameLevel_RealDamage_IgnoresDefense()
		{
			DamageResult r = Hit(Hero(level: 5), 37.9f, DamageKind.Real, Monster(def: 999, mdef: 999), NoProc, NoProc);
			Assert.Equal(37, r.Damage);
		}

		[Fact]
		public void Magic_UsesMdef()
		{
			// 同级 威力 100，魔防 80：80/180 = 0.444 → 100 × 0.556 = 55.6 → 55
			DamageResult r = Hit(Hero(level: 5), 100f, DamageKind.Magic, Monster(), NoProc, NoProc);
			Assert.Equal(55, r.Damage);
		}

		[Fact]
		public void Penetration_ReducesDefense_ClampedAtZero()
		{
			// 破甲 60 > 物防 50 → 净物防 0，不减伤
			DamageResult r = Hit(Hero(level: 5, ar: 60), 100f, DamageKind.Physics, Monster(), NoProc, NoProc);
			Assert.Equal(100, r.Damage);
		}

		[Fact]
		public void Miss_ReturnsZeroAndFlag()
		{
			// 同级，怪物闪避 70：70/(70+70) = 0.5；roll 0.49 < 0.5 → 闪避
			DamageResult r = Hit(Hero(level: 5), 100f, DamageKind.Physics, Monster(miss: 70),
				0.49f, 0f);
			Assert.True(r.IsMiss);
			Assert.Equal(0, r.Damage);
		}

		[Fact]
		public void Miss_RollAtRate_DoesNotMiss()
		{
			DamageResult r = Hit(Hero(level: 5), 100f, DamageKind.Real, Monster(miss: 70), 0.5f, 0.9999f);
			Assert.False(r.IsMiss);
		}

		[Fact]
		public void ZeroRates_NeverProc_EvenWithZeroRolls()
		{
			// 旧项目 <= 判定在率为 0、roll 恰为 0 时仍会触发；新实现率为 0 必不触发
			DamageResult r = Hit(Hero(level: 5), 100f, DamageKind.Real, Monster(), 0f, 0f);
			Assert.False(r.IsMiss);
			Assert.False(r.IsCrit);
			Assert.Equal(100, r.Damage);
		}

		[Fact]
		public void Htarget_OffsetsMiss()
		{
			// 闪避 70 − 命中 70 = 0 → 闪避率 0
			DamageResult r = Hit(Hero(level: 5, htarget: 70), 100f, DamageKind.Real, Monster(miss: 70),
				0f, 0.9999f);
			Assert.False(r.IsMiss);
		}

		[Fact]
		public void Crit_HeroAttacks_UsesHeroLuckyK()
		{
			// 同级，暴击 100 → 率 0.5；幸运 100、英雄攻击幸运K 100 → 倍率 2 + 0.5 = 2.5
			// 真实伤害：100 × 2.5 = 250
			DamageResult r = Hit(Hero(level: 5, crit: 100, lucky: 100), 100f, DamageKind.Real, Monster(),
				0.9999f, 0.49f);
			Assert.True(r.IsCrit);
			Assert.Equal(250, r.Damage);
		}

		[Fact]
		public void Crit_MonsterAttacks_UsesMonsterLuckyK()
		{
			// 同级，幸运 50、怪物攻击幸运K 50 → 倍率 2.5；真实 100 → 250
			DamageResult r = Hit(Monster(level: 1, crit: 100, lucky: 50), 100f, DamageKind.Real, Hero(level: 1),
				0.9999f, 0.49f);
			Assert.True(r.IsCrit);
			Assert.Equal(250, r.Damage);
		}

		[Fact]
		public void Crit_ToughnessOffsetsLucky_CritReduceOffsetsCrit()
		{
			// 暴击 100 − 暴抗 100 = 0 → 不暴击
			DamageResult none = Hit(Hero(level: 5, crit: 100), 100f, DamageKind.Real, Monster(critReduce: 100),
				0.9999f, 0f);
			Assert.False(none.IsCrit);

			// 幸运 100 − 韧性 100 = 0 → 倍率仅基数 2
			DamageResult plain = Hit(Hero(level: 5, crit: 100, lucky: 100), 100f, DamageKind.Real,
				Monster(toughness: 100), 0.9999f, 0f);
			Assert.True(plain.IsCrit);
			Assert.Equal(200, plain.Damage);
		}

		[Fact]
		public void Crit_ThenDefense_OrderMatchesLegacy()
		{
			// 同级 威力 100，暴击倍率 2（幸运 0），物防 50 → 200 × 0.667 = 133.4 → 133
			DamageResult r = Hit(Hero(level: 5, crit: 100), 100f, DamageKind.Physics, Monster(),
				0.9999f, 0f);
			Assert.Equal(133, r.Damage);
		}

		[Fact]
		public void LevelSuppression_MonsterDefends_CapsAtTwoLevels()
		{
			// 英雄 10 级打 1 级怪：d=9 但封顶 2 级 → 100 × 1.1 = 110（真实伤害，隔离减伤）
			DamageResult r = Hit(Hero(level: 10), 100f, DamageKind.Real, Monster(level: 1), NoProc, NoProc);
			Assert.Equal(110, r.Damage);
		}

		[Fact]
		public void LevelSuppression_HeroDefends_CapsAtFiveLevels()
		{
			// 1 级英雄被 10 级怪打：封顶 5 级，攻击方(怪)等级高 → ×1.25
			// （回归：表系数是 float，×0.75 一侧曾因 74.9999996 被截成 74，见 DamageCalculator.TruncateEpsilon）
			DamageResult up = Hit(Monster(level: 10), 100f, DamageKind.Real, Hero(level: 1), NoProc, NoProc);
			Assert.Equal(125, up.Damage);

			// 10 级英雄被 1 级怪打 → ×0.75
			DamageResult down = Hit(Monster(level: 1), 100f, DamageKind.Real, Hero(level: 10), NoProc, NoProc);
			Assert.Equal(75, down.Damage);
		}

		[Fact]
		public void LevelSuppression_MissScalesAndCaps()
		{
			// 防守方(怪物)等级高 10 级：闪避系数 min(10×0.04, 0.9) = 0.4 → 闪避 70 × 1.4 = 98
			// 率 98/(98+70) = 0.583；roll 0.58 → 闪避，0.59 → 不闪避
			CombatantStats hero = Hero(level: 1);
			CombatantStats monkey = Monster(level: 11, miss: 70);
			Assert.True(Hit(hero, 100f, DamageKind.Real, monkey, 0.58f, 0.9999f).IsMiss);
			Assert.False(Hit(hero, 100f, DamageKind.Real, monkey, 0.59f, 0.9999f).IsMiss);

			// 攻击方(英雄)等级高 30 级：闪避系数封顶 0.9 → 70 × 0.1 = 7 → 7/77 = 0.091
			CombatantStats strongHero = Hero(level: 31);
			CombatantStats weakMonkey = Monster(level: 1, miss: 70);
			Assert.True(Hit(strongHero, 100f, DamageKind.Real, weakMonkey, 0.09f, 0.9999f).IsMiss);
			Assert.False(Hit(strongHero, 100f, DamageKind.Real, weakMonkey, 0.092f, 0.9999f).IsMiss);
		}

		[Fact]
		public void LevelSuppression_CritScalesUpForHigherAttacker()
		{
			// 英雄高 5 级打怪：暴击系数 min(5×0.11, 1) = 0.55 → 暴击 50 × 1.55 = 77.5 → 率 77.5/177.5 = 0.437
			CombatantStats hero = Hero(level: 10, crit: 50);
			CombatantStats monkey = Monster(level: 5);
			Assert.True(Hit(hero, 100f, DamageKind.Real, monkey, 0.9999f, 0.436f).IsCrit);
			Assert.False(Hit(hero, 100f, DamageKind.Real, monkey, 0.9999f, 0.438f).IsCrit);
		}

		[Fact]
		public void Damage_NeverNegative()
		{
			DamageResult r = Hit(Hero(level: 5), 0f, DamageKind.Physics, Monster(), NoProc, NoProc);
			Assert.Equal(0, r.Damage);
		}

		[Fact]
		public void Knockback_DirectionAndPerSideScale()
		{
			AttackData attack = AttackData.Create(1004, Hero(), 10f, DamageKind.Physics, new Vector2(6, -5), -1, 0,
				SoundId.None);
			try
			{
				// 怪物被击退：X × 30、Y × 15，方向随出招朝左
				Assert.Equal(new Vector2(-180, -75), DamageCalculator.KnockbackVelocity(Config, attack, CombatSide.Monster));
				// 英雄被击退：X × 25
				Assert.Equal(new Vector2(-150, -75), DamageCalculator.KnockbackVelocity(Config, attack, CombatSide.Hero));
			}
			finally
			{
				ReferencePool.Release(attack);
			}
		}

		[Fact]
		public void AttackData_OneHitPerTarget_AndClearedOnRelease()
		{
			AttackData attack = AttackData.Create(1001, Hero(), 8f, DamageKind.Physics, Vector2.Zero, 1, 0,
				SoundId.None);
			Assert.True(attack.TryRegisterHit(42));
			Assert.False(attack.TryRegisterHit(42));
			Assert.True(attack.TryRegisterHit(43));
			ReferencePool.Release(attack);

			// 同类型复用池内对象：必须是干净的
			AttackData reused = AttackData.Create(1002, Hero(), 9f, DamageKind.Magic, Vector2.Zero, 1, 0,
				SoundId.None);
			Assert.True(reused.TryRegisterHit(42));
			Assert.Equal(1002, reused.AttackId);
			Assert.Equal(DamageKind.Magic, reused.Kind);
			ReferencePool.Release(reused);
		}

	}
}
