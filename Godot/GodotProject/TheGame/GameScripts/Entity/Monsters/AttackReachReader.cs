using GameLogic.Entity.Monsters.AI;
using Godot;

namespace GameLogic.Entity.Monsters
{
	/// <summary>
	/// 从攻击动画的判定盒**值轨道**推导"这一招够得着哪里"（无状态工具）。
	///
	/// 为什么从动画读：判定盒的形状/位置/开关本来就是动画关键帧（Entity/AGENTS.md「出招时序与判定盒」），
	/// 如果 AI 的出招距离再在表里写一份，两份数据迟早对不上（"站定了却打不中"）。这里把动画当唯一真相源：
	/// 在每个 disabled=false 的判定窗口内，取窗口起点与窗口内每个 shape/position 关键帧时刻的形状矩形，求并集。
	///
	/// 坐标：相对实体原点、**素材原生朝左**（与动画轨道一致；按朝向镜像交给 <see cref="MonsterAiRules.ToFacing"/>）。
	/// 离散轨道取"≤ t 的最后一个关键帧"，没有关键帧时取场景里节点的默认值（与 AnimationMixer 的捕获还原一致）。
	/// </summary>
	public static class AttackReachReader
	{
		/// <summary>
		/// 推导一招的范围。任一前提缺失（无判定盒节点、无动画、从未启用判定）返回空盒——调用方据此告警。
		/// </summary>
		public static AiBox Read(ActorEntity actor, string animName)
		{
			if (actor.AnimPlayer == null || actor.HitBox == null || string.IsNullOrEmpty(animName) ||
			    !actor.AnimPlayer.HasAnimation(animName))
			{
				return default;
			}

			CollisionShape2D shapeNode = FindShape(actor.HitBox);
			if (shapeNode == null)
			{
				return default;
			}

			Animation anim = actor.AnimPlayer.GetAnimation(animName);
			Node root = actor.AnimPlayer.GetNode(actor.AnimPlayer.RootNode);
			string nodePath = root.GetPathTo(shapeNode).ToString();
			int disabledTrack = anim.FindTrack($"{nodePath}:disabled", Animation.TrackType.Value);
			int shapeTrack = anim.FindTrack($"{nodePath}:shape", Animation.TrackType.Value);
			int positionTrack = anim.FindTrack($"{nodePath}:position", Animation.TrackType.Value);

			// 判定盒容器与 Area2D 自身的本地位置（容器 scale 只负责朝向镜像，不参与原生坐标）
			Vector2 offset = actor.HitBox.Position + (actor.HitBoxRoot?.Position ?? Vector2.Zero);

			AiBox reach = default;
			float length = (float)anim.Length;
			bool sceneDisabled = shapeNode.Disabled;
			if (disabledTrack < 0)
			{
				return sceneDisabled ? default : Sample(anim, shapeTrack, positionTrack, shapeNode, offset, 0f, length);
			}

			int keys = anim.TrackGetKeyCount(disabledTrack);
			// 第一个关键帧之前沿用场景值
			float firstKey = keys > 0 ? (float)anim.TrackGetKeyTime(disabledTrack, 0) : length;
			if (!sceneDisabled && firstKey > 0f)
			{
				reach = reach.Union(Sample(anim, shapeTrack, positionTrack, shapeNode, offset, 0f, firstKey));
			}

			for (int k = 0; k < keys; k++)
			{
				if (anim.TrackGetKeyValue(disabledTrack, k).AsBool())
				{
					continue;
				}

				float start = (float)anim.TrackGetKeyTime(disabledTrack, k);
				float end = k + 1 < keys ? (float)anim.TrackGetKeyTime(disabledTrack, k + 1) : length;
				reach = reach.Union(Sample(anim, shapeTrack, positionTrack, shapeNode, offset, start, end));
			}

			return reach;
		}

		/// <summary>判定窗口 [start, end) 内的形状并集：窗口起点 + 窗口内每个 shape/position 关键帧时刻。</summary>
		private static AiBox Sample(Animation anim, int shapeTrack, int positionTrack, CollisionShape2D shapeNode,
			Vector2 offset, float start, float end)
		{
			AiBox box = RectAt(anim, shapeTrack, positionTrack, shapeNode, offset, start);
			foreach (int track in new[] { shapeTrack, positionTrack })
			{
				if (track < 0)
				{
					continue;
				}

				for (int k = 0; k < anim.TrackGetKeyCount(track); k++)
				{
					float t = (float)anim.TrackGetKeyTime(track, k);
					if (t > start && t < end)
					{
						box = box.Union(RectAt(anim, shapeTrack, positionTrack, shapeNode, offset, t));
					}
				}
			}

			return box;
		}

		private static AiBox RectAt(Animation anim, int shapeTrack, int positionTrack, CollisionShape2D shapeNode,
			Vector2 offset, float t)
		{
			Shape2D shape = ValueAt(anim, shapeTrack, t, out Variant shapeValue) ? shapeValue.As<Shape2D>() : shapeNode.Shape;
			Vector2 position = ValueAt(anim, positionTrack, t, out Variant posValue) ? posValue.AsVector2() : shapeNode.Position;
			if (shape == null)
			{
				return default;
			}

			Rect2 rect = shape.GetRect();
			Vector2 min = rect.Position + position + offset;
			Vector2 max = rect.End + position + offset;
			return new AiBox(min.X, max.X, min.Y, max.Y);
		}

		/// <summary>离散值轨道在 t 时刻的值：≤ t 的最后一个关键帧；没有则返回 false（用场景默认值）。</summary>
		private static bool ValueAt(Animation anim, int track, float t, out Variant value)
		{
			value = default;
			if (track < 0)
			{
				return false;
			}

			bool found = false;
			for (int k = 0; k < anim.TrackGetKeyCount(track); k++)
			{
				if (anim.TrackGetKeyTime(track, k) > t + 1e-4)
				{
					break;
				}

				value = anim.TrackGetKeyValue(track, k);
				found = true;
			}

			return found;
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
