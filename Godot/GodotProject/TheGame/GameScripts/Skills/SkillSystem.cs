using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Profile;

namespace GameLogic.Skills;

/// <summary>技能学习与施放规则入口；借用档案、技能书、英雄数值及只读配置，不持有场景节点。</summary>
public sealed partial class SkillSystem(IArchitecture architecture) : AbstractSystem(architecture)
{
    // ---- 业务方法 ----

    /// <summary>一次性学习当前英雄的主动技能；重复学习、未实现角色和余额不足都不扣费。</summary>
    /// <param name="skillId">主动技能定义。</param>
    /// <returns>学习结果，界面据此展示反馈。</returns>
    public SkillLearnResult Learn(SkillId skillId)
    {
        var tables = this.GetModel<ConfigurationModel>().Tables;
        var definition = tables.TbSkillConfig.GetOrDefault(skillId);
        var profile = this.GetModel<ProfileModel>();
        if (definition == null || (int)definition.Hero != profile.ActiveHeroId ||
            !IsAvailable(skillId))
            return SkillLearnResult.Unavailable;
        var learned = this.GetModel<SkillModel>().GetLearnedSkills(profile.ActiveHeroId);
        if (learned.Contains(skillId))
            return SkillLearnResult.AlreadyLearned;
        if (!this.GetSystem<ProfileSystem>().TrySpendSoul(definition.LearnSoul))
            return SkillLearnResult.InsufficientSoul;
        learned.Add(skillId);
        return SkillLearnResult.Learned;
    }

    /// <summary>查询本档案当前出战英雄是否已学习指定技能。</summary>
    /// <param name="skillId">技能定义。</param>
    /// <returns>已学习时为 true。</returns>
    public bool IsLearned(SkillId skillId) => this.GetModel<SkillModel>()
        .GetLearnedSkills(this.GetModel<ProfileModel>().ActiveHeroId).Contains(skillId);
}

/// <summary>学习请求的业务结果；不以 UI 状态代替资格和余额检查。</summary>
public enum SkillLearnResult
{
    /// <summary>本次已扣费并学习。</summary>
    Learned,
    /// <summary>已经学习，不重复扣费。</summary>
    AlreadyLearned,
    /// <summary>灵魂不足，所有数据不变。</summary>
    InsufficientSoul,
    /// <summary>不是当前英雄的技能，或动作尚未实现。</summary>
    Unavailable
}
