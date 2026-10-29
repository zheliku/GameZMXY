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
        private static readonly EnemyAiPreset m_Preset = new(typeof(WanderState), typeof(ShrimpDemonEngageState),
            () => new WanderState(), () => new PauseState(), () => new ShrimpDemonEngageState(),
            () => new CcLockedState(), () => new DeathState()); // 巡逻和本物种交战共用实例时钟。

        [Export] private PackedScene m_ProjectileScene; // 原生场景绑定的加速魔法弹。

        /// <inheritdoc />
        protected override EnemyAiPreset AiPreset
        {
            get
            {
                return m_Preset;
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
