using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Level;
using GameLogic.Profile;

namespace GameLogic.Skills;

/// <summary>
/// 玩家在地图或运行中的关卡学习技能，学习与扣费规则由技能系统执行。
/// </summary>
/// <param name = "skillId">本次选择的技能。</param>
public sealed class LearnSkillCommand(SkillId skillId) : AbstractCommand<SkillLearnResult>
{
    /// <inheritdoc/>
    protected override SkillLearnResult OnExecute()
    {
        LevelModel level = this.GetModel<LevelModel>();
        if (level == null && (this.GetModel<ProfileModel>() == null || this.GetModel<SkillModel>() == null))
        {
            return SkillLearnResult.Unavailable;
        }

        if (level != null && (!level.SessionActive || level.Outcome.Value != LevelOutcome.Running))
        {
            return SkillLearnResult.Unavailable;
        }

        return this.GetSystem<SkillSystem>().Learn(skillId);
    }
}

/// <summary>
/// 技能面板发起一次被动升级，业务系统按提交时的等级和余额重新校验。
/// </summary>
/// <param name = "passiveId">本次选择的被动。</param>
public sealed class UpgradePassiveCommand(PassiveId passiveId) : AbstractCommand<SkillLearnResult>
{
    /// <inheritdoc/>
    protected override SkillLearnResult OnExecute()
    {
        LevelModel level = this.GetModel<LevelModel>();
        if (level == null && (this.GetModel<ProfileModel>() == null || this.GetModel<SkillModel>() == null))
        {
            return SkillLearnResult.Unavailable;
        }

        if (level != null && (!level.SessionActive || level.Outcome.Value != LevelOutcome.Running))
        {
            return SkillLearnResult.Unavailable;
        }

        return this.GetSystem<SkillSystem>().Upgrade(passiveId);
    }
}
