using GameLogic.Architecture;

namespace GameLogic.Level;

/// <summary>GGF 流程作用域拥有的本局数据；迁移、计时和结算由 LevelSystem 执行。</summary>
public sealed class LevelModel : AbstractModel
{
    /// <summary>当前关卡定义主键。</summary>
    public int LevelId { get; internal set; }

    /// <summary>是否已经开始且尚未清理本局。</summary>
    public bool SessionActive { get; internal set; }

    /// <summary>本局互斥结局；关联成绩先提交，结局最后发布。</summary>
    public BindableProperty<LevelOutcome> Outcome { get; } = new(LevelOutcome.Running);

    /// <summary>结局时冻结的成绩；进行中为空。</summary>
    public LevelResult Result { get; internal set; }

    /// <summary>清波后到下一场开战前的可前进窗口。</summary>
    public BindableProperty<bool> TravelAvailable { get; } = new();

    /// <summary>本模型拥有的阶段状态，仅供关卡系统与场景控制器协作。</summary>
    internal LevelStageSequence Sequence { get; set; }

    /// <summary>进关时的独立档案快照，未结算离开时用于回滚。</summary>
    internal GameData EntrySnapshot { get; set; }

    /// <summary>本局运行秒数，暂停和结局后停止。</summary>
    internal double ElapsedSeconds { get; set; }

    /// <summary>本局击败数量。</summary>
    internal int DefeatedMonsterCount { get; set; }

    /// <summary>本局实际发放经验。</summary>
    internal int AwardedExperience { get; set; }
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
