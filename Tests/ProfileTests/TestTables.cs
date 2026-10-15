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
        public static readonly ExperienceCurve Curve = Configuration.ExperienceCurve;

        /// <summary>纯 C# 测试使用明确的配置来源，不启动全局游戏或 Godot 节点。</summary>
        public static readonly IArchitecture Architecture = new TestArchitecture();

        /// <summary>为一个测试档案建立独立模型作用域。</summary>
        public static IArchitecture ForPlayer(PlayerModel playerModel) => new TestArchitecture(playerModel);

        /// <summary>提供测试装配的配置和档案；未注册的类型返回 null。</summary>
        private sealed class TestArchitecture(PlayerModel playerModel = null) : IArchitecture
        {
            /// <inheritdoc />
            public TModel GetModel<TModel>() where TModel : class, IModel =>
                typeof(TModel) == typeof(ConfigurationModel) ? (TModel)(object)Configuration
                : typeof(TModel) == typeof(PlayerModel) && playerModel != null ? (TModel)(object)playerModel
                : null;

            /// <inheritdoc />
            public TSystem GetSystem<TSystem>() where TSystem : class, ISystem => null;

            /// <inheritdoc />
            public TUtility GetUtility<TUtility>() where TUtility : class, IUtility => null;
        }
    }
}
