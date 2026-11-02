using GameLogic.Enemies.AI;
using GameConfig.Battle;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 虾兵：保持射击距离，原生第3帧发出可被玩家攻击打散的魔法弹。
    /// </summary>
    public partial class ShrimpDemonEntity : AbstractSeaMonsterEntity
    {
        private EnemyAiPreset m_Preset; // 巡逻与交战共用实例时钟，状态工厂注入场景退避内界。

        [Export] private CollisionShape2D m_RetreatRange; // 本物种的退避内界，不扩展通用敌人接口。
        [Export] private PackedScene m_ProjectileScene; // 原生场景绑定的加速魔法弹。

        /// <inheritdoc />
        protected override EnemyAiPreset AiPreset
        {
            get
            {
                return m_Preset ??= new(typeof(PatrolState), typeof(ShrimpDemonEngageState),
                    () => new PatrolState(), () => new ShrimpDemonEngageState(RetreatDistance));
            }
        }

        /// <summary>
        /// 场景退避形状的水平半径，像素。
        /// </summary>
        internal float RetreatDistance
        {
            get
            {
                return m_RetreatRange.Shape.GetRect().End.X;
            }
        }

        /// <inheritdoc />
        public override void OnInit(int entityId, string entityAssetName, GameFramework.Entity.IEntityGroup entityGroup,
            bool isNewInstance, object userData)
        {
            base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
            if (isNewInstance && (m_RetreatRange?.Shape is not SegmentShape2D || !m_RetreatRange.Disabled))
            {
                throw new System.InvalidOperationException("本物种必须绑定禁用物理碰撞的原生选招范围。");
            }
        }

        /// <summary>
        /// 在官方攻击第3源帧创建离身魔法弹，弹体拥有独立命中与消散生命周期。
        /// </summary>
        private void SpawnProjectileAnimEvent()
        {
            if (AttackSegment != 0)
            {
                return;
            }

            AttackConfig attack = OwnAttacks[0];
            var effect = CreateSkillEffect(m_ProjectileScene);
            int power = (int)Mathf.Lerp(attack.FlatPower.X, attack.FlatPower.Y, GD.Randf());
            effect.Show(attack, GlobalPosition, Facing, CaptureCombatAttributes(), power,
                false, ReleaseSkillEffect, hitSoundId: HitSoundId);
        }
    }
}
