using GameConfig.Equipment;
using Godot;

namespace GameLogic.Heroes;

/// <summary>
/// 共用定位网格的独立身体、武器图集；武器透明区保留每帧原始遮挡。
/// </summary>
[Tool, GlobalClass]
public partial class HeroAppearanceResource : Resource
{
    private const int FrameCellSize = 200; // 动作格的逻辑尺寸，不随素材密度改变。
    private const int FrameColumns = 6; // 身体与武器沿用六列网格。

    [Export(PropertyHint.Range, "1,2,1")] private int _textureDensity = 1; // 同一外观资源全部图集的像素密度。

    [Export] private Texture2D _baseBody; // 未穿防具时的身体图集。
    [Export] private Godot.Collections.Dictionary<EquipmentId, Texture2D> _armorTextures = new(); // 按防具身份选择完整动作图集。
    [Export] private Texture2D _baseWeapon; // 默认木棍，已裁掉被身体前景遮挡的部分。
    [Export] private Godot.Collections.Dictionary<EquipmentId, Texture2D> _weaponTextures = new(); // 武器独立图集，保留身体遮挡的透明区。
    [Export] private AnimationLibrary _previewAnimations; // 本角色的背包待机表现，只引用预览节点。
    [Export] private HeroAppearanceResource _previewAppearance; // 官方背包的独立衣装图集，复用相同换装数据形状。

    /// <summary>
    /// 背包使用独立身体和武器图集，不借用战斗实体。
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

        ApplyAtlas(body, bodyTexture, _textureDensity, "身体");
        ApplyAtlas(weapon, weaponTexture, _textureDensity, "武器");
    }

    /// <summary>
    /// 按共享动作格网装配纹理，同时拒绝尺寸不完整的换装资源。
    /// </summary>
    /// <param name="sprite">场景绑定的表现层。</param>
    /// <param name="texture">本装备独立的完整动作图集。</param>
    /// <param name="density">每逻辑像素对应的源像素数。</param>
    /// <param name="layerName">装配失败时显示的层名称。</param>
    private static void ApplyAtlas(Sprite2D sprite, Texture2D texture, int density, string layerName)
    {
        if (sprite == null)
        {
            throw new System.InvalidOperationException($"换装缺少{layerName} Sprite2D。");
        }

        int pixelCellSize = FrameCellSize * density;
        if (texture == null || texture.GetWidth() != pixelCellSize * FrameColumns ||
            texture.GetHeight() <= 0 || texture.GetHeight() % pixelCellSize != 0)
        {
            throw new System.InvalidOperationException(
                $"{layerName}动作图集必须是{FrameColumns}列、每格{pixelCellSize}px的方格图集。");
        }

        sprite.Texture = texture;
        sprite.Hframes = FrameColumns;
        sprite.Vframes = texture.GetHeight() / pixelCellSize;
        // 注册点已烘焙进200逻辑格；只补偿视觉层密度，重复换装不会累计缩放。
        sprite.Scale = Vector2.One / density;
    }
}
