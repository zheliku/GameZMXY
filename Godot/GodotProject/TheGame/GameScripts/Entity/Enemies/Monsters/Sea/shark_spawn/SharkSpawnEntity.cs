using System;
using GameConfig.Battle;
using GameFramework.Entity;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using GameLogic.Config;
using GameLogic.Effects;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using GameLogic.Entity;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies;

/// <summary>
/// 鲨魔王低生命阶段召唤的飞行鲨鱼，拥有接触斩击和隐藏后地面旋风。
/// </summary>
public partial class SharkSpawnEntity : AbstractSeaMonsterEntity, IEnemyFreeMoveBody
{
    private EnemyAiPreset _preset; // 追踪与近身决策共用时钟，工厂只传入范围和实时运动事实。
    private static readonly EnemyBodyPreset s_bodyPreset = EnemyBodyPresets.Swim<SharkSpawnAttackState>(
        288f, 96f, 72f); // 游动保持原加速度/退避速度，仅替换为鲨鱼专属攻击。

    [Export] private CollisionShape2D _trackingRange; // 本鲨鱼追踪高度，与250×50px近身条件分开。
    [Export] private PackedScene _slashScene; // 接触目标后原地创建的连续斩击。
    [Export] private PackedScene _warningScene; // 隐藏阶段目标地面的纯视觉警示。
    [Export] private PackedScene _tornadoScene; // 起手与警示结束时分别创建的旋风。
    private int _effectGeneration; // 显示周期编号，阻止旧警示完成回调污染池复用。
    private float _invulnerabilityLeft; // 旋风起手的剩余无敌秒数。

    /// <summary>
    /// 官方外层追踪高度半幅，像素。
    /// </summary>
    internal float TrackingHeight => _trackingRange.Shape.GetRect().End.Y;

    /// <summary>
    /// 本鲨鱼 AI 请求的垂直游动方向；0 为悬停。
    /// </summary>
    public int VerticalIntent
    {
        get
        {
            return AiData.VerticalIntent;
        }

        internal set
        {
            AiData.VerticalIntent = value;
        }
    }

    /// <summary>
    /// 退避时按原规则直接上游，其余阶段使用本鲨鱼的垂直加速度。
    /// </summary>
    public bool DirectVerticalMovement
    {
        get
        {
            return AiData.DirectVerticalMovement;
        }

        internal set
        {
            AiData.DirectVerticalMovement = value;
        }
    }

    /// <summary>
    /// 当前世界高度，用于保持原版退避时的垂直边界。
    /// </summary>
    internal float Altitude => GlobalPosition.Y;

    /// <inheritdoc />
    protected override EnemyAiPreset AiPreset
    {
        get
        {
            return _preset ??= new(typeof(SharkSpawnEngageState), typeof(SharkSpawnEngageState),
                () => new SharkSpawnEngageState(trackingHeight: TrackingHeight,
                    altitude: () => Altitude, horizontalSpeed: () => Velocity.X));
        }
    }

    /// <inheritdoc />
    protected override EnemyBodyPreset BodyPreset => s_bodyPreset;

    /// <inheritdoc />
    protected override bool Invulnerable => _invulnerabilityLeft > 0f;

    /// <inheritdoc />
    public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
    {
        base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
        if (isNewInstance && (_trackingRange?.Shape is not SegmentShape2D || !_trackingRange.Disabled || _slashScene == null || _warningScene == null || _tornadoScene == null))
        {
            throw new InvalidOperationException("鲨鱼必须绑定斩击、警示和旋风效果。");
        }
    }

    /// <inheritdoc />
    public override void OnShow(object userData)
    {
        _effectGeneration++;
        _invulnerabilityLeft = 0f;
        base.OnShow(userData);
        Body.Visible = true;
    }

    /// <summary>
    /// 仅在自主游动状态接受接触斩击或旋风请求，不要求落地。
    /// </summary>
    /// <param name="index">本鲨鱼的招式下标。</param>
    /// <returns>本次攻击请求是否已经登记。</returns>
    protected override bool TryRequestAttack(int index)
    {
        if (BodyFsm?.CurrentState is not EnemySwimState || !base.TryRequestAttack(index))
        {
            return false;
        }

        VerticalIntent = 0;
        return true;
    }

