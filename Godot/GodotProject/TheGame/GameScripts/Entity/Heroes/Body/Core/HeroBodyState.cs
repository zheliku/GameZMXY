using GameFramework.Fsm;
using GameLogic.Entity.Body;

namespace GameLogic.Entity.Heroes.Body
{
	/// <summary>
	/// 英雄身体状态基类：集中**打断优先级**（死亡 &gt; 受击 &gt; 本状态决策）与共用的移动/跳跃/起手规则。
	/// <code>
	///   Ground ⇄ Air                离地 / 落地（含起跳）
	///   Ground/Air → Attack         普攻请求（空中起手的连段不推进）
	///   Attack → Attack(下一段)     段时长内按过普攻且起手在地面
	///   Attack → Ground/Air         段时长结束（= 段动画长度）
	///   任意 → Hurt → Ground/Air    受击（Attack 中挂起，收招后生效：连段不被单次受击清空）
	///   任意 → Death                死亡（终态；实体回收后整台状态机重建）
	/// </code>
	/// </summary>
	public abstract class HeroBodyState : BodyState<IHeroBody>
	{
		private const int SecondJumpCount = 2; // 双跳触发所需的跳跃计数。

		/// <summary>英雄身体状态类名使用的统一前缀。</summary>
		protected override string NamePrefix => "Hero";

		/// <summary>受击是否立即打断本状态（Attack 挂起到收招；Hurt 自己处理连续受击；Death 不再受击）。</summary>
		protected virtual bool HurtInterrupts => true;

		/// <summary>集中处理死亡与可打断受击的状态转换。</summary>
		protected sealed override bool Interrupt(IFsm<IHeroBody> fsm, IHeroBody body)
		{
			if (body.Dead)
			{
				if (this is HeroDeathState)
				{
					return false;
				}

				ChangeState<HeroDeathState>(fsm);
				return true;
			}

			if (HurtInterrupts && body.HasPendingHurt)
			{
				ChangeState<HeroHurtState>(fsm);
				return true;
			}

			return false;
		}

		/// <summary>在空中：离地，或已起跳且仍在上升（含起跳那一帧还没离地）。</summary>
		protected static bool IsAirborne(IHeroBody body)
		{
			return !body.OnFloor || (body.JumpCount > 0 && body.Velocity.Y < 0f);
		}

		/// <summary>回到地面/空中（按当前是否在空中选）。</summary>
		protected void ChangeToLocomotion(IFsm<IHeroBody> fsm, IHeroBody body)
		{
			if (IsAirborne(body))
			{
				ChangeState<HeroAirState>(fsm);
			}
			else
			{
				ChangeState<HeroGroundState>(fsm);
			}
		}

		/// <summary>普攻起手：有连段且有普攻请求 → 进 Attack。</summary>
		protected bool TryAttack(IFsm<IHeroBody> fsm, IHeroBody body)
		{
			if (body.Params.ComboLength == 0 || !body.Input.ConsumeAttack())
			{
				return false;
			}

			ChangeState<HeroAttackState>(fsm);
			return true;
		}

		/// <summary>起跳：还有跳跃次数且有跳跃请求 → 次数 +1、连段归零、给向上速度（不切状态，由调用方决定）。</summary>
		protected static bool TryJump(IHeroBody body)
		{
			if (body.JumpCount >= body.Params.JumpCountMax || !body.Input.ConsumeJump())
			{
				return false;
			}

			body.JumpCount++;
			body.ComboIndex = 0;
			body.Velocity = new Godot.Vector2(body.Velocity.X, -body.Params.JumpSpeed);
			return true;
		}

		/// <summary>按输入走/跑并随之转向（地面与空中同一套横向控制）。</summary>
		protected static void Steer(IHeroBody body)
		{
			int axis = body.Input.MoveAxis;
			float speed = body.Input.Running ? body.Params.RunSpeed : body.Params.WalkSpeed;
			body.Velocity = new Godot.Vector2(axis * speed, body.Velocity.Y);
			body.SetFacing(axis);
		}

		/// <summary>空中动画：上升按跳跃次数选 jump / jump_2，下落播 fall。</summary>
		protected static string AirAnim(IHeroBody body)
		{
			if (body.Velocity.Y >= 0f)
			{
				return HeroAnims.Fall;
			}

			return body.JumpCount >= SecondJumpCount ? HeroAnims.Jump2 : HeroAnims.Jump;
		}
	}
}
