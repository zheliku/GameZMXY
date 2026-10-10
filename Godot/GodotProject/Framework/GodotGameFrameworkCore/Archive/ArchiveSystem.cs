using GodotGameFramework.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GameConfig.Constant;

namespace GodotGameFramework.Archive;
/// <summary>
/// 存档目录
/// </summary>
public class ArchiveCatalogue
{
    public long UnitId; // 单位ID
}
/// <summary>
/// 存档数据
/// </summary>
public class ArchiveData
{
    public long UnitId; // 单位ID
}

/// <summary>
/// 通用存档系统：目录（Catalogue.sav，全部槽位的显示信息）+ 每槽数据（Data/{UnitId}.sav）。
/// <para>可靠性约定（2026-10 加固，项目登记的框架例外）：</para>
/// <list type="bullet">
/// <item>所有写入都是原子的：先写临时文件再替换目标，旧内容保留为 <c>.bak</c>；写失败目标保持原样。</item>
/// <item>所有读写按调用顺序严格串行；写入在调用时刻（主线程）完成序列化，之后修改 CurrentData 不影响已排队的写入。</item>
/// <item>读取时主文件不存在或损坏会回退到 <c>.bak</c>；两者都不可用才失败，且不会新建空档覆盖。</item>
/// <item>覆盖已有槽位时同时重写目录，槽位显示信息随数据一起更新。</item>
/// <item>所有操作返回是否成功；失败原因写错误日志。</item>
/// </list>
/// </summary>
public sealed class ArchiveSystem<T, U> where T : ArchiveCatalogue, new() where U : ArchiveData, new()
{
    private const string CatalogueFile = "Catalogue.sav"; // 目录文件名（相对存档根目录）。

    private Task m_Tail = Task.CompletedTask; // 串行队列尾：每个读写操作排在它之后执行。

    public List<T> Catalogues { get; private set; } = new();
    public T CurrentCatalogue { get; private set; }
    public U CurrentData { get; private set; }
    private ArchiveSetting m_Setting;
    public ArchiveSetting Setting
    {
        get
        {
            if (m_Setting == null)
            {
                m_Setting = ResourceLoader.Load<ArchiveSetting>(ResourcesCollectionConstant.Resources_ArchiveSetting);
            }
            return m_Setting;
        }
    }

    /// <summary>目录文件路径（相对 user://）。</summary>
    private string CataloguePath => $"{Setting.Folder}/{CatalogueFile}";

    /// <summary>指定槽位的数据文件路径（相对 user://）。</summary>
    private string DataPath(long unitId) => $"{Setting.Folder}/Data/{unitId}.sav";

    /// <summary>
    /// 创建新存档槽并保存：生成新 UnitId，CurrentData 置为空数据，写入目录与数据文件。
    /// </summary>
    /// <returns>目录与数据是否都写入成功。</returns>
    public Task<bool> SaveAsync()
    {
        // 新 UnitId 用时间戳；同一秒内重复创建时顺延，保证唯一。
        long unitId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        while (Catalogues.Exists(x => x.UnitId == unitId))
        {
            unitId++;
        }

        var catalogue = new T { UnitId = unitId };
        Catalogues.Add(catalogue);
        var data = new U { UnitId = unitId };
        CurrentCatalogue = catalogue;
        CurrentData = data;

        // 调用时刻序列化，之后的修改不影响本次写入。
        string catalogueText = SerializeCatalogues();
        string dataText = Serialize(data);
        return Enqueue(async () =>
        {
            bool dataSaved = await EasySave.WriteUserTextAtomicAsync(DataPath(unitId), dataText);
            bool catalogueSaved = dataSaved && await EasySave.WriteUserTextAtomicAsync(CataloguePath, catalogueText);
            LogResult(catalogueSaved, "创建存档", unitId);
            return catalogueSaved;
        });
    }

