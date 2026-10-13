using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event.Navigation;

/// <summary>手动检查点完成后的窗口反馈。参数由 GGF 引用池管理。</summary>
public sealed class ProfileSavedEventArgs : GameEventArgs
{
    /// <summary>本事件的框架编号。</summary>
    public static readonly int EventId = typeof(ProfileSavedEventArgs).GetHashCode();

    /// <inheritdoc />
    public override int Id => EventId;

    /// <summary>请求所属窗口的打开编号，拒绝旧周期事件。</summary>
    public int FormSerialId { get; private set; }

    /// <summary>本次写入是否成功。</summary>
    public bool Succeeded { get; private set; }

    /// <summary>建立一次仅在分发期间有效的事件。</summary>
    /// <param name="formSerialId">窗口打开编号。</param>
    /// <param name="succeeded">本次写入是否成功。</param>
    /// <returns>池化事件参数。</returns>
    public static ProfileSavedEventArgs Create(int formSerialId, bool succeeded)
    {
        var args = ReferencePool.Acquire<ProfileSavedEventArgs>();
        args.FormSerialId = formSerialId;
        args.Succeeded = succeeded;
        return args;
    }

    /// <summary>回池前释放本次事件数据。</summary>
    public override void Clear()
    {
        FormSerialId = 0;
        Succeeded = default;
    }
}
