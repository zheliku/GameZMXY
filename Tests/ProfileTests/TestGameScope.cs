using GameLogic.Battle;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Save;
using GameLogic.Level;
using GameLogic.Equipment;
using GameLogic.Skills;
using GameLogic.Input;

namespace GameLogic.Profile.Tests;

/// <summary>测试专用作用域；正式代码的档案和本局英雄模型分别持有与初始化。</summary>
internal sealed class TestGameScope : IArchitecture
{
    private readonly LevelSystem m_LevelSystem; // 正式关卡业务规则。
    private readonly EquipmentSystem m_EquipmentSystem; // 正式装备业务规则。
    private readonly SaveSystem m_SaveSystem; // 仅隔离磁盘写入。
    private readonly ProfileSystem m_ProfileSystem; // 档案数据的业务协调。
    private readonly HeroSystem m_HeroSystem; // 与游戏使用同一来源协调逻辑。
    private readonly SkillSystem m_SkillSystem; // 正式技能购买规则。
    private readonly InputSystem m_InputSystem; // 正式输入组合与方向规则。
    private readonly SaveDataUtility m_Save = new(); // 与游戏注册的相同纯转换工具。

    /// <summary>测试提供的任意动作事实，走正式输入采样与消费。</summary>
    public TestInputSource Input { get; } = new();

    /// <summary>档案的唯一权威数据。</summary>
    public ProfileModel Profile { get; }

    /// <summary>当前档案独占的技能学习记录。</summary>
    public SkillModel Skills { get; }

    /// <summary>本局英雄的全部数值。</summary>
    public HeroModel Hero { get; } = new();

    /// <summary>本局模型，由正式关卡系统修改。</summary>
    public LevelModel Level { get; } = new();

    /// <summary>建立独立测试作用域，不复制正式规则。</summary>
    /// <param name="activeHeroId">出战身份。</param>
    /// <param name="heroes">累计经验。</param>
    /// <param name="soul">灵魂余额。</param>
    /// <param name="capacity">背包容量。</param>
    /// <param name="initializeHero">是否装配已有可玩英雄数值；未导入角色的技能机制测试自行提供本局蓝量。</param>
    public TestGameScope(int activeHeroId, IEnumerable<(int HeroId, int TotalExperience)> heroes,
        int soul, int capacity, bool initializeHero = true)
    {
        Profile = new(activeHeroId, heroes, TestTables.Curve, capacity, soul);
        Skills = new(Profile.HeroIds);
        foreach (IModel model in new IModel[] { Profile, Skills, Hero, Level })
        {
            model.SetArchitecture(this);
            model.Init();
        }
        m_ProfileSystem = new ProfileSystem(this);
        ((ICanInit)m_ProfileSystem).Init();
        m_HeroSystem = new HeroSystem(this);
        if (initializeHero)
            ((ICanInit)m_HeroSystem).Init();
        m_EquipmentSystem = new EquipmentSystem(this);
        m_SkillSystem = new SkillSystem(this);
        m_InputSystem = new InputSystem(this, Input);
        m_SaveSystem = new SaveSystem(this);
        m_LevelSystem = new LevelSystem(this);
        if (activeHeroId == 1)
            m_SkillSystem.SelectSkills(1, GameLogic.SkillTests.SkillTestDefinitions.Wukong());
    }

    /// <inheritdoc />
    public TModel GetModel<TModel>() where TModel : class, IModel =>
        (typeof(TModel) == typeof(ConfigurationModel) ? TestTables.Configuration :
            typeof(TModel) == typeof(ProfileModel) ? (IModel)Profile :
            typeof(TModel) == typeof(SkillModel) ? Skills :
            typeof(TModel) == typeof(HeroModel) ? Hero :
            typeof(TModel) == typeof(LevelModel) ? Level : null) as TModel;
    /// <inheritdoc />
    public TSystem GetSystem<TSystem>() where TSystem : class, ISystem =>
        (typeof(TSystem) == typeof(ProfileSystem) ? (ISystem)m_ProfileSystem :
            typeof(TSystem) == typeof(HeroSystem) ? m_HeroSystem :
            typeof(TSystem) == typeof(LevelSystem) ? m_LevelSystem :
            typeof(TSystem) == typeof(EquipmentSystem) ? m_EquipmentSystem :
            typeof(TSystem) == typeof(SkillSystem) ? m_SkillSystem :
            typeof(TSystem) == typeof(InputSystem) ? m_InputSystem :
            typeof(TSystem) == typeof(SaveSystem) ? m_SaveSystem : null) as TSystem;
    /// <inheritdoc />
    public TUtility GetUtility<TUtility>() where TUtility : class, IUtility => (typeof(TUtility) == typeof(ExperienceCurve) ? (IUtility)TestTables.Curve : m_Save) as TUtility;

    /// <summary>调用正式转换。</summary>
    /// <returns>独立快照。</returns>
    public GameData CaptureSaveData() => m_Save.Capture(Profile, Skills);

    /// <summary>调用正式回滚。</summary>
    /// <param name="data">进关快照。</param>
    public void RestoreCheckpoint(GameData data) => m_ProfileSystem.RestoreCheckpoint(data);

    /// <summary>验证后建立测试作用域，映射算法使用正式工具。</summary>
    /// <param name="data">当前格式数据。</param>
    /// <param name="curve">经验规则。</param>
    /// <param name="definitions">装备定义。</param>
    /// <param name="capacity">容量。</param>
    /// <returns>独立测试作用域。</returns>
    public static TestGameScope FromSaveData(GameData data, ExperienceCurve curve,
        GameConfig.Equipment.TbEquipmentConfig definitions, int capacity)
    {
        var utility = new SaveDataUtility();
        utility.Validate(data, definitions, capacity, TestTables.Tables.TbSkillConfig);
        var scope = new TestGameScope(data.ActiveHeroId,
            data.Heroes.Select(x => (x.HeroId, x.TotalExperience)), data.Soul, capacity);
        scope.RestoreCheckpoint(data);
        return scope;
    }
}

/// <summary>测试替代输入源，不在生产代码添加注入状态或观测接口。</summary>
internal sealed class TestInputSource : IInputSource
{
    /// <summary>本帧每个任意动作的事实。</summary>
    public Dictionary<string, (bool JustPressed, bool IsPressed, bool JustReleased)> Actions { get; } = new();
    /// <inheritdoc />
    public bool IsPressed(string action) => Actions.GetValueOrDefault(action).IsPressed;
    /// <inheritdoc />
    public bool IsJustPressed(string action) => Actions.GetValueOrDefault(action).JustPressed;
    /// <inheritdoc />
    public bool IsJustReleased(string action) => Actions.GetValueOrDefault(action).JustReleased;
}
