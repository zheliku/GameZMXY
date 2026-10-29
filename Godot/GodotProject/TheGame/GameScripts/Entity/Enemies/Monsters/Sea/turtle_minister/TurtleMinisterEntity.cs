using GameLogic.Enemies.AI;
using GameLogic.Architecture;
using GameLogic.Level;
using GameLogic.Config;
using GameFramework.Entity;
using Godot;
using System;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 龟丞相：长程魔法攻击；浅岸守关与后续普通变体共用同一身份。
    /// </summary>
    public partial class TurtleMinisterEntity : AbstractSeaMonsterEntity
    {
        private const int ShoreLevelId = 7; // 守关变体只属于浅岸，其他海域继续使用基础值。
        [Export] private TurtleGuardianConfigResource m_GuardianConfig; // 浅岸专属生命与掉落数值。

        private static readonly EnemyAiPreset m_Preset = new(typeof(WanderState), typeof(TurtleMinisterEngageState),
            () => new WanderState(), () => new PauseState(), () => new TurtleMinisterEngageState(),
            () => new CcLockedState(), () => new DeathState()); // 巡逻和本物种交战共用实例时钟。

        /// <inheritdoc />
        protected override EnemyAiPreset AiPreset
        {
            get
            {
                return m_Preset;
            }
        }

        /// <inheritdoc />
        public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
        {
            base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
            if (m_GuardianConfig == null)
            {
                throw new InvalidOperationException("龟丞相必须绑定浅岸守关配置资源。");
            }

            m_GuardianConfig.Validate();
        }

        /// <inheritdoc />
        protected override void ApplyConfigOverrides()
        {
            base.ApplyConfigOverrides();
            // Monster17 在官方3-1为守关变体：每球灵魂翻倍且不掉装备，后续海域恢复基础奖励。
            if (this.GetModel<LevelModel>()?.LevelId == ShoreLevelId)
            {
                m_MaxHp = m_GuardianConfig.MaxHp;
                m_ExperienceReward = m_GuardianConfig.Experience;
                SoulPerOrbOverride = m_GuardianConfig.SoulPerOrb;
                EquipmentDropChanceOverride = m_GuardianConfig.EquipmentDropChance;
            }
        }
    }
}
