using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event.Navigation;

/// <summary>开始读档的点击事件；由当前流程消费，分发后归还引用池。</summary>
public sealed class StartGameRequestedEventArgs : GameEventArgs
{
    /// <summary>本事件的框架编号。</summary>
    public static readonly int EventId = typeof(StartGameRequestedEventArgs).GetHashCode();

    /// <inheritdoc />
    public override int Id => EventId;

    /// <summary>发出请求的窗口打开编号，防止旧窗口的排队事件影响新周期。</summary>
    public int FormSerialId { get; private set; }

    /// <summary>从引用池创建一次开始读档请求。</summary>
    /// <param name="formSerialId">发出请求的窗口打开编号。</param>
    /// <returns>仅在本次事件分发期间有效的参数。</returns>
    public static StartGameRequestedEventArgs Create(int formSerialId)
    {
        var args = ReferencePool.Acquire<StartGameRequestedEventArgs>();
        args.FormSerialId = formSerialId;
        return args;
    }

    /// <summary>归还引用池前清除本次请求。</summary>
    public override void Clear()
    {
        FormSerialId = 0;
    }
}
