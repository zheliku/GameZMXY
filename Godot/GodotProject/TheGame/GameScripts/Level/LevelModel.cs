using System;
using GameConfig.Level;
using GameLogic.Architecture;

namespace GameLogic.Level;

/// <summary>Architecture 拥有的本局状态；不持有场景节点，业务系统借用并修改。</summary>
public sealed class LevelModel : AbstractModel
{
    private GameData m_EntrySnapshot; // 进关时间边界的独立副本，用于放弃回滚。
    private double m_ElapsedSeconds; // 本局运行秒数，暂停和结局后停止。
    private int m_DefeatedMonsterCount; // 本局击败数量。
    private int m_AwardedExperience; // 本局实际奖励经验。

    /// <summary>当前关卡定义主键。</summary>
    public int LevelId { get; private set; }

    /// <summary>是否已经开始且尚未清理本局。</summary>
    public bool SessionActive { get; private set; }

    /// <summary>本局互斥结局。</summary>
    public LevelOutcome Outcome { get; private set; } = LevelOutcome.Running;

    /// <summary>结局时冻结的成绩；进行中为空。</summary>
    public LevelResult Result { get; private set; }

    /// <summary>清波后到下一场开战前的可前进窗口。</summary>
    public bool TravelAvailable { get; private set; }

    /// <summary>可前进窗口发生变化。</summary>
    public event Action TravelAvailableChanged;

    /// <summary>结局确定后通知，流程据此导航。</summary>
    public event Action<LevelOutcome> Ended;

    /// <summary>本模型拥有的阶段状态，仅供关卡系统与场景控制器协作。</summary>
    internal LevelStageSequence Sequence { get; private set; }

    /// <summary>未结算时可用于恢复档案的时间快照。</summary>
    internal GameData EntrySnapshot => m_EntrySnapshot;

    /// <summary>建立本局阶段状态；场景验证由 ViewController 执行。</summary>
    /// <param name="config">共享配置定义。</param>
    internal void Prepare(LevelConfig config)
    {
        LevelId = config.LevelId;
        Sequence = new LevelStageSequence(config.Stages);
    }

    /// <summary>捕获已创建的进关快照并开始统计。</summary>
    /// <param name="snapshot">本局独立回滚副本。</param>
    internal void Begin(GameData snapshot)
    {
        if (SessionActive)
            throw new InvalidOperationException("关卡会话已经开始。");
        m_EntrySnapshot = snapshot;
        m_ElapsedSeconds = 0;
        m_DefeatedMonsterCount = m_AwardedExperience = 0;
        Result = null;
        Outcome = LevelOutcome.Running;
        SessionActive = true;
        SetTravelAvailable(false);
        Sequence.Begin();
    }

    /// <summary>累计本次击败事实和实际经验。</summary>
    /// <param name="experience">实际发放经验。</param>
    internal void RecordDefeat(int experience)
    {
        m_DefeatedMonsterCount++;
        m_AwardedExperience += experience;
    }

    /// <summary>推进游戏时间；结局后忽略后续帧。</summary>
    /// <param name="delta">游戏秒数。</param>
    internal void Tick(double delta)
    {
        if (SessionActive && Outcome == LevelOutcome.Running)
            m_ElapsedSeconds += delta;
    }

    /// <summary>冻结成绩并发布一次结局，释放已提交的回滚副本。</summary>
    /// <param name="outcome">通关或失败。</param>
    /// <param name="healthPercent">结局时生命百分比。</param>
    internal void Complete(LevelOutcome outcome, int healthPercent)
    {
        Outcome = outcome;
        Result = new LevelResult(outcome, m_ElapsedSeconds, healthPercent,
            m_DefeatedMonsterCount, m_AwardedExperience);
        m_EntrySnapshot = null;
        SetTravelAvailable(false);
        Ended?.Invoke(outcome);
    }

    /// <summary>结束作用域；未提交的本局标记为放弃。</summary>
    internal void Stop()
    {
        bool abandoned = m_EntrySnapshot != null;
        m_EntrySnapshot = null;
        SessionActive = false;
        Sequence?.Stop();
        SetTravelAvailable(false);
        if (abandoned)
        {
            Outcome = LevelOutcome.Abandoned;
            Ended?.Invoke(Outcome);
        }
    }

    /// <summary>原子更新前进窗口并在变化时通知观察者。</summary>
    /// <param name="available">是否允许前进。</param>
    internal void SetTravelAvailable(bool available)
    {
        if (TravelAvailable == available)
            return;
        TravelAvailable = available;
        TravelAvailableChanged?.Invoke();
    }
}

/// <summary>一次关卡会话的互斥结局。</summary>
public enum LevelOutcome
{
    /// <summary>关卡进行中。</summary>
    Running,

    /// <summary>出口交互已提交检查点。</summary>
    Cleared,

    /// <summary>英雄死亡已提交检查点。</summary>
    Defeated,

    /// <summary>中途离开，收益已回滚。</summary>
    Abandoned,
}
