using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameConfig.Level;
using GameFramework;
using GameFramework.Entity;
using GameFramework.Event;
using GameLogic.Event;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;

namespace GameLogic.Level
{
	/// <summary>
	/// 关卡场景的编排入口。
	///
	/// 场景负责地形、相机和 SpawnPoint 的空间事实；Luban 表负责关卡、阶段和生成配方。
	/// 实体仍由 GF.Entity 管理，因此 RuntimeActors 只是关卡预留插槽，不用于统计实体数量。
	/// </summary>
	public partial class LevelController : Node2D
	{
		[Export] private int m_LevelId = 1; // 关卡多行配置中的 LevelId 分组键。
		[Export] private Node m_SpawnPointsRoot; // 本场景生成点索引的根节点。
		[Export] private Node m_StageTriggersRoot; // 本场景阶段触发器索引的根节点。
		[Export] private Node m_RuntimeActors; // 关卡预留的运行时对象插槽；GF.Entity 实体不挂在此节点下。

		private readonly Dictionary<string, LevelSpawnPoint>
			m_SpawnPoints = new(StringComparer.Ordinal); // 当前场景内按稳定 ID 建立的空间锚点索引。

		private readonly Dictionary<string, LevelStageTrigger>
			m_StageTriggers = new(StringComparer.Ordinal); // 当前场景内按稳定 ID 建立的阶段触发器索引。

		private readonly Dictionary<int, IEntity> m_ActiveMonsters = new(); // 当前关卡创建且尚未死亡的实体引用。
		private readonly Dictionary<int, int> m_EntityStageOrders = new(); // runtime EntityId 到逻辑阶段顺序的归属映射。
		private readonly Dictionary<int, int> m_StageActiveCounts = new(); // 每个逻辑阶段的活跃实体计数。
		private readonly Dictionary<int, LevelConfig> m_Stages = new(); // 按 StageOrder 建立的阶段规则索引。
		private readonly Dictionary<int, LevelStageConfig[]> m_StageRecipes = new(); // 每个阶段按配方 Sequence 排序的怪物配方。
		private readonly HashSet<int> m_StartedStages = new(); // 防止阶段重复启动。
		private readonly HashSet<int> m_FinishedSpawningStages = new(); // 区分“刷怪未结束”和“全部活怪已清”。
		private readonly HashSet<int> m_PendingStageTriggers = new(); // 玩家提前经过触发器时暂存待激活阶段。

		private readonly Dictionary<LevelStageTrigger, Area2D.BodyEnteredEventHandler>
			m_TriggerHandlers = new(); // 记录回调以便卸载时逐一断开。

		private LevelConfig m_LevelConfig; // 当前关卡阶段行首行保存的关卡元数据。
		private EntityComponent m_EntityComponent; // 由流程显式注入的实体组件，供后续阶段生成复用。
		private bool m_Initialized; // 场景和配置外键已校验。
		private bool m_Subscribed; // 是否已订阅怪物死亡事件。
		private CancellationToken m_SessionToken; // 本次关卡会话的取消令牌。

		/// <summary>配置中的关卡 ID。</summary>
		public int LevelId => m_LevelId;

		/// <summary>玩家出生点世界坐标。</summary>
		public Vector2 PlayerSpawnPosition
		{
			get
			{
				EnsureInitialized();
				return SpawnPointPosition(m_LevelConfig.PlayerSpawnPointId);
			}
		}

