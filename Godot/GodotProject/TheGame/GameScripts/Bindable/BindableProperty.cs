using System;
using System.Collections.Generic;

namespace GameLogic.Bindable;

/// <summary>保存一个值，并在值实际变化后同步通知此实例的订阅者；不承担玩法规则。</summary>
/// <typeparam name="T">保存的值类型。</typeparam>
public sealed class BindableProperty<T>
{
    private T m_Value; // 当前值；通知前完成写入。

    /// <summary>使用初始值创建属性，构造时不发送通知。</summary>
    /// <param name="initialValue">初始值。</param>
    public BindableProperty(T initialValue = default)
    {
        m_Value = initialValue;
    }

    /// <summary>当前值；按默认相等比较器判断变化，再保存并同步通知。</summary>
    public T Value
    {
        get => m_Value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(m_Value, value))
            {
                return;
            }

            m_Value = value;
            Changed?.Invoke(value);
        }
    }

    /// <summary>值变化通知，参数为已经保存的新值；订阅者自行管理订阅与退订。</summary>
    public event Action<T> Changed;
}
