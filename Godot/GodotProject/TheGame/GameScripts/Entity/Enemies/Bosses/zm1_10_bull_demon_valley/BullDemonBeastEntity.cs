using System;
using GameConfig.Entity;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 牛魔王兽形：高速冲撞时无敌，在相机边缘停步暴露后折返，死亡转入人形。
    /// </summary>
    public partial class BullDemonBeastEntity : AbstractBullDemonFormEntity
    {
        private static readonly EnemyBodyPreset m_BodyPreset = new(typeof(BullDemonChargeState),
            typeof(BullDemonTransformState),
            () => new BullDemonChargeState(), () => new BullDemonTransformState()); // 本首领的状态组合；运行实例由每次入场独立创建。
        private static readonly EnemyAiPreset m_Preset = new(typeof(BullDemonChargeAiState), typeof(BullDemonChargeAiState),
            () => new BullDemonChargeAiState(), () => new CcLockedState(), () => new DeathState()); // 独立冲撞和边界停步时钟。

        /// <inheritdoc />
        protected override EnemyBodyPreset BodyPreset
        {
            get
            {
                return m_BodyPreset;
            }
        }

        /// <inheritdoc />
        protected override EnemyAiPreset AiPreset
        {
            get
            {
                return m_Preset;
            }
        }

        /// <inheritdoc />
        protected override EntityId NextForm
        {
            get
            {
                return EntityId.BullDemonKing;
            }
        }

        /// <inheritdoc />
        protected override Vector2 NextFormPosition
        {
            get
            {
                return new Vector2(1500, 450);
            }
        }

        /// <inheritdoc />
        protected override bool Invulnerable
        {
            get
            {
                return AttackSegment >= 0;
            }
        }

        /// <inheritdoc />
        protected override void FaceAttackTarget(int index)
        {
            SetFacing(((IEnemyBody)this).MoveIntent);
        }

        /// <summary>
        /// 原版第25源帧完成兽形闪烁，再由下一身体帧提交人形。
        /// </summary>
        private void CompleteTransformAnimEvent()
        {
            if (m_BodyFsm.CurrentState is BullDemonTransformState state)
            {
                state.CompleteAnimation();
            }
        }
    }
}
