using System;
using System.Linq;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Config;
using GameLogic.Input;
using GameLogic.Profile;

namespace GameLogic.Skills;

/// <summary>技能费用、执行生命周期、后续输入与技能组切换；不解释具体招式的阶段和动画事件。</summary>
public sealed partial class SkillSystem
{
    /// <summary>借用当前占用角色身体的技能。</summary>
    public SkillExecution BodySkill => this.GetModel<SkillModel>().BodySkill;

    /// <summary>当前身体技能登记的 GGF 状态；具体角色负责在身体状态机中注册它。</summary>
    public Type BodyStateType => BodySkill == null
        ? null
        : this.GetModel<SkillModel>().SelectedSet.GetDefinition(BodySkill.Id).BodyState;

    /// <summary>查询当前英雄与技能组是否支持此技能；未选组时使用已有的配置动画定义。</summary>
    /// <param name="id">技能编号。</param>
    /// <returns>当前可用时为真。</returns>
    public bool IsAvailable(SkillId id)
    {
        var model = this.GetModel<SkillModel>();
        var tables = this.GetModel<ConfigurationModel>().Tables;
        var definition = tables.TbSkillConfig.GetOrDefault(id);
        return definition != null && (int)definition.Hero == this.GetModel<ProfileModel>().ActiveHeroId &&
               (model.SelectedSet != null
                   ? model.SelectedSet.GetDefinition(id) != null
                   : tables.TbSkillActionConfig.GetOrDefault(id) != null);
    }

    /// <summary>读取当前蓝量是否足够，不预扣资源。</summary>
    /// <param name="id">技能编号。</param>
    /// <returns>蓝量足够时为真。</returns>
    public bool CanAfford(SkillId id) => this.GetModel<HeroModel>().Mp.Value >=
                                         this.GetModel<ConfigurationModel>().Tables.TbSkillConfig.Get(id).ManaCost;

    /// <summary>校验费用并激活独立执行或预定身体占用；身体执行在 GGF 旧状态退出后起手。</summary>
    /// <param name="id">技能编号。</param>
    /// <param name="actor">本次调用借用的角色能力。</param>
    /// <param name="now">当前游戏秒数。</param>
    /// <returns>成功开始时为真。</returns>
    /// <exception cref="InvalidOperationException">工厂复用执行、返回错误编号，或身体技能未登记状态。</exception>
    public bool TryActivate(SkillId id, ISkillActor actor, double now)
    {
        var model = this.GetModel<SkillModel>();
        if (model.SelectedSet == null || model.ActiveExecutions.ContainsKey(id) || !IsAvailable(id) || !IsLearned(id))
            return false;
        var definition = model.SelectedSet.GetDefinition(id);
        var execution = definition.Create(((IBelongToArchitecture)this).GetArchitecture());
        if (execution == null || execution.Id != id || execution.Started || execution.Completed)
            throw new InvalidOperationException($"技能工厂返回了错误的执行：{id}");
        if (execution.BlocksBody && definition.BodyState == null)
            throw new InvalidOperationException($"身体技能必须登记对应的 GGF 状态：{id}");
        if ((execution.BlocksBody && model.BodySkill != null) || !execution.CanStart(actor) || !this
                .GetSystem<HeroSystem>().TrySpendMp(
                    this.GetModel<ConfigurationModel>().Tables.TbSkillConfig.Get(id).ManaCost))
            return false;

        // 先登记身份再起手，起手生成的特效也能验证自己所属的执行。
        model.ActiveExecutions.Add(id, execution);
        if (execution.BlocksBody)
            model.BodySkill = execution;
        if (!execution.BlocksBody)
        {
            execution.Started = true;
            execution.Start(actor, now);
            CollectFinished(execution, actor);
        }

        return true;
    }

    /// <summary>GGF 身体状态进入后开始本次技能，旧状态的收尾已完成；持续阶段再次占用身体不重复起手。</summary>
    /// <param name="actor">角色表现。</param>
    /// <param name="now">当前游戏秒数。</param>
    /// <returns>本次身体状态借用的执行。</returns>
    /// <exception cref="InvalidOperationException">尚未激活身体技能。</exception>
    public SkillExecution BeginBody(ISkillActor actor, double now)
    {
        var execution = this.GetModel<SkillModel>().BodySkill ??
                        throw new InvalidOperationException("进入技能身体状态前必须先成功激活技能。");
        if (!execution.Started)
        {
            execution.Started = true;
            execution.Start(actor, now);
            CollectFinished(execution, actor);
        }

        return execution;
    }

    /// <summary>把当前技能组的按键事实送给已活动技能；持续、二次发动和松手不再走新技能扣费。</summary>
    /// <param name="actor">角色表现。</param>
    /// <param name="data">本角色当前帧采样。</param>
    public void HandleInput(ISkillActor actor, HeroActionData data)
    {
        var model = this.GetModel<SkillModel>();
        if (model.SelectedSet == null)
            return;
        foreach (var definition in model.SelectedSet.Definitions)
        {
            if (definition.Slot is not { } slot ||
                !model.ActiveExecutions.TryGetValue(definition.Id, out var execution) ||
                !execution.Started)
                continue;
            // 后续输入已经属于本次执行；本帧结束后也不能再次作为新激活请求扣费。
            if (data.DirectSkillSlot == slot)
                data.DirectSkillSlot = null;
            execution.HandleInput(actor, data.GetSkillButton(slot),
                model.BodySkill == null || ReferenceEquals(model.BodySkill, execution));
            CollectFinished(execution, actor);
        }
    }

