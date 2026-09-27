using GameConfig.Sound;

namespace GameLogic.Config
{
	/// <summary>
	/// 音效配置的只读查询（`Config/` 层职责：Luban 表 → 领域值，只读不写）。
	///
	/// 只有一张音频表：<c>SoundConfig</c>（音效资产登记）
	///   Id | Key | NameCn | Desc | LegacyId | Group | Path
	/// Group 是**框架的声音组名**（SoundGroupRes 注册的 Music/SFX/UI），播放时直接喂给
	/// 框架的统一入口 `GF.Sound.PlaySound(path, group)`。
	///
	/// 谁在什么时机播哪个音，不在这张表里：**触发者在哪一行，SoundId 就配在哪一列**
	///   - 攻击起手/命中 → AttackConfig.SoundId / HitSoundId
	///   - 受击/死亡语音 → HeroConfig / MonsterConfig 的 HurtSoundId / DeathSoundId
	///   - 关卡 BGM、界面音 → 关卡表 / 界面代码，直接引用 SoundId
	/// </summary>
	public static class SoundConfigQuery
	{
		/// <summary>按 SoundId 取音效资产配置；None 或表里没有该行时返回 null（调用方负责报错/忽略）。</summary>
		public static SoundConfig Get(SoundId id)
		{
			if (id == SoundId.None)
			{
				return null;
			}

			return ConfigSystem.Instance.Tables.TbSoundConfig.GetOrDefault((int)id);
		}
	}
}
