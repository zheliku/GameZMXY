using GameFramework.Fsm;

namespace GameLogic.Entity.Heroes.Body
{
	/// <summary>
	/// 受击硬直：生效时施加击退速度、从头播 hurt 动画、播受击语音；**硬直时长 = hurt 动画长度**
	///（OnInit 读进 Params，状态计时）。期间不受输入控制（保持击退横向速度），硬直前缓冲的按键作废。
	/// 硬直中再受击：重新生效（重置击退、计时，从头重播动画）。结束回 Ground/Air，移动逻辑接管
	///（与旧项目一致：击退只在 hurt 动画期间生效）。
	/// </summary>
	public sealed class HeroHurtState : HeroBodyState
	{
		/// <summary>硬直剩余秒</summary>
		private float m_Left;

		/// <summary>连续受击由本状态自己处理（见 Tick）</summary>
		protected override bool HurtInterrupts => false;

		protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
		{
			body.Input.ClearBuffers();
			ApplyHurt(body);
		}

		protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
		{
			if (body.HasPendingHurt)
			{
				ApplyHurt(body);
			}

			if (Elapsed(m_Left))
			{
				ChangeToLocomotion(fsm, body);
				return;
			}

			m_Left -= dt;
			ApplyGravity(body, dt);
		}

		/// <summary>受击生效：击退速度 + 计时重置 + 从头播 hurt 动画 + 受击音（取击退即消费）。</summary>
		private void ApplyHurt(IHeroBody body)
		{
			body.Velocity = body.TakePendingHurt();
			m_Left = body.Params.HurtTime;
			body.RestartAnim(HeroAnims.Hurt);
			body.PlayHurtSound();
		}
	}
}
