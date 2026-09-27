namespace GameLogic.Entity
{
	/// <summary>
	/// 标准动画名常量（AGENTS 8.2 命名标准）。
	/// 动画名定义在 SpriteFrames（.tres）里，代码侧只允许通过这里的常量引用，禁止裸字符串——
	/// 改名时编译器会兜底。攻击/技能动画名走配置表（AttackConfig.Animation / SkillConfig），不在此列。
	/// </summary>
	public static class ActorAnim
	{
		/// <summary>常态待机（循环）</summary>
		public const string Idle = "idle1";

		/// <summary>待机偶发小动作（憨笑），播完回 Idle</summary>
		public const string IdleEmote = "idle2";

		/// <summary>慢走</summary>
		public const string Walk = "walk";

		/// <summary>跑步（快走）</summary>
		public const string Run = "run";

		/// <summary>第一段跳（上升段）</summary>
		public const string Jump = "jump";

		/// <summary>第二段跳（空中二段跳）</summary>
		public const string Jump2 = "jump_2";

		/// <summary>下落（迁移自旧项目 drop）</summary>
		public const string Fall = "fall";

		/// <summary>受击</summary>
		public const string Hurt = "hurt";

		/// <summary>死亡</summary>
		public const string Death = "death";
	}
}
