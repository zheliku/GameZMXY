using GameConfig.Battle;
using GameLogic.Entity;
using GameLogic.Enemies.AI;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 狮驼王：保留三段官方身体招式；毒雾与状态效果由本首领实体拥有。
    /// </summary>
    public partial class LionCamelKingEntity : AbstractBossEntity
    {
        private static readonly EnemyAiPreset m_Preset = new(typeof(LionCamelKingEngageState), typeof(LionCamelKingEngageState),
            () => new LionCamelKingEngageState(), () => new CcLockedState(), () => new DeathState()); // 各实例独立持有选招时钟。

        /// <inheritdoc />
        protected override EnemyAiPreset AiPreset
        {
            get
            {
                return m_Preset;
            }
        }

        /// <summary>
        /// 原版 setYourFather(47) 覆盖完整攻击2（碎石与双侧光圈），秒长由47帧原生动画决定。
        /// </summary>
        protected override bool Invulnerable
        {
            get
            {
                return AttackSegment == 1;
            }
        }

        /// <summary>
        /// 首次发现按官方原点矩形：横向视野内且高度差不超过150；发现后使用300的交战高度。
        /// </summary>
        /// <param name="actor">索敌区中的候选角色。</param>
        /// <returns>是否满足狮驼王的首次发现条件。</returns>
        protected override bool CanSelectTarget(AbstractActorEntity actor)
        {
            return base.CanSelectTarget(actor) &&
            Mathf.Abs(actor.GlobalPosition.X - GlobalPosition.X) < Config.SightRange &&
            Mathf.Abs(actor.GlobalPosition.Y - GlobalPosition.Y) <= 150f;
        }

        /// <summary>
        /// 官方双侧旋风不重新转向；棍击与毒雾才面向目标。
        /// </summary>
        /// <param name="index">本首领的招式下标。</param>
        protected override void FaceAttackTarget(int index)
        {
            if (index != 1)
            {
                base.FaceAttackTarget(index);
            }
        }

        /// <summary>
        /// Monster11 在毒雾收招帧创建独立的十秒云雾攻击。
        /// </summary>
        /// <param name="scene">由攻击动画直接引用的毒雾场景。</param>
        private void SpawnPoisonCloudAnimEvent(PackedScene scene)
        {
            if (AttackSegment != 2)
            {
                return;
            }

            var effect = CreateSkillEffect(scene);
            AttackConfig attack = OwnAttacks[2];
            int power = (int)Mathf.Lerp(attack.FlatPower.X, attack.FlatPower.Y, GD.Randf());
            Vector2 position = GlobalPosition + new Vector2(Facing * 175f, 20f);
            effect.Show(attack, position, Facing, CaptureCombatAttributes(), power, false, ReleaseSkillEffect,
                hitSoundId: HitSoundId);
        }
    }
}
