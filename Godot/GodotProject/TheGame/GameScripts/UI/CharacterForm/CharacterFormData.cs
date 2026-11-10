using System;

namespace GameLogic.UI;

/// <summary>选角窗口的打开参数：本次需要选择的参战人数。</summary>
public sealed class CharacterFormData
{
    /// <summary>指定本次需要依次确认的英雄数量。</summary>
    /// <param name="playerCount">参战人数，只能为 1 或 2。</param>
    /// <exception cref="ArgumentOutOfRangeException">人数不是 1 或 2。</exception>
    public CharacterFormData(int playerCount)
    {
        if (playerCount is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(playerCount));
        }
        PlayerCount = playerCount;
    }

    /// <summary>本次需要依次确认的英雄数量。</summary>
    public int PlayerCount { get; }
}
