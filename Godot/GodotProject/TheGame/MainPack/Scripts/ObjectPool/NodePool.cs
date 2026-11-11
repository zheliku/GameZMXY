using System;
using System.Collections.Generic;
using GameConfig.Constant;
using GameFramework;
using GameFramework.ObjectPool;
using Godot;
using GodotGameFrameworkCore.SingletonSystem;

namespace GodotGameFramework.NodePool;

/// <summary>
/// 节点取池与归还的运行状态契约。
/// </summary>
public interface IPoolable
{
    /// <summary>
    /// 对象从池中取出时重置其运行状态。
    /// </summary>
    void OnGet();

    /// <summary>
    /// 对象归还到池中前清理其运行状态。
    /// </summary>
    void OnRelease();
}

/// <summary>
/// 以场景路径登记原生节点容器，复用GF.ObjectPool的容量、过期和闲置对象管理。
/// </summary>
public partial class NodePool : SingletonNode<NodePool>
{
    private readonly Dictionary<string, (PackedScene Scene, Node Container)> _pools = new(); // 每个场景唯一的资源和闲置节点容器。
    private readonly Dictionary<ulong, string> _nodePools = new(); // 借出节点的实例号到场景池名，归还后移除。

    /// <summary>
    /// 当前节点池配置；加载失败时为空。
    /// </summary>
    public NodePoolConfig Config { get; private set; }

    /// <summary>
    /// 加载配置并登记场景池；外部资源或单池注册失败不阻止其余条目。
    /// </summary>
    protected override void OnLoad()
    {
        base.OnLoad();
        Config = GF.Resource.LoadAsset<NodePoolConfig>(ResourcesCollectionConstant.Resources_NodePoolConfigRes);
        if (Config == null)
        {
            Log.Warning("[NodePool] 未找到 NodePoolConfig: {0}", ResourcesCollectionConstant.Resources_NodePoolConfigRes);
            return;
        }

        if (Config.Entries == null || Config.Entries.Count == 0)
        {
            Log.Warning("[NodePool] NodePoolConfig.Entries 为空，请在编辑器中点击 Scan 按钮生成。");
            return;
        }

        var seenPools = new HashSet<string>();
        foreach (PoolEntry entry in Config.Entries)
        {
            if (entry?.Scene == null)
            {
                Log.Warning("[NodePool] 跳过空条目");
                continue;
            }

            string poolName = entry.Scene;
            if (!seenPools.Add(poolName))
            {
                Log.Warning("[NodePool] 跳过重复条目: {0}", poolName);
                continue;
            }

            if (GF.ObjectPool.HasObjectPool<NodeObject>(poolName))
            {
                Log.Info("[NodePool] 池已存在，跳过: {0}", poolName);
                continue;
            }

            PackedScene scene = ResourceLoader.Load<PackedScene>(poolName);
            if (scene == null)
            {
                Log.Warning("[NodePool] 无法加载场景: {0}", poolName);
                continue;
            }

            var container = new Node { Name = poolName };
            AddChild(container);
            int capacity = entry.Capacity > 0 ? entry.Capacity : Config.DefaultCapacity;
            float expireTime = entry.ExpireTime > 0 ? entry.ExpireTime : Config.DefaultExpireTime;
            float autoRelease = entry.AutoReleaseInterval > 0 ? entry.AutoReleaseInterval : Config.DefaultAutoReleaseInterval;
            try
            {
                GF.ObjectPool.CreateSingleSpawnObjectPool<NodeObject>(poolName, autoRelease, capacity, expireTime, 0);
                _pools.Add(poolName, (scene, container));
                Log.Info("[NodePool] 已注册: {0} (容量={1}, 过期={2}s, 懒加载)", poolName, capacity, expireTime);
            }
            catch (Exception error)
            {
                Log.Error("[NodePool] 注册池失败: {0} — {1}", poolName, error.Message);
            }
        }
    }

    /// <summary>
    /// 借出已登记场景的节点，先重置状态再显示并挂到请求的父节点；类型不匹配时沿同一路径归还。
    /// </summary>
    /// <param name="scenePath">配置中已登记的场景资源路径。</param>
    /// <param name="parent">可选业务父节点；为空时保留在原容器中。</param>
    /// <typeparam name="T">需要的节点类型，必须实现池化状态契约。</typeparam>
    /// <returns>本次借用的节点；场景池不可用、实例化失败或类型不匹配时为空。</returns>
    public T RentNode<T>(string scenePath, Node parent = null) where T : class, IPoolable
    {
        IObjectPool<NodeObject> pool = GF.ObjectPool.GetObjectPool<NodeObject>(scenePath);
        if (pool == null || !_pools.TryGetValue(scenePath, out var registration))
        {
            Log.Error("[NodePool] 对象池不存在: {0}", scenePath);
            return null;
        }

        NodeObject wrapper = pool.Spawn(scenePath);
        if (wrapper == null)
        {
            Node created;
            try
            {
                created = registration.Scene.Instantiate();
            }
            catch (Exception error)
            {
                Log.Error("[NodePool] Instantiate 失败: {0} — {1}", scenePath, error.Message);
                return null;
            }

            if (created is not IPoolable)
            {
                Log.Error("[NodePool] 实例化的节点未实现 IPoolable: {0}", scenePath);
                created.QueueFree();
                return null;
            }

            registration.Container.AddChild(created);
            wrapper = NodeObject.Create(scenePath, created);
            pool.Register(wrapper, spawned: true);
        }

        // 只有上方已校验的场景节点能进入本池，不重复校验包装对象的内部契约。
        var node = (Node)wrapper.Target;
        var poolable = (IPoolable)node;
        poolable.OnGet();
        if (node is CanvasItem canvasItem)
        {
            canvasItem.Visible = true;
        }
        else if (node is Node3D node3D)
        {
            node3D.Visible = true;
        }

        if (parent != null)
        {
            node.GetParent()?.RemoveChild(node);
            parent.AddChild(node);
        }

        _nodePools[node.GetInstanceId()] = scenePath;
        if (node is T target)
        {
            return target;
        }

        Log.Error("[NodePool] 类型不匹配: 期望 {0}，实际 {1}", typeof(T).Name, node.GetType().Name);
        Release(poolable);
        return null;
    }

    /// <summary>
    /// 归还本池借出的节点；按原顺序解除GF借用、清理状态、隐藏并摘挂回闲置容器。
    /// </summary>
    /// <param name="poolItem">本池借出的节点；空值或非节点不处理，重复归还只报告归属缺失。</param>
    public void Release(IPoolable poolItem)
    {
        if (poolItem is not Node node)
        {
            return;
        }

        ulong id = node.GetInstanceId();
        if (!_nodePools.TryGetValue(id, out string poolName))
        {
            Log.Warning("[NodePool] 无法归还 IPoolable：未找到节点 {0} 的归属池", node.Name);
            return;
        }

        IObjectPool<NodeObject> pool = GF.ObjectPool.GetObjectPool<NodeObject>(poolName);
        if (pool == null)
        {
            Log.Warning("[NodePool] 无法归还：对象池 '{0}' 不存在", poolName);
            return;
        }

        pool.Unspawn((object)poolItem);
        poolItem.OnRelease();
        if (node is CanvasItem canvasItem)
        {
            canvasItem.Visible = false;
        }
        else if (node is Node3D node3D)
        {
            node3D.Visible = false;
        }

        node.GetParent()?.RemoveChild(node);
        _pools[poolName].Container.AddChild(node);
        _nodePools.Remove(id);
    }
}
