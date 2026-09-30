using GameLogic.Entity.Monsters.AI;
using Godot;

namespace GameLogic.Entity.Monsters
{
	/// <summary>
	/// 从攻击动画的判定盒**值轨道**推导"这一招够得着哪里"（无状态工具）。
	///
	/// 判定盒形状/位置/开关本来就是动画关键帧（Entity/AGENTS.md「出招时序与判定盒」）；AI 的出招距离再在表里写一份，
	/// 两份数据迟早对不上。这里把动画当唯一真相源：对 disabled=false 的每个判定窗口，取窗口起点与窗口内每个
	/// shape/position 关键帧时刻的形状矩形求并集。
	/// 坐标相对实体原点、**素材原生朝左**（同动画轨道；按朝向换算见 <see cref="AiBox.Facing"/>）。
	/// 离散轨道取"≤ t 的最后一个关键帧"，没有关键帧时取场景默认值（同 AnimationMixer 的捕获还原）。
	/// </summary>
	public static class AttackReachReader
	{
		/// <summary>推导一招的范围；前提缺失（无判定盒 / 无动画 / 从未启用判定）返回空盒，由调用方告警。</summary>
		public static AiBox Read(ActorEntity actor, string animName)
		{
			AnimationPlayer player = actor.AnimPlayer;
			if (player == null || actor.HitBox == null || string.IsNullOrEmpty(animName) || !player.HasAnimation(animName) ||
			    FindShape(actor.HitBox) is not { } node)
			{
				return default;
			}

			Animation anim = player.GetAnimation(animName);
			string path = player.GetNode(player.RootNode).GetPathTo(node).ToString();
			int disabled = anim.FindTrack($"{path}:disabled", Animation.TrackType.Value);
			int shape = anim.FindTrack($"{path}:shape", Animation.TrackType.Value);
			int position = anim.FindTrack($"{path}:position", Animation.TrackType.Value);
			// 判定盒容器与 Area2D 的本地位置（容器 scale 只负责朝向镜像，不参与原生坐标）
			Vector2 offset = actor.HitBox.Position + (actor.HitBoxRoot?.Position ?? Vector2.Zero);

			AiBox RectAt(float t)
			{
				Shape2D s = ValueAt(anim, shape, t)?.As<Shape2D>() ?? node.Shape;
				if (s == null)
				{
					return default;
				}

				Vector2 p = (ValueAt(anim, position, t)?.AsVector2() ?? node.Position) + offset;
				Rect2 r = s.GetRect();
				return new AiBox(r.Position.X + p.X, r.End.X + p.X, r.Position.Y + p.Y, r.End.Y + p.Y);
			}

			// 采样时刻：disabled 轨道把动画切成若干窗口；每个启用窗口取起点 + 窗口内 shape/position 的关键帧
			AiBox reach = default;
			float length = (float)anim.Length;
			int keys = disabled < 0 ? 0 : anim.TrackGetKeyCount(disabled);
			float start = 0f;
			bool enabled = !node.Disabled;   // 第一个关键帧之前沿用场景值
			for (int k = 0; k <= keys; k++)
			{
				float end = k < keys ? (float)anim.TrackGetKeyTime(disabled, k) : length;
				if (enabled && end > start)
				{
					reach = reach.Union(RectAt(start));
					foreach (int track in new[] { shape, position })
					{
						for (int i = 0; track >= 0 && i < anim.TrackGetKeyCount(track); i++)
						{
							float t = (float)anim.TrackGetKeyTime(track, i);
							if (t > start && t < end)
							{
								reach = reach.Union(RectAt(t));
							}
						}
					}
				}

				if (k < keys)
				{
					start = end;
					enabled = !anim.TrackGetKeyValue(disabled, k).AsBool();
				}
			}

			return reach;
		}

		/// <summary>离散值轨道在 t 时刻的值（≤ t 的最后一个关键帧）；无轨道/无关键帧返回 null（用场景默认值）。</summary>
		private static Variant? ValueAt(Animation anim, int track, float t)
		{
			Variant? value = null;
			for (int k = 0; track >= 0 && k < anim.TrackGetKeyCount(track) && anim.TrackGetKeyTime(track, k) <= t + 1e-4; k++)
			{
				value = anim.TrackGetKeyValue(track, k);
			}

			return value;
		}

		private static CollisionShape2D FindShape(Area2D area)
		{
			foreach (Node child in area.GetChildren())
			{
				if (child is CollisionShape2D shape)
				{
					return shape;
				}
			}

			return null;
		}
	}
}
