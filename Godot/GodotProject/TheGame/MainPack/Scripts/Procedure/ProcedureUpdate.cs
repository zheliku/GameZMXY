//------------------------------------------------------------
// 更新检测流程
// 连接服务器检测版本更新，有更新则下载补丁包
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using GameConfig.Constant;
using GameFramework;
using GameFramework.Procedure;
using GameFramework.Resource;
using GameLogic;
using Godot;
using GodotGameFramework;
using GodotGameFramework.HotUpdate;
using GodotGameFramework.Json;
using GodotGameFramework.Web;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;
using GodotGameFramework.Download;
using GodotGameFramework.Extensions;
using GodotGameFramework.UI;

/// <summary>
/// 更新检测流程。
/// 步骤：校验本地客户端 -> 请求版本文件 → 比对本地 → 下载差量包 → SHA256 校验 → 保存版本 → 加载子包
/// </summary>
public class ProcedureUpdate : ProcedureBase
{
    private const int MaxRetries = 3; // 每个网络操作的最大尝试次数。
    private const float RetryBaseDelaySeconds = 1.5f; // 重试之间的基础等待时间（秒）。
    private const float VersionFetchTimeoutSeconds = 10f; // 单次版本清单请求超时（秒）。
    LoadingForm m_loadingForm; // 当前热更流程使用的加载界面。

    private string SubpackDir => GetOrCreateHotUpdateDir(); // 按配置、安装目录可写性和用户目录选择热更目录。

    private string GetOrCreateHotUpdateDir() // 选择并确保存在热更子包目录。
    {
        // 1. 开发者显式配置的路径
        string customPath = GF.Resource?.UpdateSettingRes?.HotUpdatePath;
        if (!string.IsNullOrEmpty(customPath))
        {
            EnsureDirectory(customPath);
            return customPath;
        }

        // 2. 游戏安装目录（大多数 PC 游戏不装在 C:\Program Files\）
        string exeDir = OS.HasFeature("editor")
            ? $"{ProjectSettings.GlobalizePath("res://")}" + "../../Godot"
            : System.IO.Path.GetDirectoryName(OS.GetExecutablePath());

        if (!string.IsNullOrEmpty(exeDir))
        {
            string gameSubpackDir = Path.Combine(exeDir, "subpackages");
            if (IsDirectoryWritable(gameSubpackDir))
                return gameSubpackDir;
        }

        // 3. 回退 user://（一定能写，但在 C 盘）
        string userSubpackDir = Path.Combine(
            ProjectSettings.GlobalizePath("user://"), "subpackages");
        EnsureDirectory(userSubpackDir);
        return userSubpackDir;
    }

