using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameConfig.Hero;
using GameConfig.Level;
using GameConfig.Monster;
using GameConfig.Sound;
using GameFramework;
using GameFramework.Event;
using GameLogic.Config;
using GameLogic.Entity;
using GameLogic.Entity.Heroes;
using GameLogic.Event;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.Sound;
using GodotGameFrameworkCore.SingletonSystem;

namespace GameLogic.Manager
{
	/// <summary>
	/// 关卡导演（M6）：一场对局内"关卡内容"的唯一权威——波次推进（TriggerX）、场上上限与补怪节奏
	/// （MaxAlive / SpawnInterval）、清场开闸、出口开启、击杀与用时统计，并把每个节点变化广播成事件。
	///
	/// **职责边界**（对齐 Unreal GameMode 的 MatchState / 事件驱动 WaveManager 的行业惯例）：
	///  * 只管对局内容，不管流程——加载遮罩、表单开关、切流程、写存档都归 ProcedureBattle；
	///  * 与外界只经事件通信（WaveStarted / WaveCleared / LevelCleared / LevelFailed），UI 不持实体引用；
	///  * 场景节点由流程**显式注入**（红线 8：生成方注入，不做跨模块 GetNode 长链），关卡场景子树内的
	///    直引用是合法的（关卡标记是场景数据，代码不写几何数值）。
	///
	/// 注入的场景节点约定（Level_1.tscn，同名节点缺省时告警跳过，不致命）：
	/// <code>
	/// Level_1 (Node2D)              ← StartLevel 的 sceneRoot 参数
	/// ├─ HeroSpawn (Node2D)          英雄出生点
	/// ├─ Gate1..3 (StaticBody2D)     波次闸门（子节点 CollisionShape2D；清场置 Disabled）
	/// ├─ Exit (Node2D)
	/// │   ├─ Portal (AnimatedSprite2D)      出口传送门动画（初始隐藏）
	/// │   └─ ExitArea (Area2D)              触碰通关（Exit 层扫 PlayerBody；初始 Disabled）
	/// └─ BattleCamera (Camera2D)     跟随英雄（限幅 LimitLeft.. 在场景属性里配）
	/// </code>
	///
	/// 生命周期（DamagePopManager 先例）：对局流程进入时 <see cref="StartLevel"/>、离开（重试/返回/关停）
	/// 时 <see cref="StopBattle"/>；<see cref="SingletonNode{T}"/> 常驻场景树，跨局状态一律在这两个入口复位。
	/// 怪物尸体不归它管：MonsterEntity 死亡动画播完自行回收（框架实体组），这里只跟踪"已生成未死亡"。
	/// </summary>
	public partial class LevelDirector : SingletonNode<LevelDirector>
	{
		/// <summary>波次运行时：表配置 + 刷怪队列 + 已生成未死亡的实体编号集合。</summary>
		private sealed class WaveRuntime
		{
			public LevelWaveConfig Config;

			/// <summary>待刷的刷怪点（按表 Id 顺序出队）</summary>
			public readonly Queue<LevelSpawnConfig> PendingSpawns = new();

			/// <summary>本波已请求生成、尚未死亡的怪物实体编号</summary>
			public readonly HashSet<int> LiveIds = new();

			/// <summary>是否已广播 WaveCleared</summary>
			public bool Cleared;
		}

		private LevelConfig m_LevelConfig;
		private WaveRuntime[] m_Waves;
		private int m_ActivatedCount;
		private HeroEntity m_Hero;
		private Camera2D m_Camera;
		private Area2D m_ExitArea;
		private CollisionShape2D m_ExitShape;
		private AnimatedSprite2D m_ExitPortal;

		private bool m_BattleActive;
		private bool m_ResultFired;
		private bool m_Subscribed;
		private bool m_ExitWired;
		private float m_Elapsed;
		private float m_SpawnCooldown;
		private int m_KillCount;

		/// <summary>英雄出生点（场景 HeroSpawn 标记）</summary>
		private Vector2 m_HeroSpawnPosition;

		/// <summary>注入的关卡场景根（闸门/出口都在它子树内）</summary>
		private Node2D m_SceneRoot;

