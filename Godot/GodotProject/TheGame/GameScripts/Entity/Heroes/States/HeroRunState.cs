using GameFramework.Fsm;
using Godot;

namespace GameLogic.Entity
{
	/// <summary>
	/// 跑步（快走）：双击方向键后按住进入，速度取 HeroConfig.RunSpeed，播 run 动画。
	/// 跑步档丢失（松手后再单点）回 HeroWalkState，松手回 Idle。
	/// </summary>
	public class HeroRunState : HeroFsmStateBase
	{
		protected internal override void OnEnter(IFsm<HeroEntity> fsm)
		{
			base.OnEnter(fsm);

			HeroEntity hero = fsm.Owner;
			hero.HorizontalControl = true;
			hero.PlayAnim(ActorAnim.Run);
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

			if (!hero.IsRunning)
			{
				ChangeState<HeroWalkState>(fsm);
				return;
			}

			hero.SetFacing(hero.MoveInput);
		}
	}
}
