using GameConfig;
using GameConfig.Level;
using GameConfig.UI;
using GameFramework;
using GameFramework.Event;
using GameFramework.Fsm;
using GameFramework.Procedure;
using GameLogic;
using GameLogic.Event;
using Godot;
using GodotGameFramework;
using GodotGameFramework.HotUpdate;
using GodotGameFramework.UI;
using System;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 菜单流程（M6 起）：打开选人界面（兼作开始界面）并等待"开始战斗"请求，收到后写入对局参数
/// （关卡/英雄）切到 <see cref="ProcedureBattle"/>。M3~M5 的"直接进调试场地"入口移入对局流程的
/// 沙盒分支（--smoketest 专用）。
///
/// 职责单一（MainPack/AGENTS.md）：流程是入口链路，不写玩法——选人信息在表与 UI，对局内容在
/// LevelDirector，这里只做"转发请求 + 切流程"。
/// </summary>
public class ProcedureGame : ProcedureBase
{
	private HeroSelectForm m_SelectForm;
	private bool m_Subscribed;
	private ProcedureOwner m_ProcedureOwner;

	/// <summary>
	/// 状态初始化（只调用一次）。
	/// </summary>
	protected internal override void OnInit(ProcedureOwner procedureOwner)
	{
		base.OnInit(procedureOwner);
	}

	/// <summary>
	/// 进入流程：冒烟测试跳过菜单直进沙盒对局（SmokeTestDriver 靠场景树找英雄，5 秒内必须在场）；
	/// 正常路径打开选人界面后收掉加载遮罩（遮罩由 ProcedurePrelode 打开并保持到此）。
	/// </summary>
	protected internal override async void OnEnter(ProcedureOwner procedureOwner)
	{
		base.OnEnter(procedureOwner);
		m_ProcedureOwner = procedureOwner;

		// 标记启动成功：游戏已进入可玩状态，后续崩溃不再归因于热更
		HotUpdateSafetyGuard.MarkStartupSuccess();

		if (IsSmokeTestRequested())
		{
			// --smoketest / =ai → 沙盒（DebugArena，SmokeTestDriver 驱动断言）；
			// --smoketest=level → 正常关卡对局的启动检查（跳过菜单直进 Level_1，不驱动输入，看日志）
			bool sandbox = !IsLevelBootSmokeTest();
			procedureOwner.SetData<VarInt32>(ProcedureBattle.DataBattleLevelId, 1);
			procedureOwner.SetData<VarInt32>(ProcedureBattle.DataBattleHeroId, 1);
			procedureOwner.SetData<VarBoolean>(ProcedureBattle.DataBattleSandbox, sandbox);
			ChangeState<ProcedureBattle>(procedureOwner);
			return;
		}

		GF.Event.Subscribe(StartBattleRequestedEventArgs.EventId, OnStartBattleRequested);
		m_Subscribed = true;

		try
		{
			m_SelectForm = await GF.UI.OpenUIFormAsync<HeroSelectForm>(UIFormId.HeroSelectForm);
		}
		catch (Exception ex)
		{
			Log.Fatal("[ProcedureGame] 选人界面打开失败：{0}", ex);
		}
		finally
		{
			LoadingForm.Current?.CloseLoading();
		}
	}

	/// <summary>
	/// 每帧更新。
	/// </summary>
	protected internal override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
	{
		base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);
	}

	/// <summary>
	/// 离开流程：退订请求事件、关掉选人界面（进入对局时它不该留在 HUD 之下）。
	/// </summary>
	protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
	{
		if (m_Subscribed)
		{
			GF.Event.Unsubscribe(StartBattleRequestedEventArgs.EventId, OnStartBattleRequested);
			m_Subscribed = false;
		}

		if (!isShutdown && m_SelectForm != null)
		{
			GF.UI.CloseUIForm(m_SelectForm);
		}

		m_SelectForm = null;
		m_ProcedureOwner = null;
		base.OnLeave(procedureOwner, isShutdown);
	}

	/// <summary>"开始战斗"：取第一个关卡（垂直切片单关卡），写入对局参数后切对局流程。</summary>
	private void OnStartBattleRequested(object sender, GameEventArgs args)
	{
		if (args is not StartBattleRequestedEventArgs e || m_ProcedureOwner == null)
		{
			return;
		}

		LevelConfig level = null;
		foreach (LevelConfig row in ConfigSystem.Instance.Tables.TbLevelConfig.DataList)
		{
			level = row;
			break;
		}

		if (level == null)
		{
			Log.Error("[ProcedureGame] LevelConfig 没有任何行，无法开始对局");
			return;
		}

		m_ProcedureOwner.SetData<VarInt32>(ProcedureBattle.DataBattleLevelId, level.Id);
		m_ProcedureOwner.SetData<VarInt32>(ProcedureBattle.DataBattleHeroId, e.HeroId);
		m_ProcedureOwner.SetData<VarBoolean>(ProcedureBattle.DataBattleSandbox, false);
		ChangeState<ProcedureBattle>(m_ProcedureOwner);
	}

	/// <summary>是否以 --smoketest 启动（与 SmokeTestDriver 同一判定：命令行用户参数含 "smoketest"）。</summary>
	private static bool IsSmokeTestRequested()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.Contains("smoketest"))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>是否为关卡对局启动检查（--smoketest=level：进真实关卡但不驱动输入，供 headless 验证链路）。</summary>
	private static bool IsLevelBootSmokeTest()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.EndsWith("=level"))
			{
				return true;
			}
		}

		return false;
	}
}