		/// <summary>对局是否进行中（结果已出即结束；重刷由流程重入 StartLevel 完成）。</summary>
		public bool BattleActive => m_BattleActive;

		/// <summary>
		/// 开始一局：读表组装波次、生成英雄、接线出口、播关卡 BGM、激活第 1 波。
		/// 由对局流程在场景加载完成后调用（场景根节点显式注入）。
		/// </summary>
		public async Task StartLevel(int levelId, int heroId, Node2D sceneRoot)
		{
			if (m_BattleActive)
			{
				Log.Error("[LevelDirector] 上一局尚未结束（先 StopBattle 再 StartLevel）");
				return;
			}

			if (sceneRoot == null)
			{
				Log.Error("[LevelDirector] sceneRoot 为空，无法开始关卡 {0}", levelId);
				return;
			}

			m_LevelConfig = ConfigSystem.Instance.Tables.TbLevelConfig.GetOrDefault(levelId);
			if (m_LevelConfig == null)
			{
				Log.Error("[LevelDirector] LevelConfig 缺失行：Id={0}", levelId);
				return;
			}

			m_Waves = BuildWaves(levelId);
			if (m_Waves.Length == 0)
			{
				Log.Error("[LevelDirector] LevelWaveConfig 没有关卡 {0} 的波次行", levelId);
				return;
			}

			SubscribeBattleEvents();
			ReadSceneMarkers(sceneRoot);
			PlayLevelBgm();

			m_Hero = await SpawnHero(heroId);
			if (m_Hero == null)
			{
				Log.Error("[LevelDirector] 英雄生成失败：HeroId={0}", heroId);
				StopBattle();
				return;
			}

			m_Camera?.MakeCurrent();
			m_BattleActive = true;
			m_ResultFired = false;
			m_Elapsed = 0f;
			m_SpawnCooldown = 0f;
			m_KillCount = 0;
			m_ActivatedCount = 0;

			// 进关即触发 TriggerX=0 的波（以及英雄出生点已经越过的触发线——闸门挡住常态流程，这里只是容错）
			TryActivateWaves();
		}

		/// <summary>
		/// 结束一局（重试/返回选人/流程离开）：退订事件、收掉场上怪物与英雄、复位状态。
		/// 不触碰场景节点（场景由流程随后卸载）；怪物尸体（已死亡待回收）自行收尾。
		/// </summary>
		public void StopBattle()
		{
			m_BattleActive = false;

			if (m_Subscribed)
			{
				GF.Event.Unsubscribe(MonsterDiedEventArgs.EventId, OnMonsterDied);
				GF.Event.Unsubscribe(HeroDiedEventArgs.EventId, OnHeroDied);
				m_Subscribed = false;
			}

			if (m_ExitWired && m_ExitArea != null && IsInstanceValid(m_ExitArea))
			{
				m_ExitArea.BodyEntered -= OnExitBodyEntered;
			}

			m_ExitWired = false;
			m_ExitArea = null;
			m_ExitShape = null;
			m_ExitPortal = null;
			m_Camera = null;

			if (m_Waves != null)
			{
				foreach (WaveRuntime wave in m_Waves)
				{
					foreach (int id in wave.LiveIds)
					{
						GF.Entity.HideEntitySafe(id);
					}

					wave.LiveIds.Clear();
					wave.PendingSpawns.Clear();
					wave.Cleared = false;
				}

				m_Waves = null;
			}

			if (m_Hero != null)
			{
				GF.Entity.HideEntitySafe(m_Hero);
				m_Hero = null;
			}

			m_ActivatedCount = 0;
			m_KillCount = 0;
			m_Elapsed = 0f;
			m_SpawnCooldown = 0f;
			m_ResultFired = false;
			m_LevelConfig = null;
		}

		/// <summary>物理帧：波次触发 → 补怪 → 清场判定 → 相机跟随 → 计时。对局结束即空转。</summary>
		public override void _PhysicsProcess(double delta)
		{
			if (!m_BattleActive || m_Hero == null || !m_Hero.IsShown)
			{
				return;
			}

			float dt = (float)delta;
			m_Elapsed += dt;
			m_SpawnCooldown -= dt;

			TryActivateWaves();
			TrySpawnMonsters();
			CheckWaveCleared();

			if (m_Camera != null && IsInstanceValid(m_Camera))
			{
				m_Camera.GlobalPosition = m_Hero.GlobalPosition;
			}
		}

