using GameConfig.Battle;
using GameLogic.Enemies.AI;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 七星洞飞刀小怪：身体动作与远程飞刀由两段原版招式分别驱动。
    /// </summary>
    public partial class KnifeDemonEntity : AbstractGroundMonsterEntity
    {
        private static readonly EnemyAiPreset m_Preset = new(typeof(PatrolState), typeof(EngageState),
            () => new PatrolState(), () => new EngageState(cooldownAttackIndex: 1)); // 原版一秒决策、四秒优先投刀，不参与通用随机选招。

        /// <summary>
        /// 普通小怪巡逻/追击；目标靠近后由原生攻击2方法事件投出飞刀。
        /// </summary>
        protected override EnemyAiPreset AiPreset
        {
            get
            {
                return m_Preset;
            }
        }

        /// <summary>
        /// Monster13 的动画在第 3 帧投刀；飞刀持续按自己的动画运动和命中。
        /// </summary>
        /// <param name="scene">由动画资源直接引用的飞刀效果场景。</param>
        private void ThrowKnifeAnimEvent(PackedScene scene)
        {
            if (AttackSegment != 1)
            {
                return;
            }

            var effect = RentSkillEffect(scene);
            AttackConfig attack = OwnAttacks[1];
            int power = (int)Mathf.Lerp(attack.FlatPower.X, attack.FlatPower.Y, GD.Randf());
            effect.Show(attack, GlobalPosition + new Vector2(Facing * 20f, 0f), Facing,
                CaptureCombatAttributes(), power, false, ReleaseSkillEffect, hitSoundId: HitSoundId);
        }
    }
}
