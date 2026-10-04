using GodotGameFramework.Archive;
using System;
/// <summary>存档的可变游戏数据。</summary>
[Serializable]
public class GameData : ArchiveData
{
    /// <summary>当前累计分数。</summary>
    public int Score;
}
