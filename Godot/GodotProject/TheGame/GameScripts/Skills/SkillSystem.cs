using System;
using System.Linq;
using GameConfig.Skills;
using GameLogic.Battle;
using GameLogic.Input;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Profile;
using GameLogic.Heroes.Body;

namespace GameLogic.Skills;

/// <summary>技能学习与施放规则入口；借用档案、技能书、英雄数值及只读配置，不持有场景节点。</summary>
public sealed class SkillSystem(IArchitecture architecture) : AbstractSystem(architecture)
{
    /// <summary>当前身体技能登记的 GGF 状态；具体角色负责在身体状态机中注册它。</summary>
    public Type BodyStateType => this.GetModel<SkillModel>().ActiveSkillId is not { } id
        ? null
        : this.GetModel<SkillModel>().Definitions[id].BodyState;

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

    /// <summary>查询当前英雄与技能组是否支持此技能。</summary>
    /// <param name="id">技能编号。</param>
    /// <returns>当前可用时为真。</returns>
    public bool IsAvailable(SkillId id)
    {
        var model = this.GetModel<SkillModel>();
        var tables = this.GetModel<ConfigurationModel>().Tables;
        var definition = tables.TbSkillConfig.GetOrDefault(id);
        return definition != null && (int)definition.Hero == this.GetModel<ProfileModel>().ActiveHeroId &&
               model.Definitions.ContainsKey(id);
    }

    /// <summary>读取当前蓝量是否足够，不预扣资源。</summary>
    /// <param name="id">技能编号。</param>
    /// <returns>蓝量足够时为真。</returns>
    public bool CanAfford(SkillId id) => this.GetModel<HeroModel>().Mp.Value >=
                                         this.GetModel<ConfigurationModel>().Tables.TbSkillConfig.Get(id).ManaCost;

    /// <summary>校验当前技能组、学习记录、起手条件和费用，登记一次施放；动画在 GGF 进入对应状态后开始。</summary>
    /// <param name="id">技能编号。</param>
    /// <param name="actor">本次调用借用的角色能力。</param>
    /// <returns>本次登记并扣费成功时为真。</returns>
    public bool TryActivate(SkillId id, ISkillActor actor)
    {
        var model = this.GetModel<SkillModel>();
        if (model.ActiveSkillId != null || !IsAvailable(id) || !IsLearned(id) ||
            !model.Definitions.TryGetValue(id, out var definition) || definition.CanStart?.Invoke(actor) == false)
            return false;
        if (!this.GetSystem<HeroSystem>().TrySpendMp(
                this.GetModel<ConfigurationModel>().Tables.TbSkillConfig.Get(id).ManaCost))
            return false;

        model.ActiveSkillId = id;
        return true;
    }

    /// <summary>选择角色或武器的一组技能登记；校验完成后才取消旧施放并替换模型数据。</summary>
    /// <param name="heroId">此组所属英雄。</param>
    /// <param name="definitions">角色提供的登记，复制到模型，调用方不拥有运行时容器。</param>
    /// <exception cref="ArgumentException">角色、编号、动作绑定或状态不合法。</exception>
    public void SelectSkills(int heroId, params SkillDefinition[] definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var model = this.GetModel<SkillModel>();
        var tables = this.GetModel<ConfigurationModel>().Tables;
        if (heroId != this.GetModel<ProfileModel>().ActiveHeroId || definitions.Length == 0 ||
            definitions.Any(item => item == null || item.BodyState == null ||
                item.BodyState.IsAbstract || !typeof(AbstractHeroSkillState).IsAssignableFrom(item.BodyState) ||
                (item.InputAction != null && string.IsNullOrWhiteSpace(item.InputAction)) ||
                tables.TbSkillConfig.GetOrDefault(item.Id) is not { } skill || (int)skill.Hero != heroId) ||
            definitions.Select(item => item.Id).Distinct().Count() != definitions.Length ||
            definitions.Where(item => item.InputAction != null).GroupBy(item => item.InputAction).Any(group => group.Count() > 1))
            throw new ArgumentException("技能登记须属于当前英雄，编号、状态和动作绑定须合法且不重复。", nameof(definitions));
        if (model.Definitions.Values.SequenceEqual(definitions))
            return;
        CancelSkill();
        model.Definitions.Clear();
        foreach (var definition in definitions)
            model.Definitions.Add(definition.Id, definition);
    }

    /// <summary>按当前登记尝试一个直接动作；拒绝后不排队，也不尝试较低优先级的同时按键。</summary>
    /// <param name="actor">本次角色表现。</param>
    /// <param name="data">当前角色的输入消费记录与游戏时钟。</param>
    /// <returns>匹配并激活成功时为真。</returns>
    public bool TryActivateInput(ISkillActor actor, HeroActionData data)
    {
        var input = this.GetSystem<InputSystem>();
        foreach (var definition in this.GetModel<SkillModel>().Definitions.Values)
            if (definition.InputAction is { } action && input.ConsumePress(data, action))
                return TryActivate(definition.Id, actor);
        return false;
    }

    /// <summary>换组、离场或死亡时取消当前施放并清除无敌区间；具体状态的退出负责释放动画与判定。</summary>
    public void CancelSkill()
    {
        var model = this.GetModel<SkillModel>();
        model.ActiveSkillId = null;
        model.InvulnerableFrom = double.PositiveInfinity;
        model.InvulnerableUntil = 0;
    }

    /// <summary>登记一个以游戏秒数计的无敌区间，不受动画跳帧影响。</summary>
    /// <param name="from">开始的游戏时刻。</param>
    /// <param name="duration">持续秒数。</param>
    public void GrantInvulnerability(double from, double duration)
    {
        var model = this.GetModel<SkillModel>();
        model.InvulnerableFrom = from;
        model.InvulnerableUntil = from + duration;
    }

    /// <summary>查询当前游戏时刻是否在技能无敌区间。</summary>
    /// <param name="now">游戏时刻，秒。</param>
    /// <returns>处于无敌区间时为真。</returns>
    public bool IsInvulnerable(double now)
    {
        var model = this.GetModel<SkillModel>();
        return now >= model.InvulnerableFrom && now < model.InvulnerableUntil;
    }
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
