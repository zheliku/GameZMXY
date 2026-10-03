namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 怪物 AI 的宿主接口（GF.Fsm 的持有者类型）：AI 状态只通过它**读感知、写意图**，
	/// 不接触节点、场景树与随机单例——AI 目录是纯 C#，可脱离引擎单测（Tests/BattleTests）。
	///
	/// 分工（红线 7）：AI 只决定"想干什么"（移动意图 / 朝向 / 出招请求）；
	/// 受击、死亡、出招、收招硬直与动画播放由怪物的**身体状态机**（Monsters/Body/）决定——
	/// AI 只读它的结果（IsCcLocked / IsAttacking / IsDead），AI 状态名与身体状态、动画互不耦合。
	/// </summary>
	public interface IMonsterAiAgent
	{
		/// <summary>已死亡</summary>
		bool IsDead { get; }

		/// <summary>受控中：受击硬直（后续冰冻/眩晕/定身等控制 Buff 也并入这里）</summary>
		bool IsCcLocked { get; }

		/// <summary>
		/// 出招中：已请求待提交 / 攻击段在播 / 收招硬直（AttackConfig.AiRecovery）三者之一。
		/// 期间 AI 不做任何决策（不移动、不转身、不出下一招），宿主也拒绝 Face 与 RequestAttack。
		/// </summary>
		bool IsAttacking { get; }

		/// <summary>
		/// 目标受击盒相对自己原点的矩形（世界轴向，Y 向下；目标无受击盒时退化为其原点的点盒）。
		/// **空盒 = 没有目标**（未发现 / 已死亡回收 / 已丢失）。与招式判定盒（<see cref="MonsterAttackSpec.Reach"/>）
		/// 求交决定"够不够得着"——含高度；中心 X 即目标的水平方位。
		/// </summary>
		AiBox TargetBox { get; }

		/// <summary>自己相对出生点的水平偏移：self.x − home.x</summary>
		float HomeDeltaX { get; }

		/// <summary>决策参数</summary>
		MonsterAiParams Params { get; }

		/// <summary>怪物可用攻击的范围和权重</summary>
		MonsterAttackBook Attacks { get; }

		/// <summary>均匀随机数 [0,1)。随机源由宿主提供，单测可注入确定序列。</summary>
		float NextRandom();

		/// <summary>写移动意图：-1 左 / 0 停 / 1 右（移动时朝向随之翻转）</summary>
		void Move(int dir);

		/// <summary>原地转向：-1 左 / 1 右（0 不变；出招/受控中忽略）</summary>
		void Face(int dir);

		/// <summary>
		/// 请求出招（招式下标）。身体状态机在下一物理帧提交（进入出招状态并转向目标）；
		/// 当前不可出招（受控/死亡/已在出招）时返回 false。
		/// </summary>
		bool RequestAttack(int index);
	}
}
