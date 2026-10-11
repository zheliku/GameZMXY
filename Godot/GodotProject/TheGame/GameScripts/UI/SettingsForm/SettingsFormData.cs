using GameLogic.Session;

namespace GameLogic.UI;

/// <summary>设置窗口打开参数：战斗中携带当前收益事务，标题中不携带关卡。</summary>
public sealed class SettingsFormData
{
    /// <summary>所属关卡收益事务；标题设置为 null。</summary>
    public LevelRewardTransaction Rewards { get; }
    /// <summary>绑定需要暂停的关卡收益事务。</summary>
    /// <param name="rewards">当前关卡收益事务；标题设置省略。</param>
    public SettingsFormData(LevelRewardTransaction rewards = null) => Rewards = rewards;
}
