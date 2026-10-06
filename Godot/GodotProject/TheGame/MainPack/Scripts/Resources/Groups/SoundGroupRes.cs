using Godot;

/// <summary>声音分组资源。</summary>
[GlobalClass]
public partial class SoundGroupRes : Resource
{
    /// <summary>声音分组配置。</summary>
    [Export]
    public SoundGroup[] SoundGroups;
}
