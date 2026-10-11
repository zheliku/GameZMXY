using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event.Navigation;

/// <summary>选中存档格后请求读取该槽。参数由 GGF 引用池管理。</summary>
public sealed class SaveSlotSelectedEventArgs : GameEventArgs
{
    /// <summary>本事件的框架编号。</summary>
    public static readonly int EventId = typeof(SaveSlotSelectedEventArgs).GetHashCode();
    /// <inheritdoc />
    public override int Id => EventId;
    /// <summary>请求所属窗口的打开编号，拒绝旧周期事件。</summary>
    public int FormSerialId { get; private set; }
    /// <summary>从 1 开始的存档格号。</summary>
    public int SlotNumber { get; private set; }

    /// <summary>建立一次仅在分发期间有效的事件。</summary>
    /// <param name="formSerialId">窗口打开编号。</param>
    /// <param name="slotNumber">从 1 开始的存档格号。</param>
    /// <returns>池化事件参数。</returns>
    public static SaveSlotSelectedEventArgs Create(int formSerialId, int slotNumber)
    {
        var args = ReferencePool.Acquire<SaveSlotSelectedEventArgs>();
        args.FormSerialId = formSerialId;
        args.SlotNumber = slotNumber;
        return args;
    }

    /// <summary>回池前释放本次事件数据。</summary>
    public override void Clear()
    {
        FormSerialId = 0;
        SlotNumber = default;
    }
}
