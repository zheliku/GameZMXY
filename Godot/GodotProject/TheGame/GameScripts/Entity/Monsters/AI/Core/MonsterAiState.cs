using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI.States;
using System;

namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 怪物 AI 状态基类（GF.Fsm，每状态一个类；持有者 = <see cref="IMonsterAiAgent"/>）。
	///
	/// 猴子 AI 状态的共同入口：死亡与受控优先于当前状态决策。
	/// <code>
	///   Pause ⇄ Wander                    无目标游荡
	///   Pause/Wander → WalkToTarget       发现目标
	///   WalkToTarget ⇄ StandAndStrike     普攻水平范围
	///   StandAndStrike ⇄ PaceBelowTarget  平台高度与水平范围
	///   交战状态 → Wander                 目标失效或丢失
	///   任意 → CcLocked → Wander/WalkToTarget；任意 → Death（终态）
	/// </code>
	/// 本状态机只写意图（Move / Face / RequestAttack）；动作、受击与动画由身体状态机（Monsters/Body/）决定。
	/// </summary>
	public abstract class MonsterAiState : FsmState<IMonsterAiAgent>
	{
		/// <summary>调试与冒烟观测使用的状态名。</summary>
		public string StateName => GetType().Name.EndsWith("State", StringComparison.Ordinal)
			? GetType().Name[..^"State".Length]
			: GetType().Name;

		protected internal sealed override void OnEnter(IFsm<IMonsterAiAgent> fsm)
		{
			base.OnEnter(fsm);
			Enter(fsm.Owner);
		}

		protected internal sealed override void OnUpdate(IFsm<IMonsterAiAgent> fsm, float elapseSeconds,
			float realElapseSeconds)
		{
			IMonsterAiAgent agent = fsm.Owner;
			MonsterAiState current = fsm.CurrentState as MonsterAiState;
			if (agent.IsDead)
			{
				if (current is not DeathState)
				{
					ChangeState<DeathState>(fsm);
				}

				return;
			}

			if (agent.IsCcLocked && current is not CcLockedState)
			{
				ChangeState<CcLockedState>(fsm);
				return;
			}

			Tick(fsm, agent, elapseSeconds);
		}

		/// <summary>进入本状态（默认停步）。</summary>
		protected virtual void Enter(IMonsterAiAgent agent)
		{
			agent.Move(0);
		}

		/// <summary>概率判定：percent ∈ 0..100（0 必不中，100 必中），随机数取自宿主。</summary>
		protected static bool Chance(IMonsterAiAgent agent, int percent)
		{
			return agent.NextRandom() * 100f < percent;
		}

		/// <summary>未被打断时的本状态决策（elapseSeconds 为逻辑时间）。</summary>
		protected abstract void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds);

	}
}
