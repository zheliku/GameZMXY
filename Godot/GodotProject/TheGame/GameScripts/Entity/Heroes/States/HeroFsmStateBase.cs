using GameFramework.Fsm;
using Godot;
using GodotGameFramework;

namespace GameLogic.Entity
{
	/// <summary>
	/// 英雄状态公共基类。
	///
	///  1. 状态切换 Debug 日志：所有状态进入时统一在此打一行，实体不再每帧轮询状态名；
	///  2. 公共打断转移 TryCommonTransitions()（AGENTS 5.2：打断规则集中一处）：
	///     死亡 → HeroDeathState（最高优先）→ 受击 → HeroHurtState；
	///     攻击键 → HeroAttackState；跳跃键（且还有次数）→ HeroJumpState。
	///     各状态专属转移（落地、进跑、连段收招等）写在各自 OnUpdate 里，返回 false 后继续。
	///
	/// 接入情况：全部 8 个状态都继承本基类（统一日志）；
	/// 其中 HeroAttackState / HeroJumpState / HeroHurtState / HeroDeathState **不调用** TryCommonTransitions：
	///  * AttackState —— 出招期间受击/跳跃不打断，攻击键语义是"等待收招"，转移规则不同；
	///  * JumpState —— 空中再按跳跃是"原地二段跳"（TryJump 不切状态），不能走"切状态"的公共逻辑；
	///  * Hurt / Death —— 打断的唯一去向就是自身或死亡，无公共转移可言。
	/// </summary>
	public abstract class HeroFsmStateBase : FsmState<HeroEntity>
	{
		protected internal override void OnEnter(IFsm<HeroEntity> fsm)
		{
			base.OnEnter(fsm);
			Log.Debug("[Hero] {0} -> {1}", fsm.Owner.Name, GetType().Name);
		}

		/// <summary>
		/// 尝试公共打断转移。发生切换返回 true，调用方应立即 return。
		/// </summary>
		protected bool TryCommonTransitions(IFsm<HeroEntity> fsm)
		{
			HeroEntity hero = fsm.Owner;

			if (hero.IsDead)
			{
				ChangeState<HeroDeathState>(fsm);
				return true;
			}

			if (hero.ConsumeHurt())
			{
				ChangeState<HeroHurtState>(fsm);
				return true;
			}

			if (Input.IsActionJustPressed(HeroEntity.ActionAttack))
			{
				ChangeState<HeroAttackState>(fsm);
				return true;
			}

			if (Input.IsActionJustPressed(HeroEntity.ActionJump) && hero.CanJump)
			{
				ChangeState<HeroJumpState>(fsm);
				return true;
			}

			return false;
		}
	}
}
