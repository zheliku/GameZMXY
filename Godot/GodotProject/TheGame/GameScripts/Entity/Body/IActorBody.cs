using Godot;

namespace GameLogic.Entity.Body
{
	/// <summary>
	/// 身体状态机的公共宿主契约（英雄与怪物共用）：从 <see cref="IHeroBody"/> / <see cref="IMonsterBody"/>
	/// 提取的重合成员。成员全部由 <c>ActorEntity</c>（或其钩子实现）提供——速度、死亡、攻击段、
	/// 受击事实、播放、朝向，以及按动作语义的钩子（出招/收招/受击音/死亡副作用）。
	///
	/// **A 方案（2026-10-01 人类裁决）**：动画 = 纯数据（帧/特效/判定盒值轨道），从不调用代码；
	/// 代码是唯一的逻辑执行者与唯一的时钟——动作时长在 OnInit 时从动画资源**读长度**进参数快照
	/// （<c>*BodyParams</c>），由身体状态用物理 dt 计时；音效由钩子触发、音源 ID 查表。
	/// </summary>
	public interface IActorBody
	{
		/// <summary>速度 px/s（宿主在状态机推进后 MoveAndSlide）</summary>
		Vector2 Velocity { get; set; }

		/// <summary>已死亡（结算置位，死亡状态据此进入）</summary>
		bool Dead { get; }

		/// <summary>重力 px/s²（共享的 ApplyGravity 用）</summary>
		float Gravity { get; }

		/// <summary>正在播的攻击段（0 起；-1 = 不在出招，宿主在 BeginAttack/EndAttack 里维护）</summary>
		int AttackSegment { get; }

		/// <summary>
		/// 提交出招：设攻击段、装填攻击包、播起手音（<c>OwnAttacks[index].SoundId</c>，None = 无）；
		/// 怪物宿主另做转向目标、计入冷却、清移动意图。
		/// </summary>
		void BeginAttack(int index);

		/// <summary>收招/打断：归还攻击包、攻击段归 -1（打断与收招都在攻击状态 Leave）。</summary>
		void EndAttack();

		/// <summary>有待生效的受击（结算登记；身体状态决定何时生效）</summary>
		bool HasPendingHurt { get; }

		/// <summary>取出待生效受击的击退速度（取出即清除）</summary>
		Vector2 TakePendingHurt();

		/// <summary>设置朝向（0 = 不变）</summary>
		void SetFacing(int dir);

		/// <summary>请求播放动画（同名不打断）</summary>
		void PlayAnim(string anim);

		/// <summary>从第 0 帧重播动画</summary>
		void RestartAnim(string anim);

		/// <summary>播放受击语音（受击生效时）</summary>
		void PlayHurtSound();

		/// <summary>死亡生效的副作用：英雄播死亡语音；怪物关受击盒、播死亡音、广播死亡事件</summary>
		void OnDied();
	}
}
