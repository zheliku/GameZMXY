using Godot;
using GameLogic.Heroes;

namespace GameLogic.UI.Widgets
{
    /// <summary>
    /// 独立于战斗实体的角色预览，共用身体、武器图集与外置表现动画。
    /// </summary>
    public partial class HeroPreviewNode : Node2D
    {
        private static readonly StringName m_IdleAnimation = "idle"; // 预览资源自己的待机轨道，不与战斗动画库混用。
        [Export] private AnimationPlayer m_Player; // 只包含表现轨道，不含碰撞或战斗调用。
        [Export] private Sprite2D m_Body; // 与战斗角色相同网格的身体层。
        [Export] private Sprite2D m_Weapon; // 独立武器层，遮挡部分已在素材中裁成透明。
        private HeroAppearanceResource m_Appearance; // 每次打开借用当前战斗英雄的外观资源。
        private ulong m_LastTick; // 真实时间原点，不依赖 GGF 暂停后的零 delta。

        /// <summary>
        /// 同步当前穿戴状态，保持预览动画继续播放。
        /// </summary>
        /// <param name="weaponId">当前武器身份。</param>
        /// <param name="armorId">当前防具身份。</param>
        public void ApplyAppearance(GameConfig.Equipment.EquipmentId weaponId, GameConfig.Equipment.EquipmentId armorId)
        {
            m_Appearance.ApplyToSprites(m_Body, m_Weapon, weaponId, armorId);
            m_Player.Advance(0);
        }

        /// <summary>
        /// 每次打开绑定当前英雄的预览动画，从待机第一帧开始。
        /// </summary>
        /// <param name="appearance">当前英雄拥有的换装与预览资源。</param>
        public void Reset(HeroAppearanceResource appearance)
        {
            m_Player.Stop();
            m_Appearance = appearance;
            m_Player.RemoveAnimationLibrary("");
            m_Player.AddAnimationLibrary("", appearance.PreviewAnimations);
            m_Player.Play(m_IdleAnimation);
            m_LastTick = Time.GetTicksUsec();
        }

        /// <summary>
        /// 由窗口驱动一次真实时间采样，暂停战斗时预览仍可播放。
        /// </summary>
        public void Advance()
        {
            ulong now = Time.GetTicksUsec();
            m_Player.Advance((now - m_LastTick) / 1_000_000d);
            m_LastTick = now;
        }
    }
}
