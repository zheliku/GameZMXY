namespace GameLogic.Entity.Heroes.Body
{
	/// <summary>
	/// 英雄标准动画名（Entity/AGENTS.md「命名标准」，所有英雄的动画库都用这套动画名）。
	/// 角色专属动画（如悟空的憨笑 idle2）不在这里：由英雄类覆写给出（<see cref="HeroBodyParams.EmoteAnim"/>）；
	/// 普攻段动画名取自 AttackConfig.Animation（<see cref="HeroBodyParams.AttackAnims"/>）。
	/// </summary>
	public static class HeroAnims
	{
		public const string Idle = "idle1";
		public const string Idle2 = "idle2";
		public const string Walk = "walk";
		public const string Run = "run";
		public const string Jump = "jump";
		public const string Jump2 = "jump_2";
		public const string Fall = "fall";
		public const string Hurt = "hurt";
		public const string Death = "death";
	}
}
