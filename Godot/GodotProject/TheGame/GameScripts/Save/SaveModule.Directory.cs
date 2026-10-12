using System;
using System.Threading;
using System.Threading.Tasks;
using GameLogic.Save.Slots;
using GodotGameFramework;

namespace GameLogic.Save;

/// <summary>同一存档模块的目录读取部分；辅助状态对象只承担显示数据。</summary>
public sealed partial class SaveModule
{
    /// <summary>读取六格摘要并发布完整状态；取消后不再发布旧周期结果。</summary>
    /// <param name="cancellationToken">所属标题周期的取消令牌。</param>
    /// <returns>读取任务；文件错误转换为目录失败提示。</returns>
    public async Task RefreshDirectoryAsync(CancellationToken cancellationToken)
    {
        if (Directory.Phase == SaveDirectoryPhase.Loading) return;
        Directory.Update(SaveDirectoryPhase.Loading);
        try
        {
            var slots = await ReadSlotsAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Update(SaveDirectoryPhase.Ready, slots);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 离开标题会丢弃本周期模块，不再向已关闭窗口发布旧结果。
        }
        catch (Exception error)
        {
            Log.Error("[SaveModule] 目录读取失败：{0}", error);
            Directory.Update(SaveDirectoryPhase.Failed, errorMessage: "存档目录读取失败，请关闭后重试。");
        }
    }
}