    /// <summary>
    /// 将 CurrentData 保存到已有存档条目（并同步重写目录，槽位显示信息随之更新）。
    /// </summary>
    /// <param name="unitId">目标槽位。</param>
    /// <returns>数据与目录是否都写入成功；槽位不存在或没有数据时返回 false。</returns>
    public Task<bool> SaveAsync(long unitId)
    {
        T catalogue = Catalogues.Find(x => x.UnitId == unitId);
        if (catalogue == null)
        {
            Log.Error("[ArchiveSystem]存档目录中不存在该单位ID{0}", unitId);
            return Task.FromResult(false);
        }

        if (CurrentData == null)
        {
            Log.Error("[ArchiveSystem]当前没有数据可保存，单位ID{0}", unitId);
            return Task.FromResult(false);
        }

        // 数据归属该槽位后在调用时刻序列化。
        CurrentCatalogue = catalogue;
        CurrentData.UnitId = unitId;
        string dataText = Serialize(CurrentData);
        string catalogueText = SerializeCatalogues();
        return Enqueue(async () =>
        {
            bool dataSaved = await EasySave.WriteUserTextAtomicAsync(DataPath(unitId), dataText);
            bool catalogueSaved = dataSaved && await EasySave.WriteUserTextAtomicAsync(CataloguePath, catalogueText);
            LogResult(catalogueSaved, "保存存档", unitId);
            return catalogueSaved;
        });
    }

    /// <summary>
    /// 覆盖当前活跃存档（= SaveAsync(CurrentCatalogue.UnitId)）。
    /// </summary>
    /// <returns>是否写入成功；没有活跃存档时返回 false。</returns>
    public Task<bool> OverWriteAsync()
    {
        if (CurrentCatalogue == null)
        {
            Log.Error("[ArchiveSystem]当前没有激活的存档，无法覆盖");
            return Task.FromResult(false);
        }

        return SaveAsync(CurrentCatalogue.UnitId);
    }

    /// <summary>
    /// 加载或者初始化存档数据，默认加载最新存档。
    /// 仅当目录文件与其备份都不存在时才新建存档；文件存在但读取失败时拒绝覆盖，避免吞掉玩家数据。
    /// </summary>
    /// <returns>是否得到可用的 CurrentCatalogue 与 CurrentData。</returns>
    public async Task<bool> LoadAsync()
    {
        // 排在已提交的写入之后，读到的是最近一次写入的结果。
        await Drain();

        // 目录与备份都不存在 → 首次启动
        if (!EasySave.ExistsInUserOrBackup(CataloguePath))
        {
            return await SaveAsync();
        }

        var (catalogues, fromBackup) = await EasySave.LoadFromUserWithBackupAsync<List<T>>(CataloguePath,
            Setting.EnableAesEncryption, Setting.KEY, Setting.Salt);
        if (catalogues == null || catalogues.Count == 0)
        {
            Log.Error("[ArchiveSystem] 存档存在但读取失败，已拒绝覆盖。请检查密钥/盐值是否变更或存档是否损坏。");
            return false;
        }

        if (fromBackup)
        {
            Log.Warning("[ArchiveSystem] 存档目录主文件不可用，已从备份恢复。");
        }

        Catalogues = catalogues;
        return await LoadDataAsync(Catalogues[^1]);
    }

    /// <summary>
    /// 按单位ID加载存档数据
    /// </summary>
    /// <param name="unitId">目标槽位。</param>
    /// <returns>是否加载成功。</returns>
    public async Task<bool> LoadAsync(long unitId)
    {
        await Drain();
        if (Catalogues == null || Catalogues.Count == 0)
        {
            Log.Error("[ArchiveSystem]存档目录为空，请先调用 LoadAsync() 初始化");
            return false;
        }

        T catalogue = Catalogues.Find(x => x.UnitId == unitId);
        if (catalogue == null)
        {
            Log.Error("[ArchiveSystem]存档目录中不存在该单位ID{0}", unitId);
            return false;
        }

        return await LoadDataAsync(catalogue);
    }

