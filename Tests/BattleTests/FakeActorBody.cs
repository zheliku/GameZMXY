using System.Collections.Generic;
using GameLogic.Entity.Body;
using Godot;

namespace GameLogic.Battle.Tests
{
	/// <summary>公共假宿主：实现 <see cref="IActorBody"/> 的重合成员（英雄与怪物的身体状态测试共用）。</summary>
	internal abstract class FakeActorBody : IActorBody
	{
		/// <summary>当前水平和垂直速度。</summary>
		public Vector2 Velocity { get; set; }
		/// <summary>宿主是否已死亡。</summary>
		public bool Dead { get; set; }
		/// <summary>测试使用的重力常量。</summary>
		public float Gravity => 980f;
		/// <summary>当前攻击段索引，未攻击时为 -1。</summary>
		public int AttackSegment { get; set; } = -1;
		/// <summary>待处理的受击击退速度。</summary>
		public Vector2? PendingHurt;
		/// <summary>是否存在待处理受击。</summary>
		public bool HasPendingHurt => PendingHurt.HasValue;
		/// <summary>当前朝向，右为 1、左为 -1。</summary>
		public int Facing = 1;
		/// <summary>最近播放的动画名称。</summary>
		public string LastAnim = "";
		/// <summary>按播放顺序记录的动画名称。</summary>
		public readonly List<string> Anims = new();
		/// <summary>开始攻击回调次数。</summary>
		public int BeginCount;
		/// <summary>结束攻击回调次数。</summary>
		public int EndCount;
		/// <summary>受击音效播放次数。</summary>
		public int HurtSounds;
		/// <summary>死亡回调次数。</summary>
		public int DiedCount;

		public Vector2 TakePendingHurt()
		{
			Vector2 v = PendingHurt ?? Vector2.Zero;
			PendingHurt = null;
			return v;
		}

		/// <summary>按方向输入更新朝向。</summary>
		public void SetFacing(int dir)
		{
			if (dir != 0)
			{
				Facing = dir > 0 ? 1 : -1;
			}
		}

		/// <summary>仅在动画改变时播放动画。</summary>
		public void PlayAnim(string anim)
		{
			if (anim != LastAnim)
			{
				RestartAnim(anim);
			}
		}

		/// <summary>强制重新播放指定动画。</summary>
		public void RestartAnim(string anim)
		{
			LastAnim = anim;
			Anims.Add(anim);
		}

		/// <summary>记录攻击段开始。</summary>
		public void BeginAttack(int index)
		{
			BeginCount++;
			AttackSegment = index;
		}

		/// <summary>记录攻击段结束并清除当前段。</summary>
		public void EndAttack()
		{
			EndCount++;
			AttackSegment = -1;
		}

		/// <summary>记录一次受击音效播放。</summary>
		public void PlayHurtSound() => HurtSounds++;

		/// <summary>记录一次死亡回调。</summary>
		public void OnDied() => DiedCount++;
	}
}
