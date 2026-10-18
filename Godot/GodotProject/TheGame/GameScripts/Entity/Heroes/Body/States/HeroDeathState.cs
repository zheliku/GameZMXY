using GameLogic.Heroes.Body;
using GameLogic.Body;
using GameFramework.Fsm;
using Godot;

namespace GameLogic.Heroes.Body
{
    /// <summary>
    /// 死亡（终态）：从头播死亡动画（语音由动画方法轨道播放），定身、只受重力。
    /// 复活 / 池复用由实体 OnShow 重建整台状态机完成，不需要"死亡 → 地面"的回拉。
    /// </summary>
    public sealed class HeroDeathState : AbstractHeroBodyState
    {
        /// <summary>死亡状态不接受受击打断。</summary>
        protected override bool HurtInterrupts => false;

        /// <summary>进入死亡状态时从头播放死亡动画。</summary>
        protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
        {
            body.RestartAnim(HeroAnims.Death);
        }

        /// <summary>死亡期间施加重力并保持水平定身。</summary>
        protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
        {
            ApplyGravity(body, dt);
            body.Velocity = new Vector2(0f, body.Velocity.Y);
        }
    }
}
