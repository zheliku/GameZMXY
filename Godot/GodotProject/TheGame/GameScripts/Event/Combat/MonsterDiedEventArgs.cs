using GameFramework;
using GameFramework.Event;
using GameConfig.Monster;
using Godot;
using GameConfig.Loot;

namespace GameLogic.Event.Combat;

/// <summary>怪物死亡后的池化通知；刷怪服务归还名额，关卡按只读配置、死亡位置和配方经验生成掉落。</summary>
public sealed class MonsterDiedEventArgs : GameEventArgs
{
    /// <summary>GGF 事件编号。</summary>
    public static readonly int EventId = typeof(MonsterDiedEventArgs).GetHashCode();

    /// <inheritdoc />
    public override int Id => EventId;

    /// <summary>本次死亡的运行实体编号。</summary>
    public int EntityId { get; private set; }

    /// <summary>本次击败经验，已应用配方覆盖，不包含节点或配置引用。</summary>
    public int ExperienceReward { get; private set; }

    /// <summary>只读掉落配置；不携带即将回池的怪物节点。</summary>
    public MonsterConfig Config { get; private set; }

    /// <summary>本次配方确定的装备池，已解析 UseMonster。</summary>
    public EquipmentDropPoolId EquipmentDropPoolId { get; private set; }

    /// <summary>死亡时的世界坐标，不随尸体动画改变。</summary>
    public Vector2 Position { get; private set; }

    /// <summary>从引用池创建通知，分发结束后由事件池归还。</summary>
    /// <param name="entityId">运行实体编号。</param>
    /// <param name="experienceReward">本次生成确定的击败经验。</param>
    /// <param name="config">怪物只读配置。</param>
    /// <param name="position">死亡位置。</param>
    /// <param name="equipmentDropPoolId">本次确定的装备池。</param>
    /// <returns>仅在本次分发期间有效的参数。</returns>
    public static MonsterDiedEventArgs Create(int entityId, int experienceReward, MonsterConfig config,
        Vector2 position, EquipmentDropPoolId equipmentDropPoolId)
    {
        var args = ReferencePool.Acquire<MonsterDiedEventArgs>();
        args.EntityId = entityId;
        args.ExperienceReward = experienceReward;
        args.Config = config;
        args.Position = position;
        args.EquipmentDropPoolId = equipmentDropPoolId;
        return args;
    }

    /// <summary>回池前清除本次通知。</summary>
    public override void Clear()
    {
        EntityId = 0;
        ExperienceReward = 0;
        Config = null;
        Position = default;
        EquipmentDropPoolId = EquipmentDropPoolId.None;
    }
}
