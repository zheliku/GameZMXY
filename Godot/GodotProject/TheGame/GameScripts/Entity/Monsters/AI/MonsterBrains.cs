using GameLogic.Entity.Monsters.AI.States;

namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 怪物"大脑原型"工厂：每个方法返回一套**全新**的状态集（GF.Fsm 约束：状态实例不跨状态机共享）。
	///
	/// 每种怪物在自己的类里覆写 <c>MonsterEntity.CreateBrain()</c>，显式选一套原型，
	/// 需要时在返回前 <see cref="MonsterAiStateSet.Bind"/> 替换角色或 <see cref="MonsterAiStateSet.AddExtra"/> 追加状态
	/// （参考 tModLoader 的 aiStyle / Unity Game Kit 的每怪一个 Behaviour：共用"大脑"，差异放在自己的类里）。
	///
	/// 已有原型：
	///  * <see cref="GroundMelee"/>：地面近战——巡逻 → 追击 → 站定普攻/技能 → 受控 → 死亡。
	/// 待扩展（随对应怪物加入）：飞行（换 Patrol/Chase）、远程风筝（换 Attack）……
	/// </summary>
	public static class MonsterBrains
	{
		/// <summary>地面近战原型（花果山猴子等标准小怪）。</summary>
		public static MonsterAiStateSet GroundMelee()
		{
			MonsterAiStateSet set = new MonsterAiStateSet();
			set.Bind(new MonsterIdleState());
			set.Bind(new MonsterPatrolState());
			set.Bind(new MonsterChaseState());
			set.Bind(new MonsterAttackState());
			set.Bind(new MonsterCcLockedState());
			set.Bind(new MonsterDeathState());
			return set;
		}
	}
}
