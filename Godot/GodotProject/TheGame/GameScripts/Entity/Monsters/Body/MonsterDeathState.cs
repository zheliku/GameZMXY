using GameFramework.Fsm;

namespace GameLogic.Entity.Monsters.Body
{
	/// <summary>
	/// 死亡（终态）：进入时由宿主做副作用（<see cref="IActorBody.OnDied"/>：关受击盒、播死亡音、广播
	/// MonsterDiedEventArgs），作废出招请求，从头播 death 动画、定身；**death 动画时长**（OnInit 读进 Params，
	/// 状态计时）走完请求回收一次。池复用由 OnShow 重建整台状态机。
	/// </summary>
	public sealed class MonsterDeathState : MonsterBodyState
	{
		private float m_Left; // 死亡动画结束前的剩余时间（秒）。

		private bool m_Recycled; // 是否已经向宿主发送回收请求。

		/// <summary>死亡状态不再响应受击中断。</summary>
		protected override bool HurtInterrupts => false;

		/// <summary>清除待处理请求，执行死亡副作用并播放死亡动画。</summary>
		protected override void Enter(IFsm<IMonsterBody> fsm, IMonsterBody body)
		{
			body.MoveIntent = 0;
			body.TakeAttackRequest();
			body.TakePendingHurt();
			body.OnDied();
			m_Left = body.Params.DeathTime;
			m_Recycled = false;
			body.RestartAnim(MonsterAnims.Death);
		}

		/// <summary>保持尸体定身，死亡计时结束后请求回收一次。</summary>
		protected override void Tick(IFsm<IMonsterBody> fsm, IMonsterBody body, float dt)
		{
			ApplyGravity(body, dt);
			Stand(body);

			if (m_Recycled)
			{
				return;
			}

			if (Elapsed(m_Left))
			{
				m_Recycled = true;
				body.RequestRecycle();
				return;
			}

			m_Left -= dt;
		}
	}
}
