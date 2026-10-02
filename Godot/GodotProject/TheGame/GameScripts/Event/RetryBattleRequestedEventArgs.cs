using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event
{
	/// <summary>
	/// 请求重试对局事件（结算界面“再次挑战”）。发布方：GameOverForm；订阅方：ProcedureBattle
	/// （重进自身流程，收尾由 OnLeave 统一完成）。按根规范 §9 走 ReferencePool。
	/// </summary>
	public sealed class RetryBattleRequestedEventArgs : GameEventArgs
	{
		/// <summary>事件编号</summary>
		public static readonly int EventId = typeof(RetryBattleRequestedEventArgs).GetHashCode();

		/// <inheritdoc />
		public override int Id => EventId;

		public static RetryBattleRequestedEventArgs Create()
		{
			return ReferencePool.Acquire<RetryBattleRequestedEventArgs>();
		}

		/// <summary>转发复制（根规范 §5.2）</summary>
		public static RetryBattleRequestedEventArgs Create(RetryBattleRequestedEventArgs source)
		{
			return Create();
		}

		public override void Clear()
		{
		}
	}
}
