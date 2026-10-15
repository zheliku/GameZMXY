using GameLogic.Monsters.Body;
using System;
using GameFramework.Fsm;
using Godot;
using GodotGameFramework;

namespace GameLogic.Monsters.Body
{
    /// <summary>
    /// 移动 / 待机：按 AI 的移动意图走动并随之转向（idle / run）；有出招请求就进 Attack。
    /// </summary>
    public sealed class MonsterMoveState : AbstractMonsterBodyState
    {
        /// <summary>优先提交待攻击请求，否则按 AI 意图移动或待机。</summary>
        protected override void Tick(IFsm<IMonsterBody> fsm, IMonsterBody body, float dt)
        {
            int request = body.TakeAttackRequest();
            if (body.OnFloor && request >= 0 && request < body.Params.AttackCount)
            {
                // 切换参数归 FSM 拥有，不提前取得并修改另一个状态实例。
                fsm.SetData(AttackIndexKey, (VarInt32)request);
                ChangeState<MonsterAttackState>(fsm);
                return;
            }

            ApplyGravity(body, dt);
            int move = Math.Clamp(body.MoveIntent, -1, 1);
            body.Velocity = new Vector2(move * body.Params.MoveSpeed, body.Velocity.Y);
            body.SetFacing(move);
            body.PlayAnim(move == 0 ? MonsterAnims.Idle : MonsterAnims.Run);
        }
    }
}
