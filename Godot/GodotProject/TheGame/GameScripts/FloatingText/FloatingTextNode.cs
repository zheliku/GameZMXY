using System;
using System.Collections.Generic;
using GameConfig.Battle;
using Godot;
using GodotGameFramework.NodePool;

namespace GameLogic.FloatingText;

/// <summary>恢复提示的颜色样式；调用方负责实际生命或魔法结算。</summary>
public enum RecoveryKind
{
    /// <summary>绿色生命恢复。</summary>
    Health,
    /// <summary>蓝色魔法恢复。</summary>
    Mana
}

/// <summary>共用位图数字与秒制动画的飘字节点；伤害、闪避和恢复只选择不同显示样式。</summary>
public partial class FloatingTextNode : Node2D, IPoolable
{
    /// <summary>决定本次动画节奏，不影响玩法结算。</summary>
    private enum DisplayKind { Damage, Miss, Recovery }

    private const int MaxDigits = 10; // 覆盖 int 正数的全部数位。
    private const float DamageRiseDistance = 50f; // 原伤害数字上升像素。
    private const float DamageRiseSeconds = .6f; // 原伤害数字上升秒数。
    private const float DamageFadeSeconds = .4f; // 原伤害数字淡出秒数。
    private const float MissRiseDistance = 30f; // 原闪避提示上升像素。
    private const float MissRiseSeconds = .4f; // 原闪避提示上升秒数。
    private const float MissFadeSeconds = .5f; // 原闪避提示淡出秒数。
    private const float RecoveryLifetime = 1f; // 官方 25 × 40ms，独立于渲染帧率。
    private const float RecoveryRiseDistance = 75f; // 官方每 40ms 上移 3px。
    private const float DamagePeakScale = 2f; // 普通伤害的最大放大倍数。
    private const float CriticalPeakScale = 3f; // 暴击伤害的最大放大倍数。
    private const float DamagePulseSeconds = .1f; // 普通伤害放大、缩回各用的秒数。
    private const float CriticalPulseSeconds = .2f; // 暴击伤害放大、缩回各用的秒数。

    [Export] private Texture2D m_MonsterPhysics; // 怪物受物理伤害的数字图集。
    [Export] private Texture2D m_MonsterPhysicsCrit; // 怪物受物理暴击的数字图集。
    [Export] private Texture2D m_MonsterMagic; // 怪物受魔法伤害的数字图集。
    [Export] private Texture2D m_MonsterMagicCrit; // 怪物受魔法暴击的数字图集。
    [Export] private Texture2D m_HeroPhysics; // 英雄受物理伤害的数字图集。
    [Export] private Texture2D m_HeroMagic; // 英雄受魔法伤害的数字图集。
    [Export] private Texture2D m_Real; // 真实伤害数字图集。
    [Export] private Texture2D m_Health; // 绿色生命恢复数字。
    [Export] private Texture2D m_Mana; // 蓝色魔法恢复数字。
    [Export] private Sprite2D m_Miss; // 闪避提示贴图。
    [Export] private Node2D m_Content; // 数字、提示的共同缩放和透明度节点。
    [Export(PropertyHint.Range, "0,12,0.5")] private float m_DigitGap = 0f; // 描边已提供视觉间隔，默认不另加空隙；与图集格宽无关。
    [Export(PropertyHint.Range, "0,0.5,0.01")] private float m_NarrowGlyphPadding = 1f / 6f; // 根据窄字与最宽字的宽度差补充左右留白，统一覆盖 0–9。

    private readonly Sprite2D[] m_Digits = new Sprite2D[MaxDigits]; // 首次取出创建，所有样式复用同一组数字。
    private readonly int[] m_DigitValues = new int[MaxDigits]; // 本次排布的数位，避免每次显示分配字符串或数组。
    private readonly Dictionary<Texture2D, Rect2[]> m_GlyphRegions = new(); // 每种图集首次使用时读取轮廓，池复用保留只读字宽。
    private Action<FloatingTextNode> m_Finished; // 显示完成只通知模块，由模块归还节点。
    private DisplayKind m_Kind; // 本次使用的动画节奏。
    private bool m_IsCrit; // 伤害样式是否使用暴击脉冲。
    private float m_Age; // 本次已经显示的游戏秒数。
    private Vector2 m_Start; // 本次世界锚点，显示后不跟随角色移动。

    /// <summary>首次取出创建数字子节点；后续取出只复位显示状态。</summary>
    public void OnGet()
    {
        // 相同池实例可以在伤害、闪避和恢复间切换，不重新分配数字节点。
        for (int i = 0; i < m_Digits.Length; i++)
        {
            if (m_Digits[i] != null)
                continue;
            var digit = new Sprite2D
            {
                RegionEnabled = true,
                RegionFilterClipEnabled = true,
                TextureFilter = TextureFilterEnum.Linear,
                Visible = false
            };
            m_Content.AddChild(digit);
            m_Digits[i] = digit;
        }
        m_Age = 0;
        m_Content.Scale = Vector2.One;
        m_Content.Modulate = Colors.White;
        SetProcess(false);
    }

