using GameFramework.Fsm;

namespace GameLogic.Entity.Monsters.Body
{
	/// <summary>
	/// 出招：进入时提交（宿主 BeginAttack：转向目标、计冷却、装填攻击包、播起手音）、从第 0 帧播本招动画；
	/// 定身、朝向锁定。**招式时长 = 招式动画长度**（OnInit 读进 Params，状态计时）：
	/// 到点后本招 AiRecovery &gt; 0 进 Recovery 收招硬直，否则回 Move。
	/// 受击打断（霸体由宿主不登记受击）；离开时归还攻击包。
	/// </summary>
	public sealed class MonsterAttackState : MonsterBodyState
	{
		private int m_Index;

		/// <summary>本招剩余秒</summary>
		private float m_Left;

		/// <summary>进入前指定要出的招（由发起切换的状态调用）。</summary>
		public void Arm(int index)
		{
			m_Index = index;
		}

		protected override void Enter(IFsm<IMonsterBody> fsm, IMonsterBody body)
		{
			body.MoveIntent = 0;
			body.BeginAttack(m_Index);
			m_Left = body.Params.AttackTimes[m_Index];
			body.RestartAnim(body.Params.AttackAnims[m_Index]);
		}

		protected override void Leave(IFsm<IMonsterBody> fsm, IMonsterBody body, bool isShutdown)
		{
			// 收招、受击打断、死亡、实体隐藏都走这里：动画被切走，攻击包在此归还
			body.MoveIntent = 0;
			body.EndAttack();
		}

		protected override void Tick(IFsm<IMonsterBody> fsm, IMonsterBody body, float dt)
		{
			ApplyGravity(body, dt);
			Stand(body);

			if (Elapsed(m_Left))
			{
				float recovery = body.Params.AttackRecovery[m_Index];
				if (recovery > 0f)
				{
					fsm.GetState<MonsterRecoveryState>().Arm(recovery);
					ChangeState<MonsterRecoveryState>(fsm);
					return;
				}

				ChangeState<MonsterMoveState>(fsm);
				return;
			}

			m_Left -= dt;
		}
	}
}
