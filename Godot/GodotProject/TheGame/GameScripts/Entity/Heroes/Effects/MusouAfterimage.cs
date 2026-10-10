using System;
using Godot;

namespace GameLogic.Entity.Heroes.Effects;

/// <summary>英雄拥有的无双表现：采样指定精灵层并在原世界位置淡出，不复制动画、判定或玩法节点。</summary>
public partial class MusouAfterimage : Node2D
{
	[Export] private Godot.Collections.Array<Sprite2D> m_Layers = new(); // 场景显式绑定的身体、武器等表现层。
	[Export(PropertyHint.Range, "0.01,1,0.01")] private float m_Interval = 0.1f; // 旧项目残影采样间隔秒数。
	[Export(PropertyHint.Range, "0.01,1,0.01")] private float m_Lifetime = 0.27f; // 旧项目每帧残影存活秒数。
	[Export] private Color m_Tint = new(1f, 0.85098f, 0.827451f, 0.8f); // 旧项目暖色残影与初始透明度。
	private const int SnapshotLimit = 32; // 表现缓存的工程上限，避免错误检查器参数分配过量节点。
	private Sprite2D[][] m_Snapshots = []; // 固定复用的各帧精灵层，不逐帧创建或释放节点。
	private float[] m_Ages = []; // 各帧已存活秒数；达到寿命后隐藏。
	private int m_Next; // 下一次写入的循环槽位。
	private float m_SampleTime; // 距离上次采样的累计秒数。
	private bool m_Emitting; // 是否继续采样；结束后仅让已有残影自然淡出。

	/// <summary>创建固定表现缓存，顶层变换保证残影留在采样位置。</summary>
	public override void _Ready()
	{
		TopLevel = true;
		GlobalTransform = Transform2D.Identity;
		ZIndex = -1;
		int count = Math.Clamp((int)MathF.Ceiling(m_Lifetime / MathF.Max(0.01f, m_Interval)) + 1, 1, SnapshotLimit);
		m_Snapshots = new Sprite2D[count][];
		m_Ages = new float[count];
		for (int i = 0; i < count; i++)
		{
			m_Snapshots[i] = new Sprite2D[m_Layers.Count];
			for (int j = 0; j < m_Layers.Count; j++)
			{
				Sprite2D sprite = new() { Visible = false };
				AddChild(sprite);
				m_Snapshots[i][j] = sprite;
			}
		}
		Reset();
	}

	/// <summary>开启或结束采样；结束时已有残影自然淡出。</summary>
	/// <param name="emitting">是否处于无双状态。</param>
	public void SetEmitting(bool emitting)
	{
		if (m_Emitting == emitting) return;
		m_Emitting = emitting;
		m_SampleTime = m_Interval;
		if (emitting) SetProcess(true);
	}

	/// <summary>隐藏全部残影并停止采样（实体隐藏或池复用）。</summary>
	public void Reset()
	{
		m_Emitting = false;
		m_Next = 0;
		m_SampleTime = 0f;
		Array.Fill(m_Ages, m_Lifetime);
		foreach (Sprite2D[] snapshot in m_Snapshots)
			foreach (Sprite2D layer in snapshot) layer.Visible = false;
		SetProcess(false);
	}

	/// <summary>淡出已有残影，并按间隔截取动画的当前身体和武器帧。</summary>
	/// <param name="delta">本次渲染步长秒数。</param>
	public override void _Process(double delta)
	{
		bool visible = false;
		for (int i = 0; i < m_Snapshots.Length; i++)
		{
			m_Ages[i] += (float)delta;
			float alpha = Mathf.Clamp(1f - m_Ages[i] / m_Lifetime, 0f, 1f);
			foreach (Sprite2D layer in m_Snapshots[i])
			{
				if (alpha <= 0f) layer.Visible = false;
				layer.Modulate = new Color(m_Tint.R, m_Tint.G, m_Tint.B, m_Tint.A * alpha);
				visible |= layer.Visible;
			}
		}

		if (m_Emitting)
		{
			m_SampleTime += (float)delta;
			if (m_SampleTime >= m_Interval)
			{
				m_SampleTime %= m_Interval;
				Capture();
			}
		}
		else if (!visible) SetProcess(false);
	}

	/// <summary>只复制显示数据；每个槽位保留独立的世界变换和朝向。</summary>
	private void Capture()
	{
		for (int j = 0; j < m_Layers.Count; j++)
		{
			Sprite2D source = m_Layers[j];
			Sprite2D target = m_Snapshots[m_Next][j];
			target.Visible = IsInstanceValid(source) && source.IsVisibleInTree();
			if (!target.Visible) continue;
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
			target.Modulate = m_Tint;
		}
		m_Ages[m_Next] = 0f;
		m_Next = (m_Next + 1) % m_Snapshots.Length;
	}
}