    /// <summary>回池时停止计时、清除完成回调和全部内容，避免复用残留旧样式。</summary>
    public void OnRelease()
    {
        SetProcess(false);
        m_Finished = null;
        foreach (var digit in m_Digits)
            if (digit != null)
                digit.Visible = false;
        m_Miss.Visible = false;
    }

    /// <summary>显示一次已结算伤害；普通与暴击使用各自位图和放大节奏。</summary>
    /// <param name="position">受击头顶的世界锚点。</param>
    /// <param name="damage">正数显示量。</param>
    /// <param name="kind">物理、魔法或真实伤害。</param>
    /// <param name="isCrit">是否暴击。</param>
    /// <param name="targetIsHero">是否使用英雄受击样式。</param>
    /// <param name="finished">显示结束后由模块归还节点。</param>
    public void ShowDamage(Vector2 position, int damage, DamageKind kind, bool isCrit, bool targetIsHero,
        Action<FloatingTextNode> finished)
    {
        var sheet = PickDamageSheet(kind, isCrit, targetIsHero);
        LayoutDigits(damage, sheet, true);
        Begin(position, DisplayKind.Damage, isCrit, finished);
    }

    /// <summary>显示一次闪避贴图，沿用上升后淡出的节奏。</summary>
    /// <param name="position">受击头顶的世界锚点。</param>
    /// <param name="finished">显示结束后由模块归还节点。</param>
    public void ShowMiss(Vector2 position, Action<FloatingTextNode> finished)
    {
        foreach (var digit in m_Digits)
            digit.Visible = false;
        Begin(position, DisplayKind.Miss, false, finished);
    }

    /// <summary>显示绿色生命或蓝色魔法恢复，左起按字宽排布并保留官方一秒匀速上升。</summary>
    /// <param name="position">目标角色的世界原点。</param>
    /// <param name="amount">正数显示量，不改变实际属性。</param>
    /// <param name="kind">生命或魔法的位图样式。</param>
    /// <param name="finished">显示结束后由模块归还节点。</param>
    public void ShowRecovery(Vector2 position, int amount, RecoveryKind kind, Action<FloatingTextNode> finished)
    {
        LayoutDigits(amount, kind == RecoveryKind.Mana ? m_Mana : m_Health, false);
        Begin(position + new Vector2(-20, -60), DisplayKind.Recovery, false, finished);
    }

    /// <summary>为所有样式建立同一计时和结束回调；节点不访问全局事件或节点池。</summary>
    /// <param name="position">已经按样式修正的世界锚点。</param>
    /// <param name="kind">动画节奏。</param>
    /// <param name="isCrit">是否使用暴击放大。</param>
    /// <param name="finished">唯一一次完成回调。</param>
    private void Begin(Vector2 position, DisplayKind kind, bool isCrit, Action<FloatingTextNode> finished)
    {
        m_Start = position;
        GlobalPosition = position;
        m_Age = 0;
        m_Kind = kind;
        m_IsCrit = isCrit;
        m_Finished = finished;
        m_Miss.Visible = kind == DisplayKind.Miss;
        m_Content.Scale = Vector2.One;
        m_Content.Modulate = Colors.White;
        SetProcess(true);
    }

    /// <summary>所有样式共用游戏秒数推进；暂停时 delta 为零，完成后只通知一次。</summary>
    /// <param name="delta">本帧游戏秒数。</param>
    public override void _Process(double delta)
    {
        m_Age += (float)delta;
        float riseSeconds = m_Kind switch { DisplayKind.Damage => DamageRiseSeconds, DisplayKind.Miss => MissRiseSeconds, _ => RecoveryLifetime };
        float riseDistance = m_Kind switch { DisplayKind.Damage => DamageRiseDistance, DisplayKind.Miss => MissRiseDistance, _ => RecoveryRiseDistance };
        float fadeSeconds = m_Kind switch { DisplayKind.Damage => DamageFadeSeconds, DisplayKind.Miss => MissFadeSeconds, _ => 0f };

        // 上升、淡出与脉冲均根据同一年龄计算，不创建第二套动画或后台计时器。
        GlobalPosition = m_Start + Vector2.Up * (riseDistance * Math.Clamp(m_Age / riseSeconds, 0f, 1f));
        float alpha = fadeSeconds == 0 ? 1f : 1f - Math.Clamp((m_Age - riseSeconds) / fadeSeconds, 0f, 1f);
        m_Content.Modulate = new Color(1, 1, 1, alpha);
        if (m_Kind == DisplayKind.Damage)
        {
            float halfPulse = m_IsCrit ? CriticalPulseSeconds : DamagePulseSeconds;
            float peakScale = m_IsCrit ? CriticalPeakScale : DamagePeakScale;
            float progress = m_Age < halfPulse ? m_Age / halfPulse : 1f - (m_Age - halfPulse) / halfPulse;
            m_Content.Scale = Vector2.One * (1f + (peakScale - 1f) * Math.Clamp(progress, 0f, 1f));
        }
        if (m_Age < riseSeconds + fadeSeconds)
            return;

        // 先停止并清除旧回调；归还后同一实例可能立即用于另一种样式。
        SetProcess(false);
        var finished = m_Finished;
        m_Finished = null;
        finished?.Invoke(this);
    }

