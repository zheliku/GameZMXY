#if TOOLS
using System;
using System.Reflection;
using Godot;

/// <summary>调用现有节点池扫描器的命令行入口；不维护另一份扫描或生成逻辑。</summary>
public partial class RegenerateNodePools : SceneTree
{
    /// <summary>扫描当前程序集的 IPoolable 场景并保存现有池配置。</summary>
    public override void _Initialize()
    {
        try
        {
            var scanner = new GodotGameFramework.Editor.NodePoolInspectorPlugin();
            var resource = ResourceLoader.Load<Resource>("res://TheGame/MainPack/Resources/NodePoolConfigRes.tres");
            typeof(GodotGameFramework.Editor.NodePoolInspectorPlugin)
                .GetMethod("OnScanPressed", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(scanner, [resource]);
            scanner.Dispose();
            Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); Quit(1); }
    }
}

#endif
