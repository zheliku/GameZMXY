using GameFramework.Fsm;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 官方蝙蝠窟石像：静止受击目标；蝙蝠生成和出口开放由关卡负责。
    /// </summary>
    public partial class BatCaveStatueEntity : AbstractBossEntity
    {
        private static readonly EnemyAiPreset m_Preset = new(typeof(BatCaveStatueIdleState), typeof(BatCaveStatueIdleState),
            () => new BatCaveStatueIdleState(), () => new CcLockedState(), () => new DeathState()); // 石像不移动、不出招。

        /// <inheritdoc />
        protected override EnemyAiPreset AiPreset
        {
            get
            {
                return m_Preset;
            }
        }

        /// <summary>
        /// 石像固定于源坐标，受击不产生位移。
        /// </summary>
        protected override bool IsSuperArmor
        {
            get
            {
                return true;
            }
        }
    }

    /// <summary>
    /// 石像只保留可受伤、可死亡的敌方生命周期，不执行攻击决策。
    /// </summary>
    internal sealed class BatCaveStatueIdleState : AbstractEnemyAiState
    {
        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds) { }
    }
}
