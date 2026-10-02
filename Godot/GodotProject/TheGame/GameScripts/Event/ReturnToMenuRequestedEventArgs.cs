using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event
{
	/// <summary>
	/// 请求返回菜单事件（结算界面“返回选人”）。发布方：GameOverForm；订阅方：ProcedureBattle
	/// （切回 ProcedureGame）。按根规范 §9 走 ReferencePool。
	/// </summary>
	public sealed class ReturnToMenuRequestedEventArgs : GameEventArgs
	{
		/// <summary>事件编号</summary>
		public static readonly int EventId = typeof(ReturnToMenuRequestedEventArgs).GetHashCode();

		/// <inheritdoc />
		public override int Id => EventId;

		public static ReturnToMenuRequestedEventArgs Create()
		{
			return ReferencePool.Acquire<ReturnToMenuRequestedEventArgs>();
		}

		/// <summary>转发复制（根规范 §5.2）</summary>
		public static ReturnToMenuRequestedEventArgs Create(ReturnToMenuRequestedEventArgs source)
		{
			return Create();
		}

		public override void Clear()
		{
		}
	}
}
