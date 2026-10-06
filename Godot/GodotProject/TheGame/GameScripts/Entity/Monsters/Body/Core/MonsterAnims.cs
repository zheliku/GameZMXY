namespace GameLogic.Entity.Monsters.Body
{
	/// <summary>
	/// 怪物标准动画名（Entity/AGENTS.md「命名标准」，所有怪物的动画库都用这套动画名）。
	/// 攻击动画名取自 AttackConfig.Animation（<see cref="MonsterBodyParams.AttackAnims"/>）。
	/// </summary>
	/// <summary>怪物通用动画库中的标准动画名。</summary>
	public static class MonsterAnims
	{
		/// <summary>待机动画名。</summary>
		public const string Idle = "idle";
		/// <summary>奔跑动画名。</summary>
		public const string Run = "run";
		/// <summary>受击动画名。</summary>
		public const string Hurt = "hurt";
		/// <summary>死亡动画名。</summary>
		public const string Death = "death";
	}
}
