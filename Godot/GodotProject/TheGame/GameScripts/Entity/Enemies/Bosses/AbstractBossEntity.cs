using GameLogic.Effects;
using GameLogic.Enemies.Body;
using Godot;

namespace GameLogic.Enemies;

/// <summary>
/// 首领分支装配当前首领的地面身体；独立效果继承 Enemy，专属选招与无敌由具体首领实现。
/// </summary>
public abstract partial class AbstractBossEntity : AbstractEnemyEntity
{
    private static readonly EnemyBodyPreset s_deathEffectPreset = EnemyBodyPresets.Walk(true); // 普通地面首领共享状态配方，实例不共享。
    /// <summary>
    /// 原生死亡轴引用的官方BossDead；特殊形态按自己的死亡流程选择是否绑定。
    /// </summary>
    [Export] protected PackedScene DeathScene;

    /// <inheritdoc />
    protected override EnemyBodyPreset BodyPreset => DeathScene == null ? base.BodyPreset : s_deathEffectPreset;

    /// <summary>
    /// 死亡轴完成闪烁后创建官方旋涡，结束时通知本次死亡状态再回收首领。
    /// </summary>
    protected virtual void SpawnBossDeathAnimEvent()
    {
        var state = (EnemyDeathState)BodyFsm.CurrentState;
        SkillEffectNode effect = RentSkillEffect(DeathScene);
        effect.Show(null, GlobalPosition, Facing, CaptureCombatAttributes(), 0, false, ReleaseSkillEffect,
            state.CompleteEffect);
    }

    /// <summary>
    /// 官方首领进入死亡动作后立即停止受击，避免守关条与尸体重复结算。
    /// </summary>
    protected override bool KeepHurtBoxDuringDeath => false;

    /// <summary>
    /// 当前地面首领落地后才可出招，通用忙碌/死亡校验仍归Enemy。
    /// </summary>
    /// <param name="index">首领招式下标。</param>
    /// <returns>是否接受请求。</returns>
    protected override bool TryRequestAttack(int index) => OnFloor && base.TryRequestAttack(index);
}
