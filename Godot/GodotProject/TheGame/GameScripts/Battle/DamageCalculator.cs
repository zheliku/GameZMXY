using System;
using GameConfig.Battle;
using Godot;

namespace GameLogic.Battle
{
	/// <summary>
	/// 伤害结算（纯函数，无状态、无随机；只触 Godot 纯值类型，无运行时引擎依赖）。
	/// 常数全部来自 BattleConfig 单行表
	/// （调用方传 <c>ConfigSystem.Instance.Tables.TbBattleConfig.Data</c>，单测直接反序列化同一份 .bytes）。
	///
	/// 公式结构参考旧项目（英雄防守 BaseHero.gd get_Monster_last_hurt / 怪物防守 BaseMonster.gd get_Role_last_hurt），
	/// 用统一流程重写，人怪差异只体现在"取哪组常数"：
	///
	///   1. 净值：闪避 = 防.闪避 − 攻.命中；暴击 = 攻.暴击 − 防.暴抗；幸运 = 攻.幸运 − 防.韧性；
	///      物防 = 防.物防 − 攻.破甲；魔防 = 防.魔防 − 攻.破魔（全部下限 0）。
	///   2. 等级压制（d = |等级差|）：等级高的一方占优——
	///      闪避 ×(1 ∓ min(d·闪避系数, 封顶))、暴击/幸运 ×(1 ± min(d·系数, 封顶))、
	///      威力 ×(1 ± min(d, 伤害封顶级数)·每级伤害系数) 后取整。
	///   3. 闪避率 = 闪避/(闪避+K闪)，roll &lt; 率 → 闪避，伤害 0。
	///   4. 暴击率 = 暴击/(暴击+K暴)，roll &lt; 率 → 威力 ×(CritBase + 幸运/(幸运+K幸))。
	///   5. 减伤 x/(x+K)：物理吃物防、魔法吃魔防，真实不减；取整。
	///   比率均按旧项目 snapped(…, 0.001) 保留三位小数。
	///
	/// 常数分侧（防守方决定）：
	///   | 防守方 | 物防K / 魔防K | 闪避K | 暴击幸运K（攻击方侧）| 等级系数/封顶 |
	///   | 英雄   | DefK_Hero / MdefK_Hero | MissK_Hero | LuckyK_MonsterAtk | *_HeroDef |
	///   | 怪物   | DefK_Monster / MdefK_Monster | MissK_Monster | LuckyK_HeroAtk | *_MonsterDef |
	///   暴击率 K、暴击倍率基数、幸运系数与封顶、暴击封顶两侧共用。
	///
	/// 与旧项目的有意差异（借鉴不照抄）：
	///  * 旧"英雄防守"分支里，怪物等级高时英雄的**闪避也上升**（BaseHero.gd:766），与其余各项
	///    "高等级方占优"的方向相反，属旧代码笔误；这里统一为高等级方占优（等级相同时两者一致）。
	///  * 旧判定用 roll &lt;= 率，率为 0 时 roll 恰为 0 仍会闪避/暴击；这里用 &lt;，率为 0 必不触发。
	///  * 旧"怪物防守"分支在净值 ≤0 时才跳过压制缩放，结果与先钳 0 再缩放一致，合并为一条路径。
	/// </summary>
	public static class DamageCalculator
	{
		/// <summary>比率保留精度（旧项目 snapped(x, 0.001)）</summary>
		private const double RatioStep = 0.001;

		/// <summary>
		/// 取整容差：表里的系数是 float（0.05f 转 double 为 0.0500000007…），100×(1−5×0.05) 会算成
		/// 74.9999996 被截成 74；旧项目用 double 字面量不存在该误差。取整前加一个远小于 1 点伤害的容差对齐。
		/// </summary>
		private const double TruncateEpsilon = 1e-6;

