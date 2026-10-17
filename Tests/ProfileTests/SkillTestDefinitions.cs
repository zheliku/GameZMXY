using GameConfig.Skills;
using GameLogic.Heroes.Wukong;
using GameLogic.Skills;

namespace GameLogic.SkillTests;

/// <summary>纯状态测试的宿主装配；真实实体装配由引擎烟测覆盖。</summary>
internal static class SkillTestDefinitions
{
    /// <summary>登记正式状态，替代需要引擎节点的悟空实体。</summary>
    /// <returns>每个测试独立选用的不可变技能组。</returns>
    internal static SkillSet Wukong() => new(1,
        new(SkillId.RisingDragon, _ => new SkillExecution(SkillId.RisingDragon), BodyState: typeof(WukongRisingDragonState)),
        new(SkillId.FlameThrust, _ => new SkillExecution(SkillId.FlameThrust, actor => actor.OnFloor), BodyState: typeof(WukongFlameThrustState)),
        new(SkillId.BlazingFlash, _ => new SkillExecution(SkillId.BlazingFlash), 1, typeof(WukongBlazingFlashState)),
        new(SkillId.FlameStorm, _ => new SkillExecution(SkillId.FlameStorm), 2, typeof(WukongFlameStormState)),
        new(SkillId.FireDemonSlash, _ => new SkillExecution(SkillId.FireDemonSlash), 3, typeof(WukongFireDemonState)));
}
