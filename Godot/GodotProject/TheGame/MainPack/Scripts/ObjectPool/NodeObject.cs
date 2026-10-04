using Godot;
using System;
using GameFramework.ObjectPool;
using GameFramework;
namespace GodotGameFramework.NodePool;

/// <summary>对象池中的 Godot 节点包装对象。</summary>
public partial class NodeObject : ObjectBase
{
    /// <summary>从引用池创建并初始化节点包装对象。</summary>
    /// <param name="name">对象池条目的资源名称。</param>
    /// <param name="node">要包装的节点实例。</param>
    /// <returns>已初始化的节点包装对象。</returns>
    public static NodeObject Create(string name, Node node)
    {
        NodeObject obj = ReferencePool.Acquire<NodeObject>();
        obj.Initialize(name, node);
        return obj;
    }

    /// <summary>释放包装对象并销毁关联节点。</summary>
    /// <param name="isShutdown">是否因应用关闭而释放。</param>
    protected internal override void Release(bool isShutdown)
    {
        if (Target is Node node)
        {
            node.QueueFree();
        }
    }

}
