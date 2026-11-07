using GameConfig.Equipment;
using Godot;

namespace GameLogic.Heroes;

/// <summary>
/// 共用定位网格的独立身体、武器图集；武器透明区保留每帧原始遮挡。
/// </summary>
[Tool, GlobalClass]
public partial class HeroAppearanceResource : Resource
{
    private const int FrameCellSize = 200;
    private const int FrameColumns = 6;

    [Export] private Texture2D _baseBody; // 未穿防具时的身体图集。
    [Export] private Godot.Collections.Dictionary<EquipmentId, Texture2D> _armorTextures = new(); // 按防具身份选择完整动作图集。
    [Export] private Texture2D _baseWeapon; // 默认木棍，已裁掉被身体前景遮挡的部分。
    [Export] private Godot.Collections.Dictionary<EquipmentId, Texture2D> _weaponTextures = new(); // 武器独立图集，保留身体遮挡的透明区。
    [Export] private AnimationLibrary _previewAnimations; // 本角色的背包待机表现，只引用预览节点。
    [Export] private HeroAppearanceResource _previewAppearance; // 官方背包的独立衣装图集，复用相同换装数据形状。

    /// <summary>
    /// 官方背包使用自身的身体和武器图，不借用战斗待机姿势。
    /// </summary>
    public HeroAppearanceResource PreviewAppearance => _previewAppearance;

    /// <summary>
    /// 与本角色换装图集配套的背包待机动画。
    /// </summary>
    public AnimationLibrary PreviewAnimations => _previewAnimations;

    /// <summary>
    /// 按穿戴状态替换纹理，保留帧号、朝向及动画播放位置。
    /// </summary>
    /// <param name="body">身体表现层。</param>
    /// <param name="weapon">武器表现层，绘制在身体层上方。</param>
    /// <param name="weaponId">当前武器身份，None 使用默认木棍。</param>
    /// <param name="armorId">当前防具身份，None 使用裸身。</param>
    /// <exception cref="System.InvalidOperationException">该角色的已穿戴装备缺少完整动作图集。</exception>
    public void ApplyToSprites(Sprite2D body, Sprite2D weapon, EquipmentId weaponId, EquipmentId armorId)
    {
        Texture2D bodyTexture = armorId == EquipmentId.None ? _baseBody
            : _armorTextures.TryGetValue(armorId, out Texture2D armor) && armor != null ? armor
            : throw new System.InvalidOperationException($"防具缺少角色动作图集：{armorId}");
        Texture2D weaponTexture = weaponId == EquipmentId.None ? _baseWeapon
            : _weaponTextures.TryGetValue(weaponId, out Texture2D equipped) && equipped != null ? equipped
            : throw new System.InvalidOperationException($"武器缺少角色动作图集：{weaponId}");

        ApplyAtlas(body, bodyTexture, "身体");
        ApplyAtlas(weapon, weaponTexture, "武器");
    }

    /// <summary>
    /// 按共享动作格网装配纹理，同时拒绝尺寸不完整的换装资源。
    /// </summary>
    /// <param name="sprite">场景绑定的表现层。</param>
    /// <param name="texture">本装备独立的完整动作图集。</param>
    /// <param name="layerName">装配失败时显示的层名称。</param>
    private static void ApplyAtlas(Sprite2D sprite, Texture2D texture, string layerName)
    {
        if (sprite == null)
        {
            throw new System.InvalidOperationException($"换装缺少{layerName} Sprite2D。");
        }

        if (texture == null || texture.GetWidth() != FrameCellSize * FrameColumns ||
            texture.GetHeight() <= 0 || texture.GetHeight() % FrameCellSize != 0)
        {
            throw new System.InvalidOperationException(
                $"{layerName}动作图集必须是{FrameColumns}列、每格{FrameCellSize}px的方格图集。");
        }

        sprite.Texture = texture;
        sprite.Hframes = FrameColumns;
        sprite.Vframes = texture.GetHeight() / FrameCellSize;
    }
}
