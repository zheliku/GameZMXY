using System;
using GameConfig;
using GameConfig.Constant;
using GameConfig.Entity;
using GameFramework.Procedure;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.HotUpdate;
using GodotGameFramework.Scene;
using GodotGameFramework.UI;
using GameLogic;
using GameLogic.Entity.Heroes;
using GameLogic.Entity.Monsters;
using GameLogic.Manager;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 游戏流程。
/// </summary>
public class ProcedureGame : ProcedureBase
{
    /// <summary>
    /// 调试场地场景路径。
    /// M3~M5 调试场地：提供地面与相机，让控制器手感可以直接验证。
    /// </summary>
    private const string DebugArenaScenePath = "res://TheGame/Scenes/DebugArena.tscn";

    /// <summary>悟空出生点（沿用旧项目 Level_1 第 1 波的刷怪坐标量级）</summary>
    private static readonly Vector2 HeroSpawnPosition = new Vector2(300, 300);

    /// <summary>
    /// 调试猴子出生点（M5：悟空右侧 400px，在猴子索敌范围 300 之外——先巡逻，靠近后追击攻击；
    /// 用于观察索敌前巡逻、进入范围后追击攻击。
    /// </summary>
    private static readonly Vector2 MonkeySpawnPosition = new Vector2(700, 300);

    private WukongEntity m_Hero;
    private HuaguoshanMonkeyEntity m_Monkey;
    private int m_EntrySerial;
    private int m_DebugArenaOwnerEntry;

    /// <summary>
    /// 进入流程。
    /// 加载配置、重置游戏状态、创建并启动游戏状态 FSM。
    /// </summary>
    protected internal override async void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        int entrySerial = ++m_EntrySerial;

        // 标记启动成功：游戏已进入可玩状态，后续崩溃不再归因于热更
        HotUpdateSafetyGuard.MarkStartupSuccess();

        try
        {
            // M3 调试入口：加载调试场地 → 经配置驱动生成悟空（EntityId → 实体.xlsx → 场景路径）
            await GF.Scene.LoadSceneAsync(DebugArenaScenePath, LoadSceneMode.Additive);
            if (entrySerial != m_EntrySerial)
            {
                if (m_DebugArenaOwnerEntry == 0 && GF.Scene.IsSceneLoaded(DebugArenaScenePath))
                {
                    GF.Scene.UnloadScene(DebugArenaScenePath);
                }
                return;
            }

            m_DebugArenaOwnerEntry = entrySerial;

            // 飘字挂在实体组节点同一棵世界树下（与角色同坐标系）
            DamagePopManager.Instance.Activate(GF.Entity);

            WukongEntity hero = await GF.Entity.ShowEntityAsync<WukongEntity>(EntityId.Wukong, null);
            if (entrySerial != m_EntrySerial)
            {
                GF.Entity.HideEntitySafe(hero);
                return;
            }

            if (hero == null)
            {
                throw new InvalidOperationException("Failed to create Wukong entity.");
            }

            m_Hero = hero;
            m_Hero.Position = HeroSpawnPosition;

            // M5 调试怪：出生点由 MonsterEntity.OnShow 记录为巡逻圆心。
            HuaguoshanMonkeyEntity monkey = await GF.Entity.ShowEntityAsync<HuaguoshanMonkeyEntity>(EntityId.HuaguoshanMonkey, MonkeySpawnPosition);
            if (entrySerial != m_EntrySerial)
            {
                GF.Entity.HideEntitySafe(monkey);
                return;
            }

            if (monkey == null)
            {
                throw new InvalidOperationException("Failed to create Huaguoshan monkey entity.");
            }

            m_Monkey = monkey;

            (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) as LoadingForm)?.CloseLoading();
        }
        catch (Exception ex)
        {
            Log.Error("[ProcedureGame] 调试场地启动失败：{0}", ex);
            if (entrySerial == m_EntrySerial)
            {
                CleanupSession(entrySerial);
                (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) as LoadingForm)?.CloseLoading();
            }
        }
    }

    /// <summary>
    /// 离开流程。
    /// </summary>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);
        int entrySerial = m_EntrySerial;
        m_EntrySerial++;

        if (!isShutdown)
        {
            CleanupSession(entrySerial);
        }

        m_Monkey = null;
        m_Hero = null;
    }

    private void CleanupSession(int entrySerial)
    {
        DamagePopManager.Instance.Deactivate();
        GF.Entity.HideEntitySafe(m_Monkey);
        GF.Entity.HideEntitySafe(m_Hero);
        if (m_DebugArenaOwnerEntry == entrySerial)
        {
            if (GF.Scene.IsSceneLoaded(DebugArenaScenePath))
            {
                GF.Scene.UnloadScene(DebugArenaScenePath);
            }

            m_DebugArenaOwnerEntry = 0;
        }

        m_Monkey = null;
        m_Hero = null;
    }
}
