namespace GameLogic.Architecture;

/// <summary>游戏的按类型取用契约；实际对象只存储在所属 GGF 流程 FSM。</summary>
public interface IArchitecture
{
    /// <summary>借用所属作用域的同一模型。</summary>
    /// <typeparam name="TModel">模型注册类型。</typeparam>
    /// <returns>同一模型引用；未注册时返回 null。</returns>
    TModel GetModel<TModel>() where TModel : class, IModel;

    /// <summary>借用所属作用域的同一业务系统。</summary>
    /// <typeparam name="TSystem">系统注册类型。</typeparam>
    /// <returns>同一系统引用；未注册时返回 null。</returns>
    TSystem GetSystem<TSystem>() where TSystem : class, ISystem;

    /// <summary>借用已注册的第三方工具。</summary>
    /// <typeparam name="TUtility">工具注册类型。</typeparam>
    /// <returns>同一工具引用；未注册时返回 null。</returns>
    TUtility GetUtility<TUtility>() where TUtility : class, IUtility;
}

/// <summary>声明游戏作用域归属；实际访问能力由下方小接口分别授予。</summary>
public interface IBelongToArchitecture
{
    /// <summary>读取装配时绑定的所属入口。</summary>
    /// <returns>借用的 Architecture。</returns>
    IArchitecture GetArchitecture();
}

/// <summary>由装配边界绑定归属，不允许对象转移到另一游戏作用域。</summary>
public interface ICanSetArchitecture
{
    /// <summary>绑定所属入口。</summary>
    /// <param name="architecture">所属 Architecture。</param>
    void SetArchitecture(IArchitecture architecture);
}

/// <summary>注册与移除时执行的同步生命周期；异步业务仍由流程显式调度。</summary>
public interface ICanInit
{
    /// <summary>是否已经完成注册初始化。</summary>
    bool Initialized { get; }
    /// <summary>完成依赖绑定后的初始化。</summary>
    void Init();
    /// <summary>解除注册前结束本对象的同步生命周期。</summary>
    void Deinit();
}

/// <summary>允许读取 Model；模型和工具不具有此能力。</summary>
public interface ICanGetModel : IBelongToArchitecture { }

/// <summary>允许调用 System；模型和工具不具有此能力。</summary>
public interface ICanGetSystem : IBelongToArchitecture { }

/// <summary>允许使用第三方 Utility；工具自身不具有此能力。</summary>
public interface ICanGetUtility : IBelongToArchitecture { }

/// <summary>拥有数据并维护自身不变量；可使用工具，不查询其他模型或业务系统。</summary>
public interface IModel : IBelongToArchitecture, ICanSetArchitecture, ICanGetUtility, ICanInit { }

/// <summary>业务功能入口，对应 QFramework 的 System 职责。</summary>
public interface ISystem : IBelongToArchitecture, ICanSetArchitecture, ICanGetModel, ICanGetSystem, ICanGetUtility, ICanInit { }

/// <summary>第三方框架或平台适配器；没有 Architecture，不查询 Model 或 System。</summary>
public interface IUtility { }

/// <summary>表现入口的访问能力；Godot 节点与 GGF 窗口保留各自继承链。</summary>
public interface IController : ICanGetModel, ICanGetSystem, ICanGetUtility { }
