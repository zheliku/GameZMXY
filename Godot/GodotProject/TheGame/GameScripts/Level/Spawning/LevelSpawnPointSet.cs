using System;
using System.Collections.Generic;
using GameConfig.Level;
using Godot;

namespace GameLogic.Level;

/// <summary>按稳定 ID 索引生成点，并在初始化时解析玩家和配方使用的空间外键。</summary>
[Tool]
public partial class LevelSpawnPointSet : LevelMarkerSet
{
    private Dictionary<string, Marker2D> m_Points; // 初始化校验后的稳定 ID 到生成点索引。

    /// <summary>只支持 Marker2D 作为生成点节点。</summary>
    /// <param name="node">待检查的直接子节点。</param>
    /// <returns>节点是 Marker2D 时返回 true。</returns>
    protected override bool Supports(Node node) => node is Marker2D;

    /// <summary>建立索引并校验本关所有生成点引用。</summary>
    /// <param name="config">本场景对应的关卡配置。</param>
    /// <exception cref="InvalidOperationException">条目登记无效或引用了不存在的生成点时抛出。</exception>
    public void Initialize(LevelConfig config)
    {
        m_Points = BuildIndex<Marker2D>();
        RequirePoint(config.PlayerSpawnPointId);
        foreach (LevelStage stage in config.Stages)
        {
            foreach (LevelSpawnRecipe recipe in stage.Recipes)
            {
                RequirePoint(recipe.SpawnPointId);
            }
        }
    }

    /// <summary>返回已校验生成点的世界坐标。</summary>
    /// <param name="id">场景中登记的生成点稳定 ID。</param>
    /// <returns>生成点的世界坐标。</returns>
    public Vector2 PositionOf(string id) => m_Points[id].GlobalPosition;

    /// <summary>确认配置引用的生成点已经登记。</summary>
    /// <param name="id">配置引用的生成点稳定 ID。</param>
    /// <exception cref="InvalidOperationException">生成点缺失时抛出。</exception>
    private void RequirePoint(string id)
    {
        if (!m_Points.ContainsKey(id))
        {
            throw new InvalidOperationException($"生成点不存在：SpawnPointId={id}");
        }
    }
}