		// ---- 组装 ----

		/// <summary>按 WaveIndex 排序组装波次，并把 LevelSpawnConfig 按波归队（按表 Id 顺序 = 刷怪顺序）。</summary>
		private WaveRuntime[] BuildWaves(int levelId)
		{
			Dictionary<int, Queue<LevelSpawnConfig>> spawnsByWave =
				new Dictionary<int, Queue<LevelSpawnConfig>>();
			foreach (LevelSpawnConfig spawn in ConfigSystem.Instance.Tables.TbLevelSpawnConfig.DataList)
			{
				if (!spawnsByWave.TryGetValue(spawn.WaveId, out Queue<LevelSpawnConfig> queue))
				{
					queue = new Queue<LevelSpawnConfig>();
					spawnsByWave.Add(spawn.WaveId, queue);
				}

				queue.Enqueue(spawn);
			}

			List<WaveRuntime> waves = new List<WaveRuntime>();
			foreach (LevelWaveConfig waveConfig in ConfigSystem.Instance.Tables.TbLevelWaveConfig.DataList
				.Where(x => x.LevelId == levelId).OrderBy(x => x.WaveIndex))
			{
				WaveRuntime wave = new WaveRuntime { Config = waveConfig };
				if (spawnsByWave.TryGetValue(waveConfig.Id, out Queue<LevelSpawnConfig> queue))
				{
					foreach (LevelSpawnConfig spawn in queue)
					{
						wave.PendingSpawns.Enqueue(spawn);
					}
				}
				else
				{
					Log.Warning("[LevelDirector] 波次 {0}({1}) 没有任何刷怪点行", waveConfig.Id, waveConfig.NameCn);
				}

				waves.Add(wave);
			}

			return waves.ToArray();
		}

		/// <summary>读取注入场景的标记节点（HeroSpawn / Gate / Exit / BattleCamera）；缺失只告警，不致命。</summary>
		private void ReadSceneMarkers(Node2D sceneRoot)
		{
			Node2D heroSpawn = sceneRoot.GetNodeOrNull<Node2D>("HeroSpawn");
			if (heroSpawn == null)
			{
				Log.Warning("[LevelDirector] 场景缺 HeroSpawn 标记，英雄落在 (0,0)");
			}

			m_HeroSpawnPosition = heroSpawn?.GlobalPosition ?? Vector2.Zero;
			m_SceneRoot = sceneRoot;
			m_Camera = sceneRoot.GetNodeOrNull<Camera2D>("BattleCamera");

			m_ExitArea = sceneRoot.GetNodeOrNull<Area2D>("Exit/ExitArea");
			m_ExitShape = sceneRoot.GetNodeOrNull<CollisionShape2D>("Exit/ExitArea/CollisionShape2D");
			m_ExitPortal = sceneRoot.GetNodeOrNull<AnimatedSprite2D>("Exit/Portal");

			if (m_ExitArea == null || m_ExitShape == null || m_ExitPortal == null)
			{
				Log.Warning("[LevelDirector] 场景缺 Exit/ExitArea/CollisionShape2D 或 Exit/Portal，通关出口不可用");
				return;
			}

			m_ExitArea.BodyEntered += OnExitBodyEntered;
			m_ExitWired = true;
		}

		/// <summary>生成英雄（表驱动：HeroConfig.EntityId → 实体场景），出生点取场景标记。</summary>
		private async Task<HeroEntity> SpawnHero(int heroId)
		{
			HeroConfig heroConfig = ConfigSystem.Instance.Tables.TbHeroConfig.GetOrDefault(heroId);
			if (heroConfig == null)
			{
				Log.Error("[LevelDirector] HeroConfig 缺失行：Id={0}", heroId);
				return null;
			}

			HeroEntity hero = await GF.Entity.ShowEntityAsync<HeroEntity>(heroConfig.EntityId, null);
			if (hero != null)
			{
				hero.GlobalPosition = m_HeroSpawnPosition;
			}

			return hero;
		}

