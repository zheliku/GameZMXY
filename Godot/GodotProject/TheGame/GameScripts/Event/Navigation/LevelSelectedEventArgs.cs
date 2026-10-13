using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event.Navigation;

/// <summary>进入所选关卡的点击事件；由当前流程消费，分发后归还引用池。</summary>
public sealed class LevelSelectedEventArgs : GameEventArgs
{
    /// <summary>本事件的框架编号。</summary>
    public static readonly int EventId = typeof(LevelSelectedEventArgs).GetHashCode();

    /// <inheritdoc />
    public override int Id => EventId;

    /// <summary>发出请求的窗口打开编号，防止旧窗口的排队事件影响新周期。</summary>
    public int FormSerialId { get; private set; }

    /// <summary>所选关卡的配置主键。</summary>
    public int LevelId { get; private set; }

    /// <summary>从引用池创建一次进入所选关卡请求。</summary>
    /// <param name="formSerialId">发出请求的窗口打开编号。</param>
    /// <param name="levelId">所选关卡配置主键。</param>
    /// <returns>仅在本次事件分发期间有效的参数。</returns>
    public static LevelSelectedEventArgs Create(int formSerialId, int levelId)
    {
        var args = ReferencePool.Acquire<LevelSelectedEventArgs>();
        args.FormSerialId = formSerialId;
        args.LevelId = levelId;
        return args;
    }

    /// <summary>归还引用池前清除本次请求。</summary>
    public override void Clear()
    {
        FormSerialId = 0;
        LevelId = 0;
    }
}
