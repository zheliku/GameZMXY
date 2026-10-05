using GameFramework.Fsm;

namespace GameLogic.Entity.Monsters.Body
{
	/// <summary>
	/// 受击硬直：生效时施加击退速度、从头播 hurt 动画、播受击语音；**硬直时长 = hurt 动画长度**
	///（OnInit 读进 Params，状态计时）。期间保持击退速度、作废 AI 出招请求。
	/// 硬直中再受击：重新生效（重置击退、计时，从头重播动画）。结束回 Move。
	/// </summary>
	public sealed class MonsterHurtState : MonsterBodyState
	{
		private float m_Left; // 受击硬直剩余时间（秒）。

		/// <summary>连续受击由本状态自己处理（见 Tick），不走基类打断</summary>
		protected override bool HurtInterrupts => false;

		/// <summary>进入受击状态时清除移动意图并应用击退。</summary>
		protected override void Enter(IFsm<IMonsterBody> fsm, IMonsterBody body)
		{
			body.MoveIntent = 0;
			ApplyHurt(body);
		}

		/// <summary>处理连续受击、受击时长和重力。</summary>
		protected override void Tick(IFsm<IMonsterBody> fsm, IMonsterBody body, float dt)
		{
			body.TakeAttackRequest();
			if (body.HasPendingHurt)
			{
				ApplyHurt(body);
			}

			if (Elapsed(m_Left))
			{
				ChangeState<MonsterMoveState>(fsm);
				return;
			}

			m_Left -= dt;
			ApplyGravity(body, dt);
		}

		private void ApplyHurt(IMonsterBody body) // 消费击退请求并重新开始受击硬直。
		{
			// 连续受击覆盖当前击退和时钟，保持同一受击状态。
			body.Velocity = body.TakePendingHurt();
			m_Left = body.Params.HurtTime;
			body.RestartAnim(MonsterAnims.Hurt);
			body.PlayHurtSound();
		}
	}
}
