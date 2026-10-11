using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event.Navigation;

/// <summary>地图中的手动保存请求。参数由 GGF 引用池管理。</summary>
public sealed class SaveRequestedEventArgs : GameEventArgs
{
    /// <summary>本事件的框架编号。</summary>
    public static readonly int EventId = typeof(SaveRequestedEventArgs).GetHashCode();
    /// <inheritdoc />
    public override int Id => EventId;
    /// <summary>请求所属窗口的打开编号，拒绝旧周期事件。</summary>
    public int FormSerialId { get; private set; }

    /// <summary>建立一次仅在分发期间有效的事件。</summary>
    /// <param name="formSerialId">窗口打开编号。</param>
    /// <returns>池化事件参数。</returns>
    public static SaveRequestedEventArgs Create(int formSerialId)
    {
        var args = ReferencePool.Acquire<SaveRequestedEventArgs>();
        args.FormSerialId = formSerialId;
        return args;
    }

    /// <summary>回池前释放本次事件数据。</summary>
    public override void Clear()
    {
        FormSerialId = 0;
    }
}