    /// <summary>按秒推进一个活动执行；身体与持续阶段在同一物理帧不会重复推进。</summary>
    /// <param name="execution">模型拥有的执行。</param>
    /// <param name="actor">角色表现。</param>
    /// <param name="dt">游戏秒数。</param>
    /// <param name="now">本角色当前游戏时刻。</param>
    public void Advance(SkillExecution execution, ISkillActor actor, float dt, double now)
    {
        if (!IsActive(execution) || !execution.Started || execution.LastAdvancedAt == now)
            return;
        execution.LastAdvancedAt = now;
        execution.Advance(actor, dt);
        CollectFinished(execution, actor);
    }

    /// <summary>身体状态推进后继续运行已释放身体的技能；暂停时调用方不推进游戏秒数。</summary>
    /// <param name="actor">角色表现。</param>
    /// <param name="dt">游戏秒数。</param>
    /// <param name="now">本角色当前游戏时刻。</param>
    public void AdvanceBackground(ISkillActor actor, float dt, double now)
    {
        foreach (var execution in this.GetModel<SkillModel>().ActiveExecutions.Values.ToArray())
            if (!execution.BlocksBody)
                Advance(execution, actor, dt, now);
    }

    /// <summary>结束一次身体占用；主动释放身体的持续效果继续运行，受击打断只终止本次身体技能。</summary>
    /// <param name="execution">刚离开身体状态的执行。</param>
    /// <param name="actor">角色表现。</param>
    public void ReleaseBody(SkillExecution execution, ISkillActor actor)
    {
        actor.EndSkillAnimation();
        if (IsActive(execution) && execution.BlocksBody)
            Stop(execution, actor);
    }

    /// <summary>选择角色或武器技能组；切换时清理旧执行与无敌，已学习记录保留。</summary>
    /// <param name="set">不可变的新组。</param>
    /// <param name="actor">用于结束旧组规则的角色表现。</param>
    /// <exception cref="ArgumentException">技能组或其中的技能不属于当前英雄。</exception>
    public void SelectSkillSet(SkillSet set, ISkillActor actor)
    {
        ArgumentNullException.ThrowIfNull(set);
        var model = this.GetModel<SkillModel>();
        var tables = this.GetModel<ConfigurationModel>().Tables;
        if (set.HeroId != this.GetModel<ProfileModel>().ActiveHeroId || set.Definitions.Any(definition =>
                tables.TbSkillConfig.GetOrDefault(definition.Id) is not { } skill || (int)skill.Hero != set.HeroId))
            throw new ArgumentException("技能组与出战英雄不一致。", nameof(set));
        if (ReferenceEquals(model.SelectedSet, set))
            return;
        StopAll(actor);
        model.SelectedSet = set;
    }

    /// <summary>离场或死亡时清理所有执行，旧特效回调会因身份失效而被拒绝。</summary>
    /// <param name="actor">本角色表现。</param>
    public void StopAll(ISkillActor actor)
    {
        var model = this.GetModel<SkillModel>();
        foreach (var execution in model.ActiveExecutions.Values.ToArray())
            Stop(execution, actor);
        model.BodySkill = null;
        model.InvulnerableFrom = double.PositiveInfinity;
        model.InvulnerableUntil = 0;
    }

    /// <summary>验证执行身份，不能仅凭技能编号接受旧回调。</summary>
    /// <param name="execution">待验证执行。</param>
    /// <returns>仍是同一次活动执行时为真。</returns>
    public bool IsActive(SkillExecution execution) => execution != null &&
                                                      this.GetModel<SkillModel>().ActiveExecutions
                                                          .TryGetValue(execution.Id, out var active) &&
                                                      ReferenceEquals(active, execution);

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

    /// <summary>按原版取整顺序计算技能威力，最后乘无双倍率并截为整数。</summary>
    /// <param name="hit">共享伤害配置。</param>
    /// <param name="power">英雄攻击力。</param>
    /// <param name="multiplier">无双攻击倍率。</param>
    /// <returns>送入伤害系统前的技能威力。</returns>
    public static int CalculatePower(SkillHitConfig hit, float power, float multiplier)
    {
        double value = hit.PowerRounding == SkillPowerRounding.BeforeScale
            ? Math.Round(power, MidpointRounding.AwayFromZero)
            : power;
        value = value * hit.PowerScale + hit.FlatPower;
        if (hit.PowerRounding == SkillPowerRounding.AfterScale)
            value = Math.Round(value, MidpointRounding.AwayFromZero);
        return (int)(value * multiplier);
    }

    /// <summary>移除已完成执行，并同步唯一身体占用。</summary>
    /// <param name="execution">刚推进的执行。</param>
    /// <param name="actor">角色表现。</param>
    private void CollectFinished(SkillExecution execution, ISkillActor actor)
    {
        if (!IsActive(execution))
            return;
        var model = this.GetModel<SkillModel>();
        if (execution.Completed)
            Stop(execution, actor);
        else if (ReferenceEquals(model.BodySkill, execution) && !execution.BlocksBody)
            model.BodySkill = null;
        else if (model.BodySkill == null && execution.BlocksBody)
            model.BodySkill = execution;
    }

    /// <summary>先撤销身份再释放规则，保证回调不能重入已停止执行。</summary>
    /// <param name="execution">待停止执行。</param>
    /// <param name="actor">角色表现。</param>
    private void Stop(SkillExecution execution, ISkillActor actor)
    {
        if (!IsActive(execution))
            return;
        var model = this.GetModel<SkillModel>();
        model.ActiveExecutions.Remove(execution.Id);
        if (ReferenceEquals(model.BodySkill, execution))
            model.BodySkill = null;
        execution.Completed = true;
        execution.Stop(actor);
    }
}
