using Godot;

/// <summary>单个声音分组配置。</summary>
[GlobalClass]
public partial class SoundGroup : Resource
{
    /// <summary>声音组名称。</summary>
    [Export] // 音效组名称
    public string Name;
    /// <summary>声音代理数量。</summary>
    [Export] // 音效组代理数量
    public int AgentCounts;
    /// <summary>是否避免被相同优先级的声音替换。</summary>
    [Export] // 音效组避免被同优先级音效替换
    public bool AvoidBeingReplacedBySamePriority;
}
