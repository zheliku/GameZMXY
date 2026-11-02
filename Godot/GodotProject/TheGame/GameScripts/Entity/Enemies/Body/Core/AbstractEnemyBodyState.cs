using GameLogic.Enemies.Body;
using GameFramework.Fsm;
using GameLogic.Body;
using System;
using Godot;

namespace GameLogic.Enemies.Body
{
    /// <summary>
    /// 怪物身体状态基类：集中**打断优先级**（死亡 &gt; 受击 &gt; 本状态决策）。
    /// <code>
    ///   Move → Attack                 有 AI 出招请求
    ///   Attack → Recovery → Move      招式时长（= 动画长度）到点；AiRecovery &gt; 0 才进 Recovery
    ///   任意 → Hurt → Move            受击（**打断出招与收招硬直**，旧项目手感；霸体由宿主不登记受击）
    ///   任意 → Death                  死亡（终态；death 动画时长走完请求回收，回收后整台状态机重建）
    /// </code>
    /// </summary>
    public abstract class AbstractEnemyBodyState : AbstractBodyState<IEnemyBody>
    {
        /// <summary>
        /// Move 向 Attack 传递的招式下标；目标状态进入后立即消费。
        /// </summary>
        protected const string AttackIndexKey = "Enemy.AttackIndex";

        /// <summary>
        /// Attack 向 Recovery 传递的收招秒数；目标状态进入后立即消费。
        /// </summary>
        protected const string RecoveryDurationKey = "Enemy.RecoveryDuration";

        private const float SourceStepSeconds = 1f / 24f; // 源先位移后加重力的步长，用于秒制积分换算。

        /// <summary>
        /// 受击是否立即打断本状态（Hurt 自己处理连续受击；Death 不再受击）。
        /// </summary>
        protected virtual bool HurtInterrupts
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// 死亡优先于受击，受击是否打断由当前状态声明。
        /// </summary>
        /// <param name="fsm">当前宿主拥有的状态机。</param>
        /// <param name="body">当前状态借用的身体宿主。</param>
        /// <returns>是否已经处理打断，本帧无需继续执行当前状态决策。</returns>
        protected override bool Interrupt(IFsm<IEnemyBody> fsm, IEnemyBody body)
        {
            if (body.Dead)
            {
                if (GetType() == body.DeathStateType)
                {
                    return false;
                }

                ChangeState(fsm, body.DeathStateType);
                return true;
            }

            if (HurtInterrupts && body.HasPendingHurt)
            {
                ChangeState<EnemyHurtState>(fsm);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 受击与尸体共享击退衰减、重力和首次抛飞的积分校正。
        /// </summary>
        /// <param name="body">受击或死亡中的敌人身体。</param>
        /// <param name="dt">物理秒数。</param>
        /// <param name="starting">本次击退是否刚刚生效。</param>
        protected static void ApplyHitMotion(IEnemyBody body, float dt, bool starting)
        {
            if (starting)
            {
                body.Velocity -= new Vector2(0f, body.Gravity * (SourceStepSeconds + dt) * .5f);
            }

            float speed = body.Velocity.X;
            if (MathF.Abs(speed) > body.Params.MoveSpeed)
            {
                float magnitude = MathF.Max(body.Params.MoveSpeed,
                    MathF.Abs(speed) * MathF.Pow(body.Params.KnockbackRetentionPerSecond, dt));
                body.Velocity = new Vector2(MathF.CopySign(magnitude, speed), body.Velocity.Y);
            }
            ApplyGravity(body, dt);
        }

        /// <summary>
        /// 飞行与游动共享双轴积分和朝向/动画，具体状态决定出招时是否保留速度。
        /// </summary>
        /// <param name="body">同时支持垂直意图的身体。</param>
        /// <param name="dt">本步秒数。</param>
        /// <param name="acceleration">垂直加速度。</param>
        /// <param name="speed">垂直速度上限。</param>
        /// <param name="directSpeed">直接上/下行阶段的垂直速度。</param>
        protected static void ApplyFreeMovement(IEnemyFreeMoveBody body, float dt,
            float acceleration, float speed, float directSpeed)
        {
            int vertical = body.VerticalIntent;
            float speedY = 0f;
            if (vertical != 0)
            {
                if (body.DirectVerticalMovement)
                {
                    speedY = vertical * directSpeed;
                }
                else
                {
                    speedY = Mathf.Clamp(body.Velocity.Y + vertical * acceleration * dt, -speed, speed);
                }
            }
            body.Velocity = new Vector2(body.MoveIntent * body.Params.MoveSpeed, speedY);
            body.SetFacing(body.MoveIntent);
            body.PlayAnim(body.MoveIntent == 0 ? EnemyAnims.Idle : EnemyAnims.Run);
        }
    }
}
