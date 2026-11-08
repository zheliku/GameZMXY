using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLogic.Save.Slots;

/// <summary>
/// 存档页的只读显示信息，读取文件后构建，不作为存档数据写盘。
/// </summary>
public sealed class SaveSlotInfo
{
    /// <summary>
    /// 建立一个存档格的不可变显示信息。
    /// </summary>
    /// <param name="slotNumber">从 1 开始的格号。</param>
    /// <param name="unitId">框架存档编号，尚无文件时为 0。</param>
    /// <param name="state">内容状态。</param>
    /// <param name="heroes">按席位排列的参战者摘要；空格或不可读格省略。</param>
    /// <param name="savedAt">检查点时间。</param>
    public SaveSlotInfo(int slotNumber, long unitId, SaveSlotState state, IEnumerable<SaveHeroSummary> heroes = null,
        DateTimeOffset? savedAt = null)
    {
        SlotNumber = slotNumber;
        UnitId = unitId;
        State = state;
        Heroes = Array.AsReadOnly(heroes?.ToArray() ?? []);
        SavedAt = savedAt;
    }

    /// <summary>
    /// 界面格号，从 1 开始。
    /// </summary>
    public int SlotNumber { get; }

    /// <summary>
    /// 框架存档编号；0 表示该格尚无文件。
    /// </summary>
    public long UnitId { get; }

    /// <summary>
    /// 内容是否为空、可读或损坏。
    /// </summary>
    public SaveSlotState State { get; }

    /// <summary>
    /// 按玩家席位排列的参战者摘要；构造时复制，发布后不可修改。
    /// </summary>
    public IReadOnlyList<SaveHeroSummary> Heroes { get; }

    /// <summary>
    /// 最近检查点的 UTC 时间。
    /// </summary>
    public DateTimeOffset? SavedAt { get; }
}

/// <summary>
/// 一名参战者的存档显示摘要，不保存运行时绑定或临时战斗状态。
/// </summary>
/// <param name="Name">配置中的角色名称。</param>
/// <param name="Level">由累计经验计算的等级。</param>
public readonly record struct SaveHeroSummary(string Name, int Level);

/// <summary>
/// 存档格的实际内容；不可读的存档不能被当成空格覆盖。
/// </summary>
public enum SaveSlotState
{
    /// <summary>
    /// 尚未创建角色。
    /// </summary>
    Empty,

    /// <summary>
    /// 已有可读取的档案。
    /// </summary>
    Occupied,

    /// <summary>
    /// 文件损坏或版本不支持。
    /// </summary>
    Unreadable,
}
