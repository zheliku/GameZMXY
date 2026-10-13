using Godot;

namespace GameLogic.Heroes;

/// <summary>共用定位网格的独立身体、武器图集；武器透明区保留每帧原始遮挡。</summary>
[GlobalClass]
public partial class HeroAppearanceResource : Resource
{
    [Export] private Texture2D m_BaseBody; // 未穿防具时的身体图集。
    [Export] private Texture2D m_ArmoredBody; // 当前基础防具共用的身体图集。
    [Export] private Texture2D m_BaseWeapon; // 默认木棍，已裁掉被身体前景遮挡的部分。
    [Export] private Texture2D m_EquippedWeapon; // 基础武器共用的图集，同样保留遮挡透明区。

    /// <summary>按穿戴状态替换纹理，保留帧号、朝向及动画播放位置。</summary>
    /// <param name="body">身体表现层。</param>
    /// <param name="weapon">武器表现层，绘制在身体层上方。</param>
    /// <param name="armed">是否穿戴武器。</param>
    /// <param name="armored">是否穿戴防具。</param>
    public void Apply(Sprite2D body, Sprite2D weapon, bool armed, bool armored)
    {
        body.Texture = armored ? m_ArmoredBody : m_BaseBody;
        weapon.Texture = armed ? m_EquippedWeapon : m_BaseWeapon;
    }
}
