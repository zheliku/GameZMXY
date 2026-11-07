using GameFramework;

namespace GameLogic.Procedure.FlowData;

/// <summary>
/// 标题和选角向读档流程传递格号、稳定存档编号与新档角色身份的池化变量。
/// </summary>
public sealed class SaveSelectionVariable : Variable<(int SlotNumber, long UnitId, int HeroId)>
{
    /// <summary>
    /// 创建选择结果，所有权交给流程状态机。
    /// </summary>
    /// <param name="slotNumber">从 1 开始的格号。</param>
    /// <param name="unitId">GGF 稳定编号，新格为 0。</param>
    /// <param name="heroId">新档选中的英雄；已有档或尚未选角时为0。</param>
    /// <returns>池化流程变量。</returns>
    public static SaveSelectionVariable Create(int slotNumber, long unitId, int heroId = 0)
    {
        SaveSelectionVariable variable = ReferencePool.Acquire<SaveSelectionVariable>();
        variable.Value = (slotNumber, unitId, heroId);
        return variable;
    }
}
