using GameLogic.Entity.Monsters.AI;
using Godot;

namespace GameLogic.Entity.Monsters
{
	/// <summary>
	/// 花果山猴子（旧 Monster_1，小怪）。
	///
	/// 这只怪由三部分拼成，本类是它的"身份证"：
	///  * **大脑**：<see cref="MonsterBrains.GroundMelee"/> 地面近战原型（巡逻 → 追击 → 站定普攻 → 受控 → 死亡），不替换任何状态；
	///  * **数值**：MonsterConfig Id=1（血量/防御/速度/索敌/攻击欲望与节奏/滞回），
	///    招式 AttackConfig 2001（OwnerId=HuaguoshanMonkey：威力、击退、收招硬直 AiRecovery）；
	///  * **表现与判定**：Entitys/HuaguoshanMonkeyEntity.tscn + 动画库 huaguoshan_monkey_anim_library.tres
	///    （attack_1 的判定盒关键帧同时决定"打得到哪里"——AI 的出招距离/高度由它推导，表里不再写）。
	///
	/// 专属覆写：飘字锚点（素材高度）。以后加猴子独有行为（如被打后后跳、呼叫同伴）就写在这里：
	/// 在 CreateBrain 里 Bind/AddExtra 自己的状态，数值先进表。
	/// </summary>
	public partial class HuaguoshanMonkeyEntity : MonsterEntity
	{
		/// <summary>飘字锚点：猴子素材约 63px 高，头顶在原点上方约 50px（表现层布局常数，不参与玩法计算）</summary>
		protected override Vector2 PopAnchor => new Vector2(0, -50);

		/// <inheritdoc />
		protected override MonsterAiStateSet CreateBrain()
		{
			return MonsterBrains.GroundMelee();
		}
	}
}
