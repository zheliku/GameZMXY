//------------------------------------------------------------
// 预加载流程：注册实体/UI/声音分组与本地化、启动节点池，并集中校验配置
//------------------------------------------------------------

using System;
using GameFramework;
using GameFramework.Localization;
using GameFramework.Procedure;
using GameLogic.Config;
using GameLogic.Profile;
using GodotGameFramework;
using GodotGameFramework.NodePool;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;
using GameLogic;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 预加载流程：资源组与本地化注册、节点池启动、配置集中校验；全部成功后进入读档流程。
/// 任一步失败即停在加载界面并记录致命错误，不进入游戏。
/// </summary>
public class ProcedurePreload : ProcedureBase
{
    /// <summary>流程数据中保存已校验经验曲线的键（读档流程取用）。</summary>
    public const string CurveDataKey = "ExperienceCurve";

    /// <summary>加载语言及 UI、实体和声音分组，校验配置，再进入读档流程。</summary>
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
            Log.Fatal("[ProcedurePreload] 加载实体组失败（.pck 可能缺失依赖资源）: {0}", ex);
            groupsLoaded = false;
        }

        try
        {
            LoadLocalization();
        }
        catch (Exception ex)
        {
            Log.Fatal("[ProcedurePreload] 加载本地化失败: {0}", ex);
            groupsLoaded = false;
        }

        try
        {
            groupsLoaded &= LoadUIGroup();
        }
        catch (Exception ex)
        {
            Log.Fatal("[ProcedurePreload] 加载 UI 组失败（.pck 可能缺失依赖资源）: {0}", ex);
            groupsLoaded = false;
        }

        try
        {
            groupsLoaded &= LoadSoundGroup();
        }
        catch (Exception ex)
        {
            Log.Fatal("[ProcedurePreload] 加载声音组失败（.pck 可能缺失依赖资源）: {0}", ex);
            groupsLoaded = false;
        }
        NodePool.Instance.Active(); // 启动节点池
        LayerMask.Instance.Active(); // 启动层级工具
        LoadingForm loadingForm;
        try
        {
            loadingForm = await GF.UI.OpenLoadingUIFormAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal("[ProcedurePreload] 打开加载界面失败: {0}", ex);
            return;
        }

        if (!groupsLoaded)
        {
            Log.Fatal("[ProcedurePreload] 必要资源组加载失败，停止进入游戏。");
            loadingForm?.SetLogState("资源加载失败", 0);
            return;
        }

        // 跨表约束在这里一次性校验，运行期不再各自兜底；失败即停止启动。
        ExperienceCurve curve;
        try
        {
            curve = ConfigValidator.ValidateAll(ConfigSystem.Instance.Tables);
        }
        catch (Exception ex)
        {
            Log.Fatal("[ProcedurePreload] {0}", ex.Message);
            loadingForm?.SetLogState("配置校验失败", 0);
            return;
        }

        procedureOwner.SetData(CurveDataKey, ExperienceCurveVariable.Create(curve));
        ChangeState<ProcedureLoadProfile>(procedureOwner);
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
            Log.Info("[ProcedurePreload] Editor res load enabled, set language to SystemLanguage: {0}.", GF.Localization.Language);
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

/// <summary>流程数据的经验曲线包装（GGF 流程数据只接受 Variable）。</summary>
public sealed class ExperienceCurveVariable : Variable<ExperienceCurve>
{
    /// <summary>从引用池创建包装。</summary>
    /// <param name="value">已校验的经验曲线。</param>
    /// <returns>池化包装，所有权交给流程状态机。</returns>
    public static ExperienceCurveVariable Create(ExperienceCurve value)
    {
        ExperienceCurveVariable variable = ReferencePool.Acquire<ExperienceCurveVariable>();
        variable.Value = value;
        return variable;
    }
}