    private bool IsDirectoryWritable(string path) // 通过临时文件检查目录是否可写。
    {
        try
        {
            EnsureDirectory(path);
            string testFile = Path.Combine(path, ".write_test");
            File.WriteAllText(testFile, " ");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>初始化更新流程并执行基类初始化。</summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    protected internal override void OnInit(ProcedureOwner procedureOwner)
    {
        base.OnInit(procedureOwner);
    }

    /// <summary>执行版本检查、完整性校验、下载和子包加载。</summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    protected internal override async void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);

        try
        {
            await RunUpdateFlowAsync(procedureOwner);
        }
        catch (Exception ex)
        {
            Log.Error("[ProcedureUpdate] 更新流程异常: {0}", ex);

            // 检查是否为强制更新，若是则阻塞而非跳过
            var localVersion = EasySave.LoadFromUser<PackVersionList>(
                ResourceManager.GameFrameworkVersionData);
            if (localVersion?.ForceUpdate == true)
            {
                bool retry = await ShowForceUpdateDialogAsync("更新流程异常: " + ex.Message + "请重试或退出游戏。");
                if (retry)
                {
                    await RunUpdateFlowAsync(procedureOwner);
                    return;
                }
                GameEntry.Shutdown(ShutdownType.Quit);
                return;
            }

            await SkipToNextAsync(procedureOwner);
        }
    }

    // ── 主流程 ──

    private async Task RunUpdateFlowAsync(ProcedureOwner procedureOwner) // 执行版本检查、完整性校验、下载和子包加载。
    {
        // Package 模式不检测更新，但尝试加载本地子包（安装目录 subpackages/）
        if (GF.Resource.ResourceMode == ResourceMode.Package)
        {
            Log.Info("[ProcedureUpdate] Package 模式，跳过更新检测。");
            if (!GF.Base.EnableEditorResLoad)
            {
                await TryLoadLocalSubpackagesAsync();
            }
            ChangeState<ProcedurePrelode>(procedureOwner);
            return;
        }

        // ── 0. 崩溃恢复检测（必须在一切之前） ──
        if (HotUpdateSafetyGuard.WasLastSessionCrashed())
        {
            HotUpdateSafetyGuard.EnterSafeMode();
            Log.Warning("[ProcedureUpdate] 上次启动崩溃，本次跳过所有热更补丁。");
            ChangeState<ProcedurePrelode>(procedureOwner);
            return;
        }

        // 校验远程地址
        string remoteUrl = GF.Resource.UpdateSettingRes?.RemoteUrl;
        if (string.IsNullOrEmpty(remoteUrl))
        {
            // 没有远程地址 → 跳过更新检测，但仍加载已下载的本地补丁
            Log.Warning("[ProcedureUpdate] 未配置 RemoteUrl，跳过更新检测。");
            var localVersion = EasySave.LoadFromUser<PackVersionList>(
                ResourceManager.GameFrameworkVersionData);
            if (localVersion != null)
            {
                int damaged = await VerifyLocalPackIntegrityAsync(localVersion);
                if (damaged > 0)
                    Log.Warning("[ProcedureUpdate] 本地 {0} 个文件损坏，但无法连接服务器修复。", damaged);
                await LoadDownloadedPacksAsync(localVersion);
            }
            ChangeState<ProcedurePrelode>(procedureOwner);
            return;
        }

        m_loadingForm = await GF.UI.OpenLoadingUIFormAsync();

        try
        {
            bool isForceUpdate = false;
            PackVersionList serverVersion = null;

            // ── 1. 请求服务器版本文件 ──
            string versionUrl = $"{remoteUrl.TrimEnd('/')}/{ResourceManager.GameFrameworkVersionData}";
            Log.Info("[ProcedureUpdate] 请求版本文件: {0}", versionUrl);
            m_loadingForm?.SetLogState("检测更新...", 0);

            // 先加载本地版本，用于 ForceUpdate 回退判断（带完整性校验）
            var localVersionPre = ResourceManager.LocalPackVersionList
                ?? NodeUtility.LoadAndValidateVersionList(ResourceManager.GameFrameworkVersionData);
            if (localVersionPre != null)
                ResourceManager.LocalPackVersionList = localVersionPre;
            isForceUpdate = localVersionPre?.ForceUpdate == true;

            serverVersion = await FetchVersionWithRetryAsync(versionUrl);
            if (serverVersion == null || !serverVersion.IsValid())
            {
                if (isForceUpdate)
                {
                    // 强制更新模式下，服务器不可达时阻塞等待
                    string msg = localVersionPre?.ForceUpdate == true
                        ? "本次为强制更新，但无法连接服务器。\n请检查网络后重试。"
                        : "版本检测失败，请检查网络后重试。";
                    bool retry = await ShowForceUpdateDialogAsync(msg);
                    if (retry)
                    {
                        // 重试整个更新流程
                        GF.UI.CloseUIForm(m_loadingForm);
                        await RunUpdateFlowAsync(procedureOwner);
                        return;
                    }
                    GameEntry.Shutdown(ShutdownType.Quit);
                    return;
                }

                m_loadingForm?.SetLogState("版本检测失败", 100);
                await Task.Delay(1500);
                await SkipToNextAsync(procedureOwner);
                return;
            }

            isForceUpdate = serverVersion.ForceUpdate;
            Log.Info("[ProcedureUpdate] 服务器版本: {0}, {1} 个子包",
                serverVersion.Version, serverVersion.Packs?.Length ?? 0);

            // ── 2. 版本兼容性检查 ──
            string appVersion = NodeUtility.GetAppVersion();
            if (!string.IsNullOrEmpty(serverVersion.MinAppVersion) && NodeUtility.CompareVersions(appVersion, serverVersion.MinAppVersion) < 0)
            {
                Log.Warning("[ProcedureUpdate] App 版本过低 ({0} < {1})，需要去商店更新。",
                    appVersion, serverVersion.MinAppVersion);
                m_loadingForm?.SetLogState("请更新App版本", 100);
                await Task.Delay(3000);
                await ShowForceUpdateDialogAsync("客户端版本过低，请升级客户端后再试。");
                HotUpdateSafetyGuard.MarkStartupSuccess();
                GameEntry.Shutdown(ShutdownType.Quit);
                return;
            }

            // ── 3. 加载本地版本并校验完整性 ──
            var localVersion = ResourceManager.LocalPackVersionList
                ?? NodeUtility.LoadAndValidateVersionList(ResourceManager.GameFrameworkVersionData);
            m_loadingForm?.SetLogState("校验本地数据...", 5);

            int damagedCount = await VerifyLocalPackIntegrityAsync(localVersion);
            if (damagedCount > 0)
            {
                Log.Warning("[ProcedureUpdate] 本地客户端不完整！{0} 个包已损坏或丢失，将重新下载。", damagedCount);
                m_loadingForm?.SetLogState($"检测到 {damagedCount} 个文件损坏,即将修复", 8);
            }

            // ── 4. 与服务器版本比对 ──
            var toDownload = FindPacksToUpdate(serverVersion, localVersion);

            if (toDownload.Count > 0 && isForceUpdate)
            {
                Log.Info("[ProcedureUpdate] 本次为强制更新。");
            }

            // ── 4. 下载更新的包 ──
            if (toDownload.Count > 0)
            {
                // 磁盘空间预检
                long totalSize = toDownload.Sum(x => x.Pack.Size);
                long freeSpace = NodeUtility.GetFreeDiskSpace(SubpackDir);
                if (freeSpace > 0 && freeSpace < totalSize * 2)
                {
                    Log.Warning("[ProcedureUpdate] 磁盘空间不足: 需要 {0}, 可用 {1}",
                        StringExtension.FormatBytes(totalSize * 2), StringExtension.FormatBytes(freeSpace));
                    m_loadingForm?.SetLogState("磁盘空间不足", 100);

                    if (isForceUpdate)
                    {
                        await Task.Delay(1000);
                        bool retry = await ShowForceUpdateDialogAsync(
                            $"磁盘空间不足（需要 {StringExtension.FormatBytes(totalSize * 2)}），请清理后重试。");
                        if (retry)
                        {
                            GF.UI.CloseUIForm(m_loadingForm);
                            await RunUpdateFlowAsync(procedureOwner);
                            return;
                        }
                        GameEntry.Shutdown(ShutdownType.Quit);
                        return;
                    }

                    await Task.Delay(2000);
                    await SkipToNextAsync(procedureOwner);
                    return;
                }

                Log.Info("[ProcedureUpdate] 共 {0} 个包需要更新，总计 {1}，开始下载...",
                    toDownload.Count, StringExtension.FormatBytes(totalSize));
                int downloaded = await DownloadPacksWithProgressAsync(toDownload);
                Log.Info("[ProcedureUpdate] 下载完成: {0}/{1}", downloaded, toDownload.Count);

                if (downloaded == 0)
                {
                    if (isForceUpdate)
                    {
                        bool retry = await ShowForceUpdateDialogAsync(
                            "更新下载失败，请检查网络后重试。");
                        if (retry)
                        {
                            GF.UI.CloseUIForm(m_loadingForm);
                            await RunUpdateFlowAsync(procedureOwner);
                            return;
                        }
                        GameEntry.Shutdown(ShutdownType.Quit);
                        return;
                    }

                    m_loadingForm?.SetLogState("下载失败，请检查网络", 100);
                    await Task.Delay(2000);
                    await SkipToNextAsync(procedureOwner);
                    return;
                }

                // ── 4. 先加载子包（验证可用） ──
                m_loadingForm?.SetLogState("加载资源...", 95);
                HotUpdateSafetyGuard.MarkStartupBegin();

                await LoadDownloadedPacksAsync(serverVersion);

                // ── 5. 只要发生了下载，就刷新本地数据 ──
                // 不能只在"版本号变化"时保存：若服务端版本号没变但 .pck 哈希变了
                // （如重新导出过包），重启后完整性校验会拿旧哈希比对磁盘新文件 → 判定损坏
                // → 反复重下 + 反复弹"是否重启"，形成死循环。
                if (localVersion != null && EasySave.ExistsInUser(ResourceManager.GameFrameworkVersionData))
                {
                    EasySave.SaveInUser(localVersion,
                        ResourceManager.GameFrameworkVersionData + ".bak");
                }

                await EasySave.SaveInUserAsync(serverVersion, ResourceManager.GameFrameworkVersionData);
                ResourceManager.LocalPackVersionList = serverVersion;
                Log.Info("[ProcedureUpdate] 版本文件已保存。");

                m_loadingForm?.SetLogState("更新完成", 100);
                await Task.Delay(500);

                GF.UI.OpenQuestionTipsAsync("更新完成，是否重启？", "退出", "确认", () =>
                {
                    GameEntry.Shutdown(ShutdownType.Quit);
                }, () =>
                {
                    GameEntry.Shutdown(ShutdownType.Restart);
                });
            }
            else
            {
                await LoadDownloadedPacksAsync(localVersion);
                ChangeState<ProcedurePrelode>(procedureOwner);
                Log.Info("[ProcedureUpdate] 所有包已是最新，无需下载。");
            }
        }
        finally
        {
            HotUpdateSafetyGuard.MarkStartupSuccess();
            GF.UI.CloseUIForm(m_loadingForm);
        }

    }

    private async Task<bool> ShowForceUpdateDialogAsync(string message) // 显示强制更新对话框并等待用户选择。
    {
        // 用一次性任务桥接 UI 回调，保证调用方只能在用户选择后继续。
        var tcs = new TaskCompletionSource<bool>();

        m_loadingForm?.SetLogState(message, 100);

        GF.UI.OpenQuestionTipsAsync(message, "退出", "重试", () =>
        {
            tcs.TrySetResult(false); // 退出
        }, () =>
        {
            tcs.TrySetResult(true);  // 重试
        });

        return await tcs.Task;
    }


    // ── 版本比对 ──

    private async Task<int> VerifyLocalPackIntegrityAsync(PackVersionList localVersion) // 校验本地子包并移除损坏或缺失的清单项。
    {
        // 按存在性、大小和大文件哈希逐级校验，只有完整文件才进入有效列表。
        Log.Info("[ProcedureUpdate] 开始校验本地文件完整性...");
        if (localVersion?.Packs == null || localVersion.Packs.Length == 0)
            return 0;

        var validPacks = new List<Pack>();
        int damaged = 0;

        foreach (var pack in localVersion.Packs)
        {
            if (!pack.IsValid()) continue;

            string packPath = Path.Combine(SubpackDir, pack.Name + ".pck");
            string damageReason = null;

            // 1. 文件存在？
            if (!File.Exists(packPath))
            {
                damageReason = "文件不存在";
            }
            else
            {
                var fileInfo = new FileInfo(packPath);

                // 2. 大小匹配？
                if (fileInfo.Length != pack.Size)
                {
                    damageReason = $"大小不匹配 (期望 {pack.Size}, 实际 {fileInfo.Length})";
                }
                // 3. SHA256 校验（>1MB 文件，线程池执行避免卡帧）
                else if (fileInfo.Length > 1024 * 1024)
                {
                    try
                    {
                        string actualHash = await Task.Run(() => NodeUtility.ComputeSHA256(packPath));
                        if (!string.Equals(actualHash, pack.Hash, StringComparison.OrdinalIgnoreCase))
                        {
                            damageReason = "SHA256 校验失败（文件已损坏或被修改）";
                        }
                    }
                    catch (Exception ex)
                    {
                        damageReason = $"SHA256 计算失败: {ex.Message}";
                    }
                }
            }

            if (damageReason != null)
            {
                Log.Warning("[ProcedureUpdate] 本地文件损坏: {0} — {1}", pack.Name, damageReason);
                if (!EasySave.TryDelete(packPath))
                    Log.Warning("[ProcedureUpdate] 无法删除损坏文件（可能被占用）: {0}", packPath);
                damaged++;
            }
            else
            {
                validPacks.Add(pack);
            }
        }

        // 更新本地版本列表：只保留通过校验的
        localVersion.Packs = validPacks.ToArray();

        if (damaged > 0)
        {
            Log.Warning("[ProcedureUpdate] 本地完整性校验完成: {0} 个损坏/丢失, {1} 个完好",
                damaged, validPacks.Count);
        }

        return damaged;
    }

    private List<(Pack Pack, string Url)> FindPacksToUpdate(
        PackVersionList server, PackVersionList local) // 比对本机与服务器版本并生成待下载包列表。
    {
        var toDownload = new List<(Pack, string)>();

        if (server?.Packs == null || server.Packs.Length == 0)
            return toDownload;

        // 先建立本地包名索引，后续服务器清单按名称做一次比对。
        var localDict = new Dictionary<string, Pack>();
        if (local?.Packs != null)
        {
            foreach (var lp in local.Packs)
            {
                if (!string.IsNullOrEmpty(lp.Name))
                    localDict[lp.Name] = lp;
            }
        }

        // 为每个有效服务端包确定下载地址，再比较本地哈希/大小决定是否下载。
        foreach (var sp in server.Packs)
        {
            if (!sp.IsValid())
            {
                Log.Warning("[ProcedureUpdate] 服务器版本中的包数据无效: {0}", sp.Name ?? "(null)");
                continue;
            }

            string url = !string.IsNullOrEmpty(sp.Url)
                ? sp.Url
                : $"{Utility.Path.GetRemotePath(GF.Resource.UpdateSettingRes?.RemoteUrl)}/{sp.Name}.pck";

            if (!localDict.TryGetValue(sp.Name, out var lp))
            {
                Log.Info("[ProcedureUpdate] 发现新包: {0} ({1} bytes)", sp.Name, sp.Size);
                toDownload.Add((sp, url));
            }
            else if (!string.Equals(lp.Hash, sp.Hash, StringComparison.OrdinalIgnoreCase) || lp.Size != sp.Size)
            {
                Log.Info("[ProcedureUpdate] 包有更新: {0} ({1}→{2} bytes)",
                    sp.Name, lp.Size, sp.Size);
                toDownload.Add((sp, url));
            }
        }

        return toDownload;
    }

    // ── 下载逻辑 ──

    private async Task<int> DownloadPacksWithProgressAsync(
        List<(Pack Pack, string Url)> packs) // 并发下载并校验所有待更新包，汇总进度。
    {
        // 先计算总字节数，再为每个包建立独立进度槽位供主线程聚合。
        long totalBytes = 0;

        foreach (var (pack, _) in packs)
        {
            try
            {
                totalBytes = checked(totalBytes + pack.Size);
            }
            catch (OverflowException)
            {
                Log.Error("[ProcedureUpdate] 包大小累加溢出！已截断。请检查服务器 Pack.Size 配置。");
                totalBytes = long.MaxValue;
                break;
            }
        }

        EnsureDirectory(SubpackDir);

        // 每包一个进度槽位；下载事件回调都在主线程，无需加锁
        long[] perPackBytes = new long[packs.Count];
        int completedCount = 0;

        void ReportAggregateProgress()
        {
            long sum = 0;
            for (int i = 0; i < perPackBytes.Length; i++)
                sum += perPackBytes[i];

            // 进度按字节加权
            int pct = totalBytes > 0
                ? 10 + (int)(80.0 * sum / totalBytes)
                : 10 + (int)(80.0 * completedCount / packs.Count);
            m_loadingForm?.SetLogState(
                $"下载中 {completedCount}/{packs.Count} ({StringExtension.FormatBytes(sum)}/{StringExtension.FormatBytes(totalBytes)})",
                Math.Min(pct, 90));
        }

        async Task<bool> RunPackAsync(int slot, Pack pack, string url, string savePath)
        {
            bool ok = await DownloadSinglePackWithRetryAsync(pack, url, savePath, bytes =>
            {
                perPackBytes[slot] = bytes;
                ReportAggregateProgress();
            });

            perPackBytes[slot] = ok ? pack.Size : 0;
            completedCount++;
            ReportAggregateProgress();
            return ok;
        }

        m_loadingForm?.SetLogState($"下载中 0/{packs.Count}", 10);

        var tasks = new List<Task<bool>>(packs.Count);
        for (int i = 0; i < packs.Count; i++)
        {
            var (pack, url) = packs[i];
            string savePath = Path.Combine(SubpackDir, pack.Name + ".pck");
            tasks.Add(RunPackAsync(i, pack, url, savePath));
        }

        bool[] results = await Task.WhenAll(tasks);

        int downloaded = 0;
        for (int i = 0; i < results.Length; i++)
        {
            if (results[i])
            {
                downloaded++;
                Log.Info("[ProcedureUpdate] 下载+校验成功: {0}", packs[i].Pack.Name);
            }
            else
            {
                Log.Error("[ProcedureUpdate] 下载失败（已重试 {0} 次）: {1}", MaxRetries, packs[i].Pack.Name);
                // 不跳过后续包——一个失败不影响其他包
            }
        }

        return downloaded;
    }

    private async Task<bool> DownloadSinglePackWithRetryAsync(
        Pack pack, string url, string savePath, Action<long> onPackBytes) // 下载单个包并在失败时按策略重试。
    {
        // 每次重试都复用下载组件的断点续传和哈希校验。
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                // 使用指数退避降低连续失败时的请求压力。
                float delay = RetryBaseDelaySeconds * (1 << (attempt - 1));
                Log.Info("[ProcedureUpdate] 重试 {0}/{1}: {2}（{3:F1}s 后）",
                    attempt + 1, MaxRetries, pack.Name, delay);
                await Task.Delay(TimeSpan.FromSeconds(delay));
            }

            try
            {
                // DownloadFileAsync 内部处理断点续传；返回 true 前已校验预期大小与哈希。
                bool ok = await GF.Download.DownloadFileAsync(
                    downloadUri: url,
                    downloadPath: savePath,
                    expectedSize: pack.Size,
                    expectedHash: pack.Hash,
                    onProgress: (downloaded, _) => onPackBytes(downloaded));

                if (ok)
                {
                    Log.Info("[ProcedureUpdate] 下载+校验成功: {0},路径:{1}", pack.Name, savePath);
                    return true;
                }

                Log.Warning("[ProcedureUpdate] 下载失败 (attempt {0}/{1}): {2}",
                    attempt + 1, MaxRetries, pack.Name);
            }
            catch (Exception ex)
            {
                Log.Error("[ProcedureUpdate] 下载异常: {0} — {1}", pack.Name, ex.Message);
            }
        }

