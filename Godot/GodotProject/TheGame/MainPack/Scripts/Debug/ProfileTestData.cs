using System;
using System.Collections.Generic;
using GameLogic.Architecture;
using GameLogic.Equipment;
using GameLogic.Profile;

/// <summary>
/// 测试夹具的档案设置；只使用正式事务已有的内部提交能力，不向 Model 添加测试接口。
/// </summary>
internal static class ProfileTestData
{
    /// <summary>
    /// 为开发测试设置档案数据。
    /// </summary>
    /// <param name="profile">本测试拥有的档案。</param>
    extension(ProfileModel profile)
    {
        /// <summary>
        /// 设置指定英雄经验，真实曲线求值并只通知变化。
        /// </summary>
        /// <param name="heroId">已拥有英雄。</param>
        /// <param name="totalExperience">累计经验。</param>
        internal void RestoreExperience(int heroId, int totalExperience)
        {
            ExperienceCurveUtility curve = profile.GetUtility<ExperienceCurveUtility>();
            profile.GetProgress(heroId).Value = curve.GetProgress(totalExperience);
        }

        /// <summary>
        /// 设置夹具余额，保持背包和绑定身份。
        /// </summary>
        /// <param name="heroId">本次设置数据的英雄编号。</param>
        /// <param name="soul">余额，负数按零处理。</param>
        internal void RestoreSoul(int heroId, int soul) => profile.GetSoul(heroId).Value = Math.Max(0, soul);

        /// <summary>
        /// 直接设置本测试拥有的背包；非法存档通过正式回滚入口验证。
        /// </summary>
        /// <param name="heroId">本次设置数据的英雄编号。</param>
        /// <param name="datas">装备集合。</param>
        internal void RestoreItems(int heroId, IEnumerable<EquipmentData> datas) => profile.GetBag(heroId).ReplaceAll(datas);
    }
}
