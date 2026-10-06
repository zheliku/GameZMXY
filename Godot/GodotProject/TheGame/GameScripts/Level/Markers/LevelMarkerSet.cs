using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic.Level;

/// <summary>统一锚点条目列表、编辑器标注和初始化校验，不在编辑器加载时修改场景。</summary>
[Tool]
public abstract partial class LevelMarkerSet : Node2D
{
    [Export] private Godot.Collections.Array<LevelMarkerEntry> m_Entries = new(); // 当前集合登记的子节点条目。
    [Export(PropertyHint.Range, "1,700,1")] private float m_MarkerSize = 16f; // 圆圈半径或门标注半高，单位为世界像素。
    [Export] private bool m_DrawInGame; // 调试时显示锚点标注。

    /// <summary>显式同步新增和删除的子节点，保留已有条目的业务键与颜色。</summary>
    [ExportToolButton("同步子节点配置")]
    public Callable SyncChildEntries => Callable.From(SyncEntries);

    /// <summary>新条目按节点职责使用的默认标注颜色。</summary>
    protected virtual Color DefaultColor => new(1f, 0.65f, 0.1f, 0.9f);

    /// <summary>标注尺寸，单位为世界像素。</summary>
    protected float MarkerSize => m_MarkerSize;

    /// <summary>判断集合支持的直接子节点类型。</summary>
    /// <param name="node">待检查的直接子节点。</param>
    /// <returns>支持该节点时返回 true。</returns>
    protected abstract bool Supports(Node node);

    /// <summary>准备编辑器可视化；运行时的索引由关卡初始化显式建立。</summary>
    public override void _Ready()
    {
        SetProcess(Engine.IsEditorHint());
        QueueRedraw();
    }

    /// <summary>刷新子节点移动、列表编辑和颜色变化对应的标注。</summary>
    /// <param name="delta">距上一帧的秒数；绘制刷新与它无关。</param>
    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    /// <summary>为所有支持的子节点绘制标注，未登记节点也可见。</summary>
    public override void _Draw()
    {
        if (!Engine.IsEditorHint() && !m_DrawInGame)
        {
            return;
        }

        foreach (Node2D child in GetChildren().OfType<Node2D>().Where(Supports))
        {
            LevelMarkerEntry entry = FindEntry(child);
            Color color = entry?.DrawColor ?? Colors.Magenta;
            string label = entry?.Id ?? $"{child.Name} (未登记)";
            DrawMarker(ToLocal(child.GlobalPosition), color, label);
        }
    }

    /// <summary>返回检查器中的条目登记问题，避免编辑器加载时抛异常。</summary>
    /// <returns>逐条登记错误；没有问题时为空数组。</returns>
    public override string[] _GetConfigurationWarnings()
    {
        List<string> warnings = new();
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<Node> nodes = new();
        foreach (LevelMarkerEntry entry in m_Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id))
            {
                warnings.Add("条目必须有非空且唯一的 ID。");
                continue;
            }

            Node child = GetNodeOrNull(entry.Node);
            if (child == null || child.GetParent() != this || !Supports(child) || !nodes.Add(child))
            {
                warnings.Add($"{entry.Id} 必须绑定一个未重复登记的直接子节点。");
            }
        }

        if (GetChildren().Any(x => Supports(x) && !nodes.Contains(x)))
        {
            warnings.Add("存在未登记的子节点，请点击“同步子节点配置”后填写稳定 ID。");
        }

        return warnings.ToArray();
    }

    /// <summary>在初始化边界一次性校验登记信息并建立类型明确的索引。</summary>
    /// <typeparam name="T">条目绑定的子节点类型。</typeparam>
    /// <returns>稳定 ID 到已校验子节点的索引。</returns>
    /// <exception cref="InvalidOperationException">条目缺失、重复或绑定类型不符时抛出。</exception>
    protected Dictionary<string, T> BuildIndex<T>() where T : Node2D
    {
        string[] warnings = _GetConfigurationWarnings();
        if (warnings.Length > 0)
        {
            throw new InvalidOperationException($"{Name}: {string.Join("；", warnings)}");
        }

        return m_Entries.ToDictionary(x => x.Id, x => GetNode<T>(x.Node), StringComparer.Ordinal);
    }

    /// <summary>绘制生成点和触发器共用的圆圈与文字。</summary>
    /// <param name="position">标注中心，相对本节点的局部坐标。</param>
    /// <param name="color">标注颜色。</param>
    /// <param name="label">标注文本。</param>
    protected virtual void DrawMarker(Vector2 position, Color color, string label)
    {
        DrawArc(position, m_MarkerSize, 0f, Mathf.Tau, 40, color, 2f, true);
        DrawCircle(position, 3f, color);
        DrawString(ThemeDB.FallbackFont, position + new Vector2(m_MarkerSize + 6f, 5f), label,
            fontSize: 14, modulate: color);
    }

    /// <summary>在登记列表中查找子节点对应的条目。</summary>
    /// <param name="child">已确认受支持的直接子节点。</param>
    /// <returns>匹配的条目；未登记时返回 null。</returns>
    private LevelMarkerEntry FindEntry(Node child)
    {
        return m_Entries.FirstOrDefault(x => x != null && GetNodeOrNull(x.Node) == child);
    }

    /// <summary>按当前子节点重建登记列表，保留仍存在的条目及其人工设置。</summary>
    private void SyncEntries()
    {
        // 用户主动同步列表，避免加载、导入或重载脚本时产生隐式场景修改。
        Godot.Collections.Array<LevelMarkerEntry> entries = new();
        foreach (Node child in GetChildren().Where(Supports))
        {
            entries.Add(FindEntry(child) ?? CreateEntry(child));
        }

        m_Entries = entries;
        NotifyPropertyListChanged();
        UpdateConfigurationWarnings();
        QueueRedraw();
    }

    /// <summary>为用户新增的子节点创建默认条目。</summary>
    /// <param name="child">尚未登记的直接子节点。</param>
    /// <returns>绑定节点路径、名称和默认颜色的新条目。</returns>
    private LevelMarkerEntry CreateEntry(Node child)
    {
        LevelMarkerEntry entry = new();
        entry.Bind(GetPathTo(child), child.Name, DefaultColor);
        return entry;
    }
}
