using System.Linq;
using System.Threading.Tasks;
using GameConfig.Sound;
using GameLogic.Architecture;
using GameLogic.Audio;
using GameLogic.Config;
using GameLogic.Level;
using Godot;
using GodotGameFramework;

/// <summary>
/// 验证GGF音乐与结算声音代理的真实播放、暂停和清理。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 用实际GGF代理验证章节曲、循环导入、暂停策略和请求停止，并核对洞窟场景绑定。
    /// </summary>
    /// <returns>三首官方章节音乐与两个场景完成检查的任务。</returns>
    private async Task CheckChapterMusicAsync()
    {
        // 本专项只验证声音，先停止流程已经提交的开场音乐请求。
        GF.Sound.StopAllLoadingSounds();
        GF.Sound.StopAllLoadedSounds();
        var tables = GF.Fsm.GetModel<ConfigurationModel>().Tables;
        foreach (var id in new[] { ESoundId.CaveTheme, ESoundId.SeaTheme, ESoundId.BullDemonValleyTheme })
        {
            var config = tables.TbSoundConfig.Get((int)id);
            Check(config.Group == "Music", $"章节音乐分组错误：{id}");
            int serial = GF.Sound.PlayConfigured(id);
            Check(serial > 0, $"GGF未提交章节音乐请求：{id}");
            await WaitUntilAsync(() => m_Driver.GetTree().Root.FindChildren("*", "AudioStreamPlayer", true, false)
                .OfType<AudioStreamPlayer>().Any(player => player.Bus == "Music" && player.Playing &&
                    player.Stream.ResourcePath == config.Path), $"GGF章节音乐没有起播：{id}");
            var players = m_Driver.GetTree().Root.FindChildren("*", "AudioStreamPlayer", true, false)
                .OfType<AudioStreamPlayer>().Where(player => player.Bus == "Music" && player.Playing).ToArray();
            Check(players.Length == 1 && players[0].Stream is AudioStreamMP3 { Loop: true },
                $"章节音乐重叠或导入未循环：{id}");
            var music = players[0];
            double start = music.GetPlaybackPosition();
            await WaitFramesAsync(12);
            Check(music.GetPlaybackPosition() > start, $"章节音乐播放位置未推进：{id}");

            // 战斗暂停时音乐仍由Always代理继续播放，暂停不能产生第二个请求。
            GF.Base.PauseGame();
            try
            {
                start = music.GetPlaybackPosition();
                await WaitFramesAsync(12);
                Check(music.Playing && !music.StreamPaused && music.GetPlaybackPosition() > start,
                    $"章节音乐被战斗暂停冻结：{id}");
            }
            finally
            {
                GF.Base.ResumeGame();
            }
            GF.Sound.StopSound(serial);
            await WaitUntilAsync(() => !music.Playing, $"章节音乐请求停止后仍在播放：{id}");
            GD.Print($"SMOKE-CHAPTER-MUSIC PASS: {id}, native loop, GGF Music agent, advancing playback, pause and stop.");
        }

        // 场景导出字段是章节选择来源，不在Procedure里按关卡号硬编码。
        foreach (string path in new[]
        {
            "res://TheGame/Scenes/Levels/zm1_04_bat_cave/level_4.tscn",
            "res://TheGame/Scenes/Levels/zm1_05_qixing_cave/level_5.tscn"
        })
        {
            var view = GD.Load<PackedScene>(path).Instantiate<LevelViewController>();
            Check(view.MusicId == ESoundId.CaveTheme, $"洞窟关卡仍沿用花果山音乐：{path}");
            view.Free();
        }
        GD.Print("SMOKE-CHAPTER-SCENES PASS: both cave levels select the official bg2 identity.");
    }
}