		/// <summary>播关卡 BGM（表驱动：LevelConfig.BgmSoundId → SoundConfig；PlayBGM 自带停上一首）。</summary>
		private void PlayLevelBgm()
		{
			if (m_LevelConfig.BgmSoundId == SoundId.None)
			{
				return;
			}

			SoundConfig cfg = SoundConfigQuery.Get(m_LevelConfig.BgmSoundId);
			if (cfg == null)
			{
				Log.Error("[LevelDirector] SoundConfig 缺失行：SoundId={0}", m_LevelConfig.BgmSoundId);
				return;
			}

			GF.Sound.PlayBGM(cfg.Path);
		}

		private void SubscribeBattleEvents()
		{
			if (m_Subscribed)
			{
				return;
			}

			GF.Event.Subscribe(MonsterDiedEventArgs.EventId, OnMonsterDied);
			GF.Event.Subscribe(HeroDiedEventArgs.EventId, OnHeroDied);
			m_Subscribed = true;
		}

		// ---- 波次推进 ----

		/// <summary>激活所有触发线已被越过的波（闸门挡在触发线之前，正常时序一波一开；越线仅是容错）。</summary>
		private void TryActivateWaves()
		{
			while (m_ActivatedCount < m_Waves.Length && m_Hero.GlobalPosition.X >= m_Waves[m_ActivatedCount].Config.TriggerX)
			{
				WaveRuntime wave = m_Waves[m_ActivatedCount];
				m_ActivatedCount++;
				Log.Info("[LevelDirector] 关卡 {0} 波次 {1}/{2} 开始（英雄 x={3:F0}，本波 {4} 只）",
					m_LevelConfig.Id, wave.Config.WaveIndex, m_Waves.Length, m_Hero.GlobalPosition.X,
					wave.PendingSpawns.Count + wave.LiveIds.Count);
				GF.Event.Fire(this, WaveStartedEventArgs.Create(m_LevelConfig.Id, wave.Config.WaveIndex, m_Waves.Length));
			}
		}

		/// <summary>补怪：场上（已生成未死亡）低于 MaxAlive 且冷却转好，就按波次顺序补一只（出生点 userData 传入）。</summary>
		private void TrySpawnMonsters()
		{
			if (m_SpawnCooldown > 0f)
			{
				return;
			}

			int alive = m_Waves.Sum(w => w.LiveIds.Count);
			if (alive >= m_LevelConfig.MaxAlive)
			{
				return;
			}

			foreach (WaveRuntime wave in m_Waves)
			{
				if (wave.Cleared || wave.PendingSpawns.Count == 0)
				{
					continue;
				}

				LevelSpawnConfig spawn = wave.PendingSpawns.Dequeue();
				MonsterConfig monster = ConfigSystem.Instance.Tables.TbMonsterConfig.GetOrDefault(spawn.MonsterId);
				if (monster == null)
				{
					Log.Error("[LevelDirector] MonsterConfig 缺失行：MonsterId={0}（刷怪点 {1} 跳过）",
						spawn.MonsterId, spawn.NameCn);
					return;
				}

				// 出生点经 userData 传入（MonsterEntity.OnShow 约定 Vector2）
				int id = GF.Entity.ShowEntity(monster.EntityId, new Vector2(spawn.X, spawn.Y));
				if (id > 0)
				{
					wave.LiveIds.Add(id);
				}

				m_SpawnCooldown = m_LevelConfig.SpawnInterval;
				return;
			}
		}

		/// <summary>清场判定：队列刷完且本波全部死亡 → 广播 WaveCleared、开闸；最后一波清 → 开出口。</summary>
		private void CheckWaveCleared()
		{
			for (int i = 0; i < m_Waves.Length; i++)
			{
				WaveRuntime wave = m_Waves[i];
				if (wave.Cleared || wave.PendingSpawns.Count > 0 || wave.LiveIds.Count > 0)
				{
					continue;
				}

				// 没有任何刷怪点行的空波：从未激活过（m_ActivatedCount 未到）不算清场
				if (i >= m_ActivatedCount)
				{
					continue;
				}

				wave.Cleared = true;
				GF.Event.Fire(this, WaveClearedEventArgs.Create(m_LevelConfig.Id, wave.Config.WaveIndex));

				if (i == m_Waves.Length - 1)
				{
					Log.Info("[LevelDirector] 关卡 {0} 最终波清场，开启出口", m_LevelConfig.Id);
					OpenExit();
				}
				else
				{
					Log.Info("[LevelDirector] 关卡 {0} 波次 {1} 清场，开闸 Gate{1}", m_LevelConfig.Id, wave.Config.WaveIndex);
					OpenGate(wave.Config.WaveIndex);
				}
			}
		}

