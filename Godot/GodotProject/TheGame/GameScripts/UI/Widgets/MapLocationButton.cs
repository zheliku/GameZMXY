using System;
using Godot;

namespace GameLogic.UI.Widgets;

/// <summary>地图地点按钮：普通图居中命中，悬停时按放大图的变换、透明轮廓和 shader 参数命中。</summary>
public partial class MapLocationButton : TextureButton
{
    private static readonly Vector2[] m_OutlineOffsets =
    [
        new(-1, -1), new(-1, 0), new(-1, 1), new(0, -1), new(0, 1), new(1, -1), new(1, 0), new(1, 1)
    ]; // 与轮廓 shader 共用八个采样方向。

    [Export] private TextureRect m_HoverImage; // 直接子节点，使用 KeepAspect 绘制并持有地图轮廓材质。
    private Bitmap m_NormalMask; // 就绪时从普通图创建一次，池复用期间保留。
    private Bitmap m_HoverMask; // 悬停图的透明轮廓，不在鼠标移动时重新读取纹理。

    /// <summary>校验绘制约定并缓存两张图片的透明轮廓；几何和动画参数在命中时读取。</summary>
    /// <exception cref="InvalidOperationException">场景未绑定所需图片、材质或绘制模式不符合命中约定。</exception>
    public override void _Ready()
    {
        if (TextureNormal == null || m_HoverImage?.Texture == null || m_HoverImage.Material is not ShaderMaterial ||
            m_HoverImage.GetParent() != this || StretchMode != StretchModeEnum.KeepCentered ||
            m_HoverImage.StretchMode != TextureRect.StretchModeEnum.KeepAspect || TextureClickMask != null)
            throw new InvalidOperationException("地图地点需要居中的普通图、KeepAspect 悬停子图和轮廓材质，不使用固定 TextureClickMask。");

        // 只缓存源图片轮廓，不缓存尺寸、缩放和动画状态。
        m_NormalMask = CreateMask(TextureNormal);
        m_HoverMask = CreateMask(m_HoverImage.Texture);
    }

    /// <summary>根据当前实际显示的图片判断命中；就绪前不接收鼠标，不改变悬停状态。</summary>
    /// <param name="point">按钮局部坐标，单位为设计视口像素。</param>
    /// <returns>是否位于当前图像的可见轮廓内。</returns>
    public override bool _HasPoint(Vector2 point)
    {
        if (m_NormalMask == null) return false;
        if (!m_HoverImage.Visible || Disabled)
            return HasOpaquePixel(m_NormalMask, point - (Size - (Vector2)m_NormalMask.GetSize()) / 2);

        // 先逆转子节点的 Scale/Pivot，再按 KeepAspect 和 shader 的 UV 缩放还原源像素。
        Vector2 local = m_HoverImage.GetTransform().AffineInverse() * point;
        Vector2 imageSize = m_HoverMask.GetSize();
        float fit = Mathf.Min(m_HoverImage.Size.X / imageSize.X, m_HoverImage.Size.Y / imageSize.Y);
        Vector2 drawSize = imageSize * fit;
        if (!new Rect2(Vector2.Zero, drawSize).HasPoint(local)) return false;
        var material = (ShaderMaterial)m_HoverImage.Material;
        float imageScale = material.GetShaderParameter("image_scale").AsSingle();
        Vector2 pixel = ((local / drawSize - new Vector2(0.5f, 0.5f)) / imageScale + new Vector2(0.5f, 0.5f)) *
                        imageSize;
        if (HasOpaquePixel(m_HoverMask, pixel)) return true;

        // 黄色轮廓也属于当前可见图像，使用与 shader 相同的偏移采样。
        float outline = material.GetShaderParameter("line_thickness").AsSingle();
        foreach (Vector2 offset in m_OutlineOffsets)
            if (HasOpaquePixel(m_HoverMask, pixel + outline * offset))
                return true;
        return false;
    }

    /// <summary>从纹理创建可重复使用的透明轮廓，创建后释放 CPU 图片。</summary>
    /// <param name="texture">已加载的源纹理。</param>
    /// <returns>透明度超过阈值的像素集合。</returns>
    private static Bitmap CreateMask(Texture2D texture)
    {
        var mask = new Bitmap();
        using Image image = texture.GetImage();
        mask.CreateFromImageAlpha(image);
        return mask;
    }

    /// <summary>读取源像素的轮廓；图片外的坐标视为透明。</summary>
    /// <param name="mask">源图透明轮廓。</param>
    /// <param name="pixel">源图像素坐标。</param>
    /// <returns>是否位于非透明像素内。</returns>
    private static bool HasOpaquePixel(Bitmap mask, Vector2 pixel)
    {
        return new Rect2(Vector2.Zero, mask.GetSize()).HasPoint(pixel) &&
               mask.GetBitv(new Vector2I((int)pixel.X, (int)pixel.Y));
    }
}
