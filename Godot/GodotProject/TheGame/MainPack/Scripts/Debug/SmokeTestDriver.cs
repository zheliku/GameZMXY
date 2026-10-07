using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Battle;
using GameConfig.Sound;
using GameFramework;
using GameFramework.Event;
using GameLogic;
using GameLogic.Battle;
using GameLogic.Entity.Heroes;
using GameLogic.Entity.Monsters;
using GameLogic.Event;
using GameLogic.Level;
using GameLogic.UI;
using Godot;
using GodotGameFramework;

/// <summary>
/// 冒烟测试驱动器（开发工具）：仅当以 `-- --smoketest` 启动时生效，平时零开销惰性。
/// 自动注入输入（普攻连打 / 双击跑 / 受击 / 一段跳 / 二段跳），并断言悟空身体状态机与
/// AnimationPlayer 的流转，打印 `SMOKE PASS` / `SMOKE FAIL`（失败附完整观察序列）。
///
/// 它验证的是"身体状态机 → 动画播放"这条链路：观测点同时包含身体状态名与 AnimationPlayer 里真正在播的
/// 动画，不是 C# 自己的日志——所以能抓住"状态切了但动画没切"这一类问题。
///
/// 用法：S:\Godot4\Godot4CSharp_console.exe --headless --path Godot/GodotProject --quit-after N -- --smoketest[=ai]
///   `--smoketest`     英雄控制器 + M4 命中（猴子 AI 冻结为沙包，断言确定；N = 1800）
///   `--smoketest=ai`  怪物 AI（巡逻/追击/出招/转身/平台守候/丢失目标/受控/死亡）。
///   `--smoketest=level` 正式关卡四段移动、锁屏、刷怪与清波。
///   `--smoketest=level-cancel` 在途显示取消与晚到结果回收。
///   `--smoketest=ui` HUD 属性更新、退订复用、实体复用、失败清理与隔离存档重入。
///
/// 关卡首波怪物由相机抵达首阶段右界激活（Level_1 的相机右缘），因此两种模式都先注入移动输入
/// 推动相机抵达后才启动既有战斗时间轴；M4 连打验证沙包，AI 模式验证怪物行为。
///
/// 注意：**判定看 stdout 的 SMOKE PASS/FAIL，别只看退出码**——框架关闭流程有一个既有 bug
/// （WebRequestAgentHelper.Reset 访问已释放的 HttpRequest，见 Framework/…/DefaultWebRequestAgentHelper.cs:90），
/// 偶发在退出时升级成 0xC0000005 段错误，把退出码冲掉；这是框架问题，与本测试无关，已上报。
/// </summary>
public partial class SmokeTestDriver : Node
{
	/// <summary>互斥的自动回归模式。</summary>
	private enum SmokeMode
	{
		Disabled,
		Hero,
		MonsterAi,
		Level,
		Cancellation,
		Ui,
	}

	private static readonly (double At, string Action, string Mode)[] Plan =
	{
		(2.00, "attack", "mash"),
		(4.00, "attack", "release"),
		(5.00, "move_right", "hold"),
		(5.10, "move_right", "release"),
		(5.20, "move_right", "hold"),
		(6.30, "move_right", "release"),
		(6.35, "hurt", "damage"),
		(6.70, "jump", "tap"),
		(7.40, "jump", "tap"),
		// 首阶段已在准备阶段激活；第二轮连打覆盖跳跃后落地的真实命中链路。
		(8.30, "attack", "mash"),
		(10.30, "attack", "release"),
	}; // 按时间安排英雄烟测的输入动作。

	private static readonly string[] Required =
	{
		"Attack/attack_1", "Attack/attack_2", "Attack/attack_3", "Attack/attack_4",
		"Ground/run", "Hurt/hurt", "Air/jump", "Air/fall", "Air/jump_2", "Ground/idle1",
	}; // 必须观测到的身体状态与实际动画组合。

	private static readonly (string A, string B, bool LastA, bool LastB)[] Order =
	{
		("Attack/attack_1", "Attack/attack_2", false, false),
		("Attack/attack_2", "Attack/attack_3", false, false),
		("Attack/attack_3", "Attack/attack_4", false, false),
		("Attack/attack_4", "Ground/run", false, false),
		("Ground/run", "Hurt/hurt", false, false),
		("Hurt/hurt", "Air/jump", false, false),
		("Air/jump", "Air/jump_2", false, false),
		("Air/jump", "Air/fall", false, true),      // 出生时也会下落，取最后一次 fall（起跳后的下落）
		("Air/jump_2", "Ground/idle1", false, true), // 落地后回待机，取最后一次 idle1
	}; // 必须按顺序出现的状态与动画组合。

