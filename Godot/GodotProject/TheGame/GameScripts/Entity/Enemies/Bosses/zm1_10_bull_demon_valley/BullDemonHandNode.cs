using System;
using GameLogic.Entity;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 最终形态的一只巨掌拥有游荡、加速下压与落地停留，命中编号归首领共享。
    /// </summary>
    public partial class BullDemonHandNode : Node2D
    {
        [Export] private Area2D m_HitBox; // 巨掌少量基础矩形判定，不含场景背景。
        private Action<AbstractActorEntity> m_Hit; // 借用本次首领的共享命中入口。
        private Action m_ResetAttack; // 任一手掌复位时更新父攻击编号。
        private Motion m_Motion; // 当前运动阶段。
        private float m_Left; // 当前游荡或落地停留剩余秒数。
        private float m_DirectionLeft; // 下一次随机折返前秒数。
        private int m_Direction; // 0向左、1向右、2停留，与源规则一致。
        private float m_DownSpeed; // 本轮下压速度，单位px/s。
        private bool m_FirstDescent; // 首轮初速3px/源帧，随后2px/源帧。

        /// <summary>
        /// 节点随首领显示，未开始时保持停止。
        /// </summary>
        public override void _Ready()
        {
            SetPhysicsProcess(false);
        }

        /// <summary>
        /// 重置一只手掌的原点和本次首领回调。
        /// </summary>
        /// <param name="x">相对首领的初始横坐标。</param>
        /// <param name="hit">本次共享命中入口。</param>
        /// <param name="resetAttack">更新共享父攻击编号。</param>
        public void Start(float x, Action<AbstractActorEntity> hit, Action resetAttack)
        {
            m_Hit = hit;
            m_ResetAttack = resetAttack;
            Position = new Vector2(x, 18);
            m_Motion = Motion.Wandering;
            m_Left = GD.RandRange(2, 4);
            m_DirectionLeft = 1f;
            m_Direction = 0;
            m_FirstDescent = true;
            Visible = true;
            SetPhysicsProcess(true);
        }

        /// <summary>
        /// 首领死亡或离场立即停止判定和局部运动，不保留上次回调。
        /// </summary>
        public void Stop()
        {
            SetPhysicsProcess(false);
            m_Hit = null;
            m_ResetAttack = null;
            Visible = false;
        }

        /// <summary>
        /// 秒制还原游荡和加速下压，只有下压阶段进行共享编号的伤害判定。
        /// </summary>
        /// <param name="delta">暂停时不推进的游戏秒数。</param>
        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            if (m_Hit == null || dt <= 0)
            {
                return;
            }

            if (m_Motion == Motion.Wandering)
            {
                float stepX = 0;
                if (m_Direction == 0)
                {
                    stepX = -72 * dt;
                }
                else if (m_Direction == 1)
                {
                    stepX = 72 * dt;
                }
                Position += new Vector2(stepX, 0);
                if (Position.X < -400)
                {
                    m_Direction = 1;
                }
                else if (Position.X > 400)
                {
                    m_Direction = 0;
                }

                m_DirectionLeft -= dt;
                if (m_DirectionLeft <= 0)
                {
                    m_Direction = (m_Direction + (GD.Randf() > .5f ? 2 : 1)) % 3;
                    m_DirectionLeft = GD.RandRange(1, 2);
                }
                m_Left -= dt;
                if (m_Left <= 0)
                {
                    m_Motion = Motion.Descending;
                    m_DownSpeed = (m_FirstDescent ? 3f : 2f) * 24f - 6f;
                }
                return;
            }
            if (m_Motion == Motion.Descending)
            {
                Position += new Vector2(0, m_DownSpeed * dt + 144f * dt * dt);
                m_DownSpeed += 288f * dt;
                foreach (Area2D area in m_HitBox.GetOverlappingAreas())
                {
                    if (area is HurtBoxNode { OwnerEntity: { IsAlive: true } target })
                    {
                        m_Hit(target);
                    }
                }

                if (Position.Y >= 144)
                {
                    m_Motion = Motion.Impact;
                    m_Left = 1f;
                }
                return;
            }
            m_Left -= dt;
            if (m_Left <= 0)
            {
                Position = new Vector2(Position.X, 18);
                m_FirstDescent = false;
                m_Motion = Motion.Wandering;
                m_Left = GD.RandRange(3, 6);
                m_ResetAttack();
            }
        }

        /// <summary>
        /// 互斥运动阶段，避免多个移动标志竞争。
        /// </summary>
        private enum Motion { Wandering, Descending, Impact }
    }
}
