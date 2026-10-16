using System;
using System.Collections.Generic;
using GameLogic.Architecture;
using GameLogic.Save.Slots;

namespace GameLogic.Save;

/// <summary>应用拥有的存档状态：当前文件编号、六格摘要及读取状态；读写由 SaveSystem 执行。</summary>
public sealed class SaveModel : Architecture.AbstractModel
{
    /// <summary>造梦西游 2 风格的固定六格界面规格。</summary>
    public const int SlotCount = 6;

    /// <summary>可绑定的目录快照，不包含存档文件本身。</summary>
    public BindableProperty<SaveDirectoryState> Directory { get; } =
        new(new(SaveListState.NotLoaded, Array.Empty<SaveSlotInfo>(), ""));

    /// <summary>地图手动保存状态，包含发起窗口身份，重开窗口不会接受旧结果。</summary>
    public BindableProperty<ManualSaveState> ManualSave { get; } = new();

    /// <summary>当前档案所属文件编号，零表示尚未选定；仅存档系统在成功读档后修改。</summary>
    public long ActiveSlotUnitId { get; internal set; }

    /// <summary>当前读取阶段。</summary>
    public SaveListState Phase => Directory.Value.Phase;

    /// <summary>最近一次完整的目录显示信息。</summary>
    public IReadOnlyList<SaveSlotInfo> Slots => Directory.Value.Slots;

    /// <summary>读取失败时的用户提示。</summary>
    public string ErrorMessage => Directory.Value.ErrorMessage;
}

/// <summary>地图保存请求的完整状态。</summary>
/// <param name="FormSerialId">发起窗口编号。</param>
/// <param name="Phase">写盘阶段。</param>
public readonly record struct ManualSaveState(int FormSerialId, SaveWritePhase Phase);

/// <summary>地图手动保存按钮的显示阶段。</summary>
public enum SaveWritePhase
{
    /// <summary>尚未发起。</summary>
    Idle,

    /// <summary>正在写入。</summary>
    Writing,

    /// <summary>写入成功。</summary>
    Succeeded,

    /// <summary>写入失败，允许重试。</summary>
    Failed
}

/// <summary>存档目录的一次完整读取结果；三个字段共同参与显示。</summary>
/// <param name="Phase">读取阶段。</param>
/// <param name="Slots">完整的只读六格摘要。</param>
/// <param name="ErrorMessage">失败提示或空串。</param>
public readonly record struct SaveDirectoryState(SaveListState Phase, IReadOnlyList<SaveSlotInfo> Slots,
    string ErrorMessage);

/// <summary>存档选择页读取目录的互斥阶段。</summary>
public enum SaveListState
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

/// <summary>一次加载操作的来源，用于决定是否进入选角。</summary>
public enum SaveOrigin
{
    /// <summary>读取已有档案。</summary>
    Loaded,

    /// <summary>新建档案。</summary>
    Created
}
