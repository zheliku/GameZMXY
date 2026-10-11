using GameLogic.Session;
using GameFramework;

namespace GameLogic.Startup.FlowData
{
	/// <summary>
	/// 流程状态机数据的 <see cref="PlayerSession"/> 包装（GGF 流程数据只接受 Variable）。
	/// 包装对象会随 SetData/RemoveData 被引用池回收，<see cref="PlayerSession"/> 本身不受影响。
	/// </summary>
	public sealed class PlayerSessionVariable : Variable<PlayerSession>
	{
		/// <summary>从引用池创建包装。</summary>
		/// <param name="value">要放入流程数据的档案作用域。</param>
		/// <returns>池化的包装对象，所有权交给流程状态机。</returns>
		public static PlayerSessionVariable Create(PlayerSession value)
		{
			PlayerSessionVariable variable = ReferencePool.Acquire<PlayerSessionVariable>();
			variable.Value = value;
			return variable;
		}
	}
}
