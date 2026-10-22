using GameArchiveUtility = GameLogic.Utility.ArchiveUtility<GameCatalogue, GameData>;
using GameLogic.Config;
using GameLogic.Architecture;
using GameLogic.Save.Slots;
using GameLogic.Save;
using GameLogic.Save.Data;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using GameConfig;
using GameLogic.Profile;
using GameLogic.Skills;
using GodotGameFramework;
using GameLogic.Event.Save;

namespace GameLogic.Save
{
    /// <summary>
    /// 项目存档入口：读取六格摘要、读取或新建档案、保存检查点。
    /// </summary>
    /// <remarks>
    /// 全部写盘字段见 <see cref="GameData"/>；运行时档案与快照的转换见 <see cref="SaveDataUtility"/>。
    /// 文件读写只调用 ArchiveUtility，原子写、备份与串行队列由框架负责；数据流见 Docs/ProjectTour.md。
    /// 应用装配边界注册唯一实例；标题读取目录，读档流程切换当前槽，关卡提交当前档案。
    /// 本系统借用独立注册的 SaveModel，拥有读写任务；共享配置、经验曲线通过 Architecture 取用，ProfileModel 由流程注册于档案作用域。
    /// </remarks>
    public sealed class SaveSystem(IArchitecture architecture) : AbstractSystem(architecture)
    {
        private Task<bool> m_PendingWrite = Task.FromResult(true); // 最近一次提交的写入；流程重入前等待。

        private Task m_DirectoryRead = Task.CompletedTask; // 跨标题周期串行的目录读取任务。

        /// <summary>最近一次提交的写入（完成值为是否成功）；流程切换前等待它，避免读到旧档。</summary>
        public Task<bool> PendingWrite => m_PendingWrite;

        /// <summary>读取选中的存档格；新格按配置建档并立即写入，已有格只接受当前格式。</summary>
        /// <param name="slotNumber">从 1 开始的存档格号。</param>
        /// <param name="unitId">GGF 存档编号；新格为 0。</param>
        /// <param name="cancellationToken">流程周期；取消后停止发布迟到结果，已提交的框架读写仍会完成。</param>
        /// <returns>已校验的存档 DTO 及加载来源；运行时模型由流程创建并注册。</returns>
        /// <exception cref="InvalidOperationException">文件读取或写入失败，已拒绝覆盖。</exception>
        /// <exception cref="ArgumentOutOfRangeException">格号不在六格范围内。</exception>
        public async Task<(GameData Data, SaveOrigin Origin)> LoadOrCreateAsync(int slotNumber, long unitId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var archive = this.GetUtility<GameArchiveUtility>();
            if (slotNumber < 1 || slotNumber > SaveModel.SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slotNumber));
            var tables = this.GetModel<ConfigurationModel>().Tables;
            var mapper = this.GetUtility<SaveDataUtility>();
            var saveModel = this.GetModel<SaveModel>();
            await m_DirectoryRead;
            cancellationToken.ThrowIfCancellationRequested();
            await m_PendingWrite;
            cancellationToken.ThrowIfCancellationRequested();
            // 空格只创建新槽；已有编号明确读取该槽，不依赖“最近存档”。
            bool loaded;
            if (unitId == 0)
            {
                if (archive.Catalogues.Any(x => x.SlotNumber == slotNumber))
                    throw new InvalidOperationException("该存档格已有文件，拒绝覆盖。");
                loaded = await archive.SaveAsync();
            }
            else
                loaded = await archive.LoadAsync(unitId);

            cancellationToken.ThrowIfCancellationRequested();
            if (!loaded || archive.CurrentData == null || archive.CurrentCatalogue == null ||
                archive.CurrentData.UnitId != archive.CurrentCatalogue.UnitId)
                throw new InvalidOperationException("没有成功读取有效存档，已拒绝覆盖。");

