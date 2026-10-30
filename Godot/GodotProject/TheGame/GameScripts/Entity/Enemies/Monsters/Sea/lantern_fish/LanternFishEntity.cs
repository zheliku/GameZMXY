using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 灯笼鱼：两百像素追击与近身扑击，嵌套攻击第11源帧更新攻击编号。
    /// </summary>
    public partial class LanternFishEntity : AbstractSeaMonsterEntity
    {
        private static readonly EnemyAiPreset m_Preset = EnemyAiPresets.Patrol; // 每只灯笼鱼独立决策与受控。

        /// <inheritdoc />
        protected override EnemyAiPreset AiPreset
        {
            get
            {
                return m_Preset;
            }
        }
    }
}
