using Godot;
using System;
/// <summary>界面分组资源。</summary>
[GlobalClass]
public partial class UIGroupRes : Resource
{
    /// <summary>界面分组配置。</summary>
    [Export]
    public UIGroup[] Groups;
}
