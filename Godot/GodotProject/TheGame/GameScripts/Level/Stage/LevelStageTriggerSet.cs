using System;
using System.Collections.Generic;
using GameConfig.Level;
using Godot;

namespace GameLogic.Level;

/// <summary>为特殊阶段提供可选区域激活，只监听当前会话明确启用的一个触发区。</summary>
[Tool]
public partial class LevelStageTriggerSet : LevelMarkerSet
{
    private Dictionary<string, Area2D> m_Triggers; // 初始化校验后的稳定 ID 到触发区索引。
    private Area2D m_Watched; // 当前阶段唯一启用的触发区。
    private Node2D m_Player; // 本会话的玩家，用于过滤其他物理实体。

    /// <summary>本会话玩家进入当前触发区，只发送一次。</summary>
    public event Action Activated;

    /// <summary>只支持 Area2D 作为触发区节点。</summary>
    /// <param name="node">待检查的直接子节点。</param>
    /// <returns>节点是 Area2D 时返回 true。</returns>
    protected override bool Supports(Node node) => node is Area2D;

    /// <summary>触发区使用品红色标注，与生成点、门区分。</summary>
    protected override Color DefaultColor => new(0.95f, 0.2f, 0.85f, 0.9f);

    /// <summary>校验特殊阶段使用的外键，并停用所有触发区；加载期间不连接玩法信号。</summary>
    /// <param name="config">本场景对应的关卡配置。</param>
    /// <exception cref="InvalidOperationException">阶段的触发区缺失或重复时抛出。</exception>
    public void Initialize(LevelConfig config)
    {
        m_Triggers = BuildIndex<Area2D>();
        HashSet<string> used = new(StringComparer.Ordinal);
        foreach (LevelStage stage in config.Stages)
        {
            if (stage.Activation == StageActivation.Trigger &&
                (!m_Triggers.ContainsKey(stage.TriggerId) || !used.Add(stage.TriggerId)))
            {
                throw new InvalidOperationException($"阶段触发区缺失或重复：{stage.TriggerId}");
            }
        }

        // 加载期不监听玩法信号，显式启用后才允许物理重叠激活。
        foreach (Area2D area in m_Triggers.Values)
        {
            area.SetDeferred(Area2D.PropertyName.Monitoring, false);
        }
    }

    /// <summary>在会话开始后监听一个特殊阶段的触发区，并先解除上一个监听。</summary>
    /// <param name="id">特殊阶段配置引用的触发区稳定 ID。</param>
    /// <param name="player">本会话玩家，只有它进入才会激活阶段。</param>
    public void BeginWatching(string id, Node2D player)
    {
        EndWatching();
        m_Player = player;
        m_Watched = m_Triggers[id];
        m_Watched.BodyEntered += OnBodyEntered;
        m_Watched.SetDeferred(Area2D.PropertyName.Monitoring, true);
    }

    /// <summary>解除信号并停用当前触发区；没有监听时直接返回。</summary>
    public void EndWatching()
    {
        if (m_Watched == null)
        {
            return;
        }

        m_Watched.BodyEntered -= OnBodyEntered;
        m_Watched.SetDeferred(Area2D.PropertyName.Monitoring, false);
        m_Watched = null;
        m_Player = null;
    }

    /// <summary>只允许注入的玩家激活当前阶段，先解除监听再分发事件。</summary>
    /// <param name="body">进入触发区的物理实体。</param>
    private void OnBodyEntered(Node2D body)
    {
        if (body == m_Player)
        {
            Activated?.Invoke();
            EndWatching();
        }
    }
}