		/// <summary>场景空间校验和表引用校验。</summary>
		public void Initialize()
		{
			if (m_Initialized)
			{
				return;
			}

			// 先校验场景身份，防止误将其他关卡场景和配置拼接运行。
			// LevelConfig 是 list 表：用场景 LevelId 找到该关卡的阶段行，不依赖 DataList 行顺序。
			LevelConfig[] allRows = ConfigSystem.Instance.Tables.TbLevelConfig.DataList.ToArray();
			LevelConfig[] levelRows = allRows.Where(x => x.LevelId == m_LevelId).OrderBy(x => x.StageOrder).ToArray();
			if (levelRows.Length == 0)
			{
				throw new InvalidOperationException($"关卡阶段配置不存在：LevelId={m_LevelId}");
			}

			// list 行仍用独立 Id 保持唯一；同一关卡必须且只能有一个首阶段。
			if (allRows.Select(x => x.RowId).Distinct().Count() != allRows.Length ||
			    levelRows.Count(x => x.StageOrder == 1) != 1)
			{
				throw new InvalidOperationException($"LevelConfig 行 ID 重复或首阶段行不唯一：LevelId={m_LevelId}");
			}

			// 只有 StageOrder=1 行持有关卡元数据；该行也是关卡配置的稳定入口。
			m_LevelConfig = levelRows.FirstOrDefault(x => x.StageOrder == 1);
			if (m_LevelConfig == null || m_LevelConfig.StageCount <= 0)
			{
				throw new InvalidOperationException($"关卡首阶段元数据缺失或阶段数非法：LevelId={m_LevelId}");
			}

			if (!string.Equals(m_LevelConfig.ScenePath, SceneFilePath, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException($"关卡场景路径不匹配：配置={m_LevelConfig.ScenePath}，实际={SceneFilePath}");
			}

			if (levelRows.Any(x => x.StageOrder != 1 &&
			                       (x.LegacyId != 0 || !string.IsNullOrEmpty(x.NameCn) ||
			                        !string.IsNullOrEmpty(x.Desc) ||
			                        !string.IsNullOrEmpty(x.ScenePath) || !string.IsNullOrEmpty(x.PlayerSpawnPointId) ||
			                        x.StageCount != 0)))
			{
				throw new InvalidOperationException($"关卡 {m_LevelId} 的关卡元数据只能填写在第一阶段行。");
			}

			if (allRows.Any(x => x.LevelId != m_LevelId && x.StageOrder == 1 &&
			                     string.Equals(x.ScenePath, m_LevelConfig.ScenePath,
				                     StringComparison.OrdinalIgnoreCase)))
			{
				throw new InvalidOperationException($"场景路径被多个 LevelId 重复占用：{m_LevelConfig.ScenePath}");
			}

			// 建立场景空间索引，玩家出生点与所有阶段触发器都必须通过稳定 ID 引用。
			BuildSpawnPointIndex();
			if (string.IsNullOrEmpty(m_LevelConfig.PlayerSpawnPointId))
			{
				throw new InvalidOperationException($"关卡未配置玩家出生点：LevelId={m_LevelId}");
			}

			if (!string.IsNullOrEmpty(m_LevelConfig.PlayerSpawnPointId))
			{
				RequireSpawnPoint(m_LevelConfig.PlayerSpawnPointId);
			}

			BuildStageTriggerIndex();
			if (levelRows.Length != m_LevelConfig.StageCount)
			{
				throw new InvalidOperationException(
					$"关卡阶段行数量不匹配：LevelId={m_LevelId}，配置={m_LevelConfig.StageCount}，实际={levelRows.Length}");
			}

			// 阶段外键和顺序分别唯一，防止配方引用歧义或推进中断。
			if (levelRows.Select(x => x.StageOrder).Distinct().Count() != levelRows.Length)
			{
				throw new InvalidOperationException($"阶段顺序重复：LevelId={m_LevelId}");
			}

			// list 表每阶段严格一行；阶段内多怪物配方在独立的 LevelStageConfig 表中关联。
			m_Stages.Clear();
			m_StageRecipes.Clear();
			HashSet<int> stageOrders = new();
			HashSet<(int LevelId, int StageOrder)> levelStageKeys = new();
			HashSet<string> activationTriggerIds = new(StringComparer.Ordinal);
			// 先检查阶段配方表所有行的复合外键，不能让其他阶段的孤儿配方被过滤掉。
			foreach (LevelStageConfig recipe in ConfigSystem.Instance.Tables.TbLevelStageConfig.DataList)
			{
				if (!levelStageKeys.Add((recipe.LevelId, recipe.StageOrder)))
				{
					continue;
				}

				if (!allRows.Any(x => x.LevelId == recipe.LevelId && x.StageOrder == recipe.StageOrder))
				{
					throw new InvalidOperationException(
						$"阶段配方引用不存在的关卡阶段：LevelId={recipe.LevelId}，StageOrder={recipe.StageOrder}");
				}
			}

			foreach (LevelConfig stage in levelRows)
			{
				// 阶段顺序严格唯一；配方多样性不增加 LevelConfig 行数。
				if (!m_Stages.TryAdd(stage.StageOrder, stage) || !stageOrders.Add(stage.StageOrder))
				{
					throw new InvalidOperationException($"阶段顺序重复：StageOrder={stage.StageOrder}");
				}

				if (stage.StageOrder <= 0 || stage.MaxActive <= 0 || stage.ClearPolicy != StageClearPolicy.KillAll)
				{
					throw new InvalidOperationException($"阶段配置非法：StageOrder={stage.StageOrder}");
				}

				// 首阶段由进入关卡启动；后续阶段只能由前阶段清除或唯一触发器启动。
				if (stage.StageOrder == 1)
				{
					if (stage.Activation != StageActivation.OnEnter)
					{
						throw new InvalidOperationException($"首阶段必须使用 OnEnter：StageOrder={stage.StageOrder}");
					}
				}
				else if (stage.Activation == StageActivation.Trigger)
				{
					// 同一个触发器只能推进一个阶段，避免 FirstOrDefault 受行顺序影响。
					RequireStageTrigger(stage.TriggerId);
					if (!activationTriggerIds.Add(stage.TriggerId))
					{
						throw new InvalidOperationException($"多个阶段共用同一个 TriggerId：{stage.TriggerId}");
					}
				}
				else if (stage.Activation != StageActivation.OnClear)
				{
					throw new InvalidOperationException($"阶段激活配置非法：StageOrder={stage.StageOrder}");
				}

				if (stage.Activation != StageActivation.Trigger && !string.IsNullOrEmpty(stage.TriggerId))
				{
					throw new InvalidOperationException($"非 Trigger 阶段不能填写 TriggerId：StageOrder={stage.StageOrder}");
				}

				// 通过 LevelId + StageOrder 关联配方，并以 Sequence 保持策划填写顺序。
				LevelStageConfig[] recipes = ConfigSystem.Instance.Tables.TbLevelStageConfig.DataList
					.Where(x => x.LevelId == m_LevelId && x.StageOrder == stage.StageOrder)
					.OrderBy(x => x.Sequence)
					.ToArray();
				if (recipes.Length == 0 ||
				    recipes.Any(x => x.Sequence <= 0 || x.Count <= 0 || x.Delay < 0f || x.Interval < 0f) ||
				    recipes.Select(x => x.Sequence).Distinct().Count() != recipes.Length ||
				    Enumerable.Range(1, recipes.Length).Any(sequence => recipes.All(x => x.Sequence != sequence)))
				{
					throw new InvalidOperationException($"阶段生成配方序号或数值非法：StageOrder={stage.StageOrder}");
				}

				foreach (LevelStageConfig recipe in recipes)
				{
					RequireSpawnPoint(recipe.SpawnPointId);
					int monsterConfigMatches =
						ConfigSystem.Instance.Tables.TbMonsterConfig.DataList.Count(x =>
							x.EntityId == recipe.MonsterEntityId);
					if (monsterConfigMatches != 1)
					{
						throw new InvalidOperationException(
							$"MonsterConfig 的 EntityId 必须唯一：{recipe.MonsterEntityId}，命中行数={monsterConfigMatches}");
					}
				}

				m_StageRecipes.Add(stage.StageOrder, recipes);
			}

			// 后续阶段逻辑依赖 order + 1，因此阶段序号必须连续且从 1 开始。
			if (Enumerable.Range(1, m_LevelConfig.StageCount).Any(order => !stageOrders.Contains(order)))
			{
				throw new InvalidOperationException($"阶段顺序必须从 1 连续递增：LevelId={m_LevelId}");
			}

			m_Initialized = true;
			ConnectStageTriggers();
		}

		/// <summary>
		/// 启动首阶段；后续阶段由场景触发器和清除规则自动串联。
		/// </summary>
		public async Task StartFirstStageAsync(EntityComponent entityComponent, CancellationToken cancellationToken)
		{
			EnsureInitialized();
			if (entityComponent == null)
			{
				throw new ArgumentNullException(nameof(entityComponent));
			}

			m_SessionToken = cancellationToken;
			m_EntityComponent = entityComponent;
			// 订阅死亡事件后再启动配方，避免首只怪物快速死亡时漏记清波。
			SubscribeDeathEvent();
			LevelConfig stage = m_Stages.Values.OrderBy(x => x.StageOrder).FirstOrDefault();
			if (stage == null)
			{
				throw new InvalidOperationException($"关卡没有阶段配置：LevelId={m_LevelId}");
			}

			// 先完成首阶段生成，后续阶段由触发器和清除事件推进。
			await StartStageAsync(stage, entityComponent, cancellationToken);
		}

		/// <summary>清理本次关卡显示的实体和事件订阅。</summary>
		public void Cleanup(EntityComponent entityComponent)
		{
			// 断开事件和信号，避免卸载后的场景继续接收全局事件。
			DisconnectStageTriggers();
			if (m_Subscribed)
			{
				GF.Event.Unsubscribe(MonsterDiedEventArgs.EventId, OnMonsterDied);
				m_Subscribed = false;
			}

			// 只隐藏本控制器注册的实体，不读取全局实体组数量。
			if (entityComponent != null)
			{
				foreach (IEntity entity in m_ActiveMonsters.Values.ToArray())
				{
					entityComponent.HideEntitySafe(entity);
				}
			}

			m_ActiveMonsters.Clear();
			m_EntityStageOrders.Clear();
			m_StageActiveCounts.Clear();
			m_Stages.Clear();
			m_StartedStages.Clear();
			m_FinishedSpawningStages.Clear();
			m_PendingStageTriggers.Clear();
			m_StageRecipes.Clear();
			m_EntityComponent = null;
			m_LevelConfig = null;
			m_Initialized = false;
		}

		private void BuildSpawnPointIndex() // 校验生成点 ID 并建立场景局部索引。
		{
			if (m_SpawnPointsRoot == null)
			{
				throw new InvalidOperationException($"关卡缺少 SpawnPoints 节点：{Name}");
			}

			// 只遍历关卡显式拥有的根节点，避免 additive 场景中的其他标记混入。
			m_SpawnPoints.Clear();
			foreach (Node child in m_SpawnPointsRoot.GetChildren())
			{
				if (child is not LevelSpawnPoint point)
				{
					continue;
				}

				if (string.IsNullOrWhiteSpace(point.SpawnPointId))
				{
					throw new InvalidOperationException($"生成点 ID 为空：{point.GetPath()}");
				}

				if (!m_SpawnPoints.TryAdd(point.SpawnPointId, point))
				{
					throw new InvalidOperationException($"生成点 ID 重复：{point.SpawnPointId}");
				}
			}

			if (m_RuntimeActors == null)
			{
				Log.Warning("[LevelController] RuntimeActors 插槽为空；GF.Entity 实体仍由实体组管理。");
			}
		}

		private LevelSpawnPoint RequireSpawnPoint(string id) // 查找必需生成点；缺失时快速失败，不回退到默认坐标。
		{
			if (!m_SpawnPoints.TryGetValue(id, out LevelSpawnPoint point))
			{
				throw new InvalidOperationException($"关卡配置引用不存在的 SpawnPointId：{id}");
			}

			return point;
		}

		private Vector2 SpawnPointPosition(string id) => RequireSpawnPoint(id).GlobalPosition; // 读取场景编辑得到的世界坐标。

		private void BuildStageTriggerIndex() // 建立阶段触发器索引并检查 ID 唯一性。
		{
			if (m_StageTriggersRoot == null)
			{
				return;
			}

			// 触发器也由场景根节点限定作用域。
			m_StageTriggers.Clear();
			foreach (Node child in m_StageTriggersRoot.GetChildren())
			{
				if (child is not LevelStageTrigger trigger || string.IsNullOrWhiteSpace(trigger.TriggerId))
				{
					continue;
				}

				if (!m_StageTriggers.TryAdd(trigger.TriggerId, trigger))
				{
					throw new InvalidOperationException($"阶段触发器 ID 重复：{trigger.TriggerId}");
				}
			}
		}

		private void ConnectStageTriggers() // 将场景 Area2D 信号连接到阶段调度逻辑。
		{
			foreach (LevelStageTrigger trigger in m_StageTriggers.Values)
			{
				Area2D.BodyEnteredEventHandler handler = body => OnStageTrigger(trigger, body);
				trigger.BodyEntered += handler;
				m_TriggerHandlers[trigger] = handler;
			}
		}

		private void DisconnectStageTriggers() // 关卡卸载时断开所有持有的委托。
		{
			// 解除保存过的具体委托，避免节点和控制器互相保持引用。
			foreach ((LevelStageTrigger trigger, Area2D.BodyEnteredEventHandler handler) in m_TriggerHandlers)
			{
				if (IsInstanceValid(trigger))
				{
					trigger.BodyEntered -= handler;
				}
			}

			m_TriggerHandlers.Clear();
		}

		private LevelStageTrigger RequireStageTrigger(string id) // 查找配置引用的触发器；缺失即视为关卡数据错误。
		{
			if (!m_StageTriggers.TryGetValue(id, out LevelStageTrigger trigger))
			{
				throw new InvalidOperationException($"关卡配置引用不存在的 TriggerId：{id}");
			}

			return trigger;
		}

		private void EnsureInitialized() // 阻止未经场景与配置校验的关卡进入运行期。
		{
			if (!m_Initialized)
			{
				throw new InvalidOperationException("LevelController 尚未 Initialize。");
			}
		}

		private void SubscribeDeathEvent() // 只订阅一次，用于维护关卡实体归属和清波状态。
		{
			if (m_Subscribed)
			{
				return;
			}

			GF.Event.Subscribe(MonsterDiedEventArgs.EventId, OnMonsterDied);
			m_Subscribed = true;
		}

		private void OnMonsterDied(object sender, GameEventArgs args) // 按 runtime EntityId 移除当前关卡的死亡实体。
		{
			if (args is not MonsterDiedEventArgs died ||
			    !m_EntityStageOrders.TryGetValue(died.EntityId, out int stageOrder))
			{
				return;
			}

			// 事件只在分发期间有效；这里只读取 ID，不保存池化事件参数。
			m_ActiveMonsters.Remove(died.EntityId);
			m_EntityStageOrders.Remove(died.EntityId);
			m_StageActiveCounts[stageOrder]--;
			TryStartPendingStage(stageOrder);
		}

		private async Task StartStageAsync(LevelConfig stage, EntityComponent entityComponent,
			CancellationToken cancellationToken) // 按配方顺序生成本阶段内容并尊重并存上限。
		{
			// 重复触发不会重启已开始的阶段。
			if (!m_StartedStages.Add(stage.StageOrder))
			{
				return;
			}

			// 同阶段配方以 LevelId + StageOrder 关联，行内 Sequence 决定配方执行顺序。
			m_StageActiveCounts[stage.StageOrder] = 0;
			LevelStageConfig[] spawns = m_StageRecipes[stage.StageOrder];
			if (spawns.Length == 0)
			{
				throw new InvalidOperationException($"阶段没有生成配方：StageOrder={stage.StageOrder}");
			}

			foreach (LevelStageConfig spawn in spawns)
			{
				// 每条配方用枚举名定位配置怪物，行序由 Sequence 决定。
				GameConfig.Monster.MonsterConfig[] monsters = ConfigSystem.Instance.Tables.TbMonsterConfig.DataList
					.Where(x => x.EntityId == spawn.MonsterEntityId)
					.ToArray();
				if (monsters.Length != 1)
				{
					throw new InvalidOperationException(
						$"MonsterConfig 的 EntityId 必须唯一：{spawn.MonsterEntityId}，命中行数={monsters.Length}");
				}

				GameConfig.Monster.MonsterConfig monster = monsters[0];
				if (spawn.Delay > 0f)
				{
					// 延时属于当前关卡会话；返回后检查取消，防止卸载后继续刷怪。
					await ToSignal(GetTree().CreateTimer(spawn.Delay), SceneTreeTimer.SignalName.Timeout);
					cancellationToken.ThrowIfCancellationRequested();
				}

				// 分批生成并等待并存名额，避免数量超过阶段上限。
				for (int i = 0; i < spawn.Count; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					while (m_StageActiveCounts[stage.StageOrder] >= stage.MaxActive)
					{
						cancellationToken.ThrowIfCancellationRequested();
						await ToSignal(GetTree().CreateTimer(0.05f), SceneTreeTimer.SignalName.Timeout);
					}

					// 成功显示后登记 runtime entity ID，后续死亡事件才能准确归属阶段。
					IEntity entity =
						await entityComponent.ShowEntityAsync(monster.EntityId, SpawnPointPosition(spawn.SpawnPointId));
					if (entity == null)
					{
						throw new InvalidOperationException($"生成实体失败：MonsterEntityId={spawn.MonsterEntityId}");
					}

					if (cancellationToken.IsCancellationRequested || !IsInsideTree())
					{
						entityComponent.HideEntitySafe(entity);
						cancellationToken.ThrowIfCancellationRequested();
						throw new InvalidOperationException("关卡控制器已离开场景树，取消登记新实体。");
					}

					m_ActiveMonsters[entity.Id] = entity;
					m_EntityStageOrders[entity.Id] = stage.StageOrder;
					m_StageActiveCounts[stage.StageOrder]++;
					if (spawn.Interval > 0f && (i + 1 < spawn.Count || spawn.Sequence < spawns[^1].Sequence))
					{
						// 每批间隔结束后再次检查会话，防止切关期间继续生成。
						await ToSignal(GetTree().CreateTimer(spawn.Interval), SceneTreeTimer.SignalName.Timeout);
						cancellationToken.ThrowIfCancellationRequested();
					}
				}
			}

			m_FinishedSpawningStages.Add(stage.StageOrder);
			TryStartPendingStage(stage.StageOrder);
		}

		private void OnStageTrigger(LevelStageTrigger trigger, Node2D body) // 记录玩家经过的阶段，按序等待清波后激活。
		{
			if (body is not GameLogic.Entity.Heroes.HeroEntity)
			{
				return;
			}

			LevelConfig stage = m_Stages.Values.FirstOrDefault(x =>
				x.Activation == StageActivation.Trigger && x.TriggerId == trigger.TriggerId);
			if (stage == null || m_StartedStages.Contains(stage.StageOrder))
			{
				return;
			}

			// 玩家可能在前一阶段清除前连续越过多个触发器；把已越过的阶段按顺序排队，
			// 避免直接跳到后段而让中间阶段永远没有机会启动。
			foreach (LevelConfig queued in m_Stages.Values.Where(x =>
				         x.StageOrder > 1 && x.StageOrder <= stage.StageOrder))
			{
				m_PendingStageTriggers.Add(queued.StageOrder);
			}

			LevelConfig previous = m_Stages.Values
				.Where(x => x.StageOrder < stage.StageOrder)
				.OrderByDescending(x => x.StageOrder)
				.FirstOrDefault();
			if (previous != null)
			{
				TryStartPendingStage(previous.StageOrder);
			}
		}

		private void TryStartPendingStage(int clearedStageOrder) // 只有配方发完且活跃怪清零，才可开始紧邻的下阶段。
		{
			// 阶段清除需要同时满足：配方全部生成完成，且已登记实体全部死亡。
			if (!m_FinishedSpawningStages.Contains(clearedStageOrder) ||
			    !m_StageActiveCounts.TryGetValue(clearedStageOrder, out int activeCount) || activeCount > 0)
			{
				return;
			}

			// 只查找紧邻的下一阶段，禁止跨过未激活阶段。
			LevelConfig clearedStage = m_Stages[clearedStageOrder];
			LevelConfig next = m_Stages.Values
				.Where(x => x.StageOrder == clearedStageOrder + 1)
				.OrderBy(x => x.StageOrder)
				.FirstOrDefault();
			if (next == null)
			{
				return;
			}

			// 触发式阶段必须已被玩家经过；OnClear 阶段在清波后直接推进。
			if (next.Activation != StageActivation.OnClear && !m_PendingStageTriggers.Contains(next.StageOrder))
			{
				return;
			}

			m_PendingStageTriggers.Remove(next.StageOrder);
			_ = StartStageSafeAsync(next);
		}

		private async Task StartStageSafeAsync(LevelConfig stage) // 处理事件回调启动阶段时的异步异常。
		{
			try
			{
				await StartStageAsync(stage, m_EntityComponent, m_SessionToken);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				Log.Error("[LevelController] 阶段启动失败：StageOrder={0}，{1}", stage.StageOrder, ex);
			}
		}
	}
}
