
namespace GameLogic.Procedure.FlowData;

/// <summary>流程导航参数的显式键；Model、System 与 Utility直接按类型访问。</summary>
public static class FlowKeys
{
    /// <summary>标题提交的存档格号和稳定编号，使用 SaveSelectionVariable。</summary>
    public const string SaveSelection = "SaveSelection";

    /// <summary>地图提交的关卡配置主键，使用 GGF VarInt32。</summary>
    public const string SelectedLevelId = "SelectedLevelId";
}
