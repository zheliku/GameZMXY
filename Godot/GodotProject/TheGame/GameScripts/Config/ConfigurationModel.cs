using GameLogic.Heroes.Body;
using GameLogic.Monsters.Body;
using GameConfig.Battle;
using GameConfig.Monster;
using GameConfig.Entity;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Skills;
using System;
using GameConfig;
using GameLogic.Architecture;
using GameLogic.Profile;

namespace GameLogic.Config;

/// <summary>应用级配置模型：拥有查询索引、经验曲线和同类实体共享的只读身体参数。</summary>
/// <remarks>启动时建立索引；身体参数在对应场景首次载入时构建；不保留节点或构建委托。</remarks>
public sealed class ConfigurationModel : AbstractModel
{
    private readonly Dictionary<EntityId, IReadOnlyList<AttackConfig>> m_Attacks; // 同类实体共用的有序招式定义。
    private readonly Dictionary<SkillHero, IReadOnlyList<SkillConfig>> m_ActiveSkills; // 按角色和官方行序建立一次的技能目录。
    private readonly Dictionary<int, IReadOnlyList<PassiveConfig>> m_Passives; // 按等级建立一次的被动目录。
    private readonly Dictionary<EntityId, MonsterConfig> m_Monsters; // 同一实体到唯一基础定义的只读索引。
    private readonly Dictionary<int, HeroBodyParams> m_HeroBodies = new(); // 每名英雄仅构建一次的身体参数。
    private readonly Dictionary<EntityId, MonsterBodyParams> m_MonsterBodies = new(); // 每类怪物仅构建一次的身体参数。

    /// <summary>借用 ConfigSystem 已加载的同一总表，运行期只读。</summary>
    public Tables Tables { get; }

    /// <summary>本模型拥有的经验曲线，所有档案共享使用。</summary>
    public ExperienceCurve ExperienceCurve { get; }

    /// <summary>建立配置查询索引与共享经验曲线；跨表约束由 ConfigurationSystem 校验。</summary>
    /// <param name="tables">已加载的配置总表。</param>
    /// <exception cref="ArgumentNullException">总表为空。</exception>
    /// <exception cref="ArgumentException">经验曲线约束不满足。</exception>
    public ConfigurationModel(Tables tables)
    {
        Tables = tables ?? throw new ArgumentNullException(nameof(tables));
        m_Monsters = tables.TbMonsterConfig.DataList.GroupBy(x => x.EntityId).ToDictionary(x => x.Key, x => x.First());
        m_Attacks = tables.TbAttackConfig.DataList.GroupBy(x => x.OwnerId).ToDictionary(x => x.Key,
            x => (IReadOnlyList<AttackConfig>)Array.AsReadOnly(x.OrderBy(a => a.ComboIndex).ToArray()));
        m_ActiveSkills = tables.TbSkillConfig.DataList.GroupBy(x => x.Hero).ToDictionary(x => x.Key,
            x => (IReadOnlyList<SkillConfig>)Array.AsReadOnly(x.OrderBy(a => a.Order).ToArray()));
        m_Passives = tables.TbPassiveConfig.DataList.GroupBy(x => x.Level).ToDictionary(x => x.Key,
            x => (IReadOnlyList<PassiveConfig>)Array.AsReadOnly(x.OrderBy(a => a.PassiveId).ToArray()));
        ExperienceCurve = new ExperienceCurve(tables.TbHeroLevelConfig.DataList);
    }

    /// <summary>按官方面板行序读取一名角色的主动技能。</summary>
    /// <param name="hero">查看的角色；不表示当前可以出战。</param>
    /// <returns>排好序的只读定义列表。</returns>
    public IReadOnlyList<SkillConfig> GetActive(SkillHero hero) =>
        m_ActiveSkills.GetValueOrDefault(hero, Array.Empty<SkillConfig>());

    /// <summary>读取五项被动的指定等级，面板初期以第一级作为下一级效果。</summary>
    /// <param name="level">配置等级，从 1 开始。</param>
    /// <returns>按官方被动顺序排列的定义。</returns>
    public IReadOnlyList<PassiveConfig> GetPassives(int level) =>
        m_Passives.GetValueOrDefault(level, Array.Empty<PassiveConfig>());

    /// <summary>按实体类型读取唯一的基础怪物定义，不为实体复制 AI 参数。</summary>
    /// <param name="entityId">怪物实体类型。</param>
    /// <returns>共享只读定义。</returns>
    public MonsterConfig GetMonster(EntityId entityId) => m_Monsters[entityId];

    /// <summary>借用同类实体共用的有序招式，不在每个实体重新复制和排序。</summary>
    /// <param name="entityId">拥有招式的实体类型。</param>
    /// <returns>共享只读列表；无招式时为空。</returns>
    public IReadOnlyList<AttackConfig> GetAttacks(EntityId entityId) =>
        m_Attacks.GetValueOrDefault(entityId, Array.Empty<AttackConfig>());

    /// <summary>首次加载同类英雄时建立参数，后续池实例复用。</summary>
    /// <param name="heroId">英雄配置主键。</param>
    /// <param name="create">从当前场景动画读取纯数据的工厂；模型不保留委托。</param>
    public void EnsureHeroBodyParams(int heroId, Func<HeroBodyParams> create)
    {
        if (!m_HeroBodies.ContainsKey(heroId))
            m_HeroBodies.Add(heroId, create());
    }

    /// <summary>首次加载同类怪物时建立参数，后续池实例复用。</summary>
    /// <param name="entityId">怪物实体类型。</param>
    /// <param name="create">从当前场景动画读取纯数据的工厂；模型不保留委托。</param>
    public void EnsureMonsterBodyParams(EntityId entityId, Func<MonsterBodyParams> create)
    {
        if (!m_MonsterBodies.ContainsKey(entityId))
            m_MonsterBodies.Add(entityId, create());
    }

    /// <summary>借用已构建的共享英雄参数。</summary>
    /// <param name="heroId">英雄主键。</param>
    /// <returns>共享参数。</returns>
    public HeroBodyParams GetHeroBodyParams(int heroId) => m_HeroBodies[heroId];

    /// <summary>借用已构建的共享怪物参数。</summary>
    /// <param name="entityId">怪物实体类型。</param>
    /// <returns>共享参数。</returns>
    public MonsterBodyParams GetMonsterBodyParams(EntityId entityId) => m_MonsterBodies[entityId];
}
