using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameConfig;
using GameConfig.Level;
using GameConfig.Loot;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Config;
using GameLogic.Drops;
using GameLogic.Equipment;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Skills;

namespace GameLogic.Level;

/// <summary>
/// 关卡业务系统：借用独立注册的 LevelModel 和档案，执行奖励、提交与回滚；不持有节点。
/// </summary>
public sealed class LevelSystem : AbstractSystem
{
    private const double ComboCheckInterval = 20d / 24d; // 官方每20个24fps源帧比较一次连击计数。
    private double _comboCheckElapsed; // 距上次连击检查的游戏秒数。
    private int _previousComboCount; // 上次检查所见的命中数，相邻检查相同即断连。

    /// <summary>
    /// 绑定系统归属；模型由流程先行注册。
    /// </summary>
    /// <param name="architecture">所属游戏入口。</param>
    public LevelSystem(IArchitecture architecture) : base(architecture) { }

    /// <summary>
    /// 借用 Architecture 拥有的本局状态。
    /// </summary>
    private LevelModel LevelModel => this.GetModel<LevelModel>();

    /// <summary>
    /// 当前关卡是否接受玩家业务写请求；会话未开始、已经结算或已离开时均为 false。
    /// </summary>
    public bool IsRunning => LevelModel is { SessionActive: true } && LevelModel.Outcome.Value == LevelOutcome.Running;

    /// <summary>
    /// 本次结局提交的检查点写盘任务。
    /// </summary>
    public Task<bool> CheckpointWrite { get; private set; }

    /// <summary>
    /// 系统移除时兜底结束未清理的业务事务。
    /// </summary>
    protected override void OnDeinit() => EndSession();

    /// <summary>
    /// 按定义初始化本局阶段。
    /// </summary>
    /// <param name="levelId">关卡主键。</param>
    public void Prepare(int levelId)
    {
        LevelConfig config = this.GetModel<ConfigurationModel>().Tables.TbLevelConfig.Get(levelId);
        if (!this.GetSystem<ProfileSystem>().CanEnterLevel(levelId))
        {
            throw new InvalidOperationException($"关卡尚未开放：{levelId}");
        }

        LevelModel.LevelId = config.LevelId;
        LevelModel.Sequence = new LevelStageSequence(config.Stages);
    }

    /// <summary>
    /// 验证阵容顺序并捕获进关回滚副本。
    /// </summary>
    /// <param name="heroIds">按玩家席位排列的关卡英雄编号。</param>
    /// <exception cref="InvalidOperationException">关卡阵容与档案阵容不一致。</exception>
    public void StartSession(IReadOnlyList<int> heroIds)
    {
        ProfileModel profile = this.GetModel<ProfileModel>();
        if (!heroIds.SequenceEqual(profile.HeroIds))
        {
            throw new InvalidOperationException("关卡实体必须与档案阵容一致。");
        }

        if (LevelModel.SessionActive)
        {
            throw new InvalidOperationException("关卡会话已经开始。");
        }

        // 捕获独立回滚副本后再启动统计，重复开始不得覆盖原快照。
        LevelModel.EntrySnapshot = this.GetUtility<SaveDataUtility>().Capture(profile, this.GetModel<SkillModel>());
        CheckpointWrite = null;
        LevelModel.ElapsedSeconds = 0;
        LevelModel.DefeatedEnemyCount = LevelModel.AwardedExperience = 0;
        LevelModel.Result = null;
        ResetCombo();
        LevelModel.SessionActive = true;
        LevelModel.TravelAvailable.Value = false;
        LevelModel.Sequence.Begin();
        LevelModel.Outcome.Value = LevelOutcome.Running;
    }

