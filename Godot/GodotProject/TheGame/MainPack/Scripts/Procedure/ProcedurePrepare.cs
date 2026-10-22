using System;
using GameConfig;
using GameFramework.Procedure;
using GameLogic.Architecture;
using GameLogic.Battle.Damage;
using GameLogic.Config;
using GameLogic.Drops;
using GameLogic.Input;
using GameLogic.Enemies.AI;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Utility;
using GodotGameFramework;
using GodotGameFramework.NodePool;
using GodotGameFramework.Sound;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>更新完成后的准备边界：建立资源组、工具、共享模型及系统，成功后才允许预加载界面。</summary>
public sealed class ProcedurePrepare : ProcedureBase
{
    /// <summary>按工具、模型、系统的依赖顺序注册应用对象。</summary>
    /// <param name="procedureOwner">拥有应用共享对象的主流程 FSM。</param>
    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        try
        {
            // 按配置创建资源组；任何必要组失败都停止启动。
            foreach (var group in GF.Entity.EntityGroupRes.EntityGroups)
            {
                if (!GF.Entity.HasEntityGroup(group.Name) && !GF.Entity.AddEntityGroup(group.Name, group.ReleaseInterval,
                        group.Capacity, group.ExpireTime, group.Priority))
                    throw new InvalidOperationException($"实体组 {group.Name} 创建失败。");
            }
            foreach (var group in GF.UI.UIGroupRes.Groups)
            {
                if (!GF.UI.HasUIGroup(group.Name) && !GF.UI.AddUIGroup(group.Name, group.Depth))
                    throw new InvalidOperationException($"UI 组 {group.Name} 创建失败。");
            }
            foreach (var group in GF.Sound.SoundGroupRes.SoundGroups)
            {
                if (!GF.Sound.HasSoundGroup(group.Name) && !GF.Sound.AddSoundGroup(group.Name, group.AgentCounts,
                        group.AvoidBeingReplacedBySamePriority))
                    throw new InvalidOperationException($"声音组 {group.Name} 创建失败。");
            }

            // 恢复用户音量并启用表现层工具。
            GF.Sound.SetVolume(SoundComponent.DefaultMusicGroup, GF.Setting.GetFloat(SoundComponent.DefaultMusicGroup, 1));
            GF.Sound.SetVolume(SoundComponent.DefaultSfxGroup, GF.Setting.GetFloat(SoundComponent.DefaultSfxGroup, 1));
            GF.Sound.SetVolume(SoundComponent.DefaultUiGroup, GF.Setting.GetFloat(SoundComponent.DefaultUiGroup, 1));
            NodePool.Instance.Active();
            LayerMask.Instance.Active();

            // 主流程 FSM 是唯一所有者；按工具、模型、系统的依赖顺序直接注册。
            var architecture = procedureOwner.InitializeArchitecture();
            procedureOwner.RegisterUtility(new ArchiveUtility<GameCatalogue, GameData>(GF.Archive));
            procedureOwner.RegisterUtility(new SpatialUtility());
            procedureOwner.RegisterUtility(new SaveDataUtility());
            procedureOwner.RegisterModel(new ConfigurationModel(ConfigSystem.Instance.Tables));
            procedureOwner.RegisterUtility(new ExperienceCurve(procedureOwner.GetModel<ConfigurationModel>().Tables.TbHeroLevelConfig.DataList));
            procedureOwner.RegisterModel(new SaveModel());
            procedureOwner.RegisterSystem(new ConfigurationSystem(architecture));
            procedureOwner.RegisterSystem(new CombatSystem(architecture));
            procedureOwner.RegisterSystem(new InputSystem(architecture));
            procedureOwner.RegisterSystem(new EnemyAiSystem(architecture));
            procedureOwner.RegisterSystem(new LootSystem(architecture));
            procedureOwner.RegisterSystem(new SaveSystem(architecture));
            ChangeState<ProcedurePreload>(procedureOwner);
        }
        catch (Exception error)
        {
            Log.Fatal("[ProcedurePrepare] 应用准备失败，停止启动：{0}", error);
        }
    }
}
