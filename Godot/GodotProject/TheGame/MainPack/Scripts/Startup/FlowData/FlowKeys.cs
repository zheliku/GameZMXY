using GameLogic;
using GameLogic.Level;
using GameLogic.Profile;

namespace GameLogic.Startup.FlowData;

/// <summary>流程状态机共享数据键；对应的池化包装集中在本目录。</summary>
public static class FlowKeys
{
    /// <summary>启动期校验的共享经验曲线，使用 ExperienceCurveVariable。</summary>
    public const string ExperienceCurve = "ExperienceCurve";

    /// <summary>标题提交的存档格号和稳定编号，使用 SaveSelectionVariable。</summary>
    public const string SaveSelection = "SaveSelection";

    /// <summary>当前档案作用域，使用 GameContextVariable。</summary>
    public const string GameContext = "GameContext";

    /// <summary>地图提交的关卡配置主键，使用 GGF VarInt32。</summary>
    public const string SelectedLevelId = "SelectedLevelId";
}
