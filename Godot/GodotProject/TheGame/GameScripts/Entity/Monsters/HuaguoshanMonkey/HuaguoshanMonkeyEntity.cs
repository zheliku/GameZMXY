using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters
{
	/// <summary>
	/// 花果山猴子（旧 Monster_1，小怪）。
	///
	/// 这只怪由三部分拼成，本类是它的"身份证"（飘字位置由受击盒推导，无需覆写）：
	///  * **大脑**：<see cref="MonsterBrains.Brawler"/> 近身肉搏原型（游荡 → 走向目标 → 站定出招 → 受控 → 死亡），不换任何槽；
	///  * **数值**：MonsterConfig Id=1（血量/防御/速度/索敌/攻击欲望与节奏/滞回），
	///    招式 AttackConfig 2001（OwnerId=HuaguoshanMonkey：威力、击退、收招硬直 AiRecovery）；
	///  * **表现与判定**：Entitys/HuaguoshanMonkeyEntity.tscn + 动画库 huaguoshan_monkey_anim_library.tres
	///    （attack_1 的判定盒关键帧同时决定"打得到哪里"——AI 的出招距离/高度由它推导，表里不再写）。
	///
	/// 以后加猴子独有行为（如被打后后跳）：在本文件夹写 HuaguoshanMonkeyBackstepState，
	/// 再在 CreateBrain 里 <c>.Bind(MonsterAiRole.CcLocked, …)</c> 或 AddExtra；数值先进表。
	/// </summary>
	public partial class HuaguoshanMonkeyEntity : MonsterEntity
	{
		/// <inheritdoc />
		protected override MonsterAiStateSet CreateBrain()
		{
			return MonsterBrains.Brawler();
		}
	}
}
