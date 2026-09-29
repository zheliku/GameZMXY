namespace GameLogic.Battle
{
	/// <summary>
	/// 结算侧别：决定 <see cref="DamageCalculator"/> 取 BattleConfig 的哪一组常数
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
}
