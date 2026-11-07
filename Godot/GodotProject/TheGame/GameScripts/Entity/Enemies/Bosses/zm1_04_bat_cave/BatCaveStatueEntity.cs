using GameLogic.Enemies.AI;

namespace GameLogic.Enemies;

/// <summary>
/// 官方蝙蝠窟石像：静止受击目标；蝙蝠生成和出口开放由关卡负责。
/// </summary>
public partial class BatCaveStatueEntity : AbstractBossEntity
{
    /// <inheritdoc />
    protected override EnemyAiPreset AiPreset => null;

    /// <summary>
    /// 石像固定于源坐标，受击不产生位移。
    /// </summary>
    protected override bool IsSuperArmor => true;
}