	private const double EndTime = 11.5; // 英雄烟测最长运行时间（秒）。
	private const double FindTimeout = 5.0; // 等待实体出现的超时时间（秒）。

	private const float SandbagOffset = 100f; // 沙包猴子相对英雄的水平站位（像素）。

	private HeroEntity m_Hero; // 烟测中的英雄实体。

	// ---- M4 命中链路观测（悟空连打面前的猴子）----

	private MonsterEntity m_Monster; // 沙包猴子（场上第一只怪物）。

	private readonly List<(int Damage, bool Crit, bool Miss)> m_MonsterHits = new(); // 按事件记录猴子每次受击结果。

	private int m_MaxPopCount; // 场上飘字节点的可见峰值数量。
	private SmokeMode m_Mode; // 命令行选择的唯一冒烟模式。
	private double m_WaitTime; // 等待英雄出现的累计时间（秒）。
	private double m_Time; // 烟测当前时间（秒）。
	private int m_NextStep; // 下一条输入计划索引。
	private bool m_Mashing; // 是否正在交替注入攻击输入。
	private double m_MashUntil; // 攻击连打结束时间（秒）。
	private int m_MashFrame; // 攻击连打帧计数。
	private string m_LastPath = ""; // 上一次记录的身体状态与动画路径。
	private bool m_HasQuitAfter; // 命令行是否包含引擎自动退出参数。
	private readonly List<(double Time, string Path)> m_Observed = new(); // 按时间记录身体状态与动画路径。

	private readonly HashSet<int> m_WalkFrames = new(); // walk 期间采样到的身体帧号。
	private readonly HashSet<int> m_RunFrames = new(); // run 期间采样到的身体帧号。

	private LevelSmokeScenario m_LevelScenario; // 正式关卡四段流程的观测与输入计划。
	private LevelCancellationSmokeScenario m_CancelScenario; // 真实在途显示的取消回归。
	private UiSmokeScenario m_UiScenario; // HUD 与存档的真实引擎回归。

	private MonsterAiSmokeScenario m_AiScenario; // ai 模式下驱动英雄与猴子的场景。

	private double m_AiStartTime; // AI 烟测场景的开始时间。

	private double m_TravelTime; // 战斗烟测进入首阶段的准备时间。
	private bool m_StageEntered; // 相机已到达首阶段右界，战斗时间轴可开始。
	private LevelController m_Level; // 烟测观测的关卡会话。

	private double m_MonsterFoundAt = -1.0; // 找到受测猴子的时间（秒）；-1 表示尚未找到。

	private readonly HashSet<int> m_FrozenMonsters = new(); // 已冻结为沙包的怪物 runtime ID。

