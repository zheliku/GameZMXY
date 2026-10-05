using GameConfig.Battle;
using Godot;
using GodotGameFramework.NodePool;

namespace GameLogic.UI
{
	/// <summary>
	/// 伤害飘字（池化节点，根规范 §4 红线 6）：NodePool 管理实例，本类实现 IPoolable。
	///
	/// 表现参考旧项目 DamageText（借鉴时序，重写实现）：
	///  * 位图数字：每种样式一张 10 格横条图集（Sprites/Number/damage_number_*.png，gen_damage_numbers.gd 迁移），
	///    每位数字一个 Sprite2D，RegionRect 选格；
	///  * 普通：0~0.1s 放大到 2 倍、0.2s 回 1 倍；0~0.6s 上升 50px；0.6~1.0s 淡出；1.0s 回池。
	///    暴击：放大到 3 倍（0.2s）、0.4s 回 1 倍，数字间距更紧（旧 separation −20 vs −10）。
	///  * 闪避：miss 贴图，0~0.4s 上升 30px，0.4~0.9s 淡出，0.9s 回池。
	///
	/// 零分配复用：数字节点在首次实例化时按 <see cref="MaxDigits"/> 一次建好，之后只切贴图/可见性；
	/// 动画用 Tween，<see cref="OnRelease"/> 时 Kill，避免回池后回调误触发。
	/// 时序与缩放是表现层常数（非玩法数值），按根规范 §4.5 写具名常量并注明来源。
	/// </summary>
	public partial class DamagePop : Node2D, IPoolable
	{
		private static readonly int MaxDigits = 10; // 最多显示的位数，覆盖 int 伤害值上限。

		private static readonly float DigitOverlap = 10f; // 普通数字位重叠像素。
		private static readonly float CritDigitOverlap = 20f; // 暴击数字位重叠像素。

		private static readonly float PopScale = 2f; // 普通飘字放大倍率。
		private static readonly float PopScaleUpTime = 0.1f; // 普通飘字放大时长（秒）。
		private static readonly float PopScaleDownTime = 0.1f; // 普通飘字缩回时长（秒）。
		private static readonly float RiseDistance = 50f; // 普通飘字上升距离（像素）。
		private static readonly float RiseTime = 0.6f; // 普通飘字上升时长（秒）。
		private static readonly float FadeTime = 0.4f; // 普通飘字淡出时长（秒）。

		private static readonly float CritPopScale = 3f; // 暴击飘字放大倍率。
		private static readonly float CritScaleUpTime = 0.2f; // 暴击飘字放大时长（秒）。
		private static readonly float CritScaleDownTime = 0.2f; // 暴击飘字缩回时长（秒）。

		private static readonly float MissRiseDistance = 30f; // 闪避提示上升距离（像素）。
		private static readonly float MissRiseTime = 0.4f; // 闪避提示上升时长（秒）。
		private static readonly float MissFadeTime = 0.5f; // 闪避提示淡出时长（秒）。

		[Export] private Texture2D m_MonsterPhysics; // 怪物受物理伤害的数字图集。
		[Export] private Texture2D m_MonsterPhysicsCrit; // 怪物受物理暴击的数字图集。
		[Export] private Texture2D m_MonsterMagic; // 怪物受魔法伤害的数字图集。
		[Export] private Texture2D m_MonsterMagicCrit; // 怪物受魔法暴击的数字图集。
		[Export] private Texture2D m_HeroPhysics; // 英雄受物理伤害的数字图集。
		[Export] private Texture2D m_HeroMagic; // 英雄受魔法伤害的数字图集。
		[Export] private Texture2D m_Real; // 真实伤害数字图集。

		[Export] private Sprite2D m_Miss; // 闪避提示贴图。

		[Export] private Node2D m_Content; // 缩放和透明度动画作用的内容节点。

		private Sprite2D[] m_Digits; // 首次取出时创建并重复复用的数字节点。
		private Tween m_Tween; // 当前飘字动画。

		/// <summary>从池取出（NodePool 调用）：此刻尚未设置内容，这里只复位公共状态。</summary>
		public void OnGet()
		{
			EnsureDigits();
			m_Content.Scale = Vector2.One;
			m_Content.Modulate = Colors.White;
		}

		/// <summary>归还池（NodePool 调用）：停动画、隐藏全部子元素。</summary>
		public void OnRelease()
		{
			KillTween();
			if (m_Digits != null)
			{
				foreach (Sprite2D digit in m_Digits)
				{
					digit.Visible = false;
				}
			}

			if (m_Miss != null)
			{
				m_Miss.Visible = false;
			}
		}

