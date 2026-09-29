namespace GameLogic.Battle
{
	/// <summary>
	/// 战斗属性快照：结算一次命中所需的一方全部属性（攻击方与防守方用同一结构）。
	///
	/// 用值类型而不是池化类：它只有十来个 int，按值拷贝即零分配、无归还义务，
	/// 也不会出现"快照被别人归还后还在读"的问题；池化留给有引用语义、生命周期跨帧的
	/// <see cref="AttackData"/>。
	/// </summary>
	public readonly struct CombatantStats
	{
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

		public CombatantStats(CombatSide side, int level, int power, int def, int mdef, int crit, int miss,
			int lucky, int toughness, int htarget, int critReduce, int ar, int sp)
		{
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
