using GameFramework;

namespace GameLogic.Procedure.FlowData;

/// <summary>标题向读档流程一次性传递格号与稳定存档编号的池化变量。</summary>
public sealed class SaveSelectionVariable : Variable<(int SlotNumber, long UnitId)>
{
    /// <summary>创建选择结果，所有权交给流程状态机。</summary>
    /// <param name="slotNumber">从 1 开始的格号。</param>
    /// <param name="unitId">GGF 稳定编号，新格为 0。</param>
    /// <returns>池化流程变量。</returns>
    public static SaveSelectionVariable Create(int slotNumber, long unitId)
    {
        var variable = ReferencePool.Acquire<SaveSelectionVariable>();
        variable.Value = (slotNumber, unitId);
        return variable;
    }
}