            var catalogue = archive.CurrentCatalogue;
            GameData data = archive.CurrentData;
            bool created = unitId == 0 || IsUnclaimedSlot(catalogue, data);
            if (!created && (catalogue.SlotNumber != slotNumber || catalogue.SavedAtUtc <= 0))
                throw new InvalidOperationException("存档目录与所选格不一致或缺少检查点时间。");

            // 只有明确的新槽或框架新建的空槽才按建档规则创建，不转换历史文件。
            var source = created ? new GameData
            {
                ActiveHeroId = tables.TbProfileConfig.StartHeroId,
                Soul = tables.TbProfileConfig.StartSoul,
                Heroes = [new HeroSaveData { HeroId = tables.TbProfileConfig.StartHeroId }]
            } : data;
            mapper.Validate(source, tables.TbEquipmentConfig,
                tables.TbProfileConfig.EquipmentCapacity, tables.TbSkillConfig, tables.TbLevelConfig);
            saveModel.ActiveSlotUnitId = data.UnitId;
            if (created)
            {
                catalogue.SlotNumber = slotNumber;
                if (!await WriteSnapshotAsync(source))
                    throw new InvalidOperationException("新档案写入失败。");
                cancellationToken.ThrowIfCancellationRequested();
            }

            return (source, created ? SaveOrigin.Created : SaveOrigin.Loaded);
        }

