using System;
using GameConfig.Battle;
using GameConfig.Loot;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Equipment;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework;
using GodotGameFramework.NodePool;

namespace GameLogic.Drops;

/// <summary>
/// 地面恢复品或装备：落地、接触入账、上移淡出与按秒过期；关卡拥有回收与收益。
/// </summary>
public partial class GroundItemNode : CharacterBody2D, IPoolable, IController
{
    private static readonly StringName s_pickupAnimationName = "pickup"; // 领取成功后的上移淡出轨道。
    private static readonly StringName s_resetAnimation = "RESET"; // 池复用时恢复表现的原生复位轨道。
    [Export] private Sprite2D _icon; // 按本次恢复品或装备定义显示的图标。
    [Export] private AnimationPlayer _pickupAnimation; // 外置上移淡出动画，按物理游戏秒手动推进。
    [Export] private CollisionShape2D _shape; // 场景预设的唯一矩形，落地与接触共用。
    [Export] private ItemAppearancesResource _appearances; // 背包与地面掉落共用的外观资源。
    [Export] private float _gravity = 980f; // 地面物品落下的表现重力，像素/秒²。
    private PickupId _pickupId; // 恢复品定义，与装备互斥。
    private EquipmentData _equipmentData; // 已抽取实际属性的地面装备。
    private Func<AbstractHeroEntity, PickupId, EquipmentData, bool> _collect; // 接触者交给关卡校验并结算；满包继续留地。
    private Action<GroundItemNode> _finished; // 关卡移除所有权并回池。
    private float _age; // 已存在的游戏秒数，暂停时不增加。
    private bool _collected; // 已成功入账，只完成淡出，不再检测接触或过期。

    /// <summary>
    /// 以秒推进落地和过期；只有接触英雄才请求入账，满包不会销毁或重抽。
    /// </summary>
    /// <param name="delta">游戏秒数。</param>
    public override void _PhysicsProcess(double delta)
    {
        if (delta <= 0)
        {
            return;
        }
        // 成功领取后只推进表现，暂停停止；交易已经完成，不再访问英雄或物品。
        if (_collected)
        {
            _pickupAnimation.Advance(delta);
            if (!_pickupAnimation.IsPlaying())
            {
                Finish();
            }

            return;
        }

        BattleConfig config = this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.Data;
        float dt = (float)delta;
        _age += dt;
        if (_age >= config.GroundLifetime)
        {
            Finish();
            return;
        }

        Velocity += Vector2.Down * _gravity * dt;
        MoveAndSlide();
    }

    /// <summary>
    /// 借用当前游戏流程的架构入口。
    /// </summary>
    /// <returns>所属流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>
    /// 取池时等待本次关卡参数。
    /// </summary>
    public void OnGet()
    {
        SetPhysicsProcess(false);
        Velocity = Vector2.Zero;
        _age = 0;
        _collected = false;
        ResetPresentation();
    }

    /// <summary>
    /// 归还时清除全部借用引用与视觉，防止复用重复入账。
    /// </summary>
    public void OnRelease()
    {
        SetPhysicsProcess(false);
        _pickupId = PickupId.None;
        _equipmentData = null;
        _collect = null;
        _finished = null;
        _icon.Texture = null;
        Velocity = Vector2.Zero;
        _collected = false;
        ResetPresentation();
    }

    /// <summary>
    /// 显示已确定的恢复品或装备，并启用自身接触检测。
    /// </summary>
    /// <param name="position">死亡位置附近。</param>
    /// <param name="pickupId">恢复品主键，装备使用 None。</param>
    /// <param name="data">实际装备，恢复品时为空。</param>
    /// <param name="collect">校验实际接触者并结算；满包时拒绝领取。</param>
    /// <param name="finished">过期或领取动画结束后的回收回调。</param>
    /// <exception cref="ArgumentException">恢复品和装备未按互斥规则提供。</exception>
    /// <exception cref="InvalidOperationException">图标不存在、无法读取或完全透明。</exception>
    public void Show(Vector2 position, PickupId pickupId, EquipmentData data,
        Func<AbstractHeroEntity, PickupId, EquipmentData, bool> collect, Action<GroundItemNode> finished)
    {
        if ((pickupId == PickupId.None) == (data == null))
        {
            throw new ArgumentException("一次地面掉落必须且只能包含一种物品。");
        }

        GlobalPosition = position;
        Velocity = Vector2.Zero;
        _age = 0;
        _collected = false;
        ResetPresentation();
        _pickupId = pickupId;
        _equipmentData = data;
        ItemAppearanceResource appearance = data == null ? _appearances.GetAppearance(pickupId) : _appearances.GetAppearance(data.EquipmentId);
        _icon.Texture = appearance.GroundIcon;
        _icon.Scale = appearance.GroundScale;
        string iconPath = _icon.Texture.ResourcePath;
        // 透明留白不参与贴地定位；用 Offset 保留领取动画对 Position 的上移控制。
        using Image image = _icon.Texture.GetImage();
        if (image == null || image.IsEmpty() || image.IsCompressed() && image.Decompress() != Error.Ok)
        {
            throw new InvalidOperationException($"地面物品图标无法读取：{iconPath}");
        }

        Rect2I visible = image.GetUsedRect();
        if (!visible.HasArea())
        {
            throw new InvalidOperationException($"地面物品图标完全透明：{iconPath}");
        }

        float groundBottom = (_shape.Transform * _shape.Shape.GetRect()).End.Y;
        _icon.Offset = new Vector2(0,
            _icon.Texture.GetHeight() * .5f - visible.End.Y + groundBottom / _icon.Scale.Y);
        _collect = collect;
        _finished = finished;
        SetPhysicsProcess(true);
    }

    /// <summary>
    /// 关卡传入当前英雄，按预设矩形检查接触；满包时下一物理步仍可重试。
    /// </summary>
    /// <param name="hero">当前关卡的存活英雄，只在本次调用借用。</param>
    /// <returns>本次是否完成领取。</returns>
    public bool TryCollect(AbstractHeroEntity hero)
    {
        if (_collected || _collect == null || !IsInstanceValid(hero) || !hero.IsAlive ||
            !(_shape.GlobalTransform * _shape.Shape.GetRect()).Intersects(hero.HurtBox.GetGlobalBounds()) ||
            !_collect(hero, _pickupId, _equipmentData))
        {
            return false;
        }

        _collected = true;
        Velocity = Vector2.Zero;
        _pickupId = PickupId.None;
        _equipmentData = null;
        _collect = null;
        _pickupAnimation.Play(s_pickupAnimationName);
        _pickupAnimation.Advance(0);
        return true;
    }

    /// <summary>
    /// 恢复图标原点与透明度，清除上一轮领取表现。
    /// </summary>
    private void ResetPresentation()
    {
        _pickupAnimation.Play(s_resetAnimation);
        _pickupAnimation.Advance(0);
        _pickupAnimation.Stop();
    }

    /// <summary>
    /// 先停处理和回调，再交给关卡回收，保证只回收一次。
    /// </summary>
    private void Finish()
    {
        SetPhysicsProcess(false);
        Action<GroundItemNode> finished = _finished;
        _finished = null;
        _collect = null;
        finished?.Invoke(this);
    }
}
