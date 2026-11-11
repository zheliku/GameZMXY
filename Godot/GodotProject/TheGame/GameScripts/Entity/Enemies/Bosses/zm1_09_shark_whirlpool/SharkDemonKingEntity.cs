using System;
using System.Threading;
using System.Threading.Tasks;
using GameConfig.Battle;
using GameConfig.Entity;
using GameFramework.Entity;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Effects;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;

namespace GameLogic.Enemies;

/// <summary>
/// 鲨魔王拥有高低生命两套动作、一次召唤及独立鲨波、水牢和死亡效果。
/// </summary>
public partial class SharkDemonKingEntity : AbstractBossEntity
{
    private static readonly EnemyBodyPreset s_bodyPreset = new(typeof(SharkDemonKingMoveState),
        typeof(EnemyDeathState),
        () => new SharkDemonKingMoveState(), () => new SharkDemonKingAttackState(),
        () => new EnemyRecoveryState(), () => new EnemyHurtState(),
        () => new EnemyDeathState(waitForEffect: true)); // 本首领的状态组合；运行实例由每次入场独立创建。
    private static readonly EnemyAiPreset s_preset = new(typeof(DistanceSkillEngageState), typeof(DistanceSkillEngageState),
        () => new DistanceSkillEngageState()); // 近远距离共用首领时钟，原生范围与冷却保持本物种值。

    [Export] private PackedScene _sharkWaveScene; // 第22源帧释放的横向鲨波。
    [Export] private PackedScene _bubbleWarningScene; // 目标位置的24帧纯视觉警示。
    [Export] private PackedScene _bubbleScene; // 警示结束后的水牢。
    private bool _spawnedChild; // 本次显示中是否已触发一次低血召唤。
    private float _invulnerabilityLeft; // 按原生方法键启动的剩余无敌秒数。
    private CancellationTokenSource _showCancellation; // 本次显示拥有的异步和警示后续回调。
    private int _sharkChildId; // 本次显示拥有的运行编号，不能以可复用对象引用标识所有权。
    private Action<Exception> _spawnFailed; // 本次关卡会话提供的失败出口，不由首领决定导航。

    /// <inheritdoc />
    protected override EnemyBodyPreset BodyPreset => s_bodyPreset;

    /// <inheritdoc />
    protected override EnemyAiPreset AiPreset => s_preset;

    /// <inheritdoc />
    protected override bool Invulnerable => _invulnerabilityLeft > 0f;

    /// <summary>
    /// 当前生命阶段，治疗越过阈值后动作也恢复；召唤仍只触发一次。
    /// </summary>
    internal bool LowPhase => Hp < MaxHp * .7f;

    /// <inheritdoc />
    public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
    {
        base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
        if (isNewInstance && (_sharkWaveScene == null ||
            _bubbleWarningScene == null || _bubbleScene == null || DeathScene == null))
        {
            throw new InvalidOperationException("鲨魔王必须绑定鲨波、水牢警示、水牢和死亡效果。");
        }
    }

    /// <inheritdoc />
    public override void OnShow(object userData)
    {
        _spawnedChild = false;
        _invulnerabilityLeft = 0f;
        _showCancellation = new CancellationTokenSource();
        _spawnFailed = userData is EnemySpawn spawn ? spawn.SpawnFailed : null;
        base.OnShow(userData);
        // 死亡闪烁以身体隐藏结束；池复用先恢复原生RESET的可见性与几何。
        RestartAnim("RESET");
        RestartAnim(EnemyAnims.Idle);
    }

