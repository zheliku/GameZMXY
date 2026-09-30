namespace GameLogic.Entity.AI
{
	/// <summary>
	/// 怪物 AI 的宿主接口（GF.Fsm 的持有者类型）：AI 状态只通过它**读感知、写意图**，
	/// 不接触节点、场景树与随机单例——AI 目录是纯 C#，可脱离引擎单测（Tests/BattleTests）。
	///
	/// 分工（红线 7）：AI 只决定"想干什么"（移动意图 / 朝向 / 出招请求）；
	/// 动画选择仍由该怪物自己的 AnimationTree 读事实面（MoveInput / AttackSegment / Hurt / Dead）完成，
	/// AI 状态名与动画状态名互不耦合。
	/// </summary>
	public interface IMonsterAiAgent
	{
		/// <summary>已死亡</summary>
		bool IsDead { get; }

		/// <summary>受控中：受击硬直（后续冰冻/眩晕/定身等控制 Buff 也并入这里）</summary>
		bool IsCcLocked { get; }

		/// <summary>出招中（含已请求、待提交的出招）</summary>
		bool IsAttacking { get; }

		/// <summary>有有效目标（索敌命中或被打后锁定；目标死亡/回收即失效）</summary>
		bool HasTarget { get; }

		/// <summary>目标相对自己的水平距离：target.x − self.x（无目标为 0）</summary>
		float TargetDeltaX { get; }

		/// <summary>自己相对出生点的水平偏移：self.x − home.x</summary>
		float HomeDeltaX { get; }

		/// <summary>决策参数</summary>
		MonsterAiParams Params { get; }

		/// <summary>技能书（冷却与选招；冷却由宿主推进与计入）</summary>
		MonsterSkillBook Skills { get; }

		/// <summary>本状态机的状态集（角色 → 状态类型）</summary>
		MonsterAiStateSet States { get; }

		/// <summary>均匀随机数 [0,1)。随机源由宿主提供，单测可注入确定序列。</summary>
		float NextRandom();

		/// <summary>写移动意图：-1 左 / 0 停 / 1 右（移动时朝向随之翻转）</summary>
		void Move(int dir);

		/// <summary>原地转向：-1 左 / 1 右（0 不变；出招/受控中忽略）</summary>
		void Face(int dir);

		/// <summary>
		/// 请求出招（招式下标）。宿主在安全时机提交为 AttackSegment 事实，提交时转向目标并计入冷却；
		/// 当前不可出招（受控/死亡/已在出招）时返回 false。
		/// </summary>
		bool RequestAttack(int index);
	}
}
