using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using GodotGameFramework;

namespace GameLogic.Enemies.Body;

/// <summary>
/// 出招：进入时提交（宿主 BeginAttack：转向目标、计冷却、装填攻击包、播起手音）、从第 0 帧播本招动画；
/// 定身、朝向锁定。**招式时长 = 招式动画长度**（OnInit 读进 Params，状态计时）：
/// 到点后本招 AiRecovery &gt; 0 进 Recovery 收招硬直，否则回 Move。
/// 受击打断（霸体由宿主不登记受击）；离开时归还攻击包。
/// </summary>
public sealed class EnemyAttackState : AbstractEnemyBodyState
{
    private int _index; // 当前攻击配置在身体共享身体定义中的下标。

    private float _left; // 当前招式剩余时长（秒）。

    /// <summary>
    /// 提交招式、锁定移动并播放对应攻击动画。
    /// </summary>
    /// <param name="fsm">当前宿主拥有的状态机。</param>
    /// <param name="body">当前状态借用的身体宿主。</param>
    protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
    {
        // 读取后归还池化切换参数，本状态自己初始化动作。
        _index = fsm.GetData<VarInt32>(AttackIndexKey).Value;
        fsm.RemoveData(AttackIndexKey);
        body.MoveIntent = 0;
        body.BeginAttack(_index);
        _left = body.Params.AttackTimes[_index];
        body.RestartAnim(body.Params.AttackAnims[_index]);
    }

    /// <summary>
    /// 离开攻击状态时清除移动意图并归还攻击包。
    /// </summary>
    /// <param name="fsm">当前宿主拥有的状态机。</param>
    /// <param name="body">当前状态借用的身体宿主。</param>
    /// <param name="isShutdown">是否因所属服务或状态机关闭而离开。</param>
    protected override void Leave(IFsm<IEnemyBody> fsm, IEnemyBody body, bool isShutdown)
    {
        // 收招、受击打断、死亡、实体隐藏都走这里：动画被切走，攻击包在此归还
        body.MoveIntent = 0;
        body.EndAttack();
    }

    /// <summary>
    /// 推进招式计时，到点后进入收招或移动状态。
    /// </summary>
    /// <param name="fsm">当前宿主拥有的状态机。</param>
    /// <param name="body">当前状态借用的身体宿主。</param>
    /// <param name="dt">本次物理推进的秒数。</param>
    protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
    {
        ApplyGravity(body, dt);
        Stand(body);

        if (Elapsed(_left))
        {
            // 招式结束后先进入配置的收招硬直；没有硬直则立即恢复移动。
            float recovery = body.Params.AttackRecovery[_index];
            if (recovery > 0f)
            {
                fsm.SetData(RecoveryDurationKey, (VarSingle)recovery);
                ChangeState<EnemyRecoveryState>(fsm);
                return;
            }

            ChangeState(fsm, body.MovementStateType);
            return;
        }

        _left -= dt;
    }
}
