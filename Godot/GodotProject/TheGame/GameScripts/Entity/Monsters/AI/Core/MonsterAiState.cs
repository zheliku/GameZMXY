using GameFramework.Fsm;

namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 怪物 AI 状态基类（GF.Fsm，每状态一个类；持有者 = <see cref="IMonsterAiAgent"/>）。
	///
	/// **状态 = 行为，角色 = 组装时指定**：状态类只描述"做什么"（WanderState 游荡、StandAndStrikeState 站定出招……），
	/// 不写死自己占哪个角色；由大脑原型 <c>set.Bind(MonsterAiRole.Patrol, new WanderState())</c> 放进槽位。
	/// 行为按角色跳转（<see cref="ChangeRole"/>），所以同一个行为可以被任何怪、任何原型复用，换掉某个角色的行为
	/// 其余状态不用改。额外状态（<see cref="MonsterAiStateSet.AddExtra"/>）不占槽位，<see cref="Role"/> 为 null。
	///
	/// 打断优先级集中在这里：死亡 &gt; 受控 &gt; 本状态决策。
	/// <code>
	///   Idle ⇄ Patrol              游荡（PatrolInterval 一次决策）
	///   Idle/Patrol → Chase        有目标
	///   Chase ⇄ Attack             水平够得着 / 离开超过 AttackRangeSlack（出招与收招硬直中不离开）
	///   Attack ⇄ Hold              水平到位但高度够不着 / 目标落回判定高度
	///   Chase/Attack/Hold → Patrol 目标失效或丢失（实体按 LoseTargetTime 判定）
	///   任意 → CcLocked → Chase/Patrol ；任意 → Death（终态）
	/// </code>
	/// 本状态机只写意图（Move / Face / RequestAttack），动画由 AnimationTree 读实体事实决定。
	/// </summary>
	public abstract class MonsterAiState : FsmState<IMonsterAiAgent>
	{
		/// <summary>组装时放入的角色槽（额外状态为 null）</summary>
		public MonsterAiRole? Role { get; private set; }

		/// <summary>状态名（调试/冒烟观测）：占槽的状态报角色名，额外状态报类名。</summary>
		public virtual string StateName => Role?.ToString() ?? GetType().Name;

		/// <summary>本状态能否被受控打断（默认可以；Boss 演出等覆写为 false）。死亡打断不受此控制。</summary>
		protected virtual bool CanBeCcLocked => true;

		/// <summary>由状态集在 Bind / AddExtra 时写入。</summary>
		internal void AssignRole(MonsterAiRole? role)
		{
			Role = role;
		}

		protected internal sealed override void OnEnter(IFsm<IMonsterAiAgent> fsm)
		{
			base.OnEnter(fsm);
			Enter(fsm.Owner);
		}

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

		/// <summary>按角色跳转（目标行为由状态集解析）。</summary>
		protected void ChangeRole(IFsm<IMonsterAiAgent> fsm, MonsterAiRole role)
		{
			ChangeState(fsm, fsm.Owner.States.Resolve(role));
		}
	}
}
