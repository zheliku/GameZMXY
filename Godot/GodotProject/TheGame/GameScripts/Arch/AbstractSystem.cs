using System;

namespace GameLogic.Architecture;

/// <summary>
/// 业务系统基类；分别实现归属与初始化契约，具体类型只处理自身职责。
/// </summary>
public abstract class AbstractSystem : ISystem
{
    private IArchitecture _architecture; // 借用：绑定后不能转移到其他作用域。

    /// <summary>
    /// 为注册前的装配或异步准备提供明确的归属。
    /// </summary>
    /// <param name="architecture">所属游戏入口。</param>
    protected AbstractSystem(IArchitecture architecture)
    {
        ((ICanSetArchitecture)this).SetArchitecture(architecture);
    }

    /// <inheritdoc />
    public bool Initialized { get; private set; }

    /// <summary>
    /// 注册时执行；构造已完整建立数据的类型可沿用空实现。
    /// </summary>
    protected virtual void OnInit()
    {
    }

    /// <summary>
    /// 移除注册时执行；异步写盘和场景清理由流程先完成。
    /// </summary>
    protected virtual void OnDeinit()
    {
    }

    /// <inheritdoc />
    IArchitecture IBelongToArchitecture.GetArchitecture()
    {
        return _architecture
        ?? throw new InvalidOperationException($"{GetType().Name} 尚未绑定 Architecture。");
    }

    /// <inheritdoc />
    void ICanSetArchitecture.SetArchitecture(IArchitecture architecture)
    {
        ArgumentNullException.ThrowIfNull(architecture);
        if (_architecture != null && !ReferenceEquals(_architecture, architecture))
        {
            throw new InvalidOperationException("对象不能重新绑定到其他 Architecture。");
        }

        _architecture = architecture;
    }

    /// <inheritdoc />
    void ICanInit.Init()
    {
        if (Initialized)
        {
            return;
        }

        try
        {
            OnInit();
            Initialized = true;
        }
        catch (Exception error)
        {
            // 初始化可能在抛错前已经建立订阅或缓存；保证失败对象也走一次对称清理。
            try
            {
                OnDeinit();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("系统初始化失败，且清理也失败。", error, cleanupError);
            }

            throw;
        }
    }

    /// <inheritdoc />
    void ICanInit.Deinit()
    {
        if (!Initialized)
        {
            return;
        }

        try
        {
            OnDeinit();
        }
        finally
        {
            // 即使退出钩子报告错误，也不能让外部继续把对象当作已初始化成员使用。
            Initialized = false;
        }
    }
}
