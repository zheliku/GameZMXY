using GameConfig.Sound;
using GameFramework.Fsm;
using Godot;
using GodotGameFramework;

namespace GameLogic.Entity
{
	/// <summary>
	/// 攻击（连段）。规则只有三条：
	///  1. 本段时长 = max(动画时长, AttackConfig.Interval)——Interval 是配置表里直接给的秒数，
	///     就是"普攻之间的间隔"，改表即可调手感；
	///  2. **只有站在地面起手的普攻才推进连段**（末段回 0）；空中起手的普攻不推进，收招时把连段归零。
	///     旧项目 role1 的 `if not is_on_floor(): hit_count = 0`——空中永远只出第一段。
	///     判据取"起手那一帧是否在地面"（m_Chainable），不取收招帧，避免落地时机不同导致连段泄漏到地面；
	///  3. 播放期间的按键不处理，逐次点按出段。
	///
	/// 移动：站地面出招定身（每帧刷新，空中出招落地立即定住）、空中出招保留动量
	/// （旧项目 BaseHero.gd:505 is_can_move_attack_in_sky 的等价实现）。
	/// </summary>
	public class HeroAttackState : FsmState<HeroEntity>
	{
		/// <summary>本段开始时刻（秒）</summary>
		private double m_EnterTime;

		/// <summary>
		/// 本段是否推进连段：起手站在地面才为真。
		/// 空中起手（含起手在空中、落地后才收招）一律不推进，收招时归零——
		/// 否则空中那一下会把 ComboIndex 带成 1，落地后再按就是第二段。
		/// </summary>
		private bool m_Chainable;

		protected internal override void OnEnter(IFsm<HeroEntity> fsm)
		{
			base.OnEnter(fsm);

			HeroEntity hero = fsm.Owner;
			hero.HorizontalControl = false;   // 出招期间不被输入改写横向速度，空中动量得以保留
			hero.PlayAnim(hero.CurrentAttack?.Animation ?? ActorAnim.Idle);
			hero.PlaySound(hero.CurrentAttack?.SoundId ?? SoundId.None);   // 旧项目在动画 t=0 的 method 轨道调 add_music
			StopIfOnFloor(hero);

			m_Chainable = hero.IsOnFloor();
			m_EnterTime = Time.GetTicksMsec() / 1000.0;
			Log.Debug("[Hero] 普攻第 {0} 段 开始（停留 {1:F2}s）",
				hero.ComboIndex + 1, hero.CurrentAttack?.Interval ?? 0f);
		}

		protected internal override void OnUpdate(IFsm<HeroEntity> fsm, float elapseSeconds, float realElapseSeconds)
		{
			base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);

			HeroEntity hero = fsm.Owner;

			if (hero.IsDead)
			{
				ChangeState<HeroDeathState>(fsm);
				return;
			}

			StopIfOnFloor(hero);

			float elapsed = (float)(Time.GetTicksMsec() / 1000.0 - m_EnterTime);
			if (!hero.IsAnimFinished() || elapsed < (hero.CurrentAttack?.Interval ?? 0f))
			{
				return;
			}

			if (m_Chainable)
			{
				hero.AdvanceCombo();
			}
			else
			{
				hero.ResetCombo();
			}

			ChangeState<HeroIdleState>(fsm);
		}

		/// <summary>站地面出招定身。每帧调用：空中出招落地那一刻也会立即定住。</summary>
		private static void StopIfOnFloor(HeroEntity hero)
		{
			if (hero.IsOnFloor())
			{
				hero.Velocity = new Vector2(0, hero.Velocity.Y);
			}
		}
	}
}
