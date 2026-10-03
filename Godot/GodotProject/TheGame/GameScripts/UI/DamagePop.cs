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
		/// <summary>最多显示的位数（int 伤害上限 10 位）</summary>
		private static readonly int MaxDigits = 10;

		/// <summary>数字位间距（相对格宽的重叠像素；旧 HBoxContainer separation：普通 −10、暴击 −20）</summary>
		private static readonly float DigitOverlap = 10f;
		private static readonly float CritDigitOverlap = 20f;

		/// <summary>普通飘字时序（旧 DamageText "physics" 动画）</summary>
		private static readonly float PopScale = 2f;
		private static readonly float PopScaleUpTime = 0.1f;
		private static readonly float PopScaleDownTime = 0.1f;
		private static readonly float RiseDistance = 50f;
		private static readonly float RiseTime = 0.6f;
		private static readonly float FadeTime = 0.4f;

		/// <summary>暴击时序（旧 "Crit" 动画：放大 3 倍 0.2s、0.4s 回原）</summary>
		private static readonly float CritPopScale = 3f;
		private static readonly float CritScaleUpTime = 0.2f;
		private static readonly float CritScaleDownTime = 0.2f;

		/// <summary>闪避时序（旧 miss_effect：0.4s 上升 30px，0.4~0.9s 淡出）</summary>
		private static readonly float MissRiseDistance = 30f;
		private static readonly float MissRiseTime = 0.4f;
		private static readonly float MissFadeTime = 0.5f;

		/// <summary>数字样式图集（场景 [Export] 绑定，按伤害类型/暴击/受击方选）</summary>
		[Export] private Texture2D m_MonsterPhysics;
		[Export] private Texture2D m_MonsterPhysicsCrit;
		[Export] private Texture2D m_MonsterMagic;
		[Export] private Texture2D m_MonsterMagicCrit;
		[Export] private Texture2D m_HeroPhysics;
		[Export] private Texture2D m_HeroMagic;
		[Export] private Texture2D m_Real;

		/// <summary>闪避贴图</summary>
		[Export] private Sprite2D m_Miss;

		/// <summary>缩放/透明度作用节点（数字与 miss 的父节点）</summary>
		[Export] private Node2D m_Content;

		private Sprite2D[] m_Digits;
		private Tween m_Tween;

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

		private void ReturnToPool()
		{
			m_Tween = null;
			NodePool.Instance.Release(this);
		}

		/// <summary>选数字样式（对应旧 DamageNumber.gd 的贴图选择；真实伤害人怪共用一套）。</summary>
		private Texture2D PickSheet(DamageKind kind, bool isCrit, bool targetIsHero)
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

		/// <summary>把数字按位排开（整体水平居中），多余的数字节点隐藏。</summary>
		private void LayoutDigits(int value, Texture2D sheet, float overlap)
		{
		value = Mathf.Max(0, value);
		// 整数除法数位数：Mathf.Log(100000)/Mathf.Log(10) 因 float 精度算成 4.9999998，
		// floor 后少一位（整 10^5 伤害会渲染成 "00000"），禁用对数计数。
		int count = 1;
		int probe = value;
		while (probe >= 10 && count < MaxDigits)
		{
			probe /= 10;
			count++;
		}

			float cellW = sheet.GetWidth() / 10f;
			float cellH = sheet.GetHeight();
			float step = cellW - overlap;
			float startX = -(step * (count - 1)) * 0.5f;

			// 从最高位写起：divisor = 10^(count-1)
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

		private void HideDigits()
		{
			foreach (Sprite2D digit in m_Digits)
			{
				digit.Visible = false;
			}
		}

		/// <summary>数字节点一次性建好（首次取出时），之后复用。</summary>
		private void EnsureDigits()
		{
			if (m_Digits != null)
			{
				return;
			}

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

		private void KillTween()
		{
			if (m_Tween != null && m_Tween.IsValid())
			{
				m_Tween.Kill();
			}

			m_Tween = null;
		}
	}
}
