using System;
using GameLogic.Level;

namespace GameLogic.UI;

/// <summary>本次结算窗口的打开参数；成绩由关卡冻结，窗口只读取。</summary>
public sealed class SettlementFormData
{
    /// <summary>本次已经提交的关卡成绩。</summary>
    public LevelResult Result { get; }

    /// <summary>绑定窗口要显示的成绩。</summary>
    /// <param name="result">关卡已经冻结的成功或失败结果。</param>
    /// <exception cref="ArgumentNullException">成绩为空。</exception>
    public SettlementFormData(LevelResult result) => Result = result ?? throw new ArgumentNullException(nameof(result));
}
