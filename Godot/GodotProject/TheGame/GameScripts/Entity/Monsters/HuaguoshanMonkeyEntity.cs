using System;
using GameConfig.Battle;
using GameFramework.Fsm;
using GameLogic.Entity.Body;
using GameLogic.Entity.Monsters.AI;
using GameLogic.Entity.Monsters.AI.States;
using GameLogic.Entity.Monsters.Body;
using GodotGameFramework;

namespace GameLogic.Entity.Monsters
{
	/// <summary>
	/// 花果山猴子（旧 Monster_1，普通近战小怪）：直接继承 <see cref="MonsterEntity"/>，
	/// 在本类声明自己这一行为类别的状态集与参数——基类不预设任何具体怪物行为。
	///
	/// 数值与招式来自 MonsterConfig / AttackConfig，动画与判定盒来自场景动画库；
	/// 需要新行为（飞行、远程、精英、Boss）的新怪物继承 <see cref="MonsterEntity"/> 后照本类方式声明。
	/// </summary>
	public partial class HuaguoshanMonkeyEntity : MonsterEntity
	{
		/// <summary>近战范围由攻击动画判定盒推导（AiRange 留作普攻之外的补充距离）。</summary>
		/// <param name="index">招式下标（= AttackSegment）。</param>
		/// <param name="attack">该招的配置行。</param>
		/// <returns>带判定盒范围的 AI 规格。</returns>
		protected override MonsterAttackSpec BuildAttackSpec(int index, AttackConfig attack)
		{
			MonsterAttackSpec spec = base.BuildAttackSpec(index, attack);
			AiBox reach = AttackReachReader.Read(this, attack.Animation);
			if (attack.AiWeight > 0 && reach.IsEmpty && attack.AiRange.Y <= 0f)
			{
				Log.Warning("[HuaguoshanMonkeyEntity] 招式 {0}（动画 {1}）推导不出判定盒、且 AiRange 为 0,0：AI 不会使用这招",
					attack.Id, attack.Animation);
			}

			return spec with { Reach = reach };
		}

		/// <summary>巡逻、出手欲望与站定滞回等参数取自 MonsterConfig。</summary>
		/// <returns>本怪 AI 的参数快照。</returns>
		protected override MonsterAiParams BuildAiParams()
		{
			return new MonsterAiParams
			{
				AttackDesire = Config.AttackDesire,
				AttackInterval = Config.AttackInterval,
				AttackFirstDelay = (Config.AttackFirstDelay.X, Config.AttackFirstDelay.Y),
				PatrolInterval = Config.PatrolInterval,
				PatrolIdleChance = Config.PatrolIdleChance,
				PatrolRadius = Config.PatrolRadius,
				CalmTime = Config.BehitCalmTime,
				AttackRangeSlack = Config.AttackRangeSlack,
				PaceRange = Config.PaceRange,
			};
		}

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

		/// <summary>从地面移动状态起步。</summary>
		protected override Type InitialBodyStateType => typeof(MonsterMoveState);

		/// <summary>本怪 AI 状态：待机 / 游荡 / 接近 / 站定出招 / 守候踱步 / 受控 / 死亡。</summary>
		/// <returns>本怪使用的 AI 状态集。</returns>
		protected override FsmState<IMonsterAiAgent>[] CreateAiStates()
		{
			return
			[
				new PauseState(), new WanderState(), new WalkToTargetState(), new StandAndStrikeState(),
				new PaceBelowTargetState(), new CcLockedState(), new DeathState()
			];
		}

		/// <summary>从游荡起步。</summary>
		protected override Type InitialAiStateType => typeof(WanderState);

		/// <summary>受击硬直中：身体状态机处于受击状态。</summary>
		protected override bool InHurt => m_BodyFsm?.CurrentState is MonsterHurtState;

		/// <summary>收招硬直中：身体状态机处于收招状态。</summary>
		protected override bool InRecovery => m_BodyFsm?.CurrentState is MonsterRecoveryState;
	}
}
