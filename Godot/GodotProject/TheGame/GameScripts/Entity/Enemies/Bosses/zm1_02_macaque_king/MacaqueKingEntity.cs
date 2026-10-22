using System;
using GameLogic.Enemies.AI;
using Godot;

namespace GameLogic.Enemies;

/// <summary>造一猕猴王：专属选招，旋风全程无敌；身体动作沿用原生动画和怪物状态机。</summary>
public partial class MacaqueKingEntity : AbstractBossEntity
{
    private static readonly EnemyAiPreset m_Preset = new(typeof(MacaqueKingEngageState), typeof(MacaqueKingEngageState),
        () => new MacaqueKingEngageState(),
        () => new CcLockedState(), () => new DeathState()); // 每只猕猴王建立独立 AI 状态。

    /// <inheritdoc />
    protected override EnemyAiPreset AiPreset => m_Preset;

    /// <summary>Monster4.releSkill2 的 76/24 秒恰好覆盖整段旋风，期间不受伤和打断。</summary>
    protected override bool Invulnerable => AttackSegment == 1;

    /// <summary>官方起手第 4 帧创建独立风刃；其后身体收招或受击不取消已发出的攻击。</summary>
    /// <param name="scene">原生动画直接引用的池化风刃场景。</param>
    /// <exception cref="InvalidOperationException">风刃没有登记到 GGF 节点池。</exception>
    private void SpawnWindBladeAnimEvent(PackedScene scene)
    {
        if (AttackSegment != 2)
            return;
        var effect = CreateSkillEffect(scene);
        var attack = OwnAttacks[2];
        float power = Mathf.Lerp(attack.FlatPower.X, attack.FlatPower.Y, GD.Randf()) * AttackMultiplier;
        effect.Show(attack, GlobalPosition, Facing, CaptureCombatAttributes(), (int)power,
            false, ReleaseSkillEffect);
    }

}
