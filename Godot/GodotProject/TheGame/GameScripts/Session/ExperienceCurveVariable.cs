using GameFramework;
using GameLogic.Profile;

namespace GameLogic.Session;

/// <summary>流程数据的经验曲线包装，与其他作用域传递类型集中存放。</summary>
public sealed class ExperienceCurveVariable : Variable<ExperienceCurve>
{
    /// <summary>从引用池创建包装。</summary>
    /// <param name="value">已校验的经验曲线。</param>
    /// <returns>池化包装，所有权交给流程状态机。</returns>
    public static ExperienceCurveVariable Create(ExperienceCurve value)
    {
        ExperienceCurveVariable variable = ReferencePool.Acquire<ExperienceCurveVariable>();
        variable.Value = value;
        return variable;
    }
}
