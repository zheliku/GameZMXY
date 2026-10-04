using GameFramework.Fsm;
using GameLogic.Entity.Body;

namespace GameLogic.Entity.Monsters.Body
{
	/// <summary>
	/// 怪物身体状态基类：集中**打断优先级**（死亡 &gt; 受击 &gt; 本状态决策）。
	/// <code>
	///   Move → Attack                 有 AI 出招请求
	///   Attack → Recovery → Move      招式时长（= 动画长度）到点；AiRecovery &gt; 0 才进 Recovery
	///   任意 → Hurt → Move            受击（**打断出招与收招硬直**，旧项目手感；霸体由宿主不登记受击）
	///   任意 → Death                  死亡（终态；death 动画时长走完请求回收，回收后整台状态机重建）
	/// </code>
	/// </summary>
	public abstract class MonsterBodyState : BodyState<IMonsterBody>
	{
		/// <summary>怪物身体状态名去除此类名前缀。</summary>
		protected override string NamePrefix => "Monster";

		/// <summary>受击是否立即打断本状态（Hurt 自己处理连续受击；Death 不再受击）。</summary>
		protected virtual bool HurtInterrupts => true;

		/// <summary>死亡优先于受击，受击是否打断由当前状态声明。</summary>
		protected sealed override bool Interrupt(IFsm<IMonsterBody> fsm, IMonsterBody body)
		{
			if (body.Dead)
			{
				if (this is MonsterDeathState)
				{
					return false;
				}

				ChangeState<MonsterDeathState>(fsm);
				return true;
			}

			if (HurtInterrupts && body.HasPendingHurt)
			{
				ChangeState<MonsterHurtState>(fsm);
				return true;
			}

			return false;
		}
	}
}
