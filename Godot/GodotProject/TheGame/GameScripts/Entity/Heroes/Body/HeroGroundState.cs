using GameFramework.Fsm;

namespace GameLogic.Entity.Heroes.Body
{
	/// <summary>
	/// 地面：走 / 跑 / 待机（含角色专属待机小动作）。可起手普攻、起跳；离地（走出平台边缘）进 Air。
	///
	/// 待机小动作：站定无输入**连续**静止满一次随机间隔（IdleEmoteDelay 区间）才播放，
	/// **播放时长 = 小动作动画长度**（OnInit 读进 Params，状态计时）；播完回待机、同一次静止里重新累计；
	/// 走动 / 离开地面即中断、静止计时归零重计。
	/// </summary>
	public sealed class HeroGroundState : HeroBodyState
	{
		/// <summary>本次静止已累计秒</summary>
		private float m_IdleTime;

		/// <summary>正在播待机小动作</summary>
		private bool m_Emoting;

		/// <summary>小动作剩余秒</summary>
		private float m_EmoteLeft;

		/// <summary>下一次小动作需要的连续静止秒（&lt;0 = 还没掷）</summary>
		private float m_NextEmoteDelay = -1f;

		protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
		{
			ResetIdle();
		}

		protected override void Leave(IFsm<IHeroBody> fsm, IHeroBody body, bool isShutdown)
		{
			ResetIdle();
		}

		protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
		{
			if (TryAttack(fsm, body))
			{
				return;
			}

			if (TryJump(body) || IsAirborne(body))
			{
				ChangeState<HeroAirState>(fsm);
				return;
			}

			ApplyGravity(body, dt);
			Steer(body);

			if (body.Input.MoveAxis != 0)
			{
				ResetIdle();
				body.PlayAnim(body.Input.Running ? HeroAnims.Run : HeroAnims.Walk);
				return;
			}

			if (m_Emoting)
			{
				if (Elapsed(m_EmoteLeft))
				{
					m_Emoting = false;
					body.PlayAnim(HeroAnims.Idle);
					return;
				}

				m_EmoteLeft -= dt;
				body.PlayAnim(body.Params.EmoteAnim);
				return;
			}

			if (UpdateIdle(body, dt))
			{
				m_Emoting = true;
				m_EmoteLeft = body.Params.EmoteTime;
				body.PlayAnim(body.Params.EmoteAnim);
				return;
			}

			body.PlayAnim(HeroAnims.Idle);
		}

		/// <summary>推进待机静止计时，返回本帧是否该起播小动作。</summary>
		private bool UpdateIdle(IHeroBody body, float dt)
		{
			if (string.IsNullOrEmpty(body.Params.EmoteAnim))
			{
				return false;
			}

			if (m_NextEmoteDelay < 0f)
			{
				m_NextEmoteDelay = RollDelay(body);
			}

			m_IdleTime += dt;
			if (m_IdleTime < m_NextEmoteDelay)
			{
				return false;
			}

			m_IdleTime = 0f;
			m_NextEmoteDelay = RollDelay(body);
			return true;
		}

		private void ResetIdle()
		{
			m_IdleTime = 0f;
			m_Emoting = false;
		}

		private static float RollDelay(IHeroBody body)
		{
			HeroBodyParams p = body.Params;
			return p.EmoteDelayMin + (p.EmoteDelayMax - p.EmoteDelayMin) * body.NextRandom();
		}
	}
}
