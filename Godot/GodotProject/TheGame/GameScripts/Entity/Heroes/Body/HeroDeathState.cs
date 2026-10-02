using GameFramework.Fsm;
using Godot;

namespace GameLogic.Entity.Heroes.Body
{
	/// <summary>
	/// 死亡（终态）：从头播死亡动画（死亡语音在进入时经 <see cref="IActorBody.OnDied"/> 播），定身、只受重力。
	/// 复活 / 池复用由实体 OnShow 重建整台状态机完成，不需要"死亡 → 地面"的回拉。
	/// </summary>
	public sealed class HeroDeathState : HeroBodyState
	{
		protected override bool HurtInterrupts => false;

		protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
		{
			body.OnDied();
			body.RestartAnim(HeroAnims.Death);
		}

		protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
		{
			ApplyGravity(body, dt);
			body.Velocity = new Vector2(0f, body.Velocity.Y);
		}
	}
}
