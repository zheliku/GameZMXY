using System.Collections.Generic;
using System.Linq;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Config;
using GameLogic.Drops;
using GameLogic.Equipment;
using GameLogic.Input;
using GameLogic.Level;
using GameLogic.Save;
using GameLogic.Skills;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 测试专用作用域；正式代码的档案和本局英雄模型分别持有与初始化。
/// </summary>
internal sealed class TestGameScope : IArchitecture
{
    private readonly LevelSystem _levelSystem; // 正式关卡业务规则。
    private readonly EquipmentSystem _equipmentSystem; // 正式装备业务规则。
    private readonly SaveSystem _saveSystem; // 仅隔离磁盘写入。
    private readonly ProfileSystem _profileSystem; // 档案数据的业务协调。
    private readonly HeroSystem _heroSystem; // 与游戏使用同一来源协调逻辑。
    private readonly HeroStatusSystem _heroStatusSystem; // 本局状态效果规则。
    private readonly SkillSystem _skillSystem; // 正式技能购买规则。
    private readonly InputSystem _inputSystem; // 正式输入组合与方向规则。
    private readonly LootSystem _lootSystem; // 正式球体回蓝与掉落规则。
    private readonly SaveDataUtility _save = new(); // 与游戏注册的相同纯转换工具。
    private bool _isMapContext; // 测试地图购技时隐藏本局英雄与关卡模型。

    /// <summary>
    /// 测试提供的任意动作事实，走正式输入采样与消费。
    /// </summary>
    public TestInputSource Input { get; } = new();

    /// <summary>
    /// 档案的唯一权威数据。
    /// </summary>
    public ProfileModel Profile { get; }

    /// <summary>
    /// 当前档案独占的技能学习记录。
    /// </summary>
    public SkillModel Skills { get; }

    /// <summary>
    /// 本局英雄的全部数值。
    /// </summary>
    public HeroModel Hero { get; private set; } = new();

    /// <summary>
    /// 本局模型，由正式关卡系统修改。
    /// </summary>
    public LevelModel Level { get; } = new();

    /// <summary>
    /// 建立独立测试作用域，不复制正式规则。
    /// </summary>
    /// <param name="heroes">累计经验。</param>
    /// <param name="soul">灵魂余额。</param>
    /// <param name="capacity">背包容量。</param>
    /// <param name="initializeHero">是否装配已有可玩英雄数值；未导入角色的技能机制测试自行提供本局蓝量。</param>
    public TestGameScope(IEnumerable<(int HeroId, int TotalExperience)> heroes,
        int soul, int capacity, bool initializeHero = true)
    {
        Profile = new(heroes, TestTables.Curve, capacity, soul);
        Skills = new(Profile.HeroIds);
        foreach (IModel model in new IModel[] { Profile, Skills, Hero, Level })
        {
            model.SetArchitecture(this);
            model.Init();
        }
        _profileSystem = new ProfileSystem(this);
        ((ICanInit)_profileSystem).Init();
        _heroStatusSystem = new HeroStatusSystem(this);
        ((ICanInit)_heroStatusSystem).Init();
        _heroSystem = new HeroSystem(this);
        if (initializeHero)
        {
            ((ICanInit)_heroSystem).Init();
        }

        _equipmentSystem = new EquipmentSystem(this);
        _skillSystem = new SkillSystem(this);
        _inputSystem = new InputSystem(this, Input);
        _lootSystem = new LootSystem(this);
        _saveSystem = new SaveSystem(this);
        _levelSystem = new LevelSystem(this);
        if (Profile.HeroIds[0] == 1)
        {
            _skillSystem.SelectSkills(1, GameLogic.SkillTests.SkillTestDefinitions.Wukong());
        }
    }

    /// <inheritdoc />
    public TModel GetModel<TModel>() where TModel : class, IModel
    {
        return (typeof(TModel) == typeof(ConfigurationModel) ? TestTables.Configuration :
            typeof(TModel) == typeof(ProfileModel) ? (IModel)Profile :
            typeof(TModel) == typeof(SkillModel) ? Skills :
            typeof(TModel) == typeof(HeroModel) ? (_isMapContext ? null : (IModel)Hero) :
            typeof(TModel) == typeof(LevelModel) ? (_isMapContext ? null : (IModel)Level) : null) as TModel;
    }

