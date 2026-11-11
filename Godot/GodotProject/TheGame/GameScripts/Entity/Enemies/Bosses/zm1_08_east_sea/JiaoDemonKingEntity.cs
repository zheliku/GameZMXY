using System;
using GameConfig.Battle;
using GameFramework.Entity;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using GameLogic.Effects;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using Godot;

namespace GameLogic.Enemies;

/// <summary>
/// 鲛魔王：背部受击固定一点伤害，旋转时无敌，龙波和死亡旋涡拥有独立生命周期。
/// </summary>
public partial class JiaoDemonKingEntity : AbstractBossEntity
{
    private static readonly EnemyBodyPreset s_bodyPreset = new(typeof(EnemyWalkState),
        typeof(EnemyDeathState),
        () => new EnemyWalkState(), () => new EnemyAttackState(),
        () => new EnemyRecoveryState(), () => new EnemyHurtState(),
        () => new EnemyDeathState(waitForEffect: true)); // 本首领的状态组合；运行实例由每次入场独立创建。
    private static readonly EnemyAiPreset s_preset = new(typeof(DistanceSkillEngageState), typeof(DistanceSkillEngageState),
        () => new DistanceSkillEngageState()); // 本首领的原生范围与冷却仍由自身提供。

    [Export] private PackedScene _dragonWaveScene; // 原生第五源帧生成的龙波。
    [Export] private AnimatedSprite2D _backHit; // 背部承受身体攻击时的原生十帧反馈。
    private float _invulnerabilityLeft; // 旋转开始后的剩余无敌秒数，暂停不推进。

    /// <inheritdoc />
    protected override EnemyBodyPreset BodyPreset => s_bodyPreset;

    /// <inheritdoc />
    protected override EnemyAiPreset AiPreset => s_preset;

    /// <inheritdoc />
    protected override bool Invulnerable => _invulnerabilityLeft > 0f;

    /// <summary>
    /// 初次装配校验原生特效，并只绑定一次背部反馈结束信号。
    /// </summary>
    /// <param name="entityId">运行实体编号。</param>
    /// <param name="entityAssetName">场景资源路径。</param>
    /// <param name="entityGroup">所属框架实体组。</param>
    /// <param name="isNewInstance">是否首次创建。</param>
    /// <param name="userData">显示参数。</param>
    public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
    {
        base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
        if (_dragonWaveScene == null || DeathScene == null || _backHit == null)
        {
            throw new InvalidOperationException("鲛魔王必须绑定龙波、死亡旋涡和背部受击反馈。");
        }

        if (isNewInstance)
        {
            _backHit.AnimationFinished += OnBackHitFinished;
        }
    }

    /// <inheritdoc />
    public override void OnShow(object userData)
    {
        _invulnerabilityLeft = 0f;
        _backHit.Stop();
        _backHit.Visible = false;
        base.OnShow(userData);
    }

    /// <inheritdoc />
    public override void OnHide(bool isShutdown, object userData)
    {
        _invulnerabilityLeft = 0f;
        if (!isShutdown && IsInstanceValid(_backHit))
        {
            _backHit.Stop();
            _backHit.Visible = false;
        }
        base.OnHide(isShutdown, userData);
    }

    /// <inheritdoc />
    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        _invulnerabilityLeft = Math.Max(0f, _invulnerabilityLeft - elapseSeconds);
    }

    /// <summary>
    /// 与来袭方向相同表示背部命中；官方背击不走防御、闪避或暴击公式。
    /// </summary>
    /// <param name="data">仅在本次结算借用的攻击快照。</param>
    /// <returns>背击为一点，其余使用共同战斗规则。</returns>
    protected override DamageResult CalculateIncomingHit(AttackData data)
    {
        return data.Direction == Facing ? new DamageResult(1, false, false, data.Kind) : base.CalculateIncomingHit(data);
    }

    /// <summary>
    /// 背部身体攻击只闪盾而不击退；独立效果仍按官方魔法受击路径执行硬直。
    /// </summary>
    /// <param name="data">本次攻击快照。</param>
    /// <param name="result">已计算的实际伤害。</param>
    /// <param name="knockback">本次击退速度。</param>
    /// <param name="attackerEntityId">攻击者运行编号。</param>
    protected override void OnHurt(AttackData data, DamageResult result, Vector2 knockback, int attackerEntityId)
    {
        if (data.Direction == Facing && !data.IsEffect)
        {
            _backHit.Visible = true;
            _backHit.Stop();
            _backHit.Play();
            base.OnHurt(data, result, Vector2.Zero, attackerEntityId);
            return;
        }
        base.OnHurt(data, result, knockback, attackerEntityId);
    }

    /// <inheritdoc />
    protected override void OnFacingChanged(int dir)
    {
        base.OnFacingChanged(dir);
        _backHit.Scale = new Vector2(dir > 0 ? -1 : 1, 1);
    }

    /// <summary>
    /// 背部反馈由自己的原生十帧播放完成后隐藏，不影响身体当前动作。
    /// </summary>
    private void OnBackHitFinished()
    {
        _backHit.Visible = false;
    }

    /// <summary>
    /// 旋转第一个原生帧开启完整45源帧无敌，时长直接读取该动画的秒数。
    /// </summary>
    private void BeginSpinAnimEvent()
    {
        if (AttackSegment == 2)
        {
            _invulnerabilityLeft = (float)AnimPlayer.CurrentAnimationLength;
        }
    }

    /// <summary>
    /// 官方hit2第五源帧在当前朝向前方创建定点龙波，施放位置保留源分支的纵向差异。
    /// </summary>
    private void SpawnDragonWaveAnimEvent()
    {
        if (AttackSegment != 1)
        {
            return;
        }

        AttackConfig attack = OwnAttacks[1];
        SkillEffectNode effect = RentSkillEffect(_dragonWaveScene);
        int power = (int)Mathf.Lerp(attack.FlatPower.X, attack.FlatPower.Y, GD.Randf());
        effect.Show(attack, GlobalPosition + new Vector2(Facing * 30f, Facing * 30f), Facing,
            CaptureCombatAttributes(), power, false, ReleaseSkillEffect, hitSoundId: HitSoundId);
    }

}
