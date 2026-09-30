using System;

namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// AI 用的轴对齐矩形（纯 C# 值类型，AI 目录不引用 Godot 类型）。坐标相对怪物原点，Y 向下（同 Godot）。
	///
	/// 两种来源：
	///  * 招式够得着的范围（<see cref="MonsterSkillSpec.Reach"/>）：由攻击动画的判定盒关键帧推导，
	///    **素材原生朝左**坐标（同动画轨道），用 <see cref="MonsterAiRules.ToFacing"/> 按朝向镜像；
	///  * 目标受击盒（<see cref="IMonsterAiAgent.TargetBox"/>）：世界轴向，已减去怪物原点。
	/// 默认值（全 0）为空盒：Right 不大于 Left 即视为空。
	/// </summary>
	public readonly struct AiBox
	{
		public readonly float Left;
		public readonly float Right;
		public readonly float Top;
		public readonly float Bottom;

		public AiBox(float left, float right, float top, float bottom)
		{
			Left = Math.Min(left, right);
			Right = Math.Max(left, right);
			Top = Math.Min(top, bottom);
			Bottom = Math.Max(top, bottom);
		}

		/// <summary>空盒（无判定盒 / 无目标）</summary>
		public bool IsEmpty => !(Right > Left);

		/// <summary>水平中心</summary>
		public float CenterX => (Left + Right) * 0.5f;

		/// <summary>绕原点水平镜像（原生朝左 → 朝右）</summary>
		public AiBox MirroredX()
		{
			return new AiBox(-Right, -Left, Top, Bottom);
		}

		/// <summary>并集（空盒不参与）</summary>
		public AiBox Union(AiBox other)
		{
			if (IsEmpty)
			{
				return other;
			}

			if (other.IsEmpty)
			{
				return this;
			}

			return new AiBox(Math.Min(Left, other.Left), Math.Max(Right, other.Right), Math.Min(Top, other.Top),
				Math.Max(Bottom, other.Bottom));
		}

		/// <summary>点盒（目标没有受击盒时退化为原点位置；宽度极小但非空）</summary>
		public static AiBox Point(float x, float y)
		{
			const float half = 0.5f;
			return new AiBox(x - half, x + half, y - half, y + half);
		}

		public override string ToString()
		{
			return IsEmpty ? "AiBox(empty)" : $"AiBox(X {Left:F1}..{Right:F1}, Y {Top:F1}..{Bottom:F1})";
		}
	}
}
