using GameLogic.Save.Slots;
using GameLogic.Save.Data;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using GameConfig;
using GameLogic.Profile;
using GodotGameFramework;

namespace GameLogic.Save
{
    /// <summary>
    /// 项目存档入口：读取六格摘要、读取或新建档案、保存检查点。
    /// </summary>
    /// <remarks>
    /// 全部写盘字段见 <see cref="GameData"/>；运行时档案与快照的转换见 <see cref="ProfileMapper"/>。
    /// 文件读写只调用 GF.Archive，原子写、备份与串行队列由框架负责；阅读顺序见同目录 README.md。
    /// </remarks>
    public sealed class SaveService
    {
        private readonly Tables m_Tables; // 存档摘要中的角色名称来源。
        private long m_ActiveSlotUnitId; // 当前档案所属文件编号；必须与 GGF 当前读写槽一致。
        private readonly ExperienceCurve m_Curve; // 重建档案用的共享经验曲线。
        private readonly int m_StartHeroId; // 建档规则：出战英雄。
        private readonly int m_StartGold; // 建档规则：初始金币。
        private Task<bool> m_PendingWrite = Task.FromResult(true); // 最近一次提交的写入；流程重入前等待。

        /// <summary>创建存档入口。</summary>
        /// <param name="curve">共享经验曲线。</param>
        /// <param name="tables">配置总表（读取建档规则）。</param>
        /// <exception cref="ArgumentNullException">参数为空。</exception>
        public SaveService(ExperienceCurve curve, Tables tables)
        {
            ArgumentNullException.ThrowIfNull(tables);
            m_Tables = tables;
            m_Curve = curve ?? throw new ArgumentNullException(nameof(curve));
            m_StartHeroId = tables.TbProfileConfig.StartHeroId;
            m_StartGold = tables.TbProfileConfig.StartGold;
        }

        /// <summary>最近一次提交的写入（完成值为是否成功）；流程切换前等待它，避免读到旧档。</summary>
        public Task<bool> PendingWrite => m_PendingWrite;

        /// <summary>读取选中的存档格；新格按配置建档并立即写入，已有格只接受当前格式。</summary>
        /// <param name="slotNumber">从 1 开始的存档格号。</param>
        /// <param name="unitId">GGF 存档编号；新格为 0。</param>
        /// <returns>运行时档案及其原始来源。</returns>
        /// <exception cref="InvalidOperationException">文件读取或写入失败，已拒绝覆盖。</exception>
        /// <exception cref="ArgumentOutOfRangeException">格号不在六格范围内。</exception>
        public async Task<ProfileLoadResult> LoadOrCreateAsync(int slotNumber, long unitId)
        {
            if (slotNumber < 1 || slotNumber > SaveSlotDirectory.SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slotNumber));
            await m_PendingWrite;
            // 空格只创建新槽；已有编号明确读取该槽，不依赖“最近存档”。
            bool loaded;
            if (unitId == 0)
            {
                if (GF.Archive.Catalogues.Any(x => x.SlotNumber == slotNumber))
                    throw new InvalidOperationException("该存档格已有文件，拒绝覆盖。");
                loaded = await GF.Archive.SaveAsync();
            }
            else loaded = await GF.Archive.LoadAsync(unitId);
            if (!loaded || GF.Archive.CurrentData == null || GF.Archive.CurrentCatalogue == null ||
                GF.Archive.CurrentData.UnitId != GF.Archive.CurrentCatalogue.UnitId)
                throw new InvalidOperationException("没有成功读取有效存档，已拒绝覆盖。");

            var catalogue = GF.Archive.CurrentCatalogue;
            GameData data = GF.Archive.CurrentData;
            bool created = unitId == 0 || IsUnclaimedSlot(catalogue, data);
            if (!created && (catalogue.SlotNumber != slotNumber || catalogue.SavedAtUtc <= 0))
                throw new InvalidOperationException("存档目录与所选格不一致或缺少检查点时间。");

            // 只有明确的新槽或框架新建的空槽才按建档规则创建，不转换历史文件。
            PlayerProfile profile = created
                ? new PlayerProfile(m_StartHeroId,
                    [new HeroProgression(m_StartHeroId, m_Curve, 0)], new Wallet(m_StartGold))
                : ProfileMapper.RestoreProfile(data, m_Curve);
            m_ActiveSlotUnitId = data.UnitId;
            ProfileLoadOrigin origin = created ? ProfileLoadOrigin.Created : ProfileLoadOrigin.Loaded;
            if (created)
            {
                catalogue.SlotNumber = slotNumber;
                if (!await WriteSnapshotAsync(ProfileMapper.CaptureSaveData(profile)))
                    throw new InvalidOperationException("新档案写入失败。");
            }
            return new ProfileLoadResult(profile, origin);
        }

