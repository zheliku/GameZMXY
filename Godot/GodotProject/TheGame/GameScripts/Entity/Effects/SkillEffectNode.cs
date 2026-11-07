using System;
using System.Linq;
using GameConfig.Battle;
using GameConfig.Sound;
using GameFramework;
using GameLogic.Architecture;
using GameLogic.Audio;
using GameLogic.Battle;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using GameLogic.Entity;
using Godot;
using GodotGameFramework;
using GodotGameFramework.NodePool;

namespace GameLogic.Effects;

/// <summary>
/// 独立池化技能效果：原生动画拥有表现及方法键，节点拥有攻击包和命中预算。
/// </summary>
public partial class SkillEffectNode : Node2D, IPoolable, IController
{
    private const string DefaultAnimation = "play"; // 独立特效场景的普通动画约定。
    private const string MusouAnimation = "musou"; // 可选无双动画变体。

    [Export] private AnimationPlayer _player; // 原生动画是唯一的局部播放时钟。
    [Export] private Area2D _hitBox; // 本效果独立判定区。
    private CollisionShape2D[] _shapes; // 同一判定区的全部基础形状，几何及启闭均由动画驱动。
    private AttackConfig _hit; // 只借用统一数值表；纯视觉效果为空。
    private CombatantAttributes _attacker; // 效果生成时冻结的攻击者属性和阵营。
    private SoundId _hitSoundId; // 施放者提供的成功打击声音，不由受击怪物指定。
    private AttackData _attackData; // 节点拥有并按段更新的攻击包。
    private Action _completed; // 本次效果完成通知。
    private Action<SkillEffectNode> _finished; // 所属实体负责归还节点池。
    private Vector2 _sourceScale; // 场景原始镜像，不从表复制表现参数。
    private int _direction, _remainingHits, _power; // 出招朝向、总预算、冻结威力。
    private float? _knockbackOriginX; // 具体招式提供的击退中心。
    private double _repeatAt; // 依原生动画位置计算的下一次重复判定时刻。
    private bool _active, _finishRequested; // 防止方法轨道中回池或重复完成。

    /// <summary>
    /// 由取池的实体绑定伤害来源，具体施放者可在Show时替换；不保存节点引用。
    /// </summary>
    internal AttackSource Source { get; set; }

    /// <summary>
    /// 当前效果仍由所属实体持有；池回收后派生效果不得继续访问自身状态。
    /// </summary>
    protected bool IsActive => _active;

    /// <summary>
    /// 使用场景导出绑定，声音与结束回调只连接一次。
    /// </summary>
    public override void _Ready()
    {
        // Area 是判定区的所有者；多个形状仍共享一个攻击包、去重集合和命中预算。
        _shapes = _hitBox.GetChildren().OfType<CollisionShape2D>().ToArray();
        if (_shapes.Length == 0)
        {
            throw new InvalidOperationException($"技能效果的判定区缺少基础形状：{SceneFilePath}");
        }
        _sourceScale = Scale;
        _player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        _player.CallbackModeMethod = AnimationMixer.AnimationCallbackModeMethod.Immediate;
        _player.AnimationFinished += OnAnimationFinished;
        _hitBox.AreaEntered += OnAreaEntered;
        SetPhysicsProcess(false);
    }

    /// <summary>
    /// 推进原生动画，结束后再回池；暂停不推进。
    /// </summary>
    /// <param name="delta">游戏秒数。</param>
    public override void _PhysicsProcess(double delta)
    {
        if (!_active || delta <= 0)
        {
            return;
        }

        if (_hit != null && _remainingHits <= 0)
        {
            Finish();
            return;
        }
        _player.Advance(delta);
        if (_finishRequested)
        {
            Finish();
            return;
        }
        if (_hit != null && _player.CurrentAnimationPosition >= _repeatAt)
        {
            RefreshAttack();
        }

        if (_shapes.Any(shape => !shape.Disabled) && _remainingHits > 0)
        {
            foreach (Area2D area in _hitBox.GetOverlappingAreas())
            {
                OnAreaEntered(area);
            }
        }
    }

