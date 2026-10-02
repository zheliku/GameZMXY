using Godot;
using GodotGameFramework.Archive;
using System;
using System.Collections.Generic;
using GameLogic.Archive;
[Serializable]
public class GameData : ArchiveData
{
    public int Score;

    /// <summary>当前选择的英雄（HeroConfig.Id；开始对局时写入，选人界面回显用，M6 单英雄）</summary>
    public int SelectedHeroId;

    /// <summary>各关卡通关记录（按 LevelId 查找；通关时更新最好用时与次数）</summary>
    public List<LevelClearRecord> LevelClearRecords = new List<LevelClearRecord>();
}
[Serializable]
public class GameCatalogue : ArchiveCatalogue
{
    public string Name;
}
