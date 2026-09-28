using System;
using System.Collections.Generic;
using GameLogic.Entity;
using Godot;

/// <summary>
/// 冒烟测试驱动器（开发工具）：仅当以 `-- --smoketest` 启动时生效，平时零开销惰性。
/// 自动注入输入（普攻连打 / 双击跑 / 受击 / 一段跳 / 二段跳），并断言悟空 AnimationTree
/// 的状态流转，打印 `SMOKE PASS` / `SMOKE FAIL`（失败附完整观察序列）。
///
/// 它验证的是"C# 属性 → 动画状态机"这条链路：断言的是 AnimationTree 里真正在播的动画节点，
/// 不是 C# 自己的日志——所以能抓住"属性变了但动画没切"这一类问题。
///
/// 用法：S:\Godot4\Godot4CSharp_console.exe --headless --path Godot/GodotProject --quit-after 1500 -- --smoketest
///
/// 注意：**判定看 stdout 的 SMOKE PASS/FAIL，别只看退出码**——框架关闭流程有一个既有 bug
/// （WebRequestAgentHelper.Reset 访问已释放的 HttpRequest，见 Framework/…/DefaultWebRequestAgentHelper.cs:90），
/// 偶发在退出时升级成 0xC0000005 段错误，把退出码冲掉；这是框架问题，与本测试无关，已上报。
/// </summary>
public partial class SmokeTestDriver : Node
{
	/// <summary>
	/// 时间轴：[时刻(秒), 动作, 模式]。
	/// mash=逐帧连打 / tap=单帧 / hold=按住 / release=松开 / damage=对英雄造成 1 点伤害（验证受击硬直）
	/// </summary>
	private static readonly (double At, string Action, string Mode)[] Plan =
	{
		(2.00, "attack", "mash"),
		(4.00, "attack", "release"),
		(5.00, "move_right", "tap"),
		(5.15, "move_right", "hold"),
		(6.30, "move_right", "release"),
		(6.35, "hurt", "damage"),
		(6.70, "jump", "tap"),
		(7.40, "jump", "tap"),
	};

	/// <summary>
	/// 断言必须观察到（顺序也必须一致）的状态，格式 `组/动画`（主图单状态如 Hurt 没有斜杠）。
	/// 覆盖：连段 1→4、双击跑、受击、一段跳、下落、二段跳、落地回待机。
	/// </summary>
	private static readonly string[] Required =
	{
		"Attack/attack_1", "Attack/attack_2", "Attack/attack_3", "Attack/attack_4",
		"Ground/run", "Hurt", "Air/jump", "Air/fall", "Air/jump_2", "Ground/Idle/idle1",
	};

	/// <summary>顺序约束：[A, B] 表示 A 必须先于 B 出现；*Last 表示取该状态的**最后**一次出现</summary>
	private static readonly (string A, string B, bool LastA, bool LastB)[] Order =
	{
		("Attack/attack_1", "Attack/attack_2", false, false),
		("Attack/attack_2", "Attack/attack_3", false, false),
		("Attack/attack_3", "Attack/attack_4", false, false),
		("Attack/attack_4", "Ground/run", false, false),
		("Ground/run", "Hurt", false, false),
		("Hurt", "Air/jump", false, false),
		("Air/jump", "Air/jump_2", false, false),
		("Air/jump", "Air/fall", false, true),      // 出生时也会下落，取最后一次 fall（起跳后的下落）
		("Air/jump_2", "Ground/Idle/idle1", false, true), // 落地后回待机，取最后一次 idle1
	};

	private const double EndTime = 10.0;
	private const double FindTimeout = 5.0;

	private AnimationTree m_Tree;
	private HeroEntity m_Hero;
	private bool m_Active;
	private double m_WaitTime;
	private double m_Time;
	private int m_NextStep;
	private bool m_Mashing;
	private double m_MashUntil;
	private int m_MashFrame;
	private string m_LastPath = "";
	private bool m_HasQuitAfter;
	private readonly List<(double Time, string Path)> m_Observed = new();

	/// <summary>walk/run 期间身体层出现过的帧号（回归：动画库漏 m_Body:frame 轨道时身体冻结）</summary>
	private readonly HashSet<int> m_WalkFrames = new();
	private readonly HashSet<int> m_RunFrames = new();

