using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event
{
	/// <summary>
	/// 波次开始事件。发布方：LevelDirector（玩家 x 达到触发线或进关即触发第 1 波）；
	/// 订阅方：HUD（波次文本、隐藏 gogo 箭头）。按根规范 §9 走 ReferencePool。
	/// </summary>
	public sealed class WaveStartedEventArgs : GameEventArgs
	{
		/// <summary>事件编号</summary>
		public static readonly int EventId = typeof(WaveStartedEventArgs).GetHashCode();

		/// <inheritdoc />
		public override int Id => EventId;

		/// <summary>关卡ID（LevelConfig.Id）</summary>
		public int LevelId { get; private set; }

		/// <summary>波次序号（1 起）</summary>
		public int WaveIndex { get; private set; }

		/// <summary>该关卡总波数（HUD 显示“第 X/Y 波”）</summary>
		public int WaveTotal { get; private set; }

		public static WaveStartedEventArgs Create(int levelId, int waveIndex, int waveTotal)
		{
			WaveStartedEventArgs e = ReferencePool.Acquire<WaveStartedEventArgs>();
			e.LevelId = levelId;
			e.WaveIndex = waveIndex;
			e.WaveTotal = waveTotal;
			return e;
		}

		/// <summary>转发复制（根规范 §5.2）</summary>
		public static WaveStartedEventArgs Create(WaveStartedEventArgs source)
		{
			return Create(source.LevelId, source.WaveIndex, source.WaveTotal);
		}

		public override void Clear()
		{
			LevelId = 0;
			WaveIndex = 0;
			WaveTotal = 0;
		}
	}
}
