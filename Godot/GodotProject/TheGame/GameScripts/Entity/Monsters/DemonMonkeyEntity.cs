using GameLogic.Monsters.Body;
using GameLogic.Monsters.AI;
using System;
using GameFramework.Fsm;
using GameLogic.Body;

namespace GameLogic.Monsters
{
    /// <summary>
    /// 妖猴（旧 Monster_2，花果山精英近战小怪）：直接继承 <see cref="AbstractMonsterEntity"/>，
    /// 在本类声明自己这一行为类别的状态集与参数——通用 AI 由预设装配，特殊怪物可覆写状态集。
    ///
    /// 数值与招式来自 MonsterConfig / AttackConfig，按官方 Monster2 使用带前摇的普通拳击，
    /// 动画与判定盒来自场景动画库。
    /// </summary>
    public partial class DemonMonkeyEntity : AbstractMonsterEntity
    {
        /// <summary>从地面移动状态起步。</summary>
        protected override Type InitialBodyStateType => typeof(MonsterMoveState);

        /// <summary>沿用造一普通地面怪的巡逻追击预设。</summary>
        protected override MonsterAiPreset AiPreset => MonsterAiPreset.Patrol;

        /// <summary>受击硬直中：身体状态机处于受击状态。</summary>
        protected override bool InHurt => m_BodyFsm?.CurrentState is MonsterHurtState;

        /// <summary>收招硬直中：身体状态机处于收招状态。</summary>
        protected override bool InRecovery => m_BodyFsm?.CurrentState is MonsterRecoveryState;

        /// <summary>本怪身体状态：地面移动 / 出招 / 收招硬直 / 受击 / 死亡。</summary>
        /// <returns>本怪使用的身体状态集。</returns>
        protected override FsmState<IMonsterBody>[] CreateBodyStates()
        {
            return
            [
                new MonsterMoveState(), new MonsterAttackState(), new MonsterRecoveryState(), new MonsterHurtState(),
                new MonsterDeathState()
            ];
        }
    }
}
