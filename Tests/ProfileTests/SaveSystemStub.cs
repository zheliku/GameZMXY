using System.Threading.Tasks;
using GameLogic.Architecture;
using GameLogic.Profile;

namespace GameLogic.Save;

/// <summary>纯规则测试只隔离磁盘；正式存档系统由引擎烟测覆盖。</summary>
internal sealed class SaveSystem : AbstractSystem
{
    /// <summary>已请求的检查点数量。</summary>
    internal int CheckpointCount { get; private set; }

    /// <summary>绑定测试作用域。</summary>
    /// <param name="architecture">测试作用域。</param>
    internal SaveSystem(IArchitecture architecture) : base(architecture) { }

    /// <summary>记录检查点请求，不复制正式存档映射或关卡规则。</summary>
    /// <param name="profile">请求保存的档案。</param>
    /// <returns>已完成的成功任务。</returns>
    internal Task<bool> SaveCheckpointAsync(ProfileModel profile)
    {
        CheckpointCount++;
        return Task.FromResult(true);
    }
}
