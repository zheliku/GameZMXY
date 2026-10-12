using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event.Combat;

/// <summary>怪物生命归零后的池化通知；携带本次生成确定的经验，刷怪服务据此归还名额。</summary>
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

    /// <summary>从引用池创建通知，分发结束后由事件池归还。</summary>
    /// <param name="entityId">运行实体编号。</param>
    /// <param name="experienceReward">本次生成确定的击败经验。</param>
    /// <returns>仅在本次分发期间有效的参数。</returns>
    public static MonsterDiedEventArgs Create(int entityId, int experienceReward)
    {
        var args = ReferencePool.Acquire<MonsterDiedEventArgs>();
        args.EntityId = entityId;
        args.ExperienceReward = experienceReward;
        return args;
    }

    /// <summary>回池前清除本次通知。</summary>
    public override void Clear()
    {
        EntityId = 0;
        ExperienceReward = 0;
    }
}
