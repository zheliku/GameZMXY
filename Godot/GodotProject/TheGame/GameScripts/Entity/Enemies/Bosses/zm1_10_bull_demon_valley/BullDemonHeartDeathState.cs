using GameFramework.Fsm;
using GameConfig.Equipment;
using GameLogic.Architecture;
using GameLogic.Enemies.Body;
using GameLogic.Event.Equipment;
using GameLogic.Profile;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 最终形态等原生两秒退场完成才结束配方，前两形态不能触发这个清关出口。
    /// </summary>
    internal sealed class BullDemonHeartDeathState : AbstractEnemyBodyState
    {
        private float m_Left; // 原生整体退场剩余秒数。
        private bool m_Reported; // 最终收益与清场只报告一次。

        /// <inheritdoc />
        protected override bool HurtInterrupts
        {
            get
            {
                return false;
            }
        }

        /// <inheritdoc />
        protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
        {
            var owner = (BullDemonHeartEntity)body;
            body.MoveIntent = 0;
            body.TakeAttackRequest();
            body.TakePendingHurt();
            body.Velocity = Vector2.Zero;
            owner.BeginDeathPresentation();
            // 原版在退场开始时直接赠入背包；延迟的死亡事件仍留到两秒演出完成，不能提前清关。
            var reward = owner.GetSystem<ProfileSystem>().GrantUniqueEquipment(EquipmentId.DiamondSnare, GD.Randf);
            if (reward != EquipmentGrantResult.AlreadyOwned)
            {
                GF.Event.Fire(owner, EquipmentRewardEventArgs.Create(EquipmentId.DiamondSnare,
                    reward == EquipmentGrantResult.Collected));
            }

            m_Left = owner.DeathDuration;
            m_Reported = false;
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            body.Velocity = Vector2.Zero;
            if (Elapsed(m_Left) && !m_Reported)
            {
                m_Reported = true;
                body.BeginDeath();
                body.RequestRecycle();
            }
            m_Left -= dt;
        }
    }
}
