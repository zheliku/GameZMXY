using System;
using System.Linq;
using GameConfig.Battle;
using GameFramework;
using GameLogic.Architecture;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using GameLogic.Config;
using GameLogic.Entity;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework;

namespace GameLogic.Level.Traps;

/// <summary>
/// 洞窟尖刺：向下射线检测英雄触发预警，动画决定下落；不持有玩家或关卡控制器。
/// </summary>
public partial class BatCaveSpikeNode : Area2D, IController
{
    private const float FallAcceleration = 576f; // 原版每源帧竖速增加1，换算为px/s²。
    private const float SourceHalfStep = 1f / 48f; // 原版先加速后位移的积分校正，不建立24Hz循环。
    private const string SpikeAnimation = "spike"; // 完整20格原版动画。

    [Export] private AnimatedSprite2D _sprite; // 预警动画，第20源帧开始下落。
    [Export] private CollisionShape2D _shape; // 覆盖主要刺身的基础矩形。
    [Export] private RayCast2D _triggerRay; // 仅检测下方英雄受击区；长度、起点与物理层在场景编辑。
    [Export] private bool _suspended; // 原版isUp，悬顶刺经过时落下，地面刺保持原位。
    [Export] private int _attackId = 900004001; // 蝙蝠窟第1个机关的稳定攻击表引用，编辑器独立预览也使用此值。

    private Vector2 _origin; // 本关原点，复位不累积误差。
    private SpikePhase _phase; // 就绪、预警、下落互斥状态。
    private double _elapsed; // 当前编号的秒数，下落时重新计时。
    private bool _hit; // 当前编号已经命中，无敌拒绝不消费。

    /// <summary>
    /// 读取已经过启动校验的共享攻击定义。
    /// </summary>
    private AttackConfig Attack => this.GetModel<ConfigurationModel>().Tables.TbAttackConfig.Get(_attackId);

    /// <summary>
    /// 使用当前主流程拥有的架构。
    /// </summary>
    /// <returns>GGF主流程架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture()
    {
        return GF.Fsm.GetArchitecture();
    }

    /// <summary>
    /// 验证绑定并停用加载期玩法。
    /// </summary>
    public override void _Ready()
    {
        if (_sprite?.SpriteFrames == null || _shape?.Shape == null || _triggerRay == null)
        {
            throw new InvalidOperationException("尖刺必须绑定原版动画、伤害形状和向下检测射线。");
        }

        _origin = Position;
        _triggerRay.Enabled = _suspended;
        _sprite.FrameChanged += OnFrameChanged;
        ResetCycle();
    }

    /// <summary>
    /// 按游戏秒数推进预警、下落和重复命中，暂停自然停止。
    /// </summary>
    /// <param name="delta">当前物理步长秒数。</param>
    public override void _PhysicsProcess(double delta)
    {
        if (delta <= 0)
        {
            return;
        }

        _elapsed += delta;
        // 没有接触者时保持就绪，不索取游戏架构；场景可独立预览。
        bool triggered = _suspended
            ? _triggerRay.GetCollider() is HurtBoxNode { OwnerEntity: AbstractHeroEntity { IsAlive: true } }
            : GetOverlappingAreas().OfType<HurtBoxNode>()
                .Any(hurt => hurt.OwnerEntity is AbstractHeroEntity { IsAlive: true });
        if (_phase == SpikePhase.Ready && !triggered)
        {
            return;
        }

        LevelModel level = this.GetModel<LevelModel>();
        if (!level.SessionActive || level.Outcome.Value != LevelOutcome.Running)
        {
            ResetCycle();
            return;
        }
        if (Attack.DamageKind != DamageKind.Real || Attack.FlatPower.X != Attack.FlatPower.Y ||
            Attack.PowerScale != Vector2.Zero || Attack.RepeatIntervalSeconds <= 0)
        {
            throw new InvalidOperationException("尖刺攻击必须为固定真实伤害，并配置重复秒数。");
        }

        if (!_suspended && _elapsed >= Attack.RepeatIntervalSeconds)
        {
            _elapsed %= Attack.RepeatIntervalSeconds;
            _hit = false;
        }

        // 射线和物理层决定何时预警，刺身Area负责实际伤害，不在代码中另算横向范围。
        if (_suspended && _phase == SpikePhase.Ready && triggered)
        {
            _phase = SpikePhase.Warning;
            _sprite.Play(SpikeAnimation);
            _sprite.Frame = 1;
        }

        if (_phase == SpikePhase.Falling)
        {
            // y=g*t*(t+h)/2 重现源先加速后位移，不依赖当前物理帧率。
            float elapsed = (float)_elapsed;
            Position = _origin + Vector2.Down * (FallAcceleration * elapsed * (elapsed * 0.5f + SourceHalfStep));
            Visible = Position.Y < 500f;
            if (Position.Y >= 1000f)
            {
                ResetCycle();
            }
        }

        if (!_hit && Visible)
        {
            CheckHit();
        }
    }

    /// <summary>
    /// 第20源帧开始下落，不能在预警起点移动。
    /// </summary>
    private void OnFrameChanged()
    {
        if (_phase != SpikePhase.Warning || _sprite.Frame != 19)
        {
            return;
        }

        _sprite.Stop();
        _phase = SpikePhase.Falling;
        _elapsed = 0;
    }

    /// <summary>
    /// 英雄真实受击盒结算固定伤害，无敌时保留本编号资格。
    /// </summary>
    private void CheckHit()
    {
        foreach (Area2D area in GetOverlappingAreas())
        {
            if (area is not HurtBoxNode { OwnerEntity: AbstractHeroEntity { IsAlive: true } hero })
            {
                continue;
            }
            // 同级、必中且无暴击；环境固定伤害不参与等级缩放，仍走正式扣血和死亡入口。
            var attacker = new CombatantAttributes(CombatSide.Enemy, hero.Level, 0, 0, 0,
                0, 0, 0, 0, 1, 0, 0, 0);
            var data = AttackData.Create(Attack.Id, attacker, Attack.FlatPower.X, Attack.DamageKind,
                Attack.Knockback, 1, Attack.HitProtect, GlobalPosition.X,
                source: new AttackSource(0, GameConfig.Entity.EntityId.None, GetInstanceId(), GetInstanceId()));
            try
            {
                _hit = !hero.ReceiveHit(data).IsMiss;
            }
            finally
            {
                ReferencePool.Release(data);
            }
            return;
        }
    }

    /// <summary>
    /// 落刺回原点开始新编号，地面刺以表内间隔更新。
    /// </summary>
    private void ResetCycle()
    {
        Position = _origin;
        Visible = true;
        _phase = SpikePhase.Ready;
        _elapsed = 0;
        _hit = false;
        _sprite.Stop();
        _sprite.Frame = 0;
    }

    /// <summary>
    /// 悬顶刺的互斥动作阶段。
    /// </summary>
    private enum SpikePhase
    {
        Ready, Warning, Falling
    }
}
