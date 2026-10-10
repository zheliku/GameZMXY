using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameConfig;
using GameConfig.Constant;
using GameConfig.Entity;
using GameFramework.Event;
using GameFramework.Fsm;
using GameFramework.Procedure;
using GameLogic.Entity;
using GameLogic.Entity.Heroes;
using GameLogic.Entity.Monsters;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Session;
using GameLogic.UI;
using GameLogic.UI.Widgets;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.Json;
using GodotGameFramework.UI;
using Newtonsoft.Json;

/// <summary>
/// 用真实 GGF 窗口、对象池、流程与隔离存档验证：HUD 聚合刷新与退订、实体复用、升级注入、
/// 关卡事务（中途离开回滚不写盘、死亡与通关写盘）、存档迁移/原子写/备份回退与流程重入。
/// </summary>
public sealed class UiSmokeScenario
{
    private readonly Node m_Driver; // 提供真实引擎帧信号的烟测节点。
    private readonly Task m_RunTask; // 已捕获异常的测试任务。
    private HeroEntity m_ExtraHero; // 测试创建的额外英雄，结束时归还。
    private MonsterEntity m_Monster; // 复用验证创建的怪物，结束时归还。
    private BattleHud m_TestHud; // 当前由测试打开的 HUD，结束时关闭。

    /// <summary>开始一次 UI 与存档回归，原流程与测试使用同一套框架服务。</summary>
    /// <param name="driver">烟测帧驱动节点。</param>
    public UiSmokeScenario(Node driver)
    {
        m_Driver = driver;
        m_RunTask = RunAsync();
    }

    /// <summary>回归是否已结束。</summary>
    public bool IsDone => m_RunTask.IsCompleted;

    /// <summary>首个失败原因，成功时为 null。</summary>
    public string Failure { get; private set; }

    /// <summary>依次验证显示、解绑、复用、升级、关卡事务、存档与重入。</summary>
    /// <returns>整个回归完成的任务，异常被转换为 Failure。</returns>
    private async Task RunAsync()
    {
        try
        {
            ProcedureLevel procedure = await WaitForLevelAsync();
            Check(!GF.Debugger.ActiveWindow, "框架调试入口仍然遮挡游戏 HUD");
            GameContext context = SmokeInspection.Context(procedure);
            HeroEntity hero = FindHero();
            LevelController level = FindLevel();
            BattleHud hud = CurrentHud();
            hero.SetPhysicsProcess(false);

            // 首次启动按建档规则新建并立即写回为当前版本。
            GameData onDisk = await ReadDiskAsync();
            Check(onDisk?.SaveVersion == SaveMigrator.CurrentVersion && onDisk.Profile?.Heroes.Count == 1 &&
                onDisk.Player == null, "首次启动未按当前版本写回新档");
            CheckDisplay(hud, hero, context.Profile.ActiveHero.Progression, level);

            await CheckHudLifecycleAsync(hud, hero, context, level);
            await CheckEntityReuseAsync(context);
            await CheckLevelUpAsync(context, level);

            // 中途离开：关内收益回滚，不写盘；重入读回进关前状态。
            HeroProgression progression = context.Profile.ActiveHero.Progression;
            int before = progression.TotalExperience;
            await DefeatMonsterAsync(level, procedure);
            Check(progression.TotalExperience > before, "击败怪物没有把经验写入档案");
            Check((await ReadDiskAsync()).Profile.Heroes[0].TotalExperience == before, "关内收益在结算前就写盘了");
            ReenterLevel();
            procedure = await WaitForLevelAsync();
            Check(ReferenceEquals(SmokeInspection.Context(procedure), context), "重入关卡没有沿用同一档案作用域");
            Check(progression.TotalExperience == before, "中途离开没有回滚关内收益");

            // 死亡：提交本关收益（写盘），延迟后自动重开。关卡运行在击杀前捕获，重开后流程会换成新运行。
            await DefeatMonsterAsync(FindLevel(), procedure);
            int afterKill = progression.TotalExperience;
            LevelRun deathRun = SmokeInspection.Run(procedure);
            KillHero(FindHero());
            Check(deathRun.Outcome == LevelRunOutcome.Defeated, $"英雄死亡没有结束关卡运行：{deathRun.Outcome}");
            Check(await deathRun.Commit, "死亡检查点写入失败");
            Check((await ReadDiskAsync()).Profile.Heroes[0].TotalExperience == afterKill, "死亡没有保存本关收益");
            await WaitUntilAsync(() => SmokeInspection.Run(procedure) != null && !ReferenceEquals(SmokeInspection.Run(procedure), deathRun) &&
                SmokeInspection.Run(procedure).Outcome == LevelRunOutcome.Running && CurrentHud() != null,
                "死亡后没有自动重开关卡", 15000);
            Check(progression.TotalExperience == afterKill, "死亡重开后档案收益丢失");

            await CheckBackupRecoveryAsync(context);
            await CheckLegacyMigrationAsync();
            await CheckLoadingCancellationAsync();
            await CheckHudOpenFailureAsync();
            GD.Print("SMOKE-UI: HUD 聚合刷新/退订、实体与窗口复用、升级注入、关卡事务、存档迁移与备份回退、重入验证完成");
        }
        catch (Exception error)
        {
            Failure = error.ToString();
        }
        finally
        {
            CloseHud(m_TestHud);
            ReleaseTestEntity(m_ExtraHero);
            ReleaseTestEntity(m_Monster);
        }
    }