    /// <summary>
    /// 借用当前 GGF 流程拥有的架构。
    /// </summary>
    /// <returns>当前架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>
    /// 取池后等待本次参数。
    /// </summary>
    public virtual void OnGet() => SetPhysicsProcess(false);

    /// <summary>
    /// 回收攻击包并清除本次引用，复用不串技能或回调。
    /// </summary>
    public void OnRelease()
    {
        SetPhysicsProcess(false);
        if (_attackData != null)
        {
            ReferencePool.Release(_attackData);
        }

        _attackData = null;
        _hit = null;
        _finished = null;
        _completed = null;
        _knockbackOriginX = null;
        _hitSoundId = SoundId.None;
        _active = _finishRequested = false;
        _player.Stop();
        foreach (CollisionShape2D shape in _shapes)
        {
            shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        }
        Visible = false;
    }

    /// <summary>
    /// 显示效果，位置固定；大小、偏移与正反朝向留在场景和动画。
    /// </summary>
    /// <param name="hit">可选伤害数值。</param>
    /// <param name="position">世界位置。</param>
    /// <param name="direction">出招朝向。</param>
    /// <param name="attacker">攻击者快照。</param>
    /// <param name="power">按具体招式取整并应用无双后的威力。</param>
    /// <param name="musou">本次是否使用无双视觉变体。</param>
    /// <param name="finished">回收通知。</param>
    /// <param name="completed">效果完成通知。</param>
    /// <param name="knockbackOriginX">具体招式指定的击退中心。</param>
    /// <param name="hitSoundId">本次效果成功命中的声音；与目标自身的受击语音分别播放。</param>
    /// <param name="visualScaleMultiplier">本次官方视觉缩放；不写回场景默认值。</param>
    /// <param name="source">实际施放者不同于取池实体时，提供该次施放的来源快照。</param>
    public void Show(AttackConfig hit, Vector2 position, int direction,
        CombatantAttributes attacker, int power, bool musou, Action<SkillEffectNode> finished,
        Action completed = null, float? knockbackOriginX = null, SoundId hitSoundId = SoundId.None,
        Vector2? visualScaleMultiplier = null, AttackSource? source = null)
    {
        _hit = hit;
        Source = (source ?? Source) with
        {
            EmitterInstanceId = GetInstanceId()
        };
        _attacker = attacker;
        _direction = direction;
        _power = power;
        _finished = finished;
        _completed = completed;
        _knockbackOriginX = knockbackOriginX;
        _hitSoundId = hitSoundId;
        _remainingHits = hit?.HitBudget ?? 0;
        _repeatAt = double.PositiveInfinity;
        _finishRequested = false;
        _active = true;
        Vector2 visualScale = visualScaleMultiplier ?? Vector2.One;
        Scale = new Vector2(_sourceScale.X * visualScale.X * (direction > 0 ? -1 : 1),
            _sourceScale.Y * visualScale.Y);
        GlobalPosition = position;
        if (hit != null)
        {
            RefreshAttack();
        }

        _player.Stop();
        _player.Play(musou && _player.HasAnimation(MusouAnimation) ? MusouAnimation : DefaultAnimation);
        _player.Advance(0);
        // 同步排队本次动画的启闭值，防止同帧复用被上次归还的延迟禁用覆盖。
        foreach (CollisionShape2D shape in _shapes)
        {
            shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, shape.Disabled);
        }
        Visible = true;
        SetPhysicsProcess(true);
    }

    /// <summary>
    /// 创建新攻击包，不重置整段效果的预算。
    /// </summary>
    private void RefreshAttack()
    {
        if (_attackData != null)
        {
            ReferencePool.Release(_attackData);
        }

        _attackData = _hit == null ? null : AttackData.Create(_hit.Id, _attacker, _power,
                    _hit.DamageKind, _hit.Knockback, _direction, _hit.HitProtect, _knockbackOriginX,
                    isEffect: true, source: Source);
        _repeatAt = double.PositiveInfinity;
    }

    /// <summary>
    /// 由官方效果动画的刷新方法键重新装填同一效果的攻击包。
    /// </summary>
    public void RefreshAttackAnimEvent()
    {
        if (_active && _hit != null)
        {
            RefreshAttack();
        }
    }

    /// <summary>
    /// 原生动画的新攻击段方法键重新装填攻击包。
    /// </summary>
    private void NewAttackAnimEvent() => RefreshAttack();

    /// <summary>
    /// 原生声音键仍经过 GGF 的统一音频管理。
    /// </summary>
    /// <param name="id">已登记的声音编号。</param>
    private void PlaySoundAnimEvent(SoundId id) => GF.Sound.PlayConfigured(id);

    /// <summary>
    /// 请求结束，待当前物理帧的动画推进返回后回池。
    /// </summary>
    protected void RequestFinish() => _finishRequested = true;

    /// <summary>
    /// 由原生动画结束方法键请求回池。
    /// </summary>
    private void FinishAnimationAnimEvent() => RequestFinish();

    /// <summary>
    /// 取消本效果的伤害并释放攻击包，保留节点供具体效果完成消散表现。
    /// </summary>
    protected void StopAttack()
    {
        if (_attackData != null)
        {
            ReferencePool.Release(_attackData);
        }

        _attackData = null;
        _hit = null;
        foreach (CollisionShape2D shape in _shapes)
        {
            shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        }
    }

    /// <summary>
    /// 纯视觉效果在真实动画结束时请求回池。
    /// </summary>
    /// <param name="animation">刚结束的动画。</param>
    private void OnAnimationFinished(StringName animation) => RequestFinish();

    /// <summary>
    /// 同一攻击包对同一目标去重，成功命中才消耗总预算。
    /// </summary>
    /// <param name="area">进入或持续重叠的区域。</param>
    private void OnAreaEntered(Area2D area)
    {
        if (!_active || _attackData == null || _remainingHits <= 0 || !_shapes.Any(shape => !shape.Disabled) ||
            area is not HurtBoxNode { OwnerEntity: { IsAlive: true } target } hurtBox || target.Side == _attacker.Side ||
            !hurtBox.Intersects(_shapes))
        {
            return;
        }

        ApplyHit(target, true);
    }

    /// <summary>
    /// 对具体弹体已裁定的范围目标应用同一攻击，不递归扩散或消耗主命中预算。
    /// </summary>
    /// <param name="target">位于具体弹体溅射范围内的目标。</param>
    protected void ApplySplashHit(AbstractActorEntity target) => ApplyHit(target, false);

    /// <summary>
    /// 共享直接命中与范围溅射的结算、去重、音效和无双收益。
    /// </summary>
    /// <param name="target">本次受击目标。</param>
    /// <param name="primary">是否消耗弹体预算并触发具体效果的命中扩展。</param>
    private void ApplyHit(AbstractActorEntity target, bool primary)
    {
        if (_attackData == null || !target.IsAlive || target.Side == _attacker.Side ||
            !_attackData.TryRegisterHit(target.GetInstanceId()))
        {
            return;
        }

        DamageResult result = target.ReceiveHit(_attackData);
        if (result.IsMiss)
        {
            return;
        }

        GF.Sound.PlayConfigured(_hitSoundId);
        if (primary)
        {
            OnHitLanded(target, result);
            _remainingHits--;
            if (_hit.RepeatIntervalSeconds > 0)
            {
                _repeatAt = Math.Min(_repeatAt, _player.CurrentAnimationPosition + _hit.RepeatIntervalSeconds);
            }
        }

        if (_attacker.Side == CombatSide.Hero)
        {
            this.GetSystem<HeroSystem>().RecordHit(result);
        }
    }

    /// <summary>
    /// 成功命中后的效果扩展点，具体效果可施加状态；不得保存池化攻击包。
    /// </summary>
    /// <param name="target">本次真实命中的目标。</param>
    /// <param name="result">已经结算的结果。</param>
    protected virtual void OnHitLanded(AbstractActorEntity target, DamageResult result)
    {
    }

    /// <summary>
    /// 效果完成后通知所属状态并回池；是否仍能改变状态由接收方判断。
    /// </summary>
    private void Finish()
    {
        _active = false;
        _completed?.Invoke();
        _finished(this);
    }
}
