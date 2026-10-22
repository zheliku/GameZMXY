using GameLogic.Enemies.Body;
using GameLogic.Heroes.Body;
using Godot;

namespace GameLogic.Body
{
    /// <summary>英雄与怪物身体状态共用的物理、动作及受击能力；声音时点由动画方法轨道表达。</summary>
    public interface IActorBody
    {
        /// <summary>上一次物理移动是否站在地面，供起跳、落地与地面出招决策共用。</summary>
        bool OnFloor { get; }

        /// <summary>速度 px/s（宿主在状态机推进后 MoveAndSlide）</summary>
        Vector2 Velocity { get; set; }

        /// <summary>已死亡（结算置位，死亡状态据此进入）</summary>
        bool Dead { get; }

        /// <summary>重力 px/s²（共享的 ApplyGravity 用）</summary>
        float Gravity { get; }

        /// <summary>正在播的攻击段（0 起；-1 = 不在出招，宿主在 BeginAttack/EndAttack 里维护）</summary>
        int AttackSegment { get; }

        /// <summary>有待生效的受击（结算登记；身体状态决定何时生效）</summary>
        bool HasPendingHurt { get; }

        /// <summary>
        /// 提交出招：设攻击段并装填攻击包；起手音由动画方法轨道控制。
        /// 怪物宿主另做转向目标、计入冷却、清移动意图。
        /// </summary>
        void BeginAttack(int index);

        /// <summary>收招/打断：归还攻击包、攻击段归 -1（打断与收招都在攻击状态 Leave）。</summary>
        void EndAttack();

        /// <summary>取出待生效受击的击退速度（取出即清除）</summary>
        Vector2 TakePendingHurt();

        /// <summary>设置朝向（0 = 不变）</summary>
        void SetFacing(int dir);

        /// <summary>请求播放动画（同名不打断）</summary>
        void PlayAnim(string anim);

        /// <summary>从第 0 帧重播动画</summary>
        void RestartAnim(string anim);

    }
}
