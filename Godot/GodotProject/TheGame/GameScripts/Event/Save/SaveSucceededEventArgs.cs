using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event.Save;

/// <summary>档案检查点已成功写盘；应用级保存提示消费，不绑定某个地图窗口。</summary>
public sealed class SaveSucceededEventArgs : GameEventArgs
{
    /// <summary>GGF 事件编号。</summary>
    public static readonly int EventId = typeof(SaveSucceededEventArgs).GetHashCode();
    /// <inheritdoc />
    public override int Id => EventId;
    /// <summary>取得一次保存成功通知，由框架在分发后回收。</summary>
    /// <returns>本次分发的池化参数。</returns>
    public static SaveSucceededEventArgs Create() => ReferencePool.Acquire<SaveSucceededEventArgs>();
    /// <summary>本事件没有需跨次清除的数据。</summary>
    public override void Clear() { }
}
