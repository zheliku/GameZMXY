using System;

namespace GameLogic.Architecture;

/// <summary>借鉴 QFramework 的命令归属与执行契约；不承担初始化、通知、持久化或自动回滚。</summary>
/// <typeparam name="TResult">本次业务结果。</typeparam>
public abstract class AbstractCommand<TResult> : ICommand<TResult>
{
    private IArchitecture m_Architecture; // 本次同步发送借用的架构，不注册为长期服务。

    /// <summary>由具名请求实现业务入口，复用已有 System 规则。</summary>
    /// <returns>实际执行结果。</returns>
    protected abstract TResult OnExecute();

    /// <inheritdoc />
    IArchitecture IBelongToArchitecture.GetArchitecture() => m_Architecture
        ?? throw new InvalidOperationException("命令须通过 SendCommand 绑定所属架构后执行。");

    /// <inheritdoc />
    void ICanSetArchitecture.SetArchitecture(IArchitecture architecture) =>
        m_Architecture = architecture ?? throw new ArgumentNullException(nameof(architecture));

    /// <inheritdoc />
    TResult ICommand<TResult>.Execute() => OnExecute();
}
