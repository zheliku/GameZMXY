using System.Collections.Generic;
using GameLogic.Entity.Body;
using Godot;

namespace GameLogic.Battle.Tests
{
	/// <summary>公共假宿主：实现 <see cref="IActorBody"/> 的重合成员（英雄与怪物的身体状态测试共用）。</summary>
	internal abstract class FakeActorBody : IActorBody
	{
		public Vector2 Velocity { get; set; }
		public bool Dead { get; set; }
		public float Gravity => 980f;
		public int AttackSegment { get; set; } = -1;
		public Vector2? PendingHurt;
		public bool HasPendingHurt => PendingHurt.HasValue;
		public int Facing = 1;
		public string LastAnim = "";
		public readonly List<string> Anims = new();
		public int BeginCount;
		public int EndCount;
		public int HurtSounds;
		public int DiedCount;

		public Vector2 TakePendingHurt()
		{
			Vector2 v = PendingHurt ?? Vector2.Zero;
			PendingHurt = null;
			return v;
		}

		public void SetFacing(int dir)
		{
			if (dir != 0)
			{
				Facing = dir > 0 ? 1 : -1;
			}
		}

		public void PlayAnim(string anim)
		{
			if (anim != LastAnim)
			{
				RestartAnim(anim);
			}
		}

		public void RestartAnim(string anim)
		{
			LastAnim = anim;
			Anims.Add(anim);
		}

		public void BeginAttack(int index)
		{
			BeginCount++;
			AttackSegment = index;
		}

		public void EndAttack()
		{
			EndCount++;
			AttackSegment = -1;
		}

		public void PlayHurtSound() => HurtSounds++;

		public void OnDied() => DiedCount++;
	}
}
