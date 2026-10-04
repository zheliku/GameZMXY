using System;

namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// AI 用的轴对齐矩形（纯 C# 值类型；AI 目录不引用 Godot 类型）。坐标相对怪物原点，Y 向下（同 Godot）。
	///  * 招式范围（<see cref="MonsterAttackSpec.Reach"/>）：攻击动画判定盒推导，**素材原生朝左**，用 <see cref="Facing"/> 换算；
	///  * 目标受击盒（<see cref="IMonsterAiAgent.TargetBox"/>）：世界轴向，已减去怪物原点。
	/// 默认值（全 0）是空盒。
	/// </summary>
	public readonly struct AiBox
	{
		/// <summary>矩形左边界。</summary>
		public readonly float Left;
		/// <summary>矩形右边界。</summary>
		public readonly float Right;
		/// <summary>矩形上边界。</summary>
		public readonly float Top;
		/// <summary>矩形下边界。</summary>
		public readonly float Bottom;

		/// <summary>创建矩形并规范化边界顺序。</summary>
		/// <param name="left">左侧输入坐标。</param>
		/// <param name="right">右侧输入坐标。</param>
		/// <param name="top">上侧输入坐标。</param>
		/// <param name="bottom">下侧输入坐标。</param>
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

		/// <summary>原生朝左的范围按朝向换算到世界轴向：朝右（dir &gt; 0）水平镜像（同 ActorEntity.SetFacing）。</summary>
		public AiBox Facing(int dir)
		{
			return dir > 0 ? new AiBox(-Right, -Left, Top, Bottom) : this;
		}

		/// <summary>与另一盒的水平间隙（带符号：&gt;0 = 间隔 px，≤0 = 重叠深度取负）。</summary>
		public float GapX(AiBox other)
		{
			return Math.Max(Left - other.Right, other.Left - Right);
		}

		/// <summary>与另一盒的垂直间隙（带符号：&gt;0 = 间隔 px，&lt;0 = 重叠）。</summary>
		public float GapY(AiBox other)
		{
			return Math.Max(Top - other.Bottom, other.Top - Bottom);
		}

		/// <summary>并集（空盒不参与）</summary>
		public AiBox Union(AiBox other)
		{
			if (IsEmpty)
			{
				return other;
			}

			return other.IsEmpty
				? this
				: new AiBox(Math.Min(Left, other.Left), Math.Max(Right, other.Right), Math.Min(Top, other.Top),
					Math.Max(Bottom, other.Bottom));
		}

		/// <summary>点盒（目标没有受击盒时退化为其原点；1px 宽，非空）</summary>
		public static AiBox Point(float x, float y)
		{
			return new AiBox(x - 0.5f, x + 0.5f, y - 0.5f, y + 0.5f);
		}

		/// <summary>返回空盒标记或矩形边界的调试文本。</summary>
		public override string ToString()
		{
			return IsEmpty ? "AiBox(empty)" : $"AiBox(X {Left:F1}..{Right:F1}, Y {Top:F1}..{Bottom:F1})";
		}
	}
}
