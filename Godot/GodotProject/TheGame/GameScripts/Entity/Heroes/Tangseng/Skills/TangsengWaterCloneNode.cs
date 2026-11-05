using System;
using Godot;

namespace GameLogic.Heroes.Tangseng
{
    /// <summary>
    /// 官方 Role2Shadow 水幻影的移动、动作帧和 192 源帧生命周期。
    /// </summary>
    public partial class TangsengWaterCloneNode : Node2D
    {
        private const string WalkAnimation = "walk"; // 原生行进动作。
        private const string DarkIceAnimation = "dark_ice"; // 分身玄冰阵动作。
        private const string HeavenlyDewAnimation = "heavenly_dew"; // 分身甘露动作。
        private const float MoveSpeed = 8f * 24f; // 官方8像素/源帧换算为像素/秒。
        private const float LifetimeSeconds = 192f / 24f; // 官方192源帧寿命。

        [Export] private AnimationPlayer m_Player; // 原生动画唯一拥有帧序和施法时点。

        private float m_Lifetime; // 本次经过的游戏秒数。
        private int m_Facing; // 创建时的行进方向。
        private Action m_Cast; // 当前辅助施法的一次回调。
        private bool m_CastRequested, m_FinishRequested; // 原生方法键只登记事实，播放器返回后执行。

        /// <summary>
        /// 分身当前仍存在。
        /// </summary>
        public bool IsActive => Visible;

        /// <summary>
        /// 只有仍在行进的分身能换位或接受一次辅助施法。
        /// </summary>
        public bool IsAvailable => IsActive && m_Player.CurrentAnimation == WalkAnimation;

        /// <summary>
        /// 分身保留创建时的移动与施法朝向。
        /// </summary>
        public int Facing => m_Facing;

        /// <inheritdoc />
        public override void _Ready()
        {
            m_Player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            m_Player.CallbackModeMethod = AnimationMixer.AnimationCallbackModeMethod.Immediate;
            Dismiss();
        }

        /// <summary>
        /// 以当前世界坐标和朝向创建官方分身。
        /// </summary>
        /// <param name="position">本体世界位置。</param>
        /// <param name="facing">本次移动朝向。</param>
        public void Activate(Vector2 position, int facing)
        {
            GlobalPosition = position;
            m_Facing = facing < 0 ? -1 : 1;
            Scale = new Vector2(-m_Facing, 1);
            m_Lifetime = 0;
            m_Cast = null;
            BeginAnimation(WalkAnimation);
            Show();
            SetPhysicsProcess(true);
        }

        /// <summary>
        /// 隐藏分身并清除本次回调，供换位、过期和英雄离场共同使用。
        /// </summary>
        public void Dismiss()
        {
            Hide();
            m_Cast = null;
            m_CastRequested = m_FinishRequested = false;
            m_Player.Stop();
            SetPhysicsProcess(false);
        }

        /// <summary>
        /// 播放官方 Shadow hit3，命中发生在第15源帧，末帧销毁。
        /// </summary>
        /// <param name="impact">第15源帧提交独立玄冰阵。</param>
        public void PlayDarkIce(Action impact)
        {
            if (!IsAvailable)
            {
                return;
            }

            m_Cast = impact;
            BeginAnimation(DarkIceAnimation);
        }

        /// <summary>
        /// 播放官方 Shadow hit5，治疗发生在第50源帧，末帧销毁。
        /// </summary>
        /// <param name="heal">第50源帧检查范围并治疗。</param>
        public void PlayHeavenlyDew(Action heal)
        {
            if (!IsAvailable)
            {
                return;
            }

            m_Cast = heal;
            BeginAnimation(HeavenlyDewAnimation);
        }

        /// <inheritdoc />
        public override void _PhysicsProcess(double delta)
        {
            if (!IsActive || delta <= 0)
            {
                return;
            }

            float dt = (float)delta;
            m_Lifetime += dt;
            if (m_Lifetime >= LifetimeSeconds)
            {
                Dismiss();
                return;
            }

            if (m_Player.CurrentAnimation == WalkAnimation)
            {
                GlobalPosition += new Vector2(MoveSpeed * m_Facing * dt, 0);
            }

            // 与英雄技能相同：遍历轨道时不换动画、不隐藏或调用外部业务。
            m_Player.Advance(delta);
            if (m_CastRequested)
            {
                m_CastRequested = false;
                Action cast = m_Cast;
                m_Cast = null;
                cast();
            }

            if (m_FinishRequested)
            {
                Dismiss();
            }
        }

        /// <summary>
        /// 从头播放一个原生动作，时点与帧号不在代码维护。
        /// </summary>
        /// <param name="animation">分身动作名。</param>
        private void BeginAnimation(string animation)
        {
            m_CastRequested = m_FinishRequested = false;
            m_Player.Stop();
            m_Player.Play(animation);
            m_Player.Advance(0);
        }

        /// <summary>
        /// 原生出手键登记一次辅助施法。
        /// </summary>
        private void CastAnimEvent()
        {
            m_CastRequested = true;
        }

        /// <summary>
        /// 原生末帧登记结束，甘露同帧先治疗再结束。
        /// </summary>
        private void FinishAnimEvent()
        {
            m_CastRequested = m_Cast != null;
            m_FinishRequested = true;
        }
    }
}
