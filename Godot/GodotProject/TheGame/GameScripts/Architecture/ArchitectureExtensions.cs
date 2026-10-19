using System;

namespace GameLogic.Architecture;

/// <summary>按能力提供 Model、System、Utility 访问语法；能力不在继承链中反向开放。</summary>
public static class ArchitectureExtensions
{
    /// <summary>复用现有架构执行请求，不建立第二个注册容器或请求管理器。</summary>
    /// <param name="architecture">发送者所属的现有架构。</param>
    extension(IArchitecture architecture)
    {
        /// <summary>仿照 QFramework：绑定本次架构后立即执行，异常原样交给调用方。</summary>
        /// <typeparam name="TResult">业务结果类型。</typeparam>
        /// <param name="command">本次请求；不持有节点或池化事件。</param>
        /// <returns>同步执行结果。</returns>
        /// <exception cref="ArgumentNullException">架构或命令为空。</exception>
        public TResult SendCommand<TResult>(ICommand<TResult> command)
        {
            ArgumentNullException.ThrowIfNull(architecture);
            ArgumentNullException.ThrowIfNull(command);
            command.SetArchitecture(architecture);
            return command.Execute();
        }

        /// <summary>绑定当前架构后同步查询；后续变化由原有绑定或事件观察。</summary>
        /// <typeparam name="TResult">查询结果类型。</typeparam>
        /// <param name="query">本次无副作用的查询。</param>
        /// <returns>当前状态的查询结果。</returns>
        /// <exception cref="ArgumentNullException">架构或查询为空。</exception>
        public TResult SendQuery<TResult>(IQuery<TResult> query)
        {
            ArgumentNullException.ThrowIfNull(architecture);
            ArgumentNullException.ThrowIfNull(query);
            query.SetArchitecture(architecture);
            return query.Do();
        }
    }

    /// <summary>控制器、命令或组合查询使用自身所属架构读取结果。</summary>
    /// <param name="owner">具备查询能力的访问者。</param>
    extension(ICanSendQuery owner)
    {
        /// <summary>同步执行一次查询，不添加状态副本或观察关系。</summary>
        /// <typeparam name="TResult">查询结果类型。</typeparam>
        /// <param name="query">本次查询。</param>
        /// <returns>实际查询结果。</returns>
        public TResult SendQuery<TResult>(IQuery<TResult> query) =>
            owner.GetArchitecture().SendQuery(query);
    }

    /// <summary>控制器使用自身已装配的架构发送请求。</summary>
    /// <param name="owner">具备发送能力的控制器。</param>
    extension(ICanSendCommand owner)
    {
        /// <summary>同步执行一项业务请求，仍使用当前 GGF FSM 的同一组 Model/System。</summary>
        /// <typeparam name="TResult">业务结果类型。</typeparam>
        /// <param name="command">本次请求。</param>
        /// <returns>实际业务结果。</returns>
        public TResult SendCommand<TResult>(ICommand<TResult> command) =>
            owner.GetArchitecture().SendCommand(command);
    }

    /// <summary>扩展允许读取 Model 的业务入口或控制器。</summary>
    /// <param name="owner">当前访问者。</param>
    extension(ICanGetModel owner)
    {
        /// <summary>获取所属游戏的数据模型。</summary>
        /// <typeparam name="TModel">模型类型。</typeparam>
        /// <returns>同一模型引用；未注册时返回 null。</returns>
        public TModel GetModel<TModel>() where TModel : class, IModel => owner.GetArchitecture().GetModel<TModel>();
    }

    /// <summary>扩展允许调用 System 的业务入口或控制器。</summary>
    /// <param name="owner">当前访问者。</param>
    extension(ICanGetSystem owner)
    {
        /// <summary>获取所属游戏的业务系统。</summary>
        /// <typeparam name="TSystem">系统类型。</typeparam>
        /// <returns>同一业务入口；未注册时返回 null。</returns>
        public TSystem GetSystem<TSystem>() where TSystem : class, ISystem => owner.GetArchitecture().GetSystem<TSystem>();
    }

    /// <summary>扩展允许使用 Utility 的模型、系统或控制器。</summary>
    /// <param name="owner">当前访问者。</param>
    extension(ICanGetUtility owner)
    {
        /// <summary>获取所属游戏的第三方工具。</summary>
        /// <typeparam name="TUtility">工具类型。</typeparam>
        /// <returns>同一工具实例；未注册时返回 null。</returns>
        public TUtility GetUtility<TUtility>() where TUtility : class, IUtility => owner.GetArchitecture().GetUtility<TUtility>();
    }
}
