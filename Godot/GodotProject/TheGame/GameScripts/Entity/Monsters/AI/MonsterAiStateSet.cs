using System;
using System.Collections.Generic;

namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 一台怪物 AI 状态机的状态集合：**角色 → 状态实例**的绑定表 + 额外状态。
	/// 标准状态集由 <see cref="MonsterBrains"/> 的原型工厂给出。
	///
	/// 扩展方式（在怪物类覆写的 MonsterEntity.CreateBrain 里、拿到原型之后调用）：
	///  * <see cref="Bind"/>：用自定义状态替换某个角色的默认实现（如飞行怪的 Chase、远程怪的 Attack）；
	///    其余状态按角色跳转，自动跳到替换后的实现；
	///  * <see cref="AddExtra"/>：加入不占用角色槽的额外状态（如 Boss 的阶段转换/狂暴演出），
	///    由自定义状态按类型 ChangeState 进入，离开时再按角色回到标准图。
	/// 状态实例不可跨状态机共享（GF.Fsm 约束），所以每台状态机各建一套（原型工厂每次都 new）。
	/// </summary>
	public sealed class MonsterAiStateSet
	{
		private readonly Dictionary<MonsterAiRole, MonsterAiState> m_Roles = new Dictionary<MonsterAiRole, MonsterAiState>();
		private readonly List<MonsterAiState> m_Extras = new List<MonsterAiState>();

		/// <summary>初始角色（出生即巡逻，同旧项目：出生时 ran_num∈[10,100]，首个决策必然走动）</summary>
		public MonsterAiRole InitialRole { get; set; } = MonsterAiRole.Patrol;

		/// <summary>把状态绑定到它声明的角色（替换原绑定）。</summary>
		public void Bind(MonsterAiState state)
		{
			if (state == null)
			{
				throw new ArgumentNullException(nameof(state));
			}

			m_Roles[state.Role] = state;
		}

		/// <summary>加入额外状态（不占角色槽，按类型进入）。同类型不可重复。</summary>
		public void AddExtra(MonsterAiState state)
		{
			if (state == null)
			{
				throw new ArgumentNullException(nameof(state));
			}

			m_Extras.Add(state);
		}

		/// <summary>角色当前绑定的状态类型（未绑定抛异常：状态集不完整是配置错误，尽早暴露）。</summary>
		public Type Resolve(MonsterAiRole role)
		{
			if (!m_Roles.TryGetValue(role, out MonsterAiState state))
			{
				throw new InvalidOperationException($"怪物 AI 状态集缺少角色 {role} 的绑定");
			}

			return state.GetType();
		}

		/// <summary>全部状态实例（传给 GF.Fsm.CreateFsm）。校验六个角色齐全、类型不重复。</summary>
		public MonsterAiState[] ToArray()
		{
			foreach (MonsterAiRole role in Enum.GetValues(typeof(MonsterAiRole)))
			{
				Resolve(role);
			}

			List<MonsterAiState> all = new List<MonsterAiState>(m_Roles.Values);
			all.AddRange(m_Extras);
			HashSet<Type> types = new HashSet<Type>();
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
