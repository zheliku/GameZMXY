using GameFramework.Fsm;

namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 怪物 AI 状态基类（GF.Fsm，每状态一个类；持有者 = <see cref="IMonsterAiAgent"/>）。
	///
	/// **打断规则集中在这里**（不散落到各状态）：优先级 死亡 &gt; 受控 &gt; 各状态自己的决策。
	/// 每帧先判打断，未被打断才调用子类 <see cref="Tick"/>。
	///
	/// **跳转按角色**（<see cref="ChangeRole"/>）：目标类型由状态集解析，子类怪替换某角色的实现后，
	/// 其它状态跳过去时自动落到新实现。额外状态（Boss 阶段等）可直接按类型 ChangeState。
	///
	/// 标准状态图（边 = 触发条件）：
	/// <code>
	///   Patrol ⇄ Idle            巡逻决策（PatrolInterval 一次；PatrolIdleChance 概率停留）
	///   Patrol/Idle → Chase      有目标
	///   Chase → Attack           普攻判定盒水平上够得着目标；追击途中技能就绪即放
	///   Attack → Chase           水平间隙超过 AttackRangeSlack（出招/收招硬直中不离开）
	///   Chase/Attack → Patrol    目标失效（出招中不离开）
	///   任意 → CcLocked           受控；解除后再僵直 CalmTime → Chase / Patrol
	///   任意 → Death              死亡（终态，实体回收时状态机随之销毁）
	/// </code>
	/// 与动画的分工：本状态机只写意图（Move / Face / RequestAttack），动画由 AnimationTree 读实体事实决定。
	/// </summary>
	public abstract class MonsterAiState : FsmState<IMonsterAiAgent>
	{
		/// <summary>本状态占用的角色（替换实现时声明同一角色）</summary>
		public abstract MonsterAiRole Role { get; }

		/// <summary>状态名（调试/冒烟观测；默认 = 角色名，额外状态可覆写）</summary>
		public virtual string StateName => Role.ToString();

		protected internal sealed override void OnUpdate(IFsm<IMonsterAiAgent> fsm, float elapseSeconds,
			float realElapseSeconds)
		{
			IMonsterAiAgent agent = fsm.Owner;
			if (agent.IsDead)
			{
				if (Role != MonsterAiRole.Death)
				{
					ChangeRole(fsm, MonsterAiRole.Death);
				}

				return;
			}

			if (agent.IsCcLocked && Role != MonsterAiRole.CcLocked && CanBeCcLocked)
			{
				ChangeRole(fsm, MonsterAiRole.CcLocked);
				return;
			}

			Tick(fsm, agent, elapseSeconds);
		}

		/// <summary>
		/// 本状态能否被受控打断（默认可以）。Boss 阶段转换等不可打断的演出状态覆写为 false；
		/// 死亡打断不受此控制。
		/// </summary>
		protected virtual bool CanBeCcLocked => true;

		/// <summary>未被打断时的本状态决策（elapseSeconds 为逻辑时间）。</summary>
		protected abstract void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds);

		/// <summary>按角色跳转（目标实现由状态集解析）。</summary>
		protected void ChangeRole(IFsm<IMonsterAiAgent> fsm, MonsterAiRole role)
		{
			ChangeState(fsm, fsm.Owner.States.Resolve(role));
		}
	}
}
