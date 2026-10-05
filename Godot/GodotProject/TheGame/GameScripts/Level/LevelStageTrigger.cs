using Godot;

namespace GameLogic.Level
{
	/// <summary>阶段触发锚点。触发器的空间位置由场景维护，阶段表只引用 TriggerId。</summary>
	public partial class LevelStageTrigger : Area2D
	{
		/// <summary>供阶段表引用的稳定 ID。</summary>
		[Export] public string TriggerId { get; set; } = string.Empty;
	}
}
