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
		private readonly float m_DoubleTapWindow;
		private readonly float m_BufferTime;

		/// <summary>输入时钟（物理 dt 累计，双击判定用）</summary>
		private double m_Clock;

		private int m_LastTapDir;
		private double m_LastTapTime;

		/// <summary>距离上次按下的秒数（+∞ = 没有待消费的按键）</summary>
		private float m_JumpAge = float.PositiveInfinity;

		private float m_AttackAge = float.PositiveInfinity;

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

		/// <summary>方向键点击：同一方向在窗口内二次按下 → 跑步档。</summary>
		private void Tap(int dir)
		{
			Running = dir == m_LastTapDir && m_Clock - m_LastTapTime <= m_DoubleTapWindow;
			m_LastTapDir = dir;
			m_LastTapTime = m_Clock;
		}
	}
}
