using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Skills;
using GameLogic.Battle;
using GameLogic.Input;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Profile;
using GameLogic.Heroes.Body;

namespace GameLogic.Skills
{
    /// <summary>
    /// 技能学习与施放规则入口；借用档案、技能书、英雄数值及只读配置，不持有场景节点。
    /// </summary>
    /// <param name="architecture">借用的所属作用域；模型和服务由作用域持有。</param>
    public sealed class SkillSystem(IArchitecture architecture) : AbstractSystem(architecture)
    {
        private readonly Dictionary<ESkillId, SkillDefinition> m_SkillDefinitions = new(); // 当前英雄实体登记的状态类型和起手条件，不属于英雄数值。

        /// <summary>
        /// 当前身体技能登记的 GGF 状态；具体角色负责在身体状态机中注册它。
        /// </summary>
        public Type BodyStateType
        {
            get
            {
                return this.GetModel<HeroModel>()?.ActiveSkillId is { } id &&
                       m_SkillDefinitions.TryGetValue(id, out var definition)
                    ? definition.BodyState
                    : null;
            }
        }

        /// <inheritdoc />
        protected override void OnDeinit()
        {
            ClearSkillDefinitions();
        }

        // ---- 业务方法 ----

        /// <summary>
        /// 购买出战英雄的下一级被动；只应用该等级的总加成，满级或不足时不扣费。
        /// </summary>
        /// <param name="id">本次要升级的被动。</param>
        /// <returns>购买结果；AlreadyLearned表示已满级。</returns>
        public ESkillLearnResult Upgrade(EPassiveId id)
        {
            var definition = this.GetModel<ConfigurationModel>().Tables.TbPassiveConfig.GetOrDefault(id);
            if (definition == null)
            {
                return ESkillLearnResult.Unavailable;
            }

            var profile = this.GetModel<ProfileModel>();
            var levels = this.GetModel<SkillModel>().GetPassiveLevels(profile.ActiveHeroId);
            int current = levels[id];
            if (current >= definition.Levels.Count)
            {
                return ESkillLearnResult.AlreadyLearned;
            }

            if (!this.GetSystem<ProfileSystem>().TrySpendSoul(definition.Levels[current].LearnSoul))
            {
                return ESkillLearnResult.InsufficientSoul;
            }

            levels[id] = current + 1;
            return ESkillLearnResult.Learned;
        }

        /// <summary>
        /// 一次性学习当前英雄的主动技能；重复学习、其他角色和余额不足都不扣费。
        /// </summary>
        /// <param name="skillId">主动技能定义。</param>
        /// <returns>学习结果，界面据此展示反馈。</returns>
        public ESkillLearnResult Learn(ESkillId skillId)
        {
            if (!IsLearnable(skillId))
            {
                return ESkillLearnResult.Unavailable;
            }

            var definition = this.GetModel<ConfigurationModel>().Tables.TbSkillConfig.Get(skillId);
            var profile = this.GetModel<ProfileModel>();
            var learned = this.GetModel<SkillModel>().GetLearnedSkills(profile.ActiveHeroId);
            if (learned.Contains(skillId))
            {
                return ESkillLearnResult.AlreadyLearned;
            }

            if (!this.GetSystem<ProfileSystem>().TrySpendSoul(definition.LearnSoul))
            {
                return ESkillLearnResult.InsufficientSoul;
            }

            learned.Add(skillId);
            return ESkillLearnResult.Learned;
        }

        /// <summary>
        /// 查询本档案当前出战英雄是否已学习指定技能。
        /// </summary>
        /// <param name="skillId">技能定义。</param>
        /// <returns>已学习时为 true。</returns>
        public bool IsLearned(ESkillId skillId)
        {
            return this.GetModel<SkillModel>()
            .GetLearnedSkills(this.GetModel<ProfileModel>().ActiveHeroId).Contains(skillId);
        }

        /// <summary>
        /// 查询当前出战英雄的配置目录是否允许购买该技能，不要求战斗实体已登记状态。
        /// </summary>
        /// <param name="id">技能编号。</param>
        /// <returns>当前英雄的技能配置存在时为真。</returns>
        public bool IsLearnable(ESkillId id)
        {
            var definition = this.GetModel<ConfigurationModel>().Tables.TbSkillConfig.GetOrDefault(id);
            return definition != null && (int)definition.Hero == this.GetModel<ProfileModel>().ActiveHeroId;
        }

