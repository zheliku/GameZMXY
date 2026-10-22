using Godot;
using GameLogic.Heroes;

namespace GameLogic.UI.Widgets;

/// <summary>独立于战斗实体的角色预览，共用身体、武器图集与外置表现动画。</summary>
public partial class HeroPreviewNode : Node2D
{
    [Export] private AnimationPlayer m_Player; // 只包含表现轨道，不含碰撞或战斗调用。
    [Export] private Sprite2D m_Body; // 与战斗角色相同网格的身体层。
    [Export] private Sprite2D m_Weapon; // 独立武器层，遮挡部分已在素材中裁成透明。
    [Export] private HeroAppearanceResource m_Appearance; // 与战斗角色共用的外观资源。
    private ulong m_LastTick; // 真实时间原点，不依赖 GGF 暂停后的零 delta。

    /// <summary>同步当前穿戴状态，保持预览动画继续播放。</summary>
    /// <param name="weaponId">当前武器身份。</param>
    /// <param name="armorId">当前防具身份。</param>
    public void ApplyAppearance(GameConfig.Equipment.EquipmentId weaponId, GameConfig.Equipment.EquipmentId armorId) =>
        m_Appearance.ApplyToSprites(m_Body, m_Weapon, weaponId, armorId);

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
