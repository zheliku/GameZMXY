using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameLogic.Save.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 离线转存后的正式档案样例经运行时校验与快照往返，保留原成长、资产和技能。
/// </summary>
public sealed class SaveUpgradeTests
{
    /// <summary>
    /// 使用工具实际生成的唐僧 v14 样例验证完整往返与旧档个人归属的无损推导。
    /// </summary>
    [Fact]
    public void UpgradedTangsengSave_RoundTripsWithoutLosingPersistentData()
    {
        // 样例由离线工具生成，测试直接调用正式校验与恢复入口。
        string directory = Path.Combine(AppContext.BaseDirectory, "Data");
        GameData expected = JsonConvert.DeserializeObject<GameData>(
            File.ReadAllText(Path.Combine(directory, "save_v14_tangseng.json")));
        var scope = TestGameScope.FromSaveData(expected, TestTables.Curve,
            TestTables.Tables.TbEquipmentConfig, TestTables.Tables.TbProfileConfig.EquipmentCapacity);
        GameData captured = scope.CaptureSaveData();
        HeroSaveData hero = Assert.Single(expected.Heroes);
        HeroSaveData restored = Assert.Single(captured.Heroes);

        // 个人成长、钱包、归属与学习信息逐项比较，集合不依赖快照的稳定排序。
        Assert.Equal(GameData.CurrentVersion, expected.SaveVersion);
        Assert.Equal(expected.SaveVersion, captured.SaveVersion);
        Assert.Equal(hero.HeroId, restored.HeroId);
        Assert.Equal(hero.TotalExperience, restored.TotalExperience);
        Assert.Equal(hero.Soul, restored.Soul);
        Assert.Equal(hero.BagInstanceIds.OrderBy(id => id), restored.BagInstanceIds.OrderBy(id => id));
        Guid[] worn = [hero.WeaponInstanceId, hero.ArmorInstanceId, hero.AccessoryInstanceId, hero.TreasureInstanceId];
        Guid[] restoredWorn = [restored.WeaponInstanceId, restored.ArmorInstanceId, restored.AccessoryInstanceId, restored.TreasureInstanceId];
        Assert.Equal(worn, restoredWorn);
        Assert.Equal(hero.LearnedSkills.OrderBy(id => id), restored.LearnedSkills.OrderBy(id => id));
        Assert.Equal(hero.PassiveLevels.OrderBy(pair => pair.Key), restored.PassiveLevels.OrderBy(pair => pair.Key));

        // 共享进度与全档实例池保留，装备编号及已经抽出的属性不重新生成。
        Assert.Equal(expected.HighestClearedLevelId, captured.HighestClearedLevelId);
        Assert.Equal(expected.EastSeaBannerAvailable, captured.EastSeaBannerAvailable);
        Dictionary<Guid, EquipmentSaveData> expectedItems = expected.EquipmentItems.ToDictionary(item => item.InstanceId);
        Dictionary<Guid, EquipmentSaveData> restoredItems = captured.EquipmentItems.ToDictionary(item => item.InstanceId);
        Assert.Equal(expectedItems.Keys.OrderBy(id => id), restoredItems.Keys.OrderBy(id => id));
        foreach ((Guid instanceId, EquipmentSaveData item) in expectedItems)
        {
            Assert.Equal(item.EquipmentId, restoredItems[instanceId].EquipmentId);
            Assert.Equal(item.Attributes, restoredItems[instanceId].Attributes);
        }

        // 旧根钱包完整移入唯一英雄，所有原实例恰好落入背包或非空穿戴。
        JObject legacy = JObject.Parse(File.ReadAllText(Path.Combine(directory, "save_v12_tangseng.json")));
        Assert.Equal(12, legacy.Value<int>("SaveVersion"));
        Assert.Equal(legacy.Value<int>("Soul"), hero.Soul);
        Assert.Equal(((JArray)legacy["EquipmentItems"]).Count,
            hero.BagInstanceIds.Count + worn.Count(id => id != Guid.Empty));
    }
}
