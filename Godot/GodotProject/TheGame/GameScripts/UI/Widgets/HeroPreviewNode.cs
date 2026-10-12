using Godot;

namespace GameLogic.UI.Widgets;

/// <summary>独立于战斗实体的角色预览，只播放外置的身体和武器动画。</summary>
public partial class HeroPreviewNode : Node2D
{
    [Export] private AnimationPlayer m_Player; // 只包含表现轨道，不含碰撞或战斗调用。
    private ulong m_LastTick; // 真实时间原点，不依赖 GGF 暂停后的零 delta。

    /// <summary>每次打开从待机第一帧开始。</summary>
    public void Reset()
    {
        m_Player.Stop();
        m_Player.Play("idle");
        m_Player.Advance(0);
        m_LastTick = Time.GetTicksUsec();
    }

    /// <summary>由窗口驱动一次真实时间采样，暂停战斗时预览仍可播放。</summary>
    public void Advance()
    {
        ulong now = Time.GetTicksUsec();
        m_Player.Advance((now - m_LastTick) / 1_000_000d);
        m_LastTick = now;
    }
}
