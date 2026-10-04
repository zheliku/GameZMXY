using Godot;
namespace GodotGameFramework.NodePool;
/// <summary>
/// 池容器节点，作为池中所有对象在场景树中的父节点。
/// 归还对象时重新挂载到此节点下。
/// </summary>
public partial class PoolContainer : Node
{
    /// <summary>此容器所属的对象池名称。</summary>
    public string PoolName { get; set; }

    /// <summary>创建以池名称命名的对象容器节点。</summary>
    /// <param name="poolName">容器所属的池名称。</param>
    public PoolContainer(string poolName)
    {
        PoolName = poolName;
        Name = poolName;
    }
}
