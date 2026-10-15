using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameConfig.Battle;
using GameLogic.Battle.Damage;
using GameLogic.Event.Combat;
using GameLogic.FloatingText;
using Godot;
using GodotGameFramework;

/// <summary>通过真实 GGF 事件、节点池及引擎时间验证通用飘字的会话所有权。</summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>验证被动挂树、样式、暂停、自然结束、场景隔离、延迟事件与节点复用。</summary>
    /// <returns>真实引擎回归任务。</returns>
    private async Task CheckFloatingTextAsync()
    {
        int subscribers = GF.Event.Count(DamageDealtEventArgs.EventId);
        var first = new FloatingTextPresenter();
        var second = new FloatingTextPresenter();
        var targets = new HashSet<int> { -101 };
        m_Driver.AddChild(first);
        m_Driver.AddChild(second);
        var firstTexts = SmokeInspection.ReadField<HashSet<FloatingTextNode>>(first, "m_Texts");
        var secondTexts = SmokeInspection.ReadField<HashSet<FloatingTextNode>>(second, "m_Texts");
        try
        {
            Check(GF.Event.Count(DamageDealtEventArgs.EventId) == subscribers, "飘字组件仅挂树就订阅了玩法事件");
            first.StartSession(targets.Contains);
            second.StartSession(id => id == -102);
            Check(GF.Event.Count(DamageDealtEventArgs.EventId) == subscribers + 2, "飘字显式开始没有建立订阅");

            // 一个真实池实例依次显示四种反馈，验证位图、对齐、缩放和透明度不会跨样式残留。
            first.ShowRecovery(Vector2.Zero, 100, RecoveryKind.Health);
            var reused = firstTexts.Single();
            var reusedDigits = SmokeInspection.ReadField<Sprite2D[]>(reused, "m_Digits");
            Check(!reusedDigits[0].Centered && reusedDigits.Count(x => x.Visible) == 3, "恢复数字初始布局错误");
            CheckDigitSpacing(reused, 3, false);
            Check(reusedDigits[1].Position.X - reusedDigits[0].Position.X <
                reusedDigits[2].Position.X - reusedDigits[1].Position.X, "100 的窄字没有使用较短的步长");
            reused._Process(1);

            // 全部两位组合验证轮廓宽度和窄字侧留白，覆盖 0–9，包含内部零与重复字符。
            for (int value = 0; value < 100; value++)
            {
                first.ShowRecovery(Vector2.Zero, value + 100, RecoveryKind.Health);
                CheckDigitSpacing(firstTexts.Single(), 3, false);
                firstTexts.Single()._Process(1);
                first.ShowRecovery(Vector2.Zero, value + 100, RecoveryKind.Mana);
                CheckDigitSpacing(firstTexts.Single(), 3, false);
                firstTexts.Single()._Process(1);
            }
            foreach (var (kind, crit, hero) in new[]
            {
                (DamageKind.Physics, false, false), (DamageKind.Physics, true, false),
                (DamageKind.Magic, false, false), (DamageKind.Magic, true, false),
                (DamageKind.Physics, false, true), (DamageKind.Magic, false, true),
                (DamageKind.Real, false, true)
            })
                for (int value = 0; value < 100; value++)
                {
                    GF.Event.FireNow(this, DamageDealtEventArgs.Create(new DamageResult(value + 100, false, crit, kind), -101,
                        hero, Vector2.Zero));
                    CheckDigitSpacing(firstTexts.Single(), 3, true);
                    firstTexts.Single()._Process(1);
                }
            Check(firstTexts.Count == 0, "恢复样式没有自然回池");
            GF.Event.FireNow(this, DamageDealtEventArgs.Create(new DamageResult(int.MaxValue, false, true, DamageKind.Magic),
                -101, false, Vector2.Zero));
            Check(ReferenceEquals(reused, firstTexts.Single()) && reusedDigits.All(x => x.Visible && x.Centered),
                "恢复到伤害没有复用同一池实例，或最大整数的十位数布局错误");
            CheckDigitSpacing(reused, 10, true);
            reused._Process(.2);
            Check(SmokeInspection.ReadField<Node2D>(reused, "m_Content").Scale == Vector2.One * 3, "暴击放大节奏错误");
            reused._Process(.6);
            Check(SmokeInspection.ReadField<Node2D>(reused, "m_Content").Modulate.A < 1, "伤害没有淡出");
            reused._Process(.2);
            first.ShowRecovery(Vector2.Zero, 8, RecoveryKind.Mana);
            var reusedContent = SmokeInspection.ReadField<Node2D>(reused, "m_Content");
            Check(ReferenceEquals(reused, firstTexts.Single()) && reusedContent.Scale == Vector2.One &&
                reusedContent.Modulate == Colors.White && reusedDigits.Count(x => x.Visible) == 1 &&
                !reusedDigits[0].Centered && reusedDigits[0].Texture.ResourcePath.EndsWith("mana.png"),
                "伤害到魔法恢复残留旧布局、透明度或缩放");
            CheckDigitSpacing(reused, 1, false);
            reused._Process(1);
            GF.Event.FireNow(this, DamageDealtEventArgs.Create(DamageResult.Missed(DamageKind.Physics), -101,
                false, Vector2.Zero));
            Check(ReferenceEquals(reused, firstTexts.Single()) && reusedDigits.All(x => !x.Visible) &&
                SmokeInspection.ReadField<Sprite2D>(reused, "m_Miss").Visible, "闪避复用没有隐藏数字");
            reused._Process(1);

            // 七种真实数字样式和闪避分别走正式事件回调；零伤害和其他场景不应创建提示。
            foreach (var (kind, crit, hero, sheet) in new[]
            {
                (DamageKind.Physics, false, false, "m_MonsterPhysics"),
                (DamageKind.Physics, true, false, "m_MonsterPhysicsCrit"),
                (DamageKind.Magic, false, false, "m_MonsterMagic"),
                (DamageKind.Magic, true, false, "m_MonsterMagicCrit"),
                (DamageKind.Physics, false, true, "m_HeroPhysics"),
                (DamageKind.Magic, false, true, "m_HeroMagic"),
                (DamageKind.Real, false, true, "m_Real")
            })
            {
                var before = firstTexts.ToHashSet();
                GF.Event.FireNow(this, DamageDealtEventArgs.Create(new DamageResult(123, false, crit, kind), -101,
                    hero, Vector2.Zero));
                var pop = firstTexts.Single(x => !before.Contains(x));
                var digits = SmokeInspection.ReadField<Sprite2D[]>(pop, "m_Digits");
                Check(digits.Count(x => x.Visible) == 3 &&
                    digits[0].Texture == SmokeInspection.ReadField<Texture2D>(pop, sheet), "伤害数字位数或样式错误");
                CheckDigitSpacing(pop, 3, true);
            }
            GF.Event.FireNow(this, DamageDealtEventArgs.Create(DamageResult.Missed(DamageKind.Physics), -101,
                false, Vector2.Zero));
            Check(firstTexts.Count == 8 && firstTexts.Count(x =>
                SmokeInspection.ReadField<Sprite2D>(x, "m_Miss").Visible) == 1, "闪避提示没有使用专用样式");
            GF.Event.FireNow(this, DamageDealtEventArgs.Create(default, -101, false, Vector2.Zero));
            GF.Event.FireNow(this, DamageDealtEventArgs.Create(new DamageResult(10, false, false, DamageKind.Physics),
                -999, false, Vector2.Zero));
            Check(firstTexts.Count == 8 && secondTexts.Count == 0, "零伤害或其他场景事件产生了多余飘字");

            var beforeRecovery = firstTexts.ToHashSet();
            first.ShowRecovery(Vector2.Zero, 100, RecoveryKind.Health);
            first.ShowRecovery(Vector2.Zero, 50, RecoveryKind.Mana);
            first.ShowRecovery(Vector2.Zero, 0, RecoveryKind.Health);
            var recoveryTexts = firstTexts.Where(x => !beforeRecovery.Contains(x)).ToArray();
            Check(firstTexts.Count == 10 && recoveryTexts.Length == 2 && recoveryTexts.Select(x =>
                SmokeInspection.ReadField<Sprite2D[]>(x, "m_Digits")[0].Texture.ResourcePath)
                .Distinct().Count() == 2, "通用恢复入口没有区分加血和加蓝，或显示了零量提示");
            foreach (var recovery in recoveryTexts)
                CheckDigitSpacing(recovery, SmokeInspection.ReadField<Sprite2D[]>(recovery, "m_Digits").Count(x => x.Visible), false);

            // 所有反馈使用同一游戏秒数计时，暂停应同时冻结位置、脉冲和淡出。
            await WaitFramesAsync(2);
            GF.Base.PauseGame();
            // ProcessFrame 在节点更新前发出；让本帧已计算的 delta 消费完，再观察暂停区间。
            await WaitFramesAsync(2);
            var sampleDamage = beforeRecovery.First();
            var sampleRecovery = recoveryTexts.First();
            Vector2 damagePosition = sampleDamage.GlobalPosition;
            Vector2 recoveryPosition = sampleRecovery.GlobalPosition;
            var damageContent = SmokeInspection.ReadField<Node2D>(sampleDamage, "m_Content");
            Vector2 damageScale = damageContent.Scale;
            Color damageColor = damageContent.Modulate;
            await WaitFramesAsync(12);
            Check(sampleDamage.GlobalPosition == damagePosition && sampleRecovery.GlobalPosition == recoveryPosition &&
                damageContent.Scale == damageScale && damageContent.Modulate == damageColor,
                "暂停后伤害或恢复飘字仍然移动");
            GF.Base.ResumeGame();
            await WaitUntilAsync(() => firstTexts.Count == 0, "飘字没有自然结束并回池", 4000);

            // 两个组件共享同类池，停止第一个不能回收第二个的活动实例。
            GF.Event.FireNow(this, DamageDealtEventArgs.Create(new DamageResult(10, false, false, DamageKind.Physics),
                -101, false, Vector2.Zero));
            GF.Event.FireNow(this, DamageDealtEventArgs.Create(new DamageResult(20, false, true, DamageKind.Magic),
                -102, false, Vector2.Zero));
            first.ShowRecovery(Vector2.Zero, 10, RecoveryKind.Health);
            second.ShowRecovery(Vector2.Zero, 20, RecoveryKind.Mana);
            var held = firstTexts.ToHashSet();
            first.StopSession();
            first.StopSession();
            Check(firstTexts.Count == 0 && held.All(x => !x.Visible),
                "会话结束没有回收本场飘字");
            Check(secondTexts.Count == 2 && secondTexts.All(x => x.Visible), "结束一个会话错误地清空了其他场景的同类池");
            Check(GF.Event.Count(DamageDealtEventArgs.EventId) == subscribers + 1, "重复停止会话导致订阅数量错误");

            // 旧实体的排队事件不能串到复用组件的新会话；被归还的节点不保留旧完成回调。
            GF.Event.Fire(this, DamageDealtEventArgs.Create(new DamageResult(99, false, false, DamageKind.Physics),
                -101, false, Vector2.Zero));
            targets.Clear();
            targets.Add(-103);
            first.StartSession(targets.Contains);
            await WaitFramesAsync(2);
            Check(firstTexts.Count == 0, "旧周期排队伤害串入了新飘字会话");
            GF.Event.FireNow(this, DamageDealtEventArgs.Create(new DamageResult(7, false, false, DamageKind.Physics),
                -103, false, Vector2.Zero));
            first.ShowRecovery(Vector2.Zero, 8, RecoveryKind.Mana);
            Check(firstTexts.SetEquals(held),
                "结束会话后飘字实例没有从同一 GGF 节点池复用");
            Check(firstTexts.All(x => SmokeInspection.ReadField<Sprite2D[]>(x, "m_Digits").Count(digit => digit.Visible) == 1) &&
                firstTexts.Count(x => SmokeInspection.ReadField<Sprite2D[]>(x, "m_Digits")[0].Texture.ResourcePath.EndsWith("mana.png")) == 1,
                "复用残留旧数字或恢复样式");
            await WaitUntilAsync(() => firstTexts.Count == 0 && secondTexts.Count == 0, "复用后的飘字没有正确结束", 4000);

            // 同屏比较窄数字、混合数字和宽数字；暂停截图避免动画时差影响字距评估。
            GF.Base.PauseGame();
            await WaitFramesAsync(2);
            int row = 0;
            foreach (var (kind, crit, hero) in new[]
            {
                (DamageKind.Physics, false, false), (DamageKind.Physics, true, false),
                (DamageKind.Magic, false, false), (DamageKind.Magic, true, false),
                (DamageKind.Physics, false, true), (DamageKind.Magic, false, true),
                (DamageKind.Real, false, true)
            })
            {
                int column = 0;
                foreach (int value in new[] { 111, 108, 888 })
                {
                    Vector2 position = first.GetCanvasTransform().AffineInverse() * new Vector2(150 + column++ * 135, 100 + row * 40);
                    GF.Event.FireNow(this, DamageDealtEventArgs.Create(new DamageResult(value, false, crit, kind), -103, hero, position));
                }
                row++;
            }
            foreach (var recoveryKind in new[] { RecoveryKind.Health, RecoveryKind.Mana })
            {
                int column = 0;
                foreach (int value in new[] { 111, 108, 888 })
                {
                    Vector2 position = first.GetCanvasTransform().AffineInverse() * new Vector2(650 + column++ * 100, 180 + (int)recoveryKind * 70);
                    first.ShowRecovery(position + new Vector2(20, 60), value, recoveryKind);
                }
            }
            int sampleRow = 0;
            foreach (int value in new[] { 10, 17, 24, 36, 59, 70, 99, 1234567890 })
            {
                Vector2 position = first.GetCanvasTransform().AffineInverse() * new Vector2(120 + sampleRow % 4 * 210, 420 + sampleRow / 4 * 45);
                GF.Event.FireNow(this, DamageDealtEventArgs.Create(new DamageResult(value, false, false, DamageKind.Physics), -103, false, position));
                sampleRow++;
            }
            await CaptureCombatAsync("floating_text_spacing");
            GF.Base.ResumeGame();
        }
        finally
        {
            GF.Base.ResumeGame();
            first.StopSession();
            second.StopSession();
            first.QueueFree();
            second.QueueFree();
        }
        Check(GF.Event.Count(DamageDealtEventArgs.EventId) == subscribers, "飘字回归结束仍残留事件订阅");
        GD.Print("SMOKE-UI: 通用飘字九种样式×100组数字比例字距/描边/纵向注册位置、伤害/闪避/生命/魔法、暂停、作用域隔离及跨样式回池复用通过");
    }

    /// <summary>从真实贴图的透明轮廓检查比例字宽、窄字侧留白与整串居中，不依赖生产字宽缓存。</summary>
    /// <param name="node">实际池化飘字节点。</param>
    /// <param name="expectedCount">应显示的数位数量。</param>
    /// <param name="centered">伤害应整串居中，恢复应从原点左起。</param>
    private void CheckDigitSpacing(FloatingTextNode node, int expectedCount, bool centered)
    {
        var digits = SmokeInspection.ReadField<Sprite2D[]>(node, "m_Digits").Where(x => x.Visible).ToArray();
        Check(digits.Length == expectedCount, "数字字距检查的数位数量错误");
        using var image = digits[0].Texture.GetImage();
        if (image.IsCompressed())
            image.Decompress();
        int cellWidth = image.GetWidth() / 10;
        int widest = 0;
        for (int number = 0; number < 10; number++)
        {
            using var glyph = image.GetRegion(new Rect2I(number * cellWidth, 0, cellWidth, image.GetHeight()));
            widest = Math.Max(widest, glyph.GetUsedRect().Size.X);
        }
        float firstLeft = 0;
        float previousRight = 0;
        for (int i = 0; i < digits.Length; i++)
        {
            var digit = digits[i];
            var region = digit.RegionRect;
            int number = (int)region.Position.X / cellWidth;
            using var original = image.GetRegion(new Rect2I(number * cellWidth, 0, cellWidth, image.GetHeight()));
            Rect2I bounds = original.GetUsedRect();
            Check(region.Position.X == number * cellWidth + bounds.Position.X && region.Size.X == bounds.Size.X &&
                region.Position.Y == 0 && region.Size.Y == image.GetHeight(), "数字裁切丢失描边或改变纵向注册位置");
            float left = digit.Position.X - (digit.Centered ? region.Size.X * .5f : 0);
            if (i == 0)
                firstLeft = left;
            else
            {
                float expectedGap = (2 * widest - digits[i - 1].RegionRect.Size.X - region.Size.X) / 6f;
                Check(Math.Abs(left - previousRight - expectedGap) < .001,
                    $"数字侧留白错误：实际 {left - previousRight:F4}px，应为 {expectedGap:F4}px");
            }
            previousRight = left + region.Size.X;
        }
        Check(Math.Abs(centered ? firstLeft + previousRight : firstLeft) < .001, "数字整串居中或左对齐错误");
    }
}
