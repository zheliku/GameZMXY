using System;
using GameConfig.Sound;
using GameFramework.Sound;
using GodotGameFramework;
using GodotGameFramework.Sound;

namespace GameLogic.Audio;

/// <summary>按正式声音配置调用 GGF；资源加载、代理池、分组与音量均交由框架管理。</summary>
public static class SoundHelper
{
    extension(SoundComponent sound)
    {
        /// <summary>播放配置中的声音并返回本次请求编号；None 不播放。</summary>
        /// <param name="id">声音配置枚举。</param>
        /// <param name="fadeInSeconds">淡入秒数；一次性音效通常为零。</param>
        /// <returns>GGF 声音编号，None 返回零；所属流程可按编号取消加载或停止播放。</returns>
        /// <exception cref="InvalidOperationException">声音配置缺失。</exception>
        public int PlayConfigured(SoundId id, float fadeInSeconds = 0f)
        {
            if (id == SoundId.None)
                return 0;
            var config = ConfigSystem.Instance.Tables.TbSoundConfig.GetOrDefault((int)id)
                         ?? throw new InvalidOperationException($"声音配置缺失：{id}");
            // 每次请求独立取得池化参数，避免异步加载期间复用同一参数对象。
            var parameters = PlaySoundParams.Create();
            parameters.FadeInSeconds = fadeInSeconds;
            return sound.PlaySound(config.Path, config.Group, parameters);
        }
    }
}
