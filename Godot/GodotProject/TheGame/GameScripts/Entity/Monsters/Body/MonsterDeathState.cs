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
		/// <summary>回收倒计时剩余秒</summary>
		private float m_Left;

		/// <summary>已请求回收（只请求一次）</summary>
		private bool m_Recycled;

		protected override bool HurtInterrupts => false;

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