    /// <summary>
    /// 让后续命令观察地图作用域中不存在的战斗模型。
    /// </summary>
    public void EnterMapContext() => _isMapContext = true;

    /// <summary>
    /// 让后续系统访问恢复为战斗作用域。
    /// </summary>
    public void EnterBattleContext() => _isMapContext = false;

    /// <inheritdoc />
    public TSystem GetSystem<TSystem>() where TSystem : class, ISystem
    {
        return (typeof(TSystem) == typeof(ProfileSystem) ? (ISystem)_profileSystem :
            typeof(TSystem) == typeof(HeroSystem) ? _heroSystem :
            typeof(TSystem) == typeof(HeroStatusSystem) ? _heroStatusSystem :
            typeof(TSystem) == typeof(LevelSystem) ? _levelSystem :
            typeof(TSystem) == typeof(EquipmentSystem) ? _equipmentSystem :
            typeof(TSystem) == typeof(SkillSystem) ? _skillSystem :
            typeof(TSystem) == typeof(InputSystem) ? _inputSystem :
            typeof(TSystem) == typeof(LootSystem) ? _lootSystem :
            typeof(TSystem) == typeof(SaveSystem) ? _saveSystem : null) as TSystem;
    }

    /// <inheritdoc />
    public TUtility GetUtility<TUtility>() where TUtility : class, IUtility
    {
        return (typeof(TUtility) == typeof(ExperienceCurveUtility) ? (IUtility)TestTables.Curve :
            typeof(TUtility) == typeof(ConfigIdUtility) ? TestTables.ConfigIds : _save) as TUtility;
    }

    /// <summary>
    /// 调用正式转换。
    /// </summary>
    /// <returns>独立快照。</returns>
    public GameData CaptureSaveData() => _save.Capture(Profile, Skills);

    /// <summary>
    /// 模拟正式流程的离关/重试：结束数值系统与旧本局模型，再建立新模型，保留档案及技能系统。
    /// </summary>
    public void RestartHero()
    {
        _skillSystem.ClearSkillDefinitions();
        ((ICanInit)_heroSystem).Deinit();
        ((ICanInit)_heroStatusSystem).Deinit();
        ((ICanInit)Hero).Deinit();
        Hero = new HeroModel();
        ((ICanSetArchitecture)Hero).SetArchitecture(this);
        ((ICanInit)Hero).Init();
        ((ICanInit)_heroStatusSystem).Init();
        ((ICanInit)_heroSystem).Init();
    }

    /// <summary>
    /// 调用正式回滚。
    /// </summary>
    /// <param name="data">进关快照。</param>
    public void RestoreCheckpoint(GameData data) => _profileSystem.RestoreCheckpoint(data);

    /// <summary>
    /// 验证后建立测试作用域，映射算法使用正式工具。
    /// </summary>
    /// <param name="data">当前格式数据。</param>
    /// <param name="curve">经验规则。</param>
    /// <param name="definitions">装备定义。</param>
    /// <param name="capacity">容量。</param>
    /// <returns>独立测试作用域。</returns>
    public static TestGameScope FromSaveData(GameData data, ExperienceCurveUtility curve,
        GameConfig.Equipment.TbEquipmentConfig definitions, int capacity)
    {
        var utility = new SaveDataUtility();
        utility.Validate(data, definitions, capacity, TestTables.Tables.TbSkillConfig,
            TestTables.Tables.TbLevelConfig, TestTables.Tables.TbPassiveConfig);
        var scope = new TestGameScope(data.Heroes.Select(x => (x.HeroId, x.TotalExperience)), data.Soul, capacity);
        scope.RestoreCheckpoint(data);
        return scope;
    }
}

/// <summary>
/// 测试替代输入源，不在生产代码添加注入状态或观测接口。
/// </summary>
internal sealed class TestInputSource : IInputSource
{
    /// <summary>
    /// 本帧每个任意动作的事实。
    /// </summary>
    public Dictionary<string, (bool JustPressed, bool IsPressed, bool JustReleased)> Actions { get; } = new();

    /// <inheritdoc />
    public bool IsPressed(string action) => Actions.GetValueOrDefault(action).IsPressed;

    /// <inheritdoc />
    public bool IsJustPressed(string action) => Actions.GetValueOrDefault(action).JustPressed;

    /// <inheritdoc />
    public bool IsJustReleased(string action) => Actions.GetValueOrDefault(action).JustReleased;
}