		/// <summary>显示一次伤害数字（取出后调用）。</summary>
		/// <param name="worldPosition">锚点（世界坐标）</param>
		/// <param name="damage">伤害</param>
		/// <param name="kind">伤害类型</param>
		/// <param name="isCrit">是否暴击</param>
		/// <param name="targetIsHero">受击方是否为英雄</param>
		public void ShowDamage(Vector2 worldPosition, int damage, DamageKind kind, bool isCrit, bool targetIsHero)
		{
			GlobalPosition = worldPosition;
			m_Miss.Visible = false;
			LayoutDigits(damage, PickSheet(kind, isCrit, targetIsHero), isCrit ? CritDigitOverlap : DigitOverlap);

			// 旧项目：英雄受击暴击才用夸张的 Crit 动画；怪物受击暴击只换贴图。这里统一——暴击都用放大动画
			float scale = isCrit ? CritPopScale : PopScale;
			float up = isCrit ? CritScaleUpTime : PopScaleUpTime;
			float down = isCrit ? CritScaleDownTime : PopScaleDownTime;

			// 三条并行轨道（同旧动画：缩放脉冲 / 上升 / 上升结束后淡出），最后回池
			KillTween();
			m_Tween = CreateTween().SetParallel();
			m_Tween.TweenProperty(m_Content, "scale", Vector2.One * scale, up);
			m_Tween.TweenProperty(m_Content, "scale", Vector2.One, down).SetDelay(up);
			m_Tween.TweenProperty(this, "position:y", Position.Y - RiseDistance, RiseTime);
			m_Tween.TweenProperty(m_Content, "modulate:a", 0f, FadeTime).SetDelay(RiseTime);
			m_Tween.Chain().TweenCallback(Callable.From(ReturnToPool));
		}

		/// <summary>显示一次闪避（取出后调用）。</summary>
		/// <summary>显示一次闪避提示。</summary>
		/// <param name="worldPosition">提示锚点的世界坐标。</param>
		public void ShowMiss(Vector2 worldPosition)
		{
			GlobalPosition = worldPosition;
			HideDigits();
			m_Miss.Visible = true;

			// 顺序：上升 → 淡出 → 回池（同旧 miss_effect）
			KillTween();
			m_Tween = CreateTween();
			m_Tween.TweenProperty(this, "position:y", Position.Y - MissRiseDistance, MissRiseTime);
			m_Tween.TweenProperty(m_Content, "modulate:a", 0f, MissFadeTime);
			m_Tween.TweenCallback(Callable.From(ReturnToPool));
		}

		private void ReturnToPool() // 动画完成后将飘字归还对象池。
		{
			m_Tween = null;
			NodePool.Instance.Release(this);
		}

		private Texture2D PickSheet(DamageKind kind, bool isCrit, bool targetIsHero) // 按伤害类型、暴击和受击方选择数字图集。
		{
			if (kind == DamageKind.Real)
			{
				return m_Real;
			}

			if (targetIsHero)
			{
				return kind == DamageKind.Magic ? m_HeroMagic : m_HeroPhysics;
			}

			if (kind == DamageKind.Magic)
			{
				return isCrit ? m_MonsterMagicCrit : m_MonsterMagic;
			}

			return isCrit ? m_MonsterPhysicsCrit : m_MonsterPhysics;
		}

		private void LayoutDigits(int value, Texture2D sheet, float overlap) // 将数字按位居中排布并隐藏多余节点。
		{
			// 用整数除法确定数位，避免浮点对数在 10 的幂次附近少算一位。
			value = Mathf.Max(0, value);
			int count = 1;
			int probe = value;
			while (probe >= 10 && count < MaxDigits)
			{
				probe /= 10;
				count++;
			}

			// 根据图集单格宽度和重叠量计算整组数字的居中起点。
			float cellW = sheet.GetWidth() / 10f;
			float cellH = sheet.GetHeight();
			float step = cellW - overlap;
			float startX = -(step * (count - 1)) * 0.5f;

			// 从最高位开始分解数值，并逐格设置贴图区域与位置。
			int divisor = 1;
			for (int i = 1; i < count; i++)
			{
				divisor *= 10;
			}

			for (int i = 0; i < MaxDigits; i++)
			{
				Sprite2D digit = m_Digits[i];
				if (i >= count)
				{
					digit.Visible = false;
					continue;
				}

				int d = value / divisor % 10;
				divisor = Mathf.Max(1, divisor / 10);
				digit.Texture = sheet;
				digit.RegionRect = new Rect2(d * cellW, 0, cellW, cellH);
				digit.Position = new Vector2(startX + i * step, 0);
				digit.Visible = true;
			}
		}

		private void HideDigits() // 隐藏全部数字节点。
		{
			foreach (Sprite2D digit in m_Digits)
			{
				digit.Visible = false;
			}
		}

		private void EnsureDigits() // 首次取出时创建数字节点，后续直接复用。
		{
			if (m_Digits != null)
			{
				return;
			}

			// 固定创建最大位数的节点，之后只更新贴图、区域和可见性。
			m_Digits = new Sprite2D[MaxDigits];
			for (int i = 0; i < MaxDigits; i++)
			{
				Sprite2D digit = new Sprite2D
				{
					RegionEnabled = true,
					TextureFilter = TextureFilterEnum.Linear,
					Visible = false,
				};
				m_Content.AddChild(digit);
				m_Digits[i] = digit;
			}
		}

		private void KillTween() // 停止当前有效动画并清除引用。
		{
			if (m_Tween != null && m_Tween.IsValid())
			{
				m_Tween.Kill();
			}

			m_Tween = null;
		}
	}
}
