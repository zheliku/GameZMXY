using Godot;

namespace GameLogic.Level
{
	/// <summary>
	/// 关卡中的稳定空间锚点。位置由场景编辑器维护，关卡表只通过 SpawnPointId 引用它。
	/// </summary>
	public partial class LevelSpawnPoint : Marker2D
	{
		/// <summary>供关卡表引用的稳定 ID，不能依赖节点名称或层级顺序。</summary>
		[Export] public string SpawnPointId { get; set; } = string.Empty;

		/// <summary>可选的阶段提示，仅用于编辑器检查和调试显示。</summary>
		[Export] public int StageOrder { get; set; }

		/// <summary>检查场景作者是否填写了供数据表引用的稳定 ID。</summary>
		public override void _Ready()
		{
			if (string.IsNullOrWhiteSpace(SpawnPointId))
			{
				GD.PushError($"[LevelSpawnPoint] {GetPath()} 缺少 SpawnPointId。");
			}
		}
	}
}
