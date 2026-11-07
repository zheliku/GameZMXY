using GameConfig.Entity;

namespace GameLogic.Battle.Attacks;

/// <summary>
/// 一次攻击的来源身份快照；只保存编号，施放者或效果回收后仍可追溯。
/// </summary>
/// <param name="OwnerEntityId">伤害归属的GGF实体编号；环境伤害为零。</param>
/// <param name="CasterType">实际施放实体的配置身份；分身等非GGF节点为None。</param>
/// <param name="CasterInstanceId">实际施放节点的Godot实例编号。</param>
/// <param name="EmitterInstanceId">产生本次判定的身体或效果节点实例编号。</param>
public readonly record struct AttackSource(int OwnerEntityId, EntityId CasterType,
    ulong CasterInstanceId, ulong EmitterInstanceId);
