using Godot;
using GameLogic.Entity.Body;

namespace GameLogic.Entity.Heroes.Body
{
	/// <summary>
	/// 英雄身体状态机的宿主接口：在公共契约 <see cref="IActorBody"/> 之上只保留英雄专属成员
	/// （输入层、跳跃/连段事实、参数快照、随机源）。身体状态只经它读写，不接触节点、场景树、GD.*
	/// （纯 C#，可单测；Vector2 是 Godot 纯值类型，同 Battle/ 裁决）。
	/// 实现方：<see cref="HeroEntity"/>（真实物理）与 Tests 里的假宿主。
	/// </summary>
	public interface IHeroBody : IActorBody
	{
		/// <summary>身体参数（配置快照，含各动作时长 = 动画长度）</summary>
		HeroBodyParams Params { get; }

		/// <summary>输入层（移动轴 / 跑步档 / 缓冲请求）</summary>
		HeroInput Input { get; }

		/// <summary>站在地面上（上一次 MoveAndSlide 的结果）</summary>
		bool OnFloor { get; }

		/// <summary>已用跳跃次数（起跳 +1；宿主在落地后归零）</summary>
		int JumpCount { get; set; }

		/// <summary>连段序号（下一次起手用哪段；语义同旧 hit_count）</summary>
		int ComboIndex { get; set; }

		/// <summary>均匀随机数 [0,1)（随机源由宿主提供，单测注入确定值）</summary>
		float NextRandom();
	}
}
