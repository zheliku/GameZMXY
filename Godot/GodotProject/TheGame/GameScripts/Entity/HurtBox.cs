using Godot;

namespace GameLogic.Entity
{
	/// <summary>
	/// 受击判定区：挂在实体的 m_HurtBox 上，持有宿主实体的显式引用。
	/// 攻击方的 HitBox 扫到它时直接拿 <see cref="Owner"/>，不爬父节点（根规范 §4.8、GameScripts/AGENTS.md
	/// "攻击者识别：HitBox 挂宿主显式引用"）。引用由场景 [Export] 绑定（node_paths），宿主初始化时校验。
	/// </summary>
	[GlobalClass]
	public partial class HurtBox : Area2D
	{
		/// <summary>宿主实体（场景里绑定到实体根节点）</summary>
		[Export] private ActorEntity m_Owner;

		/// <summary>宿主实体</summary>
		public ActorEntity OwnerEntity => m_Owner;
	}
}
