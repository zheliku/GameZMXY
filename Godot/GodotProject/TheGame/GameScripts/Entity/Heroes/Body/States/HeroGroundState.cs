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
		private float m_IdleTime; // 本次连续静止累计时长（秒）。

		private bool m_Emoting; // 当前是否正在播放待机小动作。

		private float m_EmoteLeft; // 当前待机小动作剩余时长（秒）。

		private float m_NextEmoteDelay = -1f; // 下一次待机小动作所需静止时长；负数表示尚未抽取。

		/// <summary>进入地面状态时重置待机计时。</summary>
		protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
		{
			ResetIdle();
		}

		/// <summary>离开地面状态时清除待机小动作状态。</summary>
		protected override void Leave(IFsm<IHeroBody> fsm, IHeroBody body, bool isShutdown)
		{
			ResetIdle();
		}

		/// <summary>处理地面攻击、跳跃、移动和待机动画。</summary>
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

		private bool UpdateIdle(IHeroBody body, float dt) // 更新连续静止计时，到点时抽取下一次间隔并返回 true。
		{
			// 没有角色专属待机动画时不启动计时。
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

		private void ResetIdle() // 移动、离地或退出状态时清空待机计时和播放标记。
		{
			m_IdleTime = 0f;
			m_Emoting = false;
		}

		private static float RollDelay(IHeroBody body) // 按配置的待机延迟范围取下一次触发时间。
		{
			HeroBodyParams p = body.Params;
			return p.EmoteDelayMin + (p.EmoteDelayMax - p.EmoteDelayMin) * body.NextRandom();
		}
	}
}
