using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 造一蝙蝠：声明本物种的飞行参数与决策，空中身体由SkyMonster分支复用。
    /// </summary>
    public partial class BatEntity : AbstractSkyMonsterEntity
    {
        private static readonly EnemyAiPreset m_Preset = new(typeof(BatEngageState), typeof(BatEngageState),
            () => new BatEngageState()); // 蝙蝠专属决策与敌方公共打断。

        /// <inheritdoc />
        protected override EnemyAiPreset AiPreset
        {
            get
            {
                return m_Preset;
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
