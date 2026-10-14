namespace GameLogic.Heroes.Input
{
    /// <summary>造一按键组合与松键闩锁；拒绝的普攻不缓存到收招后。</summary>
    public sealed class HeroInput
    {
        private readonly float m_DoubleTapWindow; // 双击跑窗口秒数。
        private int m_LastKeyDirection; // 最近按下的方向，用于识别双击跑。
        private int m_FacingAxis; // 最后按下且仍按住的移动方向。
        private double m_LastTapTime = double.NegativeInfinity; // 最近方向按下时刻。
        private bool m_Left, m_Right; // 上帧方向状态。
        private bool m_LeftReleased, m_RightReleased; // 本帧松键边沿，包含一帧内松开再按回。
        private bool m_Jump, m_Attack, m_Down, m_Up; // 当前 S/J/K/W 组合。
        private bool m_CanAct = true; // 任意动作键松开后允许再执行一次。

        /// <summary>创建输入采样器。</summary>
        /// <param name="doubleTapWindow">双击跑窗口，单位秒。</param>
        public HeroInput(float doubleTapWindow) => m_DoubleTapWindow = doubleTapWindow;

        /// <summary>受暂停控制的游戏时钟，供动作冷却和连段超时使用，单位秒。</summary>
        public double Time { get; private set; }

        /// <summary>水平移动方向，同时按住时最后按下者优先。</summary>
        public int MoveAxis { get; private set; }

        /// <summary>是否处于双击跑档。</summary>
        public bool Running { get; private set; }

        /// <summary>本帧是否请求开启无双。</summary>
        public bool MusouRequested { get; private set; }

        /// <summary>采样按住状态及按下边沿；边沿接受同帧内完成的短按。</summary>
        /// <param name="dt">本帧秒数。</param>
        /// <param name="leftHeld">左方向按住。</param>
        /// <param name="rightHeld">右方向按住。</param>
        /// <param name="leftPressed">左方向刚按下。</param>
        /// <param name="rightPressed">右方向刚按下。</param>
        /// <param name="jumpHeld">跳跃按住。</param>
        /// <param name="attackHeld">普攻按住。</param>
        /// <param name="musouPressed">无双刚按下。</param>
        /// <param name="downHeld">下方向按住。</param>
        /// <param name="upHeld">上方向按住，与出口共用 W。</param>
        /// <param name="jumpPressed">跳跃刚按下。</param>
        /// <param name="attackPressed">普攻刚按下。</param>
        /// <param name="actionReleased">本帧任意 S/J/K/W 松开过，即使采样时已重新按住。</param>
        /// <param name="leftReleased">本帧左方向松开过。</param>
        /// <param name="rightReleased">本帧右方向松开过。</param>
        public void Sample(float dt, bool leftHeld, bool rightHeld, bool leftPressed, bool rightPressed,
            bool jumpHeld, bool attackHeld, bool musouPressed = false, bool downHeld = false,
            bool upHeld = false, bool jumpPressed = false, bool attackPressed = false,
            bool actionReleased = false, bool leftReleased = false, bool rightReleased = false)
        {
            Time += dt;
            MusouRequested = musouPressed;
            bool jump = jumpHeld || jumpPressed;
            bool attack = attackHeld || attackPressed;

            // 四个动作键共用闩锁；被阻止的跳跃仅在仍按住时等待动作结束。
            if (actionReleased || (m_Jump && !jump) || (m_Attack && !attack) || (m_Down && !downHeld) || (m_Up && !upHeld))
            {
                m_CanAct = true;
            }

            // 松方向取消奔跑；新的方向边沿更新朝向与双击窗口。
            m_LeftReleased = leftReleased || (m_Left && !leftHeld);
            m_RightReleased = rightReleased || (m_Right && !rightHeld);
            if (m_LeftReleased || m_RightReleased)
            {
                Running = false;
            }

            if (leftPressed)
            {
                RegisterDirectionPress(-1);
            }

            if (rightPressed)
            {
                RegisterDirectionPress(1);
            }

            if ((jump && !m_Jump) || (attack && !m_Attack) || (downHeld && !m_Down) || (upHeld && !m_Up) ||
                musouPressed)
            {
                m_LastKeyDirection = 0;
            }

            m_Left = leftHeld;
            m_Right = rightHeld;
            m_Jump = jump;
            m_Attack = attack;
            m_Down = downHeld;
            m_Up = upHeld;
            if (!IsDirectionHeld(m_FacingAxis))
            {
                m_FacingAxis = 0;
            }

            MoveAxis = m_FacingAxis;
        }

        /// <summary>消费 J 或 S＋J，冷却拒绝的尝试也耗掉闩锁。</summary>
        /// <returns>是否存在可消费的普攻组合。</returns>
        public bool ConsumeAttack() => ConsumeActionIfMatched(m_Attack && !m_Jump && !m_Up);

        /// <summary>消费单独 K。</summary>
        /// <returns>是否存在可消费的跳跃组合。</returns>
        public bool ConsumeJump() => ConsumeActionIfMatched(m_Jump && !m_Attack && !m_Down && !m_Up);

        /// <summary>消费 S＋K 穿平台组合。</summary>
        /// <returns>是否存在可消费的穿平台组合。</returns>
        public bool ConsumePlatformDrop() => ConsumeActionIfMatched(m_Down && m_Jump && !m_Attack && !m_Up);

        /// <summary>允许动作时吞掉 J＋K，避免误触普攻或跳跃。</summary>
        public void ConsumeJumpAttackCombination() => ConsumeActionIfMatched(m_Attack && m_Jump && !m_Down && !m_Up);

        /// <summary>查询攻击起手时的方向是否仍按住。</summary>
        /// <param name="direction">左为 -1，右为 1。</param>
        /// <returns>指定方向是否仍按住。</returns>
        public bool IsDirectionHeld(int direction) => direction < 0 ? m_Left : direction > 0 && m_Right;

        /// <summary>查询指定方向本帧是否松开过，供攻击状态永久停止已有横移。</summary>
        /// <param name="direction">左为 -1，右为 1；零表示没有方向。</param>
        /// <returns>指定方向是否出现松键边沿。</returns>
        public bool WasDirectionReleased(int direction) => direction < 0 ? m_LeftReleased : direction > 0 && m_RightReleased;

        /// <summary>受击或未学跑攻的地面普攻取消奔跑档。</summary>
        public void StopRunning() => Running = false;

        /// <summary>实体显示时重置时钟、方向与动作闩锁。</summary>
        public void Reset()
        {
            Time = 0;
            m_LastTapTime = double.NegativeInfinity;
            m_LastKeyDirection = m_FacingAxis = MoveAxis = 0;
            m_Left = m_Right = m_Jump = m_Attack = m_Down = m_Up = Running = MusouRequested = false;
            m_LeftReleased = m_RightReleased = false;
            m_CanAct = true;
        }

        /// <summary>消费一次匹配的动作组合。</summary>
        /// <param name="matches">按键是否匹配。</param>
        /// <returns>组合与闩锁均允许时返回 true。</returns>
        private bool ConsumeActionIfMatched(bool matches)
        {
            if (!m_CanAct || !matches)
            {
                return false;
            }

            m_CanAct = false;
            return true;
        }

        /// <summary>同方向两次按下间隔严格小于窗口时奔跑。</summary>
        /// <param name="direction">本次方向。</param>
        private void RegisterDirectionPress(int direction)
        {
            Running = direction == m_LastKeyDirection && Time - m_LastTapTime < m_DoubleTapWindow;
            m_LastKeyDirection = m_FacingAxis = direction;
            m_LastTapTime = Time;
        }
    }
}
