using System;

namespace GameLogic;

/// <summary>提供设置提示内容及确认、取消回调的界面接口。</summary>
public interface ITips
{
    /// <summary>设置提示内容、按钮文本和对应回调。</summary>
    /// <param name="content">提示正文。</param>
    /// <param name="cancelTxt">取消按钮文本。</param>
    /// <param name="confirmTxt">确认按钮文本。</param>
    /// <param name="cancel">取消回调。</param>
    /// <param name="confirm">确认回调。</param>
    void SetAction(string content, string cancelTxt, string confirmTxt, Action cancel, Action confirm);
}
