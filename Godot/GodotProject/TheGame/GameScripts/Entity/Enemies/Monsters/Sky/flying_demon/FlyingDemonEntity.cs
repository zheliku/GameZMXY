using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 造一飞行魔：使用空中身体，保留自己的高度追踪、延迟折返和双段羽刃攻击。
    /// </summary>
    public partial class FlyingDemonEntity : AbstractSkyMonsterEntity
    {
        private static readonly EnemyAiPreset m_Preset = new(typeof(FlyingDemonEngageState), typeof(FlyingDemonEngageState),
            () => new FlyingDemonEngageState()); // 每次显示创建独立的追踪状态。

        /// <inheritdoc />
        protected override EnemyAiPreset AiPreset
        {
            get
            {
                return m_Preset;
            }
        }

        /// <inheritdoc />
        protected override float VerticalAcceleration
        {
            get
            {
                return 288f;
            }
        }

        /// <inheritdoc />
        protected override float VerticalSpeed
        {
            get
            {
                return 96f;
            }
        }
    }
}
