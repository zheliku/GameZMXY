using GameFramework.Fsm;
using Godot;

namespace GameLogic.Entity
{
	/// <summary>
	/// 跳跃（上升段）。
	///
	/// **进入本状态就执行一次起跳**：OnEnter 调 TryJump() 并按 JumpCount 播 jump / jump_2。
	/// 所以 Idle/Walk/Run/Fall 只负责"切到这个状态"，不自己调 TryJump——否则会跳两次。
	/// 上升途中再按跳跃键 = 原地二段跳（TryJump 内部按 JumpCount 把关），不切状态。
	///
	/// 动画播完即转 Fall，对应旧项目 "await jump1.animation_finished 后 play(drop)"。
	/// </summary>
	public class HeroJumpState : HeroFsmStateBase
	{
		protected internal override void OnEnter(IFsm<HeroEntity> fsm)
		{
			base.OnEnter(fsm);

			HeroEntity hero = fsm.Owner;
			hero.HorizontalControl = true;
			DoJump(hero);
			hero.SetFacing(hero.MoveInput);
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

			if (hero.ConsumeHurt())
			{
				ChangeState<HeroHurtState>(fsm);
				return;
			}

			// 空中二段跳：已经在跳跃状态里，直接再跳一次即可（次数由 TryJump 把关）
			if (Input.IsActionJustPressed(HeroEntity.ActionJump))
			{
				DoJump(hero);
			}

			// 空中允许出招（沿用旧项目 is_can_move_attack_in_sky 的空中操控）
			if (Input.IsActionJustPressed(HeroEntity.ActionAttack))
			{
				ChangeState<HeroAttackState>(fsm);
				return;
			}

			hero.SetFacing(hero.MoveInput);

			if (hero.IsAnimFinished())
			{
				ChangeState<HeroFallState>(fsm);
			}
		}

		/// <summary>起跳并播放对应段数的动画（第 1 段 jump，第 2 段 jump_2）。</summary>
		private void DoJump(HeroEntity hero)
		{
			if (hero.TryJump())
			{
				hero.PlayAnim(hero.JumpCount == 1 ? ActorAnim.Jump : ActorAnim.Jump2);
			}
		}
	}
}
