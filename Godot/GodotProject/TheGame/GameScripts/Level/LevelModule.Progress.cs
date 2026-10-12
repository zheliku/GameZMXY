using System;
using System.Threading.Tasks;
using GameLogic.Battle.Attributes;
using GameLogic.Entity;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Save.Data;
using GodotGameFramework;

namespace GameLogic.Level;

/// <summary>同一关卡模块的收益部分：捕获进关快照、奖励经验并处理结局；不另建运行对象。</summary>
public partial class LevelModule
{
    private PlayerProfile m_Profile; // 本槽的持久状态；模块只借用，不拥有生命周期。
    private SaveModule m_Save; // 本槽唯一检查点入口。
    private HeroProgression m_Progression; // 出战英雄的持久进度。
    private GameData m_EntrySnapshot; // 尚未结算时的内存回滚快照。
    private int m_DefeatedMonsterCount; // 本关统计，仅日志使用，不写存档。
    private int m_AwardedExperience; // 本关实际奖励经验，仅日志使用。

    /// <summary>本次结局；Running 时接收奖励。</summary>
    public LevelOutcome Outcome { get; private set; } = LevelOutcome.Running;

    /// <summary>已提交的检查点写盘任务；尚未提交时为空。</summary>
    public Task<bool> CheckpointWrite { get; private set; }

    /// <summary>结局确定后通知一次，流程决定下一页面。</summary>
    public event Action<LevelOutcome> Ended;

    /// <summary>绑定持久状态并捕获进关快照，在刷怪开始前订阅英雄死亡。</summary>
    /// <param name="context">读档流程建立的档案作用域。</param>
    /// <exception cref="ArgumentNullException">作用域为空。</exception>
    /// <exception cref="InvalidOperationException">出战实体与档案不一致。</exception>
    private void BeginProgress(GameContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (m_Player == null || m_Player.HeroId != context.Profile.ActiveHeroId)
            throw new InvalidOperationException("关卡实体必须与档案的出战英雄一致。");
        m_Profile = context.Profile;
        m_Save = context.Save;
        m_Progression = m_Profile.ActiveHero;
        m_EntrySnapshot = ProfileSaveHelper.CaptureSaveData(m_Profile);
        m_DefeatedMonsterCount = m_AwardedExperience = 0;
        CheckpointWrite = null;
        Outcome = LevelOutcome.Running;
        m_ExitRequested = false;
        m_LootRandom.Randomize();
        m_Player.Died += OnHeroDied;
    }

    /// <summary>击败即结算固定经验；升级时刷新装配、补满生命魔法并播放脚底升级动画。</summary>
    /// <param name="experienceReward">本次击败的经验奖励。</param>
    private void AwardExperience(int experienceReward)
    {
        if (Outcome != LevelOutcome.Running) return;
        if (experienceReward <= 0 || !m_Player.IsAlive) return;
        m_AwardedExperience += experienceReward;
        if (m_Progression.AddExperience(experienceReward) > 0)
        {
            m_Player.ApplyLoadout(HeroLoadout.Create(m_Progression.HeroId, m_Progression.Level,
                ConfigSystem.Instance.Tables.TbHeroGrowthConfig), refill: true);
            m_Player.PlayLevelUp();
        }
    }

    /// <summary>英雄死亡时保留本关收益并提交检查点。</summary>
    /// <param name="actor">本关的出战英雄。</param>
    private void OnHeroDied(ActorEntity actor) => SubmitOutcome(LevelOutcome.Defeated);

    /// <summary>接受第一个结局，停止奖励，提交检查点，然后直接通知流程。</summary>
    /// <param name="outcome">通关或死亡。</param>
    private void SubmitOutcome(LevelOutcome outcome)
    {
        if (Outcome != LevelOutcome.Running) return;
        Outcome = outcome;
        ClearLoot();
        Detach();
        m_EntrySnapshot = null;
        CheckpointWrite = m_Save.SaveCheckpointAsync(m_Profile);
        Log.Info("[LevelModule] 关卡 {0}：{1}，击败 {2}，经验 {3}",
            m_LevelId, outcome, m_DefeatedMonsterCount, m_AwardedExperience);
        Ended?.Invoke(outcome);
    }

    /// <summary>未结算离开时恢复进关档案；已提交和初始化失败不重复操作。</summary>
    private void AbandonProgress()
    {
        Detach();
        if (m_EntrySnapshot == null) return;
        ProfileSaveHelper.RestoreCheckpoint(m_Profile, m_EntrySnapshot);
        m_EntrySnapshot = null;
        Outcome = LevelOutcome.Abandoned;
        Ended?.Invoke(Outcome);
    }

    /// <summary>解除英雄的纯 C# 订阅；框架关停时不访问节点或写盘。</summary>
    public void Detach()
    {
        if (m_Player != null) m_Player.Died -= OnHeroDied;
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
