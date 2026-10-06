namespace GameLogic.Entity.Heroes.Body
{
	/// <summary>
	/// 英雄输入层（纯 C#）：每物理帧由宿主喂入原始按键（<see cref="Sample"/>），产出
	/// 移动轴、跑步档与**带缓冲窗口的一次性请求**（跳 / 攻）。身体状态只经这里读输入、消费请求。
	///
	///  * 双击跑：同一方向在 RunDoubleTapWindow 内二次按下进入跑步档，松开方向即退出（旧项目手感）；
	///  * 输入缓冲：按下后 InputBufferTime 秒内仍可被消费（等到落地、收招、硬直结束等可执行时机），
	///    消费后立即作废；窗口 = 0 时只在按下那一帧有效（等同旧行为）。缓冲是**手感**设计——
	///    Godot 4 在物理帧里查 IsActionJustPressed 不会漏键。
	/// </summary>
	public sealed class HeroInput
	{
		private readonly float m_DoubleTapWindow; // 同方向双击跑判定窗口秒数。
		private readonly float m_BufferTime; // 跳跃和攻击请求的缓冲时长秒数。

		private double m_Clock; // 输入时钟，按物理 dt 累计并用于双击判定。

		private int m_LastTapDir; // 上一次方向键点击方向。
		private double m_LastTapTime; // 上一次方向键点击时的输入时钟。

		private float m_JumpAge = float.PositiveInfinity; // 距离跳跃按下的秒数，正无穷表示没有待消费请求。

		private float m_AttackAge = float.PositiveInfinity; // 距离攻击按下的秒数，正无穷表示没有待消费请求。

		/// <summary>创建使用指定双击与输入缓冲窗口的英雄输入层。</summary>
		/// <param name="doubleTapWindow">双击跑判定窗口秒（HeroConfig.RunDoubleTapWindow）</param>
		/// <param name="bufferTime">一次性请求的缓冲窗口秒（HeroConfig.InputBufferTime）</param>
		public HeroInput(float doubleTapWindow, float bufferTime)
		{
			m_DoubleTapWindow = doubleTapWindow;
			m_BufferTime = bufferTime < 0f ? 0f : bufferTime;
			Reset();
		}

		/// <summary>水平移动轴：-1 左 / 0 无 / 1 右</summary>
		public int MoveAxis { get; private set; }

		/// <summary>跑步档</summary>
		public bool Running { get; private set; }

		/// <summary>有待消费的跳跃请求</summary>
		public bool JumpBuffered => m_JumpAge <= m_BufferTime;

		/// <summary>有待消费的普攻请求</summary>
		public bool AttackBuffered => m_AttackAge <= m_BufferTime;

		/// <summary>
		/// 喂入本物理帧的原始按键：held = 按住，pressed = 本帧刚按下。
		/// </summary>
		public void Sample(float dt, bool leftHeld, bool rightHeld, bool leftPressed, bool rightPressed,
			bool jumpPressed, bool attackPressed)
		{
			m_Clock += dt;
			MoveAxis = (rightHeld ? 1 : 0) - (leftHeld ? 1 : 0);
			if (leftPressed)
			{
				Tap(-1);
			}

			if (rightPressed)
			{
				Tap(1);
			}

			if (MoveAxis == 0)
			{
				Running = false;
			}

			m_JumpAge = jumpPressed ? 0f : m_JumpAge + dt;
			m_AttackAge = attackPressed ? 0f : m_AttackAge + dt;
		}

		/// <summary>消费跳跃请求（有则作废并返回 true）。</summary>
		public bool ConsumeJump()
		{
			if (!JumpBuffered)
			{
				return false;
			}

			m_JumpAge = float.PositiveInfinity;
			return true;
		}

		/// <summary>消费普攻请求（有则作废并返回 true）。</summary>
		public bool ConsumeAttack()
		{
			if (!AttackBuffered)
			{
				return false;
			}

			m_AttackAge = float.PositiveInfinity;
			return true;
		}

		/// <summary>作废所有待消费请求（进入受击硬直时：硬直前按的键不带出硬直）。</summary>
		public void ClearBuffers()
		{
			m_JumpAge = float.PositiveInfinity;
			m_AttackAge = float.PositiveInfinity;
		}

		/// <summary>复位（实体显示时）。</summary>
		public void Reset()
		{
			m_Clock = 0;
			m_LastTapDir = 0;
			m_LastTapTime = double.NegativeInfinity;
			MoveAxis = 0;
			Running = false;
			ClearBuffers();
		}

		private void Tap(int dir) // 记录方向点击，并在双击窗口内切换跑步档。
		{
			// 同方向且间隔足够短时进入跑步档，否则只更新最近一次点击。
			Running = dir == m_LastTapDir && m_Clock - m_LastTapTime <= m_DoubleTapWindow;
			m_LastTapDir = dir;
			m_LastTapTime = m_Clock;
		}
	}
}
