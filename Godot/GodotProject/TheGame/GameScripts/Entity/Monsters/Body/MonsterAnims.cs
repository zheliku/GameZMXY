namespace GameLogic.Entity.Monsters.Body
{
	/// <summary>
	/// 怪物标准动画名（Entity/AGENTS.md「命名标准」，所有怪物的动画库都用这套动画名）。
	/// 攻击动画名取自 AttackConfig.Animation（<see cref="MonsterBodyParams.AttackAnims"/>）。
	/// </summary>
	public static class MonsterAnims
	{
		public const string Idle = "idle";
		public const string Run = "run";
		public const string Hurt = "hurt";
		public const string Death = "death";
	}
}
