using GameConfig;
using GameConfig.Entity;
using GameFramework.Procedure;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.HotUpdate;
using GodotGameFramework.Scene;
using GodotGameFramework.UI;
using GameLogic;
using GameLogic.Entity;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 游戏流程。
/// </summary>
public class ProcedureGame : ProcedureBase
{
    /// <summary>
    /// 调试场地场景路径。
    /// M3~M5 阶段用它代替关卡：提供地面与相机，让控制器手感可以直接验证；
    /// M6 起改由 LevelConfig.ScenePath 驱动（根规范 §14 M6）。
    /// </summary>
    private const string DebugArenaScenePath = "res://TheGame/Scenes/DebugArena.tscn";

    /// <summary>悟空出生点（沿用旧项目 Level_1 第 1 波的刷怪坐标量级）</summary>
    private static readonly Vector2 HeroSpawnPosition = new Vector2(300, 300);

    /// <summary>
    /// 状态初始化（只调用一次）。
    /// 获取组件引用，订阅事件。
    /// </summary>
    protected internal override void OnInit(ProcedureOwner procedureOwner)
    {
        base.OnInit(procedureOwner);

    }

    /// <summary>
    /// 进入流程。
    /// 加载配置、重置游戏状态、创建并启动游戏状态 FSM。
    /// </summary>
    protected internal override async void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);

        // 标记启动成功：游戏已进入可玩状态，后续崩溃不再归因于热更
        HotUpdateSafetyGuard.MarkStartupSuccess();

        // M3 调试入口：加载调试场地 → 经配置驱动生成悟空（EntityId → 实体.xlsx → 场景路径）
        await GF.Scene.LoadSceneAsync(DebugArenaScenePath, LoadSceneMode.Additive);

        WukongEntity hero = await GF.Entity.ShowEntityAsync<WukongEntity>(EntityId.Wukong, null);
        if (hero != null)
        {
            hero.Position = HeroSpawnPosition;
        }

        // 主菜单已打开，收掉加载遮罩（遮罩由 ProcedurePrelode 打开并保持到此）
        LoadingForm.Current?.CloseLoading();
    }

    /// <summary>
    /// 每帧更新。
    /// </summary>
    protected internal override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);

    }

    /// <summary>
    /// 离开流程。
    /// </summary>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);


    }
}
