using GameFramework.Fsm;
using Godot;

namespace GameLogic.Entity
{
	/// <summary>
	/// 下落：跳跃动画播完后的空中状态。
	/// 旧项目在上跳动画播完后接 "drop"，这里对应播放 fall（迁移后的 drop）。
	/// 空中输入有操控（旧项目 NorMalMove 在非攻击状态下同样作用于空中）。
	/// </summary>
	public class HeroFallState : HeroFsmStateBase
	{
		protected internal override void OnEnter(IFsm<HeroEntity> fsm)
		{
			base.OnEnter(fsm);

			HeroEntity hero = fsm.Owner;
			// 旧项目：空中 hit_count 归零。走到平台外自然下落也走这里，所以在这里重置。
			hero.ResetCombo();
			hero.HorizontalControl = true;
			hero.PlayAnim(ActorAnim.Fall);
		}

		protected internal override void OnUpdate(IFsm<HeroEntity> fsm, float elapseSeconds, float realElapseSeconds)
		{
			base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);

			if (TryCommonTransitions(fsm))
			{
				return;
			}

			HeroEntity hero = fsm.Owner;

			hero.SetFacing(hero.MoveInput);

			// 落地后交给 Idle 再按输入分发（Walk / Run / 攻击等）
			if (hero.IsOnFloor())
			{
				ChangeState<HeroIdleState>(fsm);
			}
		}
	}
}
