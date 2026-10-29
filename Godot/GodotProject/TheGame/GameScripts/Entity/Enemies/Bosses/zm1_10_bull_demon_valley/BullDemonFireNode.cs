using System;
using GameLogic.Entity;
using Godot;
using GodotGameFramework.NodePool;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 最终形态火柱只拥有原生播放与判定窗口，实际命中沿用首领当前共享攻击编号。
    /// </summary>
    public partial class BullDemonFireNode : Node2D, IPoolable
    {
        [Export] private AnimationPlayer m_Player; // 40帧素材在原版第36帧销毁。
        [Export] private Area2D m_HitBox; // 原生动画控制启闭和阶段矩形。
        [Export] private CollisionShape2D m_Shape; // 只在第25～35源帧启用。
        private Action<AbstractActorEntity> m_Hit; // 本次首领共享命中入口，不创建独立攻击包。
        private Action<BullDemonFireNode> m_Finished; // 所有者归还本次节点。
        private bool m_Completed; // 方法轨道中只登记结束，正常物理帧再回池。

        /// <summary>
        /// 播放器用物理帧手动推进，避免方法轨道中释放节点。
        /// </summary>
        public override void _Ready()
        {
            m_Player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            m_Player.CallbackModeMethod = AnimationMixer.AnimationCallbackModeMethod.Immediate;
            SetPhysicsProcess(false);
        }

        /// <inheritdoc />
        public void OnGet()
        {
            SetPhysicsProcess(false);
        }

        /// <inheritdoc />
        public void OnRelease()
        {
            SetPhysicsProcess(false);
            m_Player.Stop();
            m_Hit = null;
            m_Finished = null;
            m_Shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
            Visible = false;
        }

        /// <summary>
        /// 按官方世界位置显示一根火柱，所有权和命中去重仍归首领。
        /// </summary>
        /// <param name="position">世界出生位置。</param>
        /// <param name="hit">本次共享命中入口。</param>
        /// <param name="finished">归还节点的完成回调。</param>
        public void Show(Vector2 position, Action<AbstractActorEntity> hit, Action<BullDemonFireNode> finished)
        {
            GlobalPosition = position;
            m_Hit = hit;
            m_Finished = finished;
            m_Completed = false;
            m_Player.Stop();
            m_Player.Play("play");
            m_Player.Advance(0);
            Visible = true;
            SetPhysicsProcess(true);
        }

        /// <summary>
        /// 原生时间轴推进后读取有效重叠，不按整张火焰图集的画布命中。
        /// </summary>
        /// <param name="delta">本次游戏秒数。</param>
        public override void _PhysicsProcess(double delta)
        {
            if (m_Hit == null || delta <= 0)
            {
                return;
            }

            m_Player.Advance(delta);
            if (m_Completed)
            {
                m_Finished(this);
                return;
            }
            if (!m_Shape.Disabled)
            {
                foreach (Area2D area in m_HitBox.GetOverlappingAreas())
                {
                    if (area is HurtBoxNode { OwnerEntity: { IsAlive: true } target })
                    {
                        m_Hit(target);
                    }
                }
            }
        }

        /// <summary>
        /// 原生第36源帧请求销毁，不在播放器遍历期间回池。
        /// </summary>
        private void FinishAnimationAnimEvent()
        {
            m_Completed = true;
        }
    }
}
