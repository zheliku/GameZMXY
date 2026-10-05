//------------------------------------------------------------
// 启动流程（检测更新）
// 游戏的入口流程，完成框架初始化、加载配置和数据表、创建实体组
//------------------------------------------------------------

using System;
using GameFramework;
using GameFramework.Localization;
using GameFramework.Procedure;
using GodotGameFramework;
using GodotGameFramework.NodePool;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;
using GameLogic;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 启动流程。
/// </summary>
public class ProcedurePrelode : ProcedureBase
{
    /// <summary>
    /// 进入流程。
    /// 执行所有初始化工作后立即切换到菜单流程。
    /// </summary>
    /// <summary>加载语言及 UI、实体和声音分组，再进入游戏流程。</summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    protected internal async override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        bool groupsLoaded = true;

        try
        {
            groupsLoaded &= LoadEntityGroup();
        }
        catch (Exception ex)
        {
            Log.Fatal("[ProcedurePrelode] 加载实体组失败（.pck 可能缺失依赖资源）: {0}", ex);
            groupsLoaded = false;
        }

        try
        {
            LoadLocalization();
        }
        catch (Exception ex)
        {
            Log.Fatal("[ProcedurePrelode] 加载本地化失败: {0}", ex);
            groupsLoaded = false;
        }

        try
        {
            groupsLoaded &= LoadUIGroup();
        }
        catch (Exception ex)
        {
            Log.Fatal("[ProcedurePrelode] 加载 UI 组失败（.pck 可能缺失依赖资源）: {0}", ex);
            groupsLoaded = false;
        }

        try
        {
            groupsLoaded &= LoadSoundGroup();
        }
        catch (Exception ex)
        {
            Log.Fatal("[ProcedurePrelode] 加载声音组失败（.pck 可能缺失依赖资源）: {0}", ex);
            groupsLoaded = false;
        }
        NodePool.Instance.Active(); // 启动节点池
        LayerMask.Instance.Active(); // 启动层级工具\
        LoadingForm loadingForm;
        try
        {
            loadingForm = await GF.UI.OpenLoadingUIFormAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal("[ProcedurePrelode] 打开加载界面失败: {0}", ex);
            return;
        }

        if (!groupsLoaded)
        {
            Log.Fatal("[ProcedurePrelode] 必要资源组加载失败，停止进入游戏。");
            loadingForm?.SetLogState("资源加载失败", 0);
            return;
        }

        ChangeState<ProcedureGame>(procedureOwner);
    }
    private void LoadLocalization() // 根据用户设置或编辑器资源模式选择界面语言。
    {
        // 运行时恢复保存语言，编辑器资源模式跟随编辑器或系统语言。
        if (!GF.Base.EnableEditorResLoad)
        {
            GF.Localization.Language = (Language)GF.Setting.GetInt("Language", (int)Language.English);
        }
        else
        {
            GF.Localization.Language = GF.Base.EditorLanguage != Language.Unspecified ? GF.Base.EditorLanguage : GF.Localization.SystemLanguage;
            Log.Info("[ProcedurePrelode] Editor res load enabled, set language to SystemLanguage: {0}.", GF.Localization.Language);
        }
    }
    private bool LoadUIGroup() // 注册资源配置中的全部 UI 分组。
    {
        // 任一分组注册失败即中止，避免后续界面使用不完整的分组配置。
        for (int i = 0; i < GF.UI.UIGroupRes.Groups.Length; i++)
        {
            if (!GF.UI.AddUIGroup(GF.UI.UIGroupRes.Groups[i].Name, GF.UI.UIGroupRes.Groups[i].Depth))
            {
                Log.Warning("Add UI group '{0}' failure.", GF.UI.UIGroupRes.Groups[i].Name);
                return false;
            }
        }
        return true;
    }
    private bool LoadEntityGroup() // 注册资源配置中的全部实体分组。
    {
        // 实体组是实体创建的前置依赖，首个注册失败时停止本组初始化。
        var groups = GF.Entity.EntityGroupRes.EntityGroups;
        for (int i = 0; i < groups.Length; i++)
        {
            if (!GF.Entity.AddEntityGroup(groups[i].Name, groups[i].ReleaseInterval, groups[i].Capacity, groups[i].ExpireTime, groups[i].Priority))
            {
                Log.Warning("Add Entity group '{0}' failure.", groups[i].Name);
                return false;
            }
        }
        return true;
    }
    private bool LoadSoundGroup() // 注册声音分组并恢复各默认声音组音量。
    {
        // 先注册所有分组，再从设置中恢复各默认组音量。
        var groups = GF.Sound.SoundGroupRes.SoundGroups;
        for (int i = 0; i < groups.Length; i++)
        {
            if (!GF.Sound.AddSoundGroup(groups[i].Name, groups[i].AgentCounts, groups[i].AvoidBeingReplacedBySamePriority))
            {
                Log.Warning("Add UI group '{0}' failure.", groups[i].Name);
                return false;
            }
        }
        GF.Sound.SetVolume(SoundComponent.DefaultMusicGroup, GF.Setting.GetFloat(SoundComponent.DefaultMusicGroup, 1));
        GF.Sound.SetVolume(SoundComponent.DefaultSfxGroup, GF.Setting.GetFloat(SoundComponent.DefaultSfxGroup, 1));
        GF.Sound.SetVolume(SoundComponent.DefaultUiGroup, GF.Setting.GetFloat(SoundComponent.DefaultUiGroup, 1));
        return true;
    }
}
