using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using GodotGameFramework;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 鲨魔王在起手时选择高低生命动作，按所选原生动作的完成标记收招。
    /// </summary>
    internal sealed class SharkDemonKingAttackState : AbstractEnemyBodyState
    {
        private static readonly string[] m_HighAnimations = ["attack_1", "attack_2", "attack_3"]; // 高生命阶段的三招。
        private static readonly string[] m_LowAnimations = ["attack_1_low", "attack_2_low", "attack_3_low"]; // 低生命阶段的三招。
        private int m_Index; // 本次起手固定的攻击下标。
        private float m_Left; // 本次所选动作的剩余秒数。

        /// <inheritdoc />
        protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
        {
            m_Index = fsm.GetData<VarInt32>(AttackIndexKey).Value;
            fsm.RemoveData(AttackIndexKey);
            body.MoveIntent = 0;
            body.BeginAttack(m_Index);
            string[] animations = ((SharkDemonKingEntity)body).LowPhase ? m_LowAnimations : m_HighAnimations;
            var animation = ((SharkDemonKingEntity)body).AnimPlayer.GetAnimation(animations[m_Index]);
            m_Left = (float)(animation.HasMarker("complete") ? animation.GetMarkerTime("complete") : animation.Length);
            body.RestartAnim(animations[m_Index]);
        }

        /// <inheritdoc />
        protected override void Leave(IFsm<IEnemyBody> fsm, IEnemyBody body, bool isShutdown)
        {
            body.MoveIntent = 0;
            body.EndAttack();
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            ApplyGravity(body, dt);
            Stand(body);
            if (!Elapsed(m_Left))
            {
                m_Left -= dt;
                return;
            }
            float recovery = body.Params.AttackRecovery[m_Index];
            if (recovery > 0f)
            {
                fsm.SetData(RecoveryDurationKey, (VarSingle)recovery);
                ChangeState<EnemyRecoveryState>(fsm);
            }
            else
            {
                ChangeState(fsm, body.MovementStateType);
            }
        }
    }
}
