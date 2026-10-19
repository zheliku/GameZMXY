using System;
using GameConfig.Equipment;
using GameLogic.Architecture;
using GameLogic.Level;

namespace GameLogic.Equipment;

/// <summary>玩家请求穿戴一件背包装备；当前关卡接受操作时才交给装备系统。</summary>
/// <param name="instanceId">背包中仍然存在的实例编号。</param>
public sealed class EquipEquipmentCommand(Guid instanceId) : AbstractCommand<bool>
{
    /// <inheritdoc />
    protected override bool OnExecute()
    {
        var level = this.GetModel<LevelModel>();
        return level is { SessionActive: true } && level.Outcome.Value == LevelOutcome.Running &&
               this.GetSystem<EquipmentSystem>().TryEquip(instanceId);
    }
}

/// <summary>玩家请求卸下一个部位；结局或离关后拒绝迟到操作。</summary>
/// <param name="slot">要卸下的部位。</param>
public sealed class UnequipEquipmentCommand(EquipmentSlot slot) : AbstractCommand<bool>
{
    /// <inheritdoc />
    protected override bool OnExecute()
    {
        var level = this.GetModel<LevelModel>();
        return level is { SessionActive: true } && level.Outcome.Value == LevelOutcome.Running &&
               this.GetSystem<EquipmentSystem>().TryUnequip(slot);
    }
}

/// <summary>玩家请求出售一件装备；实例归属与售价规则由装备系统校验。</summary>
/// <param name="instanceId">本次明确选择的背包实例。</param>
public sealed class SellEquipmentCommand(Guid instanceId) : AbstractCommand<bool>
{
    /// <inheritdoc />
    protected override bool OnExecute()
    {
        var level = this.GetModel<LevelModel>();
        return level is { SessionActive: true } && level.Outcome.Value == LevelOutcome.Running &&
               this.GetSystem<EquipmentSystem>().TrySell(instanceId);
    }
}

/// <summary>玩家确认按品质批量出售；结束后的会话不接受交易。</summary>
/// <param name="quality">确认出售的品质，不包含已穿戴装备。</param>
public sealed class SellEquipmentByQualityCommand(EquipmentQuality quality) : AbstractCommand<int>
{
    /// <inheritdoc />
    protected override int OnExecute()
    {
        var level = this.GetModel<LevelModel>();
        return level is { SessionActive: true } && level.Outcome.Value == LevelOutcome.Running
            ? this.GetSystem<EquipmentSystem>().SellByQuality(quality)
            : 0;
    }
}
