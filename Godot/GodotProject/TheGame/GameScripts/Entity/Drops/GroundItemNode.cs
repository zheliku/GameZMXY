using GodotGameFramework;
using GameLogic.Config;
using GameLogic.Architecture;
using System;
using GameConfig.Loot;
using GameLogic.Equipment;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework.NodePool;

namespace GameLogic.Drops;

/// <summary>地面恢复品或装备：落地、接触入账、上移淡出与按秒过期；关卡拥有回收与收益。</summary>
public partial class GroundItemNode : CharacterBody2D, IPoolable, IController
{
    [Export] private Sprite2D m_Icon; // 按本次恢复品或装备定义显示的图标。
    [Export] private AnimationPlayer m_PickupAnimation; // 外置上移淡出动画，按物理游戏秒手动推进。
    [Export] private CollisionShape2D m_Shape; // 场景预设的唯一矩形，落地与接触共用。
    private PickupId m_PickupId; // 恢复品定义，与装备互斥。
    private EquipmentData m_EquipmentData; // 已抽取实际属性的地面装备。
    private Func<AbstractHeroEntity, PickupId, EquipmentData, bool> m_Collect; // 接触者交给关卡校验并结算；满包继续留地。
    private Action<GroundItemNode> m_Finished; // 关卡移除所有权并回池。
    private float m_Age; // 已存在的游戏秒数，暂停时不增加。
    private bool m_Collected; // 已成功入账，只完成淡出，不再检测接触或过期。

    /// <summary>以秒推进落地和过期；只有接触英雄才请求入账，满包不会销毁或重抽。</summary>
    /// <param name="delta">游戏秒数。</param>
    public override void _PhysicsProcess(double delta)
    {
        if (delta <= 0)
            return;
        // 成功领取后只推进表现，暂停停止；交易已经完成，不再访问英雄或物品。
        if (m_Collected)
        {
            m_PickupAnimation.Advance(delta);
            if (!m_PickupAnimation.IsPlaying())
                Finish();
            return;
        }

        var config = this.GetModel<ConfigurationModel>().Tables.TbDropConfig.Data;
        float dt = (float)delta;
        m_Age += dt;
        if (m_Age >= config.GroundLifetime)
        {
            Finish();
            return;
        }

        Velocity += Vector2.Down * config.GroundGravity * dt;
        MoveAndSlide();
    }

    /// <summary>借用当前游戏流程的架构入口。</summary>
    /// <returns>所属流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>取池时等待本次关卡参数。</summary>
    public void OnGet()
    {
        SetPhysicsProcess(false);
        Velocity = Vector2.Zero;
        m_Age = 0;
        m_Collected = false;
        ResetPresentation();
    }

    /// <summary>归还时清除全部借用引用与视觉，防止复用重复入账。</summary>
    public void OnRelease()
    {
        SetPhysicsProcess(false);
        m_PickupId = PickupId.None;
        m_EquipmentData = null;
        m_Collect = null;
        m_Finished = null;
        m_Icon.Texture = null;
        Velocity = Vector2.Zero;
        m_Collected = false;
        ResetPresentation();
    }

    /// <summary>显示已确定的恢复品或装备，并启用自身接触检测。</summary>
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
            throw new ArgumentException("一次地面掉落必须且只能包含一种物品。");
        GlobalPosition = position;
        Velocity = Vector2.Zero;
        m_Age = 0;
        m_Collected = false;
        ResetPresentation();
        m_PickupId = pickupId;
        m_EquipmentData = data;
        var tables = this.GetModel<ConfigurationModel>().Tables;
        string iconPath = data == null
            ? tables.TbPickupConfig.Get(pickupId).IconPath
            : tables.TbEquipmentConfig.Get(data.EquipmentId).GroundIconPath;
        m_Icon.Texture = GF.Resource.LoadAsset<Texture2D>(iconPath)
                         ?? throw new InvalidOperationException("地面物品图标加载失败。");
        m_Icon.Scale = Vector2.One * (pickupId == PickupId.LargePeach ? .9f : .7f);
        // 透明留白不参与贴地定位；用 Offset 保留领取动画对 Position 的上移控制。
        using var image = m_Icon.Texture.GetImage();
        if (image == null || image.IsEmpty() || image.IsCompressed() && image.Decompress() != Error.Ok)
            throw new InvalidOperationException($"地面物品图标无法读取：{iconPath}");
        Rect2I visible = image.GetUsedRect();
        if (!visible.HasArea())
            throw new InvalidOperationException($"地面物品图标完全透明：{iconPath}");
        float groundBottom = (m_Shape.Transform * m_Shape.Shape.GetRect()).End.Y;
        m_Icon.Offset = new Vector2(0,
            m_Icon.Texture.GetHeight() * .5f - visible.End.Y + groundBottom / m_Icon.Scale.Y);
        m_Collect = collect;
        m_Finished = finished;
        SetPhysicsProcess(true);
    }

    /// <summary>关卡传入当前英雄，按预设矩形检查接触；满包时下一物理步仍可重试。</summary>
    /// <param name="hero">当前关卡的存活英雄，只在本次调用借用。</param>
    /// <returns>本次是否完成领取。</returns>
    public bool TryCollect(AbstractHeroEntity hero)
    {
        if (m_Collected || m_Collect == null || !IsInstanceValid(hero) || !hero.IsAlive ||
            !(m_Shape.GlobalTransform * m_Shape.Shape.GetRect()).Intersects(hero.HurtBox.GetGlobalBounds()) ||
            !m_Collect(hero, m_PickupId, m_EquipmentData))
            return false;
        m_Collected = true;
        Velocity = Vector2.Zero;
        m_PickupId = PickupId.None;
        m_EquipmentData = null;
        m_Collect = null;
        m_PickupAnimation.Play("pickup");
        m_PickupAnimation.Advance(0);
        return true;
    }

    /// <summary>恢复图标原点与透明度，清除上一轮领取表现。</summary>
    private void ResetPresentation()
    {
        m_PickupAnimation.Play("RESET");
        m_PickupAnimation.Advance(0);
        m_PickupAnimation.Stop();
    }

    /// <summary>先停处理和回调，再交给关卡回收，保证只回收一次。</summary>
    private void Finish()
    {
        SetPhysicsProcess(false);
        var finished = m_Finished;
        m_Finished = null;
        m_Collect = null;
        finished?.Invoke(this);
    }
}
