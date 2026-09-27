using GameFramework.Fsm;
using Godot;

namespace GameLogic.Entity
{
	/// <summary>
	/// 待机：常态播 idle1（循环），隔一段随机时间播一次憨笑 idle2，播完回 idle1；
	/// 间隔取自 HeroConfig.IdleEmoteDelay（X=最短，Y=最长）。
	/// </summary>
	public class HeroIdleState : HeroFsmStateBase
	{
		/// <summary>下一次播憨笑的绝对时刻（秒）</summary>
		private double m_EmoteAt;

		/// <summary>当前是否正在播憨笑（决定等它播完再回 idle1）</summary>
		private bool m_EmotePlaying;

		protected internal override void OnEnter(IFsm<HeroEntity> fsm)
		{
			base.OnEnter(fsm);

			HeroEntity hero = fsm.Owner;
			hero.HorizontalControl = true;
			hero.PlayAnim(ActorAnim.Idle);

			m_EmotePlaying = false;
			ScheduleEmote(hero);
		}

		protected internal override void OnUpdate(IFsm<HeroEntity> fsm, float elapseSeconds, float realElapseSeconds)
		{
			base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);

			if (TryCommonTransitions(fsm))
			{
				return;
			}

			HeroEntity hero = fsm.Owner;

			if (!hero.IsOnFloor())
			{
				ChangeState<HeroFallState>(fsm);
				return;
			}

			if (hero.MoveInput != 0)
			{
				ChangeState<HeroWalkState>(fsm);
				return;
			}

			UpdateEmote(hero);
		}

		/// <summary>憨笑：到点播一次 idle2，播完回 idle1 并重新排期。</summary>
		private void UpdateEmote(HeroEntity hero)
		{
			if (!m_EmotePlaying)
			{
				if (Time.GetTicksMsec() / 1000.0 >= m_EmoteAt)
				{
					m_EmotePlaying = true;
					hero.PlayAnim(ActorAnim.IdleEmote);
				}

				return;
			}

			if (hero.IsAnimFinished())
			{
				m_EmotePlaying = false;
				hero.PlayAnim(ActorAnim.Idle);
				ScheduleEmote(hero);
			}
		}

		private void ScheduleEmote(HeroEntity hero)
		{
			m_EmoteAt = Time.GetTicksMsec() / 1000.0
				+ GD.RandRange(hero.Config.IdleEmoteDelay.X, hero.Config.IdleEmoteDelay.Y);
		}
	}
}