		/// <summary>
		/// 结算一次命中。
		/// </summary>
		/// <param name="config">战斗常数（BattleConfig 单行表）</param>
		/// <param name="attack">攻击包（含攻击方快照与本招威力）</param>
		/// <param name="defender">防守方属性快照</param>
		/// <param name="rolls">随机数（由 Godot 层取好传入）</param>
		public static DamageResult Calculate(BattleConfig config, AttackData attack, in CombatantStats defender,
			in DamageRolls rolls)
		{
			if (config == null)
			{
				throw new ArgumentNullException(nameof(config));
			}

			if (attack == null)
			{
				throw new ArgumentNullException(nameof(attack));
			}

			CombatantStats attacker = attack.Attacker;
			bool heroDefends = defender.Side == CombatSide.Hero;

			// 1. 净值（下限 0）
			double miss = Math.Max(0, defender.Miss - attacker.Htarget);
			double crit = Math.Max(0, attacker.Crit - defender.CritReduce);
			double lucky = Math.Max(0, attacker.Lucky - defender.Toughness);
			double def = Math.Max(0, defender.Def - attacker.Ar);
			double mdef = Math.Max(0, defender.Mdef - attacker.Sp);

			// 2. 等级压制：sign = +1 攻击方等级高，-1 防守方等级高，0 同级
			int levelGap = attacker.Level - defender.Level;
			int d = Math.Abs(levelGap);
			int sign = Math.Sign(levelGap);

			float missCoef = heroDefends ? config.LvMissCoefHeroDef : config.LvMissCoefMonsterDef;
			float missCap = heroDefends ? config.LvMissCapHeroDef : config.LvMissCapMonsterDef;
			float critCoef = heroDefends ? config.LvCritCoefHeroDef : config.LvCritCoefMonsterDef;
			int damageCapLv = heroDefends ? config.LvDamageCapLvHeroDef : config.LvDamageCapLvMonsterDef;

			double missBl = Math.Min(d * (double)missCoef, missCap);
			double critBl = Math.Min(d * (double)critCoef, config.LvCritCap);
			double luckyBl = Math.Min(d * (double)config.LvLuckyCoef, config.LvLuckyCap);
			double damageBl = Math.Min(d, damageCapLv) * (double)config.LvDamageCoef;

			miss *= 1 - sign * missBl;
			crit *= 1 + sign * critBl;
			lucky *= 1 + sign * luckyBl;
			double hurt = Truncate(attack.Power * (1 + sign * damageBl));

			// 3. 闪避
			double missK = heroDefends ? config.MissKHero : config.MissKMonster;
			if (rolls.Miss < Ratio(miss, missK))
			{
				return DamageResult.Missed(attack.Kind);
			}

			// 4. 暴击
			bool isCrit = rolls.Crit < Ratio(crit, config.CritK);
			if (isCrit)
			{
				double luckyK = heroDefends ? config.LuckyKMonsterAtk : config.LuckyKHeroAtk;
				hurt *= config.CritBase + Ratio(lucky, luckyK);
			}

			// 5. 减伤
			switch (attack.Kind)
			{
				case DamageKind.Physics:
					hurt *= 1 - Ratio(def, heroDefends ? config.DefKHero : config.DefKMonster);
					break;
				case DamageKind.Magic:
					hurt *= 1 - Ratio(mdef, heroDefends ? config.MdefKHero : config.MdefKMonster);
					break;
			}

			int damage = (int)Math.Max(0, Truncate(hurt));
			return new DamageResult(damage, false, isCrit, attack.Kind);
		}

		/// <summary>
		/// 击退速度（px/s）：表单位 × BattleConfig 换算系数，横向按出招方向，纵向向上为负。
		/// 横向换算按防守方分侧（旧项目英雄被击退 ×25、怪物 ×30），纵向两侧一致。
		/// </summary>
		public static Vector2 KnockbackVelocity(BattleConfig config, AttackData attack, CombatSide defenderSide)
		{
			float scaleX = defenderSide == CombatSide.Hero ? config.KnockbackScaleXHeroDef : config.KnockbackScaleXMonsterDef;
			return new Vector2(attack.Direction * attack.Knockback.X * scaleX, attack.Knockback.Y * config.KnockbackScaleY);
		}

		/// <summary>向零取整（旧 int()），带 float 系数容差（见 <see cref="TruncateEpsilon"/>）。</summary>
		private static double Truncate(double value)
		{
			return value >= 0 ? Math.Floor(value + TruncateEpsilon) : Math.Ceiling(value - TruncateEpsilon);
		}

		/// <summary>x/(x+K)，保留三位小数（旧项目 snapped(x, 0.001)）；x、K 均为 0 时视为 0。
		/// Godot 4 的 Mathf.Snapped 同为 floor(x/step+0.5)*step；x≥0 域内二者一致。</summary>
		private static double Ratio(double x, double k)
		{
			double denominator = x + k;
			if (denominator <= 0)
			{
				return 0;
			}

			return Mathf.Snapped(x / denominator, RatioStep);
		}
	}
}
