using GameLogic.Archive;
using GodotGameFramework.Archive;
using System;
/// <summary>存档的可变游戏数据。</summary>
[Serializable]
public class GameData : ArchiveData
{
    /// <summary>当前累计分数。</summary>
    public int Score;

    /// <summary>玩家普通存档快照（等级、累计经验、金币）；由游戏流程读写。</summary>
    public PlayerSaveData Player = new();
}
