namespace GameLogic.Monsters.AI;

/// <summary>单个怪物独占的 AI 数据；目标节点仍归实体，招式定义归 ConfigurationModel。</summary>
public sealed class MonsterAiData
{
    /// <summary>为本怪物分配逐招冷却，不复制共享攻击参数。</summary>
    /// <param name="attackCount">共享攻击参数的数量。</param>
    public MonsterAiData(int attackCount)
    {
        Cooldowns = new float[attackCount];
    }

    /// <summary>AI 写入、身体消费的水平移动意图。</summary>
    public int MoveIntent { get; internal set; }
    /// <summary>等待身体状态机提交的攻击下标，-1 表示没有请求。</summary>
    public int AttackRequest { get; internal set; } = -1;
    /// <summary>由 MonsterAiSystem 推进的独立逐招冷却，单位秒。</summary>
    internal float[] Cooldowns { get; }
}

