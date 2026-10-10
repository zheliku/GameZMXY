using System;
using System.Collections.Generic;
using GameLogic.Profile;

namespace GameLogic.Save
{
	/// <summary>存档迁移的结果：迁移后的当前版本快照，以及它来自哪种原始状态。</summary>
	public enum SaveOrigin
	{
		/// <summary>已是当前版本，原样读取。</summary>
		Current,

		/// <summary>由旧版本迁移而来（需要写回以完成迁移）。</summary>
		Migrated,

		/// <summary>框架首次启动建的空档，按建档规则新建（需要写回）。</summary>
		Created,
	}

	/// <summary>
	/// 存档版本识别与逐级迁移（纯函数，不触碰框架与文件）。规则：
	/// <list type="bullet">
	/// <item><c>SaveVersion == 0 且 Player != null</c>：重构前格式，迁移为版本 1；</item>
	/// <item><c>SaveVersion == 0 且 Player == null 且 Profile == null</c>：框架首次启动建的空档，按建档规则新建；</item>
	/// <item><c>SaveVersion == 1</c>：当前版本；</item>
	/// <item>更高版本：由更新的游戏写入，拒绝读取以免降级覆盖。</item>
	/// </list>
	/// </summary>
	public static class SaveMigrator
	{
		/// <summary>当前存档格式版本。</summary>
		public const int CurrentVersion = 1;

		/// <summary>把任意已知版本的存档转换为当前版本的档案快照。</summary>
		/// <param name="data">从磁盘读到的存档槽数据。</param>
		/// <param name="curve">共享经验曲线（旧格式等级合并用）。</param>
		/// <param name="startHeroId">建档规则：出战英雄（新档与 v0 迁移共用）。</param>
		/// <param name="startGold">建档规则：初始金币（只用于新档）。</param>
		/// <returns>当前版本快照与它的来源。</returns>
		/// <exception cref="ArgumentNullException">存档或经验曲线为空。</exception>
		/// <exception cref="NotSupportedException">存档版本高于当前版本或无法识别。</exception>
		public static (ProfileSaveData Profile, SaveOrigin Origin) Migrate(GameData data, ExperienceCurve curve,
			int startHeroId, int startGold)
		{
			ArgumentNullException.ThrowIfNull(data);
			ArgumentNullException.ThrowIfNull(curve);

			// 当前版本：快照必须完整，缺失说明文件被截断或手工改坏。
			if (data.SaveVersion == CurrentVersion)
			{
				if (data.Profile == null)
				{
					throw new NotSupportedException("存档版本 1 缺少档案数据。");
				}

				return (data.Profile, SaveOrigin.Current);
			}

			if (data.SaveVersion != 0)
			{
				throw new NotSupportedException($"存档版本 {data.SaveVersion} 高于当前版本 {CurrentVersion}，拒绝读取。");
			}

			// 版本 0 + 旧玩家数据：等级不倒退、累计经验不丢失，金币原样保留。
			if (data.Player != null)
			{
				PlayerSaveDataV0 old = data.Player;
				int total = curve.Restore(old.TotalExperience, Math.Max(1, old.Level));
				return (CreateProfile(startHeroId, total, old.Gold), SaveOrigin.Migrated);
			}

			// 版本 0 且没有任何数据：框架首次启动创建的空档。
			if (data.Profile == null)
			{
				return (CreateProfile(startHeroId, 0, startGold), SaveOrigin.Created);
			}

			throw new NotSupportedException("存档版本 0 却包含版本 1 数据，无法识别。");
		}

		/// <summary>按建档规则创建只有一名英雄的档案快照。</summary>
		/// <param name="heroId">出战英雄。</param>
		/// <param name="totalExperience">累计经验。</param>
		/// <param name="gold">金币。</param>
		/// <returns>新的档案快照。</returns>
		private static ProfileSaveData CreateProfile(int heroId, int totalExperience, int gold) => new()
		{
			ActiveHeroId = heroId,
			Heroes = new List<HeroSaveData> { new() { HeroId = heroId, TotalExperience = totalExperience } },
			Gold = Math.Max(0, gold),
		};
	}
}