    /// <summary>
    /// 删除指定存档
    /// </summary>
    /// <param name="unitId">目标槽位。</param>
    /// <returns>数据文件与目录是否都处理成功。</returns>
    public Task<bool> Delete(long unitId)
    {
        if (Catalogues == null || Catalogues.Count == 0)
        {
            Log.Error("[ArchiveSystem]存档目录为空，无法删除");
            return Task.FromResult(false);
        }

        T catalogue = Catalogues.Find(x => x.UnitId == unitId);
        if (catalogue == null)
        {
            Log.Error("[ArchiveSystem]存档目录中不存在该单位ID{0}", unitId);
            return Task.FromResult(false);
        }

        Catalogues.Remove(catalogue);

        // 如果删除的是当前活跃的存档，重置 CurrentCatalogue 和 CurrentData
        if (CurrentCatalogue != null && CurrentCatalogue.UnitId == unitId)
        {
            CurrentCatalogue = Catalogues.Count > 0 ? Catalogues[^1] : null;
            CurrentData = null;
        }

        string catalogueText = SerializeCatalogues();
        return Enqueue(async () =>
        {
            bool deleted = await EasySave.DeleteInUserWithBackupAsync(DataPath(unitId));
            bool catalogueSaved = await EasySave.WriteUserTextAtomicAsync(CataloguePath, catalogueText);
            LogResult(deleted && catalogueSaved, "删除存档", unitId);
            return deleted && catalogueSaved;
        });
    }

    /// <summary>读取一个槽位的数据文件（主文件不可用时回退备份）并设为当前存档。</summary>
    /// <param name="catalogue">目录中的槽位。</param>
    /// <returns>是否加载成功。</returns>
    private async Task<bool> LoadDataAsync(T catalogue)
    {
        var (data, fromBackup) = await EasySave.LoadFromUserWithBackupAsync<U>(DataPath(catalogue.UnitId),
            Setting.EnableAesEncryption, Setting.KEY, Setting.Salt);
        if (data == null)
        {
            Log.Error("[ArchiveSystem]加载存档数据失败，单位ID{0}", catalogue.UnitId);
            return false;
        }

        if (fromBackup)
        {
            Log.Warning("[ArchiveSystem]存档数据主文件不可用，已从备份恢复，单位ID{0}", catalogue.UnitId);
        }

        CurrentCatalogue = catalogue;
        CurrentData = data;
        Log.Info("[ArchiveSystem]加载存档数据成功，单位ID{0}", catalogue.UnitId);
        return true;
    }

    /// <summary>把一个 IO 操作排到队尾，按调用顺序严格执行；前一个操作失败不影响后续操作。</summary>
    /// <param name="operation">要执行的 IO 操作。</param>
    /// <returns>该操作的结果。</returns>
    private Task<bool> Enqueue(Func<Task<bool>> operation)
    {
        Task previous = m_Tail;
        Task<bool> next = RunAfterAsync(previous, operation);
        m_Tail = next;
        return next;
    }

    /// <summary>等待前一个操作结束（忽略其异常）后执行本操作。</summary>
    private static async Task<bool> RunAfterAsync(Task previous, Func<Task<bool>> operation)
    {
        try
        {
            await previous;
        }
        catch (Exception)
        {
            // 前一个操作的失败已由它自己的调用方处理，这里只保证顺序。
        }

        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            Log.Error("[ArchiveSystem]存档 IO 异常：{0}", ex.Message);
            return false;
        }
    }

    /// <summary>等待队列中已提交的操作全部完成。</summary>
    private async Task Drain()
    {
        try
        {
            await m_Tail;
        }
        catch (Exception)
        {
            // 失败已由提交方处理；读取只需要顺序。
        }
    }

    /// <summary>按存档设置序列化单个对象。</summary>
    private string Serialize<V>(V value) => EasySave.Serialize(value, Setting.EnableAesEncryption, Setting.KEY, Setting.Salt);

    /// <summary>序列化当前目录列表。</summary>
    private string SerializeCatalogues() => Serialize(Catalogues);

    /// <summary>按结果记录成功或失败日志。</summary>
    private static void LogResult(bool success, string operation, long unitId)
    {
        if (success)
        {
            Log.Info("[ArchiveSystem]{0}成功，单位ID{1}", operation, unitId);
        }
        else
        {
            Log.Error("[ArchiveSystem]{0}失败，单位ID{1}", operation, unitId);
        }
    }
}
