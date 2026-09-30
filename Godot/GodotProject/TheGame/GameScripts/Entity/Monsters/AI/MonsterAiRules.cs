using System;

namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 怪物 AI 的纯规则函数（无状态、无随机源，随机数由调用方传入）——单测直接覆盖。
	/// 招式选择在 <see cref="MonsterSkillBook"/>，这里只放几何/移动/判定类规则。
	/// </summary>
	public static class MonsterAiRules
	{
		/// <summary>
		/// 近身招判定为"够得着"所需的最小水平重叠深度 px（工程常数，非平衡数值）：
		/// 判定盒与受击盒只擦边时物理重叠检测不稳定，要求吃进几像素再出招，避免"站定了却打不中"。
		/// </summary>
		public static readonly float ReachMargin = 4f;

		/// <summary>水平方向：dx &gt; 0 → 1，dx &lt; 0 → -1，0 → 0。</summary>
		public static int Sign(float dx)
		{
			return dx > 0f ? 1 : dx < 0f ? -1 : 0;
		}

		/// <summary>
		/// 招式范围（素材原生朝左）按朝向换算到世界轴向：朝右（dir &gt; 0）水平镜像，朝左/0 保持原样
		/// （同 ActorEntity.SetFacing：面朝右 = 判定盒容器 scale.x = -1）。
		/// </summary>
		public static AiBox ToFacing(AiBox nativeLeft, int dir)
		{
			return dir > 0 ? nativeLeft.MirroredX() : nativeLeft;
		}

		/// <summary>两盒水平间隙（带符号）：&gt;0 = 间隔 px，≤0 = 重叠深度取负。</summary>
		public static float HorizontalGap(AiBox a, AiBox b)
		{
			return Math.Max(a.Left - b.Right, b.Left - a.Right);
		}

		/// <summary>两盒垂直间隙（带符号）：&gt;0 = 间隔 px，&lt;0 = 重叠。</summary>
		public static float VerticalGap(AiBox a, AiBox b)
		{
			return Math.Max(a.Top - b.Bottom, b.Top - a.Bottom);
		}

		/// <summary>概率判定：roll ∈ [0,1)，percent ∈ 0..100；roll×100 &lt; percent 为命中（0 必不中，100 必中）。</summary>
		public static bool Chance(float roll, int percent)
		{
			return roll * 100f < percent;
		}

		/// <summary>巡逻折返方向：离出生点超出半径时返回指向出生点的方向，否则 0（不干预；半径 ≤0 = 不限）。</summary>
		public static int LeashDirection(float homeDeltaX, float patrolRadius)
		{
			if (patrolRadius <= 0f || Math.Abs(homeDeltaX) <= patrolRadius)
			{
				return 0;
			}

			return -Sign(homeDeltaX);
		}

		/// <summary>
		/// 技能尝试：未在出招时按"够得着"选可用技能并请求释放。返回是否已发起（发起后调用方本帧不再做移动决策）。
		/// 追击与站定两个状态共用（技能不要求先站定，同旧 Monster_103 在 have_target 途中放技能）。
		/// </summary>
		public static bool TryCastSkill(IMonsterAiAgent agent)
		{
			if (agent.IsAttacking)
			{
				return false;
			}

			int index = agent.Skills.SelectSkill(agent.TargetBox, Sign(agent.TargetDeltaX), agent.NextRandom());
			return index >= 0 && agent.RequestAttack(index);
		}
	}
}
