using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GameLogic.Save.Slots;

/// <summary>标题流程拥有的存档目录状态源；窗口只订阅变更并显示。</summary>
public sealed class SaveSlotDirectory
{
    /// <summary>造梦西游 2 风格的固定六格界面规格。</summary>
    public const int SlotCount = 6;
    private readonly SaveService m_Save; // 目录读取仍通过唯一存档入口。

    /// <summary>当前读取阶段。</summary>
    public SaveDirectoryPhase Phase { get; private set; }
    /// <summary>最近一次完整的目录显示信息。</summary>
    public IReadOnlyList<SaveSlotInfo> Slots { get; private set; } = Array.Empty<SaveSlotInfo>();
    /// <summary>读取失败时的用户提示。</summary>
    public string ErrorMessage { get; private set; } = "";
    /// <summary>阶段与显示数据完整更新后通知一次。</summary>
    public event Action Changed;

    /// <summary>绑定标题流程创建的存档入口。</summary>
    /// <param name="save">存档入口。</param>
    /// <exception cref="ArgumentNullException">存档入口为空。</exception>
    public SaveSlotDirectory(SaveService save) => m_Save = save ?? throw new ArgumentNullException(nameof(save));

    /// <summary>异步刷新目录，离开标题后不再发布旧结果。</summary>
    /// <param name="cancellationToken">所属标题周期的取消令牌。</param>
    /// <returns>目录刷新任务；读取错误转换为可见的失败状态。</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (Phase == SaveDirectoryPhase.Loading) return;
        Phase = SaveDirectoryPhase.Loading;
        ErrorMessage = "";
        Changed?.Invoke();
        try
        {
            var slots = await m_Save.ReadSlotsAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Slots = slots;
            Phase = SaveDirectoryPhase.Ready;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
        catch (Exception error)
        {
            Phase = SaveDirectoryPhase.Failed;
            ErrorMessage = "存档目录读取失败，请关闭后重试。";
            GodotGameFramework.Log.Error("[SaveSlotDirectory] {0}", error);
        }
        Changed?.Invoke();
    }
}

/// <summary>存档选择页读取目录的互斥阶段。</summary>
public enum SaveDirectoryPhase
{
    /// <summary>尚未请求读取。</summary>
    NotLoaded,
    /// <summary>正在读取，禁止选择。</summary>
    Loading,
    /// <summary>六个存档格已经准备好。</summary>
    Ready,
    /// <summary>目录读取失败，保留文件并允许重新打开重试。</summary>
    Failed,
}
