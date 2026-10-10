using System;
using GodotGameFramework;
using GodotGameFramework.Sound;

namespace GameLogic.Startup;

/// <summary>按主包资源配置注册实体、UI、声音组，供正式启动和独立场地共用。</summary>
public static class GameResourceGroups
{
    /// <summary>尝试注册全部资源组并恢复声音设置；已存在的组保持不变，重复调用安全。</summary>
    /// <returns>各类资源组是否全部注册成功；失败详情写入日志。</returns>
    public static bool Register() =>
        TryRegister("实体", RegisterEntityGroups) & TryRegister("UI", RegisterUiGroups) & TryRegister("声音", RegisterSoundGroups);

    /// <summary>独立执行一类注册，失败不阻止其他组建立，以便加载界面显示错误。</summary>
    /// <param name="category">日志中的资源类别。</param>
    /// <param name="register">本类资源的注册步骤。</param>
    /// <returns>本类资源是否成功注册。</returns>
    private static bool TryRegister(string category, Action register)
    {
        try
        {
            register();
            return true;
        }
        catch (Exception error)
        {
            Log.Fatal("[GameResourceGroups] {0}组注册失败：{1}", category, error);
            return false;
        }
    }

    /// <summary>创建资源配置中尚未存在的实体组。</summary>
    /// <exception cref="InvalidOperationException">框架拒绝创建实体组。</exception>
    private static void RegisterEntityGroups()
    {
        foreach (var group in GF.Entity.EntityGroupRes.EntityGroups)
        {
            if (!GF.Entity.HasEntityGroup(group.Name) && !GF.Entity.AddEntityGroup(group.Name, group.ReleaseInterval,
                    group.Capacity, group.ExpireTime, group.Priority))
            {
                throw new InvalidOperationException($"实体组 {group.Name} 创建失败。");
            }
        }
    }

    /// <summary>创建资源配置中尚未存在的 UI 组。</summary>
    /// <exception cref="InvalidOperationException">框架拒绝创建 UI 组。</exception>
    private static void RegisterUiGroups()
    {
        foreach (var group in GF.UI.UIGroupRes.Groups)
        {
            if (!GF.UI.HasUIGroup(group.Name) && !GF.UI.AddUIGroup(group.Name, group.Depth))
            {
                throw new InvalidOperationException($"UI 组 {group.Name} 创建失败。");
            }
        }
    }

    /// <summary>创建资源配置中尚未存在的声音组，并从设置恢复音量。</summary>
    /// <exception cref="InvalidOperationException">框架拒绝创建声音组。</exception>
    private static void RegisterSoundGroups()
    {
        foreach (var group in GF.Sound.SoundGroupRes.SoundGroups)
        {
            if (!GF.Sound.HasSoundGroup(group.Name) && !GF.Sound.AddSoundGroup(group.Name, group.AgentCounts,
                    group.AvoidBeingReplacedBySamePriority))
            {
                throw new InvalidOperationException($"声音组 {group.Name} 创建失败。");
            }
        }

        // 两种启动入口都采用同一套用户声音设置。
        GF.Sound.SetVolume(SoundComponent.DefaultMusicGroup, GF.Setting.GetFloat(SoundComponent.DefaultMusicGroup, 1));
        GF.Sound.SetVolume(SoundComponent.DefaultSfxGroup, GF.Setting.GetFloat(SoundComponent.DefaultSfxGroup, 1));
        GF.Sound.SetVolume(SoundComponent.DefaultUiGroup, GF.Setting.GetFloat(SoundComponent.DefaultUiGroup, 1));
    }
}
