using System;
using System.Threading.Tasks;
using GameConfig.Level;
using GameLogic.Level;
using GameLogic.Heroes;
using GameLogic.Entity;
using System.Reflection;
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

    /// <summary>使用真实节点和物理信号验证类型、会话归属、监听周期及回调重入。</summary>
    /// <returns>全部物理断言完成的任务。</returns>
    private async Task ValidateAsync()
    {
        uint playerLayer = PlayerLayer();
        LevelTriggersNode manager = new();
        Area2D first = CreateArea("special_first", new Vector2(100f, 100f), playerLayer);
        Area2D second = CreateArea("special_second", new Vector2(300f, 100f), playerLayer);
        manager.AddChild(first);
        manager.AddChild(second);
        AddChild(manager);
        WukongEntity player = CreateHero(new Vector2(100f, 100f), playerLayer);
        CharacterBody2D other = CreateBody(new Vector2(100f, 100f), playerLayer);
        AddChild(player);
        player.SetPhysicsProcess(false);
        player.SetProcess(false);
        AddChild(other);
        WukongEntity foreign = CreateHero(new Vector2(500f, 100f), playerLayer);
        AddChild(foreign);
        foreign.SetPhysicsProcess(false);
        foreign.SetProcess(false);
        manager.SyncChildEntries.Call();
        int activations = 0;
        int contacts = 0;
        string nextTrigger = null;
        manager.Activated += hero =>
        {
            contacts++;
            if (!ReferenceEquals(hero, player))
                return;
            manager.EndWatching();
            activations++;
            if (nextTrigger != null)
            {
                string next = nextTrigger;
                nextTrigger = null;
                manager.BeginWatching(next);
            }
        };

        // 读取游戏同一产物，仅验证空间目录；不改正式关卡配置。
        byte[] data = FileAccess.GetFileAsBytes("res://TheGame/DataTables/GameConfigs/level_tblevelconfig.bytes");
        manager.Initialize(new TbLevelConfig(new ByteBuf(data)).Get(1));
        await SettlePhysicsAsync();
        Require(!first.Monitoring && !second.Monitoring && activations == 0, "初始化期间触发区没有停用");

        // 启用第二个区域，第一区域内的玩家和第二区域内的其他实体均不能激活。
        manager.BeginWatching("special_second");
        other.GlobalPosition = second.GlobalPosition;
        await SettlePhysicsAsync();
        Require(second.Monitoring && !first.Monitoring && activations == 0, "非当前区域或非会话玩家触发了激活");
        // 节点报告实际英雄，所有者拒绝其他会话；拒绝不会消耗监听。
        foreign.GlobalPosition = second.GlobalPosition;
        await SettlePhysicsAsync();
        Require(contacts == 1 && activations == 0 && second.Monitoring, "其他会话英雄消耗了当前触发区");
        foreign.GlobalPosition = new Vector2(500f, 100f);
        await SettlePhysicsAsync();
        typeof(AbstractActorEntity).GetProperty(nameof(AbstractActorEntity.Dead)).SetValue(foreign, true);
        foreign.GlobalPosition = second.GlobalPosition;
        await SettlePhysicsAsync();
        Require(contacts == 1, "死亡英雄仍触发接触通知");
        player.GlobalPosition = second.GlobalPosition;
        await SettlePhysicsAsync();
        Require(activations == 1 && !second.Monitoring, "玩家进入后必须只激活一次并停用监听");
        player.GlobalPosition = first.GlobalPosition;
        await SettlePhysicsAsync();
        player.GlobalPosition = second.GlobalPosition;
        await SettlePhysicsAsync();
        Require(activations == 1, "同一监听周期重复激活");

        // 玩家已在区域内时才启用监听，Godot 必须重新报告重叠，而不是永久等下一次进入。
        manager.BeginWatching("special_second");
        await SettlePhysicsAsync();
        Require(activations == 2, "启用时已经重叠的玩家未激活");

        // 停止后跨区不发事件；重新显式启用时不残留旧委托。
        manager.BeginWatching("special_first");
        await SettlePhysicsAsync();
        manager.EndWatching();
        player.GlobalPosition = first.GlobalPosition;
        await SettlePhysicsAsync();
        Require(activations == 2 && !first.Monitoring, "停止后仍响应物理进入");
        manager.BeginWatching("special_first");
        await SettlePhysicsAsync();
        Require(activations == 3, "重新启用时出现遗漏或重复委托");
        manager.EndWatching();

        // 回调中开始监听下一区域，旧接触处理不得再把新监听清掉。
        nextTrigger = "special_second";
        manager.BeginWatching("special_first");
        await SettlePhysicsAsync();
        Require(activations == 4 && second.Monitoring && !first.Monitoring, "回调重入清除了下一阶段监听");
        player.GlobalPosition = second.GlobalPosition;
        await SettlePhysicsAsync();
        Require(activations == 5, "回调重入后的区域无法正常激活");
        manager.EndWatching();
    }

    /// <summary>等待监测开关与 PhysicsServer 重叠列表完整更新。</summary>
    /// <returns>物理状态稳定后的任务。</returns>
    private async Task SettlePhysicsAsync()
    {
        for (int i = 0; i < 4; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>建立与正式关卡相同结构的区域夹具。</summary>
    /// <param name="name">区域节点名。</param>
    /// <param name="position">测试位置。</param>
    /// <param name="mask">检测层。</param>
    /// <returns>包含形状的区域。</returns>
    private static Area2D CreateArea(string name, Vector2 position, uint mask)
    {
        Area2D area = new() { Name = name, Position = position, CollisionLayer = 0, CollisionMask = mask };
        area.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(80f, 80f) } });
        return area;
    }

    /// <summary>建立与英雄同层的非英雄，验证类型过滤。</summary>
    /// <param name="position">测试位置。</param>
    /// <param name="layer">玩家身体层。</param>
    /// <returns>非英雄碰撞体。</returns>
    private static CharacterBody2D CreateBody(Vector2 position, uint layer)
    {
        CharacterBody2D body = new() { Position = position, CollisionLayer = layer, CollisionMask = 0 };
        body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 6f } });
        return body;
    }

    /// <summary>创建最小真实英雄类型；只在测试反射打开显示标志，不启动战斗和框架流程。</summary>
    /// <param name="position">物理位置。</param>
    /// <param name="layer">玩家身体层。</param>
    /// <returns>用于真实 Area2D 检测的英雄。</returns>
    private static WukongEntity CreateHero(Vector2 position, uint layer)
    {
        WukongEntity hero = new() { Position = position, CollisionLayer = layer, CollisionMask = 0 };
        hero.SetPhysicsProcess(false);
        typeof(AbstractActorEntity).GetProperty(nameof(AbstractActorEntity.IsShown)).SetValue(hero, true);
        hero.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 6f } });
        return hero;
    }

    /// <summary>独立验证没有框架初始化，从项目配置按名称解析玩家层。</summary>
    /// <returns>玩家身体层掩码。</returns>
    /// <exception cref="InvalidOperationException">项目缺少玩家层。</exception>
    private static uint PlayerLayer()
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

    /// <summary>契约失败时终止独立验证，不影响正式玩法。</summary>
    /// <param name="condition">必须满足的条件。</param>
    /// <param name="message">失败原因。</param>
    /// <exception cref="InvalidOperationException">断言不成立。</exception>
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
