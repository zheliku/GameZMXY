using Godot;
using GameConfig.Equipment;

namespace GameLogic.Heroes;

/// <summary>共用定位网格的独立身体、武器图集；武器透明区保留每帧原始遮挡。</summary>
[Tool, GlobalClass]
public partial class HeroAppearanceResource : Resource
{
    [Export] private Texture2D m_BaseBody; // 未穿防具时的身体图集。
    [Export] private Godot.Collections.Dictionary<EquipmentId, Texture2D> m_ArmorTextures = new(); // 按防具身份选择完整动作图集。
    [Export] private Texture2D m_BaseWeapon; // 默认木棍，已裁掉被身体前景遮挡的部分。
    [Export] private Godot.Collections.Dictionary<EquipmentId, Texture2D> m_WeaponTextures = new(); // 武器独立图集，保留身体遮挡的透明区。

    /// <summary>按穿戴状态替换纹理，保留帧号、朝向及动画播放位置。</summary>
    /// <param name="body">身体表现层。</param>
    /// <param name="weapon">武器表现层，绘制在身体层上方。</param>
    /// <param name="weaponId">当前武器身份，None 使用默认木棍。</param>
    /// <param name="armorId">当前防具身份，None 使用裸身。</param>
    /// <exception cref="System.InvalidOperationException">该角色的已穿戴装备缺少完整动作图集。</exception>
    public void ApplyToSprites(Sprite2D body, Sprite2D weapon, EquipmentId weaponId, EquipmentId armorId)
    {
        body.Texture = armorId == EquipmentId.None ? m_BaseBody
            : m_ArmorTextures.TryGetValue(armorId, out var armor) && armor != null ? armor
            : throw new System.InvalidOperationException($"防具缺少角色动作图集：{armorId}");
        weapon.Texture = weaponId == EquipmentId.None ? m_BaseWeapon
            : m_WeaponTextures.TryGetValue(weaponId, out var equipped) && equipped != null ? equipped
            : throw new System.InvalidOperationException($"武器缺少角色动作图集：{weaponId}");
    }
}
