using System;
using System.Collections.Generic;
using GameLogic.Save.Data;
using GodotGameFramework.Archive;
using Newtonsoft.Json;
using GameConfig.Equipment;
using GameLogic.Equipment;
using GameConfig.Skills;

/// <summary>一格存档的完整数据：版本、出战英雄、经验、灵魂、装备实例和已学技能；文件编号继承自框架。</summary>
/// <remarks>GGF 直接写盘；关卡回滚也使用本类型的独立快照。与运行时档案的转换集中在 SaveDataUtility。</remarks>
[Serializable]
public class GameData : ArchiveData
{
    /// <summary>当前存档格式；格式改变时递增，不保留迁移分支。</summary>
    public const int CurrentVersion = 9;

    /// <summary>必需的格式版本；框架创建的新空槽使用当前版本。</summary>
    [JsonProperty(Required = Required.Always)]
    public int SaveVersion = CurrentVersion;

    /// <summary>当前出战英雄的 HeroConfig.Id；0 仅用于框架尚未认领的新空槽。</summary>
    [JsonProperty(Required = Required.Always)]
    public int ActiveHeroId;

    /// <summary>拥有英雄的配置 ID、累计经验与学习记录；等级和战斗属性读档时重新计算。</summary>
    [JsonProperty(Required = Required.Always)]
    public List<HeroSaveData> Heroes = new();

    /// <summary>钱包灵魂总数。</summary>
    [JsonProperty(Required = Required.Always)]
    public int Soul;

    /// <summary>已通关的最远关卡主键；0 表示尚未通关，进入或失败不推进。</summary>
    [JsonProperty(Required = Required.Always)]
    public int HighestClearedLevelId;

    /// <summary>已拾取的装备实例；随机属性作为实例状态保存，配置范围不写盘。</summary>
    [JsonProperty(Required = Required.Always)]
    public List<EquipmentSaveData> EquipmentItems = new();
}

namespace GameLogic.Save.Data
{
    /// <summary>一件装备的完整持久状态，不含贴图、节点或重复的装备定义。</summary>
    [Serializable]
    public sealed class EquipmentSaveData
    {
        /// <summary>同名装备之间也唯一的实例编号。</summary>
        [JsonProperty(Required = Required.Always)]
        public Guid InstanceId { get; set; }

        /// <summary>装备配置编号。</summary>
        [JsonProperty(Required = Required.Always)]
        public EquipmentId EquipmentId { get; set; }

        /// <summary>本件生成时已经确定的属性，读档不得重新随机。</summary>
        [JsonProperty(Required = Required.Always)]
        public EquipmentAttributes Attributes { get; set; }
    }

    /// <summary>档案快照中的一名英雄：身份、成长、穿戴引用与技能学习记录。</summary>
    [Serializable]
    public sealed class HeroSaveData
    {
        /// <summary>英雄的 HeroConfig.Id。</summary>
        [JsonProperty(Required = Required.Always)]
        public int HeroId { get; set; }

        /// <summary>累计经验，等级和本级经验由经验曲线派生。</summary>
        [JsonProperty(Required = Required.Always)]
        public int TotalExperience { get; set; }

        /// <summary>已学习的主动技能编号；不保存费用、伤害、图标或施放中的临时状态。</summary>
        [JsonProperty(Required = Required.Always)]
        public List<SkillId> LearnedSkills { get; set; } = new();

        /// <summary>已穿戴武器的实例编号；空编号表示未穿戴。</summary>
        [JsonProperty(Required = Required.Always)]
        public Guid WeaponInstanceId { get; set; }

        /// <summary>已穿戴防具的实例编号；空编号表示未穿戴。</summary>
        [JsonProperty(Required = Required.Always)]
        public Guid ArmorInstanceId { get; set; }

        /// <summary>已穿戴饰品的实例编号；空编号表示未穿戴。</summary>
        [JsonProperty(Required = Required.Always)]
        public Guid AccessoryInstanceId { get; set; }
    }
}
