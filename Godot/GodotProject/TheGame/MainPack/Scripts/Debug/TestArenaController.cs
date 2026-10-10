using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameConfig;
using GameConfig.Entity;
using GameFramework;
using GameFramework.Entity;
using GameLogic.Config;
using GameLogic.Entity.Monsters;
using GameLogic.Profile;
using GameLogic.UI;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.NodePool;

/// <summary>
/// 怪物与角色测试场地（独立场景，不进入游戏流程）。
///
/// 用法：在 Godot 里对 <c>TestArena.tscn</c> 按 F6“运行当前场景”即可，不需要改流程或配置表。
/// 场景内自带一份**真实的** <c>Framework/GameFramework.tscn</c>（去掉 Procedure 流程），脚本只补上正式流程里
/// <c>ProcedurePreload</c> 的三组注册与节点池启动；之后把怪物实体场景拖进 <c>Monsters</c> 容器、摆好位置即可测试。
///
/// 占位节点只提供“哪只怪、放在哪”，真正的实体仍由 <see cref="GF.Entity"/> 池化创建，生命周期与正式关卡一致。
/// </summary>
public partial class TestArenaController : Node2D
{
	[Export] private PackedScene m_FrameworkScene; // 真实框架场景；仅去掉 Procedure 流程后复用。

	[Export] private EntityId m_HeroEntityId = EntityId.Wukong; // 测试用玩家实体枚举。

	[Export] private Node2D m_PlayerStart; // 玩家出生点（Marker2D）。

	[Export] private Node2D m_Monsters; // 怪物占位容器：拖入怪物实体场景即可测试。

	[Export] private Camera2D m_Camera; // 跟随玩家的测试相机；限位由场景 Camera2D 设置。

	private Node2D m_Hero; // 已显示的玩家节点，供相机跟随。
	private float m_CameraY; // 相机固定的纵向位置。
	private DamagePopPresenter m_DamagePops; // 场地的伤害飘字表现，离开场景树时释放。

	/// <summary>离开场景树时释放飘字订阅。</summary>
	public override void _ExitTree()
	{
		m_DamagePops?.Dispose();
		m_DamagePops = null;
	}

	/// <summary>准备框架、取走怪物占位节点，然后异步启动测试场地。</summary>
	public override void _Ready()
	{
		m_CameraY = m_Camera.GlobalPosition.Y;
		EnsureFramework();
		ActivateTestServices();
		List<(EntityId Id, Vector2 Position)> monsters = TakeMonsterPlaceholders();
		_ = StartAsync(monsters);
	}

	/// <summary>相机水平跟随玩家；纵向保持场景初始高度，限位交给场景 Camera2D。</summary>
	/// <param name="delta">距上一帧的秒数，不使用。</param>
	public override void _Process(double delta)
	{
		if (m_Hero == null || !IsInstanceValid(m_Hero))
		{
			return;
		}

		m_Camera.GlobalPosition = new Vector2(m_Hero.GlobalPosition.X, m_CameraY);
	}

	/// <summary>复用真实框架场景；若已在框架内运行则跳过，去掉 Procedure 以避免进入正式流程。</summary>
	private void EnsureFramework()
	{
		if (GameEntry.GetComponent<EntityComponent>() != null)
		{
			return;
		}

		Node framework = m_FrameworkScene.Instantiate();
		if (framework.GetNodeOrNull("Procedure") is Node procedure)
		{
			// 在挂树前移除流程组件：它一旦 OnInit 就会启动 ProcedureLaunch 进入正式游戏。
			framework.RemoveChild(procedure);
			procedure.Free();
		}

		AddChild(framework);
	}