        /// <summary>读取六格显示信息；损坏槽明确禁用，不作为空档重新创建。</summary>
        /// <param name="cancellationToken">所属标题周期的取消令牌。</param>
        /// <returns>按稳定格号排列的只读目录。</returns>
        /// <exception cref="InvalidOperationException">目录不可读或超出六格容量。</exception>
        private async Task<IReadOnlyList<SaveSlotInfo>> ReadSlotsAsync(CancellationToken cancellationToken)
        {
            var archive = this.GetUtility<GameArchiveUtility>();
            var tables = this.GetModel<ConfigurationModel>().Tables;
            var curve = this.GetUtility<ExperienceCurve>();
            var mapper = this.GetUtility<SaveDataUtility>();
            await m_PendingWrite;
            cancellationToken.ThrowIfCancellationRequested();
            // GGF 首次初始化可能建立一个空文件；它不创建角色，在界面仍显示为空格。
            if (archive.Catalogues.Count == 0 && !await archive.LoadAsync() && archive.Catalogues.Count == 0)
                throw new InvalidOperationException("存档目录不可读，已保留原文件。");
            cancellationToken.ThrowIfCancellationRequested();
            var catalogues = archive.Catalogues.ToArray();
            if (catalogues.Length > SaveModel.SlotCount)
                throw new InvalidOperationException("已有存档超过六格，当前界面不会覆盖任何存档。");
            var slots = Enumerable.Range(1, SaveModel.SlotCount)
                .Select(number => new SaveSlotInfo(number, 0, SaveSlotState.Empty)).ToArray();
            var used = new HashSet<int>();
            foreach (var catalogue in catalogues)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // GGF 首次启动的唯一空槽认领为第一格；已有档案必须已经具有稳定格号。
                int number = catalogue.SlotNumber == 0 ? 1 : catalogue.SlotNumber;
                if (number < 1 || number > SaveModel.SlotCount || !used.Add(number))
                    throw new InvalidOperationException("目录中存在重复或无效的存档格号。");
                try
                {
                    if (!await archive.LoadAsync(catalogue.UnitId))
                        throw new InvalidOperationException("存档文件及备份都不可读。");
                    cancellationToken.ThrowIfCancellationRequested();
                    if (IsUnclaimedSlot(catalogue, archive.CurrentData))
                        slots[number - 1] = new SaveSlotInfo(number, catalogue.UnitId, SaveSlotState.Empty);
                    else
                    {
                        if (catalogue.SlotNumber != number || catalogue.SavedAtUtc <= 0)
                            throw new InvalidOperationException("已有档案缺少稳定格号或检查点时间。");
                        var data = archive.CurrentData;
                        mapper.Validate(data,
                            tables.TbEquipmentConfig, tables.TbProfileConfig.EquipmentCapacity, tables.TbSkillConfig, tables.TbLevelConfig);
                        string name = tables.TbHeroConfig.Get(data.ActiveHeroId).NameCn;
                        int level = curve.Evaluate(data.Heroes.Single(x => x.HeroId == data.ActiveHeroId).TotalExperience).Level;
                        slots[number - 1] = new SaveSlotInfo(number, catalogue.UnitId, SaveSlotState.Occupied,
                            name, level, DateTimeOffset.FromUnixTimeSeconds(catalogue.SavedAtUtc));
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception error)
                {
                    Log.Warning("[SaveSystem] 存档格 {0} 不可读：{1}", number, error.Message);
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
            data.ActiveHeroId == 0 && data.Heroes is { Count: 0 } && data.Soul == 0 && data.HighestClearedLevelId == 0 &&
            data.EquipmentItems is { Count: 0 } &&
            catalogue.SlotNumber == 0 && catalogue.SavedAtUtc == 0 && string.IsNullOrEmpty(catalogue.Name);

        /// <summary>把档案当前状态写成一个检查点（调用时刻捕获快照，之后的修改不影响本次写入）。</summary>
        /// <param name="profile">要保存的档案。</param>
        /// <returns>是否写入成功；失败已记录日志，档案状态不受影响。</returns>
        /// <exception cref="ArgumentNullException">档案为空。</exception>
        public Task<bool> SaveCheckpointAsync(ProfileModel profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            if (!profile.Initialized || !ReferenceEquals(profile, this.GetModel<ProfileModel>()))
            {
                Log.Error("[SaveSystem] 旧档案已离开作用域，检查点未写入。");
                return Task.FromResult(false);
            }

            return WriteSnapshotAsync(this.GetUtility<SaveDataUtility>().Capture(profile, this.GetModel<SkillModel>()));
        }

        /// <summary>保存地图当前档案，并在请求周期仍有效时发布按钮结果。</summary>
        /// <param name="formSerialId">发起保存的窗口编号。</param>
        /// <param name="cancellationToken">窗口所属流程周期；取消只停止反馈，不取消已排队写盘。</param>
        /// <returns>实际保存结果；过期或重复请求返回 false。</returns>
        public async Task<bool> SaveManuallyAsync(int formSerialId, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;
            var saveModel = this.GetModel<SaveModel>();
            if (saveModel.ManualSave.Value is { Phase: SaveWritePhase.Writing, FormSerialId: var serial } &&
                serial == formSerialId)
                return false;
            var profile = this.GetModel<ProfileModel>();
            saveModel.ManualSave.Value = new(formSerialId, SaveWritePhase.Writing);
            bool succeeded;
            try
            { succeeded = await SaveCheckpointAsync(profile); }
            catch (Exception error)
            {
                Log.Error("[SaveSystem] 手动保存失败：{0}", error);
                succeeded = false;
            }
            // 不向已离开的窗口或新档案发布上一周期结果。
            if (!cancellationToken.IsCancellationRequested &&
                ReferenceEquals(profile, this.GetModel<ProfileModel>()) &&
                saveModel.ManualSave.Value.FormSerialId == formSerialId)
                saveModel.ManualSave.Value = new(formSerialId, succeeded ? SaveWritePhase.Succeeded : SaveWritePhase.Failed);
            return succeeded;
        }

        /// <summary>把快照写入框架当前存档并提交；返回值即最近一次写入任务。</summary>
        /// <param name="snapshotData">当前版本的档案快照。</param>
        /// <returns>是否写入成功。</returns>
        private Task<bool> WriteSnapshotAsync(GameData snapshotData)
        {
            var archive = this.GetUtility<GameArchiveUtility>();
            GameData data = archive.CurrentData;
            if (data == null || archive.CurrentCatalogue?.UnitId != this.GetModel<SaveModel>().ActiveSlotUnitId ||
                data.UnitId != this.GetModel<SaveModel>().ActiveSlotUnitId)
            {
                Log.Error("[SaveSystem] 当前存档不属于本档案作用域，检查点未写入。");
                return m_PendingWrite = Task.FromResult(false);
            }

            // 框架在调用时刻序列化；目录摘要与档案快照一起提交。
            archive.CurrentCatalogue.Name = this.GetModel<ConfigurationModel>().Tables.TbHeroConfig
                .Get(snapshotData.ActiveHeroId).NameCn;
            archive.CurrentCatalogue.SavedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            data.SaveVersion = GameData.CurrentVersion;
            data.ActiveHeroId = snapshotData.ActiveHeroId;
            data.Heroes = snapshotData.Heroes;
            data.Soul = snapshotData.Soul;
            data.HighestClearedLevelId = snapshotData.HighestClearedLevelId;
            data.EquipmentItems = snapshotData.EquipmentItems;
            m_PendingWrite = NotifyWriteAsync(archive.OverWriteAsync());
            return m_PendingWrite;
        }

        /// <summary>统一处理实际写盘结果，新建与所有检查点成功后都通知应用级提示。</summary>
        /// <param name="write">框架已经排队的写盘任务。</param>
        /// <returns>原始写盘结果；失败不发成功事件。</returns>
        private async Task<bool> NotifyWriteAsync(Task<bool> write)
        {
            var events = GF.Event;
            bool succeeded = await write;
            if (succeeded && Godot.GodotObject.IsInstanceValid(events))
                events.Fire(this, SaveSucceededEventArgs.Create());
            return succeeded;
        }

        /// <summary>等待上次目录任务结束，再读取本周期摘要；取消后不发布旧周期结果。</summary>
        /// <param name="cancellationToken">所属标题周期的取消令牌。</param>
        /// <returns>目录刷新任务。</returns>
        public async Task RefreshDirectoryAsync(CancellationToken cancellationToken)
        {
            await m_DirectoryRead;
            if (cancellationToken.IsCancellationRequested)
                return;
            m_DirectoryRead = ReadDirectoryAsync(cancellationToken);
            await m_DirectoryRead;
        }

        /// <summary>读取六格摘要并发布完整状态；取消后不再发布旧周期结果。</summary>
        /// <param name="cancellationToken">所属标题周期的取消令牌。</param>
        /// <returns>读取任务；文件错误转换为目录失败提示。</returns>
        private async Task ReadDirectoryAsync(CancellationToken cancellationToken)
        {
            var model = this.GetModel<SaveModel>();
            UpdateDirectory(model, SaveListState.Loading);
            try
            {
                var slots = await ReadSlotsAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                UpdateDirectory(model, SaveListState.Ready, slots);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 取消的是本次读取；共享目录可由下一个标题周期重新读取。
                UpdateDirectory(model, SaveListState.NotLoaded);
            }
            catch (Exception error)
            {
                Log.Error("[SaveSystem] 目录读取失败：{0}", error);
                UpdateDirectory(model, SaveListState.Failed, errorMessage: "存档目录读取失败，请关闭后重试。");
            }
        }

        /// <summary>由 SaveSystem 一次提交完整阶段和显示数据，再通知观察者。</summary>
        /// <param name="model">本次读取开始时借用的目录状态。</param>
        /// <param name="phase">目录读取阶段。</param>
        /// <param name="slots">完整格子摘要；省略时保留上次结果。</param>
        /// <param name="errorMessage">本次读取失败提示。</param>
        private static void UpdateDirectory(SaveModel model, SaveListState phase, IReadOnlyList<SaveSlotInfo> slots = null, string errorMessage = "")
        {
            // 复制集合边界，调用方随后修改数组不会偷偷改变已发布目录。
            var entries = slots == null ? model.Slots : Array.AsReadOnly(slots.ToArray());
            model.Directory.Value = new(phase, entries, errorMessage);
        }
    }
}
