using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameConfig.Equipment;
using GameLogic.Architecture;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.UI;
using GameLogic.UI.Widgets;
using Godot;
using GodotGameFramework;

/// <summary>
/// 地图关卡图鉴的真实配置、布局与原生tooltip回归。
/// </summary>
public sealed partial class FrontendSmokeScenario
{
    /// <summary>
    /// 验证十个地图入口、守关图像、透明装备图标与嵌套详情。
    /// </summary>
    /// <param name="map">真实GGF地图窗口。</param>
    /// <returns>卡片渲染和鼠标行为完成。</returns>
    private async Task CheckLevelGuideAsync(MapForm map)
    {
        IArchitecture architecture = GF.Fsm.GetArchitecture();
        ProfileSystem profile = GF.Fsm.GetSystem<ProfileSystem>();
        await CheckWhirlpoolStatesAsync(map);
        for (int id = 1; id < 10; id++)
        {
            profile.RecordLevelClear(id);
        }
        string[] names = new[] { "Huaguoshan", "Shuiliandong", "Taohuayuan", "Bianfuku", "Qixingdong",
            "Jialuoluo", "Qianan", "Donghai", "Shazhiwoliu", "Niumogu" };
        Window window = _driver.GetWindow();
        window.GuiEmbedSubwindows = true;
        for (int id = 1; id <= 10; id++)
        {
            LevelGuideData data = architecture.SendQuery(new LevelGuideQuery(id));
            Check(!string.IsNullOrEmpty(data.Level.Desc) && !string.IsNullOrEmpty(data.Enemies[0].NameCn), "图鉴缺少介绍或守关敌人");
            Check(data.Drops.Select(item => item.Id).Distinct().Count() == data.Drops.Count, "图鉴掉落没有按装备身份去重");
            Check(id != 10 || data.Drops.Any(item => item.Id == EquipmentId.DiamondSnare), "牛魔谷图鉴缺少实际直接奖励");
            PackedScene packed = GD.Load<PackedScene>("res://TheGame/UIs/Map/level_guide_tooltip.tscn");
            LevelGuideTooltip card = packed.Instantiate<LevelGuideTooltip>();
            map.AddChild(card);
            // 静态美术测量独立于鼠标生命周期，后半段另测真实移入/移出。
            card.SetProcess(false);
            typeof(LevelGuideTooltip).GetMethod("Bind", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(card, [data]);
            card.Position = new Vector2(30, 20);
            card.Show();
            for (int frame = 0; frame < 5; frame++)
            {
                await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            card.Size = card.GetCombinedMinimumSize();
            Check(card.IsVisibleInTree(), "卡片测量期间已错误关闭");
            Check(card.Size.X <= 210 && card.Size.Y <= 260, $"图鉴没有适配小型浮层：{id}, size={card.Size}");
            Check(card.GetNode<GridContainer>("Content/Loot/Drops").GetChildCount() == data.Drops.Count, "掉落图文数与查询不符");
            ItemList enemies = card.GetNode<ItemList>("Content/Encounter/Enemies");
            Check(enemies.ItemCount == data.Enemies.Count && enemies.GetItemRect(0).Position.Y == enemies.GetItemRect(1).Position.Y,
                "怪物列表数量错误或未按两列紧凑排布");
            for (int index = 0; index < enemies.ItemCount; index++)
            {
                var portrait = enemies.GetItemIcon(index) as AtlasTexture;
                Check(enemies.GetItemText(index) == data.Enemies[index].NameCn, "怪物名称与图片身份不一致");
                float textWidth = enemies.GetThemeFont("font").GetStringSize(enemies.GetItemText(index),
                    fontSize: enemies.GetThemeFontSize("font_size")).X;
                Check(enemies.GetItemRect(index).Size.X >= textWidth + enemies.FixedIconSize.X +
                    enemies.GetThemeConstant("icon_margin"), "怪物名称在紧凑列表中被裁断");
                Check(portrait != null && new Rect2(Vector2.Zero, portrait.Atlas.GetSize()).Encloses(portrait.Region),
                    "怪物图像裁切超出正式图集");
                using (Image portraitImage = portrait.GetImage())
                {
                    Check(!portraitImage.IsInvisible() && portraitImage.GetUsedRect().Size == portraitImage.GetSize(),
                        "怪物图片为空或仍含未裁去的透明边缘");
                }
            }
            Check(!card.HasNode("Content/Footer"), "图鉴仍保留隐藏关入口或提示尾句");
            foreach (EquipmentCell icon in card.GetNode<GridContainer>("Content/Loot/Drops").GetChildren().OfType<EquipmentCell>())
            {
                Check(icon.Texture == null && icon.CustomMinimumSize == new Vector2(32, 32), "图鉴装备仍使用背包背景或未缩小");
            }

            foreach (Label text in card.FindChildren("*", "Label", true, false).OfType<Label>())
            {
                Check(!text.IsVisibleInTree() || card.GetGlobalRect().Encloses(text.GetGlobalRect()), $"图鉴文本超出背景：{id}/{text.Name}");
            }
            RenderingServer.ForceDraw(false);
            DirAccess.MakeDirRecursiveAbsolute("res://.godot/validation/guide_compact_20261102/cards");
            using (Image image = map.GetViewport().GetTexture().GetImage())
            {
                image.SavePng($"res://.godot/validation/guide_compact_20261102/cards/{id}.png");
            }
            map.RemoveChild(card);
            card.Free();
            GD.Print($"SMOKE-LEVEL GUIDE CARD PASS: {id}, {data.Level.NameCn}, drops={data.Drops.Count}");
        }
        await CheckGuideEquipmentDetailsAsync(map);
        foreach (string name in names)
        {
            await MoveMouseAsync(new Vector2(20, 20));
            await WaitUntilAsync(() => !Find<LevelGuideTooltip>().Any(card => card.IsVisibleInTree()), "上一张关卡卡片未关闭");
            MapLocationButton button = map.GetNode<MapLocationButton>("m_" + name);
            Vector2 point = Vector2.Zero;
            bool found = false;
            for (int y = 2; y < button.Size.Y && !found; y += 3)
            {
                for (int x = 2; x < button.Size.X; x += 3)
                {
                    if (button._HasPoint(new Vector2(x, y)))
                    {
                        point = button.GetGlobalTransformWithCanvas() * new Vector2(x, y);
                        found = true;
                        break;
                    }
                }
            }
            Check(found, "地图入口没有可见命中点：" + name);
            await MoveMouseAsync(point);
            await WaitUntilAsync(() => Find<LevelGuideTooltip>().Any(card => card.IsVisibleInTree()), "原生鼠标悬停没有显示关卡图鉴");
            LevelGuideTooltip guide = Find<LevelGuideTooltip>().Single(card => card.IsVisibleInTree());
            for (int frame = 0; frame < 5; frame++)
            {
                await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            EquipmentCell cell = guide.GetNode<GridContainer>("Content/Loot/Drops").GetChildren().OfType<EquipmentCell>().First();
            await MoveMouseAsync(cell.GetGlobalRect().GetCenter());
            GD.Print($"GUIDE HOVER PROBE: {name}, card={guide.GetGlobalRect()}, icon={cell.GetGlobalRect()}, visible={guide.Visible}");
            await CaptureAsync("guide_nested_probe_" + name.ToLowerInvariant());
            await WaitUntilAsync(() => Find<EquipmentTooltip>().Any(item => item.IsVisibleInTree()), "关卡图标没有显示嵌套装备详情");
            Check(guide.IsVisibleInTree(), "移入装备图标时关卡浮层关闭");
            Check(GF.Procedure.CurrentProcedure is ProcedureMap, "悬停图鉴误触发进关");
            await CaptureAsync("guide_hover_" + name.ToLowerInvariant());
            await MoveMouseAsync(new Vector2(20, 20));
            await WaitUntilAsync(() => !Find<LevelGuideTooltip>().Any(card => card.IsVisibleInTree()), "离开地图地点后图鉴仍显示");
        }
        // 图鉴使用定义范围，背包仍显示实例数值，不能为了预览随机生成一份装备。
        EquipmentTooltip tooltip = GD.Load<PackedScene>("res://TheGame/UIs/Inventory/equipment_tooltip.tscn").Instantiate<EquipmentTooltip>();
        tooltip.BindDefinition(GF.Fsm.GetModel<GameLogic.Config.ConfigurationModel>().Tables.TbEquipmentConfig.Get(EquipmentId.CommonPilgrimClothes));
        Check(tooltip.GetNode<VBoxContainer>("Content/Attributes").GetChildren().OfType<Label>().Any(label => label.Text.Contains("20～40")),
            "图鉴没有显示配置属性范围");
        tooltip.Free();
        Vector2I originalSize = DisplayServer.WindowGetSize();
        try
        {
            foreach (Vector2I size in new[] { new Vector2I(940, 590), new Vector2I(1175, 738) })
            {
                DisplayServer.WindowSetSize(size);
                await MoveMouseAsync(new Vector2(20, 20));
                MapLocationButton button = map.GetNode<MapLocationButton>("m_Huaguoshan");
                await MoveMouseAsync(button.GetGlobalRect().GetCenter());
                await WaitUntilAsync(() => Find<LevelGuideTooltip>().Any(card => card.IsVisibleInTree()), "缩放后没有显示卡片");
                LevelGuideTooltip guide = Find<LevelGuideTooltip>().Single(card => card.IsVisibleInTree());
                Check(new Rect2(Vector2.Zero, map.GetViewportRect().Size).Encloses(guide.GetGlobalRect()), "缩放后卡片超出视口");
                await CaptureAsync("guide_scale_" + size.X);
            }
        }
        finally
        {
            DisplayServer.WindowSetSize(originalSize);
        }
        await MoveMouseAsync(new Vector2(20, 20));
        await WaitUntilAsync(() => !Find<LevelGuideTooltip>().Any(card => card.IsVisibleInTree()), "点击夹具旧卡未关闭");
        MapLocationButton first = map.GetNode<MapLocationButton>("m_Huaguoshan");
        await ClickAsync(map, "m_Huaguoshan");
        await WaitUntilAsync(() => GF.Procedure.CurrentProcedure is ProcedureLevel, "图鉴阻挡了原地点的真实点击进关");
        Check(!map.IsVisibleInTree() && !Find<LevelGuideTooltip>().Any(card => card.IsVisibleInTree()), "离开地图后遗留浮层");
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<BattleForm>().Any(form => form.Visible) &&
            GF.Fsm.GetModel<LevelModel>()?.SessionActive == true, "图鉴点击后关卡和HUD未完成加载");
        Find<LevelViewController>().Single().Cleanup();
        for (int frame = 0; frame < 3; frame++)
        {
            await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GD.Print("SMOKE-LEVEL GUIDE PASS: 10 compact 208px cards, named boss-first enemy lists, transparent 32px equipment, no hidden-room disclosure, 10 nested hovers/exits, two sizes and real click-to-level cleanup");
    }

    /// <summary>
    /// 对照官方三状态检查漩涡：普通透明、最新关旋转、悬停显示且离开恢复。
    /// </summary>
    /// <param name="map">正式地图窗口。</param>
    /// <returns>三态渲染与鼠标检查完成。</returns>
    private async Task CheckWhirlpoolStatesAsync(MapForm map)
    {
        ProfileSystem profile = GF.Fsm.GetSystem<ProfileSystem>();
        MapLocationButton button = map.GetNode<MapLocationButton>("m_Shazhiwoliu");
        Sprite2D current = button.GetNode<Sprite2D>("CurrentHint");
        TextureRect glow = button.GetNode<TextureRect>("LocationGlow");
        AnimationPlayer animation = map.GetNode<AnimationPlayer>("m_LocationAnimation");
        using (Image normal = button.TextureNormal.GetImage())
        {
            Check(normal.IsInvisible(), "鲨之涡流普通态不再是官方透明点击区");
        }
        Check(button.Position.IsEqualApprox(new Vector2(573.55f, 427.15f)) &&
            button.Size.IsEqualApprox(new Vector2(386, 164)), "漩涡原版注册位置或尺寸发生偏移");
        for (int id = 1; id <= 8; id++)
        {
            profile.RecordLevelClear(id);
        }
        await MoveMouseAsync(new Vector2(20, 20));
        Check(current.Visible && !glow.Visible && animation.CurrentAnimation == "shazhiwoliu/current",
            "最新关卡未显示官方旋转漩涡");
        Check(current.Hframes == 5 && current.Vframes == 10 &&
            Math.Abs(animation.GetAnimation("shazhiwoliu/current").Length - 2) < .001, "漩涡未使用48帧/24fps原生时间轴");
        int firstFrame = current.Frame;
        for (int frame = 0; frame < 8; frame++)
        {
            await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Check(current.Frame != firstFrame, "最新漩涡提示是静止画面");
        await CaptureAsync("guide_whirlpool_current");
        await MoveMouseAsync(new Vector2(766.55f, 500.15f));
        Check(glow.Visible && !current.Visible && animation.CurrentAnimation == "shazhiwoliu/hover",
            "漩涡透明输入区未触发原版悬停");
        await CaptureAsync("guide_whirlpool_hover");
        await MoveMouseAsync(new Vector2(20, 20));
        await WaitUntilAsync(() => !Find<LevelGuideTooltip>().Any(card => card.IsVisibleInTree()),
            "漩涡三态检查离开后仍残留图鉴");
        Check(current.Visible && !glow.Visible, "离开漩涡后没有恢复最新关动画");
        profile.RecordLevelClear(9);
        Check(!current.Visible && !glow.Visible && button.SelfModulate.A == 1,
            "已通关漩涡未恢复官方透明普通态");
        await CaptureAsync("guide_whirlpool_normal");
        GD.Print("SMOKE-WHIRLPOOL STATES PASS: official invisible normal, animated 48/24 current, native hover and unchanged registration");
    }

    /// <summary>
    /// 逐件渲染正式装备的配置详情，覆盖所有品质、长名称、说明和属性行数。
    /// </summary>
    /// <param name="map">只提供渲染画布，不注入字体以检验详情自身装配。</param>
    /// <returns>布局检查与详情裁图保存完成。</returns>
    private async Task CheckGuideEquipmentDetailsAsync(MapForm map)
    {
        PackedScene scene = GD.Load<PackedScene>("res://TheGame/UIs/Inventory/equipment_tooltip.tscn");
        IReadOnlyList<EquipmentConfig> definitions = GF.Fsm.GetModel<GameLogic.Config.ConfigurationModel>().Tables.TbEquipmentConfig.DataList;
        string output = "res://.godot/validation/guide_visual_20261102/equipment";
        DirAccess.MakeDirRecursiveAbsolute(output);
        foreach (EquipmentConfig definition in definitions)
        {
            EquipmentTooltip tooltip = scene.Instantiate<EquipmentTooltip>();
            tooltip.BindDefinition(definition);
            tooltip.Position = new Vector2(30, 20);
            map.AddChild(tooltip);
            for (int frame = 0; frame < 3; frame++)
            {
                await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            foreach (string row in new[] { "Quality", "Type" })
            {
                Label caption = tooltip.GetNode<Label>($"Content/Header/{row}/Caption");
                Label value = tooltip.GetNode<Label>($"Content/Header/{row}/Value");
                TextureRect line = caption.GetNode<TextureRect>("Underline");
                Check(Math.Abs(caption.GetGlobalRect().GetCenter().Y - value.GetGlobalRect().GetCenter().Y) <= .5 &&
                    caption.GetThemeFont("font") == value.GetThemeFont("font") &&
                    caption.GetThemeFontSize("font_size") == value.GetThemeFontSize("font_size") &&
                    caption.VerticalAlignment == VerticalAlignment.Center && value.VerticalAlignment == VerticalAlignment.Center,
                    $"{definition.Id}/{row} 标题与数值没有按同一行居中：{caption.GetGlobalRect()} / {value.GetGlobalRect()}");
                Check(line.Position == new Vector2(36, 18) && line.Size == new Vector2(80, 2),
                    $"{definition.Id}/{row} 渐隐线位置不正确");
            }
            Label price = tooltip.GetNode<Label>("Content/Footer/Price");
            Check(Math.Abs(tooltip.GetGlobalRect().End.Y - price.GetGlobalRect().End.Y - 6) < 1,
                $"{definition.Id} 详情底部出现多余留白");
            foreach (Label label in tooltip.FindChildren("*", "Label", true, false).OfType<Label>())
            {
                Check(!label.IsVisibleInTree() || tooltip.GetGlobalRect().Encloses(label.GetGlobalRect()),
                    $"{definition.Id}/{label.Name} 文本超出详情背景");
            }
            RenderingServer.ForceDraw(false);
            using (Image image = map.GetViewport().GetTexture().GetImage())
            using (Image crop = image.GetRegion(new Rect2I((Vector2I)tooltip.GlobalPosition, (Vector2I)tooltip.Size)))
            {
                crop.SavePng($"{output}/{(int)definition.Id}.png");
            }
            tooltip.Free();
        }
        Check(definitions.Select(item => item.Quality).Distinct().Count() == 6, "详情夹具未覆盖六种品质");
        GD.Print($"SMOKE-EQUIPMENT DETAILS PASS: {definitions.Count} definitions, six qualities, centered rows, fade lines, variable content and fitted footer");
    }

}
