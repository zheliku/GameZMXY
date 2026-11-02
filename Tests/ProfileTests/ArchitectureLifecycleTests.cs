using System;
using GameLogic.Architecture;
using Xunit;

namespace GameLogic.Profile.Tests
{
    /// <summary>
    /// 验证业务架构成员的初始化失败和退出失败仍保持可回收的生命周期状态。
    /// </summary>
    public sealed class ArchitectureLifecycleTests
    {
        /// <summary>
        /// Model 初始化失败时执行一次对称清理，并保持未初始化。
        /// </summary>
        [Fact]
        public void ModelInitFailure_CleansPartialState()
        {
            var model = new ProbeModel(throwOnInit: true, throwOnDeinit: false);
            ((ICanSetArchitecture)model).SetArchitecture(new EmptyArchitecture());
            var lifecycle = (ICanInit)model;

            Assert.Throws<InvalidOperationException>(lifecycle.Init);
            Assert.False(lifecycle.Initialized);
            Assert.Equal(1, model.InitCount);
            Assert.Equal(1, model.DeinitCount);
        }

        /// <summary>
        /// System 初始化失败时执行一次对称清理，并保持未初始化。
        /// </summary>
        [Fact]
        public void SystemInitFailure_CleansPartialState()
        {
            var system = new ProbeSystem(new EmptyArchitecture(), throwOnInit: true, throwOnDeinit: false);
            var lifecycle = (ICanInit)system;

            Assert.Throws<InvalidOperationException>(lifecycle.Init);
            Assert.False(lifecycle.Initialized);
            Assert.Equal(1, system.InitCount);
            Assert.Equal(1, system.DeinitCount);
        }

        /// <summary>
        /// 退出钩子失败也不会把成员留在已初始化状态。
        /// </summary>
        [Fact]
        public void DeinitFailure_StillMarksMemberInactive()
        {
            var model = new ProbeModel(throwOnInit: false, throwOnDeinit: true);
            ((ICanSetArchitecture)model).SetArchitecture(new EmptyArchitecture());
            var lifecycle = (ICanInit)model;
            lifecycle.Init();

            Assert.Throws<InvalidOperationException>(lifecycle.Deinit);
            Assert.False(lifecycle.Initialized);
        }

        /// <summary>
        /// 只提供契约所需的空架构；不创建第二个注册容器。
        /// </summary>
        private sealed class EmptyArchitecture : IArchitecture
        {
            /// <inheritdoc />
            public TModel GetModel<TModel>() where TModel : class, IModel { return null; }

            /// <inheritdoc />
            public TSystem GetSystem<TSystem>() where TSystem : class, ISystem { return null; }

            /// <inheritdoc />
            public TUtility GetUtility<TUtility>() where TUtility : class, IUtility { return null; }
        }

        /// <summary>
        /// 可控的模型生命周期探针，仅存在于测试程序集。
        /// </summary>
        private sealed class ProbeModel(bool throwOnInit, bool throwOnDeinit) : AbstractModel
        {
            /// <summary>
            /// 初始化钩子实际执行次数。
            /// </summary>
            public int InitCount { get; private set; }

            /// <summary>
            /// 清理钩子实际执行次数。
            /// </summary>
            public int DeinitCount { get; private set; }

            /// <inheritdoc />
            protected override void OnInit()
            {
                InitCount++;
                if (throwOnInit)
                {
                    throw new InvalidOperationException("probe init");
                }
            }

            /// <inheritdoc />
            protected override void OnDeinit()
            {
                DeinitCount++;
                if (throwOnDeinit)
                {
                    throw new InvalidOperationException("probe deinit");
                }
            }
        }

        /// <summary>
        /// 可控的系统生命周期探针，仅存在于测试程序集。
        /// </summary>
        private sealed class ProbeSystem(IArchitecture architecture, bool throwOnInit, bool throwOnDeinit)
            : AbstractSystem(architecture)
        {
            /// <summary>
            /// 初始化钩子实际执行次数。
            /// </summary>
            public int InitCount { get; private set; }

            /// <summary>
            /// 清理钩子实际执行次数。
            /// </summary>
            public int DeinitCount { get; private set; }

            /// <inheritdoc />
            protected override void OnInit()
            {
                InitCount++;
                if (throwOnInit)
                {
                    throw new InvalidOperationException("probe init");
                }
            }

            /// <inheritdoc />
            protected override void OnDeinit()
            {
                DeinitCount++;
                if (throwOnDeinit)
                {
                    throw new InvalidOperationException("probe deinit");
                }
            }
        }
    }
}