        /// <summary>
        /// 查询当前战斗英雄实体是否已登记此技能状态。
        /// </summary>
        /// <param name="id">技能编号。</param>
        /// <returns>当前可用时为真。</returns>
        public bool IsAvailable(ESkillId id)
        {
            var model = this.GetModel<HeroModel>();
            var tables = this.GetModel<ConfigurationModel>().Tables;
            var definition = tables.TbSkillConfig.GetOrDefault(id);
            return model != null && definition != null && (int)definition.Hero == this.GetModel<ProfileModel>().ActiveHeroId &&
                   m_SkillDefinitions.ContainsKey(id);
        }

        /// <summary>
        /// 读取当前蓝量是否足够，不预扣资源。
        /// </summary>
        /// <param name="id">技能编号。</param>
        /// <returns>蓝量足够时为真。</returns>
        public bool CanAfford(ESkillId id)
        {
            return this.GetModel<HeroModel>().Mp.Value >=
                                             this.GetModel<ConfigurationModel>().Tables.TbSkillConfig.Get(id).ManaCost;
        }

        /// <summary>
        /// 校验当前技能组、学习记录、起手条件和费用，登记一次施放；动画在 GGF 进入对应状态后开始。
        /// </summary>
        /// <param name="id">技能编号。</param>
        /// <param name="actor">本次调用借用的角色能力。</param>
        /// <returns>本次登记并扣费成功时为真。</returns>
        public bool TryActivate(ESkillId id, ISkillActor actor)
        {
            var model = this.GetModel<HeroModel>();
            // 先检查本局控制权和当前动作，再检查该技能的学习与起手条件。
            if (model == null || !model.ControlsEnabled || this.GetSystem<HeroStatusSystem>().IsFrozen || model.ActiveSkillId != null)
            {
                return false;
            }
            if (!IsAvailable(id) || !IsLearned(id))
            {
                return false;
            }
            if (!m_SkillDefinitions.TryGetValue(id, out var definition) || definition.CanStart?.Invoke(actor) == false)
            {
                return false;
            }

            var manaCost = this.GetModel<ConfigurationModel>().Tables.TbSkillConfig.Get(id).ManaCost;
            if (definition.DeferredMana)
            {
                if (!CanAfford(id))
                {
                    return false;
                }
            }
            else if (!this.GetSystem<HeroSystem>().TrySpendMp(manaCost))
            {
                return false;
            }

            model.ActiveSkillId = id;
            return true;
        }

        /// <summary>
        /// 扣除已激活的延迟耗蓝技能；未满足延迟契约时不改变蓝量。
        /// </summary>
        public bool TrySpendActivatedMana(ESkillId id)
        {
            var model = this.GetModel<HeroModel>();
            if (model?.ActiveSkillId != id || !m_SkillDefinitions.TryGetValue(id, out var definition) || !definition.DeferredMana)
            {
                return false;
            }

            return this.GetSystem<HeroSystem>().TrySpendMp(
                this.GetModel<ConfigurationModel>().Tables.TbSkillConfig.Get(id).ManaCost);
        }

        /// <summary>
        /// 选择角色或武器的一组技能登记；校验完成后才取消旧施放并替换模型数据。
        /// </summary>
        /// <param name="heroId">此组所属英雄。</param>
        /// <param name="definitions">角色提供的登记，复制到模型，调用方不拥有运行时容器。</param>
        /// <exception cref="ArgumentException">角色、编号、动作绑定或状态不合法。</exception>
        /// <exception cref="InvalidOperationException">本局英雄模型尚未注册。</exception>
        public void SelectSkills(int heroId, params SkillDefinition[] definitions)
        {
            ArgumentNullException.ThrowIfNull(definitions);
            var model = this.GetModel<HeroModel>() ?? throw new InvalidOperationException("登记技能前必须先创建本局英雄模型。");
            var tables = this.GetModel<ConfigurationModel>().Tables;
            if (heroId != this.GetModel<ProfileModel>().ActiveHeroId || definitions.Length == 0)
            {
                throw new ArgumentException("技能登记须属于当前英雄，编号、状态和动作绑定须合法且不重复。", nameof(definitions));
            }

            // 完整校验后才取消旧技能：状态类型、配置归属和输入唯一性分别检查。
            var skillIds = new HashSet<ESkillId>();
            var inputActions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                if (definition == null || definition.BodyState == null || definition.BodyState.IsAbstract ||
                    !typeof(AbstractHeroSkillState).IsAssignableFrom(definition.BodyState))
                {
                    throw new ArgumentException("技能登记必须提供具体的英雄技能状态。", nameof(definitions));
                }

                var skill = tables.TbSkillConfig.GetOrDefault(definition.Id);
                if (skill == null || (int)skill.Hero != heroId || !skillIds.Add(definition.Id))
                {
                    throw new ArgumentException("技能编号必须属于当前英雄且不重复。", nameof(definitions));
                }

                if (definition.InputAction != null &&
                    (string.IsNullOrWhiteSpace(definition.InputAction) || !inputActions.Add(definition.InputAction)))
                {
                    throw new ArgumentException("技能输入动作必须非空白且不重复。", nameof(definitions));
                }
            }