        /// <summary>读取六格显示信息；损坏槽明确禁用，不作为空档重新创建。</summary>
        /// <param name="cancellationToken">所属标题周期的取消令牌。</param>
        /// <returns>按稳定格号排列的只读目录。</returns>
        /// <exception cref="InvalidOperationException">目录不可读或超出六格容量。</exception>
        public async Task<IReadOnlyList<SaveSlotInfo>> ReadSlotsAsync(CancellationToken cancellationToken)
        {
            await m_PendingWrite;
            cancellationToken.ThrowIfCancellationRequested();
            // GGF 首次初始化可能建立一个空文件；它不创建角色，在界面仍显示为空格。
            if (GF.Archive.Catalogues.Count == 0 && !await GF.Archive.LoadAsync() && GF.Archive.Catalogues.Count == 0)
                throw new InvalidOperationException("存档目录不可读，已保留原文件。");
            cancellationToken.ThrowIfCancellationRequested();
            var catalogues = GF.Archive.Catalogues.ToArray();
            if (catalogues.Length > SaveSlotDirectory.SlotCount)
                throw new InvalidOperationException("已有存档超过六格，当前界面不会覆盖任何存档。");
            var slots = Enumerable.Range(1, SaveSlotDirectory.SlotCount)
                .Select(number => new SaveSlotInfo(number, 0, SaveSlotState.Empty)).ToArray();
            var used = new HashSet<int>();
            foreach (var catalogue in catalogues)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // GGF 首次启动的唯一空槽认领为第一格；已有档案必须已经具有稳定格号。
                int number = catalogue.SlotNumber == 0 ? 1 : catalogue.SlotNumber;
                if (number < 1 || number > SaveSlotDirectory.SlotCount || !used.Add(number))
                    throw new InvalidOperationException("目录中存在重复或无效的存档格号。");
                try
                {
                    if (!await GF.Archive.LoadAsync(catalogue.UnitId))
                        throw new InvalidOperationException("存档文件及备份都不可读。");
                    cancellationToken.ThrowIfCancellationRequested();
                    if (IsUnclaimedSlot(catalogue, GF.Archive.CurrentData))
                        slots[number - 1] = new SaveSlotInfo(number, catalogue.UnitId, SaveSlotState.Empty);
                    else
                    {
                        if (catalogue.SlotNumber != number || catalogue.SavedAtUtc <= 0)
                            throw new InvalidOperationException("已有档案缺少稳定格号或检查点时间。");
                        var profile = ProfileMapper.RestoreProfile(GF.Archive.CurrentData, m_Curve);
                        string name = m_Tables.TbHeroConfig.Get(profile.ActiveHeroId).NameCn;
                        slots[number - 1] = new SaveSlotInfo(number, catalogue.UnitId, SaveSlotState.Occupied,
                            name, profile.ActiveHero.Level, DateTimeOffset.FromUnixTimeSeconds(catalogue.SavedAtUtc));
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception error)
                {
                    Log.Warning("[SaveService] 存档格 {0} 不可读：{1}", number, error.Message);
                    slots[number - 1] = new SaveSlotInfo(number, catalogue.UnitId, SaveSlotState.Unreadable);
                }
            }
            return Array.AsReadOnly(slots);
        }

        /// <summary>识别框架刚创建且尚未建立角色的当前格式空槽。</summary>
        /// <param name="catalogue">框架目录条目。</param>
        /// <param name="data">同一编号的存档信封。</param>
        /// <returns>仅当前版本、无档案且无业务目录元数据时为 true。</returns>
        private static bool IsUnclaimedSlot(GameCatalogue catalogue, GameData data) =>
            data != null && data.UnitId == catalogue.UnitId && data.SaveVersion == GameData.CurrentVersion &&
            data.ActiveHeroId == 0 && data.Heroes is { Count: 0 } && data.Gold == 0 &&
            catalogue.SlotNumber == 0 && catalogue.SavedAtUtc == 0 && string.IsNullOrEmpty(catalogue.Name);

        /// <summary>把档案当前状态写成一个检查点（调用时刻捕获快照，之后的修改不影响本次写入）。</summary>
        /// <param name="profile">要保存的档案。</param>
        /// <returns>是否写入成功；失败已记录日志，档案状态不受影响。</returns>
        /// <exception cref="ArgumentNullException">档案为空。</exception>
        public Task<bool> SaveCheckpointAsync(PlayerProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            return WriteSnapshotAsync(ProfileMapper.CaptureSaveData(profile));
        }

        /// <summary>把快照写入框架当前存档并提交；返回值即最近一次写入任务。</summary>
        /// <param name="snapshot">当前版本的档案快照。</param>
        /// <returns>是否写入成功。</returns>
        private Task<bool> WriteSnapshotAsync(GameData snapshot)
        {
            GameData data = GF.Archive.CurrentData;
            if (data == null || GF.Archive.CurrentCatalogue?.UnitId != m_ActiveSlotUnitId || data.UnitId != m_ActiveSlotUnitId)
            {
                Log.Error("[SaveService] 当前存档不属于本档案作用域，检查点未写入。");
                return m_PendingWrite = Task.FromResult(false);
            }

            // 框架在调用时刻序列化；目录摘要与档案快照一起提交。
            GF.Archive.CurrentCatalogue.Name = m_Tables.TbHeroConfig.Get(snapshot.ActiveHeroId).NameCn;
            GF.Archive.CurrentCatalogue.SavedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            data.SaveVersion = GameData.CurrentVersion;
            data.ActiveHeroId = snapshot.ActiveHeroId;
            data.Heroes = snapshot.Heroes;
            data.Gold = snapshot.Gold;
            m_PendingWrite = GF.Archive.OverWriteAsync();
            return m_PendingWrite;
        }
    }
}
