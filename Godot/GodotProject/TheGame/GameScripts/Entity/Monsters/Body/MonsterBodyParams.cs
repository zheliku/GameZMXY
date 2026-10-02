namespace GameLogic.Entity.Monsters.Body
{
	/// <summary>
	/// 怪物身体参数（值快照）：MonsterEntity 从 MonsterConfig / AttackConfig 拷入，身体状态只读这里。
	/// 纯 C#——单测用对象初始化器直接构造。动作时长 = 动画长度（OnInit 读动画资源；状态计时）。
	/// </summary>
	public readonly struct MonsterBodyParams
	{
		/// <summary>移动速度 px/s（MonsterConfig.MoveSpeed）</summary>
		public float MoveSpeed { get; init; }

		/// <summary>重力 px/s²（MonsterConfig.Gravity）</summary>
		public float Gravity { get; init; }

		/// <summary>受击硬直秒（= hurt 动画长度）</summary>
		public float HurtTime { get; init; }

		/// <summary>死亡到回收的秒数（= death 动画长度）</summary>
		public float DeathTime { get; init; }

		/// <summary>每招的攻击动画名（下标 = 招式序号，AttackConfig.Animation 按 ComboIndex 排序）</summary>
		public string[] AttackAnims { get; init; }

		/// <summary>每招的时长秒（= 招式动画长度，OnInit 读动画资源；状态计时）</summary>
		public float[] AttackTimes { get; init; }

		/// <summary>每招的收招硬直秒（AttackConfig.AiRecovery；下标同上）——表数值，与动画无关</summary>
		public float[] AttackRecovery { get; init; }

		/// <summary>招式数</summary>
		public int AttackCount => AttackAnims?.Length ?? 0;
	}
}
