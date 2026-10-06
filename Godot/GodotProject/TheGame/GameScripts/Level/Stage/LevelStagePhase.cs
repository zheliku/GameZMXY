namespace GameLogic.Level;

/// <summary>关卡会话的互斥阶段状态。</summary>
public enum LevelStagePhase
{
    /// <summary>已初始化，尚未开放玩法事件。</summary>
    Ready,
    /// <summary>通路已开放，等待当前阶段的启动条件。</summary>
    Travelling,
    /// <summary>区域已锁定，等待配方发完和敌人清空。</summary>
    Fighting,
    /// <summary>最后一个阶段已清除。</summary>
    Completed,
    /// <summary>会话已停止。</summary>
    Stopped,
}
