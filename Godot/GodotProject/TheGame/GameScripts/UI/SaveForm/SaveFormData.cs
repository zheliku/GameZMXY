namespace GameLogic.UI;

/// <summary>存档页用途。</summary>
public enum SaveFormMode
{
    /// <summary>读取已有存档；空格不可选，可删除。</summary>
    Load,
    /// <summary>为新阵容选择存档格；已有格需确认覆盖，不提供删除。</summary>
    Save
}

/// <summary>存档页打开参数。</summary>
public sealed class SaveFormData
{
    /// <summary>指定本次存档页的用途。</summary>
    /// <param name="mode">读取已有档案或为新阵容选择保存位置。</param>
    public SaveFormData(SaveFormMode mode)
    {
        Mode = mode;
    }

    /// <summary>本次打开的用途。</summary>
    public SaveFormMode Mode { get; }
}