    /// <inheritdoc />
    public override void OnHide(bool isShutdown, object userData)
    {
        _effectGeneration++;
        base.OnHide(isShutdown, userData);
    }

    /// <inheritdoc />
    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        _invulnerabilityLeft = Math.Max(0f, _invulnerabilityLeft - elapseSeconds);
    }

    /// <inheritdoc />
    protected override void OnHitLanded(AbstractActorEntity target, AttackData data, DamageResult result)
    {
        base.OnHitLanded(target, data, result);
        if (AttackSegment != 0 || BodyFsm.CurrentState is not SharkSpawnAttackState state)
        {
            return;
        }

        state.CompleteContact(GlobalPosition + ((IEnemyAiAgent)this).TargetOffset - new Vector2(100f, 100f));
    }

    /// <summary>
    /// 在正常身体帧创建接触斩击，避免AreaEntered查询期间改变物理形状。
    /// </summary>
    /// <param name="position">接触时已捕获的世界原点。</param>
    internal void SpawnContactSlash(Vector2 position)
    {
        int attackId = this.GetUtility<ConfigIdUtility>().GetAttackId(GameConfig.Entity.EntityId.SharkSpawn, 1, 2);
        AttackConfig slash = this.GetModel<ConfigurationModel>().Tables.TbAttackConfig.Get(attackId);
        SkillEffectNode effect = RentSkillEffect(_slashScene);
        effect.Show(slash, position, Facing, CaptureCombatAttributes(),
            Mathf.RoundToInt(slash.FlatPower.X), false, ReleaseSkillEffect, hitSoundId: HitSoundId);
    }

    /// <summary>
    /// hit1第34/37/40/43/46源帧沿施放朝向突进，帧间不重新创建攻击包。
    /// </summary>
    private void DashAnimEvent()
    {
        if (AttackSegment == 0)
        {
            Velocity = new Vector2(Facing * 480f, Velocity.Y);
        }
    }

    /// <summary>
    /// 旋风施放起手进入官方30/24秒无敌，同时留下初始位置的旋风攻击。
    /// </summary>
    private void BeginTornadoAnimEvent()
    {
        if (AttackSegment != 1)
        {
            return;
        }

        _invulnerabilityLeft = 30f / 24f;
        SpawnTornado(GlobalPosition);
    }

    /// <summary>
    /// 隐藏身体并在目标地面留下警示；本周期仍有效时才创建旋风和恢复身体。
    /// </summary>
    private void SpawnTornadoWarningAnimEvent()
    {
        if (AttackSegment != 1 || BodyFsm.CurrentState is not SharkSpawnAttackState state)
        {
            return;
        }

        int generation = _effectGeneration;
        Vector2 position = new(GlobalPosition.X + ((IEnemyAiAgent)this).TargetOffset.X, 510f);
        Body.Visible = false;
        SetHurtBoxEnabled(false);
        Velocity = Vector2.Zero;
        state.WaitForReturn();
        SkillEffectNode warning = RentSkillEffect(_warningScene);
        warning.Show(null, position, Facing, CaptureCombatAttributes(), 0, false, ReleaseSkillEffect,
            () =>
            {
                if (generation == _effectGeneration && IsAlive && BodyFsm.CurrentState == state)
                {
                    SpawnTornado(position);
                    RestoreSharkBody();
                    state.CompleteReturn();
                }
            });
    }

    /// <summary>
    /// 警示结束或攻击中断时恢复身体和受击盒，不改变实体世界原点。
    /// </summary>
    internal void RestoreSharkBody()
    {
        Body.Visible = true;
        if (IsAlive)
        {
            SetHurtBoxEnabled(true);
        }
    }

    /// <summary>
    /// 创建鲨鱼旋风并使用本招独立命中预算。
    /// </summary>
    /// <param name="position">本次旋风的世界原点。</param>
    private void SpawnTornado(Vector2 position)
    {
        SkillEffectNode effect = RentSkillEffect(_tornadoScene);
        effect.Show(OwnAttacks[1], position, Facing, CaptureCombatAttributes(),
            Mathf.RoundToInt(OwnAttacks[1].FlatPower.X), false, ReleaseSkillEffect,
            hitSoundId: HitSoundId);
    }
}
