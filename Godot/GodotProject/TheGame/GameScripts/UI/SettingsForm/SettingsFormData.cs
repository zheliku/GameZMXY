using GameLogic.Session;

namespace GameLogic.UI;

/// <summary>设置窗口打开参数：战斗中携带当前运行引用，标题中不携带关卡。</summary>
public sealed class SettingsFormData
{
    /// <summary>所属关卡运行；标题设置为 null。</summary>
    public LevelRun Run { get; }
    /// <summary>绑定需要暂停的关卡运行。</summary>
    /// <param name="run">当前关卡运行；标题设置省略。</param>
    public SettingsFormData(LevelRun run = null) => Run = run;
}
