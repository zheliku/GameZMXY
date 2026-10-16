using System;
using System.Threading.Tasks;
using GameConfig.Equipment;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Equipment;
using GameLogic.Profile;
using GameLogic.Save;

namespace GameLogic.Level;

/// <summary>关卡业务系统：借用独立注册的 LevelModel 和档案，执行奖励、装备交易、提交与回滚；不持有节点。</summary>
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
        LevelModel.EntrySnapshot = this.GetUtility<SaveDataUtility>().Capture(this.GetModel<ProfileModel>());
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
    /// <param name="equipment">掉落时已抽定属性的装备实例。</param>
    /// <returns>是否完成领取。</returns>
    public bool TryCollectEquipment(EquipmentInstance equipment) =>
        LevelModel.SessionActive && LevelModel.Outcome.Value == LevelOutcome.Running
                            && this.GetSystem<ProfileSystem>().TryCollectEquipment(equipment);

    /// <summary>实际接触灵魂球时入账；本关结束后拒绝迟到奖励。</summary>
    /// <param name="amount">球中已确定的灵魂数量。</param>
    public void CollectSoul(int amount)
    {
        if (LevelModel.SessionActive && LevelModel.Outcome.Value == LevelOutcome.Running)
            this.GetSystem<ProfileSystem>().AddSoul(amount);
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

    /// <summary>提交玩家在背包发起的穿戴请求，规则由档案作用域装备系统执行。</summary>
    /// <param name="instanceId">背包实例编号。</param>
    /// <returns>进行中的本关成功穿戴时为 true。</returns>
    public bool TryEquip(Guid instanceId) =>
        LevelModel.Outcome.Value == LevelOutcome.Running && LevelModel.SessionActive && this.GetSystem<EquipmentSystem>()
            .TryEquip(instanceId);

    /// <summary>提交卸装请求；满包时保留装备。</summary>
    /// <param name="slot">武器或防具部位。</param>
    /// <returns>成功卸下时为 true。</returns>
    public bool TryUnequip(EquipmentSlot slot) =>
        LevelModel.Outcome.Value == LevelOutcome.Running && LevelModel.SessionActive &&
        this.GetSystem<EquipmentSystem>().TryUnequip(slot);

    /// <summary>提交背包出售请求；收益与本关其他档案变化一起保存或回滚。</summary>
    /// <param name="instanceId">仍在背包中的装备实例。</param>
    /// <returns>本关进行中且实际出售时为 true。</returns>
    public bool TrySell(Guid instanceId) =>
        LevelModel.Outcome.Value == LevelOutcome.Running && LevelModel.SessionActive && this.GetSystem<EquipmentSystem>()
            .TrySell(instanceId);

    /// <summary>关卡运行期间提交按品质出售；收益遵守本关检查点和放弃回滚规则。</summary>
    /// <param name="quality">背包内要出售的装备品质。</param>
    /// <returns>实际出售件数。</returns>
    public int SellByQuality(EquipmentQuality quality) =>
        LevelModel.Outcome.Value == LevelOutcome.Running && LevelModel.SessionActive
            ? this.GetSystem<EquipmentSystem>().SellByQuality(quality)
            : 0;


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
