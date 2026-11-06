using System;
using Godot;
using GodotGameFramework.NodePool;

namespace GameLogic.Effects
{
    /// <summary>
    /// 怪物受击的官方八帧闪光；多次命中各自播放，由受击实体持有并归还节点池。
    /// </summary>
    public partial class MonsterHitFeedbackNode : AnimatedSprite2D, IPoolable
    {
        [Export] private SpriteFrames m_WukongFrames; // 原始黄色闪光。
        [Export] private SpriteFrames m_TangsengFrames; // 官方Role2的色相+160变体。
        private Action<MonsterHitFeedbackNode> m_Finished; // 当前受击实体的回收入口。

        /// <inheritdoc />
        public override void _Ready()
        {
            AnimationFinished += OnAnimationFinished;
        }

        /// <summary>
        /// 从第一帧播放本次随机闪光，位置由受击实体的受击盒中心提供。
        /// </summary>
        /// <param name="tangseng">是否使用唐僧本体色相；水幻影保留原色。</param>
        /// <param name="position">受击实体内的局部位置。</param>
        /// <param name="finished">本次回收通知。</param>
        public void ShowHit(bool tangseng, Vector2 position, Action<MonsterHitFeedbackNode> finished)
        {
            m_Finished = finished;
            SpriteFrames = tangseng ? m_TangsengFrames : m_WukongFrames;
            Position = position;
            Stop();
            Frame = 0;
            Show();
            Play(GD.Randf() > .5f ? "hit_1" : "hit_2");
        }

        /// <inheritdoc />
        public void OnGet()
        {
        }

        /// <inheritdoc />
        public void OnRelease()
        {
            Stop();
            Hide();
            m_Finished = null;
        }

        /// <summary>
        /// 八个源帧结束后通知持有者，避免回收时留下旧实体引用。
        /// </summary>
        private void OnAnimationFinished()
        {
            m_Finished(this);
        }
    }
}
