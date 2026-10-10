using System;
using GameFramework.Event;
using GameLogic.Event;
using Godot;
using GodotGameFramework;
using GodotGameFramework.NodePool;

namespace GameLogic.UI
{
	/// <summary>
	/// 伤害飘字表现：订阅 <see cref="DamageDealtEventArgs"/>，从 NodePool 取 <see cref="DamagePop"/> 挂到世界层显示。
	/// 命中方与飘字互不认识（跨模块一次性反馈走 GF.Event）；飘字节点池化。
	/// 普通对象，不是单例：由关卡运行（或测试场地）创建并持有，<see cref="Dispose"/> 时退订并回收场上全部飘字。
	/// </summary>
	public sealed class DamagePopPresenter : IDisposable
	{
		/// <summary>飘字场景（池名 = 场景路径，与 NodePoolConfigRes 条目一致）</summary>
		public static readonly string DamagePopScene = "res://TheGame/UIs/DamagePop.tscn";

		private readonly Node m_Layer; // 飘字父节点，必须位于世界坐标系；随所有者的场景一起存在。

		private bool m_Disposed; // 已退订并回收；重复释放直接返回。

		/// <summary>创建表现并立即开始监听命中事件。</summary>
		/// <param name="layer">飘字的父节点（关卡或测试场地的世界节点）。</param>
		/// <exception cref="ArgumentNullException">父节点为空。</exception>
		public DamagePopPresenter(Node layer)
		{
			m_Layer = layer ?? throw new ArgumentNullException(nameof(layer));
			GF.Event.Subscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
		}

		/// <summary>停止监听并回收场上全部飘字；所有者离开作用域时调用，重复调用安全。</summary>
		public void Dispose()
		{
			if (m_Disposed)
			{
				return;
			}

			m_Disposed = true;
			GF.Event.Unsubscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
			NodePool.Instance.ReleaseAll(DamagePopScene);
		}

		/// <summary>校验伤害事件并显示对应的池化飘字。</summary>
		/// <param name="sender">事件源，不使用。</param>
		/// <param name="args">伤害事件参数，只在回调期间读取。</param>
		private void OnDamageDealt(object sender, GameEventArgs args)
		{
			// 忽略类型不符或挂载层已失效的事件（事件下一帧分发，期间场景可能已卸载）。
			if (args is not DamageDealtEventArgs e || !GodotObject.IsInstanceValid(m_Layer))
			{
				return;
			}

			// 未闪避但伤害为 0（被减伤截断）不飘字，同旧项目 reduce_hp 的 value > 0 判断。
			if (!e.IsMiss && e.Damage <= 0)
			{
				return;
			}

			// 取得池化实例后按闪避或伤害结果选择显示内容。
			DamagePop pop = NodePool.Instance.Get<DamagePop>(DamagePopScene, m_Layer);
			if (pop == null)
			{
				return;
			}

			if (e.IsMiss)
			{
				pop.ShowMiss(e.PopPosition);
			}
			else
			{
				pop.ShowDamage(e.PopPosition, e.Damage, e.Kind, e.IsCrit, e.TargetIsHero);
			}
		}
	}
}