    /// <inheritdoc />
    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        if (IsAlive && !_spawnedChild && LowPhase)
        {
            _spawnedChild = true;
            _ = SpawnChildAsync(GF.Entity, GlobalPosition, _showCancellation.Token, _spawnFailed);
        }
        _invulnerabilityLeft = Math.Max(0f, _invulnerabilityLeft - elapseSeconds);
    }

    /// <inheritdoc />
    public override void OnHide(bool isShutdown, object userData)
    {
        _showCancellation?.Cancel();
        _showCancellation?.Dispose();
        _showCancellation = null;
        if (!isShutdown && _sharkChildId != 0)
        {
            GF.Entity.HideEntitySafe(_sharkChildId);
        }

        _sharkChildId = 0;
        _invulnerabilityLeft = 0f;
        _spawnFailed = null;
        base.OnHide(isShutdown, userData);
    }

    /// <summary>
    /// 等待前捕获服务与显示令牌，迟到的鲨鱼仍由原服务归还，不能污染池复用后的首领。
    /// </summary>
    /// <param name="entities">本次显示的GGF实体服务。</param>
    /// <param name="position">官方与首领相同的出生原点。</param>
    /// <param name="token">本次显示的取消令牌。</param>
    /// <param name="failed">等待前捕获的本次会话失败出口。</param>
    /// <returns>召唤装配完成的任务。</returns>
    private async Task SpawnChildAsync(EntityComponent entities, Vector2 position, CancellationToken token,
        Action<Exception> failed)
    {
        try
        {
            IEntity child = await entities.ShowEntityAsync(EntityId.SharkSpawn, new EnemySpawn(position));
            if (token.IsCancellationRequested)
            {
                entities.HideEntitySafe(child.Id);
                return;
            }
            _sharkChildId = child.Id;
        }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
            {
                Log.Error("[SharkDemonKing] 召唤鲨鱼失败：{0}", exception);
                failed?.Invoke(exception);
            }
        }
    }

    /// <summary>
    /// 原生起手键保持唤鲨20/24秒、水牢18/24秒无敌，暂停不推进。
    /// </summary>
    /// <param name="seconds">官方源帧换算的秒数。</param>
    private void BeginInvulnerabilityAnimEvent(float seconds)
    {
        _invulnerabilityLeft = seconds;
    }

    /// <summary>
    /// 原生第22源帧在首领身后600px、上方40px创建独立鲨波。
    /// </summary>
    private void SpawnSharkWaveAnimEvent()
    {
        if (AttackSegment != 1)
        {
            return;
        }

        int attackId = this.GetUtility<ConfigIdUtility>().GetAttackId(EntityId.SharkDemonKing, 2, 2);
        AttackConfig data = this.GetModel<ConfigurationModel>().Tables.TbAttackConfig.Get(attackId);
        SpawnEffect(_sharkWaveScene, data, GlobalPosition + new Vector2(-Facing * 600f, -40f));
    }

    /// <summary>
    /// 原生第5源帧在目标原点左上方100px创建不镜像的水牢警示。
    /// </summary>
    private void SpawnBubbleAnimEvent()
    {
        if (AttackSegment != 2)
        {
            return;
        }

        CancellationToken token = _showCancellation.Token;
        int attackId = this.GetUtility<ConfigIdUtility>().GetAttackId(EntityId.SharkDemonKing, 3, 2);
        Vector2 position = GlobalPosition + ((IEnemyAiAgent)this).TargetOffset - new Vector2(100, 100);
        SkillEffectNode warning = RentSkillEffect(_bubbleWarningScene);
        warning.Show(null, position, -1, CaptureCombatAttributes(), 0, false, ReleaseSkillEffect,
            () =>
            {
                if (!token.IsCancellationRequested && IsAlive)
                {
                    SpawnEffect(_bubbleScene, this.GetModel<ConfigurationModel>().Tables.TbAttackConfig.Get(attackId), position);
                }
            });
    }

    /// <summary>
    /// 创建本次招式独立的效果、攻击快照和伤害预算。
    /// </summary>
    /// <param name="scene">已绑定的效果场景。</param>
    /// <param name="attack">该效果的独立攻击配置。</param>
    /// <param name="position">官方世界原点。</param>
    private void SpawnEffect(PackedScene scene, AttackConfig attack, Vector2 position)
    {
        SkillEffectNode effect = RentSkillEffect(scene);
        int power = Mathf.RoundToInt(Mathf.Lerp(attack.FlatPower.X, attack.FlatPower.Y, GD.Randf()));
        effect.Show(attack, position, Facing, CaptureCombatAttributes(), power, false, ReleaseSkillEffect, hitSoundId: HitSoundId);
    }
}
