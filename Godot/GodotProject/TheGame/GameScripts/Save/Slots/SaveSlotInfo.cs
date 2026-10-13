using System;

namespace GameLogic.Save.Slots;

/// <summary>存档页的只读显示信息，读取文件后构建，不作为存档数据写盘。</summary>
public sealed class SaveSlotInfo
{
    /// <summary>界面格号，从 1 开始。</summary>
    public int SlotNumber { get; }

    /// <summary>框架存档编号；0 表示该格尚无文件。</summary>
    public long UnitId { get; }

    /// <summary>内容是否为空、可读或损坏。</summary>
    public SaveSlotState State { get; }

    /// <summary>出战角色名称；空格不显示文字。</summary>
    public string HeroName { get; }

    /// <summary>从档案累计经验计算的角色等级，仅用于显示。</summary>
    public int Level { get; }

    /// <summary>最近检查点的 UTC 时间。</summary>
    public DateTimeOffset? SavedAt { get; }

    /// <summary>建立一个存档格的不可变显示信息。</summary>
    /// <param name="slotNumber">从 1 开始的格号。</param>
    /// <param name="unitId">框架存档编号，尚无文件时为 0。</param>
    /// <param name="state">内容状态。</param>
    /// <param name="heroName">角色名称。</param>
    /// <param name="level">角色等级。</param>
    /// <param name="savedAt">检查点时间。</param>
    public SaveSlotInfo(int slotNumber, long unitId, SaveSlotState state, string heroName = "", int level = 0,
        DateTimeOffset? savedAt = null)
    {
        SlotNumber = slotNumber;
        UnitId = unitId;
        State = state;
        HeroName = heroName;
        Level = level;
        SavedAt = savedAt;
    }
}

/// <summary>存档格的实际内容；不可读的存档不能被当成空格覆盖。</summary>
public enum SaveSlotState
{
    /// <summary>尚未创建角色。</summary>
    Empty,

    /// <summary>已有可读取的档案。</summary>
    Occupied,

    /// <summary>文件损坏或版本不支持。</summary>
    Unreadable,
}
