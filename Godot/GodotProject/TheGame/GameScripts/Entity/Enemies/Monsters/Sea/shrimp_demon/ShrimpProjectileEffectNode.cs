using GameLogic.Effects;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 虾兵魔法弹：按源加速并在1000px距离内飞行，被玩家攻击碰到后停伤并淡出一秒。
    /// </summary>
    public partial class ShrimpProjectileEffectNode : SkillEffectNode
    {
        private const float InitialSpeed = 96f; // 源setSpeed(4)按24fps换算。
        private const float Acceleration = 230.4f; // 源每帧增加0.4，换算为像素每秒平方。
        private const float RightSpeedLimit = 172.8f; // 源speed<7后最后一次增加到7.2；向左保留原符号判断。
        private const float MaxDistance = 1000f; // 官方弹体飞行距离。
        private const float FadeDuration = 1f; // 官方TweenMax一秒消散。

        [Export] private Area2D m_CancelArea; // 只监听玩家攻击层，不充当角色受击体。
        private float m_Speed; // 当前绝对速度。
        private float m_Distance; // 本次显示已经飞过的距离。
        private float m_FadeLeft; // 大于零表示已被击散，期间不再运动或伤害。

        /// <inheritdoc />
        public override void _Ready()
        {
            base._Ready();
            m_CancelArea.AreaEntered += OnPlayerAttackEntered;
        }

        /// <inheritdoc />
        public override void OnGet()
        {
            base.OnGet();
            m_Speed = InitialSpeed;
            m_Distance = 0f;
            m_FadeLeft = 0f;
            Modulate = Colors.White;
        }

        /// <inheritdoc />
        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            if (m_FadeLeft > 0f)
            {
                m_FadeLeft = Mathf.Max(0f, m_FadeLeft - dt);
                float alpha = m_FadeLeft / FadeDuration;
                Modulate = new Color(1f, 1f, 1f, alpha * alpha);
                if (m_FadeLeft <= 0f)
                {
                    FinishAnimationAnimEvent();
                }
            }
            else
            {
                int direction = Scale.X > 0f ? -1 : 1;
                float next = m_Speed + Acceleration * dt;
                if (direction > 0)
                {
                    next = Mathf.Min(next, RightSpeedLimit);
                }

                float distance = (m_Speed + next) * .5f * dt;
                Position += Vector2.Right * direction * distance;
                m_Distance += distance;
                m_Speed = next;
                if (m_Distance >= MaxDistance)
                {
                    FinishAnimationAnimEvent();
                }

                foreach (Area2D area in m_CancelArea.GetOverlappingAreas())
                {
                    OnPlayerAttackEntered(area);
                }
            }
            base._PhysicsProcess(delta);
        }

        /// <summary>
        /// 玩家攻击碰到弹体时清除伤害，只保留原地消散。
        /// </summary>
        /// <param name="area">进入取消区的玩家攻击区域。</param>
        private void OnPlayerAttackEntered(Area2D area)
        {
            if (m_FadeLeft > 0f)
            {
                return;
            }

            StopAttack();
            m_FadeLeft = FadeDuration;
        }
    }
}
