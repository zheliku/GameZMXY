using System;
using GameFramework.Fsm;
using Godot;

namespace GameLogic.Entity.Monsters.Body
{
	/// <summary>
	/// 移动 / 待机：按 AI 的移动意图走动并随之转向（idle / run）；有出招请求就进 Attack。
	/// </summary>
	public sealed class MonsterMoveState : MonsterBodyState
	{
		/// <summary>优先提交待攻击请求，否则按 AI 意图移动或待机。</summary>
		protected override void Tick(IFsm<IMonsterBody> fsm, IMonsterBody body, float dt)
		{
			int request = body.TakeAttackRequest();
			if (request >= 0 && request < body.Params.AttackCount)
			{
				fsm.GetState<MonsterAttackState>().Arm(request);
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
