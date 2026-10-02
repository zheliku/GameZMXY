using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event
{
	/// <summary>
	/// 波次清空事件（该波队列已刷完且场上怪物归零）。发布方：LevelDirector；
	/// 订阅方：HUD（显示 gogo 前进箭头，直到下一波开始）。按根规范 §9 走 ReferencePool。
	/// </summary>
	public sealed class WaveClearedEventArgs : GameEventArgs
	{
		/// <summary>事件编号</summary>
		public static readonly int EventId = typeof(WaveClearedEventArgs).GetHashCode();

		/// <inheritdoc />
		public override int Id => EventId;

		/// <summary>关卡ID（LevelConfig.Id）</summary>
		public int LevelId { get; private set; }

		/// <summary>波次序号（1 起）</summary>
		public int WaveIndex { get; private set; }

		public static WaveClearedEventArgs Create(int levelId, int waveIndex)
		{
			WaveClearedEventArgs e = ReferencePool.Acquire<WaveClearedEventArgs>();
			e.LevelId = levelId;
			e.WaveIndex = waveIndex;
			return e;
		}

		/// <summary>转发复制（根规范 §5.2）</summary>
		public static WaveClearedEventArgs Create(WaveClearedEventArgs source)
		{
			return Create(source.LevelId, source.WaveIndex);
		}

		public override void Clear()
		{
			LevelId = 0;
			WaveIndex = 0;
		}
	}
}
