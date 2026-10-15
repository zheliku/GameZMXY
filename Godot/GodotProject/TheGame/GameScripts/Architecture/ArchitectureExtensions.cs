namespace GameLogic.Architecture;

/// <summary>按能力提供 Model、System、Utility 访问语法；能力不在继承链中反向开放。</summary>
public static class ArchitectureExtensions
{
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
