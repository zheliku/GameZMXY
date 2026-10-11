using GodotGameFramework.Archive;
using System;

/// <summary>Catalogue.sav 中一格的索引与显示信息：文件编号、格号、角色名称和保存时间。</summary>
/// <remarks>这是文件目录元数据；角色经验与金币保存在 GameData 中。</remarks>
[Serializable]
public class GameCatalogue : ArchiveCatalogue
{
    /// <summary>存档显示名称。</summary>
    public string Name;

    /// <summary>六格界面中的稳定格号；0 仅表示框架尚未认领的新空槽。</summary>
    public int SlotNumber;

    /// <summary>最近一次检查点的 UTC Unix 秒数。</summary>
    public long SavedAtUtc;
}
