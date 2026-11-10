using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GameConfig.Battle;
using GameConfig.Constant;
using GameConfig.Entity;
using GameFramework;
using GameFramework.Fsm;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attacks;
using GameLogic.Drops;
using GameLogic.Enemies;
using GameLogic.Enemies.AI;
using GameLogic.Equipment;
using GameLogic.FloatingText;
using GameLogic.Heroes;
using GameLogic.Input;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.Save;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 真实引擎中的单向平台、飞行拾取、暂停和升级画面回归。
/// </summary>
public sealed partial class UiSmokeScenario
{
    private LootOrbNode _previousOrb; // 已回池实例，供下一次击败检查 GGF 实际复用。

    /// <summary>
    /// 读取引擎实际映射，并用真实键盘事件验证单人忽略 P2、方向键不抢 HUD 焦点。
    /// </summary>
    /// <param name="hero">正式流程生成的单人英雄。</param>
    /// <param name="form">已绑定流程的战斗界面。</param>
    /// <returns>映射与原生输入分发检查完成的任务。</returns>
    private async Task CheckSeatInputAsync(AbstractHeroEntity hero, GameLogic.UI.BattleForm form)
    {
        (string Action, Key Key)[] mappings =
        [
            (InputSystem.MoveLeft, Key.Left), (InputSystem.MoveRight, Key.Right),
            (InputSystem.MoveDown, Key.Down), (InputSystem.Interact, Key.Up),
            (InputSystem.Attack, Key.Kp1), (InputSystem.Jump, Key.Kp2),
            (InputSystem.Skill1, Key.Kp4), (InputSystem.Skill2, Key.Kp5),
            (InputSystem.Skill3, Key.Kp6), (InputSystem.Skill4, Key.Kp3),
            (InputSystem.Musou, Key.Kp0), (InputSystem.InventoryToggle, Key.KpDivide),
            (InputSystem.SkillToggle, Key.KpMultiply)
        ];
        int requests = 0;
        Action<int, PlayerSlot> observe = (_, _) => requests++;
        await WaitUntilAsync(() => hero.OnFloor, "输入映射夹具中的主英雄没有落地");
        float originalX = hero.GlobalPosition.X;
        form.InventoryRequested += observe;
        form.SkillsRequested += observe;
        try
        {
            foreach ((string action, Key key) in mappings)
            {
                string name = InputSystem.ActionOf(action, PlayerSlot.P2);
                Godot.Collections.Array<InputEvent> events = InputMap.ActionGetEvents(name);
                Check(events.Count == 1 && events[0] is InputEventKey, "P2 映射缺失或不是单一键盘事件：" + name);
                var mapped = (InputEventKey)events[0];
                Check(mapped.Device == -1 && mapped.Keycode == Key.None && mapped.PhysicalKeycode == key &&
                    Math.Abs(InputMap.ActionGetDeadzone(name) - (action == InputSystem.MoveDown ? .2f : .5f)) < .001f,
                    "P2 原生映射键码、设备或死区错误：" + name);
                GD.Print($"SMOKE-P2 INPUTMAP: {name} physical={(long)mapped.PhysicalKeycode}, device={mapped.Device}");

                // 同时提供逻辑/物理键，真实触发 P2 映射和方向键 UI 导航。
                using var pressed = new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true };
                Input.ParseInputEvent(pressed);
                try
                {
                    await WaitFramesAsync(2);
                    Check(Input.IsActionPressed(name), "原生键盘事件没有命中 P2 动作：" + name);
                    Check(_driver.GetViewport().GuiGetFocusOwner() == null && !GF.Base.IsGamePaused && requests == 0 &&
                        Math.Abs(hero.GlobalPosition.X - originalX) < .1f && hero.ActionData.MoveAxis == 0 &&
                        !hero.ActionData.AttackHeld && !hero.ActionData.JumpHeld && !hero.ActionData.MusouRequested,
                        "单人 P2 键影响主英雄、抢占 HUD 焦点或请求开窗：" + name);
                }
                finally
                {
                    using var released = new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false };
                    Input.ParseInputEvent(released);
                }
                await WaitFramesAsync(1);
            }

