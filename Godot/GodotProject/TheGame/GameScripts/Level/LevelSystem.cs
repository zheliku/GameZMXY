using System;
using System.Threading.Tasks;
using GameConfig.Loot;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Config;
using GameLogic.Profile;
using GameLogic.Equipment;
using GameLogic.Save;
using GameLogic.Skills;
using GameLogic.Drops;

namespace GameLogic.Level;

/// <summary>关卡业务系统：借用独立注册的 LevelModel 和档案，执行奖励、提交与回滚；不持有节点。</summary>
public sealed class LevelSystem : AbstractSystem
{
    /// <summary>借用 Architecture 拥有的本局状态。</summary>
    private LevelModel LevelModel => this.GetModel<LevelModel>();

    /// <summary>本次结局提交的检查点写盘任务。</summary>
    public Task<bool> CheckpointWrite { get; private set; }

    /// <summary>绑定系统归属；模型由流程先行注册。</summary>
    /// <param name="architecture">所属游戏入口。</param>
    public LevelSystem(IArchitecture architecture) : base(architecture) { }

    /// <summary>系统移除时兜底结束未清理的业务事务。</summary>
    protected override void OnDeinit() => EndSession();

    /// <summary>按定义初始化本局阶段。</summary>
    /// <param name="levelId">关卡主键。</param>
    public void Prepare(int levelId)
    {
        var config = this.GetModel<ConfigurationModel>().Tables.TbLevelConfig.Get(levelId);
        LevelModel.LevelId = config.LevelId;
        LevelModel.Sequence = new LevelStageSequence(config.Stages);
    }

    /// <summary>验证出战身份并捕获进关回滚副本。</summary>
    /// <param name="heroId">实际显示的英雄身份。</param>
    public void StartSession(int heroId)
    {
        var profile = this.GetModel<ProfileModel>();
        if (heroId != profile.ActiveHeroId)
            throw new InvalidOperationException("关卡实体必须与档案的出战英雄一致。");
        if (LevelModel.SessionActive)
            throw new InvalidOperationException("关卡会话已经开始。");

        // 捕获独立回滚副本后再启动统计，重复开始不得覆盖原快照。
        LevelModel.EntrySnapshot = this.GetUtility<SaveDataUtility>().Capture(profile, this.GetModel<SkillModel>());
        CheckpointWrite = null;
        LevelModel.ElapsedSeconds = 0;
        LevelModel.DefeatedMonsterCount = LevelModel.AwardedExperience = 0;
        LevelModel.Result = null;
        LevelModel.SessionActive = true;
        LevelModel.TravelAvailable.Value = false;
        LevelModel.Sequence.Begin();
        LevelModel.Outcome.Value = LevelOutcome.Running;
    }

    /// <summary>记录击败并奖励经验，返回表现层是否需要播放升级。</summary>
    /// <param name="experienceReward">本次配方经验。</param>
    /// <returns>是否至少升了一级。</returns>
    public bool RecordDefeat(int experienceReward)
    {
        if (!LevelModel.SessionActive || LevelModel.Outcome.Value != LevelOutcome.Running)
            return false;
        int experience = Math.Max(0, experienceReward);
        LevelModel.DefeatedMonsterCount++;
        LevelModel.AwardedExperience += experience;
        return experience > 0 && this.GetSystem<ProfileSystem>().AddExperience(experience) > 0;
    }

    /// <summary>实际拾取时将装备加入本槽背包；结局后拒绝领取，满包不消耗地面物品。</summary>
    /// <param name="data">掉落时已抽定属性的装备实例。</param>
    /// <returns>是否完成领取。</returns>
    public bool TryCollectEquipment(EquipmentData data) =>
        LevelModel.SessionActive && LevelModel.Outcome.Value == LevelOutcome.Running
                            && this.GetSystem<ProfileSystem>().TryCollectEquipment(data);

