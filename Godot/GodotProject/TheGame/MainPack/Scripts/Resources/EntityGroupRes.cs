using Godot;
using System;
/// <summary>实体对象池分组资源。</summary>
[GlobalClass]
public partial class EntityGroupRes : Resource
{
    /// <summary>实体分组配置。</summary>
    [Export]
    public EntityGroup[] EntityGroups;
}
