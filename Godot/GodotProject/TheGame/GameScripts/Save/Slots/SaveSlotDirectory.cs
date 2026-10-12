using System;
using System.Collections.Generic;

namespace GameLogic.Save.Slots;

/// <summary>存档模块拥有的目录状态源；窗口只订阅变更并显示，不承担读盘任务。</summary>
public sealed class SaveSlotDirectory
{
    /// <summary>造梦西游 2 风格的固定六格界面规格。</summary>
    public const int SlotCount = 6;

    /// <summary>当前读取阶段。</summary>
    public SaveDirectoryPhase Phase { get; private set; }
    /// <summary>最近一次完整的目录显示信息。</summary>
    public IReadOnlyList<SaveSlotInfo> Slots { get; private set; } = Array.Empty<SaveSlotInfo>();
    /// <summary>读取失败时的用户提示。</summary>
    public string ErrorMessage { get; private set; } = "";
    /// <summary>阶段与显示数据完整更新后通知一次。</summary>
    public event Action Changed;

    /// <summary>由存档模块创建空目录。</summary>
    internal SaveSlotDirectory() { }

    /// <summary>由模块一次提交完整阶段和显示数据，再通知观察者。</summary>
    /// <param name="phase">目录读取阶段。</param>
    /// <param name="slots">完整格子摘要；省略时保留上次结果。</param>
    /// <param name="errorMessage">本次读取失败提示。</param>
    internal void Update(SaveDirectoryPhase phase, IReadOnlyList<SaveSlotInfo> slots = null, string errorMessage = "")
    {
        Phase = phase;
        if (slots != null) Slots = slots;
        ErrorMessage = errorMessage;
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
