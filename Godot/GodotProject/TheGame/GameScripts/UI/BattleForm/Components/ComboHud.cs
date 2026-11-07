using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace GameLogic.UI.Widgets;

/// <summary>
/// 官方连击底图与数字表现；每次命中重新播放五帧数字入场，不持有连击规则或计时。
/// </summary>
public partial class ComboHud : Control
{
    [Export] private SpriteFrames _digitFrames; // OtherMat_v9的num0..9，保留五帧模糊缩放与注册点。
    private readonly List<AnimatedSprite2D> _digits = new(); // 本窗口拥有并复用的数字节点，随窗口释放。

    /// <summary>
    /// 在装配边界确认十个官方数字动画均可用。
    /// </summary>
    /// <exception cref="InvalidOperationException">官方数字资源缺失或帧数错误。</exception>
    public override void _Ready()
    {
        if (_digitFrames == null)
        {
            throw new InvalidOperationException("ComboHud 必须绑定官方数字 SpriteFrames。");
        }
        for (int digit = 0; digit < 10; digit++)
        {
            StringName animation = digit.ToString(CultureInfo.InvariantCulture);
            if (!_digitFrames.HasAnimation(animation) || _digitFrames.GetFrameCount(animation) != 5)
            {
                throw new InvalidOperationException($"ComboHud 缺少官方五帧数字：{digit}");
            }
        }
    }

    /// <summary>
    /// 按官方一、二、三位起点和50px间距显示数字，并重播其入场动画。
    /// </summary>
    /// <param name="count">关卡已累计的有效连击数；调用方从第二次命中开始显示。</param>
    public void SetCount(int count)
    {
        string digits = count.ToString(CultureInfo.InvariantCulture);
        // Batter把Number起点传给ANumber的int参数，原版实际截为4/-44/-95和28。
        int startX = 4;
        if (count >= 100)
        {
            startX = -95;
        }
        else if (count >= 10)
        {
            startX = -44;
        }
        for (int index = 0; index < digits.Length; index++)
        {
            if (index == _digits.Count)
            {
                var node = new AnimatedSprite2D { Name = $"Digit{index}", SpriteFrames = _digitFrames };
                AddChild(node);
                _digits.Add(node);
            }
            AnimatedSprite2D sprite = _digits[index];
            sprite.Position = new Vector2(startX + index * 50, 28);
            sprite.Visible = true;
            sprite.Stop();
            sprite.Play(digits[index].ToString());
        }
        // 连击断开后位数可以变少；隐藏旧节点，避免遗留上一组高位数字。
        for (int index = digits.Length; index < _digits.Count; index++)
        {
            _digits[index].Stop();
            _digits[index].Visible = false;
        }
    }

    /// <summary>
    /// 关闭或回池时停止并隐藏旧数字；窗口复用不沿用旧动画进度。
    /// </summary>
    public void Clear()
    {
        foreach (AnimatedSprite2D digit in _digits)
        {
            digit.Stop();
            digit.Visible = false;
        }
    }
}
