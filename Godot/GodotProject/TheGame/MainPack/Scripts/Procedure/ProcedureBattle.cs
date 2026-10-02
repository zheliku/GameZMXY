using System;
using System.Linq;
using GameConfig;
using GameConfig.Entity;
using GameConfig.Level;
using GameConfig.UI;
using GameFramework;
using GameFramework.Event;
using GameFramework.Fsm;
using GameFramework.Procedure;
using GameLogic;
using GameLogic.Archive;
using GameLogic.Event;
using GameLogic.Manager;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.Scene;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;
using GameLogic.Entity.Heroes;
using GameLogic.Entity.Monsters;
using GameConfig.Entity;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 对局流程（M6）：一场关卡战斗的完整生命周期——加载遮罩 → 关卡场景（GF.Scene）→ HUD →
/// <see cref="LevelDirector"/>（波次/出口/判负判胜）→ 结算界面 → 重试（重进自身）/返回选人（回 ProcedureGame）。
///
/// 分工（行业惯例 × GGF 结构）：
///  * 流程只做编排：加载/表单开关/切流程/写存档；关卡内容（波次/刷怪/闸门/出口）全在 LevelDirector；
///  * 与 Director/UI 之间只走事件（LevelCleared / LevelFailed / Retry / ReturnToMenu），互相不持引用；
///  * 对局参数经流程 FSM 数据传入（<see cref="DataBattleLevelId"/> 等，ProcedureGame 在切流程前写入）；
///  * <c>--smoketest</c> 走沙盒分支（DebugArena + 悟空 + 猴子，无表单无 Director），完整复刻 M3~M5 的
///    冒烟测试环境——SmokeTestDriver 靠场景树找英雄，必须跳过菜单直进对局。
///
/// async 时序（根规范 §5.2）：OnEnter 为 async void，所有 await 包 try/catch；事件先订阅后开局；
/// 事件参数即发即收，不进闭包。
/// </summary>
public class ProcedureBattle : ProcedureBase
{
	// ---- 流程间传参键（ProcedureGame 写入；沙盒模式由本流程兜底默认值）----

	/// <summary>对局关卡ID（LevelConfig.Id；沙盒模式无意义）</summary>
	public const string DataBattleLevelId = "BattleLevelId";

	/// <summary>对局英雄ID（HeroConfig.Id）</summary>
	public const string DataBattleHeroId = "BattleHeroId";

	/// <summary>是否沙盒对局（--smoketest 调试通道：DebugArena、无表单、无 Director）</summary>
	public const string DataBattleSandbox = "BattleSandbox";

	/// <summary>调试场地场景路径（沙盒对局用；M3~M5 冒烟测试环境）</summary>
	private static readonly string DebugArenaScenePath = "res://TheGame/Scenes/DebugArena.tscn";

	/// <summary>沙盒出生点（沿用 M3~M5：悟空 300，猴子 700，在索敌范围外先巡逻）</summary>
	private static readonly Vector2 DebugHeroSpawnPosition = new Vector2(300, 300);

	/// <summary>沙盒猴子出生点</summary>
	private static readonly Vector2 DebugMonkeySpawnPosition = new Vector2(700, 300);

	/// <summary>失败结算延迟秒：等英雄死亡动画播完再弹结算（表现节奏常数，非玩法数值）</summary>
	private static readonly float FailFormDelaySeconds = 2.0f;

	private ProcedureOwner m_ProcedureOwner;
	private string m_ScenePath;
	private HudForm m_HudForm;
	private GameOverForm m_GameOverForm;
	private bool m_Subscribed;
	private bool m_DirectorStarted;
	private bool m_ResultHandled;
	private bool m_FailPending;
	private float m_FailFormTimer;

	/// <summary>
	/// 进入流程：订阅结果事件 → 开加载遮罩 → 加载关卡场景 →（沙盒：生成调试实体 / 正常：开 HUD + Director）。
	/// </summary>
	protected internal override async void OnEnter(ProcedureOwner procedureOwner)
	{
		base.OnEnter(procedureOwner);
		m_ProcedureOwner = procedureOwner;
		m_ScenePath = null;
		m_HudForm = null;
		m_GameOverForm = null;
		m_ResultHandled = false;
		m_FailPending = false;
		m_FailFormTimer = 0f;

		int levelId = GetDataInt(procedureOwner, DataBattleLevelId, 0);
		int heroId = GetDataInt(procedureOwner, DataBattleHeroId, 1);
		bool sandbox = GetDataBool(procedureOwner, DataBattleSandbox, false);

		SubscribeResultEvents();

		try
		{
			LevelConfig level = null;
			if (!sandbox)
			{
				level = ConfigSystem.Instance.Tables.TbLevelConfig.GetOrDefault(levelId);
				if (level == null)
				{
					Log.Fatal("[ProcedureBattle] LevelConfig 缺失行：Id={0}，无法开始对局", levelId);
					return;
				}
			}

			m_ScenePath = sandbox ? DebugArenaScenePath : level.ScenePath;
			Node scene = await GF.Scene.LoadSceneAsync(m_ScenePath, LoadSceneMode.Single);
			if (scene == null)
			{
				Log.Fatal("[ProcedureBattle] 关卡场景加载失败：{0}", m_ScenePath);
				return;
			}

			// 飘字挂实体组世界树（同 M3~M5）
			DamagePopManager.Instance.Activate(GF.Entity);

			if (sandbox)
			{
				await StartSandboxBattle();
			}
			else
			{
				await StartLevelBattle(levelId, heroId, scene as Node2D);
			}
		}
		catch (Exception ex)
		{
			Log.Fatal("[ProcedureBattle] 对局初始化失败：{0}", ex);
		}
		finally
		{
			// 主菜单/对局内容就绪，收掉加载遮罩（遮罩由 ProcedurePrelode 打开并保持到此）
			LoadingForm.Current?.CloseLoading();
		}
	}

