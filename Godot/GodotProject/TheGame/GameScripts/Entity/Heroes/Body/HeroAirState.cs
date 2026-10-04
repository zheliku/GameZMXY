using GameFramework.Fsm;

namespace GameLogic.Entity.Heroes.Body
{
	/// <summary>
	/// 空中：起跳上升 / 二段跳 / 下落。空中保留横向控制、可二段跳、可起手普攻（空中连段从第一段起、不推进）；
	/// 落地进 Ground。空中连段序号归零（旧 BaseHero.gd:270 role1 在空中时 hit_count = 0）。
	/// jump/jump_2 播完停在末帧（不循环），Air 不计时——下落姿势由纵向速度切换。
	/// </summary>
	public sealed class HeroAirState : HeroBodyState
	{
		/// <summary>处理空中攻击、二段跳、转向与跳跃/下落动画。</summary>
		protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
		{
			if (!body.OnFloor)
			{
				body.ComboIndex = 0;
			}

			if (TryAttack(fsm, body))
			{
				return;
			}

			TryJump(body);
			if (!IsAirborne(body))
			{
				ChangeState<HeroGroundState>(fsm);
				return;
			}

			ApplyGravity(body, dt);
			Steer(body);
			body.PlayAnim(AirAnim(body));
		}
	}
}
