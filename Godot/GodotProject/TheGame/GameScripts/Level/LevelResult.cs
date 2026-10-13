namespace GameLogic.Level;

/// <summary>关卡结局时冻结的本局成绩；不写入玩家存档，也不保留场景节点。</summary>
/// <param name="Outcome">成功或失败。</param>
/// <param name="ElapsedSeconds">实际游玩秒数，不包含暂停、加载与结算。</param>
/// <param name="HealthPercent">结局时剩余生命百分比。</param>
/// <param name="DefeatedMonsters">本局实际击败的敌人数。</param>
/// <param name="Experience">本局发放的经验。</param>
public sealed record LevelResult(
    LevelOutcome Outcome,
    double ElapsedSeconds,
    int HealthPercent,
    int DefeatedMonsters,
    int Experience);