    /// <summary>
    /// 记录击败并奖励经验，返回表现层是否需要播放升级。
    /// </summary>
    /// <param name="experienceReward">本次配方经验。</param>
    /// <returns>是否至少升了一级。</returns>
    public bool RecordDefeat(int experienceReward)
    {
        if (!LevelModel.SessionActive || LevelModel.Outcome.Value != LevelOutcome.Running)
        {
            return false;
        }

        int experience = Math.Max(0, experienceReward);
        LevelModel.DefeatedEnemyCount++;
        LevelModel.AwardedExperience += experience;
        return experience > 0 && this.GetSystem<ProfileSystem>().AddExperience(this.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), experience) > 0;
    }

    /// <summary>
    /// 实际拾取时将装备加入本槽背包；结局后拒绝领取，满包不消耗地面物品。
    /// </summary>
    /// <param name="data">掉落时已抽定属性的装备实例。</param>
    /// <returns>是否完成领取。</returns>
    public bool TryCollectEquipment(EquipmentData data)
    {
        return LevelModel.SessionActive && LevelModel.Outcome.Value == LevelOutcome.Running
                            && this.GetSystem<ProfileSystem>().TryCollectEquipment(data);
    }

    /// <summary>
    /// 接触地面恢复品时执行其配置效果；满值仍消耗，未开始、死亡及结局后拒绝。
    /// </summary>
    /// <param name="id">恢复品定义。</param>
    /// <returns>本次已消费恢复品时为真，场景据此回收节点并播放反馈。</returns>
    public bool TryCollectPickup(PickupId id)
    {
        if (!LevelModel.SessionActive || LevelModel.Outcome.Value != LevelOutcome.Running ||
            this.GetModel<HeroModel>().IsDepleted)
        {
            return false;
        }

        PickupConfig pickup = this.GetModel<ConfigurationModel>().Tables.TbPickupConfig.GetOrDefault(id);
        if (pickup == null)
        {
            return false;
        }

        HeroSystem hero = this.GetSystem<HeroSystem>();
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

    /// <summary>
    /// 本关球体接触后发放收益；场景只确认接触和实例所有权，不执行奖励规则。
    /// </summary>
    /// <param name="reward">球体在生成时确定的收益，接触时发放并钳制上限。</param>
    public void CollectLoot(LootReward reward)
    {
        if (!LevelModel.SessionActive || LevelModel.Outcome.Value != LevelOutcome.Running ||
            this.GetModel<HeroModel>().IsDepleted)
        {
            return;
        }

        switch (reward.Kind)
        {
            case LootKind.Soul:
                this.GetSystem<ProfileSystem>().AddSoul(reward.Amount);
                break;
            case LootKind.Musou:
                this.GetSystem<HeroSystem>().AddWsFromPickup(reward.Amount);
                break;
            case LootKind.Mana:
                this.GetSystem<HeroSystem>().RestoreMp(reward.Amount);
                break;
            case LootKind.Health:
                this.GetSystem<HeroSystem>().Heal(reward.Amount);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(reward));
        }
    }

    /// <summary>
    /// 接收有效游戏帧时间，推进成绩时钟与连击检查；暂停、结局和未开始的会话不累计。
    /// </summary>
    /// <param name="delta">本帧游戏秒数。</param>
    public void AdvanceTime(double delta)
    {
        if (double.IsFinite(delta) && delta > 0 && LevelModel.SessionActive &&
            LevelModel.Outcome.Value == LevelOutcome.Running)
        {
            LevelModel.ElapsedSeconds += delta;
            _comboCheckElapsed += delta;
            while (_comboCheckElapsed >= ComboCheckInterval)
            {
                _comboCheckElapsed -= ComboCheckInterval;
                int count = LevelModel.ComboCount.Value;
                if (_previousComboCount == count)
                {
                    LevelModel.ComboCount.Value = 0;
                    _previousComboCount = 0;
                }
                else
                {
                    _previousComboCount = count;
                }
            }
        }
    }

    /// <summary>
    /// 接受本关玩家对敌人的一次非闪避命中；按命中目标累计，不读取普攻段号。
    /// </summary>
    public void RecordComboHit()
    {
        if (IsRunning)
        {
            LevelModel.ComboCount.Value++;
        }
    }

    /// <summary>
    /// 接收场景抵达或触发事实，迁移到战斗并关闭前进窗口。
    /// </summary>
    public void StartBattle()
    {
        LevelModel.Sequence.StartBattle();
        LevelModel.TravelAvailable.Value = false;
    }

    /// <summary>
    /// 接收本波清除事实，推进阶段并更新前进窗口。
    /// </summary>
    public void ClearBattle()
    {
        LevelModel.Sequence.ClearBattle();
        LevelModel.TravelAvailable.Value = LevelModel.Sequence.Phase == LevelStagePhase.Travelling;
    }

    /// <summary>
    /// 蝙蝠洞石像完成掉落后开放出口，无需耗尽蝙蝠的持续生成流。
    /// </summary>
    /// <param name="spawnPointId">被击败实体归属的配方生成点。</param>
    /// <returns>本次是否开放了出口。</returns>
    public bool TryOpenBatCaveExit(string spawnPointId)
    {
        Tables tables = this.GetModel<ConfigurationModel>().Tables;
        LevelConfig config = tables.TbLevelConfig.Get(LevelModel.LevelId);
        if (!LevelModel.SessionActive || LevelModel.Outcome.Value != LevelOutcome.Running ||
            LevelModel.Sequence.Phase != LevelStagePhase.Fighting ||
            LevelModel.Sequence.Current.StageOrder != config.Stages.Count ||
            LevelModel.LevelId != BatCaveRules.LevelId ||
            BatCaveRules.StatueSpawnPointId != spawnPointId)
        {
            return false;
        }

        LevelModel.Sequence.OpenExit();
        LevelModel.TravelAvailable.Value = false;
        return true;
    }

    /// <summary>
    /// 接受第一个结局；先捕获检查点，再发布冻结成绩。
    /// </summary>
    /// <param name="outcome">通关或失败。</param>
    /// <param name="healthPercent">结局时生命百分比。</param>
    public void Complete(LevelOutcome outcome, int healthPercent)
    {
        if (!LevelModel.SessionActive || LevelModel.Outcome.Value != LevelOutcome.Running)
        {
            return;
        }

        if (outcome != LevelOutcome.Cleared && outcome != LevelOutcome.Defeated)
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        if (outcome == LevelOutcome.Cleared)
        {
            LevelModel.Sequence.Complete();
            this.GetSystem<ProfileSystem>().RecordLevelClear(LevelModel.LevelId);
        }
        CheckpointWrite = this.GetSystem<SaveSystem>().SaveCheckpointAsync(this.GetModel<ProfileModel>());
        // 先冻结结果并释放回滚副本，再让结局订阅者读取完整状态。
        LevelModel.Result = new LevelResult(outcome, LevelModel.ElapsedSeconds, healthPercent,
            LevelModel.DefeatedEnemyCount, LevelModel.AwardedExperience);
        LevelModel.EntrySnapshot = null;
        LevelModel.TravelAvailable.Value = false;
        ResetCombo();
        LevelModel.Outcome.Value = outcome;
    }

    /// <summary>
    /// 结束本局状态；未保存离开默认恢复进关快照。
    /// </summary>
    /// <param name="rollback">是否恢复进关快照；设置返回前已保存时传 false。</param>
    public void EndSession(bool rollback = true)
    {
        bool abandoned = LevelModel.EntrySnapshot != null;
        if (rollback && abandoned)
        {
            this.GetSystem<ProfileSystem>().RestoreCheckpoint(LevelModel.EntrySnapshot);
        }

        LevelModel.EntrySnapshot = null;
        LevelModel.SessionActive = false;
        LevelModel.Sequence?.Stop();
        LevelModel.TravelAvailable.Value = false;
        ResetCombo();
        if (rollback && abandoned)
        {
            LevelModel.Outcome.Value = LevelOutcome.Abandoned;
        }
    }

    /// <summary>
    /// 场景入口完成转场演出后结束当前事务，已领取收益成为隐藏关的进入快照。
    /// </summary>
    /// <param name="levelId">入口请求的隐藏关编号。</param>
    /// <returns>本次是否接受转场；重复、死亡或错误入口拒绝。</returns>
    public bool TransferToDragonPalace(int levelId)
    {
        if (!LevelModel.SessionActive || LevelModel.Outcome.Value != LevelOutcome.Running ||
            this.GetModel<HeroModel>().IsDepleted || levelId != DragonPalaceRules.LevelId ||
            DragonPalaceRules.EntranceLevelId != LevelModel.LevelId)
        {
            return false;
        }

        LevelModel.EntrySnapshot = null;
        LevelModel.SessionActive = false;
        LevelModel.TravelAvailable.Value = false;
        LevelModel.Sequence.Stop();
        ResetCombo();
        LevelModel.Outcome.Value = LevelOutcome.Transferred;
        return true;
    }

    /// <summary>
    /// 新会话、结局和离关同时清除连击计数与检查节拍。
    /// </summary>
    private void ResetCombo()
    {
        _comboCheckElapsed = 0;
        _previousComboCount = 0;
        LevelModel.ComboCount.Value = 0;
    }
}
