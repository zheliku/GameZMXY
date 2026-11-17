using GameLogic.Architecture;
using Godot;

namespace GameLogic.Display;

/// <summary>
/// 应用共用的显示配置：每个平台只读取一套构图基准，所有流程使用项目统一的显示设置。
/// </summary>
public sealed class DisplaySystem : AbstractSystem
{
    /// <summary>当前平台菜单的逻辑设计尺寸，与窗口实际输出像素分开。</summary>
    public Vector2 MenuDesignSize
    {
        get; private set;
    }

    /// <summary>由应用准备流程注册，不创建窗口、节点或独立配置资源。</summary>
    /// <param name="architecture">应用共享架构。</param>
    public DisplaySystem(IArchitecture architecture) : base(architecture)
    {
    }

    /// <summary>按平台特性读取审定构图，初始化后保持不变；不修改窗口显示设置。</summary>
    protected override void OnInit()
    {
        Vector2I referenceSize = ProjectSettings.GetSettingWithOverride("game/ui/reference_size").AsVector2I();
        int logicalHeight = ProjectSettings.GetSetting("display/window/size/viewport_height").AsInt32();
        MenuDesignSize = new Vector2(Mathf.CeilToInt(referenceSize.X * (float)logicalHeight / referenceSize.Y), logicalHeight);
    }
}
