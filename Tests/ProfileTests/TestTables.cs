using System;
using System.IO;
using GameConfig;
using GameLogic.Architecture;
using GameLogic.Config;
using Luban;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 读取与游戏相同的 Luban 产物（测试输出目录 Data/），只加载被访问到的表。
/// </summary>
internal static class TestTables
{
    /// <summary>
    /// 全部配置表（懒加载，与游戏 ConfigSystem 同一读取方式）。
    /// </summary>
    public static readonly Tables Tables = new(name =>
        new ByteBuf(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", name + ".bytes"))));
    /// <summary>
    /// 正式配置的共享查询索引。
    /// </summary>
    public static readonly ConfigurationModel Configuration = new(Tables);
    /// <summary>
    /// 与配置模型共享的经验曲线。
    /// </summary>
    public static readonly ExperienceCurveUtility Curve = new(Tables.TbHeroLevelConfig.DataList);
    /// <summary>
    /// 与游戏使用同一实现的配置编号工具。
    /// </summary>
    public static readonly ConfigIdUtility ConfigIds = new();
    /// <summary>
    /// 纯 C# 测试使用明确的配置来源，不启动全局游戏或 Godot 节点。
    /// </summary>
    public static readonly IArchitecture Architecture = new TestArchitecture();

    /// <summary>
    /// 纯规则测试也执行模型归属与初始化。
    /// </summary>
    /// <param name="model">受测模型。</param>
    public static void Initialize(IModel model)
    {
        model.SetArchitecture(new TestArchitecture(model as ProfileModel));
        model.Init();
    }

    /// <summary>
    /// 为独立档案提供正式档案系统，避免在夹具中重复业务规则。
    /// </summary>
    /// <param name="profile">本测试拥有的档案。</param>
    /// <returns>绑定该档案的正式业务系统。</returns>
    internal static ProfileSystem ForProfile(ProfileModel profile) => new(new TestArchitecture(profile));

    /// <summary>
    /// 为一个测试档案建立独立模型作用域。
    /// </summary>
    /// <param name="playerModel">借用的测试档案作用域；允许为空的方法会新建作用域。</param>
    /// <returns>持有指定档案模型的同一架构引用。</returns>
    public static IArchitecture ForPlayer(TestGameScope playerModel) => playerModel;

    /// <summary>
    /// 按正式注册顺序装配独立配置作用域，并执行配置系统的初始化校验。
    /// </summary>
    /// <param name="tables">本次校验拥有的配置总表，失败注入不影响其他用例。</param>
    /// <returns>已完成配置系统初始化的测试作用域。</returns>
    internal static IArchitecture CreateConfigurationScope(Tables tables)
    {
        var scope = new TestArchitecture(tables: tables);
        ((ICanInit)scope.GetModel<ConfigurationModel>()).Init();
        ((ICanInit)scope.GetSystem<ConfigurationSystem>()).Init();
        return scope;
    }

    /// <summary>
    /// 提供测试装配的配置和档案；未注册的类型返回 null。
    /// </summary>
    private sealed class TestArchitecture : IArchitecture
    {
        private readonly ProfileModel _profile; // 本测试拥有的独立档案。
        private readonly ConfigurationModel _configuration; // 本作用域登记的配置模型。
        private readonly ExperienceCurveUtility _curve; // 先于配置系统装配并验证经验表的共享工具。
        private readonly ConfigIdUtility _configIds; // 本作用域登记的配置编号工具。
        private readonly ConfigurationSystem _configurationSystem; // 与游戏相同的配置业务入口。

        /// <summary>
        /// 建立测试模型来源。
        /// </summary>
        /// <param name="profile">独立档案，可为空。</param>
        /// <param name="tables">独立表校验时使用指定配置，其他规则测试借用正式只读表。</param>
        internal TestArchitecture(ProfileModel profile = null, Tables tables = null)
        {
            _profile = profile;
            _configuration = tables == null ? Configuration : new ConfigurationModel(tables);
            _curve = tables == null ? Curve : new ExperienceCurveUtility(tables.TbHeroLevelConfig.DataList);
            _configIds = tables == null ? ConfigIds : new ConfigIdUtility();
            _configurationSystem = new ConfigurationSystem(this);
        }

        /// <inheritdoc />
        public TModel GetModel<TModel>() where TModel : class, IModel => typeof(TModel) == typeof(ConfigurationModel) ? (TModel)(object)_configuration : _profile as TModel;

        /// <inheritdoc />
        public TSystem GetSystem<TSystem>() where TSystem : class, ISystem => _configurationSystem as TSystem;

        /// <inheritdoc />
        public TUtility GetUtility<TUtility>() where TUtility : class, IUtility => (typeof(TUtility) == typeof(ConfigIdUtility) ? (IUtility)_configIds : _curve) as TUtility;
    }
}
