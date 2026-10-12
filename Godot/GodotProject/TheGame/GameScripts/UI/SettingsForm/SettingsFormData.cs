using GameLogic;
using GameLogic.Level;

namespace GameLogic.UI;

/// <summary>设置窗口打开参数：战斗中携带当前模块，标题中不携带关卡。</summary>
public sealed class SettingsFormData
{
    /// <summary>所属关卡模块；标题设置为 null。</summary>
    public LevelModule Level { get; }
    /// <summary>绑定需要暂停的关卡模块。</summary>
    /// <param name="level">当前关卡模块；标题设置省略。</param>
    public SettingsFormData(LevelModule level = null) => Level = level;
}
