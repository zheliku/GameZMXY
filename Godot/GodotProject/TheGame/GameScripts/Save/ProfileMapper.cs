using System;
using System.Collections.Generic;
using GameLogic.Profile;

namespace GameLogic.Save
{
	/// <summary>运行时档案与存档快照之间的唯一映射点；新增持久状态时同时改这里与 <see cref="ProfileSaveData"/>。</summary>
	public static class ProfileMapper
	{
		/// <summary>把档案当前状态复制为可序列化快照（与运行时对象不共享引用）。</summary>
		/// <param name="profile">运行时档案。</param>
		/// <returns>新的档案快照。</returns>
		/// <exception cref="ArgumentNullException">档案为空。</exception>
		public static ProfileSaveData Capture(PlayerProfile profile)
		{
			ArgumentNullException.ThrowIfNull(profile);
			ProfileSaveData data = new() { ActiveHeroId = profile.ActiveHeroId, Gold = profile.Wallet.Gold };

			// 按 HeroId 排序写出，同一状态得到同一份 JSON，便于比对。
			List<int> heroIds = new(profile.Heroes.Keys);
			heroIds.Sort();
			foreach (int heroId in heroIds)
			{
				HeroRecord hero = profile.Heroes[heroId];
				data.Heroes.Add(new HeroSaveData { HeroId = heroId, TotalExperience = hero.Progression.TotalExperience });
			}

			return data;
		}

		/// <summary>由快照重建运行时档案。</summary>
		/// <param name="data">已迁移到当前版本的档案快照。</param>
		/// <param name="curve">共享经验曲线。</param>
		/// <returns>新的运行时档案。</returns>
		/// <exception cref="ArgumentNullException">参数为空。</exception>
		/// <exception cref="ArgumentException">快照缺少英雄、英雄重复或出战英雄不存在。</exception>
		public static PlayerProfile Restore(ProfileSaveData data, ExperienceCurve curve)
		{
			ArgumentNullException.ThrowIfNull(data);
			ArgumentNullException.ThrowIfNull(curve);
			if (data.Heroes == null || data.Heroes.Count == 0)
			{
				throw new ArgumentException("档案快照没有英雄。", nameof(data));
			}

			// 逐个重建英雄记录；等级等派生值由累计经验重新计算。
			List<HeroRecord> heroes = new(data.Heroes.Count);
			foreach (HeroSaveData hero in data.Heroes)
			{
				if (hero == null)
				{
					throw new ArgumentException("档案快照包含空英雄记录。", nameof(data));
				}

				heroes.Add(new HeroRecord(hero.HeroId, new HeroProgression(curve, hero.TotalExperience)));
			}

			return new PlayerProfile(data.ActiveHeroId, heroes, new Wallet(data.Gold));
		}

		/// <summary>按快照回滚已存在的档案（关卡事务回滚），保留对象引用，订阅者会收到变更通知。</summary>
		/// <param name="profile">要回滚的运行时档案。</param>
		/// <param name="snapshot">进关时捕获的快照。</param>
		/// <exception cref="ArgumentNullException">参数为空。</exception>
		/// <exception cref="InvalidOperationException">快照与档案的英雄集合不一致。</exception>
		public static void RollBack(PlayerProfile profile, ProfileSaveData snapshot)
		{
			ArgumentNullException.ThrowIfNull(profile);
			ArgumentNullException.ThrowIfNull(snapshot);

			// 关内不会增减英雄，集合不一致说明快照不属于该档案。
			if (snapshot.Heroes.Count != profile.Heroes.Count)
			{
				throw new InvalidOperationException("回滚快照与档案的英雄数量不一致。");
			}

			foreach (HeroSaveData hero in snapshot.Heroes)
			{
				if (!profile.Heroes.TryGetValue(hero.HeroId, out HeroRecord record))
				{
					throw new InvalidOperationException($"回滚快照中的英雄 {hero.HeroId} 不在档案中。");
				}

				record.Progression.Restore(hero.TotalExperience);
			}

			profile.Wallet.Restore(snapshot.Gold);
		}
	}
}
