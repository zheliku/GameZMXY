using GameLogic.Config;
using GameLogic.Architecture;
using System;
using System.IO;
using GameConfig;
using Luban;

namespace GameLogic.Profile.Tests
{
    /// <summary>读取与游戏相同的 Luban 产物（测试输出目录 Data/），只加载被访问到的表。</summary>
    internal static class TestTables
    {
        /// <summary>全部配置表（懒加载，与游戏 ConfigSystem 同一读取方式）。</summary>
        public static readonly Tables Tables = new(name =>
            new ByteBuf(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", name + ".bytes"))));

        /// <summary>经过启动校验的经验曲线。</summary>
        public static readonly ConfigurationModel Configuration = new(Tables);

        /// <summary>与配置模型共享的经验曲线。</summary>
        public static readonly ExperienceCurve Curve = new(Tables.TbHeroLevelConfig.DataList);

        /// <summary>纯 C# 测试使用明确的配置来源，不启动全局游戏或 Godot 节点。</summary>
        public static readonly IArchitecture Architecture = new TestArchitecture();

        /// <summary>纯规则测试也执行模型归属与初始化。</summary>
        /// <param name="model">受测模型。</param>
        public static void Initialize(IModel model)
        {
            model.SetArchitecture(new TestArchitecture(model as ProfileModel));
            model.Init();
        }

        /// <summary>为独立档案提供正式档案系统，避免在夹具中重复业务规则。</summary>
        /// <param name="profile">本测试拥有的档案。</param>
        /// <returns>绑定该档案的正式业务系统。</returns>
        internal static ProfileSystem ForProfile(ProfileModel profile) => new(new TestArchitecture(profile));

        /// <summary>为一个测试档案建立独立模型作用域。</summary>
        public static IArchitecture ForPlayer(TestGameScope playerModel) => playerModel;

        /// <summary>提供测试装配的配置和档案；未注册的类型返回 null。</summary>
        private sealed class TestArchitecture : IArchitecture
        {
            private readonly ProfileModel m_Profile; // 本测试拥有的独立档案。

            /// <summary>建立测试模型来源。</summary>
            /// <param name="profile">独立档案，可为空。</param>
            internal TestArchitecture(ProfileModel profile = null) => m_Profile = profile;

            /// <inheritdoc />
            public TModel GetModel<TModel>() where TModel : class, IModel =>
                typeof(TModel) == typeof(ConfigurationModel) ? (TModel)(object)Configuration
                : m_Profile as TModel;

            /// <inheritdoc />
            public TSystem GetSystem<TSystem>() where TSystem : class, ISystem => null;

            /// <inheritdoc />
            public TUtility GetUtility<TUtility>() where TUtility : class, IUtility => Curve as TUtility;
        }
    }
}
