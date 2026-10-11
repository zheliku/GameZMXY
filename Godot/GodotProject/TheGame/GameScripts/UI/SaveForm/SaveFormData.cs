using GameLogic.Save.Slots;
using System;
using GameLogic.Save;

namespace GameLogic.UI;

/// <summary>存档窗口参数，只引用标题流程拥有的目录状态源。</summary>
public sealed class SaveFormData
{
    /// <summary>当前目录状态源。</summary>
    public SaveSlotDirectory Directory { get; }

    /// <summary>绑定当前目录。</summary>
    /// <param name="directory">标题周期内的目录状态源。</param>
    /// <exception cref="ArgumentNullException">目录为空。</exception>
    public SaveFormData(SaveSlotDirectory directory) => Directory = directory ?? throw new ArgumentNullException(nameof(directory));
}
