using Godot;

namespace GameLogic.Heroes;

/// <summary>
/// 一次英雄显示的玩家席位与出生位置；由流程创建，随 GF.Entity.ShowEntityAsync 传入。
/// </summary>
/// <param name="Slot">本次控制该英雄的玩家席位。</param>
/// <param name="Position">出生世界坐标，单位像素。</param>
public readonly record struct HeroSpawn(PlayerSlot Slot, Vector2 Position);
