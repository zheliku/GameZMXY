using Godot;

namespace GameLogic.Level;

/// <summary>
/// 本关所属怪物的一次击败（关卡内部的普通值事件载荷）：只携带值，实体随后会被回收复用。
/// 由刷怪服务在首次归还名额时报告一次，关卡运行据此结算经验、掉落与统计。
/// </summary>
public readonly struct MonsterDefeat
{
	/// <summary>MonsterConfig.Id（经验、掉落查表用）。</summary>
	public readonly int MonsterId;

	/// <summary>击杀者实体编号；0 表示无实体来源。</summary>
	public readonly int KillerEntityId;

	/// <summary>死亡位置（世界坐标，掉落物出生点）。</summary>
	public readonly Vector2 Position;

	/// <summary>创建击败记录。</summary>
	/// <param name="monsterId">怪物配置编号。</param>
	/// <param name="killerEntityId">击杀者实体编号。</param>
	/// <param name="position">死亡位置。</param>
	public MonsterDefeat(int monsterId, int killerEntityId, Vector2 position)
	{
		MonsterId = monsterId;
		KillerEntityId = killerEntityId;
		Position = position;
	}
}
