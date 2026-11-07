using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Level;
using Godot;

namespace GameLogic.Level;

/// <summary>
/// 管理阶段空间区域与物理门，区分行进时开放通路和开战时封闭左右边界。
/// </summary>
[Tool]
public partial class LevelGatesNode : AbstractLevelMarkersNode
{
    private readonly List<StageRegion> _regions = new(); // 按阶段顺序预计算的空间区域。
    private Dictionary<string, StaticBody2D> _gates; // 初始化校验后的稳定 ID 到物理门索引。
    private string _travelLeftGateId = string.Empty; // 行进期间保留上一场战斗左界，抵达后替换。

    /// <summary>
    /// 阶段门使用绿色标注，与生成点、触发区区分。
    /// </summary>
    protected override Color DefaultColor => new(0.6f, 1f, 0.2f, 0.9f);

    /// <summary>
    /// 只支持 StaticBody2D 作为物理门节点。
    /// </summary>
    /// <param name="node">待检查的直接子节点。</param>
    /// <returns>节点是 StaticBody2D 时返回 true。</returns>
    protected override bool Supports(Node node) => node is StaticBody2D;

    /// <summary>
    /// 校验独立左右门和视野宽度，允许相邻战斗区域重叠。
    /// </summary>
    /// <param name="config">本场景对应的关卡配置。</param>
    /// <param name="sceneLeft">场景配置的关卡左界，单位为世界像素。</param>
    /// <param name="sceneRight">场景配置的关卡右界，单位为世界像素。</param>
    /// <param name="viewWidth">相机单屏宽度，每个区域至少容纳一屏。</param>
    /// <exception cref="InvalidOperationException">门缺失、重复、没有碰撞形状或阶段区域非法时抛出。</exception>
    public void Initialize(LevelConfig config, float sceneLeft, float sceneRight, float viewWidth)
    {
        _gates = BuildIndex<StaticBody2D>();
        _regions.Clear();
        HashSet<string> used = new(StringComparer.Ordinal);
        float previousRight = sceneLeft;
        foreach (LevelStage stage in config.Stages)
        {
            // 每场战斗拥有独立左界；不能用上一场出口推导，原版区域存在重叠。
            string entryGateId = stage.LeftGateId;
            string exitGateId = stage.GateId;
            float left = string.IsNullOrEmpty(entryGateId)
                ? sceneLeft
                : RequireGate(entryGateId, used).GlobalPosition.X;
            float right = string.IsNullOrEmpty(exitGateId)
                ? sceneRight
                : RequireGate(exitGateId, used).GlobalPosition.X;
            if (!float.IsFinite(left) || !float.IsFinite(right) || left < sceneLeft ||
                right > sceneRight || right <= previousRight ||
                right - left < viewWidth && !Mathf.IsEqualApprox(right - left, viewWidth))
            {
                throw new InvalidOperationException($"阶段 {stage.StageOrder} 区域 [{left}, {right}] 必须位于关卡内且至少容纳一屏。");
            }

            _regions.Add(new StageRegion(left, right, entryGateId, exitGateId));
            previousRight = right;
        }

        _travelLeftGateId = string.Empty;
        CloseOnly(string.Empty, string.Empty);
    }

    /// <summary>
    /// 返回指定阶段区域的世界边界。
    /// </summary>
    /// <param name="stageOrder">阶段的 1 起顺序号。</param>
    /// <returns>该阶段区域的左右边界，单位为世界像素。</returns>
    public (float Left, float Right) BoundsOf(int stageOrder)
    {
        StageRegion region = _regions[stageOrder - 1];
        return (region.Left, region.Right);
    }

    /// <summary>
    /// 保留上一场左界并封住新出口；停用经过的旧出口，避免重叠区域留下墙。
    /// </summary>
    /// <param name="stageOrder">阶段的 1 起顺序号。</param>
    public void BeginTravel(int stageOrder)
    {
        StageRegion region = _regions[stageOrder - 1];
        CloseOnly(_travelLeftGateId, region.ExitGateId);
    }

    /// <summary>
    /// 开战时关闭入口；出口保持关闭，角色只能在当前区域内移动。
    /// </summary>
    /// <param name="stageOrder">阶段的 1 起顺序号。</param>
    public void LockRegion(int stageOrder)
    {
        StageRegion region = _regions[stageOrder - 1];
        _travelLeftGateId = region.EntryGateId;
        CloseOnly(region.EntryGateId, region.ExitGateId);
    }

    /// <summary>
    /// 当前阶段清除后开放出口，允许进入下一段通路。
    /// </summary>
    /// <param name="stageOrder">阶段的 1 起顺序号。</param>
    public void ReleaseRegion(int stageOrder) => SetClosed(_regions[stageOrder - 1].ExitGateId, false);

    /// <summary>
    /// 只启用当前需要的左右门，保证重叠区域中不残留其他阶段的阻挡。
    /// </summary>
    /// <param name="leftId">本次保留的左门；空表示场景原始左墙。</param>
    /// <param name="rightId">本次保留的右门；空表示场景原始右墙。</param>
    private void CloseOnly(string leftId, string rightId)
    {
        foreach (string id in _gates.Keys)
        {
            SetClosed(id, id == leftId || id == rightId);
        }
    }

    /// <summary>
    /// 绘制门位置：竖直标记线，两端带圆点。
    /// </summary>
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

    /// <summary>
    /// 确认阶段引用的门已经登记且未被其他阶段复用。
    /// </summary>
    /// <param name="id">阶段配置引用的门稳定 ID。</param>
    /// <param name="used">已分配给其他阶段或边界的门 ID 集合。</param>
    /// <returns>已校验的物理门节点。</returns>
    /// <exception cref="InvalidOperationException">门缺失、重复或没有碰撞形状时抛出。</exception>
    private StaticBody2D RequireGate(string id, HashSet<string> used)
    {
        if (!_gates.TryGetValue(id, out StaticBody2D gate) || !used.Add(id) ||
            gate.GetChildren().OfType<CollisionShape2D>().All(x => x.Shape == null))
        {
            throw new InvalidOperationException($"阶段门缺失、重复或没有碰撞形状：{id}");
        }

        return gate;
    }

    /// <summary>
    /// 在物理帧安全边界修改门的碰撞开关。
    /// </summary>
    /// <param name="id">门的稳定 ID；空值表示关卡自身墙体，没有可开关的节点。</param>
    /// <param name="closed">true 关闭碰撞；false 开放通路。</param>
    private void SetClosed(string id, bool closed)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        foreach (CollisionShape2D shape in _gates[id].GetChildren().OfType<CollisionShape2D>())
        {
            shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, !closed);
        }
    }

    /// <summary>
    /// 一场战斗的独立世界边界及左右门稳定 ID。
    /// </summary>
    /// <param name="Left">战斗区左侧世界边界，单位为像素。</param>
    /// <param name="Right">战斗区右侧世界边界，单位为像素。</param>
    /// <param name="EntryGateId">本战斗区入口门的稳定ID。</param>
    /// <param name="ExitGateId">本战斗区出口门的稳定ID。</param>
    private readonly record struct StageRegion(float Left, float Right, string EntryGateId, string ExitGateId);
}
