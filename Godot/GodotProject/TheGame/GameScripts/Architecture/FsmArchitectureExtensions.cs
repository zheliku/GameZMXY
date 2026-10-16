using System;
using GameFramework;
using GameFramework.Fsm;
using GameFramework.Procedure;

namespace GameLogic.Architecture;

/// <summary>以 GGF 主流程 FSM 的 GetData/SetData 实现 Architecture；不创建第二个数据容器。</summary>
public static class FsmArchitectureExtensions
{
    /// <summary>先检查类型是否已占用，避免重复注册改变对象的归属或状态。</summary>
    /// <typeparam name="T">注册类型。</typeparam>
    /// <param name="fsm">所属 FSM。</param>
    private static void EnsureMissing<T>(IFsm<IProcedureManager> fsm) where T : class
    {
        if (fsm.HasData(Entry<T>.Key))
            throw new InvalidOperationException($"FSM 已持有 {typeof(T).Name}，须先结束原作用域。");
    }

    /// <summary>结束同步生命周期后解除 FSM 的持有。</summary>
    /// <typeparam name="T">模型或系统注册类型。</typeparam>
    /// <param name="fsm">所属 FSM。</param>
    /// <returns>是否实际移除。</returns>
    private static bool RemoveMember<T>(IFsm<IProcedureManager> fsm) where T : class, ICanInit
    {
        if (!fsm.HasData(Entry<T>.Key))
            return false;
        ReadRequired<T>(fsm).Deinit();
        return fsm.RemoveData(Entry<T>.Key);
    }

    /// <summary>只负责 GGF Variable 的包装与重复注册检查。</summary>
    /// <typeparam name="T">注册类型。</typeparam>
    /// <param name="fsm">唯一实际存储位置。</param>
    /// <param name="value">已初始化的对象。</param>
    private static void Store<T>(IFsm<IProcedureManager> fsm, T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        if (fsm.HasData(Entry<T>.Key))
            throw new InvalidOperationException($"FSM 已持有 {typeof(T).Name}，须先结束原作用域。");
        var entry = ReferencePool.Acquire<Entry<T>>();
        entry.Value = value;
        fsm.SetData(Entry<T>.Key, entry);
    }

    /// <summary>读取内部必需注册项；用于架构入口及已经确认存在的生命周期操作。</summary>
    /// <typeparam name="T">注册类型。</typeparam>
    /// <param name="fsm">所属 FSM。</param>
    /// <returns>已注册的对象。</returns>
    private static T ReadRequired<T>(IFsm<IProcedureManager> fsm) where T : class =>
        fsm.GetData<Entry<T>>(Entry<T>.Key)?.Value
        ?? throw new InvalidOperationException($"当前 FSM 未初始化 {typeof(T).Name}。");

    /// <summary>扩展明确传入的流程 FSM，具名 FSM 之间保持完全隔离。</summary>
    /// <param name="fsm">所属流程状态机。</param>
    extension(IFsm<IProcedureManager> fsm)
    {
        /// <summary>建立本 FSM 唯一的 Architecture；由启动流程调用一次。</summary>
        /// <returns>只开放取用能力的游戏入口。</returns>
        public IArchitecture InitializeArchitecture()
        {
            IArchitecture architecture = new FsmArchitecture(fsm);
            Store(fsm, architecture);
            return architecture;
        }

        /// <summary>获取本 FSM 已初始化的 Architecture，不自动创建。</summary>
        /// <returns>本游戏的访问入口。</returns>
        public IArchitecture GetArchitecture() => ReadRequired<IArchitecture>(fsm);

        /// <summary>注册模型并统一绑定归属；模型构造负责数据校验，注册不重复构造数据。</summary>
        /// <typeparam name="TModel">模型的注册类型。</typeparam>
        /// <param name="model">已完成数据初始化的模型。</param>
        public void RegisterModel<TModel>(TModel model) where TModel : class, IModel
        {
            ArgumentNullException.ThrowIfNull(model);
            EnsureMissing<TModel>(fsm);
            model.SetArchitecture(fsm.GetArchitecture());
            Store(fsm, model);
            try
            {
                model.Init();
            }
            catch
            {
                fsm.RemoveData(Entry<TModel>.Key);
                throw;
            }
        }

        /// <summary>借用本 FSM 已注册的模型。</summary>
        /// <typeparam name="TModel">模型类型。</typeparam>
        /// <returns>同一模型引用；未注册时返回 null。</returns>
        public TModel GetModel<TModel>() where TModel : class, IModel => fsm.GetArchitecture().GetModel<TModel>();

