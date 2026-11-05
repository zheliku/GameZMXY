using GameLogic.Battle.Damage;
using GameLogic.Effects;
using GameLogic.Entity;
using GameLogic.Enemies;
using Godot;
using GodotGameFramework;

namespace GameLogic.Heroes.Tangseng
{
    /// <summary>
    /// 唐僧的直线法弹，复用独立效果的攻击预算、暂停和回池生命周期。
    /// </summary>
    public partial class TangsengBoltNode : SkillEffectNode
    {
        [Export] private float m_Speed = 192f; // 场景指定的官方速度，像素每秒。
        [Export] private float m_TravelDistance = 304f; // 普攻源距离300，8px源步在第38次移动后销毁。
        [Export] private float m_SplashRadius; // 普攻在主命中附近的官方溅射半径；冰龙波为零。
        private float m_DistanceLeft; // 本次弹体仍可移动的距离，不写回共享场景。

        /// <inheritdoc />
        public override void OnGet()
        {
            base.OnGet();
            m_DistanceLeft = m_TravelDistance;
        }

        /// <summary>
        /// 按本次蓄力结果配置剩余射程，不改写场景默认值。
        /// </summary>
        /// <param name="distance">最大射程，像素。</param>
        public void ConfigureTravel(float distance)
        {
            m_DistanceLeft = distance;
        }

        /// <inheritdoc />
        public override void _PhysicsProcess(double delta)
        {
            if (!IsActive || delta <= 0)
            {
                return;
            }

            // 先完成本帧位移并登记过期，再交给基类推进判定和回池；基类可能在本帧释放对象，
            // 因此派生逻辑不能在 base 返回后继续读取位置或距离。
            float distance = Mathf.Min(m_DistanceLeft, m_Speed * (float)delta);
            Position += Vector2.Right * (Scale.X > 0 ? -1 : 1) * distance;
            m_DistanceLeft -= distance;
            if (m_DistanceLeft <= 0)
            {
                RequestFinish();
            }
            base._PhysicsProcess(delta);
        }

        /// <inheritdoc />
        protected override void OnHitLanded(AbstractActorEntity target, DamageResult result)
        {
            if (m_SplashRadius <= 0)
            {
                return;
            }
            // 源attackRange只扩散这次主命中，不递归触发或消费额外弹体预算。
            foreach (var group in GF.Entity.GetAllEntityGroups())
            {
                foreach (var actor in group.GetAllEntities())
                {
                    if (actor is AbstractEnemyEntity enemy &&
                        target.GlobalPosition.DistanceTo(enemy.GlobalPosition) <= m_SplashRadius)
                    {
                        ApplySplashHit(enemy);
                    }
                }
            }
        }
    }
}