	/// <summary>补上正式流程 <c>ProcedurePreload</c> 的注册步骤：实体/UI/声音分组与节点池、层级工具。</summary>
	private void ActivateTestServices()
	{
		var entityGroups = GF.Entity.EntityGroupRes.EntityGroups;
		for (int i = 0; i < entityGroups.Length; i++)
		{
			GF.Entity.AddEntityGroup(entityGroups[i].Name, entityGroups[i].ReleaseInterval, entityGroups[i].Capacity,
				entityGroups[i].ExpireTime, entityGroups[i].Priority);
		}

		var uiGroups = GF.UI.UIGroupRes.Groups;
		for (int i = 0; i < uiGroups.Length; i++)
		{
			GF.UI.AddUIGroup(uiGroups[i].Name, uiGroups[i].Depth);
		}

		var soundGroups = GF.Sound.SoundGroupRes.SoundGroups;
		for (int i = 0; i < soundGroups.Length; i++)
		{
			GF.Sound.AddSoundGroup(soundGroups[i].Name, soundGroups[i].AgentCounts,
				soundGroups[i].AvoidBeingReplacedBySamePriority);
		}

		NodePool.Instance.Active();
		LayerMask.Instance.Active();
	}

	/// <summary>等框架组件就绪后显示玩家与全部占位怪物。</summary>
	/// <param name="monsters">占位节点提供的怪物枚举与出生坐标。</param>
	/// <returns>启动完成的异步任务。</returns>
	private async Task StartAsync(List<(EntityId Id, Vector2 Position)> monsters)
	{
		try
		{
			// 等一帧，确保框架组件的 OnEnter 已完成。
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			m_DamagePops = new DamagePopPresenter(GF.Entity);
			await SpawnHeroAsync();
			foreach ((EntityId id, Vector2 position) in monsters)
			{
				await GF.Entity.ShowEntityAsync(id, position);
				Log.Info("[TestArena] 显示怪物 {0} @ {1}", id, position);
			}

			Log.Info("[TestArena] 测试场地就绪：玩家 {0}，怪物 {1} 只", m_HeroEntityId, monsters.Count);
		}
		catch (Exception error)
		{
			// 异步任务不能静默失败，否则场地看起来“什么都没发生”。
			GD.PushError($"[TestArena] 启动失败：{error}");
		}
	}

	/// <summary>
	/// 显示测试玩家并放到出生点。属性只来自出战装配：用建档规则创建一份仅内存的临时档案，不读写存档。
	/// </summary>
	/// <returns>显示完成的异步任务。</returns>
	private async Task SpawnHeroAsync()
	{
		Tables tables = ConfigSystem.Instance.Tables;
		ExperienceCurve curve = ConfigValidator.ValidateAll(tables);
		int heroId = tables.TbHeroConfig.DataList.FirstOrDefault(x => x.EntityId == m_HeroEntityId)?.Id
			?? throw new InvalidOperationException($"HeroConfig 中没有实体 {m_HeroEntityId}");
		HeroRecord record = new(heroId, new HeroProgression(curve, 0));
		HeroLoadout loadout = new HeroStatBuilder(tables.TbHeroGrowthConfig).Build(record);
		IEntity entity = await GF.Entity.ShowEntityAsync(m_HeroEntityId, loadout);
		if (entity?.Handle is not Node2D hero)
		{
			GD.PushError($"[TestArena] 玩家实体显示失败：{m_HeroEntityId}");
			return;
		}

		hero.GlobalPosition = m_PlayerStart.GlobalPosition;
		m_Hero = hero;
		Log.Info("[TestArena] 显示玩家 {0} @ {1}", m_HeroEntityId, hero.GlobalPosition);
	}

	/// <summary>取出并移除 Monsters 容器中的怪物占位节点，返回它们提供的枚举与坐标。</summary>
	/// <returns>每只待生成怪物的枚举与出生坐标。</returns>
	private List<(EntityId Id, Vector2 Position)> TakeMonsterPlaceholders()
	{
		List<(EntityId Id, Vector2 Position)> monsters = new();
		foreach (Node child in m_Monsters.GetChildren())
		{
			if (child is not MonsterEntity placeholder)
			{
				continue;
			}

			monsters.Add((placeholder.MonsterEntityId, placeholder.GlobalPosition));
			m_Monsters.RemoveChild(placeholder);
			placeholder.QueueFree();
		}

		return monsters;
	}
}
