namespace GameLogic.Enemies.AI
{
    /// <summary>
    /// 单个怪物独占的 AI 数据；目标节点仍归实体，招式定义归 ConfigurationModel。
    /// </summary>
    public sealed class EnemyAiData
    {
        /// <summary>
        /// 本次显示以来的游戏秒数，受控、出招和 AI 状态切换不重置。
        /// </summary>
        internal double ElapsedSeconds { get; set; }

        /// <summary>
        /// 上次 AI 推进前的游戏秒数，用于检测节拍跨越，不补发出招期间错过的决策。
        /// </summary>
        internal double PreviousElapsedSeconds { get; set; }

        /// <summary>
        /// 官方共用计数器最近一次游荡归零时点；状态切换不重置。
        /// </summary>
        internal double ClockOrigin { get; set; }

        /// <summary>
        /// 当前游荡周期的停步时点，原版在三秒和四秒之间选择。
        /// </summary>
        internal double PatrolStopTime { get; set; } = 3;

        /// <summary>
        /// AI 写入、身体消费的水平移动意图。
        /// </summary>
        public int MoveIntent { get; internal set; }

        /// <summary>
        /// 飞行与游动AI写入的垂直意图：-1上升、0悬停、1下降；身体只消费同一事实。
        /// </summary>
        public int VerticalIntent { get; internal set; }

        /// <summary>
        /// 当前退避阶段直接采用垂直速度，其他阶段按身体参数加速。
        /// </summary>
        public bool DirectVerticalMovement { get; internal set; }

        /// <summary>
        /// 等待身体状态机提交的攻击下标，-1 表示没有请求。
        /// </summary>
        public int AttackRequest { get; internal set; } = -1;

        /// <summary>
        /// 由 EnemyAiSystem 推进的独立逐招冷却，单位秒。
        /// </summary>
        internal float[] Cooldowns { get; }

        /// <summary>
        /// 为本怪物分配逐招冷却，不复制共享攻击参数。
        /// </summary>
        /// <param name="attackCount">共享攻击参数的数量。</param>
        public EnemyAiData(int attackCount)
        {
            Cooldowns = new float[attackCount];
        }
    }
}