	/// <summary>读取烟测参数并设置物理处理优先级。</summary>
	public override void _Ready()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			m_Mode = arg switch
			{
				"--smoketest" => SmokeMode.Hero,
				"--smoketest=ai" => SmokeMode.MonsterAi,
				"--smoketest=level" => SmokeMode.Level,
				"--smoketest=level-cancel" => SmokeMode.Cancellation,
				"--smoketest=ui" => SmokeMode.Ui,
				_ => m_Mode,
			};
		}

		// 引擎参数里带了 --quit-after 时，通过流程交给引擎自己退出（见 StopDriving 注释）
		foreach (string arg in OS.GetCmdlineArgs())
		{
			if (arg == "--quit-after")
			{
				m_HasQuitAfter = true;
			}
		}

		if (m_Mode == SmokeMode.Disabled)
		{
			SetPhysicsProcess(false);
		}
		else
		{
			// 每次烟测使用独立存档目录，自动保存和失败测试不触碰玩家实际进度。
			GF.Archive.Setting.Folder = $"Validation/{m_Mode}/{Guid.NewGuid():N}";
			GD.Print($"SMOKE ARCHIVE: user://{GF.Archive.Setting.Folder}");
		}

		// 排在实体与 AnimationPlayer 之后处理：采样到的才是"本帧更新完"的状态，
		// 否则读到的永远是上一帧的播放状态（会误判成"动画晚一帧"）。
		ProcessPriority = 1000;
		ProcessPhysicsPriority = 1000;
	}

	/// <summary>每物理帧驱动输入、实体采样及烟测断言。</summary>
	/// <param name="delta">上一帧到当前帧的秒数。</param>
	public override void _PhysicsProcess(double delta)
	{
		if (m_Mode == SmokeMode.Ui)
		{
			DriveUiScenario(delta);
			return;
		}
		if (m_Hero == null)
		{
			m_WaitTime += delta;
			if (m_WaitTime > FindTimeout)
			{
				Fail("5 秒内没有找到英雄（游戏没起来？）");
			}

			// FindChildren 的类型过滤只认引擎原生类名（C# 脚本类名不匹配），按 CharacterBody2D 取再判脚本类型
			foreach (Node node in GetTree().Root.FindChildren("*", "CharacterBody2D", true, false))
			{
				if (node is HeroEntity hero)
				{
					m_Hero = hero;
					break;
				}
			}

			if (m_Hero == null)
			{
				return;
			}

			GF.Event.Subscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
			GD.Print("SMOKE: 找到 hero，开始时间轴");
		}

		if (m_Mode == SmokeMode.Cancellation)
		{
			DriveCancellationScenario(delta);
			return;
		}

		if (m_Mode == SmokeMode.Level)
		{
			DriveLevelScenario(delta);
			return;
		}

		FindMonster();
		if (!m_StageEntered)
		{
			DriveStageEntry(delta);
			return;
		}

		m_Time += delta;
		if (m_Mode == SmokeMode.MonsterAi)
		{
			DriveAiScenario();
			return;
		}

		DriveInput();
		Sample();
		TrackBodyFrames();
		TrackPops();
		if (m_Time >= EndTime)
		{
			Finish();
		}
	}

	/// <summary>等待正式 HUD 就绪，再驱动真实 UI 生命周期与存档回归。</summary>
	/// <param name="delta">物理帧间隔秒数。</param>
	private void DriveUiScenario(double delta)
	{
		if (m_UiScenario == null)
		{
			m_WaitTime += delta;
			var hud = GF.UI?.GetUIForm(GameConfig.Constant.ResourcesCollectionConstant.UIs_BattleHud) as BattleHud;
			var hero = GetTree().Root.FindChildren("*", "CharacterBody2D", true, false)
				.OfType<HeroEntity>().FirstOrDefault(x => x.IsShown);
			var level = GetTree().Root.FindChildren("*", "Node2D", true, false)
				.OfType<LevelController>().FirstOrDefault();
			if (hud != null && hero != null && level != null)
			{
				m_UiScenario = new UiSmokeScenario(this, hero, level, hud);
			}
			else if (m_WaitTime > 15.0)
			{
				Fail("15 秒内正式 HUD 未就绪");
			}
			return;
		}

		if (m_UiScenario.IsDone)
		{
			if (m_UiScenario.Failure == null)
			{
				GD.Print("SMOKE PASS：UI 数值、解绑复用、实体复用、加载取消、失败清理与存档重入全部通过");
				StopDriving(false);
			}
			else
			{
				Fail(m_UiScenario.Failure);
			}
		}
	}

	private void DriveInput() // 按时间计划注入测试输入。
	{
		// 先维持连打期间的交替按键，再执行所有已到时刻的计划动作。
		if (m_Mashing)
		{
			m_MashFrame++;
			if (m_Time >= m_MashUntil)
			{
				Input.ActionRelease("attack");
				m_Mashing = false;
			}
			else if (m_MashFrame % 2 == 0)
			{
				Input.ActionPress("attack");
			}
			else
			{
				Input.ActionRelease("attack");
			}
		}

		while (m_NextStep < Plan.Length && m_Time >= Plan[m_NextStep].At)
		{
			(double _, string action, string mode) = Plan[m_NextStep];
			switch (mode)
			{
				case "tap":
					Input.ActionPress(action);
					Input.ActionRelease(action);
					break;
				case "hold":
					Input.ActionPress(action);
					break;
				case "release":
					Input.ActionRelease(action);
					break;
				case "mash":
					m_Mashing = true;
					m_MashUntil = m_Time + 2.0;
					m_MashFrame = 0;
					break;
				case "damage":
					HurtHeroOnce();   // 验证受击：硬直 + hurt 动画（走真实结算链路）
					break;
			}

			m_NextStep++;
		}
	}

	private void HurtHeroOnce() // 经真实战斗结算链路对英雄造成一次 1 点伤害。
	{
		// 临时攻击数据只在结算期间持有，并在调用后归还对象池。
		AttackData attack = AttackData.Create(0, default, 1f, DamageKind.Real, Vector2.Zero, -1, 0, SoundId.None);
		m_Hero.ReceiveHit(attack, 0);
		ReferencePool.Release(attack);
	}

	private void DriveAiScenario() // 等待测试实体并推进 AI 场景断言。
	{
		// 场景只在英雄与猴子都准备好后创建，避免测试时钟先行。
		if (m_AiScenario == null)
		{
			if (m_Monster == null)
			{
				if (m_Time > FindTimeout + 2.0)
				{
					Fail("相机抵达首阶段后 7 秒内没有找到猴子");
				}

				return;
			}

			// 等受测猴子在视野外丢失生成时锁定的目标（LoseTargetTime），AI 场景的阶段 0 才是干净巡逻。
			if (m_Monster.AiStateName is not ("Wander" or "Pause"))
			{
				if (m_MonsterFoundAt < 0.0)
				{
					m_MonsterFoundAt = m_Time;
				}

				if (m_Time > m_MonsterFoundAt + 7.0)
				{
					Fail($"受测猴子未在视野外丢失目标（当前 AI：{m_Monster.AiStateName}）");
				}

				return;
			}

			m_AiScenario = new MonsterAiSmokeScenario(m_Hero, m_Monster);
			m_AiStartTime = m_Time;
			GD.Print("SMOKE-AI: 开始怪物 AI 场景");
		}

		double t = m_Time - m_AiStartTime;
		// 每帧驱动场景一次；未结束时等待下一帧，结束时统一采集断言并关闭。
		m_AiScenario.Update(t);
		if (!m_AiScenario.IsDone)
		{
			return;
		}

		List<string> failures = m_AiScenario.Finish();
		m_AiScenario = null;
		if (failures.Count == 0)
		{
			GD.Print("SMOKE PASS：怪物 AI 场景断言全部通过");
			StopDriving(false);
		}
		else
		{
			Fail(string.Join("；", failures));
		}
	}

	private void DriveStageEntry(double delta) // 通过真实移动推动相机到首阶段右界，再启动既有战斗回归时间轴。
	{
		m_TravelTime += delta;
		foreach (Node node in GetTree().Root.FindChildren("*", "Node2D", true, false))
		{
			if (node is LevelController level)
			{
				m_Level = level;
				break;
			}
		}

		if (m_Level?.Phase == LevelStagePhase.Fighting)
		{
			Input.ActionRelease("move_right");
			Input.ActionRelease("jump");
			// 战斗回归需要平地，使用场景中已配置的平坦玩家出生点，避免斜坡改变高度断言。
			m_Hero.GlobalPosition = m_Level.PlayerSpawnPosition;
			m_Hero.Velocity = Vector2.Zero;
			m_Hero.Input.Reset();
			m_StageEntered = true;
			GD.Print("SMOKE: 相机抵达首阶段，开始战斗时间轴");
			return;
		}

		Input.ActionPress("move_right");
		if (m_Hero.IsOnWall() && m_Hero.IsOnFloor())
		{
			Input.ActionPress("jump");
		}
		else
		{
			Input.ActionRelease("jump");
		}
		if (m_TravelTime > 20.0)
		{
			Fail("20 秒内相机未抵达首阶段右界");
		}
	}

	private void DriveLevelScenario(double delta) // 正式关卡烟测独立于战斗回归时间轴，按阶段事件推进。
	{
		if (m_LevelScenario == null)
		{
			foreach (Node node in GetTree().Root.FindChildren("*", "Node2D", true, false))
			{
				if (node is LevelController level && level.Phase != LevelStagePhase.Ready)
				{
					m_LevelScenario = new LevelSmokeScenario(m_Hero, level);
					break;
				}
			}
		}

		if (m_LevelScenario == null)
		{
			return;
		}

		m_LevelScenario.Update(delta);
		if (m_LevelScenario.IsDone)
		{
			if (m_LevelScenario.Failures.Count == 0)
			{
				GD.Print("SMOKE PASS：四段关卡、相机连续移动、左右物理门、抵达延迟和怪物总数全部通过");
				StopDriving(false);
			}
			else
			{
				Fail(string.Join("；", m_LevelScenario.Failures));
			}
		}
	}

	private void DriveCancellationScenario(double delta) // 单独验证停止时仍在加载的实体不会泄漏到场景。
	{
		if (m_CancelScenario == null)
		{
			foreach (Node node in GetTree().Root.FindChildren("*", "Node2D", true, false))
			{
				if (node is LevelController level && level.Phase != LevelStagePhase.Ready)
				{
					m_CancelScenario = new LevelCancellationSmokeScenario(m_Hero, level);
					break;
				}
			}
		}

		m_CancelScenario?.Update(delta);
		if (m_CancelScenario?.IsDone == true)
		{
			if (m_CancelScenario.Failures.Count == 0)
			{
				GD.Print("SMOKE PASS：在途显示取消，晚到怪物已回收且关卡不再推进");
				StopDriving(false);
			}
			else
			{
				Fail(string.Join("；", m_CancelScenario.Failures));
			}
		}
	}

	private void Sample() // 记录身体状态和动画路径变化。
	{
		string path = ObservePath(m_Hero.BodyStateName, m_Hero.CurrentAnim);
		if (path.Length == 0 || path == m_LastPath)
		{
			return;
		}

		m_LastPath = path;
		m_Observed.Add((m_Time, path));
		GD.Print($"SMOKE[{m_Time:F2}] 身体/动画 {path}  ({Facts()})");
	}

	/// <summary>
	/// 观测点 = `身体状态/播放器当前动画`（如 Attack/attack_2、Ground/idle1）。
	/// 动画为空（播放器还没播过）时返回空串，不计入观测。
	/// </summary>
	/// <param name="bodyState">当前身体状态名。</param>
	/// <param name="anim">当前动画名。</param>
	/// <returns>组合路径；动画为空时返回空串。</returns>
	public static string ObservePath(string bodyState, string anim)
	{
		return anim.Length == 0 ? "" : $"{bodyState}/{anim}";
	}

	/// <summary>
	/// 身体帧采样：walk/run 状态下记录 m_Body 的帧号。动画库若漏了 m_Body:frame 轨道，
	/// 帧会停在进入前的一个值上不动——这里用"出现过的不同帧数"抓这类回归。
	/// </summary>
	private void TrackBodyFrames() // 收集 walk/run 期间身体动画的帧变化证据。
	{
		if (m_Hero?.Body is not Sprite2D body)
		{
			return;
		}

		if (m_LastPath == "Ground/walk")
		{
			m_WalkFrames.Add(body.Frame);
		}
		else if (m_LastPath == "Ground/run")
		{
			m_RunFrames.Add(body.Frame);
		}
	}

	private string Facts() // 生成英雄身体状态快照供烟测日志定位问题。
	{
		return $"move={m_Hero.Input.MoveAxis} run={m_Hero.Input.Running} jump={m_Hero.JumpCount} "
			+ $"seg={m_Hero.AttackSegment} combo={m_Hero.ComboIndex} dead={m_Hero.Dead} "
			+ $"floor={m_Hero.IsOnFloor()} vy={m_Hero.Velocity.Y:F0}";
	}

	private void Finish() // 汇总状态顺序、动画、移动帧和战斗断言并结束驱动。
	{
		// 汇总必须出现的观测点与先后约束。
		List<string> failures = new();
		foreach (string required in Required)
		{
			if (LastIndex(required) < 0)
			{
				failures.Add($"未观察到 {required}");
			}
		}

		foreach ((string a, string b, bool lastA, bool lastB) in Order)
		{
			int indexA = lastA ? LastIndex(a) : FirstIndex(a);
			int indexB = lastB ? LastIndex(b) : FirstIndex(b);
			if (indexA >= 0 && indexB >= 0 && indexA > indexB)
			{
				failures.Add($"顺序错误：{a} 晚于 {b}");
			}
		}

		// 补充检查动画层、移动帧和实际命中链路的回归条件。
		CheckEffectLayer(failures);
		CheckLocomotionFrames(failures);
		CheckAttackFinishTiming(failures);
		CheckCombat(failures);

		if (failures.Count == 0)
		{
			GD.Print($"SMOKE PASS：观察到 {m_Observed.Count} 次状态切换，断言全部通过");
			StopDriving(false);
			return;
		}

		Fail(string.Join("；", failures));
	}

	private void CheckEffectLayer(List<string> failures) // 校验待机特效层已回到空白基准状态。
	{
		if (m_Hero.GetNodeOrNull<Node2D>("m_EffectRoot/m_Effect") is not AnimatedSprite2D effect)
		{
			failures.Add("场景里找不到 m_EffectRoot/m_Effect");
			return;
		}

		if (effect.Animation.ToString() != "empty")
		{
			failures.Add($"待机时特效动画应为 empty，实际 {effect.Animation}");
		}

		if (!effect.Scale.IsEqualApprox(Vector2.One))
		{
			failures.Add($"待机时特效 scale 应为 (1,1)，实际 {effect.Scale}");
		}

		if (effect.Frame != 0)
		{
			failures.Add($"待机时特效帧应为 0，实际 {effect.Frame}");
		}
	}

	private void CheckLocomotionFrames(List<string> failures) // 校验走跑动画持续改变身体帧。
	{
		if (m_RunFrames.Count < 3)
		{
			failures.Add($"run 期间身体帧没有在动（观察到 {m_RunFrames.Count} 个不同帧：{string.Join(",", m_RunFrames)}）");
		}

		if (m_WalkFrames.Count < 1)
		{
			failures.Add("walk 期间没有采样到身体帧");
		}
	}

	private void CheckAttackFinishTiming(List<string> failures) // 校验连段切换等待当前攻击动画结束。
	{
		for (int seg = 1; seg < 4; seg++)
		{
			int indexA = FirstIndex($"Attack/attack_{seg}");
			int indexB = FirstIndex($"Attack/attack_{seg + 1}");
			if (indexA < 0 || indexB < 0)
			{
				continue;
			}

			double gap = m_Observed[indexB].Time - m_Observed[indexA].Time;
			if (gap < 0.30)
			{
				failures.Add($"连段 {seg}→{seg + 1} 间隔 {gap:F2}s < 动画长度 0.35s（没等播完就切段？）");
			}
		}
	}

	private void FindMonster() // 找到场景猴子并按烟测模式调整其行为。
	{
		// 首波猴子在英雄走进首阶段右界后成批出现；除受测/沙包目标外的所有猴子都冻结成沙包，
		// 避免活怪攻击英雄带来不确定的受击/击退时序（AI 行为由 ai 场景单独覆盖）。
		foreach (Node node in GetTree().Root.FindChildren("*", "CharacterBody2D", true, false))
		{
			if (node is not MonsterEntity { IsShown: true } monster)
			{
				continue;
			}

			if (m_Monster == null)
			{
				m_Monster = monster;
				if (m_Mode == SmokeMode.MonsterAi)
				{
					// 受测猴子在相机抵达后生成时会索敌到英雄；
					// 先挪到视野外让目标按 LoseTargetTime 自然丢失，AI 场景才能从干净的巡逻开始。
					monster.GlobalPosition = new Vector2(m_Hero.GlobalPosition.X + 900f, monster.GlobalPosition.Y);
					monster.Velocity = Vector2.Zero;
					m_MonsterFoundAt = m_Time;
					GD.Print($"SMOKE[{m_Time:F2}] 找到受测猴子 HP {monster.Hp.Value}/{monster.MaxHp.Value}，已挪出视野等待丢失目标");
				}
				else
				{
					// M4 站位：悟空右侧、普攻判定范围内，供第二轮连打命中。
					monster.GlobalPosition = new Vector2(m_Hero.GlobalPosition.X + SandbagOffset, monster.GlobalPosition.Y);
					GD.Print($"SMOKE[{m_Time:F2}] 找到沙包猴子 HP {monster.Hp.Value}/{monster.MaxHp.Value} 位置 {monster.GlobalPosition}");
				}
			}

			// ai 模式保留受测怪物的 AI；M4 模式连同沙包一起全部冻结。
			if (!m_FrozenMonsters.Add(monster.Id) || (m_Mode == SmokeMode.MonsterAi && m_Monster == monster))
			{
				continue;
			}

			monster.SetAiEnabled(false);
			monster.Velocity = Vector2.Zero;
		}
	}

	private void OnDamageDealt(object sender, GameEventArgs args) // 只记录烟测目标猴子的命中事件。
	{
		if (args is DamageDealtEventArgs e && m_Monster != null && e.TargetEntityId == m_Monster.Id)
		{
			m_MonsterHits.Add((e.Damage, e.IsCrit, e.IsMiss));
			GD.Print($"SMOKE[{m_Time:F2}] 猴子受击 伤害={e.Damage} 暴击={e.IsCrit} 闪避={e.IsMiss} 剩余HP={e.TargetHp} "
				+ $"猴子x={m_Monster.GlobalPosition.X:F0} 英雄x={m_Hero.GlobalPosition.X:F0}");
		}
	}

	private void TrackPops() // 统计当前场景可见飘字数量峰值。
	{
		int count = 0;
		// FindChildren 的类型过滤只认引擎原生类名（C# 脚本类名不匹配），按 Node2D 取再判脚本类型
		foreach (Node node in GetTree().Root.FindChildren("*", "Node2D", true, false))
		{
			if (node is DamagePop pop && pop.Visible && pop.IsInsideTree())
			{
				count++;
			}
		}

		m_MaxPopCount = Mathf.Max(m_MaxPopCount, count);
	}

	private void CheckCombat(List<string> failures) // 校验猴子受击、伤害结果与飘字创建。
	{
		if (m_Monster == null)
		{
			failures.Add("场上没有找到沙包猴子");
			return;
		}

		if (m_MonsterHits.Count == 0)
		{
			failures.Add("连打期间猴子没有受到任何命中");
			return;
		}

		int total = 0;
		// 按真实伤害事件累计并检查每次命中结果。
		foreach ((int damage, bool crit, bool miss) in m_MonsterHits)
		{
			total += damage;
			if (miss || crit)
			{
				failures.Add($"双方闪避/暴击为 0 时不应出现闪避/暴击（伤害 {damage} 暴击 {crit} 闪避 {miss}）");
			}
			else if (damage < 4 || damage > 6)
			{
				failures.Add($"命中伤害 {damage} 不在手算区间 [4,6]");
			}
		}

		// 事件累计伤害应与实体血量一致，再检查命中后是否确实创建池化飘字。
		int expectedHp = Mathf.Max(0, m_Monster.MaxHp.Value - total);
		if (m_Monster.Hp.Value != expectedHp && !m_Monster.Dead)
		{
			failures.Add($"猴子 HP {m_Monster.Hp.Value} 与事件累计 {expectedHp} 不一致");
		}

		if (m_MaxPopCount == 0)
		{
			failures.Add("命中后场上没有出现过飘字（NodePool 未取出 DamagePop）");
		}

		GD.Print($"SMOKE: 猴子共受击 {m_MonsterHits.Count} 次，累计伤害 {total}，HP {m_Monster.Hp.Value}/{m_Monster.MaxHp.Value}，同屏飘字峰值 {m_MaxPopCount}");
	}

	private void Fail(string reason) // 输出失败原因及已观察序列，然后停止驱动。
	{
		GD.PrintErr($"SMOKE FAIL：{reason}");
		foreach ((double time, string path) in m_Observed)
		{
			GD.PrintErr($"  观察[{time:F2}] {path}");
		}

		StopDriving(true);
	}

	private void StopDriving(bool failed) // 退订事件并按退出参数安排引擎关闭。
	{
		// 先停止帧驱动并释放事件订阅，避免退出等待期间继续触发烟测逻辑。
		SetPhysicsProcess(false);
		Input.ActionRelease("move_left");
		Input.ActionRelease("move_right");
		Input.ActionRelease("jump");
		if (m_Hero != null)
		{
			GF.Event.Unsubscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
		}

		if (!failed && m_HasQuitAfter)
		{
			return;
		}

		SceneTreeTimer timer = GetTree().CreateTimer(0.05);
		timer.Timeout += () => GetTree().Quit(failed ? 1 : 0);
	}

	private int FirstIndex(string path) // 返回观测序列中路径首次出现的位置，不存在时为 -1。
	{
		return m_Observed.FindIndex(o => o.Path == path);
	}

	private int LastIndex(string path) // 返回观测序列中路径最后出现的位置，不存在时为 -1。
	{
		return m_Observed.FindLastIndex(o => o.Path == path);
	}
}