    /// <summary>按伤害类型、暴击和受击方选择数字位图。</summary>
    /// <param name="kind">伤害类型。</param>
    /// <param name="isCrit">是否暴击。</param>
    /// <param name="targetIsHero">是否英雄受击。</param>
    /// <returns>对应样式的十格数字图集。</returns>
    private Texture2D PickDamageSheet(DamageKind kind, bool isCrit, bool targetIsHero)
    {
        if (kind == DamageKind.Real)
            return m_Real;
        if (targetIsHero)
            return kind == DamageKind.Magic ? m_HeroMagic : m_HeroPhysics;
        if (kind == DamageKind.Magic)
            return isCrit ? m_MonsterMagicCrit : m_MonsterMagic;
        return isCrit ? m_MonsterPhysicsCrit : m_MonsterPhysics;
    }

    /// <summary>按可见宽度排布并给窄字补充适量侧留白；仅横向去透明空列，保留纵向注册位置。</summary>
    /// <param name="value">非负显示量。</param>
    /// <param name="sheet">十格数字图集。</param>
    /// <param name="centered">伤害居中，恢复按官方左起排列。</param>
    /// <exception cref="InvalidOperationException">图集不是有效的十格 RGBA 数字图。</exception>
    private void LayoutDigits(int value, Texture2D sheet, bool centered)
    {
        // 整数除法避免 10 的幂次附近因浮点对数导致少算一位。
        value = Math.Max(0, value);
        int count = 1;
        for (int probe = value; probe >= 10 && count < MaxDigits; probe /= 10)
            count++;
        int divisor = 1;
        for (int i = 1; i < count; i++)
            divisor *= 10;
        var regions = GetGlyphRegions(sheet);
        float widest = 0;
        foreach (Rect2 region in regions)
            widest = Math.Max(widest, region.Size.X);
        float width = m_DigitGap * (count - 1);
        for (int i = 0; i < count; i++)
        {
            int number = value / divisor % 10;
            m_DigitValues[i] = number;
            width += regions[number].Size.X;
            divisor = Math.Max(1, divisor / 10);
        }
        // 相邻两字各贡献一侧留白；宽字保持紧凑，窄字不会因削掉透明列而挤在一起。
        for (int i = 1; i < count; i++)
            width += (2 * widest - regions[m_DigitValues[i - 1]].Size.X - regions[m_DigitValues[i]].Size.X) * m_NarrowGlyphPadding;
        float cursor = centered ? -width * .5f : 0;
        for (int i = 0; i < m_Digits.Length; i++)
        {
            var digit = m_Digits[i];
            digit.Visible = i < count;
            if (!digit.Visible)
                continue;
            digit.Centered = centered;
            digit.Texture = sheet;
            Rect2 region = regions[m_DigitValues[i]];
            digit.RegionRect = region;
            digit.Position = new Vector2(cursor + (centered ? region.Size.X * .5f : 0), 0);
            cursor += region.Size.X + m_DigitGap;
            if (i + 1 < count)
                cursor += (2 * widest - region.Size.X - regions[m_DigitValues[i + 1]].Size.X) * m_NarrowGlyphPadding;
        }
    }

    /// <summary>首次使用图集时缓存十个数字的横向轮廓，包含所有非透明描边；不会在帧更新时读图。</summary>
    /// <param name="sheet">等宽十格仅用于存储的数字图集。</param>
    /// <returns>横向去留白、完整保留原图高度的十个字符区域。</returns>
    /// <exception cref="InvalidOperationException">图集尺寸、图像或数字轮廓无效。</exception>
    private Rect2[] GetGlyphRegions(Texture2D sheet)
    {
        if (m_GlyphRegions.TryGetValue(sheet, out var cached))
            return cached;

        // 只在本池实例首次使用某种素材时解析；缓存随资源引用和池实例一起存活。
        using var image = sheet.GetImage();
        if (image == null || image.GetWidth() % 10 != 0 || image.GetHeight() <= 0 ||
            image.GetWidth() < 10 || image.IsCompressed() && image.Decompress() != Error.Ok)
            throw new InvalidOperationException($"数字图集必须为有效十格图：{sheet.ResourcePath}");
        image.Convert(Image.Format.Rgba8);
        byte[] pixels = image.GetData();
        int width = image.GetWidth();
        int height = image.GetHeight();
        int cellWidth = width / 10;
        var regions = new Rect2[10];
        for (int number = 0; number < regions.Length; number++)
        {
            int left = cellWidth;
            int right = -1;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < cellWidth; x++)
                {
                    if (pixels[((y * width + number * cellWidth + x) * 4) + 3] == 0)
                        continue;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                }
            if (right < left)
                throw new InvalidOperationException($"数字图集的 {number} 没有可见像素：{sheet.ResourcePath}");
            regions[number] = new Rect2(number * cellWidth + left, 0, right - left + 1, height);
        }
        m_GlyphRegions.Add(sheet, regions);
        return regions;
    }
}
