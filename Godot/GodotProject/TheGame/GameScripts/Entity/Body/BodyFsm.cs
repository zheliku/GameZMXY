using GameFramework.Fsm;

namespace GameLogic.Entity.Body
{
	/// <summary>
	/// 身体状态机的驱动入口（无状态静态辅助）。状态机本身就是 GF.Fsm 的 <see cref="IFsm{T}"/>，
	/// 这里只补上"按物理帧推进"这一件框架没有的事（见 <see cref="BodyState{TBody}"/> 注释）。
	/// **状态机单入口**：A 方案下动画不回调代码，Tick 是进状态机的唯一通道。
	/// 状态机未运行 / 已销毁 / 当前状态不是身体状态时一律无操作（实体隐藏期间安全调用）。
	/// </summary>
	public static class BodyFsm
	{
		/// <summary>
		/// 一个物理帧内最多连续切换几次状态。切换当帧由新状态立即执行本帧决策（受击当帧就击退、
		/// 起跳当帧就受重力、硬直结束当帧就能走），不留"晚一帧"；上限只防状态之间互相踢皮球的死循环。
		/// </summary>
		public const int MaxHopsPerFrame = 4;

		/// <summary>物理帧推进当前状态（实体 _PhysicsProcess 里、MoveAndSlide 之前调用）。</summary>
		public static void Tick<T>(IFsm<T> fsm, float dt) where T : class, IActorBody
		{
			for (int hop = 0; hop < MaxHopsPerFrame; hop++)
			{
				BodyState<T> state = Current(fsm);
				if (state == null)
				{
					return;
				}

				int serial = state.EnterSerial;
				state.PhysicsTick(fsm, dt);

				// 没切换（含切回自己 = 重新进入）就结束本帧
				if (Current(fsm) == state && state.EnterSerial == serial)
				{
					return;
				}
			}
		}

		/// <summary>当前状态名（调试/冒烟观测；无状态机为空串）。</summary>
		public static string CurrentName<T>(IFsm<T> fsm) where T : class, IActorBody
		{
			return Current(fsm)?.StateName ?? "";
		}

		/// <summary>当前状态是否为 TState（无状态机为 false）。</summary>
		public static bool IsIn<T, TState>(IFsm<T> fsm) where T : class, IActorBody where TState : BodyState<T>
		{
			return Current(fsm) is TState;
		}

		private static BodyState<T> Current<T>(IFsm<T> fsm) where T : class, IActorBody // 仅返回有效运行中的身体状态。
		{
			if (fsm == null || fsm.IsDestroyed || !fsm.IsRunning)
			{
				return null;
			}

			return fsm.CurrentState as BodyState<T>;
		}
	}
}
