using GameLogic.Battle.Attributes;
using GameLogic.Battle.Attacks;
using System;
using GameConfig.Attributes;

namespace GameLogic.Battle.Damage
{
	/// <summary>
	/// 结算侧别：决定 <see cref="DamageHelper"/> 取 BattleConfig 的哪一组常数
	/// （旧项目人怪两侧的 K 值、等级压制系数与封顶都不一致）。
	/// 它只描述"按哪套公式算"，不是阵营——敌我关系仍由物理层表达（根规范 §7）。
	/// </summary>
	public enum CombatSide
	{
		/// <summary>英雄</summary>
		Hero = 0,

		/// <summary>怪物</summary>
		Monster = 1,
	}

	/// <summary>命中结算使用的一方属性快照；攻击方在出招时冻结，防守方在受击时冻结，按值复制。</summary>
	public readonly struct CombatantAttributes
	{
		/// <summary>来源运行实体编号；场景外的公式计算可为空，不持有节点引用。</summary>
		public readonly int? ActorId;

		/// <summary>结算侧别（选常数组用）</summary>
		public readonly CombatSide Side;

		/// <summary>等级（参与等级压制）</summary>
		public readonly int Level;

		/// <summary>攻击力（乘攻击倍率得到本招威力）</summary>
		public readonly int Power;

		/// <summary>物防</summary>
		public readonly int Def;

		/// <summary>魔防</summary>
		public readonly int Mdef;

		/// <summary>暴击</summary>
		public readonly int Crit;

		/// <summary>闪避</summary>
		public readonly int Miss;

		/// <summary>幸运（加暴击倍率）</summary>
		public readonly int Lucky;

		/// <summary>韧性（减对方幸运）</summary>
		public readonly int Toughness;

		/// <summary>命中（减对方闪避）</summary>
		public readonly int Htarget;

		/// <summary>暴击抵抗（减对方暴击）</summary>
		public readonly int CritReduce;

		/// <summary>破甲（减对方物防）</summary>
		public readonly int Ar;

		/// <summary>破魔（减对方魔防）</summary>
		public readonly int Sp;

		/// <summary>从属性汇总取当前最终值生成快照（出招与受击时各取一次，之后不随属性变化）。</summary>
		/// <param name="side">参与结算的一方。</param>
		/// <param name="level">等级。</param>
		/// <param name="attributes">角色的属性汇总。</param>
		/// <param name="actorId">来源运行实体编号；非实体来源可以省略。</param>
		/// <returns>当前最终值的结算快照。</returns>
		/// <exception cref="ArgumentNullException">属性汇总为空。</exception>
		public static CombatantAttributes From(CombatSide side, int level, ActorAttributes attributes, int? actorId = null)
		{
			ArgumentNullException.ThrowIfNull(attributes);
			return new CombatantAttributes(side, level, attributes.Power, attributes.Def,
				attributes.Mdef, attributes.Crit, attributes.Miss,
				attributes.Lucky, attributes.Toughness, attributes.Htarget,
				attributes.CritReduce, attributes.Ar, attributes.Sp, actorId);
		}

		/// <summary>初始化战斗属性快照。</summary>
		/// <param name="side">参与结算的一方。</param>
		/// <param name="level">等级。</param>
		/// <param name="power">攻击力。</param>
		/// <param name="def">物理防御。</param>
		/// <param name="mdef">魔法防御。</param>
		/// <param name="crit">暴击。</param>
		/// <param name="miss">闪避。</param>
		/// <param name="lucky">幸运。</param>
		/// <param name="toughness">韧性。</param>
		/// <param name="htarget">命中。</param>
		/// <param name="critReduce">暴击抵抗。</param>
		/// <param name="ar">破甲。</param>
		/// <param name="sp">破魔。</param>
		/// <param name="actorId">来源运行实体编号；场景外计算可为空。</param>
		public CombatantAttributes(CombatSide side, int level, int power, int def, int mdef, int crit, int miss,
			int lucky, int toughness, int htarget, int critReduce, int ar, int sp, int? actorId = null)
		{
			ActorId = actorId;
			Side = side;
			Level = level;
			Power = power;
			Def = def;
			Mdef = mdef;
			Crit = crit;
			Miss = miss;
			Lucky = lucky;
			Toughness = toughness;
			Htarget = htarget;
			CritReduce = critReduce;
			Ar = ar;
			Sp = sp;
		}
	}
}