    /// <summary>HUD 打开时拉取一致快照、变化时刷新、关闭后不再响应、窗口池复用、改绑到其他状态源。</summary>
    /// <param name="hud">正式流程已打开的 HUD。</param>
    /// <param name="hero">正式英雄。</param>
    /// <param name="context">档案作用域。</param>
    /// <param name="level">正式关卡。</param>
    /// <returns>验证完成的任务。</returns>
    private async Task CheckHudLifecycleAsync(BattleHud hud, HeroEntity hero, GameContext context, LevelController level)
    {
        HeroProgression progression = context.Profile.ActiveHero.Progression;
        CloseHud(hud);
        await WaitFramesAsync(2);

        // 打开前改变状态，验证打开时读取当前值而不是依赖后续事件。
        hero.Vitals.Damage(30);
        hero.Musou.Add(35);
        m_TestHud = await OpenHudAsync(new BattleHudData(hero, progression, level));
        Check(ReferenceEquals(hud, m_TestHud), "HUD 未从真实窗口池复用");
        CheckDisplay(m_TestHud, hero, progression, level);

        ResourceBar bar = m_TestHud.GetNode<ResourceBar>("StatusPanel/m_HpBar");
        hero.Vitals.Damage(20);
        hero.Musou.Add(20);
        CheckDisplay(m_TestHud, hero, progression, level);
        await CheckMusouAnimationAsync(m_TestHud, hero, progression);

        // 关闭后修改状态源，控件保持关闭时的显示。
        CloseHud(m_TestHud);
        AnimatedSprite2D fullAnimation = hud.GetNode<AnimatedSprite2D>("MenuPanel/m_WsMax");
        Check(!fullAnimation.Visible && !fullAnimation.IsPlaying() && fullAnimation.Frame == 0,
            "HUD 关闭后无双动画没有停止并复位");
        double closedHp = bar.Value;
        string closedLevel = hud.GetNode<Label>("StatusPanel/m_LevelLabel").Text;
        hero.Vitals.Damage(5);
        progression.AddExperience(140);
        Check(Near(bar.Value, closedHp), "关闭后血条仍响应旧状态源");
        Check(hud.GetNode<Label>("StatusPanel/m_LevelLabel").Text == closedLevel, "关闭后等级仍响应旧状态源");
        progression.Restore(0);
        await WaitFramesAsync(2);

        // 改绑到另一英雄：旧英雄的变化不再影响显示。
        m_TestHud = await OpenHudAsync(new BattleHudData(hero, progression, level));
        Check(fullAnimation.Visible && fullAnimation.IsPlaying(), "HUD 复用后没有恢复已蓄满的无双动画");
        m_ExtraHero = await ShowHeroAsync(context, new HeroRecord(1, new HeroProgression(context.Curve, 517)));
        HeroProgression extraProgression = new(context.Curve, 517);
        GF.UI.RefocusUIForm(m_TestHud, new BattleHudData(m_ExtraHero, extraProgression, level));
        CheckDisplay(m_TestHud, m_ExtraHero, extraProgression, level);
        Check(!fullAnimation.Visible && !fullAnimation.IsPlaying(), "改绑未蓄满的英雄后残留旧无双动画");
        hero.Vitals.Damage(1);
        CheckDisplay(m_TestHud, m_ExtraHero, extraProgression, level);
        CloseHud(m_TestHud);
        ReleaseTestEntity(m_ExtraHero);
        m_ExtraHero = null;

        // 正式英雄恢复满血并重新打开正式 HUD，后续关卡事务用例从干净状态开始。
        hero.Heal(hero.Vitals.MaxHp);
        m_TestHud = await OpenHudAsync(new BattleHudData(hero, progression, level));
        await WaitFramesAsync(2);
    }

    /// <summary>验证无双只有蓄满才连续循环，消耗和零上限会停止，经验变化不触发播放。</summary>
    /// <param name="hud">当前打开的真实 HUD。</param>
    /// <param name="hero">HUD 绑定的英雄，验证后保持蓄满以继续检查关闭与复用。</param>
    /// <param name="progression">HUD 绑定的成长，经验验证后恢复原值。</param>
    /// <returns>真实动画至少循环两次后的验证任务。</returns>
    private async Task CheckMusouAnimationAsync(BattleHud hud, HeroEntity hero, HeroProgression progression)
    {
        AnimatedSprite2D animation = hud.GetNode<AnimatedSprite2D>("MenuPanel/m_WsMax");
        ResourceBar bar = hud.GetNode<ResourceBar>("MenuPanel/WsFrame/m_WsBar");
        Check(!animation.Visible && !animation.IsPlaying(), "无双未蓄满就显示或播放了闪烁动画");
        int originalExperience = progression.TotalExperience;
        progression.AddExperience(1);
        await WaitFramesAsync(3);
        Check(!animation.Visible && !animation.IsPlaying(), "获得经验错误地触发了无双闪烁动画");
        progression.Restore(originalExperience);

        // 保持数值不变，直接观察引擎循环信号，避免只检查 Play 标志而漏掉停帧。
        int loops = 0;
        Action onLooped = () => loops++;
        animation.AnimationLooped += onLooped;
        try
        {
            hero.Musou.Add(hero.Musou.Max);
            Check(animation.Visible && animation.IsPlaying(), "无双蓄满后没有开始闪烁");
            await WaitUntilAsync(() => loops >= 2, "无双蓄满后没有在数值不变时连续循环");
            int frame = animation.Frame;
            float frameProgress = animation.FrameProgress;
            bar.SetValue(hero.Musou.Value, hero.Musou.Max);
            Check(animation.Frame == frame && animation.FrameProgress == frameProgress,
                "重复刷新满条错误地重置了动画进度");
        }
        finally
        {
            animation.AnimationLooped -= onLooped;
        }

        // 消耗与零上限都应隐藏并复位；重新蓄满留给调用方验证窗口池复用。
        Check(hero.Musou.TryConsume(), "烟测未能消耗已蓄满的无双");
        await WaitFramesAsync(3);
        Check(!animation.Visible && !animation.IsPlaying() && animation.Frame == 0 && Near(bar.Value, 0),
            "无双消耗后动画没有停止并复位");
        int originalMaximum = hero.Musou.Max;
        hero.Musou.Reset(0);
        Check(!animation.Visible && !animation.IsPlaying(), "零上限错误地触发了满值动画");
        hero.Musou.Reset(originalMaximum);
        hero.Musou.Add(originalMaximum);
        GD.Print("SMOKE-UI: 无双未满/经验变化不闪烁、满值连续循环、消耗与零上限复位验证完成");
    }

