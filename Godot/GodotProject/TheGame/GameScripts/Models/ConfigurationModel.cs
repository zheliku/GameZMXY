using GameLogic.Heroes.Body;
using GameLogic.Enemies.Body;
using GameConfig.Battle;
using GameConfig.Enemy;
using GameConfig.Entity;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Skills;
using System;
using GameConfig;
using GameLogic.Architecture;

namespace GameLogic.Config
{
    /// <summary>
    /// 应用级配置模型：拥有配置查询索引和同类实体共享的只读身体参数。
    /// </summary>
    /// <remarks>启动时建立索引；身体参数在对应场景首次载入时构建；不保留节点或构建委托。</remarks>
    public sealed class ConfigurationModel : AbstractModel
    {
        private readonly Dictionary<EntityId, IReadOnlyList<AttackConfig>> m_Attacks; // 同类实体共用的有序招式定义。
        private readonly Dictionary<SkillHero, IReadOnlyList<SkillConfig>> m_ActiveSkills; // 按角色和官方行序建立一次的技能目录。
        private readonly Dictionary<EntityId, EnemyConfig> m_Enemies; // 同一实体到唯一基础定义的只读索引。

        /// <summary>
        /// 同类英雄共享的身体参数，由配置系统首次建立。
        /// </summary>
        internal Dictionary<int, HeroBodyParams> HeroBodies { get; } = new();

        /// <summary>
        /// 同类怪物共享的身体参数，由配置系统首次建立。
        /// </summary>
        internal Dictionary<EntityId, EnemyBodyParams> EnemyBodies { get; } = new();

        /// <summary>
        /// 借用 ConfigSystem 已加载的同一总表，运行期只读。
        /// </summary>
        public Tables Tables { get; }

        /// <summary>
        /// 建立配置查询索引；跨表约束由 ConfigurationSystem 校验。
        /// </summary>
        /// <param name="tables">已加载的配置总表。</param>
        /// <exception cref="ArgumentNullException">总表为空。</exception>
        public ConfigurationModel(Tables tables)
        {
            Tables = tables ?? throw new ArgumentNullException(nameof(tables));
            m_Enemies = new Dictionary<EntityId, EnemyConfig>();
            foreach (var enemy in tables.TbEnemyConfig.DataList)
            {
                // 同实体的难度档共用基础定义，保持原表中首条记录的选择规则。
                m_Enemies.TryAdd(enemy.EntityId, enemy);
            }

            m_Attacks = new Dictionary<EntityId, IReadOnlyList<AttackConfig>>();
            var ownedAttacks = tables.TbAttackConfig.DataList
                .Where(attack => attack.OwnerId != EntityId.None && attack.ComboIndex >= 0);
            foreach (var attacksByOwner in ownedAttacks.GroupBy(attack => attack.OwnerId))
            {
                var orderedAttacks = attacksByOwner.OrderBy(attack => attack.ComboIndex).ToArray();
                m_Attacks.Add(attacksByOwner.Key, Array.AsReadOnly(orderedAttacks));
            }

            m_ActiveSkills = new Dictionary<SkillHero, IReadOnlyList<SkillConfig>>();
            foreach (var skillsByHero in tables.TbSkillConfig.DataList.GroupBy(skill => skill.Hero))
            {
                var orderedSkills = skillsByHero.OrderBy(skill => skill.Order).ToArray();
                m_ActiveSkills.Add(skillsByHero.Key, Array.AsReadOnly(orderedSkills));
            }
        }

        /// <summary>
        /// 按官方面板行序读取一名角色的主动技能。
        /// </summary>
        /// <param name="hero">查看的角色；不表示当前可以出战。</param>
        /// <returns>排好序的只读定义列表。</returns>
        public IReadOnlyList<SkillConfig> GetActive(SkillHero hero)
        {
            return m_ActiveSkills.GetValueOrDefault(hero, Array.Empty<SkillConfig>());
        }

        /// <summary>
        /// 按实体类型读取唯一的基础怪物定义，不为实体复制 AI 参数。
        /// </summary>
        /// <param name="entityId">怪物实体类型。</param>
        /// <returns>共享只读定义。</returns>
        public EnemyConfig GetEnemy(EntityId entityId)
        {
            return m_Enemies[entityId];
        }

        /// <summary>
        /// 借用同类实体共用的有序招式，不在每个实体重新复制和排序。
        /// </summary>
        /// <param name="entityId">拥有招式的实体类型。</param>
        /// <returns>共享只读列表；无招式时为空。</returns>
        public IReadOnlyList<AttackConfig> GetAttacks(EntityId entityId)
        {
            return m_Attacks.GetValueOrDefault(entityId, Array.Empty<AttackConfig>());
        }

        /// <summary>
        /// 借用已构建的共享英雄参数。
        /// </summary>
        /// <param name="heroId">英雄主键。</param>
        /// <returns>共享参数。</returns>
        public HeroBodyParams GetHeroBodyParams(int heroId)
        {
            return HeroBodies[heroId];
        }

        /// <summary>
        /// 借用已构建的共享怪物参数。
        /// </summary>
        /// <param name="entityId">怪物实体类型。</param>
        /// <returns>共享参数。</returns>
        public EnemyBodyParams GetEnemyBodyParams(EntityId entityId)
        {
            return EnemyBodies[entityId];
        }
    }
}
