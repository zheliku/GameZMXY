using GameLogic.Enemies;
using GameFramework.Fsm;

namespace GameLogic.Enemies.AI
{
    /// <summary>
    /// 终态：停止一切意图。死亡动画与回收由 AbstractEnemyEntity 负责，实体隐藏时状态机随之销毁。
    /// </summary>
    public class DeathState : AbstractEnemyAiState
    {
        /// <summary>
        /// 死亡 AI 状态不再产生意图。
        /// </summary>
        /// <param name="fsm">当前宿主拥有的状态机。</param>
        /// <param name="agent">当前状态借用的敌人AI宿主。</param>
        /// <param name="elapseSeconds">逻辑流逝时间，单位为秒。</param>
        protected override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
        {
        }
    }
}
