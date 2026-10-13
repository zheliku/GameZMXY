using System;
using Godot;
using GodotGameFramework.NodePool;

namespace GameLogic.UI;

/// <summary>恢复量位图飘字；按官方 40 毫秒上移 3 像素、25 次的节奏以秒推进。</summary>
public partial class RecoveryPopNode : Node2D, IPoolable
{
    private const float Lifetime = 1f; // 官方 25 × 40ms，不依赖渲染帧率。
    private const float RiseSpeed = 75f; // 官方每 40ms 上移 3px。
    private const int DigitSpacing = 20; // 官方数字横向间隔。
    [Export] private Texture2D m_Health; // 绿色恢复生命数字。
    [Export] private Texture2D m_Mana; // 蓝色恢复魔法数字。
    private readonly Sprite2D[] m_Digits = new Sprite2D[10]; // 覆盖 int 正数的全部数位，池内复用。
    private Action<RecoveryPopNode> m_Finished; // 显示结束由本关所有者归还。
    private float m_Age; // 本次已经显示的游戏秒数。
    private Vector2 m_Start; // 世界锚点，不跟随角色移动。

    /// <summary>首次取出时建立数字节点；复用时只复位计时。</summary>
    public void OnGet()
    {
        for (int i = 0; i < m_Digits.Length; i++)
        {
            if (m_Digits[i] != null) continue;
            m_Digits[i] = new Sprite2D { RegionEnabled = true, Centered = false };
            AddChild(m_Digits[i]);
        }
        m_Age = 0;
        SetProcess(false);
    }

    /// <summary>归还时移除回调和显示，避免旧周期回调影响新关卡。</summary>
    public void OnRelease()
    {
        SetProcess(false);
        m_Finished = null;
        foreach (var digit in m_Digits) if (digit != null) digit.Visible = false;
    }

    /// <summary>显示拾取配置的恢复量，保留原版在恢复上限处仍显示配置量的行为。</summary>
    /// <param name="position">英雄世界原点。</param>
    /// <param name="amount">正数恢复量。</param>
    /// <param name="mana">是否恢复魔法。</param>
    /// <param name="finished">本关所有者的回收方法。</param>
    public void Show(Vector2 position, int amount, bool mana, Action<RecoveryPopNode> finished)
    {
        m_Start = position + new Vector2(-20, -60);
        GlobalPosition = m_Start;
        m_Finished = finished;
        Texture2D sheet = mana ? m_Mana : m_Health;
        string text = amount.ToString();
        int width = sheet.GetWidth() / 10;
        for (int i = 0; i < m_Digits.Length; i++)
        {
            var digit = m_Digits[i];
            digit.Visible = i < text.Length;
            if (!digit.Visible) continue;
            digit.Texture = sheet;
            digit.RegionRect = new Rect2((text[i] - '0') * width, 0, width, sheet.GetHeight());
            digit.Position = new Vector2(i * DigitSpacing, 0);
        }
        SetProcess(true);
    }

    /// <summary>按游戏时间匀速上升；暂停时 delta 为零，结束后只通知一次。</summary>
    /// <param name="delta">本帧游戏秒数。</param>
    public override void _Process(double delta)
    {
        m_Age += (float)delta;
        GlobalPosition = m_Start + Vector2.Up * (RiseSpeed * Math.Min(m_Age, Lifetime));
        if (m_Age < Lifetime) return;
        SetProcess(false);
        var finished = m_Finished;
        m_Finished = null;
        finished?.Invoke(this);
    }
}