		/// <summary>开闸：置场景 Gate{waveIndex} 的碰撞为 Disabled（闸门几何是场景数据，代码只拨开关）。</summary>
		private void OpenGate(int waveIndex)
		{
			if (m_SceneRoot == null || !IsInstanceValid(m_SceneRoot))
			{
				return;
			}

			CollisionShape2D gate = m_SceneRoot.GetNodeOrNull<CollisionShape2D>($"Gate{waveIndex}/CollisionShape2D");
			if (gate == null)
			{
				Log.Warning("[LevelDirector] 波次 {0} 清场但场景缺 Gate{0}/CollisionShape2D（闸门未开）", waveIndex);
				return;
			}

			gate.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		}

		/// <summary>开出口：传送门显形播放、碰撞启用；英雄触碰由 <see cref="OnExitBodyEntered"/> 判定。</summary>
		private void OpenExit()
		{
			if (m_ExitPortal == null || m_ExitShape == null)
			{
				return;
			}

			m_ExitPortal.Visible = true;
			if (!m_ExitPortal.IsPlaying())
			{
				m_ExitPortal.Play();
			}

			m_ExitShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
		}

		// ---- 事件回调（参数用完即止，不持有：根规范 §5.2/§9）----

		private void OnMonsterDied(object sender, GameEventArgs args)
		{
			if (!m_BattleActive || args is not MonsterDiedEventArgs e)
			{
				return;
			}

			m_KillCount++;
			foreach (WaveRuntime wave in m_Waves)
			{
				wave.LiveIds.Remove(e.EntityId);
			}
		}

		private void OnHeroDied(object sender, GameEventArgs args)
		{
			if (!m_BattleActive || args is not HeroDiedEventArgs e)
			{
				return;
			}

			m_BattleActive = false;
			Log.Info("[LevelDirector] 关卡 {0} 失败：用时 {1:F1}s，击杀 {2}，凶手实体 {3}",
				m_LevelConfig.Id, m_Elapsed, m_KillCount, e.KillerEntityId);
			FireResult(LevelFailedEventArgs.Create(m_LevelConfig.Id, e.KillerEntityId, m_Elapsed, m_KillCount));
		}

		/// <summary>英雄触碰出口 → 通关（对局结束；结算数据快照随事件带走）。</summary>
		private void OnExitBodyEntered(Node body)
		{
			if (!m_BattleActive || m_Hero == null || body != m_Hero)
			{
				return;
			}

			m_BattleActive = false;
			Log.Info("[LevelDirector] 关卡 {0} 通关：用时 {1:F1}s，击杀 {2}，剩余 HP {3}/{4}",
				m_LevelConfig.Id, m_Elapsed, m_KillCount, m_Hero.Hp, m_Hero.MaxHp);
			FireResult(LevelClearedEventArgs.Create(m_LevelConfig.Id, m_Elapsed, m_KillCount, m_Hero.Hp, m_Hero.MaxHp));
		}

		/// <summary>结果只广播一次（出口/死亡同帧竞争时以先到者为准）。</summary>
		private void FireResult(GameEventArgs args)
		{
			if (m_ResultFired)
			{
				ReferencePool.Release(args);
				return;
			}

			m_ResultFired = true;
			GF.Event.Fire(this, args);
		}

		/// <summary>单例释放兜底：常驻节点被移出树时退订（DamagePopManager 同款）。</summary>
		protected override void OnRelease()
		{
			if (m_Subscribed)
			{
				GF.Event.Unsubscribe(MonsterDiedEventArgs.EventId, OnMonsterDied);
				GF.Event.Unsubscribe(HeroDiedEventArgs.EventId, OnHeroDied);
				m_Subscribed = false;
			}

			base.OnRelease();
		}
	}
}
