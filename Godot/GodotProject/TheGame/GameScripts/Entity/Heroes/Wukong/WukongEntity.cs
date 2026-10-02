using GameLogic.Entity.Heroes.Body;

namespace GameLogic.Entity.Heroes
{
	/// <summary>
	/// 悟空：**角色专属**部分都在这里。通用机制（输入、走跑、跳、普攻连段、受击、死亡、
	/// 待机小动作）全在 <see cref="HeroEntity"/> 与英雄身体状态机（Heroes/Body/），本类只放"只有悟空不一样"的东西。
	///
	/// 场景绑定（Entitys/WukongEntity.tscn）：
	///  * HeroId —— 对应的 HeroConfig 行；
	///  * m_Body / m_Weapon —— 身体层 + 武器层 Sprite2D（6x14 网格），帧由 m_AnimPlayer 驱动；
	///  * m_AnimPlayer —— 悟空自己的动画库（Entitys/Animations/wukong_anim_library.tres，
	///    纯表现数据：帧/特效/判定盒值轨道，无方法轨道），由 HeroEntity 经 ActorEntity 直接驱动；
	///  * m_HurtBox / m_HitBox —— 判定区（层见 Entity/AGENTS.md）。
	///
	/// 角色专属动画名只允许出现在三个地方：该角色的动画库资源、本类覆写与 AttackConfig.Animation。
	/// 换装测试：替换 m_Weapon 的 Texture 即可换武器外观（同网格图集，见 Docs/LegacyAssetMap.md）。
	/// </summary>
	public partial class WukongEntity : HeroEntity
	{
		/// <summary>悟空专属：待机憨笑用的动画名（素材 idle2；原版有、旧项目未使用）。</summary>
		protected override string IdleFlavorAnim => HeroAnims.Idle2;
	}
}
