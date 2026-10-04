using GameFramework.Fsm;
using Godot;

namespace GameLogic.Entity.Body
{
	/// <summary>
	/// 身体动作状态基类（英雄与怪物共用）：GF.Fsm 的状态，但**按物理帧驱动**。
	///
	/// 为什么不走框架帧：GF.Fsm 由 GameEntry 在 _Process（渲染帧）里轮询，而身体状态要和
	/// MoveAndSlide 同步。框架开放了 <c>IFsm.CurrentState</c>（public）与 <c>FsmState.ChangeState</c>（protected），
	/// 所以这里把框架帧的 OnUpdate 封成空实现，由实体在 _PhysicsProcess 里调用
	/// <see cref="BodyFsm.Tick{T}"/>——状态登记、进入/离开生命周期、切换全用框架自带的，框架零改动。
	///
	/// 约定（A 方案：动画纯数据、代码唯一时钟）：
	///  * 状态只经宿主接口 <typeparamref name="TBody"/> 读写事实、请求播放动画，不碰节点 / GD.* / GF.*（纯 C#，可单测）；
	///  * **状态机单入口**：只有 Tick 一条路进状态机——物理/结算事实（受击、死亡、输入、出招请求）
	///    都写进宿主、状态在 Tick 里自己消费；动画从不调用代码；
	///  * 动作时长 = OnInit 从动画资源读的长度（参数快照），状态用物理 dt 自己计时（<see cref="Elapsed"/> 判到点）；
	///    **禁止用 <c>fsm.CurrentStateTime</c>**（按渲染帧累计）；
	///  * 打断优先级集中在 <see cref="Interrupt"/>（英雄/怪物各自的基类实现），派生状态只写本状态决策；
	///  * Tick 先判断要不要切走（切走就 return，新状态当帧接着执行），再做本状态的移动/动画。
	/// </summary>
	public abstract class BodyState<TBody> : FsmState<TBody> where TBody : class, IActorBody
	{
		/// <summary>缓存去前缀后的状态名。</summary>
		private string m_StateName;

		/// <summary>状态名（调试/冒烟观测）：类名去掉 <see cref="NamePrefix"/> 与 "State" 后缀，如 HeroGroundState → Ground。</summary>
		public string StateName => m_StateName ??= TrimName(GetType().Name, NamePrefix);

		/// <summary>状态类名前缀（英雄 "Hero"、怪物 "Monster"，避免与 AI 行为类重名）。</summary>
		protected virtual string NamePrefix => "";

		/// <summary>进入次数（BodyFsm 据此识别"切回自己"的重新进入）。</summary>
		internal int EnterSerial { get; private set; }

		/// <summary>
		/// 计时到点的容差（工程常数）：按物理 dt 累减有浮点误差（0.2s 在 60Hz 下减 12 次可能剩 1e-8），
		/// 不加容差会多拖一帧。状态计时一律用 <see cref="Elapsed"/> 判到点。
		/// </summary>
		protected const float TimeEpsilon = 1e-4f;

		/// <summary>剩余时间是否已到点（含浮点容差）。</summary>
		protected static bool Elapsed(float left)
		{
			return left <= TimeEpsilon;
		}

		/// <summary>框架帧轮询：身体状态不在渲染帧做任何事（见类注释）。</summary>
		protected internal sealed override void OnUpdate(IFsm<TBody> fsm, float elapseSeconds, float realElapseSeconds)
		{
		}

		/// <summary>进入时递增进入序号并调用身体状态钩子。</summary>
		protected internal sealed override void OnEnter(IFsm<TBody> fsm)
		{
			base.OnEnter(fsm);
			EnterSerial++;
			Enter(fsm, fsm.Owner);
		}

		/// <summary>调用身体状态离开钩子并完成框架离开流程。</summary>
		protected internal sealed override void OnLeave(IFsm<TBody> fsm, bool isShutdown)
		{
			Leave(fsm, fsm.Owner, isShutdown);
			base.OnLeave(fsm, isShutdown);
		}

		/// <summary>物理帧推进（由 <see cref="BodyFsm.Tick{T}"/> 调用）：先过打断规则，未被打断才跑本状态决策。</summary>
		internal void PhysicsTick(IFsm<TBody> fsm, float dt)
		{
			TBody body = fsm.Owner;
			if (Interrupt(fsm, body))
			{
				return;
			}

			Tick(fsm, body, dt);
		}

		/// <summary>进入本状态（通常在这里请求本状态的动画）。</summary>
		protected virtual void Enter(IFsm<TBody> fsm, TBody body)
		{
		}

		/// <summary>离开本状态。isShutdown = 状态机被销毁（实体隐藏/关停），此时只能清理纯 C# 事实。</summary>
		protected virtual void Leave(IFsm<TBody> fsm, TBody body, bool isShutdown)
		{
		}

		/// <summary>打断规则（死亡 / 受击……）：已切换状态时返回 true，本状态这一帧不再跑 <see cref="Tick"/>。</summary>
		protected virtual bool Interrupt(IFsm<TBody> fsm, TBody body)
		{
			return false;
		}

		/// <summary>本状态的物理帧决策。</summary>
		protected abstract void Tick(IFsm<TBody> fsm, TBody body, float dt);

		/// <summary>
		/// 重力每帧施加（英雄与怪物共用）：速度为 0 时 MoveAndSlide 不做运动检测，IsOnFloor 会闪断，
		/// 始终向下压住地面才稳定。
		/// </summary>
		protected static void ApplyGravity(TBody body, float dt)
		{
			body.Velocity += new Vector2(0f, body.Gravity * dt);
		}

		/// <summary>原地站定（横向速度归零，保留纵向）。</summary>
		protected static void Stand(TBody body)
		{
			body.Velocity = new Vector2(0f, body.Velocity.Y);
		}

		/// <summary>移除状态类名前缀和 State 后缀，生成调试名称。</summary>
		private static string TrimName(string name, string prefix)
		{
			if (prefix.Length > 0 && name.StartsWith(prefix))
			{
				name = name.Substring(prefix.Length);
			}

			return name.EndsWith("State") ? name.Substring(0, name.Length - "State".Length) : name;
		}
	}
}
