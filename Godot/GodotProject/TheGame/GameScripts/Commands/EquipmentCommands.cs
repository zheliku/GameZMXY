using System;
using GameConfig.Equipment;
using GameLogic.Architecture;
using GameLogic.Level;

namespace GameLogic.Equipment;

/// <summary>
/// 玩家请求穿戴一件背包装备；当前关卡接受操作时才交给装备系统。
/// </summary>
/// <param name="heroId">本次操作的参战英雄编号。</param>
/// <param name = "instanceId">背包中仍然存在的实例编号。</param>
public sealed class EquipEquipmentCommand(int heroId, Guid instanceId) : AbstractCommand<bool>
{
    /// <inheritdoc/>
    protected override bool OnExecute()
    {
        LevelModel level = this.GetModel<LevelModel>();
        if (level is not { SessionActive: true } || level.Outcome.Value != LevelOutcome.Running)
        {
            return false;
        }

        return this.GetSystem<EquipmentSystem>().TryEquip(heroId, instanceId);
    }
}

/// <summary>
/// 玩家请求卸下一个部位；结局或离关后拒绝迟到操作。
/// </summary>
/// <param name="heroId">本次操作的参战英雄编号。</param>
/// <param name = "slot">要卸下的部位。</param>
public sealed class UnequipEquipmentCommand(int heroId, EquipmentSlot slot) : AbstractCommand<bool>
{
    /// <inheritdoc/>
    protected override bool OnExecute()
    {
        LevelModel level = this.GetModel<LevelModel>();
        if (level is not { SessionActive: true } || level.Outcome.Value != LevelOutcome.Running)
        {
            return false;
        }

        return this.GetSystem<EquipmentSystem>().TryUnequip(heroId, slot);
    }
}

/// <summary>
/// 玩家请求出售一件装备；实例归属与售价规则由装备系统校验。
/// </summary>
/// <param name="heroId">本次出售的资产所属英雄编号。</param>
/// <param name = "instanceId">本次明确选择的背包实例。</param>
public sealed class SellEquipmentCommand(int heroId, Guid instanceId) : AbstractCommand<bool>
{
    /// <inheritdoc/>
    protected override bool OnExecute()
    {
        LevelModel level = this.GetModel<LevelModel>();
        if (level is not { SessionActive: true } || level.Outcome.Value != LevelOutcome.Running)
        {
            return false;
        }

        return this.GetSystem<EquipmentSystem>().TrySell(heroId, instanceId);
    }
}

/// <summary>
/// 玩家确认按品质批量出售；结束后的会话不接受交易。
/// </summary>
/// <param name="heroId">本次出售的资产所属英雄编号。</param>
/// <param name = "quality">确认出售的品质，不包含已穿戴装备。</param>
public sealed class SellEquipmentByQualityCommand(int heroId, EquipmentQuality quality) : AbstractCommand<int>
{
    /// <inheritdoc/>
    protected override int OnExecute()
    {
        LevelModel level = this.GetModel<LevelModel>();
        if (level is not { SessionActive: true } || level.Outcome.Value != LevelOutcome.Running)
        {
            return 0;
        }

        return this.GetSystem<EquipmentSystem>().SellByQuality(heroId, quality);
    }
}

/// <summary>
/// 玩家请求赠送背包装备；当前关卡运行时交给装备系统校验归属与容量。
/// </summary>
/// <param name="fromHeroId">发送者。</param>
/// <param name="toHeroId">接收者。</param>
/// <param name="instanceId">要移交的背包实例。</param>
public sealed class GiftEquipmentCommand(int fromHeroId, int toHeroId, Guid instanceId) : AbstractCommand<bool>
{
    /// <inheritdoc/>
    protected override bool OnExecute()
    {
        LevelModel level = this.GetModel<LevelModel>();
        if (level is not { SessionActive: true } || level.Outcome.Value != LevelOutcome.Running)
        {
            return false;
        }

        return this.GetSystem<EquipmentSystem>().TryGift(fromHeroId, toHeroId, instanceId);
    }
}