        /// <summary>结束模型的持有；调用方先结束订阅和异步任务。</summary>
        /// <typeparam name="TModel">模型类型。</typeparam>
        /// <returns>本次是否移除了模型。</returns>
        public bool RemoveModel<TModel>() where TModel : class, IModel => RemoveMember<TModel>(fsm);

        /// <summary>发布已装配且绑定本 Architecture 的业务系统。</summary>
        /// <typeparam name="TSystem">系统类型。</typeparam>
        /// <param name="system">已完成初始化的业务入口。</param>
        public void RegisterSystem<TSystem>(TSystem system) where TSystem : class, ISystem
        {
            ArgumentNullException.ThrowIfNull(system);
            EnsureMissing<TSystem>(fsm);
            system.SetArchitecture(fsm.GetArchitecture());
            Store(fsm, system);
            try
            {
                system.Init();
            }
            catch
            {
                fsm.RemoveData(Entry<TSystem>.Key);
                throw;
            }
        }

        /// <summary>借用本 FSM 已注册的业务系统。</summary>
        /// <typeparam name="TSystem">系统类型。</typeparam>
        /// <returns>同一业务入口；未注册时返回 null。</returns>
        public TSystem GetSystem<TSystem>() where TSystem : class, ISystem => fsm.GetArchitecture().GetSystem<TSystem>();

        /// <summary>结束系统持有，不递归清理或保存，生命周期仍由所属流程管理。</summary>
        /// <typeparam name="TSystem">系统类型。</typeparam>
        /// <returns>本次是否移除了系统。</returns>
        public bool RemoveSystem<TSystem>() where TSystem : class, ISystem => RemoveMember<TSystem>(fsm);

        /// <summary>注册第三方工具；Utility 不绑定 Architecture。</summary>
        /// <typeparam name="TUtility">工具类型。</typeparam>
        /// <param name="utility">已初始化的工具。</param>
        public void RegisterUtility<TUtility>(TUtility utility) where TUtility : class, IUtility => Store(fsm, utility);

        /// <summary>借用本 FSM 已注册的第三方工具。</summary>
        /// <typeparam name="TUtility">工具类型。</typeparam>
        /// <returns>同一工具实例；未注册时返回 null。</returns>
        public TUtility GetUtility<TUtility>() where TUtility : class, IUtility => fsm.GetArchitecture().GetUtility<TUtility>();
    }

    /// <summary>GGF 数据区的访问视图；本身不拥有另一组 Model、System 或 Utility 字典。</summary>
    private sealed class FsmArchitecture : IArchitecture
    {
        private readonly IFsm<IProcedureManager> m_Fsm; // 借用所属 FSM；身份检查拒绝池复用后的旧入口。

        /// <summary>绑定明确的 FSM。</summary>
        /// <param name="fsm">所属游戏流程。</param>
        public FsmArchitecture(IFsm<IProcedureManager> fsm) => m_Fsm = fsm;

        /// <inheritdoc />
        public TModel GetModel<TModel>() where TModel : class, IModel => GetRegistered<TModel>();

        /// <inheritdoc />
        public TSystem GetSystem<TSystem>() where TSystem : class, ISystem => GetRegistered<TSystem>();

        /// <inheritdoc />
        public TUtility GetUtility<TUtility>() where TUtility : class, IUtility => GetRegistered<TUtility>();

        /// <summary>确认视图仍属于当前 FSM 生命周期后读取原数据区。</summary>
        /// <typeparam name="T">注册类型。</typeparam>
        /// <returns>同一对象引用；未注册时返回 null。</returns>
        private T GetRegistered<T>() where T : class
        {
            EnsureActive();
            return m_Fsm.GetData<Entry<T>>(Entry<T>.Key)?.Value;
        }

        /// <summary>拒绝已经销毁或池化复用后的旧架构视图。</summary>
        /// <exception cref="InvalidOperationException">所属作用域已经结束。</exception>
        private void EnsureActive()
        {
            if (m_Fsm.IsDestroyed || !ReferenceEquals(m_Fsm.GetData<Entry<IArchitecture>>(Entry<IArchitecture>.Key)?.Value, this))
                throw new InvalidOperationException("所属 Architecture 已结束，不能继续取用数据。");
        }
    }

    /// <summary>唯一的 GGF 池化包装；移除或 FSM 销毁时由框架回收。</summary>
    /// <typeparam name="T">注册类型。</typeparam>
    private sealed class Entry<T> : Variable<T> where T : class
    {
        internal static readonly string Key = $"Architecture:{typeof(T).AssemblyQualifiedName}"; // 不与原生的流程参数键混用。

        /// <summary>供 GGF 引用池创建空包装。</summary>
        public Entry() { }
    }
}
