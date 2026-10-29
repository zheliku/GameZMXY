using System;
using Godot;

namespace GameLogic.Heroes.Effects
{
    /// <summary>
    /// 所有英雄共用的墓碑演出，独立拥有下落、落地碰撞和原生动画生命周期。
    /// </summary>
    public partial class HeroTombstoneNode : CharacterBody2D
    {
        private static readonly StringName PlayAnimation = "play"; // 墓碑场景自己的原生动画名。
        [Export] private AnimationPlayer m_AnimationPlayer; // 官方78源帧及末帧保持。
        [Export] private float m_Gravity = 864f; // 下落加速度，单位像素/秒平方。

        /// <summary>
        /// 实际墓碑动画结束；由所属英雄转达关卡，暂停不会提前完成。
        /// </summary>
        public event Action Finished;

        /// <summary>
        /// 绑定自己的播放结束信号，初始隐藏并停止物理推进。
        /// </summary>
        public override void _Ready()
        {
            m_AnimationPlayer.AnimationFinished += OnAnimationFinished;
            Reset();
        }

        /// <summary>
        /// 在死亡世界坐标从头播放；不继承英雄朝向、缩放、胶囊或击退速度。
        /// </summary>
        /// <param name="position">英雄死亡的世界坐标。</param>
        public void ShowAt(Vector2 position)
        {
            GlobalPosition = position;
            Velocity = Vector2.Zero;
            Visible = true;
            SetPhysicsProcess(true);
            m_AnimationPlayer.Stop();
            m_AnimationPlayer.Play(PlayAnimation);
            m_AnimationPlayer.Advance(0);
        }

        /// <summary>
        /// 仅用本场景的脚底碰撞锚点下落，不受玩家战斗门或英雄胶囊影响。
        /// </summary>
        /// <param name="delta">本次物理游戏秒数。</param>
        public override void _PhysicsProcess(double delta)
        {
            Velocity = new Vector2(0, Velocity.Y + m_Gravity * (float)delta);
            MoveAndSlide();
        }

        /// <summary>
        /// 英雄回池或重新显示时停止并隐藏，下一次死亡从同一干净场景开始。
        /// </summary>
        public void Reset()
        {
            m_AnimationPlayer.Stop();
            Velocity = Vector2.Zero;
            Visible = false;
            SetPhysicsProcess(false);
        }

        /// <summary>
        /// 完整时间轴结束后只发出完成通知，关卡在自己的流程帧提交结算。
        /// </summary>
        /// <param name="animation">刚结束的本地动画名。</param>
        private void OnAnimationFinished(StringName animation)
        {
            if (animation == PlayAnimation && Visible)
            {
                SetPhysicsProcess(false);
                Finished?.Invoke();
            }
        }
    }
}
