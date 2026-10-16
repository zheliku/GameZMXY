using System;

namespace GameLogic.Architecture;

/// <summary>数据模型基类；分别实现归属与初始化契约，具体类型只处理自身职责。</summary>
public abstract class AbstractModel : IModel
{
    private IArchitecture m_Architecture; // 借用：绑定后不能转移到其他作用域。

    /// <inheritdoc />
    public bool Initialized { get; private set; }

    /// <summary>注册时执行；构造已完整建立数据的类型可沿用空实现。</summary>
    protected virtual void OnInit() { }

    /// <summary>移除注册时执行；异步写盘和场景清理由流程先完成。</summary>
    protected virtual void OnDeinit() { }

    /// <inheritdoc />
    IArchitecture IBelongToArchitecture.GetArchitecture() => m_Architecture
        ?? throw new InvalidOperationException($"{GetType().Name} 尚未绑定 Architecture。");

    /// <inheritdoc />
    void ICanSetArchitecture.SetArchitecture(IArchitecture architecture)
    {
        ArgumentNullException.ThrowIfNull(architecture);
        if (m_Architecture != null && !ReferenceEquals(m_Architecture, architecture))
            throw new InvalidOperationException("对象不能重新绑定到其他 Architecture。");
        m_Architecture = architecture;
    }

    /// <inheritdoc />
    void ICanInit.Init()
    {
        if (Initialized)
            return;
        OnInit();
        Initialized = true;
    }

    /// <inheritdoc />
    void ICanInit.Deinit()
    {
        if (!Initialized)
            return;
        OnDeinit();
        Initialized = false;
    }
}
