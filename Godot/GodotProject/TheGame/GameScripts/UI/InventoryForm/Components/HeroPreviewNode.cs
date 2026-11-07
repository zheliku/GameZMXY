using GameLogic.Heroes;
using Godot;

namespace GameLogic.UI.Widgets;

/// <summary>
/// 独立于战斗实体的官方背包预览，使用专用身体、武器图集与外置表现动画。
/// </summary>
public partial class HeroPreviewNode : Node2D
{
    private static readonly StringName s_idleAnimation = "idle"; // 预览资源自己的待机轨道，不与战斗动画库混用。
    [Export] private AnimationPlayer _player; // 只包含表现轨道，不含碰撞或战斗调用。
    [Export] private Sprite2D _body; // 官方背包姿势的独立身体层。
    [Export] private Sprite2D _weapon; // 独立武器层，遮挡部分已在素材中裁成透明。
    private HeroAppearanceResource _appearance; // 每次打开借用当前英雄的背包换装资源。
    private ulong _lastTick; // 真实时间原点，不依赖 GGF 暂停后的零 delta。

    /// <summary>
    /// 同步当前穿戴状态，保持预览动画继续播放。
    /// </summary>
    /// <param name="weaponId">当前武器身份。</param>
    /// <param name="armorId">当前防具身份。</param>
    public void ApplyAppearance(GameConfig.Equipment.EquipmentId weaponId, GameConfig.Equipment.EquipmentId armorId)
    {
        _appearance.ApplyToSprites(_body, _weapon, weaponId, armorId);
        _player.Advance(0);
    }

    /// <summary>
    /// 每次打开绑定当前英雄的预览动画，从待机第一帧开始。
    /// </summary>
    /// <param name="appearance">当前英雄拥有的换装与预览资源。</param>
    public void Reset(HeroAppearanceResource appearance)
    {
        _player.Stop();
        _appearance = appearance.PreviewAppearance;
        _player.RemoveAnimationLibrary("");
        _player.AddAnimationLibrary("", appearance.PreviewAnimations);
        _player.Play(s_idleAnimation);
        _lastTick = Time.GetTicksUsec();
    }

    /// <summary>
    /// 由窗口驱动一次真实时间采样，暂停战斗时预览仍可播放。
    /// </summary>
    public void Advance()
    {
        ulong now = Time.GetTicksUsec();
        _player.Advance((now - _lastTick) / 1_000_000d);
        _lastTick = now;
    }
}
