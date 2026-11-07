using GameFramework.Fsm;
using GameLogic.Heroes.Body;

namespace GameLogic.Heroes.Tangseng;

/// <summary>
/// 首次水幻影创建独立分身；再次输入由实体直接换位，不重复扣蓝。
/// </summary>
public sealed class TangsengWaterCloneState : AbstractHeroSkillState
{
    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        base.Enter(fsm, body);
        ((TangsengEntity)body).CreateWaterClone();
        CompleteAnimation();
    }
}