    /// <summary>池化实体复用时按装配/配置重建属性，不残留上次的数值或修正来源。</summary>
    /// <param name="context">档案作用域。</param>
    /// <returns>验证完成的任务。</returns>
    private async Task CheckEntityReuseAsync(GameContext context)
    {
        // 正式英雄占用一个池实例；先关掉测试 HUD 以免它观察额外英雄。
        CloseHud(m_TestHud);
        m_TestHud = null;
        m_ExtraHero = await ShowHeroAsync(context, new HeroRecord(1, new HeroProgression(context.Curve, 0)));
        HeroEntity previous = m_ExtraHero;
        await CheckMusouActivationAsync(previous);
        var vitals = previous.Vitals;
        previous.Vitals.Damage(70);
        previous.Musou.Add(70);
        previous.Musou.Add(previous.Musou.Max);
        Check(previous.TryActivateMusou(), "复用测试前未开启无双");
        previous.Stats.SetSource(new GameLogic.Battle.Stats.StatSource("Buff", 1),
            new[] { GameLogic.Battle.Stats.StatModifier.Flat(GameConfig.Stat.StatType.MaxHp, 999) });
        ReleaseTestEntity(previous);
        await WaitFramesAsync(2);

        // 2 级装配：生命上限来自成长表 2 级行。
        m_ExtraHero = await ShowHeroAsync(context, new HeroRecord(1, new HeroProgression(context.Curve, 140)));
        int expectedMaxHp = ConfigSystem.Instance.Tables.TbHeroGrowthConfig.Get(1, 2).Stats.MaxHp;
        Check(ReferenceEquals(previous, m_ExtraHero) && ReferenceEquals(vitals, m_ExtraHero.Vitals), "英雄运行时容器未随池实例稳定复用");
        Check(m_ExtraHero.Level == 2 && m_ExtraHero.Vitals.MaxHp == expectedMaxHp &&
            m_ExtraHero.Vitals.Hp == expectedMaxHp && m_ExtraHero.Musou.Value == 0 &&
            m_ExtraHero.Musou.Max == ConfigSystem.Instance.Tables.TbBattleConfig.Data.WsMax,
            "英雄复用残留上次的修正、生命或无双");
        bool rejected = false;
        try
        {
            m_ExtraHero.ApplyLoadout(new HeroLoadout(99, 1, ConfigSystem.Instance.Tables.TbHeroGrowthConfig.Get(1, 1).Stats,
                Array.Empty<System.Collections.Generic.KeyValuePair<GameLogic.Battle.Stats.StatSource,
                    System.Collections.Generic.IReadOnlyList<GameLogic.Battle.Stats.StatModifier>>>()), refill: true);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        Check(rejected, "英雄接受了其他英雄的装配");
        float power = m_ExtraHero.Stats.Get(GameConfig.Stat.StatType.Power);
        m_ExtraHero.Musou.Add(m_ExtraHero.Musou.Max);
        Check(m_ExtraHero.TryActivateMusou(), "死亡测试前未开启无双");
        KillHero(m_ExtraHero);
        Check(!m_ExtraHero.Musou.IsActive && m_ExtraHero.Musou.Value == 0 &&
            Near(m_ExtraHero.Stats.Get(GameConfig.Stat.StatType.Power), power), "死亡没有结束无双或移除局内加成");
        ReleaseTestEntity(m_ExtraHero);
        m_ExtraHero = null;

        await CheckMonsterHealthBarsAsync();
        await WaitFramesAsync(2);
    }

    /// <summary>验证空格真实输入、无双战斗效果、残影世界位置与耗尽还原。</summary>
    /// <param name="hero">由实体池显示的额外英雄，不影响正式关卡档案。</param>
    /// <returns>引擎输入和残影淡出验证完成的任务。</returns>
    private async Task CheckMusouActivationAsync(HeroEntity hero)
    {
        var battle = ConfigSystem.Instance.Tables.TbBattleConfig.Data;
        float power = hero.Stats.Get(GameConfig.Stat.StatType.Power);
        Check(!hero.TryActivateMusou(), "无双未满也能开启");
        hero.Musou.Add(hero.Musou.Max);
        try
        {
            hero.SetPhysicsProcess(true);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Space, Keycode = Key.Space, Pressed = true });
            await WaitFramesAsync(10);
            Check(hero.Musou.IsActive && !hero.Musou.IsFull, "空格没有经项目输入映射开启无双");
            Check(Near(hero.Stats.Get(GameConfig.Stat.StatType.Power), power * battle.WsPowerMultiplier),
                "无双没有通过属性来源提高攻击力");
            Check(Near(((GameLogic.Entity.Heroes.Body.IHeroBody)hero).MoveSpeedMultiplier, battle.WsMoveSpeedMultiplier),
                "无双横向移速倍率不正确");
            Check(!hero.TryActivateMusou(), "无双期间可以重复开启");
        }
        finally
        {
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Space, Keycode = Key.Space, Pressed = false });
            hero.SetPhysicsProcess(false);
        }

        Node2D effect = hero.GetNode<Node2D>("m_MusouAfterimage");
        Sprite2D[] snapshots = effect.GetChildren().OfType<Sprite2D>().ToArray();
        Sprite2D snapshot = snapshots.FirstOrDefault(x => x.Visible);
        Check(snapshot != null, "无双没有采样角色残影（检查导出层绑定）");
        Vector2 capturedPosition = snapshot.GlobalPosition;
        hero.GlobalPosition += new Vector2(40f, 0f);
        Check(snapshot.GlobalPosition.IsEqualApprox(capturedPosition), "角色移动时已有残影也跟着移动");
        int hp = hero.Vitals.Hp;
        for (int i = 0; i < 100 && hero.Vitals.Hp == hp; i++)
        {
            var attack = GameLogic.Battle.AttackData.Create(0, default, 10f,
                GameConfig.Battle.DamageKind.Real, new Vector2(30, 0), 1, 0, GameConfig.Sound.SoundId.None);
            hero.ReceiveHit(attack, 0);
            GameFramework.ReferencePool.Release(attack);
        }
        Check(hero.Vitals.Hp < hp && !((GameLogic.Entity.Heroes.Body.IHeroBody)hero).HasPendingHurt,
            "无双霸体没有保留正常伤害或仍登记了受击击退");
        hero._PhysicsProcess(battle.WsDuration + 1f);
        Check(!hero.Musou.IsActive && hero.Musou.Value == 0 && Near(hero.Stats.Get(GameConfig.Stat.StatType.Power), power)
            && Near(((GameLogic.Entity.Heroes.Body.IHeroBody)hero).MoveSpeedMultiplier, 1f), "无双耗尽没有完整还原战斗效果");
        await WaitUntilAsync(() => snapshots.All(x => !x.Visible), "结束无双后残影没有自然淡出");
        Check(effect.GetChildCount() == snapshots.Length, "无双残影反复分配节点而未复用缓存");
        GD.Print("SMOKE-UI: 空格开启无双、攻击/移速/霸体、残影定位与耗尽还原验证完成");
    }

    /// <summary>验证两种小怪的共享血条、生命通知、定位、死亡隐藏与真实实体池复用。</summary>
    /// <returns>血条生命周期回归完成的任务。</returns>
    private async Task CheckMonsterHealthBarsAsync()
    {
        foreach (EntityId entityId in new[] { EntityId.HuaguoshanMonkey, EntityId.DemonMonkey })
        {
            var config = ConfigSystem.Instance.Tables.TbMonsterConfig.DataList.Single(x => x.EntityId == entityId);
            m_Monster = (MonsterEntity)await GF.Entity.ShowEntityAsync(entityId, new Vector2(200, 200));
            SmokeInspection.FreezeAi(m_Monster);
            m_Monster.SetPhysicsProcess(false);
            MonsterEntity previous = m_Monster;
            ResourceBar bar = m_Monster.GetNode<ResourceBar>("m_HealthBar");
            Check(bar.Size.IsEqualApprox(bar.TextureProgress.GetSize()) && bar.Size.IsEqualApprox(new Vector2(50, 5)),
                $"{entityId} 实例尺寸覆盖了官方 50×5 填充，导致可见血条偏心");
            Check(!bar.Visible && Near(bar.Value, 1), $"{entityId} 出生血条未隐藏或未补满");

            // 初次与连续受伤都立即读取最新生命，治疗不改变领域规则。
            m_Monster.Vitals.Damage(1);
            Check(bar.Visible && Near(bar.Value, (double)m_Monster.Vitals.Hp / m_Monster.Vitals.MaxHp),
                $"{entityId} 初次受伤未立即显示正确比例");
            m_Monster.Vitals.Damage(1);
            Check(Near(bar.Value, (double)m_Monster.Vitals.Hp / m_Monster.Vitals.MaxHp),
                $"{entityId} 连续受伤未立即更新血条");
            m_Monster.Heal(1);
            Check(bar.Visible && Near(bar.Value, (double)m_Monster.Vitals.Hp / m_Monster.Vitals.MaxHp),
                $"{entityId} 部分治疗未刷新血条");

            // 位移由父节点继承；转向只镜像身体，血条始终从左到右且位于受击盒头顶。
            Vector2 before = bar.GlobalPosition;
            Vector2 movement = new(70, -30);
            m_Monster.GlobalPosition += movement;
            m_Monster.SetFacing(1);
            Check(bar.GlobalPosition.IsEqualApprox(before + movement) && bar.GetGlobalTransform().X.X > 0 &&
                Near(bar.GlobalPosition.X + bar.TextureProgress.GetWidth() * 0.5, m_Monster.HeadPosition.X) &&
                bar.GlobalPosition.Y + bar.Size.Y < m_Monster.HeadPosition.Y,
                $"{entityId} 血条没有跟随头顶或被转向镜像");
            m_Monster.SetFacing(-1);
            Check(bar.GetGlobalTransform().X.X > 0 &&
                Near(bar.GlobalPosition.X + bar.TextureProgress.GetWidth() * 0.5, m_Monster.HeadPosition.X),
                $"{entityId} 向左转身后可见血条未保持头顶居中");
            m_Monster.Heal(m_Monster.Vitals.MaxHp);
            Check(!bar.Visible && Near(bar.Value, 1), $"{entityId} 满血后血条未隐藏或未补满");

            // 通过真实受击入口归零；Changed 先于 Dead 置位也必须立即隐藏。
            m_Monster.Vitals.Damage(1);
            Check(bar.Visible, $"{entityId} 再次受伤未恢复血条显示");
            var attack = GameLogic.Battle.AttackData.Create(0, default, m_Monster.Vitals.MaxHp * 10f,
                GameConfig.Battle.DamageKind.Real, Vector2.Zero, 1, 0, GameConfig.Sound.SoundId.None);
            try
            {
                await WaitUntilAsync(() =>
                {
                    if (!m_Monster.Dead)
                    {
                        m_Monster.ReceiveHit(attack, 0);
                    }

                    return m_Monster.Dead;
                }, $"{entityId} 致命受击未归零");
            }
            finally
            {
                GameFramework.ReferencePool.Release(attack);
            }

            Check(!bar.Visible && Near(bar.Value, 0), $"{entityId} 死亡血条未立即隐藏与复位");
            ReleaseTestEntity(m_Monster);

            // 隐藏后仍存活的纯 C# 状态源不再驱动控件；池复用使用同一血条并重新订阅一次。
            previous.Vitals.SetMaximums(config.Stats.MaxHp, config.Stats.MaxMp, refill: true);
            previous.Vitals.Damage(1);
            Check(!bar.Visible && Near(bar.Value, 0), $"{entityId} 隐藏后血条仍订阅旧状态");
            await WaitFramesAsync(2);
            m_Monster = (MonsterEntity)await GF.Entity.ShowEntityAsync(entityId, Vector2.Zero);
            SmokeInspection.FreezeAi(m_Monster);
            m_Monster.SetPhysicsProcess(false);
            Check(ReferenceEquals(previous, m_Monster) && ReferenceEquals(bar, m_Monster.GetNode<ResourceBar>("m_HealthBar")),
                $"{entityId} 怪物和血条未从同一实体池实例复用");
            Check(m_Monster.Vitals.Hp == config.Stats.MaxHp && m_Monster.Vitals.MaxHp == config.Stats.MaxHp &&
                !m_Monster.Dead && !bar.Visible && Near(bar.Value, 1),
                $"{entityId} 复用残留上次生命、死亡事实或血条表现");
            m_Monster.Vitals.Damage(1);
            Check(bar.Visible && Near(bar.Value, (double)m_Monster.Vitals.Hp / m_Monster.Vitals.MaxHp),
                $"{entityId} 复用后血条未恢复订阅");
            ReleaseTestEntity(m_Monster);
            m_Monster = null;
            await WaitFramesAsync(2);
        }

        GD.Print("SMOKE-UI: 两种小怪血条即时受伤/治疗/转向/死亡隐藏/退订与实体池复用验证完成");
    }

    /// <summary>升级：档案成长变化后由装配重建注入实体并补满；HUD 跟随刷新；满级显示 MAX。</summary>
    /// <param name="context">档案作用域。</param>
    /// <param name="level">正式关卡。</param>
    /// <returns>验证完成的任务。</returns>
    private async Task CheckLevelUpAsync(GameContext context, LevelController level)
    {
        HeroRecord record = new(1, new HeroProgression(context.Curve, 0));
        m_ExtraHero = await ShowHeroAsync(context, record);
        m_TestHud = await OpenHudAsync(new BattleHudData(m_ExtraHero, record.Progression, level));
        m_ExtraHero.Vitals.Damage(m_ExtraHero.Vitals.MaxHp - 1);
        m_ExtraHero.Vitals.TrySpendMp(m_ExtraHero.Vitals.Mp - 1);

        // 门槛前不升级、不补满。
        record.Progression.AddExperience(139);
        Check(record.Progression.Level == 1 && m_ExtraHero.Vitals.Hp == 1, "升级前门槛或补满时机错误");
        CheckDisplay(m_TestHud, m_ExtraHero, record.Progression, level);

        // 跨三级：与 LevelRun 相同的注入路径（档案 → 装配 → 实体，补满）。
        record.Progression.AddExperience(378);
        m_ExtraHero.ApplyLoadout(context.StatBuilder.Build(record), refill: true);
        var row = ConfigSystem.Instance.Tables.TbHeroGrowthConfig.Get(1, 4).Stats;
        Check(record.Progression.Level == 4 && record.Progression.Experience == 37 && record.Progression.MaxExperience == 200,
            "连升未保留余量或等级错误");
        Check(m_ExtraHero.Level == 4 && m_ExtraHero.Vitals.MaxHp == row.MaxHp && m_ExtraHero.Vitals.Hp == row.MaxHp &&
            m_ExtraHero.Vitals.MaxMp == row.MaxMp && m_ExtraHero.Vitals.Mp == row.MaxMp, "升级未按成长表注入并补满");
        CheckDisplay(m_TestHud, m_ExtraHero, record.Progression, level);

        record.Progression.AddExperience(int.MaxValue);
        Check(record.Progression.IsMaxLevel && record.Progression.MaxExperience == 0, "满级没有封顶");
        CheckDisplay(m_TestHud, m_ExtraHero, record.Progression, level);
        CloseHud(m_TestHud);
        ReleaseTestEntity(m_ExtraHero);
        m_ExtraHero = null;
        await WaitFramesAsync(2);
    }

    /// <summary>主数据文件损坏时读档回退 .bak（覆盖写时由原子替换保留）。</summary>
    /// <param name="context">档案作用域。</param>
    /// <returns>验证完成的任务。</returns>
    private async Task CheckBackupRecoveryAsync(GameContext context)
    {
        // 再写一个检查点，确保 .bak 是上一份完整数据、主文件是最新数据。
        int expected = context.Profile.ActiveHero.Progression.TotalExperience;
        Check(await context.Save.CheckpointAsync(context.Profile), "检查点写入失败");
        string dataPath = DataFilePath();
        Check(File.Exists(dataPath + EasySave.BackupSuffix), "覆盖写入没有保留 .bak");
        File.WriteAllText(dataPath, "{ broken json");

        Check(await GF.Archive.LoadAsync(), "主文件损坏时没有回退备份");
        Check(GF.Archive.CurrentData.Profile.Heroes[0].TotalExperience == expected, "备份回退读到的不是最近一次检查点");

        // 用恢复后的数据重写主文件，后续用例从完好的存档开始。
        Check(await context.Save.CheckpointAsync(context.Profile), "备份回退后重写失败");
    }

    /// <summary>重构前格式（v0，无 SaveVersion）经读档流程迁移为 v1 并写回。</summary>
    /// <returns>验证完成的任务。</returns>
    private async Task CheckLegacyMigrationAsync()
    {
        // 直接写出旧格式数据文件，模拟老玩家存档。
        GameData legacy = JsonConvert.DeserializeObject<GameData>(
            $"{{\"UnitId\":{GF.Archive.CurrentCatalogue.UnitId},\"Score\":6,\"Player\":{{\"Level\":3,\"TotalExperience\":90,\"Gold\":7}}}}");
        Check(legacy.SaveVersion == 0 && legacy.Player.Level == 3, "旧格式 JSON 无法读取");
        File.WriteAllText(DataFilePath(), EasySave.Serialize(legacy, GF.Archive.Setting.EnableAesEncryption,
            GF.Archive.Setting.KEY, GF.Archive.Setting.Salt));

        SaveService save = new(SmokeInspection.Context(FindLevelProcedure()).Curve, ConfigSystem.Instance.Tables);
        LoadedProfile loaded = await save.LoadOrCreateAsync();
        Check(loaded.Origin == SaveOrigin.Migrated && loaded.Profile.ActiveHero.Progression.Level == 3 &&
            loaded.Profile.ActiveHero.Progression.TotalExperience == 300 && loaded.Profile.Wallet.Gold == 7,
            "旧格式没有按等级不倒退规则迁移");
        GameData disk = await ReadDiskAsync();
        Check(disk.SaveVersion == SaveMigrator.CurrentVersion && disk.Player == null && disk.Profile.Gold == 7,
            "迁移后没有写回当前版本");
    }

    /// <summary>冷加载中的 HUD 请求由正式流程离开时取消，晚到结果不会打开。</summary>
    /// <returns>验证完成的任务。</returns>
    private async Task CheckLoadingCancellationAsync()
    {
        ProcedureLevel procedure = await WaitForLevelAsync();
        // 新工作区的引擎缓存尚无 validation 目录，夹具保存前先建立它。
        Check(DirAccess.MakeDirRecursiveAbsolute("res://.godot/validation") == Error.Ok, "创建冷加载 UI 夹具目录失败");
        string path = $"res://.godot/validation/hud_cancel_{Guid.NewGuid():N}.tscn";
        Node fixture = GD.Load<PackedScene>(ResourcesCollectionConstant.UIs_BattleHud).Instantiate();
        PackedScene packed = new();
        Check(packed.Pack(fixture) == Error.Ok && ResourceSaver.Save(packed, path) == Error.Ok, "创建冷加载 UI 夹具失败");
        fixture.Free();
        int serial = 0;
        bool opened = false;
        EventHandler<GameEventArgs> onSuccess = (_, args) =>
        {
            if (args is OpenUIFormSuccessEventArgs success && success.UIForm.SerialId == serial)
            {
                opened = true;
            }
        };
        GF.Event.Subscribe(OpenUIFormSuccessEventArgs.EventId, onSuccess);
        try
        {
            serial = GF.UI.OpenUIForm(path, "Normal",
                new BattleHudData(FindHero(), SmokeInspection.Context(procedure).Profile.ActiveHero.Progression, FindLevel()));
            Check(GF.UI.IsLoadingUIForm(serial), "取消用例没有覆盖实际加载中的窗口请求");
            ReenterLevel();
            GF.UI.CloseUIForm(serial);
            await WaitFramesAsync(60);
            Check(!opened && !GF.UI.HasUIForm(serial) && !GF.UI.IsLoadingUIForm(serial), "取消的 HUD 晚到后仍打开");
        }
        finally
        {
            GF.Event.Unsubscribe(OpenUIFormSuccessEventArgs.EventId, onSuccess);
            if (GF.UI.HasUIForm(serial) || GF.UI.IsLoadingUIForm(serial))
            {
                GF.UI.CloseUIForm(serial);
            }
        }
    }

    /// <summary>真实 OnOpen 失败（必需绑定缺失）由流程完整清理，修复后重入恢复。</summary>
    /// <returns>验证完成的任务。</returns>
    private async Task CheckHudOpenFailureAsync()
    {
        await WaitForLevelAsync();
        BattleHud reenteredHud = CurrentHud();
        ResourceBar savedBar = reenteredHud.GetNode<ResourceBar>("StatusPanel/m_HpBar");
        CloseHud(reenteredHud);
        await WaitFramesAsync(2);
        reenteredHud.Set("m_HpBar", default(Variant));
        string openFailure = null;
        EventHandler<GameEventArgs> onFailure = (_, args) =>
        {
            if (args is OpenUIFormFailureEventArgs failed && failed.UIFormAssetName == ResourcesCollectionConstant.UIs_BattleHud)
            {
                openFailure = failed.ErrorMessage;
            }
        };
        GF.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, onFailure);
        try
        {
            ReenterLevel();
            await WaitUntilAsync(() => openFailure != null && FindHero() == null && CurrentHud() == null,
                "HUD 打开失败未完整结束正式流程");
            Check(openFailure.Contains("必须绑定"), "没有观察到必需节点装配失败");
        }
        finally
        {
            GF.Event.Unsubscribe(OpenUIFormFailureEventArgs.EventId, onFailure);
            reenteredHud.Set("m_HpBar", savedBar);
        }

        ReenterLevel();
        ProcedureLevel procedure = await WaitForLevelAsync();
        CheckDisplay(CurrentHud(), FindHero(), SmokeInspection.Context(procedure).Profile.ActiveHero.Progression, FindLevel());
    }

    /// <summary>经真实结算链路击败一只本关怪物，等待关卡运行结算经验。</summary>
    /// <param name="level">正式关卡。</param>
    /// <param name="procedure">关卡流程。</param>
    /// <returns>结算完成的任务。</returns>
    private async Task DefeatMonsterAsync(LevelController level, ProcedureLevel procedure)
    {
        HeroEntity hero = FindHero();
        LevelRun run = SmokeInspection.Run(procedure);
        hero.SetPhysicsProcess(true);
        int kills = run.Stats.Kills;

        // 推动相机抵达首阶段右界触发首波刷怪；刷出的怪物立即停用 AI，避免它先打死测试英雄。
        await WaitUntilAsync(() =>
        {
            if (SmokeInspection.Sequence(level).Phase != LevelStagePhase.Fighting)
            {
                Input.ActionPress("move_right");
                Input.ActionPress("jump");
                Input.ActionRelease("jump");
                return false;
            }

            Input.ActionRelease("move_right");
            MonsterEntity spawned = FindMonster();
            SmokeInspection.FreezeAi(spawned);
            return spawned != null;
        }, "首阶段没有刷出怪物", 20000);

        Check(run.Outcome == LevelRunOutcome.Running, $"击杀前关卡运行已结束：{run.Outcome}");
        MonsterEntity monster = FindMonster();

        // 死亡事件由身体状态机进入死亡状态时广播：受测怪物必须保留物理步进。
        monster.SetPhysicsProcess(true);

        // 闪避按真实概率结算：每帧再打一次直到死亡，再等事件下一帧分发到关卡运行。
        await WaitUntilAsync(() =>
        {
            if (!monster.Dead)
            {
                var attack = GameLogic.Battle.AttackData.Create(0, default, monster.Vitals.MaxHp * 10f,
                    GameConfig.Battle.DamageKind.Real, Vector2.Zero, 1, 0, GameConfig.Sound.SoundId.None);
                monster.ReceiveHit(attack, hero.Id);
                GameFramework.ReferencePool.Release(attack);
            }

            return run.Stats.Kills > kills;
        }, "击败怪物没有经关卡运行结算", 10000, () => Describe(monster, run, level));
        hero.SetPhysicsProcess(false);
    }

    /// <summary>诊断：怪物与关卡运行的当前状态（仅失败消息使用）。</summary>
    /// <param name="monster">受测怪物。</param>
    /// <param name="run">关卡运行。</param>
    /// <param name="level">关卡。</param>
    /// <returns>可读的状态描述。</returns>
    private static string Describe(MonsterEntity monster, LevelRun run, LevelController level) =>
        $"monster shown={monster.IsShown} dead={monster.Dead} hp={monster.Vitals.Hp}/{monster.Vitals.MaxHp} " +
        $"id={monster.Id}; run={run.Outcome} kills={run.Stats.Kills}; level={SmokeInspection.Sequence(level).Phase}/{SmokeInspection.Sequence(level).Current.StageOrder}; " +
        $"procedure={GF.Procedure.CurrentProcedure?.GetType().Name}";

    /// <summary>经真实结算链路击杀英雄（闪避按真实概率结算，重复攻击直到死亡）。</summary>
    /// <param name="hero">正式英雄。</param>
    /// <exception cref="InvalidOperationException">多次攻击仍未致死。</exception>
    private static void KillHero(HeroEntity hero)
    {
        for (int i = 0; i < 100 && !hero.Dead; i++)
        {
            var attack = GameLogic.Battle.AttackData.Create(0, default, hero.Vitals.MaxHp * 100f,
                GameConfig.Battle.DamageKind.Real, Vector2.Zero, -1, 0, GameConfig.Sound.SoundId.None);
            hero.ReceiveHit(attack, 0);
            GameFramework.ReferencePool.Release(attack);
        }

        Check(hero.Dead, "连续攻击仍未击杀英雄");
    }

    /// <summary>
    /// 恢复测试关闭的物理步进后归还实体：物理开关是节点属性，会随池实例保留，
    /// 不恢复的话正式流程复用该实例时身体状态机不再推进（死亡事件也不会广播）。
    /// </summary>
    /// <param name="entity">测试显示的实体，可为空。</param>
    private static void ReleaseTestEntity(ActorEntity entity)
    {
        if (entity == null || !GodotObject.IsInstanceValid(entity))
        {
            return;
        }

        entity.SetPhysicsProcess(true);
        GF.Entity.HideEntitySafe(entity);
    }

    /// <summary>按出战装配显示一名额外英雄（关闭物理，测试只观测数值；归还经 <see cref="ReleaseTestEntity"/>）。</summary>
    /// <param name="context">档案作用域。</param>
    /// <param name="record">英雄记录。</param>
    /// <returns>已显示的英雄。</returns>
    private static async Task<HeroEntity> ShowHeroAsync(GameContext context, HeroRecord record)
    {
        WukongEntity hero = await GF.Entity.ShowEntityAsync<WukongEntity>(EntityId.Wukong, context.StatBuilder.Build(record));
        hero.SetPhysicsProcess(false);
        return hero;
    }

    /// <summary>通过正式窗口 API 打开 HUD，等待实际完成。</summary>
    /// <param name="data">打开参数。</param>
    /// <returns>本次请求打开的 HUD。</returns>
    private async Task<BattleHud> OpenHudAsync(BattleHudData data)
    {
        int serial = GF.UI.OpenUIForm(UIFormId.BattleHud, data);
        await WaitUntilAsync(() => GF.UI.GetUIForm(serial) is BattleHud, "HUD 请求未完成");
        return (BattleHud)GF.UI.GetUIForm(serial);
    }

    /// <summary>等待关卡流程装配完成（运行、英雄与 HUD 就绪）。</summary>
    /// <returns>当前关卡流程。</returns>
    private async Task<ProcedureLevel> WaitForLevelAsync()
    {
        await WaitUntilAsync(() => SmokeInspection.Run(FindLevelProcedure())?.Outcome == LevelRunOutcome.Running &&
            CurrentHud() != null && FindHero() != null, "关卡流程未就绪", 15000);
        return FindLevelProcedure();
    }

    /// <summary>读取当前流程（只在处于关卡流程时返回）。</summary>
    /// <returns>关卡流程或 null。</returns>
    private static ProcedureLevel FindLevelProcedure() => GF.Procedure.CurrentProcedure as ProcedureLevel;

    /// <summary>使用真实流程状态机触发一次普通离开与再次进入。</summary>
    private static void ReenterLevel()
    {
        ((Fsm<IProcedureManager>)GF.Fsm.GetFsm<IProcedureManager>()).ChangeState<ProcedureLevel>();
    }

    /// <summary>当前存档槽数据文件的绝对路径（与 ArchiveSystem 布局一致，只用于测试注入损坏与旧格式）。</summary>
    /// <returns>数据文件路径。</returns>
    private static string DataFilePath() => ProjectSettings.GlobalizePath(
        $"user://{GF.Archive.Setting.Folder}/Data/{GF.Archive.CurrentCatalogue.UnitId}.sav");

    /// <summary>直接读取磁盘上的当前槽数据（不经 ArchiveSystem，证明写盘真实发生）。</summary>
    /// <returns>磁盘数据。</returns>
    private static async Task<GameData> ReadDiskAsync()
    {
        var setting = GF.Archive.Setting;
        return await EasySave.LoadFromUserAsync<GameData>(
            $"{setting.Folder}/Data/{GF.Archive.CurrentCatalogue.UnitId}.sav",
            setting.EnableAesEncryption, setting.KEY, setting.Salt);
    }

    /// <summary>查找当前已显示的英雄，场景树查询只用于调试断言。</summary>
    /// <returns>当前英雄或 null。</returns>
    private HeroEntity FindHero() => m_Driver.GetTree().Root.FindChildren("*", "CharacterBody2D", true, false)
        .OfType<HeroEntity>().FirstOrDefault(x => x.IsShown && x != m_ExtraHero);

    /// <summary>查找当前已显示且存活的怪物。</summary>
    /// <returns>怪物或 null。</returns>
    private MonsterEntity FindMonster() => m_Driver.GetTree().Root.FindChildren("*", "CharacterBody2D", true, false)
        .OfType<MonsterEntity>().FirstOrDefault(x => x.IsShown && !x.Dead && x != m_Monster);

    /// <summary>查找当前正式关卡，排除已经等待释放的旧节点。</summary>
    /// <returns>当前关卡或 null。</returns>
    private LevelController FindLevel() => m_Driver.GetTree().Root.FindChildren("*", "Node2D", true, false)
        .OfType<LevelController>().FirstOrDefault(x => !x.IsQueuedForDeletion());

    /// <summary>读取当前正式 HUD。</summary>
    /// <returns>当前窗口或 null。</returns>
    private static BattleHud CurrentHud() => GF.UI.GetUIForm(ResourcesCollectionConstant.UIs_BattleHud) as BattleHud;

    /// <summary>关闭仍由框架管理的窗口，重复调用安全。</summary>
    /// <param name="hud">待关闭窗口，可为空。</param>
    private static void CloseHud(BattleHud hud)
    {
        if (hud != null && GF.UI.HasUIForm(hud.SerialId))
        {
            GF.UI.CloseUIForm(hud.SerialId);
        }
    }

    /// <summary>比对实际控件与状态源，而不是只检查通知次数。</summary>
    /// <param name="hud">实际窗口。</param>
    /// <param name="hero">预期绑定英雄。</param>
    /// <param name="progression">预期绑定成长。</param>
    /// <param name="level">预期绑定关卡。</param>
    private static void CheckDisplay(BattleHud hud, HeroEntity hero, HeroProgression progression, LevelController level)
    {
        foreach (string path in new[] { "StatusPanel/m_HpBar", "StatusPanel/m_MpBar", "StatusPanel/m_ExpBar", "MenuPanel/WsFrame/m_WsBar" })
        {
            ResourceBar resourceBar = hud.GetNode<ResourceBar>(path);
            Check(resourceBar.Visible && resourceBar.TextureProgress != null, $"资源条隐藏或填充纹理未加载：{path}");
        }

        var vitals = hero.Vitals;
        foreach (string path in new[] { "StatusPanel/m_HpBar/m_HpText", "StatusPanel/m_MpBar/MpText", "StatusPanel/m_ExpBar/ExpText" })
        {
            Label label = hud.GetNode<Label>(path);
            Font font = label.GetThemeFont("font");
            Check(font is FontVariation variation && variation.BaseFont.ResourcePath == ResourcesCollectionConstant.Fonts_fz_cu_yuan_hud &&
                label.GetThemeFontSize("font_size") == 14,
                $"{path} 实际解析的字体/字号不是旧 HUD 粗圆 14px，检查 Sprite2D 对主题继承的隔断");
        }
        Check(hud.GetNode<Label>("StatusPanel/m_LevelLabel").GetThemeFont("font").ResourcePath ==
            ResourcesCollectionConstant.Fonts_dfp_hai_bao_w12, "等级标签未使用旧 HUD 海报体");
        Check(Near(hud.GetNode<ResourceBar>("StatusPanel/m_HpBar").Value, (double)vitals.Hp / vitals.MaxHp), "血条填充与当前英雄不一致");
        Check(hud.GetNode<Label>("StatusPanel/m_HpBar/m_HpText").Text == $"{vitals.Hp}/{vitals.MaxHp}", "生命文本不一致");
        Check(hud.GetNode<Label>("StatusPanel/m_LevelLabel").Text == progression.Level.ToString(), "等级文本不一致");
        Check(Near(hud.GetNode<ResourceBar>("StatusPanel/m_MpBar").Value, (double)vitals.Mp / vitals.MaxMp), "魔法条不一致");
        Check(hud.GetNode<Label>("StatusPanel/m_MpBar/MpText").Text == $"{vitals.Mp}/{vitals.MaxMp}", "魔法文本不一致");
        double experienceRatio = progression.MaxExperience == 0 ? 1 : (double)progression.Experience / progression.MaxExperience;
        Check(Near(hud.GetNode<ResourceBar>("StatusPanel/m_ExpBar").Value, experienceRatio), "经验条不一致");
        Check(hud.GetNode<Label>("StatusPanel/m_ExpBar/ExpText").Text ==
            (progression.MaxExperience == 0 ? "MAX" : $"{progression.Experience}/{progression.MaxExperience}"), "经验文本不一致");
        Check(Near(hud.GetNode<TextureProgressBar>("MenuPanel/WsFrame/m_WsBar").Value, (double)hero.Musou.Value / hero.Musou.Max),
            "无双条不一致");
        Check(hud.GetNode<AnimatedSprite2D>("m_Go").Visible == level.TravelAvailable, "Go 显示与关卡状态不一致");
    }

    /// <summary>比较控件比例，容纳浮点显示误差。</summary>
    /// <param name="actual">控件值。</param>
    /// <param name="expected">预期值。</param>
    /// <returns>误差是否足够小。</returns>
    private static bool Near(double actual, double expected) => Math.Abs(actual - expected) < 0.0001;

    /// <summary>断言失败时终止当前回归并保留具体原因。</summary>
    /// <param name="condition">断言条件。</param>
    /// <param name="message">失败原因。</param>
    /// <exception cref="InvalidOperationException">条件不成立。</exception>
    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>按真实引擎帧等待，不创建后台轮询任务。</summary>
    /// <param name="frames">帧数。</param>
    /// <returns>指定帧数完成的任务。</returns>
    private async Task WaitFramesAsync(int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    /// <summary>等待实际引擎条件，超时报告失败。</summary>
    /// <param name="condition">完成条件。</param>
    /// <param name="message">超时原因。</param>
    /// <param name="timeoutMs">超时毫秒数。</param>
    /// <returns>条件成立的任务。</returns>
    /// <exception cref="InvalidOperationException">等待超时。</exception>
    private async Task WaitUntilAsync(Func<bool> condition, string message, ulong timeoutMs = 10000,
        Func<string> describe = null)
    {
        ulong start = Time.GetTicksMsec();
        while (!condition())
        {
            if (Time.GetTicksMsec() - start >= timeoutMs)
            {
                Check(false, describe == null ? message : $"{message}（{describe()}）");
            }

            await WaitFramesAsync(1);
        }
    }
}
