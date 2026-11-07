using System;
using GameLogic.Entity;
using Godot;
using GodotGameFramework.NodePool;

namespace GameLogic.Enemies;

/// <summary>
/// 最终形态火柱只拥有原生播放与判定窗口，实际命中沿用首领当前共享攻击编号。
/// </summary>
public partial class BullDemonFireNode : Node2D, IPoolable
{
    [Export] private AnimationPlayer _player; // 40帧素材在原版第36帧销毁。
    [Export] private Area2D _hitBox; // 原生动画控制启闭和阶段矩形。
    [Export] private CollisionShape2D _shape; // 第25～36源帧命中，末帧判定后回池。
    private Action<AbstractActorEntity> _hit; // 本次首领共享命中入口，不创建独立攻击包。
    private Action<BullDemonFireNode> _finished; // 所有者归还本次节点。
    private bool _completed; // 方法轨道中只登记结束，正常物理帧再回池。

    /// <summary>
    /// 播放器用物理帧手动推进，避免方法轨道中释放节点。
    /// </summary>
    public override void _Ready()
    {
        _player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        _player.CallbackModeMethod = AnimationMixer.AnimationCallbackModeMethod.Immediate;
        SetPhysicsProcess(false);
    }

    /// <inheritdoc />
    public void OnGet() => SetPhysicsProcess(false);

    /// <inheritdoc />
    public void OnRelease()
    {
        SetPhysicsProcess(false);
        _player.Stop();
        _hit = null;
        _finished = null;
        _shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        Visible = false;
    }

    /// <summary>
    /// 按官方世界位置显示一根火柱，所有权和命中去重仍归首领。
    /// </summary>
    /// <param name="position">世界出生位置。</param>
    /// <param name="hit">本次共享命中入口。</param>
    /// <param name="finished">归还节点的完成回调。</param>
    public void Show(Vector2 position, Action<AbstractActorEntity> hit, Action<BullDemonFireNode> finished)
    {
        GlobalPosition = position;
        _hit = hit;
        _finished = finished;
        _completed = false;
        _player.Stop();
        _player.Play("play");
        _player.Advance(0);
        Visible = true;
        SetPhysicsProcess(true);
    }

    /// <summary>
    /// 原生时间轴推进后读取有效重叠，不按整张火焰图集的画布命中。
    /// </summary>
    /// <param name="delta">本次游戏秒数。</param>
    public override void _PhysicsProcess(double delta)
    {
        if (_hit == null || delta <= 0)
        {
            return;
        }

        _player.Advance(delta);
        if (!_shape.Disabled)
        {
            foreach (Area2D area in _hitBox.GetOverlappingAreas())
            {
                if (area is HurtBoxNode { OwnerEntity: { IsAlive: true } target })
                {
                    _hit(target);
                }
            }
        }
        // 原版第36帧先checkAttack再destroy，不能先回池漏掉末帧。
        if (_completed)
        {
            _finished(this);
        }
    }

    /// <summary>
    /// 原生第36源帧请求销毁，不在播放器遍历期间回池。
    /// </summary>
    private void FinishAnimationAnimEvent() => _completed = true;
}
