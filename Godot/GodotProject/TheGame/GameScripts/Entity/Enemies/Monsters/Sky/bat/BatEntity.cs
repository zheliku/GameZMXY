using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 造一蝙蝠：声明本物种的飞行参数与决策，空中身体由SkyMonster分支复用。
    /// </summary>
    public partial class BatEntity : AbstractSkyMonsterEntity
    {
        private EnemyAiPreset m_Preset; // 共用追踪规则，注入蝙蝠的90px悬停高度和本场景追踪范围。

        /// <inheritdoc />
        protected override EnemyAiPreset AiPreset
        {
            get
            {
                return m_Preset ??= new(typeof(FlyingPursuitState), typeof(FlyingPursuitState),
                    () => new FlyingPursuitState(pursuitHeight: 90f, trackingHeight: TrackingHeight,
                        altitude: () => Altitude));
            }
        }

        /// <summary>
        /// 原版追踪每源帧加减0.5，换算为px/s²。
        /// </summary>
        protected override float VerticalAcceleration
        {
            get
            {
                return 288f;
            }
        }

        /// <summary>
        /// 原版追踪竖速上限4px/frame，换算为px/s。
        /// </summary>
        protected override float VerticalSpeed
        {
            get
            {
                return 96f;
            }
        }
    }
}
