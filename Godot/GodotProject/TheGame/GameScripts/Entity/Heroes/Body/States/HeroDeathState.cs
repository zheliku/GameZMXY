using GameLogic.Heroes.Body;
using GameLogic.Body;
using GameFramework.Fsm;
using Godot;

namespace GameLogic.Heroes.Body
{
    /// <summary>
    /// 死亡终态：关闭身体表现和动作，语音由本角色动画播放；墓碑场景独立负责下落和落地。
    /// 复活 / 池复用由实体 OnShow 重建整台状态机完成，不需要"死亡 → 地面"的回拉。
    /// </summary>
    public sealed class HeroDeathState : AbstractHeroBodyState
    {
        /// <summary>
        /// 死亡状态不接受受击打断。
        /// </summary>
        protected override bool HurtInterrupts
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// 进入死亡状态时从头播放死亡动画。
        /// </summary>
        /// <param name="fsm">当前宿主拥有的状态机。</param>
        /// <param name="body">当前状态借用的身体宿主。</param>
        protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
        {
            // 死亡墓碑直接下落，不继承起跳、升龙或致死击退的向上速度。
            body.Velocity = Vector2.Zero;
            body.RestartAnim(HeroAnims.Death);
        }

        /// <summary>
        /// 死亡身体保持静止，不再用英雄胶囊驱动墓碑。
        /// </summary>
        /// <param name="fsm">当前宿主拥有的状态机。</param>
        /// <param name="body">当前状态借用的身体宿主。</param>
        /// <param name="dt">本次物理推进的秒数。</param>
        protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
        {
            body.Velocity = Vector2.Zero;
        }
    }
}