    /// <summary>接触地面恢复品时执行其配置效果；满值仍消耗，未开始、死亡及结局后拒绝。</summary>
    /// <param name="id">恢复品定义。</param>
    /// <returns>本次已消费恢复品时为真，场景据此回收节点并播放反馈。</returns>
    public bool TryCollectPickup(PickupId id)
    {
        if (!LevelModel.SessionActive || LevelModel.Outcome.Value != LevelOutcome.Running ||
            this.GetModel<HeroModel>().IsDepleted)
            return false;
        var pickup = this.GetModel<ConfigurationModel>().Tables.TbPickupConfig.GetOrDefault(id);
        if (pickup == null)
            return false;
        var hero = this.GetSystem<HeroSystem>();
        switch (pickup.Effect)
        {
            case PickupEffect.Health:
                hero.Heal(pickup.RestoreAmount);
                break;
            case PickupEffect.Mana:
                hero.RestoreMp(pickup.RestoreAmount);
                break;
            default:
                throw new InvalidOperationException($"恢复品 {id} 的效果未实现：{pickup.Effect}。");
        }

        return true;
    }

    /// <summary>本关球体接触后发放收益；场景只确认接触和实例所有权，不执行奖励规则。</summary>
    /// <param name="reward">球体在生成时确定的收益，蓝球按接触时的魔法上限计算。</param>
    public void CollectLoot(LootReward reward)
    {
        if (!LevelModel.SessionActive || LevelModel.Outcome.Value != LevelOutcome.Running ||
            this.GetModel<HeroModel>().IsDepleted)
            return;
        switch (reward.Kind)
        {
            case LootKind.Soul:
                this.GetSystem<ProfileSystem>().AddSoul(reward.Amount);
                break;
            case LootKind.Musou:
                this.GetSystem<HeroSystem>().AddWsFromPickup(reward.Amount);
                break;
            case LootKind.Mana:
                int amount = this.GetSystem<LootSystem>().ManaAmount(this.GetModel<HeroModel>().MaxMp.Value);
                this.GetSystem<HeroSystem>().RestoreMp(amount);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(reward));
        }
    }

    /// <summary>接收有效游戏帧时间；暂停、结局和未开始的会话不累计。</summary>
    /// <param name="delta">本帧游戏秒数。</param>
    public void AdvanceTime(double delta)
    {
        if (double.IsFinite(delta) && delta > 0 && LevelModel.SessionActive &&
            LevelModel.Outcome.Value == LevelOutcome.Running)
            LevelModel.ElapsedSeconds += delta;
    }

    /// <summary>接收场景抵达或触发事实，迁移到战斗并关闭前进窗口。</summary>
    public void StartBattle()
    {
        LevelModel.Sequence.StartBattle();
        LevelModel.TravelAvailable.Value = false;
    }

    /// <summary>接收本波清除事实，推进阶段并更新前进窗口。</summary>
    public void ClearBattle()
    {
        LevelModel.Sequence.ClearBattle();
        LevelModel.TravelAvailable.Value = LevelModel.Sequence.Phase == LevelStagePhase.Travelling;
    }

    /// <summary>接受第一个结局；先捕获检查点，再发布冻结成绩。</summary>
    /// <param name="outcome">通关或失败。</param>
    /// <param name="healthPercent">结局时生命百分比。</param>
    public void Complete(LevelOutcome outcome, int healthPercent)
    {
        if (!LevelModel.SessionActive || LevelModel.Outcome.Value != LevelOutcome.Running)
            return;
        if (outcome != LevelOutcome.Cleared && outcome != LevelOutcome.Defeated)
            throw new ArgumentOutOfRangeException(nameof(outcome));
        if (outcome == LevelOutcome.Cleared)
            LevelModel.Sequence.Complete();
        CheckpointWrite = this.GetSystem<SaveSystem>().SaveCheckpointAsync(this.GetModel<ProfileModel>());
        // 先冻结结果并释放回滚副本，再让结局订阅者读取完整状态。
        LevelModel.Result = new LevelResult(outcome, LevelModel.ElapsedSeconds, healthPercent,
            LevelModel.DefeatedMonsterCount, LevelModel.AwardedExperience);
        LevelModel.EntrySnapshot = null;
        LevelModel.TravelAvailable.Value = false;
        LevelModel.Outcome.Value = outcome;
    }

    /// <summary>未提交时恢复档案，然后结束本局状态；重复结束安全。</summary>
    public void EndSession()
    {
        bool abandoned = LevelModel.EntrySnapshot != null;
        if (abandoned)
            this.GetSystem<ProfileSystem>().RestoreCheckpoint(LevelModel.EntrySnapshot);
        LevelModel.EntrySnapshot = null;
        LevelModel.SessionActive = false;
        LevelModel.Sequence?.Stop();
        LevelModel.TravelAvailable.Value = false;
        if (abandoned)
            LevelModel.Outcome.Value = LevelOutcome.Abandoned;
    }
}
