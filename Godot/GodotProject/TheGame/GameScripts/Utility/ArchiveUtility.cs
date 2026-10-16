using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameLogic.Architecture;
using GodotGameFramework.Archive;

namespace GameLogic.Utility;

/// <summary>泛型 GGF 存档适配器；传递框架读写结果，不解释游戏字段、版本或检查点。</summary>
/// <typeparam name="TCatalogue">框架目录类型。</typeparam>
/// <typeparam name="TData">框架存档类型，工具不依赖它的业务字段。</typeparam>
public sealed class ArchiveUtility<TCatalogue, TData> : IUtility
    where TCatalogue : ArchiveCatalogue, new()
    where TData : ArchiveData, new()
{
    private readonly ArchiveSystem<TCatalogue, TData> m_Archive; // 借用：GGF 拥有并串行调度的存档服务。

    /// <summary>框架当前目录列表，只借用。</summary>
    public IReadOnlyList<TCatalogue> Catalogues => m_Archive.Catalogues;

    /// <summary>框架当前目录记录，只借用。</summary>
    public TCatalogue CurrentCatalogue => m_Archive.CurrentCatalogue;

    /// <summary>框架当前数据记录，只借用。</summary>
    public TData CurrentData => m_Archive.CurrentData;

    /// <summary>绑定已初始化的第三方存档服务。</summary>
    /// <param name="archive">框架存档服务。</param>
    public ArchiveUtility(ArchiveSystem<TCatalogue, TData> archive) =>
        m_Archive = archive ?? throw new ArgumentNullException(nameof(archive));

    /// <summary>由框架新建一个槽位并写盘。</summary>
    /// <returns>框架读写是否成功。</returns>
    public Task<bool> SaveAsync() => m_Archive.SaveAsync();

    /// <summary>读取框架目录和默认槽。</summary>
    /// <returns>框架读写是否成功。</returns>
    public Task<bool> LoadAsync() => m_Archive.LoadAsync();

    /// <summary>读取明确指定的文件。</summary>
    /// <param name="unitId">框架文件编号。</param>
    /// <returns>框架读写是否成功。</returns>
    public Task<bool> LoadAsync(long unitId) => m_Archive.LoadAsync(unitId);

    /// <summary>请求框架覆盖当前槽，备份与原子写由框架处理。</summary>
    /// <returns>框架读写是否成功。</returns>
    public Task<bool> OverWriteAsync() => m_Archive.OverWriteAsync();
}
