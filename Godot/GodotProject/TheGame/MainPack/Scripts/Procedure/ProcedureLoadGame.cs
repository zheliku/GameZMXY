//------------------------------------------------------------
// 读档流程：读取当前存档槽（当前格式读取或建档），创建档案作用域
//------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using GameConfig.Constant;
using GameFramework.Procedure;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Equipment;
using GameLogic.Procedure.FlowData;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Skills;
using GodotGameFramework;
using GodotGameFramework.UI;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 读档流程：经 <see cref="SaveSystem"/> 读取当前存档（首次建档或当前格式读取），
/// 借用应用 SaveSystem，完整加载后注册ProfileModel、ProfileSystem 与 EquipmentSystem；新旧档案进入地图。
/// 读档失败时停在加载界面，绝不新建空档覆盖玩家数据。
/// </summary>
public class ProcedureLoadGame : ProcedureBase
{
    private int _entrySerial; // 每次进出使旧异步结果失效，不让离开后的读档覆盖新作用域。
    private CancellationTokenSource _cancellation; // 离开时停止迟到读档结果的校验与发布，已提交的磁盘操作仍由框架完成。

    /// <summary>
    /// 读档并创建档案作用域。
    /// </summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    protected internal override async void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        int entrySerial = ++_entrySerial;
        _cancellation = new CancellationTokenSource();
        CancellationToken token = _cancellation.Token;
        bool ownsProfile = false;
        try
        {
            // 标题界面已关闭加载页；读档期间重新显示框架共享加载界面。
            await GF.UI.OpenLoadingUIFormAsync();
            if (entrySerial != _entrySerial)
            {
                return;
            }
            // 准备流程已建立 Architecture 并注册 ConfigurationModel。
            IArchitecture architecture = procedureOwner.GetArchitecture();
            (int SlotNumber, long UnitId, int HeroId) selection = procedureOwner.GetData<SaveSelectionVariable>(FlowKeys.SaveSelection)?.Value
                ?? throw new InvalidOperationException("缺少存档选择结果。");
            // 先完成读档和校验，再一次发布状态与系统；等待期间不暴露半份运行时数据。
            SaveSystem saveSystem = procedureOwner.GetSystem<SaveSystem>();
            (GameData Data, SaveOrigin Origin) loaded = await saveSystem.LoadOrCreateAsync(selection.SlotNumber, selection.UnitId, token, selection.HeroId);
            if (entrySerial != _entrySerial)
            {
                return;
            }
            // 校验成功后由流程创建、发布档案模型；不把 Model 再包装成 LoadResult。
            GameData data = loaded.Data;
            ConfigurationModel config = procedureOwner.GetModel<GameLogic.Config.ConfigurationModel>();
            var profile = new ProfileModel(data.Heroes.Select(x => (x.HeroId, x.TotalExperience)), procedureOwner.GetUtility<ExperienceCurveUtility>(),
                config.Tables.TbProfileConfig.EquipmentCapacity);
            procedureOwner.RegisterModel(profile);
            ownsProfile = true;
            procedureOwner.RegisterModel(new SkillModel(profile.HeroIds));
            procedureOwner.RegisterSystem(new ProfileSystem(architecture));
            procedureOwner.GetSystem<ProfileSystem>().RestoreCheckpoint(data);
            procedureOwner.RegisterSystem(new EquipmentSystem(architecture));
            procedureOwner.RegisterSystem(new SkillSystem(architecture));
            Log.Info("[ProcedureLoadGame] 档案就绪：来源 {0}，出战英雄 {1}，等级 {2}", loaded.Origin,
                profile.HeroOf(PlayerSlot.P1), profile.GetProgress(profile.HeroOf(PlayerSlot.P1)).Value.Level);
            // 空槽已在首次写盘前确认角色；新旧档案都沿同一已装配的地图入口继续。
            ChangeState<ProcedureMap>(procedureOwner);
        }
        catch (Exception ex)
        {
            if (entrySerial != _entrySerial)
            {
                return;
            }

            if (ownsProfile)
            {
                // 只清理本次已经开始装配的档案，保留应用对象与磁盘文件供下次读取。
                procedureOwner.RemoveSystem<SkillSystem>();
                procedureOwner.RemoveSystem<EquipmentSystem>();
                procedureOwner.RemoveSystem<ProfileSystem>();
                procedureOwner.RemoveModel<SkillModel>();
                procedureOwner.RemoveModel<ProfileModel>();
            }
            Log.Fatal("[ProcedureLoadGame] 读档失败：{0}", ex);
            (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) as LoadingForm)?.SetLogState("存档读取失败", 0);
        }
    }

    /// <summary>
    /// 使尚未完成的加载结果失效；磁盘读写仍由 SaveSystem 串行完成。
    /// </summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    /// <param name="isShutdown">是否框架关停。</param>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        ++_entrySerial;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        base.OnLeave(procedureOwner, isShutdown);
    }
}
