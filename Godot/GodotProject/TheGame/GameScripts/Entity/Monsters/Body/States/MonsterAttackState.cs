using GameLogic.Monsters.Body;
using GameFramework.Fsm;
using GodotGameFramework;

namespace GameLogic.Monsters.Body
{
    /// <summary>
    /// 出招：进入时提交（宿主 BeginAttack：转向目标、计冷却、装填攻击包、播起手音）、从第 0 帧播本招动画；
    /// 定身、朝向锁定。**招式时长 = 招式动画长度**（OnInit 读进 Params，状态计时）：
    /// 到点后本招 AiRecovery &gt; 0 进 Recovery 收招硬直，否则回 Move。
    /// 受击打断（霸体由宿主不登记受击）；离开时归还攻击包。
    /// </summary>
    public sealed class MonsterAttackState : AbstractMonsterBodyState
    {
        private int m_Index; // 当前攻击配置在身体共享身体定义中的下标。

        private float m_Left; // 当前招式剩余时长（秒）。

        /// <summary>提交招式、锁定移动并播放对应攻击动画。</summary>
        protected override void Enter(IFsm<IMonsterBody> fsm, IMonsterBody body)
        {
            // 读取后归还池化切换参数，本状态自己初始化动作。
            m_Index = fsm.GetData<VarInt32>(AttackIndexKey).Value;
            fsm.RemoveData(AttackIndexKey);
            body.MoveIntent = 0;
            body.BeginAttack(m_Index);
            m_Left = body.Params.AttackTimes[m_Index];
            body.RestartAnim(body.Params.AttackAnims[m_Index]);
        }

        /// <summary>离开攻击状态时清除移动意图并归还攻击包。</summary>
        protected override void Leave(IFsm<IMonsterBody> fsm, IMonsterBody body, bool isShutdown)
        {
            // 收招、受击打断、死亡、实体隐藏都走这里：动画被切走，攻击包在此归还
            body.MoveIntent = 0;
            body.EndAttack();
        }

        /// <summary>推进招式计时，到点后进入收招或移动状态。</summary>
        protected override void Tick(IFsm<IMonsterBody> fsm, IMonsterBody body, float dt)
        {
            ApplyGravity(body, dt);
            Stand(body);

            if (Elapsed(m_Left))
            {
                // 招式结束后先进入配置的收招硬直；没有硬直则立即恢复移动。
                float recovery = body.Params.AttackRecovery[m_Index];
                if (recovery > 0f)
                {
                    fsm.SetData(RecoveryDurationKey, (VarSingle)recovery);
                    ChangeState<MonsterRecoveryState>(fsm);
                    return;
                }

                ChangeState<MonsterMoveState>(fsm);
                return;
            }

            m_Left -= dt;
        }
    }
}
