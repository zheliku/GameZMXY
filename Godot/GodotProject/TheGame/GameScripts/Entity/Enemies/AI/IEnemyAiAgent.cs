using GameLogic.Architecture;
using Godot;
namespace GameLogic.Enemies.AI
{
    /// <summary>
    /// 怪物 AI 的宿主接口（GF.Fsm 的持有者类型）：AI 状态只通过它**读感知、写意图**，
    /// 不接触节点、场景树与随机单例——AI 目录是纯 C#，可脱离引擎单测（Tests/BattleTests）。
    ///
    /// AI 只决定移动意图、朝向和出招请求；
    /// 受击、死亡、出招、收招硬直与动画播放由敌人的身体状态机（Enemies/Body/）执行；
    /// AI 只读它的结果（IsCcLocked / IsAttacking / IsDead），AI 状态名与身体状态、动画互不耦合。
    /// </summary>
    public interface IEnemyAiAgent : ICanGetSystem
    {
        /// <summary>
        /// 已死亡
        /// </summary>
        bool IsDead { get; }

        /// <summary>
        /// 受控中：受击硬直（后续冰冻/眩晕/定身等控制 Buff 也并入这里）
        /// </summary>
        bool IsCcLocked { get; }

        /// <summary>
        /// 出招中：已请求待提交 / 攻击段在播 / 收招硬直（AttackConfig.AiRecovery）三者之一。
        /// 期间 AI 不做任何决策（不移动、不转身、不出下一招），宿主也拒绝 Face 与 RequestAttack。
        /// </summary>
        bool IsAttacking { get; }


        /// <summary>
        /// 当前是否持有存活目标；目标受击盒不参与AI距离决策。
        /// </summary>
        bool HasTarget { get; }

        /// <summary>
        /// 场景配置的巡逻折返边界，相对相机或出生点，像素。
        /// </summary>
        Vector2 PatrolBounds { get; }

        /// <summary>
        /// 场景的交战范围；矩形包含出招水平距离与允许追击高度。
        /// </summary>
        Rect2 EngageBounds { get; }

        /// <summary>
        /// 当前目标原点是否处于场景交战形状内。
        /// </summary>
        bool IsInEngageRange { get; }

        /// <summary>
        /// 目标实体原点相对自身的位移；原版小怪和BOSS均按角色原点判断追击和选招距离。
        /// </summary>
        Vector2 TargetOffset { get; }

        /// <summary>
        /// 自己相对巡逻中心的水平偏移；关卡使用当前相机中心，独立场地使用出生点
        /// </summary>
        float PatrolOffsetX { get; }

        /// <summary>
        /// 决策参数
        /// </summary>
        GameConfig.Enemy.EnemyConfig Config { get; }

        /// <summary>
        /// 本怪物独占的冷却和意图。
        /// </summary>
        EnemyAiData AiData { get; }

        /// <summary>
        /// 同类怪物共享的只读状态配方，不包含状态实例。
        /// </summary>
        EnemyAiPreset AiPreset { get; }

        /// <summary>
        /// 均匀随机数 [0,1)。随机源由宿主提供，单测可注入确定序列。
        /// </summary>
        /// <returns>均匀分布在[0,1)内的下一随机样本。</returns>
        float NextRandom();

        /// <summary>
        /// 写移动意图：-1 左 / 0 停 / 1 右（移动时朝向随之翻转）
        /// </summary>
        /// <param name="dir">方向符号：-1向左、0保持或停止、1向右。</param>
        void Move(int dir);

        /// <summary>
        /// 原地转向：-1 左 / 1 右（0 不变；出招/受控中忽略）
        /// </summary>
        /// <param name="dir">方向符号：-1向左、0保持或停止、1向右。</param>
        void Face(int dir);

        /// <summary>
        /// 请求出招（招式下标）。身体状态机在下一物理帧提交（进入出招状态并转向目标）；
        /// 当前不可出招（受控/死亡/已在出招）时返回 false。
        /// </summary>
        /// <param name="index">当前宿主攻击参数中的招式编号。</param>
        /// <returns>请求是否被接纳；实际动作由下一物理帧提交。</returns>
        bool RequestAttack(int index);
    }
}
