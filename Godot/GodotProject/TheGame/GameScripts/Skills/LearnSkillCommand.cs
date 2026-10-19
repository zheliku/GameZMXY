using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Level;

namespace GameLogic.Skills;

/// <summary>玩家从技能面板发起学习；检查会话资格，学习与扣费规则仍由技能系统执行。</summary>
/// <param name="skillId">本次选择的技能。</param>
public sealed class LearnSkillCommand(SkillId skillId) : AbstractCommand<SkillLearnResult>
{
    /// <inheritdoc />
    protected override SkillLearnResult OnExecute()
    {
        var level = this.GetModel<LevelModel>();
        return level is { SessionActive: true } && level.Outcome.Value == LevelOutcome.Running
            ? this.GetSystem<SkillSystem>().Learn(skillId)
            : SkillLearnResult.Unavailable;
    }
}
