using System;
using System.Collections.Generic;
using GameLogic.Profile;

namespace GameLogic.Save.Data
{
    /// <summary>运行时档案与 GameData 之间的唯一转换点，也负责放弃关卡时恢复进关快照。</summary>
    public static class ProfileSaveHelper
    {
        /// <summary>把档案当前状态复制为可序列化快照（与运行时对象不共享引用）。</summary>
        /// <param name="profile">运行时档案。</param>
        /// <returns>新的档案快照。</returns>
        /// <exception cref="ArgumentNullException">档案为空。</exception>
        public static GameData CaptureSaveData(PlayerProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            GameData data = new() { ActiveHeroId = profile.ActiveHeroId, Gold = profile.Wallet.Gold };

            // 按 HeroId 排序写出，同一状态得到同一份 JSON，便于比对。
            List<int> heroIds = new(profile.Heroes.Keys);
            heroIds.Sort();
            foreach (int heroId in heroIds)
            {
                HeroProgression hero = profile.Heroes[heroId];
                data.Heroes.Add(new HeroSaveData { HeroId = heroId, TotalExperience = hero.TotalExperience });
            }

            return data;
        }

        /// <summary>读取当前存档格式；不转换历史版本或补全缺失档案。</summary>
        /// <param name="data">GGF 读取的存档信封。</param>
        /// <param name="curve">已校验的共享经验曲线。</param>
        /// <returns>当前格式重建的运行时档案。</returns>
        /// <exception cref="ArgumentNullException">存档为空。</exception>
        /// <exception cref="NotSupportedException">版本不等于当前版本。</exception>
        /// <exception cref="ArgumentException">档案内容不满足英雄集合约束。</exception>
        public static PlayerProfile RestoreProfile(GameData data, ExperienceCurve curve)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (data.SaveVersion != GameData.CurrentVersion)
                throw new NotSupportedException($"只支持存档格式 v{GameData.CurrentVersion}，当前文件版本为 {data.SaveVersion}。");
            ArgumentNullException.ThrowIfNull(curve);
            if (data.Heroes == null || data.Heroes.Count == 0)
            {
                throw new ArgumentException("档案快照没有英雄。", nameof(data));
            }

            // 逐个重建英雄记录；等级等派生值由累计经验重新计算。
            List<HeroProgression> heroes = new(data.Heroes.Count);
            foreach (HeroSaveData hero in data.Heroes)
            {
                if (hero == null)
                {
                    throw new ArgumentException("档案快照包含空英雄记录。", nameof(data));
                }

                heroes.Add(new HeroProgression(hero.HeroId, curve, hero.TotalExperience));
            }

            return new PlayerProfile(data.ActiveHeroId, heroes, new Wallet(data.Gold));
        }

        /// <summary>按快照回滚已存在的档案（关卡事务回滚），保留对象引用，订阅者会收到变更通知。</summary>
        /// <param name="profile">要回滚的运行时档案。</param>
        /// <param name="snapshot">进关时捕获的快照。</param>
        /// <exception cref="ArgumentNullException">参数为空。</exception>
        /// <exception cref="InvalidOperationException">快照与档案的英雄集合不一致。</exception>
        public static void RestoreCheckpoint(PlayerProfile profile, GameData snapshot)
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
                if (!profile.Heroes.TryGetValue(hero.HeroId, out HeroProgression record))
                {
                    throw new InvalidOperationException($"回滚快照中的英雄 {hero.HeroId} 不在档案中。");
                }

                record.Restore(hero.TotalExperience);
            }

            profile.Wallet.Restore(snapshot.Gold);
        }
    }
}
