using System;
using System.Collections.Generic;

namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 一台怪物 AI 状态机的组装表：**角色槽 → 行为实例** + 额外状态。标准组合见 <see cref="MonsterBrains"/>。
	///  * <see cref="Bind"/>：把一个行为放进角色槽（替换原绑定）；其余状态按角色跳转，自动落到新行为；
	///  * <see cref="AddExtra"/>：不占槽的额外状态（Boss 阶段/狂暴演出），按类型进入、按角色回落。
	/// GF.Fsm 约束：状态实例不跨状态机共享、同一类型只能有一个实例——所以同一个行为类不能同时占两个槽，每台状态机各建一套。
	/// </summary>
	public sealed class MonsterAiStateSet
	{
		private readonly Dictionary<MonsterAiRole, MonsterAiState> m_Roles = new();
		private readonly List<MonsterAiState> m_Extras = new();

		/// <summary>初始角色（出生即游荡，同旧项目出生首个决策必然走动）</summary>
		public MonsterAiRole InitialRole { get; set; } = MonsterAiRole.Patrol;

		/// <summary>把行为放进角色槽（替换原绑定）。可链式调用。</summary>
		public MonsterAiStateSet Bind(MonsterAiRole role, MonsterAiState state)
		{
			ArgumentNullException.ThrowIfNull(state);
			if (m_Roles.TryGetValue(role, out MonsterAiState old))
			{
				old.AssignRole(null);
			}

			state.AssignRole(role);
			m_Roles[role] = state;
			return this;
		}

		/// <summary>加入额外状态（不占槽，按类型进入）。可链式调用。</summary>
		public MonsterAiStateSet AddExtra(MonsterAiState state)
		{
			ArgumentNullException.ThrowIfNull(state);
			state.AssignRole(null);
			m_Extras.Add(state);
			return this;
		}

		/// <summary>角色槽当前的行为类型（未绑定抛异常：组装不完整是配置错误，尽早暴露）。</summary>
		public Type Resolve(MonsterAiRole role)
		{
			return m_Roles.TryGetValue(role, out MonsterAiState state)
				? state.GetType()
				: throw new InvalidOperationException($"怪物 AI 缺少角色 {role} 的行为绑定");
		}

		/// <summary>全部状态实例（传给 GF.Fsm.CreateFsm）。校验角色齐全、类型不重复。</summary>
		public MonsterAiState[] ToArray()
		{
			foreach (MonsterAiRole role in Enum.GetValues<MonsterAiRole>())
			{
				Resolve(role);
			}

			List<MonsterAiState> all = new(m_Roles.Values);
			all.AddRange(m_Extras);
			HashSet<Type> types = new();
			foreach (MonsterAiState state in all)
			{
				if (!types.Add(state.GetType()))
				{
					throw new InvalidOperationException($"怪物 AI 状态类型重复：{state.GetType().Name}（GF.Fsm 每种类型只能有一个实例）");
				}
			}

			return all.ToArray();
		}
	}
}
