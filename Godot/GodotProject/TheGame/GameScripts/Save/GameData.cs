using GameLogic.Save;
using GodotGameFramework.Archive;
using System;

/// <summary>
/// 存档槽数据（框架 <c>ArchiveSystem&lt;GameCatalogue, GameData&gt;</c> 写死的类型，必须保持全局命名空间与类名）。
/// 字段一律不设初始值：Newtonsoft 先调用默认构造再填 JSON，带初始值会让缺字段的旧档被误判为新版本。
/// 版本判定与迁移只在 <see cref="SaveMigrator"/>。
/// </summary>
[Serializable]
public class GameData : ArchiveData
{
	/// <summary>当前格式版本；0 = 重构前格式或框架首次启动创建的空档。</summary>
	public int SaveVersion;

	/// <summary>版本 1 起的档案快照。</summary>
	public ProfileSaveData Profile;

	/// <summary>版本 0 的玩家数据，只读；迁移后置空。</summary>
	public PlayerSaveDataV0 Player;
}