	/// <summary>
	/// 每帧更新：失败结算延迟计时（等死亡动画）。
	/// </summary>
	protected internal override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
	{
		base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);

		if (!m_FailPending)
		{
			return;
		}

		m_FailFormTimer -= elapseSeconds;
		if (m_FailFormTimer <= 0f)
		{
			m_FailPending = false;
			OpenResultForm(false, 0f, 0);
		}
	}

	/// <summary>
	/// 离开流程（重试重入 / 返回选人 / 关停）：统一收尾——关表单、停导演（收怪收英雄）、
	/// 停飘字与 BGM、卸载关卡场景。怪物尸体（已死亡待回收）由实体组自行收尾。
	/// </summary>
	protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
	{
		UnsubscribeResultEvents();

		if (!isShutdown)
		{
			CloseForms();
			if (m_DirectorStarted)
			{
				LevelDirector.Instance.StopBattle();
			}

			DamagePopManager.Instance.Deactivate();
			GF.Sound.StopBGM();
			if (!string.IsNullOrEmpty(m_ScenePath) && GF.Scene.IsSceneLoaded(m_ScenePath))
			{
				GF.Scene.UnloadScene(m_ScenePath);
			}
		}

		m_DirectorStarted = false;
		m_ScenePath = null;
		m_ProcedureOwner = null;
		m_ResultHandled = false;
		m_FailPending = false;
		base.OnLeave(procedureOwner, isShutdown);
	}

	// ---- 开局 ----

	/// <summary>沙盒对局（--smoketest）：DebugArena + 悟空 + 一只带 AI 的猴子，完整复刻 M3~M5 冒烟环境。</summary>
	private async System.Threading.Tasks.Task StartSandboxBattle()
	{
		WukongEntity hero = await GF.Entity.ShowEntityAsync<WukongEntity>(EntityId.Wukong, null);
		if (hero != null)
		{
			hero.Position = DebugHeroSpawnPosition;
		}

		await GF.Entity.ShowEntityAsync<HuaguoshanMonkeyEntity>(EntityId.HuaguoshanMonkey, DebugMonkeySpawnPosition);
	}

	/// <summary>正常对局：先开 HUD（订阅状态事件），后开导演（生成英雄即广播初始状态/第 1 波，顺序保证 HUD 收得到）。</summary>
	private async System.Threading.Tasks.Task StartLevelBattle(int levelId, int heroId, Node2D sceneRoot)
	{
		m_HudForm = await GF.UI.OpenUIFormAsync<HudForm>(UIFormId.HudForm);

		// 记录所选英雄（存档字段；通关时随记录一起落盘）
		if (GF.Archive.CurrentData != null)
		{
			GF.Archive.CurrentData.SelectedHeroId = heroId;
		}

		await LevelDirector.Instance.StartLevel(levelId, heroId, sceneRoot);
		m_DirectorStarted = true;
	}

	// ---- 结果事件 ----

	/// <summary>通关：写存档记录 + 打开胜利结算。</summary>
	private void OnLevelCleared(object sender, GameEventArgs args)
	{
		if (m_ResultHandled || args is not LevelClearedEventArgs e)
		{
			return;
		}

		m_ResultHandled = true;
		SaveClearRecord(e);
		OpenResultForm(true, e.ElapsedSeconds, e.KillCount, e.HeroHp, e.HeroMaxHp);
	}

	/// <summary>失败：等死亡动画后再弹结算（计时在 OnUpdate 推进）。</summary>
	private void OnLevelFailed(object sender, GameEventArgs args)
	{
		if (m_ResultHandled || args is not LevelFailedEventArgs e)
		{
			return;
		}

		m_ResultHandled = true;
		m_FailPending = true;
		m_FailFormTimer = FailFormDelaySeconds;
	}

	/// <summary>"再次挑战"：重进自身流程（OnLeave 完成全部收尾，FSM 数据保留同一关/同一英雄）。</summary>
	private void OnRetryBattleRequested(object sender, GameEventArgs args)
	{
		ChangeState<ProcedureBattle>(m_ProcedureOwner);
	}

	/// <summary>"返回选人"：切回菜单流程。</summary>
	private void OnReturnToMenuRequested(object sender, GameEventArgs args)
	{
		ChangeState<ProcedureGame>(m_ProcedureOwner);
	}

	/// <summary>写通关记录（最好用时取最小值、次数累计）并覆盖存档；fire-and-forget，异常自捕获。
	/// 事件参数在方法入口即拷贝为本地值（根规范 §5.2：await 后不再读事件参数）。</summary>
	private async void SaveClearRecord(LevelClearedEventArgs e)
	{
		int levelId = e.LevelId;
		float elapsedSeconds = e.ElapsedSeconds;
		try
		{
			GameData data = GF.Archive.CurrentData;
			if (data == null)
			{
				Log.Error("[ProcedureBattle] 存档数据未加载，通关记录未写入");
				return;
			}

			LevelClearRecord record = data.LevelClearRecords.FirstOrDefault(r => r.LevelId == levelId);
			if (record == null)
			{
				record = new LevelClearRecord { LevelId = levelId, BestSeconds = elapsedSeconds, ClearCount = 0 };
				data.LevelClearRecords.Add(record);
			}

			record.BestSeconds = Math.Min(record.BestSeconds, elapsedSeconds);
			record.ClearCount++;
			await GF.Archive.OverWriteAsync();
			Log.Info("[ProcedureBattle] 通关记录已保存：关卡 {0}，用时 {1:F1}s，累计 {2} 次",
				levelId, elapsedSeconds, record.ClearCount);
		}
		catch (Exception ex)
		{
			Log.Error("[ProcedureBattle] 通关记录写入失败：{0}", ex);
		}
	}

	/// <summary>打开结算界面（载荷纯数据，不引用实体）。</summary>
	private async void OpenResultForm(bool victory, float elapsedSeconds, int killCount, int heroHp = 0, int heroMaxHp = 0)
	{
		try
		{
			GameOverPayload payload = new GameOverPayload
			{
				IsVictory = victory,
				ElapsedSeconds = elapsedSeconds,
				KillCount = killCount,
				HeroHp = heroHp,
				HeroMaxHp = heroMaxHp,
			};
			m_GameOverForm = await GF.UI.OpenUIFormAsync<GameOverForm>(UIFormId.GameOverForm, payload);
		}
		catch (Exception ex)
		{
			Log.Fatal("[ProcedureBattle] 结算界面打开失败：{0}", ex);
		}
	}

	// ---- 表单与订阅收尾 ----

	/// <summary>关掉本流程打开的表单（HUD / 结算）；池化实例由 UI 系统回收。</summary>
	private void CloseForms()
	{
		if (m_HudForm != null)
		{
			GF.UI.CloseUIForm(m_HudForm);
			m_HudForm = null;
		}

		if (m_GameOverForm != null)
		{
			GF.UI.CloseUIForm(m_GameOverForm);
			m_GameOverForm = null;
		}
	}

	private void SubscribeResultEvents()
	{
		if (m_Subscribed)
		{
			return;
		}

		GF.Event.Subscribe(LevelClearedEventArgs.EventId, OnLevelCleared);
		GF.Event.Subscribe(LevelFailedEventArgs.EventId, OnLevelFailed);
		GF.Event.Subscribe(RetryBattleRequestedEventArgs.EventId, OnRetryBattleRequested);
		GF.Event.Subscribe(ReturnToMenuRequestedEventArgs.EventId, OnReturnToMenuRequested);
		m_Subscribed = true;
	}

	private void UnsubscribeResultEvents()
	{
		if (!m_Subscribed)
		{
			return;
		}

		GF.Event.Unsubscribe(LevelClearedEventArgs.EventId, OnLevelCleared);
		GF.Event.Unsubscribe(LevelFailedEventArgs.EventId, OnLevelFailed);
		GF.Event.Unsubscribe(RetryBattleRequestedEventArgs.EventId, OnRetryBattleRequested);
		GF.Event.Unsubscribe(ReturnToMenuRequestedEventArgs.EventId, OnReturnToMenuRequested);
		m_Subscribed = false;
	}

	private static int GetDataInt(ProcedureOwner procedureOwner, string key, int defaultValue)
	{
		VarInt32 value = procedureOwner.GetData<VarInt32>(key);
		return value?.Value ?? defaultValue;
	}

	private static bool GetDataBool(ProcedureOwner procedureOwner, string key, bool defaultValue)
	{
		VarBoolean value = procedureOwner.GetData<VarBoolean>(key);
		return value?.Value ?? defaultValue;
	}
}
