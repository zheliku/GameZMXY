using Godot;

namespace GameLogic.Level;

/// <summary>登记集合中纯子节点的稳定标识与编辑器显示信息。</summary>
[Tool, GlobalClass]
public partial class LevelMarkerEntry : Resource
{
    [Export] private string m_Id = string.Empty; // 配置表引用的稳定业务键。
    [Export] private NodePath m_Node = new(); // 相对所属集合节点的直接子节点路径。
    [Export] private Color m_DrawColor = new(1f, 0.65f, 0.1f, 0.9f); // 条目在场景中的标注颜色。

    /// <summary>供配置表引用的稳定业务键。</summary>
    public string Id => m_Id;

    /// <summary>相对所属集合节点的直接子节点路径。</summary>
    public NodePath Node => m_Node;

    /// <summary>场景标注颜色。</summary>
    public Color DrawColor => m_DrawColor;

    /// <summary>为编辑器中新登记的子节点填写初始信息。</summary>
    /// <param name="node">相对所属集合节点的子节点路径。</param>
    /// <param name="id">稳定业务键，默认取子节点名称。</param>
    /// <param name="color">标注颜色。</param>
    public void Bind(NodePath node, string id, Color color)
    {
        m_Node = node;
        m_Id = id;
        m_DrawColor = color;
    }
}
