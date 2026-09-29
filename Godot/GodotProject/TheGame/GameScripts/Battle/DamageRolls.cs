namespace GameLogic.Battle
{
	/// <summary>
	/// 一次结算用到的随机数（均为 [0,1) 均匀分布）。
	/// Battle/ 不自己取随机（GameScripts/AGENTS.md）：由 Godot 层取好传入，单测可精确构造分支。
	/// </summary>
	public readonly struct DamageRolls
	{
		/// <summary>闪避判定：Miss &lt; 闪避率 即闪避</summary>
		public readonly float Miss;

		/// <summary>暴击判定：Crit &lt; 暴击率 即暴击</summary>
		public readonly float Crit;

		public DamageRolls(float miss, float crit)
		{
			Miss = miss;
			Crit = crit;
		}
	}
}
