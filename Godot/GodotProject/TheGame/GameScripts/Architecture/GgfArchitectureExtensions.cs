using GameFramework.Procedure;
using GodotGameFramework;

namespace GameLogic.Architecture;

/// <summary>表现层经 GGF 门面访问主流程的 Architecture；不增加全局单例或可变静态状态。</summary>
public static class GgfArchitectureExtensions
{
    /// <summary>主流程只有一个无名 IProcedureManager FSM；具名局部 FSM 使用自己的绑定入口。</summary>
    /// <param name="fsms">GGF 的 FSM 服务。</param>
    extension(FsmComponent fsms)
    {
        /// <summary>获取启动流程注册的游戏访问入口。</summary>
        /// <returns>主流程的 Architecture。</returns>
        public IArchitecture GetArchitecture() => fsms.GetFsm<IProcedureManager>().GetArchitecture();

        /// <summary>获取主流程的数据模型；局部 FSM 使用自己绑定的 Architecture。</summary>
        /// <typeparam name="TModel">模型类型。</typeparam>
        /// <returns>同一模型引用；未注册时返回 null。</returns>
        public TModel GetModel<TModel>() where TModel : class, IModel => fsms.GetArchitecture().GetModel<TModel>();

        /// <summary>获取主流程的业务系统。</summary>
        /// <typeparam name="TSystem">系统类型。</typeparam>
        /// <returns>同一业务入口；未注册时返回 null。</returns>
        public TSystem GetSystem<TSystem>() where TSystem : class, ISystem => fsms.GetArchitecture().GetSystem<TSystem>();

        /// <summary>获取主流程的第三方工具。</summary>
        /// <typeparam name="TUtility">工具类型。</typeparam>
        /// <returns>同一工具实例；未注册时返回 null。</returns>
        public TUtility GetUtility<TUtility>() where TUtility : class, IUtility => fsms.GetArchitecture().GetUtility<TUtility>();
    }
}
