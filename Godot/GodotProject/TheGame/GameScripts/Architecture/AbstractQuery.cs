using System;

namespace GameLogic.Architecture;

/// <summary>借鉴 QFramework 的同步查询契约；借用发送者架构，不注册、缓存或订阅。</summary>
/// <typeparam name="TResult">本次查询结果。</typeparam>
/// <remarks>用于组合读取；简单属性直接 GetModel/GetSystem，持续显示另行订阅 Model 的绑定或事件。</remarks>
public abstract class AbstractQuery<TResult> : IQuery<TResult>
{
    private IArchitecture m_Architecture; // 本次发送借用的架构，不拥有模型或系统。

    /// <summary>实现无副作用的读取或组合计算。</summary>
    /// <returns>当前状态的查询结果。</returns>
    protected abstract TResult OnDo();

    /// <inheritdoc />
    IArchitecture IBelongToArchitecture.GetArchitecture() => m_Architecture
        ?? throw new InvalidOperationException("查询须通过 SendQuery 绑定所属架构后执行。");

    /// <inheritdoc />
    void ICanSetArchitecture.SetArchitecture(IArchitecture architecture) =>
        m_Architecture = architecture ?? throw new ArgumentNullException(nameof(architecture));

    /// <inheritdoc />
    TResult IQuery<TResult>.Do() => OnDo();
}
