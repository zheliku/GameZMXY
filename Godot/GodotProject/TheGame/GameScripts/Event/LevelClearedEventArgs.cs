using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event
{
	/// <summary>
	/// 关卡通关事件（英雄触碰已开启的出口）。发布方：LevelDirector；
	/// 订阅方：ProcedureBattle（打开结算界面、写存档）。按根规范 §9 走 ReferencePool。
	///
	/// 统计值是快照：关卡用时（秒）、击杀数、英雄剩余血量（供结算界面显示与存档比较）。
	/// </summary>
	public sealed class LevelClearedEventArgs : GameEventArgs
	{
		/// <summary>事件编号</summary>
		public static readonly int EventId = typeof(LevelClearedEventArgs).GetHashCode();

		/// <inheritdoc />
		public override int Id => EventId;

		/// <summary>关卡ID（LevelConfig.Id）</summary>
		public int LevelId { get; private set; }

		/// <summary>本局用时（秒，进场到触碰出口）</summary>
		public float ElapsedSeconds { get; private set; }

		/// <summary>击杀怪物数</summary>
		public int KillCount { get; private set; }

		/// <summary>英雄剩余生命</summary>
		public int HeroHp { get; private set; }

		/// <summary>英雄最大生命</summary>
		public int HeroMaxHp { get; private set; }

		public static LevelClearedEventArgs Create(int levelId, float elapsedSeconds, int killCount,
			int heroHp, int heroMaxHp)
		{
			LevelClearedEventArgs e = ReferencePool.Acquire<LevelClearedEventArgs>();
			e.LevelId = levelId;
			e.ElapsedSeconds = elapsedSeconds;
			e.KillCount = killCount;
			e.HeroHp = heroHp;
			e.HeroMaxHp = heroMaxHp;
			return e;
		}

		/// <summary>转发复制（根规范 §5.2）</summary>
		public static LevelClearedEventArgs Create(LevelClearedEventArgs source)
		{
			return Create(source.LevelId, source.ElapsedSeconds, source.KillCount, source.HeroHp, source.HeroMaxHp);
		}

		public override void Clear()
		{
			LevelId = 0;
			ElapsedSeconds = 0f;
			KillCount = 0;
			HeroHp = 0;
			HeroMaxHp = 0;
		}
	}
}
