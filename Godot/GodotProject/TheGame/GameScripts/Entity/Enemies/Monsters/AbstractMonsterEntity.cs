namespace GameLogic.Enemies;

/// <summary>
/// 小怪分支承载普通怪物的身份；每个具体物种必须明确选择自己的 AI 预设，运动分支决定地面或飞行身体。
/// </summary>
public abstract partial class AbstractMonsterEntity : AbstractEnemyEntity
{
}
