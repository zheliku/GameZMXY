using GameFramework.Fsm;
using Godot;

namespace GameLogic.Entity
{
	/// <summary>受击：播放受击动画期间失去控制（横向定身）。</summary>
	public class HeroHurtState : HeroFsmStateBase
	{
		protected internal override void OnEnter(IFsm<HeroEntity> fsm)
		{
			base.OnEnter(fsm);

			HeroEntity hero = fsm.Owner;
			hero.HorizontalControl = false;
			hero.Velocity = new Vector2(0, hero.Velocity.Y);
			hero.PlayAnim(ActorAnim.Hurt);
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

			if (hero.IsAnimFinished())
			{
				ChangeState<HeroIdleState>(fsm);
			}
		}
	}
}
