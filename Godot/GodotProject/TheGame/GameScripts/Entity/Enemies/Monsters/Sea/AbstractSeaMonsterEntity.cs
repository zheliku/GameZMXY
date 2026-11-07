using GameLogic.Enemies.Body;

namespace GameLogic.Enemies;

/// <summary>
/// 海域小怪分支：海底移动需要落地出招，游动物种由自己的身体状态约束攻击窗口。
/// </summary>
public abstract partial class AbstractSeaMonsterEntity : AbstractMonsterEntity
{
    /// <summary>
    /// 复用身体运动事实，而不让海域怪物依赖 Ground 或 Sky 的实体分类。
    /// </summary>
    /// <param name="index">本物种的攻击下标。</param>
    /// <returns>当前身体是否允许提交攻击请求。</returns>
    protected override bool TryRequestAttack(int index)
    {
        if (BodyFsm?.CurrentState is EnemyWalkState && !OnFloor)
        {
            return false;
        }

        return base.TryRequestAttack(index);
    }
}