	public override void _Ready()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.Contains("smoketest"))
			{
				m_Active = true;
			}
		}

		// 引擎参数里带了 --quit-after 时，通过流程交给引擎自己退出（见 StopDriving 注释）
		foreach (string arg in OS.GetCmdlineArgs())
		{
			if (arg == "--quit-after")
			{
				m_HasQuitAfter = true;
			}
		}

		if (!m_Active)
		{
			SetPhysicsProcess(false);
		}

		// 排在实体与 AnimationTree 之后处理：采样到的才是"本帧更新完"的状态，
		// 否则读到的永远是上一帧的树状态（会误判成"动画晚一帧"）。
		ProcessPriority = 1000;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (m_Tree == null)
		{
			m_WaitTime += delta;
			Godot.Collections.Array<Node> found = GetTree().Root.FindChildren("*", "AnimationTree", true, false);
			if (found.Count == 0)
			{
				if (m_WaitTime > FindTimeout)
				{
					Fail("5 秒内没有找到 AnimationTree（游戏没起来？）");
				}

				return;
			}

			m_Tree = found[0] as AnimationTree;
			m_Hero = m_Tree?.GetParent() as HeroEntity;
			GD.Print("SMOKE: 找到 hero，开始时间轴");
		}

		m_Time += delta;
		DriveInput();
		Sample();
		TrackBodyFrames();
		if (m_Time >= EndTime)
		{
			Finish();
		}
	}

	private void DriveInput()
	{
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
					m_Hero.TakeDamage(1, -1);   // 验证受击：硬直 + hurt 动画
					break;
			}

			m_NextStep++;
		}
	}

	private void Sample()
	{
		string path = CurrentStatePath();
		if (path.Length == 0 || path == m_LastPath)
		{
			return;
		}

		m_LastPath = path;
		m_Observed.Add((m_Time, path));
		GD.Print($"SMOKE[{m_Time:F2}] 动画状态 {path}  (属性 {Facts()})");
	}

	/// <summary>
	/// 树当前的状态路径：从主图逐层下钻（主图 → 组子机 → 组内子机如 Idle），
	/// 直到当前节点不是状态机为止。例：Ground/Idle/idle1、Attack/attack_2、Hurt。
	/// </summary>
	private string CurrentStatePath()
	{
		if (m_Tree.Get("parameters/playback").As<AnimationNodeStateMachinePlayback>() is not { } playback)
		{
			return "";
		}

		List<string> names = new();
		string prefix = "";
		for (int depth = 0; depth < 4; depth++)
		{
			string node = playback.GetCurrentNode().ToString();
			if (node.Length == 0)
			{
				break;
			}

			names.Add(node);
			prefix += node + "/";
			if (m_Tree.Get($"parameters/{prefix}playback").As<AnimationNodeStateMachinePlayback>() is not { } child)
			{
				break;
			}

			playback = child;
		}

		return string.Join("/", names);
	}

	/// <summary>
	/// 身体帧采样：walk/run 状态下记录 m_Body 的帧号。动画库若漏了 m_Body:frame 轨道，
	/// 帧会停在进入前的一个值上不动——这里用"出现过的不同帧数"抓这类回归。
	/// </summary>
	private void TrackBodyFrames()
	{
		if (m_Hero?.Body == null)
		{
			return;
		}

		if (m_LastPath == "Ground/walk")
		{
			m_WalkFrames.Add(m_Hero.Body.Frame);
		}
		else if (m_LastPath == "Ground/run")
		{
			m_RunFrames.Add(m_Hero.Body.Frame);
		}
	}

	/// <summary>角色属性快照（HeroEntity 的表达式事实面），用于日志与调试。</summary>
	private string Facts()
	{
		return $"move={m_Hero.MoveInput} run={m_Hero.Running} air={m_Hero.Airborne} rise={m_Hero.Rising} "
			+ $"jump={m_Hero.JumpCount} seg={m_Hero.AttackSegment} hurt={m_Hero.Hurt} emote={m_Hero.Emoting} dead={m_Hero.Dead}";
	}

	private void Finish()
	{
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

		CheckEffectLayer(failures);
		CheckLocomotionFrames(failures);

		if (failures.Count == 0)
		{
			GD.Print($"SMOKE PASS：观察到 {m_Observed.Count} 次状态切换，断言全部通过");
			StopDriving(false);
			return;
		}

		Fail(string.Join("；", failures));
	}

	/// <summary>
	/// 特效层检查：待机时必须是"空白"状态（动画 empty、帧 0、scale 1）。
	/// 抓的是"AnimationTree 切到不含某属性轨道的动画时该属性被写成垃圾值"这一类问题
	/// （旧库漏轨道时实测 scale 被写成 1e-05，棍气不可见）。
	/// </summary>
	private void CheckEffectLayer(List<string> failures)
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

	/// <summary>
	/// 走/跑身体帧检查：run 期间至少出现 3 个不同帧（4 帧循环），walk 至少出现 1 个有效帧。
	/// 直接针对"行走、奔跑动画身体冻结"这一类动画库缺轨道的回归。
	/// </summary>
	private void CheckLocomotionFrames(List<string> failures)
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

	private void Fail(string reason)
	{
		GD.PrintErr($"SMOKE FAIL：{reason}");
		foreach ((double time, string path) in m_Observed)
		{
			GD.PrintErr($"  观察[{time:F2}] {path}");
		}

		StopDriving(true);
	}

	/// <summary>
	/// 结束驱动。失败必须自己退出（带非零码，供自动化判断）；通过则优先交给引擎退出：
	/// 引擎自己的退出路径（`--quit-after`）干净，而从节点回调里 Quit 会踩到框架关闭期的
	/// 既有 bug（见类注释），偶发 0xC0000005 把退出码冲掉。没带 `--quit-after` 时才自己退，
	/// 免得进程悬着。
	/// </summary>
	private void StopDriving(bool failed)
	{
		SetPhysicsProcess(false);
		if (!failed && m_HasQuitAfter)
		{
			return;
		}

		SceneTreeTimer timer = GetTree().CreateTimer(0.05);
		timer.Timeout += () => GetTree().Quit(failed ? 1 : 0);
	}

	private int FirstIndex(string path)
	{
		return m_Observed.FindIndex(o => o.Path == path);
	}

	private int LastIndex(string path)
	{
		return m_Observed.FindLastIndex(o => o.Path == path);
	}
}
