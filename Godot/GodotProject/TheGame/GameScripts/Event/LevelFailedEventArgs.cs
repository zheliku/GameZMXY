using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event
{
	/// <summary>
	/// 关卡失败事件（英雄死亡）。发布方：LevelDirector（订阅 HeroDiedEventArgs 转译，关卡层不认识英雄内部）；
	/// 订阅方：ProcedureBattle（延迟打开失败结算）。按根规范 §9 走 ReferencePool。
	///
	/// 统计值是快照（与 <see cref="LevelClearedEventArgs"/> 对称），供失败结算显示"坚持了多久/击杀数"。
	/// </summary>
	public sealed class LevelFailedEventArgs : GameEventArgs
	{
		/// <summary>事件编号</summary>
		public static readonly int EventId = typeof(LevelFailedEventArgs).GetHashCode();

		/// <inheritdoc />
		public override int Id => EventId;

		/// <summary>关卡ID（LevelConfig.Id）</summary>
		public int LevelId { get; private set; }

		/// <summary>击杀英雄的实体编号（0 = 无实体来源，如 Buff/调试伤害）</summary>
		public int KillerEntityId { get; private set; }

		/// <summary>阵亡时的本局用时（秒）</summary>
		public float ElapsedSeconds { get; private set; }

		/// <summary>阵亡时的击杀怪物数</summary>
		public int KillCount { get; private set; }

		public static LevelFailedEventArgs Create(int levelId, int killerEntityId, float elapsedSeconds, int killCount)
		{
			LevelFailedEventArgs e = ReferencePool.Acquire<LevelFailedEventArgs>();
			e.LevelId = levelId;
			e.KillerEntityId = killerEntityId;
			e.ElapsedSeconds = elapsedSeconds;
			e.KillCount = killCount;
			return e;
		}

		/// <summary>转发复制（根规范 §5.2）</summary>
		public static LevelFailedEventArgs Create(LevelFailedEventArgs source)
		{
			return Create(source.LevelId, source.KillerEntityId, source.ElapsedSeconds, source.KillCount);
		}

		public override void Clear()
		{
			LevelId = 0;
			KillerEntityId = 0;
			ElapsedSeconds = 0f;
			KillCount = 0;
		}
	}
}
