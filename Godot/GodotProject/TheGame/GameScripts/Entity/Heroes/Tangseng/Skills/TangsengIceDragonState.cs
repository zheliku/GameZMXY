using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Heroes.Body;
using GameLogic.Input;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes.Tangseng
{
    /// <summary>
    /// 冰龙波在原生蓄力姿态等待松键，再按蓄力结果发射独立法弹。
    /// </summary>
    public sealed class TangsengIceDragonState : AbstractHeroSkillState
    {
        private const float ChargeSeconds = 30f / 24f; // 官方30源帧蓄力上限。
        private bool m_Releasing; // 松键后进入释放片段。
        private bool m_EffectReleased; // 本次释放方法键已经提交法弹。
        private bool m_FullCharge; // 本次已达到满蓄上限。
        private float m_ChargeTime; // 按游戏秒累计，暂停不增长。
        private int m_MoveAxis; // 空中沿用普攻起手方向，松键后本次不再恢复横移。

        /// <summary>
        /// 本次是否达到官方30源帧蓄力上限。
        /// </summary>
        public bool FullCharge => m_FullCharge;

        /// <inheritdoc />
        protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
        {
            base.Enter(fsm, body);
            m_Releasing = m_EffectReleased = m_FullCharge = false;
            m_ChargeTime = 0f;
            m_MoveAxis = body.ActionData.MoveAxis;
            if (!IsAirborne(body))
            {
                Stand(body);
            }
            BeginAnimation(body, "skills/ice_dragon");
            ((ISkillActor)body).PauseSkillAnimation();
        }

        /// <inheritdoc />
        protected override void UpdateSkill(IHeroBody body, float dt)
        {
            var input = body.GetSystem<InputSystem>();
            if (!input.IsDirectionHeld(body.ActionData, m_MoveAxis) ||
                input.WasDirectionReleased(body.ActionData, m_MoveAxis))
            {
                m_MoveAxis = 0;
            }
            float speed = body.ActionData.Running ? body.Params.RunSpeed : body.Params.WalkSpeed;
            body.Velocity = new Vector2(IsAirborne(body) ? m_MoveAxis * speed * body.MoveSpeedMultiplier : 0,
                body.Velocity.Y);
            if (m_Releasing)
            {
                return;
            }

            if (input.IsActionPressed(InputSystem.Attack))
            {
                m_ChargeTime = Mathf.Min(m_ChargeTime + dt, ChargeSeconds);
                ((TangsengEntity)body).UpdateIceDragonCharge(m_ChargeTime / ChargeSeconds);
                return;
            }

            m_FullCharge = m_ChargeTime >= ChargeSeconds - 0.0001f;
            m_Releasing = true;
            ((TangsengEntity)body).UpdateIceDragonCharge(0);
            BeginAnimation(body, "skills/ice_dragon");
        }

        /// <inheritdoc />
        protected override void Leave(IFsm<IHeroBody> fsm, IHeroBody body, bool isShutdown)
        {
            if (!isShutdown)
            {
                ((TangsengEntity)body).UpdateIceDragonCharge(0);
            }
            base.Leave(fsm, body, isShutdown);
        }

        /// <summary>
        /// 释放方法键按本次蓄力结果扣费并提交唯一法弹。
        /// </summary>
        /// <param name="body">唐僧身体。</param>
        /// <param name="normalScene">未满蓄力的普通法弹。</param>
        /// <param name="chargedScene">满蓄力的冰龙法弹。</param>
        public void Release(IHeroBody body, PackedScene normalScene, PackedScene chargedScene)
        {
            if (m_EffectReleased)
            {
                return;
            }

            m_EffectReleased = true;
            if (m_FullCharge && !body.GetSystem<SkillSystem>().TrySpendActivatedMana(SkillId.IceDragonWave))
            {
                body.GetSystem<SkillSystem>().CancelSkill();
                return;
            }

            ((TangsengEntity)body).SpawnIceDragonBolt(m_FullCharge ? chargedScene : normalScene, m_FullCharge);
        }
    }
}
