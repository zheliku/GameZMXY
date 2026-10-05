using GameFramework.Fsm;

namespace GameLogic.Entity.Monsters.Body
{
	/// <summary>
	/// 收招硬直（AttackConfig.AiRecovery 秒——表数值，与动画无关）：原地待机、不转身、
	/// 不接受出招请求（请求留到硬直结束后由 Move 消费）；结束回 Move。
	/// 受击打断（同旧项目：受击打断出招与硬直）。
	/// </summary>
	public sealed class MonsterRecoveryState : MonsterBodyState
	{
		private float m_Duration; // 切入收招状态前由攻击状态装填的硬直时长。
		private float m_Left; // 当前收招硬直剩余时间（秒）。

		/// <summary>进入前指定硬直时长（由发起切换的状态调用）。</summary>
		public void Arm(float seconds)
		{
			m_Duration = seconds;
		}

		/// <summary>装载本次收招硬直计时并播放待机动画。</summary>
		protected override void Enter(IFsm<IMonsterBody> fsm, IMonsterBody body)
		{
			m_Left = m_Duration;
			body.PlayAnim(MonsterAnims.Idle);
		}

		/// <summary>计时结束后返回移动状态，否则原地站立并施加重力。</summary>
		protected override void Tick(IFsm<IMonsterBody> fsm, IMonsterBody body, float dt)
		{
			if (Elapsed(m_Left))
			{
				ChangeState<MonsterMoveState>(fsm);
				return;
			}

			m_Left -= dt;
			ApplyGravity(body, dt);
			Stand(body);
		}
	}
}
