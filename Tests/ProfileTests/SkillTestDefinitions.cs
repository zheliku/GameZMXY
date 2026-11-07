using GameConfig.Skills;
using GameLogic.Heroes.Wukong;
using GameLogic.Skills;
using GameLogic.Input;

namespace GameLogic.SkillTests
{
    /// <summary>
    /// 纯状态测试的宿主装配；真实实体装配由引擎烟测覆盖。
    /// </summary>
    internal static class SkillTestDefinitions
    {
        /// <summary>
        /// 登记正式状态，替代需要引擎节点的悟空实体。
        /// </summary>
        /// <returns>由正式状态组成的独立登记项数组。</returns>
        internal static SkillDefinition[] Wukong()
        {
            return [
                new(ESkillId.RisingDragon, typeof(WukongRisingDragonState)),
            new(ESkillId.FlameThrust, typeof(WukongFlameThrustState), CanStart: actor => actor.OnFloor),
            new(ESkillId.BlazingFlash, typeof(WukongBlazingFlashState), InputSystem.Skill1),
            new(ESkillId.FlameStorm, typeof(WukongFlameStormState), InputSystem.Skill2),
            new(ESkillId.FireDemonSlash, typeof(WukongFireDemonState), InputSystem.Skill3)];
        }
    }
}