            using var accept = new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = true };
            Input.ParseInputEvent(accept);
            using var releaseAccept = new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = false };
            Input.ParseInputEvent(releaseAccept);
            await WaitFramesAsync(2);
            Check(_driver.GetViewport().GuiGetFocusOwner() == null && !GF.Base.IsGamePaused && requests == 0,
                "方向键之后 ui_accept 误触发 HUD 按钮");
            GD.Print("SMOKE-SEAT INPUT PASS: 13 native P2 mappings, single-player ignores P2 keys, arrows keep HUD unfocused and ui_accept opens no window.");
        }
        finally
        {
            form.InventoryRequested -= observe;
            form.SkillsRequested -= observe;
        }
    }

    /// <summary>
    /// 验证地面分支的落地门槛、具体怪物的索敌选择和猴子的受击激怒与复用。
    /// </summary>
    /// <param name="hero">本关实际英雄，测试结束恢复其位置。</param>
    /// <returns>真实地面身体和感知检查完成的任务。</returns>
    private async Task CheckMonsterResponsibilitiesAsync(AbstractHeroEntity hero)
    {
        Vector2 origin = hero.GlobalPosition;
        hero.GlobalPosition = new Vector2(600, 400);
        try
        {
            foreach (EntityId id in new[] { EntityId.HuaguoshanMonkey, EntityId.DemonMonkey, EntityId.Gorilla,
                         EntityId.CaveBrute, EntityId.KnifeDemon })
            {
                _monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id,
                    new EnemySpawn(new Vector2(720, 100)));
                AbstractEnemyEntity enemy = _monster;
                SmokeInspection.FreezeAi(enemy);
                var agent = (IEnemyAiAgent)enemy;
                Check(enemy is AbstractGroundMonsterEntity && !agent.RequestAttack(0),
                    $"{id} 没有使用地面分支的空中出招限制");
                await WaitUntilAsync(() => enemy.OnFloor, $"{id} 未通过地面身体落地");
                hero.GlobalPosition = enemy.GlobalPosition - new Vector2(120, 0);
                await WaitFramesAsync(3);
                Check(agent.HasTarget == (id != EntityId.HuaguoshanMonkey),
                    $"{id} 的主动索敌规则错误");
                if (id == EntityId.HuaguoshanMonkey)
                {
                    var data = AttackData.Create(0, default, 1, DamageKind.Real, Vector2.Zero, hero.Id, 0, source: new GameLogic.Battle.Attacks.AttackSource(hero.Id, GameConfig.Entity.EntityId.None, 0, 0));
                    try
                    {
                        enemy.ReceiveHit(data);
                    }
                    finally { ReferencePool.Release(data); }
                    Check(agent.HasTarget, "猴子受击后没有锁定攻击者");
                    typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(enemy, [null]);
                    await WaitFramesAsync(3);
                    Check(agent.HasTarget, "猴子被激怒后不能重新发现目标");
                }
                Check(agent.RequestAttack(0), $"{id} 落地后拒绝合法攻击");
                await WaitUntilAsync(() => enemy.AttackSegment == 0, $"{id} 未消费攻击请求");
                await WaitUntilAsync(() => SmokeInspection.BodyStateName(enemy) == "Walk", $"{id} 收招未返回地面移动");
                ReleaseTestEntity(enemy);
                _monster = null;
                await WaitFramesAsync(2); // GGF 在帧末完成隐藏并归还可复用池。
                _monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id,
                    new EnemySpawn(new Vector2(720, 100)));
                Check(ReferenceEquals(enemy, _monster), $"{id} 未复用实体");
                SmokeInspection.FreezeAi(_monster);
                await WaitFramesAsync(3);
                if (id == EntityId.HuaguoshanMonkey)
                {
                    Check(!((IEnemyAiAgent)_monster).HasTarget, "猴子复用后遗留激怒和目标");
                }

                ReleaseTestEntity(_monster);
                _monster = null;
            }
            GD.Print("SMOKE-ENEMY-HIERARCHY PASS: five ground species, airborne attack rejection, grounded attack/recovery, monkey provocation/reacquisition and reuse.");
        }
        finally
        {
            if (_monster != null)
            {
                ReleaseTestEntity(_monster);
            }

            _monster = null;
            hero.GlobalPosition = origin;
        }
    }

    /// <summary>
    /// 验证红、蓝、黄球实际拾取、上限、跨类型池复用与关卡目标归属。
    /// </summary>
    /// <param name="flow">测试档案作用域。</param>
    /// <param name="level">正式奖励所有者。</param>
    /// <param name="hero">正式出战英雄。</param>
    /// <returns>真实节点飞行与资源变化验证。</returns>
    private async Task CheckManaPickupAsync(ProcedureOwner flow, LevelViewController level, AbstractHeroEntity hero)
    {
        HashSet<LootOrbNode> owned = SmokeInspection.ReadField<HashSet<LootOrbNode>>(level, "_lootOrbs");
        GF.Fsm.GetSystem<HeroSystem>().TrySpendMp(hero.HeroId, GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Mp.Value - 2);
        LootOrbNode first = SpawnManaOrb(level, hero.GlobalPosition, hero.Id);
        Check(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Mp.Value == 2, "蓝球出生时已直接回蓝");
        Vector2 pausedPosition = first.GlobalPosition;
        GF.Base.PauseGame();
        try
        {
            await WaitFramesAsync(4);
            Check(first.GlobalPosition.IsEqualApprox(pausedPosition) && owned.Contains(first) && GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Mp.Value == 2,
                "暂停时飞行球仍移动或领取");
        }
        finally
        {
            GF.Base.ResumeGame();
        }
        // 飞行开始后改变英雄位置，必须跟随本帧坐标而非出生时的目标点。
        Vector2 originalHeroPosition = hero.GlobalPosition;
        hero.GlobalPosition += new Vector2(-40, 0);
        await WaitUntilAsync(() => owned.Count == 0, "蓝球没有接触英雄", 10000);
        Check(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Mp.Value == Math.Min(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).MaxMp.Value, 2 + flow.GetSystem<LootSystem>().ManaAmount(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).MaxMp.Value)), "接触后回蓝量不符合配置");

        FloatingTextPresenter floating = SmokeInspection.ReadField<FloatingTextPresenter>(level, "_floatingText");
        HashSet<FloatingTextNode> texts = SmokeInspection.ReadField<HashSet<FloatingTextNode>>(floating, "_texts");
        Check(!texts.Any(x => SmokeInspection.ReadField<object>(x, "_kind").ToString() == "Recovery"),
            "蓝量灵魂不应生成恢复飘字");
        hero.GlobalPosition = originalHeroPosition;
        GF.Fsm.GetSystem<HeroSystem>().RestoreMp(hero.HeroId, GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).MaxMp.Value);
        GF.Fsm.GetSystem<HeroSystem>().TrySpendMp(hero.HeroId, 2);
        LootOrbNode reused = SpawnManaOrb(level, hero.GlobalPosition, hero.Id);
        Check(ReferenceEquals(first, reused), "蓝球没有复用同一 GGF 池实例");
        await WaitUntilAsync(() => owned.Count == 0, "复用蓝球没有结束飞行", 10000);
        Check(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Mp.Value == GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).MaxMp.Value, "蓝球超出上限或复用后未正确回蓝");

        // 生命球复用刚归还的蓝球实例，暂停不能移动或提前恢复生命。
        HeroState model = flow.GetModel<HeroModel>().GetState(hero.HeroId);
        int health = flow.GetSystem<LootSystem>().HealthAmount(model.MaxHp.Value);
        // 红球数值检查隔离场内敌人的受击，球仍按原受击盒几何追踪；退出时恢复原层。
        uint hurtLayer = hero.HurtBox.CollisionLayer;
        hero.HurtBox.CollisionLayer = 0;
        try
        {
            model.Hp.Value = 1;
            LootOrbNode red = SpawnOrb(level, hero.GlobalPosition, new(LootKind.Health, health), hero.Id);
            Check(ReferenceEquals(reused, red) && red.Animation == LootAnims.Health && model.Hp.Value == 1,
                "蓝球复用为红球后动画/奖励未刷新或提前回血");
            Vector2 redPosition = red.GlobalPosition;
            GF.Base.PauseGame();
            try
            {
                await WaitFramesAsync(4);
                Check(red.GlobalPosition.IsEqualApprox(redPosition) && model.Hp.Value == 1, "暂停时红球仍在飞行或回血");
            }
            finally
            {
                GF.Base.ResumeGame();
            }
            await WaitUntilAsync(() => owned.Count == 0, "红球未接触英雄", 10000);
            Check(model.Hp.Value == Math.Min(model.MaxHp.Value, 1 + health),
                $"红球实际恢复量错误：actual={model.Hp.Value}, expected={Math.Min(model.MaxHp.Value, 1 + health)}, " +
                $"alive={hero.IsAlive}, physics={hero.IsPhysicsProcessing()}, regen={model.HpRegen.Value}, " +
                $"recovery={model.RecoveryProgress}, position={hero.GlobalPosition}, outcome={SmokeInspection.Model(level).Outcome.Value}");
            foreach (int hp in new[] { model.MaxHp.Value - 1, model.MaxHp.Value })
            {
                model.Hp.Value = hp;
                LootOrbNode clamped = SpawnOrb(level, hero.GlobalPosition, new(LootKind.Health, health), hero.Id);
                Check(ReferenceEquals(red, clamped), "红球未复用原池实例");
                await WaitUntilAsync(() => owned.Count == 0, "满血附近红球未回池", 10000);
                Check(model.Hp.Value == model.MaxHp.Value, "红球超出生命上限或满血无法回收");
            }
        }
        finally
        {
            hero.HurtBox.CollisionLayer = hurtLayer;
        }

        // 黄球只增加无双，不能再次增加经验；开启期间也允许拾取补充。
        BattleConfig battle = ConfigSystem.Instance.Tables.TbBattleConfig.Data;
        GF.Fsm.GetSystem<HeroSystem>().ClearWs(hero.HeroId);
        GF.Fsm.GetSystem<HeroSystem>().AddWs(hero.HeroId, GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).MaxWs.Value);
        Check(hero.TryActivateMusou(), "黄球回归前未开启无双");
        await WaitUntilAsync(() => !GF.Base.IsGamePaused, "黄球回归的开启停顿未结束", 4000);
        GF.Fsm.GetSystem<HeroSystem>().AdvanceWs(hero.HeroId, 2);
        int musou = GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Ws.Value;
        int experience = flow.GetModel<ProfileModel>().GetProgress(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.TotalExperience;
        SpawnOrb(level, hero.GlobalPosition, new(LootKind.Musou, ConfigSystem.Instance.Tables.TbBattleConfig.Data.MusouPerOrb), hero.Id);
        await WaitUntilAsync(() => owned.Count == 0, "黄球没有接触英雄", 10000);
        Check(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Ws.Value == musou + 5 && flow.GetModel<ProfileModel>().GetProgress(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.TotalExperience == experience,
            "开启期间的黄球没有补充5无双，或错误增加经验");
        hero._PhysicsProcess(battle.WsMax / battle.WsDrainPerSecond + 1f);

        _extraHero = await ShowHeroAsync(flow, flow.GetModel<ProfileModel>().GetProgress(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.TotalExperience);
        // 两个表现节点借用同一 HeroModel，不能通过击杀额外节点来伪造独立生命。
        // 用明显不同的位置验证球的飞行目标，不改动本关生命状态。
        _extraHero.GlobalPosition = hero.GlobalPosition + new Vector2(400, 0);
        GF.Fsm.GetSystem<HeroSystem>().TrySpendMp(hero.HeroId, GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Mp.Value - 2);
        LootOrbNode targetProbe = SpawnManaOrb(level, _extraHero.GlobalPosition, hero.Id);
        Check(targetProbe.OwnerEntityId == hero.Id, "飞行球没有保存指定归属英雄编号");
        Vector2 lastPosition = targetProbe.GlobalPosition;
        await WaitUntilAsync(() =>
        {
            if (owned.Contains(targetProbe))
            {
                lastPosition = targetProbe.GlobalPosition;
            }

            return owned.Count == 0;
        }, "飞行球没有跟随关卡所有者的实际英雄", 10000);
        Check(lastPosition.DistanceTo(hero.GlobalPosition) < lastPosition.DistanceTo(_extraHero.GlobalPosition),
            "球飞向了额外显示的英雄，没有飞向本关实际英雄");
        Check(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Mp.Value == Math.Min(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).MaxMp.Value, 2 + flow.GetSystem<LootSystem>().ManaAmount(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).MaxMp.Value)),
            "球错误跟随了同场景的其他英雄，未向本关英雄发放奖励");
        Check(targetProbe.OwnerEntityId == 0, "飞行球领取后归还节点池没有清零归属");

        // 反向指定现有额外节点，确认目标由每颗球的编号决定，而非统一使用关卡主节点。
        model.Mp.Value = 2;
        LootOrbNode personalProbe = SpawnManaOrb(level, hero.GlobalPosition, _extraHero.Id);
        lastPosition = personalProbe.GlobalPosition;
        await WaitUntilAsync(() =>
        {
            if (owned.Contains(personalProbe))
            {
                lastPosition = personalProbe.GlobalPosition;
            }
            return !owned.Contains(personalProbe);
        }, "指定额外节点的飞行球没有完成追踪", 10000);
        Check(lastPosition.DistanceTo(_extraHero.GlobalPosition) < lastPosition.DistanceTo(hero.GlobalPosition) &&
            model.Mp.Value == Math.Min(model.MaxMp.Value, 2 + flow.GetSystem<LootSystem>().ManaAmount(model.MaxMp.Value)),
            "飞行球没有跟随自身归属节点，或回池清零后丢失了奖励");

        // 用已有额外表现节点验证目标离场；它与本关英雄共用数值，不能伪造独立死亡。
        int soul = flow.GetModel<ProfileModel>().GetSoul(hero.HeroId).Value;
        LootOrbNode orphan = SpawnOrb(level, _extraHero.GlobalPosition, new(LootKind.Soul, 50), _extraHero.Id);
        ReleaseTestEntity(_extraHero);
        _extraHero = null;
        await WaitUntilAsync(() => !owned.Contains(orphan), "归属英雄离场后飞行球没有回收");
        Check(!orphan.Visible && orphan.OwnerEntityId == 0 && flow.GetModel<ProfileModel>().GetSoul(hero.HeroId).Value == soul,
            "归属失效的球没有清零回收，或错误改发给本关英雄");
        GD.Print("SMOKE-ORB OWNERSHIP PASS: explicit target, reward before pool reset, owner cleared, vanished owner reclaimed without reward.");
        // 留一颗正在等待的球，调用方随后通过正式 Cleanup 验证卸载回收。
        SpawnManaOrb(level, hero.GlobalPosition, hero.Id);
        SpawnOrb(level, hero.GlobalPosition, new(LootKind.Health, health), hero.Id);
        GD.Print("SMOKE-UI: health/mana/musou pickup/clamp/pool reuse/scene-owned target selection passed");
    }

    /// <summary>
    /// 只在测试侧通过反射把蓝球挂入正式所有权，避免为随机测试增加生产接口。
    /// </summary>
    /// <param name="level">实际结算所有者。</param>
    /// <param name="position">本次出生位置。</param>
    /// <param name="ownerEntityId">本次球的归属英雄实体编号。</param>
    /// <returns>正在飞行的池实例。</returns>
    private static LootOrbNode SpawnManaOrb(LevelViewController level, Vector2 position, int ownerEntityId)
    {
        return SpawnOrb(level, position, new(LootKind.Mana,
            GF.Fsm.GetSystem<LootSystem>().ManaAmount(GF.Fsm.GetModel<HeroModel>().GetState(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).MaxMp.Value)), ownerEntityId);
    }

    /// <summary>
    /// 在测试中生成指定球并接入当前关卡实际的拾取回调。
    /// </summary>
    /// <param name="level">实际奖励所有者。</param>
    /// <param name="position">本次出生位置。</param>
    /// <param name="reward">受测奖励，不向生产代码增加观测入口。</param>
    /// <param name="ownerEntityId">本次球的归属英雄实体编号。</param>
    /// <returns>真实池实例。</returns>
    private static LootOrbNode SpawnOrb(LevelViewController level, Vector2 position, LootReward reward, int ownerEntityId)
    {
        LootOrbNode orb = GodotGameFramework.NodePool.NodePool.Instance.RentNode<LootOrbNode>(ResourcesCollectionConstant.Drops_loot_orb, SmokeInspection.Scene(level));
        Check(orb.OwnerEntityId == 0, "飞行球取池时遗留旧归属");
        SmokeInspection.ReadField<HashSet<LootOrbNode>>(level, "_lootOrbs").Add(orb);
        MethodInfo finish = typeof(LevelViewController).GetMethod("OnLootFinished", BindingFlags.Instance | BindingFlags.NonPublic)!;
        orb.Show(position + new Vector2(180, -30), reward, ownerEntityId,
            (node, reward) => finish.Invoke(level, [node, reward]));
        return orb;
    }

    /// <summary>
    /// 在真实妖猴身体和播放器上验证重新进入追击不会插入持棒待机帧。
    /// </summary>
    /// <param name="hero">静止的真实目标。</param>
    /// <returns>两次追击进入的引擎回归。</returns>
    private async Task CheckDemonChaseAsync(AbstractHeroEntity hero)
    {
        Vector2 origin = hero.GlobalPosition;
        hero.GlobalPosition = new Vector2(300, 100);
        _monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.DemonMonkey,
            new EnemySpawn(new Vector2(600, 100)));
        _monster.SetPhysicsProcess(true); // 前一声音夹具使用同一池实例时冻结过身体，测试侧显式恢复。
        await WaitUntilAsync(() => _monster.OnFloor, "妖猴追击夹具没有落地");
        hero.GlobalPosition = _monster.GlobalPosition - new Vector2(300, 0);
        typeof(AbstractEnemyEntity).GetField("_target", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_monster, hero);
        IFsm<IEnemyAiAgent> fsm = SmokeInspection.ReadField<IFsm<IEnemyAiAgent>>(_monster, "AiFsm");
        AnimatedSprite2D body = _monster.GetNode<AnimatedSprite2D>("m_Body");
        FieldInfo desire = _monster.Config.GetType().GetField("AttackDesire")!;
        int originalDesire = _monster.Config.AttackDesire;
        desire.SetValue(_monster.Config, 0); // 只检查实际追击渲染，不让随机合法出招混入夹具。
        try
        {
            foreach (float direction in new[] { -1f, 1f })
            {
                hero.GlobalPosition = _monster.GlobalPosition + new Vector2(direction * 300, 0);
                await WaitUntilAsync(() => _monster.AiData.MoveIntent == (int)direction, "妖猴没有自主转向追击");
                await WaitFramesAsync(2);
                for (int frame = 0; frame < 16; frame++)
                {
                    hero.GlobalPosition = _monster.GlobalPosition + new Vector2(direction * (frame % 2 == 0 ? 140 : 300), 0);
                    await _driver.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    Check(fsm.CurrentState is EngageState && body.Animation == "run" && _monster.AttackSegment < 0,
                        $"合法追击中闪出举棒：AI={fsm.CurrentState.GetType().Name}，身体={body.Animation}");
                }
                // 原版妖猴交战高度为220px；超限后游荡可以停步，但不能提前进入攻击动作。
                for (int frame = 0; frame < 30; frame++)
                {
                    hero.GlobalPosition = _monster.GlobalPosition + new Vector2(direction * 30, -240);
                    await _driver.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    Check(_monster.AttackSegment < 0 && body.Animation != "attack_1", "高处不可达目标引起一帧举棒");
                }
            }
        }
        finally
        {
            desire.SetValue(_monster.Config, originalDesire);
            hero.GlobalPosition = origin;
        }
        ReleaseTestEntity(_monster);
        _monster = null;
        GD.Print("SMOKE-UI: 妖猴自主左右追击与高处不可达目标逐渲染帧检查通过，无瞬间举棒");
    }

    /// <summary>
    /// 用真实 CharacterBody2D 分别穿过平台底部并落在平台上。
    /// </summary>
    /// <param name="level">平台所在关卡。</param>
    /// <param name="flow">用于创建独立测试英雄的档案作用域。</param>
    /// <returns>三个物理探针及英雄下穿完成的任务。</returns>
    private async Task CheckPlatformsAsync(LevelViewController level, ProcedureOwner flow)
    {
        foreach (StaticBody2D platform in SmokeInspection.Scene(level).GetNode("World/Platforms").GetChildren())
        {
            var probe = new CharacterBody2D { CollisionLayer = 0, CollisionMask = 8 };
            probe.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(20, 20) } });
            SmokeInspection.Scene(level).AddChild(probe);
            try
            {
                CollisionShape2D surface = platform.GetNode<CollisionShape2D>("Surface");
                Rect2 bounds = surface.GlobalTransform * surface.Shape.GetRect();
                float centerX = bounds.GetCenter().X;
                float surfaceTop = bounds.Position.Y;
                probe.GlobalPosition = new Vector2(centerX, surfaceTop + 60);
                probe.Velocity = new Vector2(0, -500);
                bool above = false;
                for (int frame = 0; frame < 150; frame++)
                {
                    await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                    probe.Velocity += new Vector2(0, 900f / Engine.PhysicsTicksPerSecond);
                    probe.MoveAndSlide();
                    above |= probe.GlobalPosition.Y < surfaceTop - 20;
                    if (above && probe.IsOnFloor())
                    {
                        break;
                    }
                }
                float landingY = surfaceTop - 10f;
                Check(above && probe.IsOnFloor() && Math.Abs(probe.GlobalPosition.Y - landingY) < 1.1f,
                    $"平台 {platform.Name} 无法从下方穿过并落到上方：{probe.GlobalPosition}");
            }
            finally
            {
                probe.QueueFree();
            }
        }
        _extraHero = await ShowHeroAsync(flow, flow.GetModel<ProfileModel>().GetProgress(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.TotalExperience);
        AbstractHeroEntity hero = _extraHero;
        try
        {
            StaticBody2D platform = SmokeInspection.Scene(level).GetNode("World/Platforms").GetChildren().OfType<StaticBody2D>().First();
            CollisionShape2D bodyShape = hero.GetNode<CollisionShape2D>("CollisionShape2D");
            CollisionShape2D platformSurface = platform.GetNode<CollisionShape2D>("Surface");
            Rect2 platformBounds = platformSurface.GlobalTransform * platformSurface.Shape.GetRect();
            float feetOffset = bodyShape.Position.Y + bodyShape.Shape.GetRect().End.Y;
            float surfaceTop = platformBounds.Position.Y;
            float centerX = platformBounds.GetCenter().X;
            hero.GlobalPosition = new Vector2(centerX, surfaceTop - feetOffset - 40f);
            hero.Velocity = Vector2.Zero;
            hero.SetPhysicsProcess(true);
            // 等两次帧信号，确保至少完成一次移位后的物理更新。
            for (int frame = 0; frame < 2; frame++)
            {
                await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
            }

            for (int frame = 0; frame < 120 && !hero.IsOnFloor(); frame++)
            {
                await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
            }

            Check(hero.IsOnFloor() && Enumerable.Range(0, hero.GetSlideCollisionCount()).Any(index =>
                            ReferenceEquals(hero.GetSlideCollision(index).GetCollider(), platform)), "英雄未落在可穿平台");
            float before = hero.GlobalPosition.Y;
            Input.ActionPress(InputSystem.MoveDown);
            Input.ActionPress(InputSystem.Jump);
            for (int frame = 0; frame < 20; frame++)
            {
                await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
            }

            Check(hero.GlobalPosition.Y > before + hero.Config.PlatformDropDistance && hero.Velocity.Y >= 0,
                            $"S+K 未从单向平台向下离开，或误触起跳：before={before}, after={hero.GlobalPosition.Y}, vy={hero.Velocity.Y}, floor={hero.IsOnFloor()}");
        }
        finally
        {
            Input.ActionRelease(InputSystem.MoveDown);
            Input.ActionRelease(InputSystem.Jump);
            ReleaseTestEntity(_extraHero);
            _extraHero = null;
        }
        GD.Print("SMOKE-UI: three official one-way platforms and hero S+K drop passed real physics");
    }

    /// <summary>
    /// 观测真正的在场球：未接触不入账，暂停不前进，恢复后追踪移动中的英雄。
    /// </summary>
    /// <param name="level">奖励所有者。</param>
    /// <param name="hero">本关英雄。</param>
    /// <returns>飞行中行为检查任务。</returns>
    private async Task CheckFlyingLootAsync(LevelViewController level, AbstractHeroEntity hero)
    {
        HashSet<LootOrbNode> orbs = SmokeInspection.ReadField<HashSet<LootOrbNode>>(level, "_lootOrbs");
        Check(orbs.Count is >= 2 and <= 13, "怪物死亡掉落数量超出官方范围");
        Check(orbs.All(orb => orb.OwnerEntityId == hero.Id), "正式死亡掉落没有写入命中英雄归属");
        if (_previousOrb != null)
        {
            Check(orbs.Contains(_previousOrb), "下一次击败未复用已归还的飞行球");
        }

        _previousOrb = orbs.First();
        var positions = orbs.ToDictionary(x => x, x => x.GlobalPosition);
        double elapsed = SmokeInspection.Model(level).ElapsedSeconds;
        GF.Base.PauseGame();
        try
        {
            // 暂停可能发生在delta已经采样的物理/流程回调中，验证同帧旧delta也不能推进关卡。
            level._Process(1.0 / 60.0);
            Check(SmokeInspection.Model(level).ElapsedSeconds == elapsed, "暂停当帧仍推进关卡计时");
            await _driver.ToSignal(_driver.GetTree().CreateTimer(.3, true, false, true), SceneTreeTimer.SignalName.Timeout);
            Check(positions.All(x => x.Key.GlobalPosition.IsEqualApprox(x.Value)), "暂停时飞行球仍在运动");
        }
        finally
        {
            GF.Base.ResumeGame();
        }

        // 跟踪目标换位置，后续调用方等待真正接触后的档案变化。
        hero.GlobalPosition += new Vector2(-40, 0);
        foreach (AbstractEnemyEntity monster in _driver.GetTree().Root.FindChildren("*", "CharacterBody2D", true, false).OfType<AbstractEnemyEntity>())
        {
            SmokeInspection.FreezeAi(monster);
        }

        await CaptureCombatAsync("flying_loot");
        GD.Print($"SMOKE-LOOT PAUSE {Engine.PhysicsTicksPerSecond}Hz PASS: stale delta rejected, clock and native flying orbs frozen, resume tracks hero");
    }

    /// <summary>
    /// 有渲染器时保留实际游戏截图，headless 时只执行状态与物理断言。
    /// </summary>
    /// <param name="name">证据文件名称。</param>
    /// <returns>渲染帧保存任务。</returns>
    private Task CaptureCombatAsync(string name)
    {
        if (DisplayServer.GetName() == "headless")
        {
            return Task.CompletedTask;
        }

        // 主动渲染当前测试状态，避免窗口不再重绘时永远等不到FramePostDraw。
        RenderingServer.ForceDraw(false);
        DirAccess.MakeDirRecursiveAbsolute("res://.godot/validation");
        Check(_driver.GetViewport().GetTexture().GetImage().SavePng($"res://.godot/validation/{name}.png") == Error.Ok,
            "战斗反馈截图保存失败");
        return Task.CompletedTask;
    }
}