        return false;
    }



    // ── 版本文件请求 ──

    private async Task<PackVersionList> FetchVersionWithRetryAsync(string versionUrl) // 请求并校验服务器版本清单，失败时退避重试。
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                // 服务器请求失败后逐轮延长等待时间。
                float delay = RetryBaseDelaySeconds * (1 << (attempt - 1));
                await Task.Delay(TimeSpan.FromSeconds(delay));
            }

            try
            {
                // 先校验 HTTP 状态，再解码正文，避免把错误页当成版本数据。
                var result = await GF.WebRequest.SendRequestAsync(versionUrl, VersionFetchTimeoutSeconds);
                if (!IsHttpSuccess(result))
                {
                    m_loadingForm.SetLogState($"版本文件请求失败(再次尝试:{attempt + 1}/{MaxRetries})", 0);
                    Log.Warning("[ProcedureUpdate] 版本文件请求失败 (attempt {0}/{1}, HTTP {2})",
                        attempt + 1, MaxRetries, result?.ResponseCode);
                    continue;
                }

                // JSON 反序列化成功后继续验证清单结构，结构无效时进入下一次重试。
                string json = Encoding.UTF8.GetString(result.Body);
                var version = Utility.Json.ToObject<PackVersionList>(json);
                if (version != null)
                {
                    // 校验服务器版本清单完整性
                    if (!version.Validate(out string validateError))
                    {
                        Log.Warning("[ProcedureUpdate] 服务器版本数据校验失败: {0} (attempt {1}/{2})",
                            validateError, attempt + 1, MaxRetries);
                        continue;
                    }
                    return version;
                }

                Log.Warning("[ProcedureUpdate] 版本 JSON 解析为 null (attempt {0}/{1})",
                    attempt + 1, MaxRetries);
            }
            catch (Exception ex)
            {
                Log.Error("[ProcedureUpdate] 版本文件请求异常: {0}", ex.Message);
            }
        }

        return null;
    }

    // ── 子包加载 ──

    private async Task LoadDownloadedPacksAsync(PackVersionList version) // 校验并按 Config 到 Resource 顺序加载已下载子包。
    {
        // 先按资源类型排序，保证配置和本地化数据在场景资源之前可用。
        if (version?.Packs == null || version.Packs.Length == 0)
            return;

        // 先 Config 后 Resource，确保场景加载时配置已就绪
        var ordered = version.Packs
            .OrderBy(p => p.Type == PackType.Config ? 0 : 1)
            .ToArray();

        int loaded = 0;
        int failed = 0;

        foreach (var pack in ordered)
        {
            if (!pack.IsValid()) continue;

            string packPath = Path.Combine(SubpackDir, pack.Name + ".pck");
            if (!File.Exists(packPath))
            {
                Log.Warning("[ProcedureUpdate] 子包不存在，跳过: {0}", packPath);
                failed++;
                continue;
            }

            // 大小校验
            var fileInfo = new FileInfo(packPath);
            if (fileInfo.Length != pack.Size)
            {
                Log.Warning("[ProcedureUpdate] 子包大小不匹配({0})，可能已损坏，跳过: {1}",
                    pack.Name, fileInfo.Length);
                if (!EasySave.TryDelete(packPath))
                    Log.Warning("[ProcedureUpdate] 无法删除损坏文件（可能被占用）: {0}", packPath);
                failed++;
                continue;
            }

            // 对大文件做 SHA256 重校验（线程池执行，防御磁盘静默损坏）
            if (fileInfo.Length > 1024 * 1024)
            {
                try
                {
                    string actualHash = await Task.Run(() => NodeUtility.ComputeSHA256(packPath));
                    if (!string.Equals(actualHash, pack.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Warning("[ProcedureUpdate] SHA256 重校验失败，文件可能损坏: {0}", pack.Name);
                        if (!EasySave.TryDelete(packPath))
                            Log.Warning("[ProcedureUpdate] 无法删除损坏文件（可能被占用）: {0}", packPath);
                        failed++;
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("[ProcedureUpdate] SHA256 计算失败: {0} — {1}", pack.Name, ex.Message);
                    failed++;
                    continue;
                }
            }

            // 加载
            if (ProjectSettings.LoadResourcePack(packPath))
            {
                loaded++;
                Log.Info("[ProcedureUpdate] 子包加载成功: {0} ({1})", pack.Name, pack.Type);
            }
            else
            {
                Log.Warning("[ProcedureUpdate] 子包加载失败: {0}", packPath);
                failed++;
            }
        }

        // 清理不属于当前版本的废弃 .pck
        CleanStalePacks(version);

        // 如果有包加载失败，回退版本文件
        if (failed > 0)
        {
            Log.Warning("[ProcedureUpdate] {0} 个子包加载失败，回退版本文件。", failed);
            RollbackVersionFile();
        }

        Log.Info("[ProcedureUpdate] 子包加载完成: {0}/{1} (失败: {2})",
            loaded, version.Packs.Length, failed);
    }

    private void CleanStalePacks(PackVersionList version) // 删除热更目录中不属于当前清单的旧子包。
    {
        // 以当前清单建立白名单，只清理目录中的过期 .pck 文件。
        if (!Directory.Exists(SubpackDir)) return;

        var validNames = new HashSet<string>(
            version.Packs.Where(p => p.IsValid()).Select(p => p.Name + ".pck"));

        try
        {
            foreach (string file in Directory.GetFiles(SubpackDir, "*.pck"))
            {
                string fileName = Path.GetFileName(file);
                if (!validNames.Contains(fileName))
                {
                    Log.Info("[ProcedureUpdate] 清理废弃子包: {0}", fileName);
                    if (!EasySave.TryDelete(file))
                        Log.Warning("[ProcedureUpdate] 无法删除废弃包（可能被占用）: {0}", file);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning("[ProcedureUpdate] 清理废弃包异常: {0}", ex.Message);
        }
    }

    private void RollbackVersionFile() // 使用版本备份文件回退当前版本清单。
    {
        try
        {
            string versionPath = Path.Combine(
                ProjectSettings.GlobalizePath("user://"), ResourceManager.GameFrameworkVersionData);
            string backupPath = versionPath + ".bak";

            if (File.Exists(backupPath))
            {
                EasySave.TryDelete(versionPath);
                File.Move(backupPath, versionPath);
                Log.Info("[ProcedureUpdate] 已自动回退到上一版本。");
            }
        }
        catch (Exception ex)
        {
            Log.Error("[ProcedureUpdate] 版本回退失败: {0}", ex.Message);
        }
    }

    // ── 工具方法 ──

    private async Task TryLoadLocalSubpackagesAsync() // 在 Package 模式加载安装目录中的本地子包清单。
    {
        // 缺少本地清单是正常首次启动路径，直接跳过本地子包加载。
        string manifestPath = Path.Combine(SubpackDir, ResourceManager.GameFrameworkVersionData);
        if (!File.Exists(manifestPath))
        {
            Log.Info("[ProcedureUpdate] 本地子包清单不存在，跳过: {0}", manifestPath);
            return;
        }

        Log.Info("[ProcedureUpdate] 检测到本地子包清单，尝试加载: {0}", manifestPath);

        try
        {
            // 读取并反序列化清单后先验证，再登记版本并按资源依赖顺序加载子包。
            string json = File.ReadAllText(manifestPath, Encoding.UTF8);
            var localVersion = Utility.Json.ToObject<PackVersionList>(json);

            if (localVersion == null)
            {
                Log.Warning("[ProcedureUpdate] 本地子包清单解析为 null");
                return;
            }
            if (!localVersion.Validate(out string validateError))
            {
                Log.Warning("[ProcedureUpdate] 本地子包清单校验失败: {0}", validateError);
                return;
            }

            ResourceManager.LocalPackVersionList = localVersion;
            await LoadDownloadedPacksAsync(localVersion);
        }
        catch (Exception ex)
        {
            Log.Warning("[ProcedureUpdate] 加载本地子包异常: {0}", ex.Message);
        }
    }

    private bool IsHttpSuccess(WebRequestCompleteEventArgs result) // 判断版本请求是否返回可解析的成功响应。
    {
        if (result == null) return false;
        if (result.Result == -1 && result.ResponseCode == 0) return false; // timeout
        if (result.ResponseCode != 200 || result.Result != (long)Error.Ok) return false;
        if (result.Body == null || result.Body.Length == 0) return false;
        return true;
    }

    private void EnsureDirectory(string path) // 确保目标目录存在。
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    private async Task SkipToNextAsync(ProcedureOwner procedureOwner) // 加载可用本地子包后进入预加载流程。
    {
        // 尝试加载已存在的本地版本（优先使用缓存的统一版本，带完整性校验）
        var local = ResourceManager.LocalPackVersionList
            ?? NodeUtility.LoadAndValidateVersionList(ResourceManager.GameFrameworkVersionData);
        if (local != null)
        {
            ResourceManager.LocalPackVersionList = local;
            await LoadDownloadedPacksAsync(local);
        }

        ChangeState<ProcedurePrelode>(procedureOwner);
    }

    /// <summary>离开更新流程并执行基类清理。</summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    /// <param name="isShutdown">是否因框架关闭而离开。</param>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);
    }
}
