using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event
{
	/// <summary>
	/// 请求开始对局事件。发布方：HeroSelectForm（“开始战斗”按钮）；订阅方：ProcedureGame（切到对局流程）。
	/// UI 不驱动流程（跨模块只走事件，根规范 §9），流程收到后自行 ChangeState。按根规范 §9 走 ReferencePool。
	/// </summary>
	public sealed class StartBattleRequestedEventArgs : GameEventArgs
	{
		/// <summary>事件编号</summary>
		public static readonly int EventId = typeof(StartBattleRequestedEventArgs).GetHashCode();

		/// <inheritdoc />
		public override int Id => EventId;

		/// <summary>所选英雄ID（HeroConfig.Id）</summary>
		public int HeroId { get; private set; }

		public static StartBattleRequestedEventArgs Create(int heroId)
		{
			StartBattleRequestedEventArgs e = ReferencePool.Acquire<StartBattleRequestedEventArgs>();
			e.HeroId = heroId;
			return e;
		}

		/// <summary>转发复制（根规范 §5.2）</summary>
		public static StartBattleRequestedEventArgs Create(StartBattleRequestedEventArgs source)
		{
			return Create(source.HeroId);
		}

		public override void Clear()
		{
			HeroId = 0;
		}
	}
}
