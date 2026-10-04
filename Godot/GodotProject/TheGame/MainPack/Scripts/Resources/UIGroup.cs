using Godot;
/// <summary>单个界面分组配置。</summary>
[GlobalClass]
public partial class UIGroup : Resource
{
    /// <summary>分组名称。</summary>
    [Export]
    public string Name;
    /// <summary>分组显示层级。</summary>
    [Export]
    public int Depth;
}
