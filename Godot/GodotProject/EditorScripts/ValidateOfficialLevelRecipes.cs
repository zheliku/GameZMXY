using System;
using System.Linq;
using System.Text.Json;
using GameConfig.Entity;
using GameConfig.Level;
using Godot;
using Luban;

/// <summary>只读审计真实二进制配方与官方元件，防止恢复坐标时将猴子和妖猴的生成点串位。</summary>
public partial class ValidateOfficialLevelRecipes : RefCounted
{
    /// <summary>比较全部三关的稳定生成点、敌人、数量、首次等待和间隔。</summary>
    /// <param name="metadataPath">由官方主文件及 GameSence.as 直接提取的 JSON 文件。</param>
    /// <returns>已核对的生成流数量。</returns>
    /// <exception cref="InvalidOperationException">任何配方与官方源元件不一致。</exception>
    public int Verify(string metadataPath)
    {
        using var source = JsonDocument.Parse(System.IO.File.ReadAllText(metadataPath));
        var table = new TbLevelConfig(new ByteBuf(FileAccess.GetFileAsBytes(
            "res://TheGame/DataTables/GameConfigs/level_tblevelconfig.bytes")));
        int count = 0;
        foreach (int number in new[] { 1, 2, 3 })
        {
            LevelConfig config = table.Get(number);
            var level = source.RootElement.GetProperty($"1-{number}");
            foreach (var item in level.GetProperty("spawns").EnumerateArray())
            {
                var properties = item.GetProperty("properties");
                int stage = properties.GetProperty("stopPointIdx").GetInt32() + 1;
                string node = item.GetProperty("projectNode").GetString();
                string id = $"stage_{stage}_{char.ToLowerInvariant(node[^1])}";
                LevelStage stageConfig = config.Stages.Single(value => value.StageOrder == stage);
                LevelSpawnRecipe recipe = stageConfig.Recipes.Single(value => value.SpawnPointId == id);
                EntityId enemy = properties.GetProperty("enemyType").GetInt32() switch
                {
                    1 => EntityId.HuaguoshanMonkey,
                    2 => EntityId.DemonMonkey,
                    3 => EntityId.Gorilla,
                    4 => EntityId.MacaqueKing,
                    6 => EntityId.TamarinKing,
                    _ => throw new InvalidOperationException("未知官方敌人编号")
                };
                float first = item.GetProperty("firstSpawnSeconds").GetSingle();
                if (recipe.MonsterEntityId != enemy || recipe.Count != properties.GetProperty("totalNum").GetInt32() ||
                    Math.Abs(stageConfig.SpawnDelay + recipe.Delay - first) > 0.001 ||
                    recipe.Count > 1 && Math.Abs(recipe.Interval - properties.GetProperty("interval").GetSingle()) > 0.001)
                    throw new InvalidOperationException($"关卡 {number} {id} 与官方元件 {item.GetProperty("name").GetString()} 不符：" +
                        $"actual={recipe}, sourceEnemy={enemy}, first={first}, source={properties}");
                count++;
            }
            if (config.Stages.Sum(stage => stage.Recipes.Count) != level.GetProperty("spawns").GetArrayLength())
                throw new InvalidOperationException($"关卡 {number} 存在多余配方");
        }
        GD.Print($"OFFICIAL RECIPES PASS: {count} streams, enemy/count/first delay/repeated interval and stable point identities.");
        return count;
    }
}
