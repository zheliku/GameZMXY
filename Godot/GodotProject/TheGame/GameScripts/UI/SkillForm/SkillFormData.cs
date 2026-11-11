namespace GameLogic.UI;

/// <summary>
/// 技能窗口打开参数，指定默认显示的参战英雄。
/// </summary>
public sealed class SkillFormData
{
    /// <summary>
    /// 指定本次默认显示的英雄，窗口可切换到阵容中的其他英雄。
    /// </summary>
    /// <param name="heroId">默认显示的参战英雄。</param>
    public SkillFormData(int heroId)
    {
        HeroId = heroId;
    }

    /// <summary>
    /// 默认显示的参战英雄编号。
    /// </summary>
    public int HeroId { get; }
}
