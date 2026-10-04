using GameFramework.Event;
using GameLogic.Event;
using Godot;
using GodotGameFramework;
using GodotGameFramework.NodePool;
using GodotGameFrameworkCore.SingletonSystem;

namespace GameLogic.Manager
{
	/// <summary>
	/// 伤害飘字管理：订阅 <see cref="DamageDealtEventArgs"/>，从 NodePool 取 <see cref="UI.DamagePop"/> 显示。
	/// 命中方与飘字互不认识（根规范 §9 跨模块只走事件）；飘字节点池化（红线 6）。
	///
	/// 生命周期：流程（ProcedureGame）进入时 <see cref="Activate"/>、离开时 <see cref="Deactivate"/>；
	/// 飘字挂在 <see cref="SetLayer"/> 指定的节点下（关卡/调试场地），随场景销毁前由 NodePool.ReleaseAll 回收。
	/// </summary>
	public partial class DamagePopManager : SingletonNode<DamagePopManager>
	{
		/// <summary>飘字场景（池名 = 场景路径，与 NodePoolConfigRes 条目一致）</summary>
		public static readonly string DamagePopScene = "res://TheGame/UIs/DamagePop.tscn";

		/// <summary>飘字父节点（世界坐标系的场景节点）</summary>
		private Node m_Layer;

		/// <summary>是否已订阅伤害事件。</summary>
		private bool m_Subscribed;

		/// <summary>开始监听命中事件。</summary>
		/// <summary>设置飘字挂载层并订阅伤害事件。</summary>
		/// <param name="layer">飘字的父节点。</param>
		public void Activate(Node layer)
		{
			SetLayer(layer);
			if (m_Subscribed)
			{
				return;
			}

			GF.Event.Subscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
			m_Subscribed = true;
		}

		/// <summary>停止监听并回收场上全部飘字。</summary>
		public void Deactivate()
		{
			if (m_Subscribed)
			{
				GF.Event.Unsubscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
				m_Subscribed = false;
			}

			NodePool.Instance.ReleaseAll(DamagePopScene);
			m_Layer = null;
		}

		/// <summary>切换飘字父节点（切关时由流程设置）。</summary>
		public void SetLayer(Node layer)
		{
			m_Layer = layer;
		}

		/// <summary>释放管理器时解除事件订阅。</summary>
		protected override void OnRelease()
		{
			if (m_Subscribed)
			{
				GF.Event.Unsubscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
				m_Subscribed = false;
			}

			base.OnRelease();
		}

		/// <summary>事件回调：参数用完即止，不持有（根规范 §5.2）。</summary>
		private void OnDamageDealt(object sender, GameEventArgs args)
		{
			if (args is not DamageDealtEventArgs e || m_Layer == null || !IsInstanceValid(m_Layer))
			{
				return;
			}

			// 未闪避但伤害为 0（被减伤截断）不飘字，同旧项目 reduce_hp 的 value > 0 判断
			if (!e.IsMiss && e.Damage <= 0)
			{
				return;
			}

			UI.DamagePop pop = NodePool.Instance.Get<UI.DamagePop>(DamagePopScene, m_Layer);
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
