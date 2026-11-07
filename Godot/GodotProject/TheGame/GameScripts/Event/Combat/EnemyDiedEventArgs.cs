using GameConfig.Enemy;
using GameConfig.Loot;
using GameFramework;
using GameFramework.Event;
using Godot;

namespace GameLogic.Event.Combat;

/// <summary>
/// 怪物死亡后的池化通知；刷怪服务归还名额，关卡按只读配置、死亡位置和配方经验生成掉落。
/// </summary>
public sealed class EnemyDiedEventArgs : GameEventArgs
{
    /// <summary>
    /// GGF 事件编号。
    /// </summary>
    public static readonly int EventId = typeof(EnemyDiedEventArgs).GetHashCode();

    /// <inheritdoc />
    public override int Id => EventId;

    /// <summary>
    /// 本次死亡的运行实体编号。
    /// </summary>
    public int EntityId { get; private set; }

    /// <summary>
    /// 本次击败经验，已应用配方覆盖，不包含节点或配置引用。
    /// </summary>
    public int ExperienceReward { get; private set; }

    /// <summary>
    /// 特殊敌人的每球灵魂覆盖值；为空时使用 Config.SoulPerOrb。
    /// </summary>
    public int? SoulPerOrbOverride { get; private set; }

    /// <summary>
    /// 特殊敌人的装备掉落概率覆盖值；为空时使用 Config.EquipmentDropChance。
    /// </summary>
    public float? EquipmentDropChanceOverride { get; private set; }

    /// <summary>
    /// 特殊敌人选择的装备池覆盖值；为空时使用 Config.EquipmentDropPoolId。
    /// </summary>
    public EquipmentDropPoolId? EquipmentDropPoolIdOverride { get; private set; }

    /// <summary>
    /// 本次死亡是否结束配方名额；变身中的形态只结算收益，后继形态继续持有名额。
    /// </summary>
    public bool EncounterComplete { get; private set; }

    /// <summary>
    /// 只读掉落配置；不携带即将回池的怪物节点。
    /// </summary>
    public EnemyConfig Config { get; private set; }

    /// <summary>
    /// 死亡时的世界坐标，不随尸体动画改变。
    /// </summary>
    public Vector2 Position { get; private set; }

    /// <summary>
    /// 从引用池创建通知，分发结束后由事件池归还。
    /// </summary>
    /// <param name="entityId">运行实体编号。</param>
    /// <param name="experienceReward">本次生成确定的击败经验。</param>
    /// <param name="config">怪物只读配置。</param>
    /// <param name="position">死亡位置。</param>
    /// <param name="soulPerOrbOverride">特殊的每球灵魂覆盖值；为空时使用基础配置。</param>
    /// <param name="equipmentDropChanceOverride">特殊的装备概率覆盖值；为空时使用基础配置。</param>
    /// <param name="encounterComplete">是否归还当前配方名额并允许目标出口开放。</param>
    /// <param name="equipmentDropPoolIdOverride">特殊的装备池覆盖值；为空时使用基础配置。</param>
    /// <returns>仅在本次分发期间有效的参数。</returns>
    public static EnemyDiedEventArgs Create(int entityId, int experienceReward, EnemyConfig config,
        Vector2 position, int? soulPerOrbOverride = null, float? equipmentDropChanceOverride = null,
        bool encounterComplete = true, EquipmentDropPoolId? equipmentDropPoolIdOverride = null)
    {
        EnemyDiedEventArgs args = ReferencePool.Acquire<EnemyDiedEventArgs>();
        args.EntityId = entityId;
        args.ExperienceReward = experienceReward;
        args.Config = config;
        args.Position = position;
        args.SoulPerOrbOverride = soulPerOrbOverride;
        args.EquipmentDropChanceOverride = equipmentDropChanceOverride;
        args.EquipmentDropPoolIdOverride = equipmentDropPoolIdOverride;
        args.EncounterComplete = encounterComplete;
        return args;
    }

    /// <summary>
    /// 回池前清除本次通知。
    /// </summary>
    public override void Clear()
    {
        EntityId = 0;
        ExperienceReward = 0;
        Config = null;
        Position = default;
        SoulPerOrbOverride = null;
        EquipmentDropChanceOverride = null;
        EquipmentDropPoolIdOverride = null;
        EncounterComplete = true;
    }
}
