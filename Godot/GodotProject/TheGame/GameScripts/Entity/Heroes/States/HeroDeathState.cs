using GameConfig.Sound;
using GameFramework.Fsm;
using Godot;

namespace GameLogic.Entity
{
	/// <summary>
	/// 死亡：终态（横向定身）。
	/// M4 接入后在此触发隐藏/掉落；目前只播动画，便于死亡流程先跑通。
	/// </summary>
	public class HeroDeathState : HeroFsmStateBase
	{
		protected internal override void OnEnter(IFsm<HeroEntity> fsm)
		{
			base.OnEnter(fsm);

			HeroEntity hero = fsm.Owner;
			hero.HorizontalControl = false;
			hero.Velocity = new Vector2(0, hero.Velocity.Y);
			hero.PlayAnim(ActorAnim.Death);
			hero.PlaySound(hero.Config.DeathSoundId);   // 死亡语音（旧 death 动画的 add_music(7) → 59_Role1_dead）
		}
	}
}