            if (m_SkillDefinitions.Values.SequenceEqual(definitions))
            {
                return;
            }

            CancelSkill();
            m_SkillDefinitions.Clear();
            foreach (var definition in definitions)
            {
                m_SkillDefinitions.Add(definition.Id, definition);
            }
        }

        /// <summary>
        /// 按当前登记尝试一个直接动作；拒绝后不排队，也不尝试较低优先级的同时按键。
        /// </summary>
        /// <param name="actor">本次角色表现。</param>
        /// <param name="data">当前角色的输入消费记录与游戏时钟。</param>
        /// <returns>匹配并激活成功时为真。</returns>
        public bool TryActivateInput(ISkillActor actor, HeroActionData data)
        {
            var input = this.GetSystem<InputSystem>();
            foreach (var definition in m_SkillDefinitions.Values)
            {
                if (definition.InputAction is { } action && input.ConsumePress(data, action))
                {
                    return TryActivate(definition.Id, actor);
                }
            }

            return false;
        }

        /// <summary>
        /// 换组、离场或死亡时取消当前施放并清除无敌区间；具体状态的退出负责释放动画与判定。
        /// </summary>
        public void CancelSkill()
        {
            var model = this.GetModel<HeroModel>();
            if (model == null)
            {
                return;
            }

            model.ActiveSkillId = null;
            model.SkillInvulnerableFrom = double.PositiveInfinity;
            model.SkillInvulnerableUntil = 0;
        }

        /// <summary>
        /// 当前英雄实体回池或卸载时撤销其状态类型与起手条件登记。
        /// </summary>
        public void ClearSkillDefinitions()
        {
            CancelSkill();
            m_SkillDefinitions.Clear();
        }

        /// <summary>
        /// 当前招式正常收招后解除身体占用，保留按秒计算的无敌区间；过期招式不得结束其他技能。
        /// </summary>
        /// <param name="id">正在退出的状态所对应的技能。</param>
        public void EndSkill(ESkillId id)
        {
            var model = this.GetModel<HeroModel>();
            if (model?.ActiveSkillId == id)
            {
                model.ActiveSkillId = null;
            }
        }

        /// <summary>
        /// 登记一个以游戏秒数计的无敌区间，不受动画跳帧影响。
        /// </summary>
        /// <param name="from">开始的游戏时刻。</param>
        /// <param name="duration">持续秒数。</param>
        public void GrantInvulnerability(double from, double duration)
        {
            var model = this.GetModel<HeroModel>();
            model.SkillInvulnerableFrom = from;
            model.SkillInvulnerableUntil = from + duration;
        }

        /// <summary>
        /// 查询当前游戏时刻是否在技能无敌区间。
        /// </summary>
        /// <param name="now">游戏时刻，秒。</param>
        /// <returns>处于无敌区间时为真。</returns>
        public bool IsInvulnerable(double now)
        {
            var model = this.GetModel<HeroModel>();
            return model != null && now >= model.SkillInvulnerableFrom && now < model.SkillInvulnerableUntil;
        }
    }

    /// <summary>
    /// 角色提供的一条不可变技能登记，具体行为由登记的 GGF 状态实现。
    /// </summary>
    /// <param name="Id">技能编号。</param>
    /// <param name="BodyState">本技能的 GGF 身体状态。</param>
    /// <param name="InputAction">可选直接动作名；组合技能由具体角色匹配。</param>
    /// <param name="CanStart">可选起手条件；在扣费前检查。</param>
    public sealed record SkillDefinition(ESkillId Id, Type BodyState,
        string InputAction = null, Func<ISkillActor, bool> CanStart = null, bool DeferredMana = false);

    /// <summary>
    /// 学习请求的业务结果；不以 UI 状态代替资格和余额检查。
    /// </summary>
    public enum ESkillLearnResult
    {
        /// <summary>
        /// 本次已扣费并学习。
        /// </summary>
        Learned,
        /// <summary>
        /// 已经学习，不重复扣费。
        /// </summary>
        AlreadyLearned,
        /// <summary>
        /// 灵魂不足，所有数据不变。
        /// </summary>
        InsufficientSoul,
        /// <summary>
        /// 不是当前英雄的技能，或动作尚未实现。
        /// </summary>
        Unavailable
    }
}
