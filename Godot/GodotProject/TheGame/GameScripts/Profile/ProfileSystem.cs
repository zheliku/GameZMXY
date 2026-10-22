using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Architecture;
using GameLogic.Equipment;
using GameLogic.Save;
using GameLogic.Skills;
using GameLogic.Config;

namespace GameLogic.Profile;

/// <summary>档案业务入口：协调成长、背包资产、穿戴与检查点回滚，不持有节点。</summary>
public sealed class ProfileSystem : AbstractSystem
{
    /// <summary>绑定已注册档案模型的架构。</summary>
    /// <param name="architecture">所属档案架构。</param>
    public ProfileSystem(IArchitecture architecture) : base(architecture) { }

    /// <summary>最新已开放关卡；完成当前内容后仍指向最后一关，不开放尚未实现的地点。</summary>
    public int LatestLevelId => this.GetModel<ConfigurationModel>().Tables.TbLevelConfig.DataList
        .Where(level => level.LevelId <= this.GetModel<ProfileModel>().HighestClearedLevelId.Value + 1)
        .Max(level => level.LevelId);

    /// <summary>地图与流程共用的进入资格；历史关卡可重玩，未开放或不存在的关卡拒绝。</summary>
    /// <param name="levelId">关卡配置主键。</param>
    /// <returns>本档案是否允许进入。</returns>
    public bool CanEnterLevel(int levelId) => levelId <= LatestLevelId &&
        this.GetModel<ConfigurationModel>().Tables.TbLevelConfig.GetOrDefault(levelId) != null;

    /// <summary>成功通关时推进最远记录；重玩旧关不退回，不允许跳过锁定关卡。</summary>
    /// <param name="levelId">实际完成的关卡。</param>
    /// <exception cref="InvalidOperationException">关卡不存在或尚未开放。</exception>
    public void RecordLevelClear(int levelId)
    {
        if (!CanEnterLevel(levelId))
            throw new InvalidOperationException($"不能完成尚未开放的关卡：{levelId}");
        var progress = this.GetModel<ProfileModel>().HighestClearedLevelId;
        progress.Value = Math.Max(progress.Value, levelId);
    }

    /// <summary>奖励出战英雄经验，按共享曲线求值后一次提交完整成长进度。</summary>
    /// <param name="amount">非负经验。</param>
    /// <returns>提升等级数。</returns>
    public int AddExperience(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var profile = this.GetModel<ProfileModel>();
        var curve = this.GetUtility<ExperienceCurve>();
        int previousLevel = profile.Level;
        profile.Progress.Value = curve.GetProgress(curve.Add(profile.TotalExperience, amount));
        return profile.Level - previousLevel;
    }

    /// <summary>增加档案灵魂，保持余额整数上限。</summary>
    /// <param name="amount">非负奖励。</param>
    public void AddSoul(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var soul = this.GetModel<ProfileModel>().Soul;
        soul.Value = (int)Math.Min((long)soul.Value + amount, int.MaxValue);
    }

    /// <summary>在余额足够时支付灵魂。</summary>
    /// <param name="amount">非负价格。</param>
    /// <returns>是否完成支付。</returns>
    public bool TrySpendSoul(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var soul = this.GetModel<ProfileModel>().Soul;
        if (amount > soul.Value)
            return false;
        soul.Value -= amount;
        return true;
    }

    /// <summary>领取已确定属性的装备；满包、背包或穿戴中已拥有此实例时拒绝。</summary>
    /// <param name="data">掉落实例。</param>
    /// <returns>是否完成领取。</returns>
    public bool TryCollectEquipment(EquipmentData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var profile = this.GetModel<ProfileModel>();
        foreach (int heroId in profile.HeroIds)
        {
            var slots = profile.GetSlots(heroId).Value;
            if (slots.Weapon?.InstanceId == data.InstanceId || slots.Armor?.InstanceId == data.InstanceId ||
                slots.Accessory?.InstanceId == data.InstanceId)
                return false;
        }

        if (profile.EquipmentDatas.Count == profile.Capacity || profile.EquipmentDatas.Any(x => x.InstanceId == data.InstanceId))
            return false;
        profile.EquipmentDatas.Add(data);
        return true;
    }

    /// <summary>先验证关卡快照，再原地恢复档案数据，保留绑定身份。</summary>
    /// <param name="data">同一档案的进关快照。</param>
    /// <exception cref="ArgumentException">快照内容无效。</exception>
    /// <exception cref="InvalidOperationException">快照不属于当前英雄集合。</exception>
    public void RestoreCheckpoint(GameData data)
    {
        var profile = this.GetModel<ProfileModel>();
        SaveDataUtility.ValidateEnvelope(data);
        SaveDataUtility.ValidateSkills(data, this.GetModel<ConfigurationModel>().Tables.TbSkillConfig);
        SaveDataUtility.ValidateLevelProgress(data, this.GetModel<ConfigurationModel>().Tables.TbLevelConfig);
        if (data.ActiveHeroId != profile.ActiveHeroId || data.Heroes.Count != profile.HeroIds.Count ||
            data.Heroes.Select(x => x.HeroId).Distinct().Count() != data.Heroes.Count ||
            data.Heroes.Any(x => !profile.HeroIds.Contains(x.HeroId) || x.TotalExperience < 0) || data.Soul < 0)
            throw new InvalidOperationException("回滚快照与档案不一致或数值非法。");
        var (bag, slots) = SaveDataUtility.ReadItems(data, profile.Capacity);
        // 校验完成后使用正常修改入口；派生系统按绑定优先级先于 UI 同步。
        foreach (var hero in data.Heroes)
        {
            this.GetModel<SkillModel>().GetLearnedSkills(hero.HeroId).ReplaceAll(hero.LearnedSkills);
            profile.GetSlots(hero.HeroId).Value = slots[hero.HeroId];
            profile.GetProgress(hero.HeroId).Value = this.GetUtility<ExperienceCurve>().GetProgress(hero.TotalExperience);
        }
        profile.EquipmentDatas.ReplaceAll(bag);
        profile.Soul.Value = data.Soul;
        profile.HighestClearedLevelId.Value = data.HighestClearedLevelId;
    }
}
