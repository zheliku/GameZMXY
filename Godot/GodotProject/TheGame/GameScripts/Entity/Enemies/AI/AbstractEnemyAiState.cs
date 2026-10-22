using GameFramework.Fsm;

namespace GameLogic.Enemies.AI
{
    /// <summary>
    /// 敌人 AI 公共入口：死亡与受控优先于小怪和首领各自的决策。
    /// AI 只写移动、朝向与攻击意图；实际动作由 Enemies/Body 身体状态执行。
    /// </summary>
    public abstract class AbstractEnemyAiState : FsmState<IEnemyAiAgent>
    {
        /// <summary>进入时执行框架流程并调用 AI 状态进入钩子。</summary>
        /// <param name="fsm">本敌人实例拥有的 AI 状态机。</param>
        protected internal sealed override void OnEnter(IFsm<IEnemyAiAgent> fsm)
        {
            base.OnEnter(fsm);
            Enter(fsm.Owner);
        }

        /// <summary>按死亡、受控优先级处理打断，再推进 AI 状态决策。</summary>
        /// <param name="fsm">本敌人实例拥有的 AI 状态机。</param>
        /// <param name="elapseSeconds">本帧游戏秒数。</param>
        /// <param name="realElapseSeconds">本帧实际秒数。</param>
        protected internal sealed override void OnUpdate(IFsm<IEnemyAiAgent> fsm, float elapseSeconds,
            float realElapseSeconds)
        {
            IEnemyAiAgent agent = fsm.Owner;
            AbstractEnemyAiState current = fsm.CurrentState as AbstractEnemyAiState;
            if (agent.IsDead)
            {
                if (current is not DeathState)
                {
                    ChangeState<DeathState>(fsm);
                }

                return;
            }

            if (agent.IsCcLocked && current is not CcLockedState)
            {
                ChangeState<CcLockedState>(fsm);
                return;
            }

            Tick(fsm, agent, elapseSeconds);
        }

        /// <summary>进入本状态（默认停步）。</summary>
        /// <param name="agent">提供感知与意图的敌人实例。</param>
        protected virtual void Enter(IEnemyAiAgent agent)
        {
            agent.Move(0);
        }


        /// <summary>未被打断时的本状态决策（elapseSeconds 为逻辑时间）。</summary>
        /// <param name="fsm">本敌人实例拥有的 AI 状态机。</param>
        /// <param name="agent">提供感知与意图的敌人实例。</param>
        /// <param name="elapseSeconds">本帧游戏秒数。</param>
        protected abstract void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds);
    }
}
