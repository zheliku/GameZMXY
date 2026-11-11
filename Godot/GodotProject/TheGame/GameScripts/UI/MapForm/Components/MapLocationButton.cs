using System;
using GameLogic.Architecture;
using GameLogic.Level;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI.Widgets;

/// <summary>
/// 地图地点按钮：普通图或官方透明点击区命中，悬停时补充放大图的可见轮廓。
/// </summary>
public partial class MapLocationButton : TextureButton, IController
{
    private static readonly Vector2[] s_outlineOffsets =
    [
        new(-1, -1), new(-1, 0), new(-1, 1), new(0, -1), new(0, 1), new(1, -1), new(1, 0), new(1, 1)
    ]; // 与轮廓 shader 共用八个采样方向。

    [Export] private TextureRect _hoverImage; // 直接子节点，保持比例绘制并持有地图轮廓材质。
    [Export] private int _levelId; // 本地点对应的关卡配置主键。
    [Export] private GameLogic.UI.LevelGuideTooltip _guide; // 地图共用的可停留关卡浮层。
    private Bitmap _normalMask; // 就绪时从普通图创建一次，池复用期间保留。
    private Bitmap _hoverMask; // 悬停图的透明轮廓，不在鼠标移动时重新读取纹理。
    private bool _openGuideOnMotion; // 进入地点后的首个GUI鼠标事件携带精确局部坐标。

    /// <summary>
    /// 本地点场景绑定的关卡主键；导航和图鉴共用这一身份。
    /// </summary>
    internal int LevelId => _levelId;

    /// <summary>
    /// 借用当前GGF流程的架构，只组合读取图鉴配置。
    /// </summary>
    /// <returns>当前流程共享架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture()
    {
        return GF.Fsm.GetArchitecture();
    }

    /// <summary>
    /// 悬停时更新地图共用浮层，保持原有放大命中逻辑。
    /// </summary>
    private void OnGuideMouseEntered()
    {
        _openGuideOnMotion = true;
    }

    /// <summary>
    /// 从原生GUI事件取得本次进入点，仅首次进入执行图鉴组合查询。
    /// </summary>
    /// <param name="inputEvent">按钮局部输入事件。</param>
    public override void _GuiInput(InputEvent inputEvent)
    {
        if (Disabled || !_openGuideOnMotion || inputEvent is not InputEventMouseMotion motion || _levelId <= 0 || _guide == null)
        {
            return;
        }
        _openGuideOnMotion = false;
        _guide.ShowFor(this, this.SendQuery(new LevelGuideQuery(_levelId)), GetGlobalTransformWithCanvas() * motion.Position);
    }

    /// <summary>
    /// 校验绘制约定并缓存两张图片的透明轮廓；几何和动画参数在命中时读取。
    /// </summary>
    /// <exception cref="InvalidOperationException">场景未绑定所需图片、材质或绘制模式不符合命中约定。</exception>
    public override void _Ready()
    {
        if (TextureNormal == null || _hoverImage?.Texture == null || _hoverImage.Material is not ShaderMaterial ||
            _hoverImage.GetParent() != this ||
            StretchMode is not (StretchModeEnum.KeepCentered or StretchModeEnum.KeepAspectCentered) ||
            _hoverImage.StretchMode is not (TextureRect.StretchModeEnum.KeepAspect or TextureRect.StretchModeEnum.KeepAspectCentered))
        {
            throw new InvalidOperationException("地图地点需要居中的普通图、保持比例的悬停子图和轮廓材质。");
        }

        // 只缓存源图片轮廓，不缓存尺寸、缩放和动画状态。
        _normalMask = TextureClickMask ?? CreateMask(TextureNormal);
        _hoverMask = CreateMask(_hoverImage.Texture);
        MouseEntered += OnGuideMouseEntered;
    }

    /// <summary>
    /// 根据当前实际显示的图片判断命中；就绪前不接收鼠标，不改变悬停状态。
    /// </summary>
    /// <param name="point">按钮局部坐标，单位为设计视口像素。</param>
    /// <returns>是否位于当前图像的可见轮廓内。</returns>
    public override bool _HasPoint(Vector2 point)
    {
        if (_normalMask == null)
        {
            return false;
        }

        Vector2 normalSize = _normalMask.GetSize();
        float normalFit = StretchMode == StretchModeEnum.KeepAspectCentered
            ? Mathf.Min(Size.X / normalSize.X, Size.Y / normalSize.Y) : 1f;
        bool normalHit = HasOpaquePixel(_normalMask, (point - (Size - normalSize * normalFit) / 2) / normalFit);
        if (!_hoverImage.Visible || Disabled || (TextureClickMask != null && normalHit))
        {
            // 浅岸、东海原版用透明形状接收输入，不能因中心无图像而反复退出悬停。
            return normalHit;
        }

        // 先逆转子节点的 Scale/Pivot，再按 KeepAspect 和 shader 的 UV 缩放还原源像素。
        Vector2 local = _hoverImage.GetTransform().AffineInverse() * point;
        Vector2 imageSize = _hoverMask.GetSize();
        float fit = Mathf.Min(_hoverImage.Size.X / imageSize.X, _hoverImage.Size.Y / imageSize.Y);
        Vector2 drawSize = imageSize * fit;
        if (_hoverImage.StretchMode == TextureRect.StretchModeEnum.KeepAspectCentered)
        {
            local -= (_hoverImage.Size - drawSize) * .5f;
        }

        if (!new Rect2(Vector2.Zero, drawSize).HasPoint(local))
        {
            return false;
        }

        var material = (ShaderMaterial)_hoverImage.Material;
        float imageScale = material.GetShaderParameter("image_scale").AsSingle();
        Vector2 pixel = ((local / drawSize - new Vector2(0.5f, 0.5f)) / imageScale + new Vector2(0.5f, 0.5f)) *
                        imageSize;
        if (HasOpaquePixel(_hoverMask, pixel))
        {
            return true;
        }

        // 黄色轮廓也属于当前可见图像，使用与 shader 相同的偏移采样。
        float outline = material.GetShaderParameter("line_thickness").AsSingle();
        foreach (Vector2 offset in s_outlineOffsets)
        {
            if (HasOpaquePixel(_hoverMask, pixel + outline * offset))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 从纹理创建可重复使用的透明轮廓，创建后释放 CPU 图片。
    /// </summary>
    /// <param name="texture">已加载的源纹理。</param>
    /// <returns>透明度超过阈值的像素集合。</returns>
    private static Bitmap CreateMask(Texture2D texture)
    {
        var mask = new Bitmap();
        using Image image = texture.GetImage();
        mask.CreateFromImageAlpha(image);
        return mask;
    }

    /// <summary>
    /// 读取源像素的轮廓；图片外的坐标视为透明。
    /// </summary>
    /// <param name="mask">源图透明轮廓。</param>
    /// <param name="pixel">源图像素坐标。</param>
    /// <returns>是否位于非透明像素内。</returns>
    private static bool HasOpaquePixel(Bitmap mask, Vector2 pixel)
    {
        return new Rect2(Vector2.Zero, mask.GetSize()).HasPoint(pixel) &&
               mask.GetBitv(new Vector2I((int)pixel.X, (int)pixel.Y));
    }
}
