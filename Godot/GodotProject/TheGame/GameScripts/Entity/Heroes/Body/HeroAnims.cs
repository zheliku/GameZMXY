namespace GameLogic.Entity.Heroes.Body
{
	/// <summary>
	/// 英雄标准动画名（Entity/AGENTS.md「命名标准」，所有英雄的动画库都用这套动画名）。
	/// 角色专属动画（如悟空的憨笑 idle2）不在这里：由英雄类覆写给出（<see cref="HeroBodyParams.EmoteAnim"/>）；
	/// 普攻段动画名取自 AttackConfig.Animation（<see cref="HeroBodyParams.AttackAnims"/>）。
	/// </summary>
	/// <summary>英雄通用动画库中的标准动画名。</summary>
	public static class HeroAnims
	{
		/// <summary>基础待机动画名。</summary>
		public const string Idle = "idle1";
		/// <summary>待机小动作动画名。</summary>
		public const string Idle2 = "idle2";
		/// <summary>行走动画名。</summary>
		public const string Walk = "walk";
		/// <summary>跑步动画名。</summary>
		public const string Run = "run";
		/// <summary>首次跳跃动画名。</summary>
		public const string Jump = "jump";
		/// <summary>二段跳动画名。</summary>
		public const string Jump2 = "jump_2";
		/// <summary>下落动画名。</summary>
		public const string Fall = "fall";
		/// <summary>受击动画名。</summary>
		public const string Hurt = "hurt";
		/// <summary>死亡动画名。</summary>
		public const string Death = "death";
	}
}
