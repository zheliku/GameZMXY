using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameFramework;
using GameFramework.WebRequest;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Web;

/// <summary>
/// 原生验证用最小框架：检查本地HTTP、在途退出、任务池清理和同进程重新装配。
/// </summary>
public partial class ValidateFrameworkShutdown : RefCounted
{
    private WebRequestComponent _web; // 当前验证周期的真实网络组件。
    private IWebRequestManager _manager; // 保留关闭后的纯托管管理器以核对清理结果。
    private Task<WebRequestCompleteEventArgs>[] _requests; // 本周期调用方拥有的请求任务。

    /// <summary>
    /// 创建无流程、无磁盘存档的最小框架；调用方将返回节点加入场景树并负责释放。
    /// </summary>
    /// <returns>带基础、事件与网络组件的框架根节点。</returns>
    public Node CreateFramework()
    {
        var root = new GameEntry();
        root.AddChild(new BaseComponent());
        root.AddChild(new EventComponent());
        _web = new WebRequestComponent();
        root.AddChild(_web);
        _requests = [];
        return root;
    }

    /// <summary>
    /// 发起本地GET和POST，验证修复未改变正常请求和代理复用。
    /// </summary>
    /// <param name="origin">测试HTTP服务器地址，仅使用127.0.0.1。</param>
    public void StartNormal(string origin)
    {
        _manager = GameFrameworkEntry.GetModule<IWebRequestManager>();
        Check(_manager.TotalAgentCount == 4, "网络代理数量不正确或重复注册");
        _requests =
        [
            _web.SendRequestAsync(origin + "/get"),
            _web.SendRequestAsync(origin + "/post", Encoding.UTF8.GetBytes("payload"))
        ];
    }

    /// <summary>
    /// 检查本周期异步请求是否全部完成。
    /// </summary>
    /// <returns>所有任务均已完成时为真。</returns>
    public bool RequestsCompleted()
    {
        return _requests.All(request => request.IsCompleted);
    }

    /// <summary>
    /// 验证本地GET和POST的实际响应。
    /// </summary>
    public void VerifyNormal()
    {
        Check(RequestsCompleted(), "本地请求未完成");
        Check(_requests.All(request => request.Result?.ResponseCode == 200), "本地请求未成功");
        Check(Encoding.UTF8.GetString(_requests[0].Result.Body) == "get", "GET响应不正确");
        Check(Encoding.UTF8.GetString(_requests[1].Result.Body) == "payload", "POST响应不正确");
    }

    /// <summary>
    /// 发起超过代理数的慢请求，同时覆盖在途和排队任务的关停。
    /// </summary>
    /// <param name="origin">本地测试服务器地址。</param>
    public void StartPending(string origin)
    {
        _requests = Enumerable.Range(0, 6).Select(index => _web.SendRequestAsync(origin + "/slow/" + index)).ToArray();
    }

    /// <summary>
    /// 使用本地慢响应验证正常超时仍能取消原生请求并完成调用方任务。
    /// </summary>
    /// <param name="origin">本地测试服务器地址。</param>
    public void StartTimeout(string origin)
    {
        _requests = [_web.SendRequestAsync(origin + "/slow/timeout", .1f)];
    }

    /// <summary>
    /// 验证超时沿既有失败结果返回，之后代理可以继续使用。
    /// </summary>
    public void VerifyTimeout()
    {
        Check(RequestsCompleted() && _requests[0].Result?.Result == -1, "请求超时行为改变");
    }

    /// <summary>
    /// 检查在途和等待队列均已进入验证状态。
    /// </summary>
    /// <returns>四条在途、两条排队且都未完成时为真。</returns>
    public bool RequestsRunning()
    {
        return _manager.WorkingAgentCount == 4 && _manager.WaitingTaskCount == 2 &&
            _requests.All(request => !request.IsCompleted);
    }

    /// <summary>
    /// 验证节点销毁已完成所有等待者，并清空核心任务池与组件注册。
    /// </summary>
    public void VerifyStopped()
    {
        Check(_requests.All(request => request.IsCompletedSuccessfully && request.Result == null), "退出遗留未完成请求");
        Check(_manager.TotalAgentCount == 0 && _manager.WaitingTaskCount == 0, "任务池未完成关闭");
        Check(GameEntry.GetComponent<WebRequestComponent>() == null && GameEntry.GetComponent<BaseComponent>() == null,
            "组件注册表保留已释放节点");
    }

    /// <summary>
    /// 显式关闭框架两次，验证随后销毁节点不会重复清理或访问已释放代理。
    /// </summary>
    public void StopExplicitly()
    {
        GameEntry.Shutdown(ShutdownType.None);
        GameEntry.Shutdown(ShutdownType.None);
    }

    /// <summary>
    /// 将验证错误报告给原生驱动，不增加正式组件的观测接口。
    /// </summary>
    /// <param name="condition">需要成立的清理条件。</param>
    /// <param name="message">失败原因。</param>
    /// <exception cref="InvalidOperationException">清理条件不成立。</exception>
    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
