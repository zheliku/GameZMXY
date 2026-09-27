namespace GameLogic.Entity
{
	/// <summary>
	/// 悟空。M3 阶段它是"这一只英雄"的身份落点，通用控制器逻辑全在 <see cref="HeroEntity"/>。
	///
	/// 场景绑定（WukongEntity.tscn）：
	///  * m_Body / m_Weapon —— 身体层 + 武器层，两层播同一动画名即天然对齐；
	///  * m_HurtBox —— PlayerHurtBox 层，供怪物攻击判定扫描；
	///  * m_HitBox —— PlayerHitBox 层，只扫 EnemyHurtBox；判定帧开关由 M4 接入。
	///
	/// 换装测试：装备系统落地前，替换 m_Weapon 的 SpriteFrames 即可换武器外观
	/// （可选资源见 Docs/LegacyAssetMap.md「武器层」）。
	/// </summary>
	public partial class WukongEntity : HeroEntity
	{
	}
}
