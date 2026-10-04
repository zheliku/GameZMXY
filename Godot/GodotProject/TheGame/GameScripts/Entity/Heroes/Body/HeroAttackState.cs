using GameFramework.Fsm;
using Godot;

namespace GameLogic.Entity.Heroes.Body
{
	/// <summary>
	/// 普攻连段。**段时长 = 段动画长度**（OnInit 读动画资源进 Params，状态用物理 dt 计时）：
	///  * 进入（状态 Enter）即提交出招（宿主 BeginAttack：装填攻击包、播起手音）并从第 0 帧播本段动画；
	///  * 段时长到点：出招中按过普攻（连击缓冲）且起手站在地面、还有下一段 → 接下一段；否则收招回 Ground/Air。
	/// 连段序号每执行一段 +1（末段回 0），跨按键保持——连段始终 1→2→3→4→1 循环（同旧 hit_count）。
	///
	/// 出招中：地面定身、空中保留动量（旧 is_can_move_attack_in_sky），朝向锁定；跳跃不打断出招；
	/// **受击挂起**到收招后才生效（有意的手感取舍：连段不被单次受击清空）；死亡立即打断。
	/// </summary>
	public sealed class HeroAttackState : HeroBodyState
	{
		/// <summary>正在播的段</summary>
		private int m_Segment;

		/// <summary>本套连段起手时站在地面（可推进连段的前提）</summary>
		private bool m_Chainable;

		/// <summary>出招中按过普攻（段结束时消费）</summary>
		private bool m_Chain;

		/// <summary>本段剩余秒</summary>
		private float m_Left;

		/// <summary>攻击段期间挂起受击，待收招后再处理。</summary>
		protected override bool HurtInterrupts => false;

		/// <summary>记录起手是否在地面，并开始当前连段。</summary>
		protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
		{
			m_Chainable = body.OnFloor;
			StartSegment(body, Mathf.Clamp(body.ComboIndex, 0, body.Params.ComboLength - 1));
		}

		/// <summary>离开攻击状态时归还攻击包。</summary>
		protected override void Leave(IFsm<IHeroBody> fsm, IHeroBody body, bool isShutdown)
		{
			// 收招、死亡打断、实体隐藏都走这里：动画被切走，攻击包在此归还
			body.EndAttack();
		}

		/// <summary>推进攻击段计时并消费连段输入。</summary>
		protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
		{
			if (body.Input.ConsumeAttack())
			{
				m_Chain = true;
			}

			ApplyGravity(body, dt);
			if (body.OnFloor)
			{
				body.Velocity = new Vector2(0f, body.Velocity.Y);
			}

			if (Elapsed(m_Left))
			{
				FinishSegment(fsm, body);
				return;
			}

			m_Left -= dt;
		}

		/// <summary>本段时长到点：推进连段或收招。</summary>
		private void FinishSegment(IFsm<IHeroBody> fsm, IHeroBody body)
		{
			int combo = body.Params.ComboLength;
			int finished = m_Segment;
			body.ComboIndex = (finished + 1) % combo;
			if (m_Chain && m_Chainable && finished < combo - 1)
			{
				StartSegment(body, finished + 1);
				return;
			}

			ChangeToLocomotion(fsm, body);
		}

		/// <summary>装填指定攻击段并从头播放其动画。</summary>
		private void StartSegment(IHeroBody body, int segment)
		{
			m_Segment = segment;
			m_Chain = false;
			m_Left = body.Params.AttackTimes[segment];
			body.BeginAttack(segment);
			body.RestartAnim(body.Params.AttackAnims[segment]);
		}
	}
}
