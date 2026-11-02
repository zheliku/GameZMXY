using GameFramework.Fsm;
using GameLogic.Enemies.Body;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 彭魔王身体的真实优先级：最终死亡、首次耗尽化卵、普通受击、当前招式。
    /// </summary>
    internal abstract class PengDemonKingBodyState : AbstractEnemyBodyState
    {
        /// <inheritdoc />
        protected override bool Interrupt(IFsm<IEnemyBody> fsm, IEnemyBody body)
        {
            if (body.Dead)
            {
                ChangeState(fsm, body.DeathStateType);
                return true;
            }
            if (((PengDemonKingEntity)body).IsEgg)
            {
                if (this is not PengDemonKingEggState)
                {
                    ChangeState<PengDemonKingEggState>(fsm);
                    return true;
                }
                return false;
            }
            if (HurtInterrupts && body.HasPendingHurt)
            {
                ChangeState<PengDemonKingHurtState>(fsm);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 本首领独有的原生动画名，不向所有敌人添加凤凰动作。
    /// </summary>
    internal static class PengDemonKingAnims
    {
        internal const string Fly = "fly"; // 空中持续动作。
        internal const string Egg = "egg"; // 化卵后停在最后一帧。
        internal const string Rebirth = "rebirth"; // 回满生命后的复生动作。
    }
}
