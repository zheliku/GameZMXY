using GameFramework.Fsm;
using Godot;

namespace GameLogic.Entity
{
	/// <summary>
	/// 慢走：按住方向键的默认移动档（HeroConfig.WalkSpeed）。
	/// 双击方向键（RunDoubleTapWindow 内）进入 HeroRunState；松手回 Idle。
	/// </summary>
	public class HeroWalkState : HeroFsmStateBase
	{
		protected internal override void OnEnter(IFsm<HeroEntity> fsm)
		{
			base.OnEnter(fsm);

			HeroEntity hero = fsm.Owner;
			hero.HorizontalControl = true;
			hero.PlayAnim(ActorAnim.Walk);
			hero.SetFacing(hero.MoveInput);
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

			if (hero.MoveInput == 0)
			{
				ChangeState<HeroIdleState>(fsm);
				return;
			}

			if (hero.IsRunning)
			{
				ChangeState<HeroRunState>(fsm);
				return;
			}

			hero.SetFacing(hero.MoveInput);
		}
	}
}
