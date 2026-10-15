using System;
using GameConfig.Sound;
using GameLogic.Architecture;
using GameLogic.Config;
using GameFramework.Sound;
using GodotGameFramework;
using GodotGameFramework.Sound;

namespace GameLogic.Audio;

/// <summary>将游戏声音配置直接适配到 GGF 声音组件。</summary>
public static class SoundExtensions
{
    /// <summary>扩展 GGF 声音组件，不另外创建声音系统或工具实例。</summary>
    /// <param name="sound">框架声音组件。</param>
    extension(SoundComponent sound)
    {
        /// <summary>播放游戏定义的声音，None 不提交请求。</summary>
        /// <param name="id">配置枚举。</param>
        /// <param name="fadeInSeconds">淡入秒数。</param>
        /// <returns>GGF 请求编号；None 为零。</returns>
        /// <exception cref="InvalidOperationException">声音配置缺失。</exception>
        public int PlayConfigured(SoundId id, float fadeInSeconds = 0f)
        {
            if (id == SoundId.None)
                return 0;
            var config = GF.Fsm.GetModel<ConfigurationModel>().Tables.TbSoundConfig.GetOrDefault((int)id)
                ?? throw new InvalidOperationException($"声音配置缺失：{id}");
            var parameters = PlaySoundParams.Create();
            parameters.FadeInSeconds = fadeInSeconds;
            return sound.PlaySound(config.Path, config.Group, parameters);
        }
    }
}
