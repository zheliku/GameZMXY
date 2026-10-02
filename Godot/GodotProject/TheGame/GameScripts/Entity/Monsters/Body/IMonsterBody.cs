using Godot;
using GameLogic.Entity.Body;

namespace GameLogic.Entity.Monsters.Body
{
	/// <summary>
	/// 怪物身体状态机的宿主接口：在公共契约 <see cref="IActorBody"/> 之上只保留怪物专属成员
	/// （移动意图、出招请求、回收）。身体状态只经它读写，不接触节点、场景树、GD.*
	/// （纯 C#，可单测；Vector2 是 Godot 纯值类型，同 Battle/ 裁决）。
	///
	/// **两台状态机的边界**：AI 状态机（<see cref="AI.IMonsterAiAgent"/>）只写意图——移动意图 <see cref="MoveIntent"/>
	/// 与出招请求（<see cref="TakeAttackRequest"/> 取走）；身体状态机是受击、死亡、出招、收招硬直的唯一权威，
	/// 它不知道 AI 的存在。实现方：<see cref="MonsterEntity"/> 与 Tests 里的假宿主。
	/// </summary>
	public interface IMonsterBody : IActorBody
	{
		/// <summary>身体参数（配置快照，含各动作时长 = 动画长度）</summary>
		MonsterBodyParams Params { get; }

		/// <summary>移动意图：-1 左 / 0 停 / 1 右（AI 写入；身体在可移动状态下执行）</summary>
		int MoveIntent { get; set; }

		/// <summary>取走 AI 的出招请求（招式序号；-1 = 没有）。受击/死亡时取走即作废。</summary>
		int TakeAttackRequest();

		/// <summary>请求回收（宿主在本帧物理结束后 HideEntity，不在状态机推进中途销毁自己）</summary>
		void RequestRecycle();
	}
}
