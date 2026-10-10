using System;
using System.Threading.Tasks;
using GameConfig;
using GameLogic.Profile;
using GodotGameFramework;

namespace GameLogic.Save
{
	/// <summary>读档结果：档案与它来自哪种原始状态。</summary>
	public readonly struct LoadedProfile
	{
		/// <summary>运行时档案。</summary>
		public readonly PlayerProfile Profile;

		/// <summary>存档来源（原样读取、迁移或新建）。</summary>
		public readonly SaveOrigin Origin;

		/// <summary>创建读档结果。</summary>
		/// <param name="profile">运行时档案。</param>
		/// <param name="origin">存档来源。</param>
		public LoadedProfile(PlayerProfile profile, SaveOrigin origin)
		{
			Profile = profile;
			Origin = origin;
		}
	}

	/// <summary>
	/// 项目侧唯一的存档入口：读档（含版本迁移与建档）和检查点写入。
	/// 只调用 <c>GF.Archive</c>，不直接使用文件或 EasySave；原子写、备份与串行化由框架 ArchiveSystem 负责。
	/// 检查点：关卡结算（通关/死亡）、返回地图/标题、交易/锻造完成、手动保存；其余时刻不写盘。
	/// </summary>
	public sealed class SaveService
	{
		private readonly ExperienceCurve m_Curve; // 迁移与重建档案用的共享经验曲线。
		private readonly int m_StartHeroId; // 建档规则：出战英雄。
		private readonly int m_StartGold; // 建档规则：初始金币。
		private Task<bool> m_Pending = Task.FromResult(true); // 最近一次提交的写入；流程重入前等待。

		/// <summary>创建存档入口。</summary>
		/// <param name="curve">共享经验曲线。</param>
		/// <param name="tables">配置总表（读取建档规则）。</param>
		/// <exception cref="ArgumentNullException">参数为空。</exception>
		public SaveService(ExperienceCurve curve, Tables tables)
		{
			ArgumentNullException.ThrowIfNull(tables);
			m_Curve = curve ?? throw new ArgumentNullException(nameof(curve));
			m_StartHeroId = tables.TbProfileConfig.StartHeroId;
			m_StartGold = tables.TbProfileConfig.StartGold;
		}

		/// <summary>最近一次提交的写入（完成值为是否成功）；流程切换前等待它，避免读到旧档。</summary>
		public Task<bool> Pending => m_Pending;

		/// <summary>读取当前存档槽；首次启动建新档，旧版本迁移后立即写回。</summary>
		/// <returns>运行时档案与来源。</returns>
		/// <exception cref="InvalidOperationException">存档存在但读取失败，或迁移后写回失败（不会新建空档覆盖）。</exception>
		/// <exception cref="NotSupportedException">存档版本无法识别。</exception>
		public async Task<LoadedProfile> LoadOrCreateAsync()
		{
			// 框架读档：目录与备份都不存在时自动建槽；存在但损坏时拒绝覆盖并返回失败。
			await m_Pending;
			if (!await GF.Archive.LoadAsync() || GF.Archive.CurrentData == null || GF.Archive.CurrentCatalogue == null ||
				GF.Archive.CurrentData.UnitId != GF.Archive.CurrentCatalogue.UnitId)
			{
				throw new InvalidOperationException("没有成功读取有效的当前存档，已拒绝覆盖。");
			}

			// 版本识别与迁移是纯函数；迁移或新建的档案立刻写回，下次按当前版本读取。
			(ProfileSaveData snapshot, SaveOrigin origin) = SaveMigrator.Migrate(GF.Archive.CurrentData, m_Curve,
				m_StartHeroId, m_StartGold);
			PlayerProfile profile = ProfileMapper.Restore(snapshot, m_Curve);
			if (origin != SaveOrigin.Current && !await CommitAsync(snapshot))
			{
				throw new InvalidOperationException("存档迁移后写回失败。");
			}

			return new LoadedProfile(profile, origin);
		}

		/// <summary>把档案当前状态写成一个检查点（调用时刻捕获快照，之后的修改不影响本次写入）。</summary>
		/// <param name="profile">要保存的档案。</param>
		/// <returns>是否写入成功；失败已记录日志，档案状态不受影响。</returns>
		/// <exception cref="ArgumentNullException">档案为空。</exception>
		public Task<bool> CheckpointAsync(PlayerProfile profile)
		{
			ArgumentNullException.ThrowIfNull(profile);
			return CommitAsync(ProfileMapper.Capture(profile));
		}

		/// <summary>把快照写入框架当前存档并提交；返回值即最近一次写入任务。</summary>
		/// <param name="snapshot">当前版本的档案快照。</param>
		/// <returns>是否写入成功。</returns>
		private Task<bool> CommitAsync(ProfileSaveData snapshot)
		{
			GameData data = GF.Archive.CurrentData;
			if (data == null)
			{
				Log.Error("[SaveService] 当前没有激活的存档，检查点未写入。");
				return Task.FromResult(false);
			}

			// 框架在调用时刻序列化 CurrentData，这里只需把快照放进去。
			data.SaveVersion = SaveMigrator.CurrentVersion;
			data.Profile = snapshot;
			data.Player = null;
			m_Pending = GF.Archive.OverWriteAsync();
			return m_Pending;
		}
	}
}
