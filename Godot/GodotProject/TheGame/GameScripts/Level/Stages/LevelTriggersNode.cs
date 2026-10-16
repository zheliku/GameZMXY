using System;
using System.Collections.Generic;
using GameConfig.Level;
using GameLogic.Heroes;
using Godot;

namespace GameLogic.Level;

/// <summary>为特殊阶段提供可选区域激活，只监听当前会话明确启用的一个触发区。</summary>
[Tool]
public partial class LevelTriggersNode : AbstractLevelMarkersNode
{
    private Dictionary<string, Area2D> m_Triggers; // 初始化校验后的稳定 ID 到触发区索引。
    private Area2D m_Watched; // 当前阶段唯一启用的触发区。

    /// <summary>报告进入当前区域的存活英雄；关卡接受后调用 EndWatching 结束监听。</summary>
    public event Action<AbstractHeroEntity> Activated;

    /// <summary>触发区使用品红色标注，与生成点、门区分。</summary>
    protected override Color DefaultColor => new(0.95f, 0.2f, 0.85f, 0.9f);

    /// <summary>只支持 Area2D 作为触发区节点。</summary>
    /// <param name="node">待检查的直接子节点。</param>
    /// <returns>节点是 Area2D 时返回 true。</returns>
    protected override bool Supports(Node node) => node is Area2D;

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
    public void BeginWatching(string id)
    {
        EndWatching();
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
    }

    /// <summary>从物理接触识别存活英雄，不保存接触者引用。</summary>
    /// <param name="body">进入触发区的物理实体。</param>
    private void OnBodyEntered(Node2D body)
    {
        if (body is AbstractHeroEntity { IsAlive: true } hero)
            Activated?.Invoke(hero);
    }
}
