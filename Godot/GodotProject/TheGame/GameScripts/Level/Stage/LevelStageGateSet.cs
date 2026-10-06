using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Level;
using Godot;

namespace GameLogic.Level;

/// <summary>管理阶段空间区域与物理门，区分行进时开放通路和开战时封闭左右边界。</summary>
[Tool]
public partial class LevelStageGateSet : LevelMarkerSet
{
    private readonly record struct StageRegion(float Left, float Right, string EntryGateId, string ExitGateId); // 阶段空间区域与左右门稳定 ID。
    private readonly List<StageRegion> m_Regions = new(); // 按阶段顺序预计算的空间区域。
    private Dictionary<string, StaticBody2D> m_Gates; // 初始化校验后的稳定 ID 到物理门索引。

    /// <summary>只支持 StaticBody2D 作为物理门节点。</summary>
    /// <param name="node">待检查的直接子节点。</param>
    /// <returns>节点是 StaticBody2D 时返回 true。</returns>
    protected override bool Supports(Node node) => node is StaticBody2D;

    /// <summary>阶段门使用绿色标注，与生成点、触发区区分。</summary>
    protected override Color DefaultColor => new(0.6f, 1f, 0.2f, 0.9f);

    /// <summary>校验门、递增区域与视野宽度，并预计算每阶段的左右边界。</summary>
    /// <param name="config">本场景对应的关卡配置。</param>
    /// <param name="sceneLeft">场景配置的关卡左界，单位为世界像素。</param>
    /// <param name="sceneRight">场景配置的关卡右界，单位为世界像素。</param>
    /// <param name="viewWidth">相机单屏宽度，每个区域至少容纳一屏。</param>
    /// <exception cref="InvalidOperationException">门缺失、重复、没有碰撞形状或阶段区域非法时抛出。</exception>
    public void Initialize(LevelConfig config, float sceneLeft, float sceneRight, float viewWidth)
    {
        m_Gates = BuildIndex<StaticBody2D>();
        m_Regions.Clear();
        HashSet<string> used = new(StringComparer.Ordinal);
        float left = sceneLeft;
        string entryGateId = string.Empty;
        foreach (LevelStage stage in config.Stages)
        {
            // 门位置是场景空间事实，区域只在初始化时计算一次。
            string exitGateId = stage.GateId;
            float right = string.IsNullOrEmpty(exitGateId)
                ? sceneRight
                : RequireGate(exitGateId, used).GlobalPosition.X;
            if (right > sceneRight || right - left < viewWidth)
            {
                throw new InvalidOperationException($"阶段 {stage.StageOrder} 区域 [{left}, {right}] 必须位于关卡内且至少容纳一屏。");
            }

            m_Regions.Add(new StageRegion(left, right, entryGateId, exitGateId));
            left = right;
            entryGateId = exitGateId;
        }
    }

    /// <summary>返回指定阶段区域的世界边界。</summary>
    /// <param name="stageOrder">阶段的 1 起顺序号。</param>
    /// <returns>该阶段区域的左右边界，单位为世界像素。</returns>
    public (float Left, float Right) BoundsOf(int stageOrder)
    {
        StageRegion region = m_Regions[stageOrder - 1];
        return (region.Left, region.Right);
    }

    /// <summary>开放当前阶段入口并封住出口，让玩家与相机进入该区域。</summary>
    /// <param name="stageOrder">阶段的 1 起顺序号。</param>
    public void BeginTravel(int stageOrder)
    {
        StageRegion region = m_Regions[stageOrder - 1];
        SetClosed(region.EntryGateId, false);
        SetClosed(region.ExitGateId, true);
    }

    /// <summary>开战时关闭入口；出口保持关闭，角色只能在当前区域内移动。</summary>
    /// <param name="stageOrder">阶段的 1 起顺序号。</param>
    public void LockRegion(int stageOrder) => SetClosed(m_Regions[stageOrder - 1].EntryGateId, true);

    /// <summary>当前阶段清除后开放出口，允许进入下一段通路。</summary>
    /// <param name="stageOrder">阶段的 1 起顺序号。</param>
    public void ReleaseRegion(int stageOrder) => SetClosed(m_Regions[stageOrder - 1].ExitGateId, false);

    /// <summary>绘制门位置：竖直标记线，两端带圆点。</summary>
    /// <param name="position">门中心，相对本节点的局部坐标。</param>
    /// <param name="color">标注颜色。</param>
    /// <param name="label">标注文本。</param>
    protected override void DrawMarker(Vector2 position, Color color, string label)
    {
        Vector2 top = position - new Vector2(0f, MarkerSize);
        Vector2 bottom = position + new Vector2(0f, MarkerSize);
        DrawLine(top, bottom, color, 3f, true);
        DrawCircle(top, 4f, color);
        DrawCircle(bottom, 4f, color);
        DrawString(ThemeDB.FallbackFont, top + new Vector2(8f, 14f), label, fontSize: 14, modulate: color);
    }

    /// <summary>确认阶段引用的门已经登记且未被其他阶段复用。</summary>
    /// <param name="id">阶段配置引用的门稳定 ID。</param>
    /// <param name="used">已分配给其他阶段的出口门 ID 集合。</param>
    /// <returns>已校验的物理门节点。</returns>
    /// <exception cref="InvalidOperationException">门缺失、重复或没有碰撞形状时抛出。</exception>
    private StaticBody2D RequireGate(string id, HashSet<string> used)
    {
        if (!m_Gates.TryGetValue(id, out StaticBody2D gate) || !used.Add(id) ||
            gate.GetChildren().OfType<CollisionShape2D>().All(x => x.Shape == null))
        {
            throw new InvalidOperationException($"阶段门缺失、重复或没有碰撞形状：{id}");
        }

        return gate;
    }

    /// <summary>在物理帧安全边界修改门的碰撞开关。</summary>
    /// <param name="id">门的稳定 ID；空值表示关卡自身墙体，没有可开关的节点。</param>
    /// <param name="closed">true 关闭碰撞；false 开放通路。</param>
    private void SetClosed(string id, bool closed)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        foreach (CollisionShape2D shape in m_Gates[id].GetChildren().OfType<CollisionShape2D>())
        {
            shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, !closed);
        }
    }
}
