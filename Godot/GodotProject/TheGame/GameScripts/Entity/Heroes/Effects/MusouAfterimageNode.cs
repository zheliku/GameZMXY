using System;
using Godot;

namespace GameLogic.Heroes.Effects;

/// <summary>
/// 英雄拥有的无双表现：采样指定精灵层并在原世界位置淡出，不复制动画、判定或玩法节点。
/// </summary>
public partial class MusouAfterimageNode : Node2D
{
    [Export] private Godot.Collections.Array<Sprite2D> _layers = new(); // 场景显式绑定的身体、武器等表现层。

    [Export(PropertyHint.Range, "0.01,1,0.01")]
    private float _interval = 1f / 6f; // 官方残影采样周期，秒。

    [Export(PropertyHint.Range, "0.01,2,0.01")]
    private float _lifetime = 1f; // 官方 TweenMax 残影淡出秒数。

    [Export] private Color _tint = Colors.White; // 保留角色原色与初始透明度。
    private const int SnapshotLimit = 32; // 表现缓存的工程上限，避免错误检查器参数分配过量节点。
    private Sprite2D[][] _snapshots = []; // 固定复用的各帧精灵层，不逐帧创建或释放节点。
    private float[] _ages = []; // 各帧已存活秒数；达到寿命后隐藏。
    private int _next; // 下一次写入的循环槽位。
    private float _sampleTime; // 距离上次采样的累计秒数。
    private bool _emitting; // 是否继续采样；结束后仅让已有残影自然淡出。

    /// <summary>
    /// 创建固定表现缓存，顶层变换保证残影留在采样位置。
    /// </summary>
    public override void _Ready()
    {
        TopLevel = true;
        GlobalTransform = Transform2D.Identity;
        ZIndex = -1;
        int count = Math.Clamp((int)MathF.Ceiling(_lifetime / MathF.Max(0.01f, _interval)) + 1, 1, SnapshotLimit);
        _snapshots = new Sprite2D[count][];
        _ages = new float[count];
        for (int i = 0; i < count; i++)
        {
            _snapshots[i] = new Sprite2D[_layers.Count];
            for (int j = 0; j < _layers.Count; j++)
            {
                Sprite2D sprite = new()
                {
                    Visible = false
                };
                AddChild(sprite);
                _snapshots[i][j] = sprite;
            }
        }

        Reset();
    }

    /// <summary>
    /// 淡出已有残影，并按间隔截取动画的当前身体和武器帧。
    /// </summary>
    /// <param name="delta">本次渲染步长秒数。</param>
    public override void _Process(double delta)
    {
        bool visible = false;
        for (int i = 0; i < _snapshots.Length; i++)
        {
            _ages[i] += (float)delta;
            float alpha = Mathf.Clamp(1f - _ages[i] / _lifetime, 0f, 1f);
            foreach (Sprite2D layer in _snapshots[i])
            {
                if (alpha <= 0f)
                {
                    layer.Visible = false;
                }
                // 官方 TweenMax 默认 Quad.easeOut：透明度由 1 降至 0。
                layer.Modulate = new Color(_tint.R, _tint.G, _tint.B, _tint.A * alpha * alpha);
                visible |= layer.Visible;
            }
        }

        if (_emitting)
        {
            _sampleTime += (float)delta;
            if (_sampleTime >= _interval)
            {
                _sampleTime %= _interval;
                Capture();
            }
        }
        else if (!visible)
        {
            SetProcess(false);
        }
    }

    /// <summary>
    /// 开启或结束采样；结束时已有残影自然淡出。
    /// </summary>
    /// <param name="emitting">是否处于无双状态。</param>
    public void SetEmitting(bool emitting)
    {
        if (_emitting == emitting)
        {
            return;
        }

        _emitting = emitting;
        _sampleTime = _interval;
        if (emitting)
        {
            SetProcess(true);
        }
    }

    /// <summary>
    /// 隐藏全部残影并停止采样（实体隐藏或池复用）。
    /// </summary>
    public void Reset()
    {
        _emitting = false;
        _next = 0;
        _sampleTime = 0f;
        Array.Fill(_ages, _lifetime);
        foreach (Sprite2D[] snapshot in _snapshots)
        {
            foreach (Sprite2D layer in snapshot)
            {
                layer.Visible = false;
            }
        }

        SetProcess(false);
    }

    /// <summary>
    /// 只复制显示数据；每个槽位保留独立的世界变换和朝向。
    /// </summary>
    private void Capture()
    {
        for (int j = 0; j < _layers.Count; j++)
        {
            Sprite2D source = _layers[j];
            Sprite2D target = _snapshots[_next][j];
            target.Visible = IsInstanceValid(source) && source.IsVisibleInTree();
            if (!target.Visible)
            {
                continue;
            }

            target.Texture = source.Texture;
            target.Hframes = source.Hframes;
            target.Vframes = source.Vframes;
            target.Frame = source.Frame;
            target.Centered = source.Centered;
            target.Offset = source.Offset;
            target.FlipH = source.FlipH;
            target.FlipV = source.FlipV;
            target.RegionEnabled = source.RegionEnabled;
            target.RegionRect = source.RegionRect;
            target.GlobalTransform = source.GlobalTransform;
            target.SelfModulate = source.SelfModulate;
            target.Modulate = _tint;
        }

        _ages[_next] = 0f;
        _next = (_next + 1) % _snapshots.Length;
    }
}
