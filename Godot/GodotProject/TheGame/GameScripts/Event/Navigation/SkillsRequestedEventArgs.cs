using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event.Navigation;

/// <summary>请求打开本关技能面板；由当前关卡流程消费。</summary>
public sealed class SkillsRequestedEventArgs : GameEventArgs
{
    /// <summary>本事件的框架编号。</summary>
    public static readonly int EventId = typeof(SkillsRequestedEventArgs).GetHashCode();
    /// <inheritdoc />
    public override int Id => EventId;
    /// <summary>发出请求的窗口编号，防止旧周期排队事件影响新窗口。</summary>
    public int FormSerialId { get; private set; }

    /// <summary>从引用池创建本次请求。</summary>
    /// <param name="formSerialId">发出请求的 HUD 编号。</param>
    /// <returns>仅在本次事件分发期间有效的参数。</returns>
    public static SkillsRequestedEventArgs Create(int formSerialId)
    {
        var args = ReferencePool.Acquire<SkillsRequestedEventArgs>();
        args.FormSerialId = formSerialId;
        return args;
    }

    /// <summary>归还引用池时清除打开编号。</summary>
    public override void Clear() => FormSerialId = 0;
}
