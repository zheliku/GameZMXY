using System;
using System.Threading.Tasks;
using GameConfig.Level;
using GameLogic.Level;
using Godot;
using Luban;

namespace GameLogic.Debug;

/// <summary>用真实 Area2D 物理重叠验证可选触发器的启用、玩家过滤和停止契约。</summary>
public partial class LevelTriggerValidation : Node2D
{
    /// <summary>运行独立引擎回归，并以明确的 PASS/FAIL 协议结束。</summary>
    public override async void _Ready()
    {
        try
        {
            await ValidateAsync();
            GD.Print("LEVEL TRIGGER PASS: 初始化停用、当前区监听、玩家过滤、单次激活和停止后重启全部通过");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PrintErr($"LEVEL TRIGGER FAIL: {error.Message}");
            GetTree().Quit(1);
        }
    }

    private async Task ValidateAsync() // 回归使用真实节点和物理信号，不手动调用 BodyEntered 回调。
    {
        uint playerLayer = PlayerLayer();
        LevelStageTriggerSet manager = new();
        Area2D first = CreateArea("special_first", new Vector2(100f, 100f), playerLayer);
        Area2D second = CreateArea("special_second", new Vector2(300f, 100f), playerLayer);
        manager.AddChild(first);
        manager.AddChild(second);
        AddChild(manager);
        CharacterBody2D player = CreateBody(new Vector2(100f, 100f), playerLayer);
        CharacterBody2D other = CreateBody(new Vector2(100f, 100f), playerLayer);
        AddChild(player);
        AddChild(other);
        manager.SyncChildEntries.Call();
        int activations = 0;
        manager.Activated += () => activations++;

        // 读取游戏同一产物，仅验证空间目录；不改正式关卡配置。
        byte[] data = FileAccess.GetFileAsBytes("res://TheGame/DataTables/GameConfigs/level_tblevelconfig.bytes");
        manager.Initialize(new TbLevelConfig(new ByteBuf(data)).Get(1));
        await SettlePhysicsAsync();
        Require(!first.Monitoring && !second.Monitoring && activations == 0, "初始化期间触发区没有停用");

        // 启用第二个区域，第一区域内的玩家和第二区域内的其他实体均不能激活。
        manager.BeginWatching("special_second", player);
        other.GlobalPosition = second.GlobalPosition;
        await SettlePhysicsAsync();
        Require(second.Monitoring && !first.Monitoring && activations == 0, "非当前区域或非会话玩家触发了激活");
        player.GlobalPosition = second.GlobalPosition;
        await SettlePhysicsAsync();
        Require(activations == 1 && !second.Monitoring, "玩家进入后必须只激活一次并停用监听");
        player.GlobalPosition = first.GlobalPosition;
        await SettlePhysicsAsync();
        player.GlobalPosition = second.GlobalPosition;
        await SettlePhysicsAsync();
        Require(activations == 1, "同一监听周期重复激活");

        // 玩家已在区域内时才启用监听，Godot 必须重新报告重叠，而不是永久等下一次进入。
        manager.BeginWatching("special_second", player);
        await SettlePhysicsAsync();
        Require(activations == 2, "启用时已经重叠的玩家未激活");

        // 停止后跨区不发事件；重新显式启用时不残留旧委托。
        manager.BeginWatching("special_first", player);
        await SettlePhysicsAsync();
        manager.EndWatching();
        player.GlobalPosition = first.GlobalPosition;
        await SettlePhysicsAsync();
        Require(activations == 2 && !first.Monitoring, "停止后仍响应物理进入");
        manager.BeginWatching("special_first", player);
        await SettlePhysicsAsync();
        Require(activations == 3, "重新启用时出现遗漏或重复委托");
        manager.EndWatching();
    }

    private async Task SettlePhysicsAsync() // 等待 deferred 监测开关及 PhysicsServer 重叠更新完整生效。
    {
        for (int i = 0; i < 4; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static Area2D CreateArea(string name, Vector2 position, uint mask) // 纯节点与碰撞形状，匹配新关卡的配置方式。
    {
        Area2D area = new() { Name = name, Position = position, CollisionLayer = 0, CollisionMask = mask };
        area.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(80f, 80f) } });
        return area;
    }

    private static CharacterBody2D CreateBody(Vector2 position, uint layer) // 同层的两个实体用于验证注入玩家身份过滤。
    {
        CharacterBody2D body = new() { Position = position, CollisionLayer = layer, CollisionMask = 0 };
        body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 6f } });
        return body;
    }

    private static uint PlayerLayer() // 独立验证进程没有框架初始化，直接从项目层名配置解析测试层。
    {
        for (int i = 1; i <= 32; i++)
        {
            if (ProjectSettings.GetSetting($"layer_names/2d_physics/layer_{i}").AsString() == "PlayerBody")
            {
                return 1u << (i - 1);
            }
        }

        throw new InvalidOperationException("项目没有 PlayerBody 物理层");
    }

    private static void Require(bool condition, string message) // 一旦契约不成立就结束独立验证，不影响正式玩法。
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
