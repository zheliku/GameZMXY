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

    /// <summary>借用已注册的无状态工具或平台适配器。</summary>
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

/// <summary>由装配或发送边界绑定归属；长期 Model/System 不允许转移作用域。</summary>
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

/// <summary>允许取得 Model；Controller 只读和订阅，接口不限制所取对象自身的写入 API。</summary>
public interface ICanGetModel : IBelongToArchitecture { }

/// <summary>允许取得 System；Controller 调用查询方法，玩家业务修改通过 Command 提交。</summary>
public interface ICanGetSystem : IBelongToArchitecture { }

/// <summary>允许使用第三方 Utility；工具自身不具有此能力。</summary>
public interface ICanGetUtility : IBelongToArchitecture { }

/// <summary>允许提交一次同步业务请求；命令不注册进 FSM，也不排队。</summary>
public interface ICanSendCommand : IBelongToArchitecture { }

/// <summary>允许执行一次无副作用的组合查询；不负责注册绑定或订阅变化。</summary>
public interface ICanSendQuery : IBelongToArchitecture { }

/// <summary>一次带结果的业务请求；执行期间借用发送者所属架构。</summary>
/// <typeparam name="TResult">原业务结果，不另建统一返回值包装。</typeparam>
public interface ICommand<TResult> : IBelongToArchitecture, ICanSetArchitecture,
    ICanGetModel, ICanGetSystem, ICanGetUtility, ICanSendQuery
{
    /// <summary>同步完成一次请求，业务规则由相应 System 执行。</summary>
    /// <returns>本次操作结果。</returns>
    TResult Execute();
}

/// <summary>一次读取或组合计算；不修改 Model、不发送命令、不持有订阅。</summary>
/// <typeparam name="TResult">本次查询结果；优先返回值或不可变数据。</typeparam>
public interface IQuery<TResult> : IBelongToArchitecture, ICanSetArchitecture,
    ICanGetModel, ICanGetSystem, ICanSendQuery
{
    /// <summary>同步读取当前状态并返回结果，不缓存以后的变化。</summary>
    /// <returns>执行这一刻的查询结果。</returns>
    TResult Do();
}

/// <summary>拥有权威数据并提供查询；可使用工具，不查询其他模型或业务系统，业务修改由 System 执行。</summary>
public interface IModel : IBelongToArchitecture, ICanSetArchitecture, ICanGetUtility, ICanInit { }

/// <summary>业务功能入口，对应 QFramework 的 System 职责。</summary>
public interface ISystem : IBelongToArchitecture, ICanSetArchitecture, ICanGetModel, ICanGetSystem, ICanGetUtility, ICanInit { }

/// <summary>纯转换或平台适配器；不持有 Architecture、Model 或 System，可按显式调用参数临时读取数据。</summary>
public interface IUtility { }

/// <summary>表现入口读取、订阅状态并提交业务请求；节点局部交互仍自行处理，保留 Godot/GGF 继承链。</summary>
public interface IController : ICanGetModel, ICanGetSystem, ICanGetUtility, ICanSendCommand, ICanSendQuery { }
