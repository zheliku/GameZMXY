using System;
using GameFramework.Fsm;
using GameLogic.Entity.Heroes.Body;

namespace GameLogic.Entity.Heroes
{
	/// <summary>
	/// 悟空：**角色专属**部分都在这里。通用宿主机制（输入、物理、结算、状态机推进）在 <see cref="HeroEntity"/>；
	/// 悟空这一行为类别（地面近战连段）的身体状态集在本类声明——基类不预设任何具体英雄行为。
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
	/// 唐僧（远程）、八戒、沙僧（双武器）等新角色各自继承 <see cref="HeroEntity"/>，在本类同样的位置声明自己的状态集。
	/// </summary>
	public partial class WukongEntity : HeroEntity
	{
		/// <summary>悟空专属：待机憨笑用的动画名（素材 idle2；原版有、旧项目未使用）。</summary>
		protected override string IdleFlavorAnim => HeroAnims.Idle2;

		/// <summary>悟空的身体状态：地面 / 空中 / 普攻连段 / 受击 / 死亡。</summary>
		/// <returns>本角色使用的身体状态集。</returns>
		protected override FsmState<IHeroBody>[] CreateBodyStates()
		{
			return new FsmState<IHeroBody>[]
			{
				new HeroGroundState(), new HeroAirState(), new HeroAttackState(), new HeroHurtState(),
				new HeroDeathState(),
			};
		}

		/// <summary>悟空从地面状态起步。</summary>
		protected override Type InitialBodyStateType => typeof(HeroGroundState);
	}
}
