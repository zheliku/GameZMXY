namespace GameLogic.Entity.AI
{
	/// <summary>
	/// 怪物 AI 的纯规则函数（无状态、无随机源，随机数由调用方传入）——单测直接覆盖。
	/// 招式选择在 <see cref="MonsterSkillBook"/>，这里只放移动/判定类规则。
	/// </summary>
	public static class MonsterAiRules
	{
		/// <summary>水平方向：dx &gt; 0 → 1，dx &lt; 0 → -1，0 → 0。</summary>
		public static int Sign(float dx)
		{
			return dx > 0f ? 1 : dx < 0f ? -1 : 0;
		}

		/// <summary>目标是否进入站定距离（只比水平距离，同旧 follow_Hero 的 abs(dx) 判定）。</summary>
		public static bool InAttackRange(float targetDeltaX, float attackRange)
		{
			return System.Math.Abs(targetDeltaX) <= attackRange;
		}

		/// <summary>概率判定：roll ∈ [0,1)，percent ∈ 0..100；roll×100 &lt; percent 为命中（0 必不中，100 必中）。</summary>
		public static bool Chance(float roll, int percent)
		{
			return roll * 100f < percent;
		}

		/// <summary>巡逻折返方向：离出生点超出半径时返回指向出生点的方向，否则 0（不干预；半径 ≤0 = 不限）。</summary>
		public static int LeashDirection(float homeDeltaX, float patrolRadius)
		{
			if (patrolRadius <= 0f || System.Math.Abs(homeDeltaX) <= patrolRadius)
			{
				return 0;
			}

			return -Sign(homeDeltaX);
		}

		/// <summary>
		/// 技能尝试：未在出招时按距离选可用技能并请求释放。返回是否已发起（发起后调用方本帧不再做移动决策）。
		/// 追击与站定两个状态共用（技能不要求先站定，同旧 Monster_103 在 have_target 途中放技能）。
		/// </summary>
		public static bool TryCastSkill(IMonsterAiAgent agent)
		{
			if (agent.IsAttacking)
			{
				return false;
			}

			float distance = System.Math.Abs(agent.TargetDeltaX);
			int index = agent.Skills.SelectSkill(distance, agent.NextRandom());
			return index >= 0 && agent.RequestAttack(index);
		}
	}
}
