namespace GameLogic.Entity.Heroes.Body
{
	/// <summary>
	/// 英雄身体参数（值快照）：HeroEntity 从 HeroConfig / AttackConfig / 动画库拷入，身体状态只读这里。
	/// 纯 C#——状态不直接依赖 Luban 生成类，单测用对象初始化器直接构造。
	/// </summary>
	public readonly struct HeroBodyParams
	{
		/// <summary>慢走速度 px/s（HeroConfig.WalkSpeed）</summary>
		public float WalkSpeed { get; init; }

		/// <summary>跑步速度 px/s（HeroConfig.RunSpeed）</summary>
		public float RunSpeed { get; init; }

		/// <summary>起跳速度 px/s，向上为正（HeroConfig.JumpSpeed）</summary>
		public float JumpSpeed { get; init; }

		/// <summary>重力 px/s²（HeroConfig.Gravity）</summary>
		public float Gravity { get; init; }

		/// <summary>最多跳跃次数（含地面一段，HeroConfig.JumpCountMax）</summary>
		public int JumpCountMax { get; init; }

		/// <summary>待机小动作动画名（角色专属，空 = 没有待机小动作）</summary>
		public string EmoteAnim { get; init; }

		/// <summary>待机小动作时长秒（= 其动画长度；状态计时）</summary>
		public float EmoteTime { get; init; }

		/// <summary>受击硬直秒（= hurt 动画长度；0 = 不硬直）</summary>
		public float HurtTime { get; init; }

		/// <summary>连续静止多久后播一次小动作：区间下限 / 上限秒（HeroConfig.IdleEmoteDelay）</summary>
		public float EmoteDelayMin { get; init; }

		/// <summary>待机小动作触发间隔的最大秒数。</summary>
		public float EmoteDelayMax { get; init; }

		/// <summary>普攻连段每段的动画名（下标 = 段序号，AttackConfig.Animation 按 ComboIndex 排序）</summary>
		public string[] AttackAnims { get; init; }

		/// <summary>普攻连段每段的时长秒（= 段动画长度，OnInit 读动画资源；状态计时）</summary>
		public float[] AttackTimes { get; init; }

		/// <summary>普攻段数</summary>
		public int ComboLength => AttackAnims?.Length ?? 0;
	}
}
