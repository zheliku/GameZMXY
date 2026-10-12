using System;
using GameLogic.Profile;
using GameLogic.Save;

namespace GameLogic;

/// <summary>一个已加载存档槽的会话：将玩家档案与对应的存档服务一起传给流程。</summary>
public sealed class GameContext
{
    /// <summary>本槽的持久状态，读档到返回标题期间保持同一实例。</summary>
    public PlayerProfile Profile { get; }

    /// <summary>绑定本槽编号的存档服务。</summary>
    public SaveModule Save { get; }

    /// <summary>创建已加载的玩家会话。</summary>
    /// <param name="profile">本槽档案。</param>
    /// <param name="save">本槽存档服务。</param>
    /// <exception cref="ArgumentNullException">任一参数为空。</exception>
    public GameContext(PlayerProfile profile, SaveModule save)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Save = save ?? throw new ArgumentNullException(nameof(save));
    }
}
