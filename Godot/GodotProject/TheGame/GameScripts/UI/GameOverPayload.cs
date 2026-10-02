namespace GameLogic
{
	/// <summary>
	/// 结算界面的开屏载荷（纯数据，不引用实体：实体随对局收尾可能被回收）。
	/// 由对局流程从 LevelCleared/LevelFailed 事件快照拷贝，经 <c>OpenUIForm</c> 的 userData 传入
	/// <see cref="GameOverForm"/>（根规范 §8：生成方显式注入，UI 不反查实体）。
	/// </summary>
	public sealed class GameOverPayload
	{
		/// <summary>是否通关（false = 英雄阵亡的失败结算）</summary>
		public bool IsVictory;

		/// <summary>关卡ID（LevelConfig.Id）</summary>
		public int LevelId;

		/// <summary>本局用时（秒；失败结算为 0 不显示）</summary>
		public float ElapsedSeconds;

		/// <summary>击杀怪物数</summary>
		public int KillCount;

		/// <summary>英雄剩余生命（快照）</summary>
		public int HeroHp;

		/// <summary>英雄最大生命（快照）</summary>
		public int HeroMaxHp;
	}
}
