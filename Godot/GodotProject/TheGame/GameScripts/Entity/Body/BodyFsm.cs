using GameFramework.Fsm;

namespace GameLogic.Entity.Body
{
	/// <summary>
	/// 身体状态机的扩展成员：为 GF.Fsm 的 <see cref="IFsm{T}"/> 补充物理帧推进。
	/// 调度属于状态机，单个状态的决策属于 <see cref="BodyState{TBody}"/>；动画不回调代码。
	/// 状态机未运行 / 已销毁 / 当前状态不是身体状态时一律无操作（实体隐藏期间安全调用）。
	/// </summary>
	public static class BodyFsm
	{
		private const int MaxHopsPerFrame = 4; // 同一物理帧的连续状态切换上限，防止状态互相切换形成死循环。

		/// <summary>为身体宿主的状态机补充物理帧驱动。</summary>
		/// <typeparam name="TBody">英雄或怪物的身体宿主接口。</typeparam>
		/// <param name="fsm">身体状态机；允许为空、尚未运行或已销毁。</param>
		extension<TBody>(IFsm<TBody> fsm) where TBody : class, IActorBody
		{
			/// <summary>物理帧推进；切换后的状态在同帧执行决策，最多连续切换四次。</summary>
			/// <param name="dt">物理帧间隔，单位秒；在实体 MoveAndSlide 前调用。</param>
			public void Tick(float dt)
			{
				// 新状态同帧接管移动与动作，避免受击、起跳或硬直结束后晚一帧生效。
				for (int hop = 0; hop < MaxHopsPerFrame; hop++)
				{
					if (fsm is not { IsDestroyed: false, IsRunning: true } ||
					    fsm.CurrentState is not BodyState<TBody> state)
					{
						return;
					}

					int serial = state.EnterSerial;
					state.PhysicsTick(fsm, dt);

					// 没切换也没重新进入才结束；切回同一实例仍让新一轮立即决策。
					if (fsm.CurrentState == state && state.EnterSerial == serial)
					{
						return;
					}
				}
			}
		}
	}
}
