using System;
using System.Threading.Tasks;
using GameConfig.Hero;
using GameConfig.Monster;
using GameLogic.Battle.Attributes;
using GameLogic.Entity;
using GameLogic.Entity.Heroes;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Save.Data;
using GodotGameFramework;

namespace GameLogic.Session;

/// <summary>结算本关经验；通关或死亡保存检查点，放弃则恢复进关档案。场景和实体生命周期归流程。</summary>
public sealed class LevelRewardTransaction
{
    private readonly PlayerProfile m_Profile; // 本槽持久状态。
    private readonly SaveService m_Save; // 本槽检查点入口。
    private readonly LevelController m_Level; // 本关事实来源。
    private readonly HeroEntity m_Hero; // 接收升级属性的出战实体。
    private readonly HeroProgression m_Progression; // 出战英雄的持久进度。
    private readonly TbMonsterConfig m_Monsters; // 击败经验配置。
    private readonly TbHeroGrowthConfig m_Growth; // 升级后的基础属性配置。
    private readonly GameData m_EntrySnapshot; // 进关快照，仅用于内存回滚。

    /// <summary>本次结局；Running 时才接收奖励。</summary>
    public LevelOutcome Outcome { get; private set; } = LevelOutcome.Running;

    /// <summary>本关已击败的怪物数，不写入存档。</summary>
    public int DefeatedMonsterCount { get; private set; }

    /// <summary>本关发放的经验总量，不写入存档。</summary>
    public int AwardedExperience { get; private set; }

    /// <summary>已提交的检查点写盘任务；未提交时为空。</summary>
    public Task<bool> CheckpointWrite { get; private set; }

    /// <summary>结局确定后触发一次；此时已经退订关卡与实体事件。</summary>
    public event Action<LevelOutcome> Ended;

    /// <summary>捕获进关档案并开始接收本关击败与结局通知。</summary>
    /// <param name="profile">本槽档案。</param>
    /// <param name="save">本槽存档服务。</param>
    /// <param name="level">已开始会话的关卡。</param>
    /// <param name="hero">已显示的出战英雄。</param>
    /// <param name="monsters">怪物经验配置。</param>
    /// <param name="growth">英雄成长配置。</param>
    /// <exception cref="ArgumentNullException">任一依赖为空。</exception>
    /// <exception cref="InvalidOperationException">实体不是档案的出战英雄。</exception>
    public LevelRewardTransaction(PlayerProfile profile, SaveService save, LevelController level,
        HeroEntity hero, TbMonsterConfig monsters, TbHeroGrowthConfig growth)
    {
        m_Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        m_Save = save ?? throw new ArgumentNullException(nameof(save));
        m_Level = level ?? throw new ArgumentNullException(nameof(level));
        m_Hero = hero ?? throw new ArgumentNullException(nameof(hero));
        m_Monsters = monsters ?? throw new ArgumentNullException(nameof(monsters));
        m_Growth = growth ?? throw new ArgumentNullException(nameof(growth));
        if (hero.HeroId != profile.ActiveHeroId)
            throw new InvalidOperationException($"英雄 {hero.HeroId} 不是档案的出战英雄。");

        // 只保存真实所需的依赖，避免关卡事务重新持有整个玩家会话或配置总表。
        m_Progression = profile.ActiveHero;
        m_EntrySnapshot = ProfileMapper.CaptureSaveData(profile);
        m_Level.MonsterDefeated += OnMonsterDefeated;
        m_Level.Completed += OnCompleted;
        m_Hero.Died += OnHeroDied;
    }

    /// <summary>中途放弃或运行失败时恢复进关档案，不写盘；已经结束时无操作。</summary>
    public void Abandon()
    {
        if (Outcome != LevelOutcome.Running) return;
        ProfileMapper.RestoreCheckpoint(m_Profile, m_EntrySnapshot);
        Finish(LevelOutcome.Abandoned);
    }

    /// <summary>框架关停时解除订阅，不再操作即将释放的状态与存档服务。</summary>
    public void Detach() => Unsubscribe();

    /// <summary>发放击败经验；升级后更新实体基础属性并补满生命魔法。</summary>
    /// <param name="monsterId">MonsterConfig 主键，不是运行实体编号。</param>
    private void OnMonsterDefeated(int monsterId)
    {
        if (Outcome != LevelOutcome.Running) return;
        DefeatedMonsterCount++;
        MonsterConfig monster = m_Monsters.Get(monsterId);
        if (monster.AddExp <= 0 || !m_Hero.IsAlive) return;

        AwardedExperience += monster.AddExp;
        if (m_Progression.AddExperience(monster.AddExp) > 0)
            m_Hero.ApplyBattleSnapshot(HeroBattleSnapshot.Create(m_Progression.HeroId, m_Progression.Level, m_Growth), refill: true);
    }

    /// <summary>清波完成时保存本关收益。</summary>
    private void OnCompleted() => Submit(LevelOutcome.Cleared);

    /// <summary>死亡时保留本关收益并保存检查点。</summary>
    /// <param name="actor">死亡的出战英雄。</param>
    private void OnHeroDied(ActorEntity actor) => Submit(LevelOutcome.Defeated);

    /// <summary>只接受第一个结局，提交当前档案后结束事务。</summary>
    /// <param name="outcome">通关或死亡。</param>
    private void Submit(LevelOutcome outcome)
    {
        if (Outcome != LevelOutcome.Running) return;
        CheckpointWrite = m_Save.SaveCheckpointAsync(m_Profile);
        Log.Info("[LevelRewardTransaction] 关卡 {0}：{1}，击败 {2}，经验 {3}",
            m_Level.LevelId, outcome, DefeatedMonsterCount, AwardedExperience);
        Finish(outcome);
    }

    /// <summary>完成状态迁移并通知流程与窗口。</summary>
    /// <param name="outcome">已经确定的结局。</param>
    private void Finish(LevelOutcome outcome)
    {
        Outcome = outcome;
        Unsubscribe();
        Ended?.Invoke(outcome);
    }

    /// <summary>对称解除本关订阅，重复调用安全。</summary>
    private void Unsubscribe()
    {
        m_Level.MonsterDefeated -= OnMonsterDefeated;
        m_Level.Completed -= OnCompleted;
        m_Hero.Died -= OnHeroDied;
    }
}

/// <summary>关卡收益事务的结局。</summary>
public enum LevelOutcome
{
    /// <summary>仍在进行。</summary>
    Running,
    /// <summary>通关并提交检查点。</summary>
    Cleared,
    /// <summary>死亡并提交检查点。</summary>
    Defeated,
    /// <summary>中途放弃，已回滚收益。</summary>
    Abandoned,
}
