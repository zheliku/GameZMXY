using Godot;
using System;
/// <summary>单个实体对象池分组配置。</summary>
[GlobalClass]
public partial class EntityGroup : Resource
{
    /// <summary>资源组名称。</summary>
    [Export] // 资源组名称
    public string Name;
    /// <summary>池实例自动释放检查间隔（秒）。</summary>
    [Export] // 资源组释放间隔
    public float ReleaseInterval;
    /// <summary>对象池容量上限。</summary>
    [Export] // 资源组容量
    public int Capacity;
    /// <summary>池实例闲置过期时间（秒）。</summary>
    [Export] // 资源组过期时间
    public float ExpireTime;
    /// <summary>资源组优先级。</summary>
    [Export] // 资源组优先级
    public int Priority;
}
