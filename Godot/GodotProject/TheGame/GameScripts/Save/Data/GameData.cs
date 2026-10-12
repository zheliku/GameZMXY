using System;
using System.Collections.Generic;
using GameLogic.Save.Data;
using GodotGameFramework.Archive;
using Newtonsoft.Json;

/// <summary>一格存档的完整数据：格式版本、出战英雄、英雄经验和灵魂；文件编号继承自框架。</summary>
/// <remarks>GGF 直接写盘；关卡回滚也使用本类型的独立快照。与运行时档案的转换集中在 ProfileSaveHelper。</remarks>
[Serializable]
public class GameData : ArchiveData
{
    /// <summary>当前存档格式；格式改变时递增，不保留迁移分支。</summary>
    public const int CurrentVersion = 4;

    /// <summary>必需的格式版本；框架创建的新空槽使用当前版本。</summary>
    [JsonProperty(Required = Required.Always)]
    public int SaveVersion = CurrentVersion;

    /// <summary>当前出战英雄的 HeroConfig.Id；0 仅用于框架尚未认领的新空槽。</summary>
    [JsonProperty(Required = Required.Always)]
    public int ActiveHeroId;

    /// <summary>拥有英雄的配置 ID 与累计经验；等级和战斗属性读档时重新计算。</summary>
    [JsonProperty(Required = Required.Always)]
    public List<HeroSaveData> Heroes = new();

    /// <summary>钱包灵魂总数。</summary>
    [JsonProperty(Required = Required.Always)]
    public int Soul;
}

namespace GameLogic.Save.Data
{
    /// <summary>档案快照中的一名英雄：配置 ID 和累计经验。</summary>
    [Serializable]
    public sealed class HeroSaveData
    {
        /// <summary>英雄的 HeroConfig.Id。</summary>
        public int HeroId { get; set; }

        /// <summary>累计经验，等级和本级经验由经验曲线派生。</summary>
        public int TotalExperience { get; set; }
    }
}
