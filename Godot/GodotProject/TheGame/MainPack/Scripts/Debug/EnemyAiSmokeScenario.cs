using System;
using System.Collections.Generic;
using System.Reflection;
using GameConfig.Battle;
using GameConfig.Sound;
using GameFramework;
using GameFramework.Event;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attacks;
using GameLogic.Enemies;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using GameLogic.Event.Combat;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework;

/// <summary>
/// 冒烟场景：怪物 AI（M5 完成标准"猴子巡逻、追击、攻击"+ 2026-09-30 判定盒范围/收招硬直）。
/// 由 <see cref="SmokeTestDriver"/> 在 `-- --smoketest=ai` 时驱动，英雄全程不输入（站桩当目标）。
///
/// 阶段（事件驱动，时间只作超时兜底）：
///  0 视野外   猴子挪到英雄右侧 600px → 巡逻，不追击；
///  1 受击激怒 3s 时挪到右侧 150px 并由英雄打一下 → Chase → Attack，出招 attack_1 并打中英雄；
///  2 转身     打中英雄后，下一次出招的当帧把英雄瞬移到猴子身后 → 出招与收招硬直期间朝向不变、出招中身体/动画一直是
///             Attack/attack_1；硬直结束后 1s 内转向英雄；
///  3 平台     英雄钉在猴子前方高 120px 的固定点 AirDuration 秒 → 不出招、AI 保留交战状态，按官方计数游荡；
///  4 落地     放英雄落地 → 猴子回 Attack 并出招；
///  5 丢失     英雄始终保持在猴子视野外（模拟跑得比它快）→ 始终保留存活目标；随后以英雄身份打一下（带击退）→ 身体受控，AI暂停；
///  6 击杀     致命伤 → 销毁AI、身体广播 EnemyDiedEventArgs、死亡动画后回收。
/// 另断言：AI交战距离来自场景；实际伤害轨道由原生几何专项核对。
/// </summary>
public sealed class EnemyAiSmokeScenario
{
    private const double InSightAt = 3.0; // 进入视野并开始追击的时间（秒）。
    private const double TimeoutAt = 36.0; // 整体超时，必须在引擎退出前给出结论（秒）。
    private const double TurnAroundWindow = 2.0; // 收招硬直结束后允许转向的时限（秒）。
    private const double AirDuration = 4.0; // 空中阶段持续时间（秒）。
    private const float AirHeight = 220f; // 空中阶段英雄离地高度（像素）。
    private const float AirOffsetX = 30f; // 空中阶段英雄相对猴子的水平偏移（像素）。
    private const double LandedTimeout = 6.0; // 英雄落地后等待猴子出招的时限（秒）；出手是概率判定，窗口要够覆盖多次掷骰。
    private const float LoseOffset = 420f; // 将英雄放到视野外的水平偏移（像素）。
    private const double LoseTimeout = 4.5; // 观测存活目标保留的持续时间（秒）。
    private const double KillDelay = 1.5; // 受控阶段到致命攻击的间隔（秒）。
    private const double EndDelay = 2.5; // 死亡阶段到场景结束的间隔（秒）。
    private const float OutOfSightOffset = 600f; // 初始视野外偏移（像素）。
    private const float InSightOffset = 150f; // 进入怪物视野时的水平偏移（像素）。
    private const float BehindOffset = 60f; // 转身测试中英雄位于怪物身后的水平偏移（像素）。

    private readonly AbstractHeroEntity _hero; // 受测英雄实体。
    private readonly AbstractEnemyEntity _monster; // 受测怪物实体。
    private readonly int _originalAttackDesire; // 结束时恢复测试夹具覆盖的出手概率。
    private readonly int _monsterId; // 受测怪物实体编号。
    private Phase _phase = Phase.OutOfSight; // 当前场景阶段。
    private double _phaseStart; // 当前阶段开始时间（秒）。
    private bool _placedOutOfSight; // 是否已将怪物放到视野外。
    private int _lastSegment = -1; // 上一帧攻击段编号。
    private string _lastAi = ""; // 上一次观测到的 AI 状态名。
    private string _lastAnim = ""; // 上一次观测到的动画路径。
    private readonly List<(double Time, string Ai)> _aiObserved = new(); // 按时间记录 AI 状态序列。
    private readonly List<(double Time, string Anim)> _animObserved = new(); // 按时间记录动画序列。
    private readonly List<(double Time, string Ai)> _aiBeforeSight = new(); // 进入视野前记录的 AI 状态序列。
    private readonly List<string> _failures = new(); // 断言失败原因列表。
    private int _heroHitsByMonster; // 怪物命中英雄的次数。
    private int _diedEvents; // 收到怪物死亡事件的次数。
    private double? _deathAiTime; // 死亡后AI时钟停止，尸体阶段只推进身体。
    private bool _hiddenAfterDeath; // 是否已观察到死亡后的实体回收。

    // 转身阶段
    private int _lockedFacing; // 转身测试中锁定的朝向。
    private double _recoveredAt = -1; // 收招硬直结束时间（秒）。
    private bool _turnedAfterRecovery; // 是否在收招后成功转向英雄。

    // 空中（平台）阶段
    private float _heroFloorY; // 英雄落地时的地面 Y 坐标。
    private float _airX; // 平台阶段固定的英雄 X 坐标。
    private float _airMinX; // 平台阶段怪物走位的最小 X 坐标。
    private float _airMaxX; // 平台阶段怪物走位的最大 X 坐标。
    private bool _sawUnreachableBehavior; // 是否观察到守候状态。
    private int _airAttacks; // 英雄在空中时怪物出招次数。
    private bool _struckAfterLanding; // 英雄落地后怪物是否已出招。

    // 视野外保留目标阶段
    private bool _retainedTarget; // 是否确认怪物在视野外保留存活目标。

    /// <summary>
    /// 场景已结束（通过或超时），驱动器据此调用 <see cref="Finish"/>。
    /// </summary>
    public bool IsDone => _phase == Phase.Done;

    /// <summary>
    /// 创建 AI 烟测场景并订阅战斗事件。
    /// </summary>
    /// <param name="hero">受测英雄实体。</param>
    /// <param name="monster">受测怪物实体。</param>
    public EnemyAiSmokeScenario(AbstractHeroEntity hero, AbstractEnemyEntity monster)
    {
        _hero = hero;
        _monster = monster;
        _monsterId = monster.Id;
        _originalAttackDesire = monster.Config.AttackDesire;
        // 本场验证行为能否恢复，不验证随机出手概率；只在测试侧固定，避免把连续未出手误判为卡死。
        typeof(GameConfig.Enemy.EnemyConfig).GetField("AttackDesire").SetValue(monster.Config, 100);
        GF.Event.Subscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
        GF.Event.Subscribe(EnemyDiedEventArgs.EventId, OnEnemyDied);
        CheckReach();
    }

    /// <summary>
    /// 每物理帧推进（t = 相对开始的秒数）。
    /// </summary>
    /// <param name="t">相对场景开始时间（秒）。</param>
    public void Update(double t)
    {
        if (t >= TimeoutAt && _phase != Phase.Done)
        {
            _failures.Add($"超时：停在阶段 {_phase}");
            _phase = Phase.Done;
            return;
        }

        int segment = _monster.IsShown ? _monster.AttackSegment : -1;
        bool attackStarted = segment >= 0 && _lastSegment < 0;
        _lastSegment = segment;

        switch (_phase)
        {
            case Phase.OutOfSight:
                if (!_placedOutOfSight)
                {
                    Teleport(OutOfSightOffset);
                    _placedOutOfSight = true;
                }

                if (t >= InSightAt)
                {
                    Teleport(InSightOffset);
                    HitMonster(1, Vector2.Zero);
                    Enter(Phase.WaitFirstHit, t);
                }

                break;

            case Phase.WaitFirstHit:
                if (_heroHitsByMonster > 0)
                {
                    _hero.Heal(GF.Fsm.GetModel<HeroModel>().MaxHp.Value);   // 防止后续阶段英雄被打死导致目标失效
                    Enter(Phase.WaitSecondAttack, t);
                }

                break;

            case Phase.WaitSecondAttack:
                if (attackStarted)
                {
                    // 出招当帧把英雄放到猴子身后：朝向必须锁到收招硬直结束
                    _lockedFacing = _monster.Facing;
                    float behind = _monster.GlobalPosition.X - _lockedFacing * BehindOffset;
                    _hero.GlobalPosition = new Vector2(behind, _hero.GlobalPosition.Y);
                    _hero.Velocity = Vector2.Zero;
                    GD.Print($"SMOKE-AI[{t:F2}] 转身测试：猴子出招朝向 {_lockedFacing}，英雄瞬移到身后 x={behind:F0}");
                    Enter(Phase.TurnAround, t);
                }

                break;

            case Phase.TurnAround:
                UpdateTurnAround(t);
                break;

            case Phase.Air:
                UpdateAir(t, attackStarted);
                break;

            case Phase.WaitLanded:
                // 看"正在出招"而不是起手沿：落地当帧就可能已提交出招（起手沿落在 Air 阶段的最后一帧）
                if (!_struckAfterLanding && segment >= 0)
                {
                    GD.Print($"SMOKE-AI[{t:F2}] 英雄落地后 {t - _phaseStart:F2}s 猴子出招");
                    _struckAfterLanding = true;
                }

                if (_struckAfterLanding || t - _phaseStart > LandedTimeout)
                {
                    _hero.Heal(GF.Fsm.GetModel<HeroModel>().MaxHp.Value);
                    Enter(Phase.Lose, t);
                }

                break;

            case Phase.Lose:
                UpdateLose(t);
                break;

            case Phase.WaitKill:
                if (t - _phaseStart >= KillDelay)
                {
                    HitMonster(_monster.MaxHp * 10f, Vector2.Zero);
                    Enter(Phase.WaitEnd, t);
                }

                break;

            case Phase.WaitEnd:
                if (t - _phaseStart >= EndDelay)
                {
                    Enter(Phase.Done, t);
                }

                break;
        }

        Sample(t);
    }

    /// <summary>
    /// 收尾断言，返回失败原因列表（空 = 通过）。
    /// </summary>
    /// <returns>断言失败原因；全部通过时为空列表。</returns>
    public List<string> Finish()
    {
        typeof(GameConfig.Enemy.EnemyConfig).GetField("AttackDesire").SetValue(_monster.Config, _originalAttackDesire);
        GF.Event.Unsubscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
        GF.Event.Unsubscribe(EnemyDiedEventArgs.EventId, OnEnemyDied);

        List<string> failures = new(_failures);
        if (!_aiBeforeSight.Exists(o => o.Ai is "Patrol"))
        {
            failures.Add("视野外期间没有观察到 Wander/Pause");
        }

        if (_aiBeforeSight.Exists(o => o.Ai is "Engage"))
        {
            failures.Add("视野外期间不应追击/攻击（索敌范围或目标锁定有误）");
        }

        RequireInOrder(failures, _aiObserved.ConvertAll(o => o.Ai), "AI", "Engage");
        RequireInOrder(failures, _animObserved.ConvertAll(o => o.Anim), "猴子身体/动画", "Walk/run", "Attack/attack_1", "Hurt/hurt", "Death/death");

        if (_heroHitsByMonster == 0)
        {
            failures.Add("猴子出招期间没有打中英雄（判定盒/物理层/出招链路）");
        }

        if (!_turnedAfterRecovery)
        {
            failures.Add("转身测试未完成：收招硬直结束后猴子没有转向身后的英雄");
        }

        if (_airAttacks > 0)
        {
            failures.Add($"英雄在头顶 {AirHeight}px 期间猴子出招了 {_airAttacks} 次（高度判定失效）");
        }

        if (!_struckAfterLanding)
        {
            failures.Add($"英雄落地 {LandedTimeout}s 内猴子没有出招（交战决策未恢复）");
        }

        if (!_retainedTarget)
        {
            failures.Add("英雄离开视野后猴子没有保留存活目标");
        }

        if (_diedEvents != 1)
        {
            failures.Add($"EnemyDiedEventArgs 应恰好广播 1 次，实际 {_diedEvents}");
        }

        if (_deathAiTime != _monster.AiData.ElapsedSeconds)
        {
            failures.Add("死亡后仍在推进AI时钟");
        }

        if (!_hiddenAfterDeath)
        {
            failures.Add("死亡动画后猴子没有被回收（HideEntity）");
        }

        GD.Print($"SMOKE-AI: AI 序列 {string.Join(" → ", _aiObserved.ConvertAll(o => $"{o.Ai}@{o.Time:F2}"))}");
        GD.Print($"SMOKE-AI: 动画序列 {string.Join(" → ", _animObserved.ConvertAll(o => $"{o.Anim}@{o.Time:F2}"))}");
        GD.Print($"SMOKE-AI: 猴子命中英雄 {_heroHitsByMonster} 次，英雄 HP {GF.Fsm.GetModel<HeroModel>().Hp.Value}/{GF.Fsm.GetModel<HeroModel>().MaxHp.Value}");
        return failures;
    }

    /// <summary>
    /// 切换场景阶段并记录阶段开始时间。
    /// </summary>
    /// <param name="phase">下一烟测阶段。</param>
    /// <param name="t">当前烟测累计时间，单位为秒。</param>
    private void Enter(Phase phase, double t)
    {
        _phase = phase;
        _phaseStart = t;
        GD.Print($"SMOKE-AI[{t:F2}] 阶段 → {phase}");
    }

    /// <summary>
    /// 校验出招和收招硬直期间锁定朝向，结束后恢复追踪。
    /// </summary>
    /// <param name="t">当前烟测累计时间，单位为秒。</param>
    private void UpdateTurnAround(double t)
    {
        if (_recoveredAt >= 0 && _monster.Facing == -_lockedFacing)
        {
            _turnedAfterRecovery = true;
            StartAir(t);
            return;
        }
        // 忙碌期间只允许攻击动画和原朝向；恢复后等待 AI 转向英雄。
        bool busy = _monster.AttackSegment >= 0 || SmokeInspection.InRecovery(_monster);
        if (busy)
        {
            if (_monster.Facing != _lockedFacing)
            {
                _failures.Add($"出招/收招硬直期间转身了（t={t:F2} seg={_monster.AttackSegment} 硬直={SmokeInspection.InRecovery(_monster)}）");
                Enter(Phase.Air, t);
                return;
            }

            if (_monster.AttackSegment >= 0 && !CurrentMonsterAnim().StartsWith("Attack/attack_"))
            {
                _failures.Add($"出招期间动画被切走：{CurrentMonsterAnim()}（t={t:F2}）");
            }

            return;
        }

        if (_recoveredAt < 0)
        {
            _recoveredAt = t;
            GD.Print($"SMOKE-AI[{t:F2}] 收招硬直结束（出招开始后 {t - _phaseStart:F2}s）");
        }

        if (_monster.Facing == -_lockedFacing)
        {
            _turnedAfterRecovery = true;
            GD.Print($"SMOKE-AI[{t:F2}] 硬直结束后 {t - _recoveredAt:F2}s 转向英雄");
            StartAir(t);
        }
        else if (t - _recoveredAt > TurnAroundWindow)
        {
            _failures.Add($"收招硬直结束 {TurnAroundWindow}s 后仍未转向身后的英雄");
            StartAir(t);
        }
    }

    /// <summary>
    /// 将英雄固定到怪物前方的空中测试点并开始平台阶段。
    /// </summary>
    /// <param name="t">当前烟测累计时间，单位为秒。</param>
    private void StartAir(double t)
    {
        _heroFloorY = _hero.GlobalPosition.Y;
        _airX = _monster.GlobalPosition.X - _monster.Facing * AirOffsetX;
        _airMinX = _airMaxX = _monster.GlobalPosition.X;
        _hero.Heal(GF.Fsm.GetModel<HeroModel>().MaxHp.Value);
        Enter(Phase.Air, t);
    }

    /// <summary>
    /// 校验目标在空中时的守候与踱步，结束后恢复攻击。
    /// </summary>
    /// <param name="t">当前烟测累计时间，单位为秒。</param>
    /// <param name="attackStarted">本帧是否观测到怪物开始攻击。</param>
    private void UpdateAir(double t, bool attackStarted)
    {
        // 每帧锁定英雄位置并收集怪物在目标两侧的移动证据。
        // 每帧重设位置（本驱动器在实体物理之后处理：猴子下一帧读到的就是这个位置）
        _hero.GlobalPosition = new Vector2(_airX, _heroFloorY - AirHeight);
        _hero.Velocity = Vector2.Zero;

        float mx = _monster.GlobalPosition.X;
        _airMinX = Mathf.Min(_airMinX, mx);
        _airMaxX = Mathf.Max(_airMaxX, mx);
        _sawUnreachableBehavior |= SmokeInspection.AiStateName(_monster) == "Engage";
        if (attackStarted && t - _phaseStart > 0.1)
        {
            _airAttacks++;
            GD.Print($"SMOKE-AI[{t:F2}] 英雄在头顶时猴子出招了（seg={_monster.AttackSegment}）");
        }

        if (t - _phaseStart < AirDuration)
        {
            return;
        }

        GD.Print($"SMOKE-AI[{t:F2}] 守候踱步范围 x∈[{_airMinX:F0},{_airMaxX:F0}]（英雄 x={_airX:F0}）");
        if (!_sawUnreachableBehavior)
        {
            _failures.Add($"英雄高度不可达时应保留交战状态并游荡，实际 {SmokeInspection.AiStateName(_monster)}");
        }

        _hero.GlobalPosition = new Vector2(_monster.GlobalPosition.X + 30, _heroFloorY);
        Enter(Phase.WaitLanded, t);
    }

    /// <summary>
    /// 将英雄保持在视野外，校验官方保留存活目标的规则。
    /// </summary>
    /// <param name="t">当前烟测累计时间，单位为秒。</param>
    private void UpdateLose(double t)
    {
        _hero.GlobalPosition = new Vector2(_monster.GlobalPosition.X + LoseOffset, _heroFloorY);
        _hero.Velocity = Vector2.Zero;
        if (!((IEnemyAiAgent)_monster).HasTarget)
        {
            _failures.Add("离开视野错误清除了仍存活的目标");
            Enter(Phase.WaitKill, t);
        }
        else if (t - _phaseStart > LoseTimeout)
        {
            _retainedTarget = true;
            GD.Print($"SMOKE-AI[{t:F2}] 视野外{LoseTimeout}s仍保留目标，符合官方");
            _hero.GlobalPosition = new Vector2(_monster.GlobalPosition.X + InSightOffset, _heroFloorY);
            HitMonster(1f, new Vector2(3, 0));
            Enter(Phase.WaitKill, t);
        }
    }

    /// <summary>
    /// 校验场景独立配置的AI交战范围。
    /// </summary>
    private void CheckReach()
    {
        Rect2 bounds = ((IEnemyAiAgent)_monster).EngageBounds;
        if (bounds != new Rect2(-80, -160, 160, 320))
        {
            _failures.Add($"猴子场景交战范围错误：{bounds}");
        }
    }

    /// <summary>
    /// 返回猴子身体状态与当前动画树节点的组合路径。
    /// </summary>
    /// <returns>当前身体状态与动画树节点的组合路径。</returns>
    private string CurrentMonsterAnim() => SmokeTestDriver.ObservePath(SmokeInspection.BodyStateName(_monster), _monster.AnimPlayer.AssignedAnimation.ToString());

    /// <summary>
    /// 记录 AI、动画变化及死亡后的实体回收。
    /// </summary>
    /// <param name="t">当前烟测累计时间，单位为秒。</param>
    private void Sample(double t)
    {
        // 先捕获死亡回收，再在实体仍可见时采样状态序列。
        if (!_hiddenAfterDeath && _diedEvents > 0 && !_monster.IsShown)
        {
            _hiddenAfterDeath = true;
            GD.Print($"SMOKE-AI[{t:F2}] 猴子已回收");
        }

        if (!_monster.IsShown)
        {
            return;
        }

        string ai = SmokeInspection.AiStateName(_monster);
        if (ai.Length > 0 && ai != _lastAi)
        {
            _lastAi = ai;
            _aiObserved.Add((t, ai));
            if (t < InSightAt)
            {
                _aiBeforeSight.Add((t, ai));
            }

            GD.Print($"SMOKE-AI[{t:F2}] AI {ai}  (dx={_hero.GlobalPosition.X - _monster.GlobalPosition.X:F0} "
                + $"move={((IEnemyBody)_monster).MoveIntent} body={SmokeInspection.BodyStateName(_monster)} seg={_monster.AttackSegment} dead={_monster.Dead})");
        }

        string anim = CurrentMonsterAnim();
        if (anim.Length > 0 && anim != _lastAnim)
        {
            _lastAnim = anim;
            _animObserved.Add((t, anim));
            GD.Print($"SMOKE-AI[{t:F2}] 猴子动画 {anim}  (facing={_monster.Facing} 硬直={SmokeInspection.InRecovery(_monster)})");
        }
    }

    /// <summary>
    /// 将怪物传送到英雄水平位置的指定偏移处。
    /// </summary>
    /// <param name="offset">怪物相对英雄的水平偏移，单位为像素。</param>
    private void Teleport(float offset)
    {
        var pos = new Vector2(_hero.GlobalPosition.X + offset, _monster.GlobalPosition.Y);
        _monster.GlobalPosition = pos;
        _monster.Velocity = Vector2.Zero;
        GD.Print($"SMOKE-AI: 猴子挪到 x={pos.X:F0}（英雄 x={_hero.GlobalPosition.X:F0}）");
    }

    /// <summary>
    /// 以英雄为攻击方对怪物执行一次真实伤害结算。
    /// </summary>
    /// <param name="power">本次攻击使用的威力值。</param>
    /// <param name="knockback">施加给怪物的击退速度，单位为像素/秒。</param>
    private void HitMonster(float power, Vector2 knockback)
    {
        // 攻击数据只在结算期间持有，完成后立即归还引用池。
        var data = AttackData.Create(0, default, power, DamageKind.Real, knockback, 1, 0, source: new GameLogic.Battle.Attacks.AttackSource(_hero.Id, GameConfig.Entity.EntityId.None, 0, 0));
        _monster.ReceiveHit(data);
        ReferencePool.Release(data);
    }

    /// <summary>
    /// 记录受测怪物命中英雄的伤害事件。
    /// </summary>
    /// <param name="sender">框架事件发送者。</param>
    /// <param name="args">当前同步回调借用的事件参数。</param>
    private void OnDamageDealt(object sender, GameEventArgs args)
    {
        if (args is DamageDealtEventArgs e && ReferenceEquals(sender, _hero) &&
            !e.Result.IsMiss)
        {
            _heroHitsByMonster++;
        }
    }

    /// <summary>
    /// 记录受测怪物死亡事件。
    /// </summary>
    /// <param name="sender">框架事件发送者。</param>
    /// <param name="args">当前同步回调借用的事件参数。</param>
    private void OnEnemyDied(object sender, GameEventArgs args)
    {
        if (args is EnemyDiedEventArgs e && e.EntityId == _monsterId)
        {
            _diedEvents++;
            _deathAiTime = _monster.AiData.ElapsedSeconds;
            if (SmokeInspection.ReadField<IFsm<IEnemyAiAgent>>(_monster, "AiFsm") != null)
            {
                _failures.Add("身体进入死亡后AI状态机仍未释放");
            }
        }
    }

    /// <summary>
    /// 校验观测序列按期望顺序包含所有状态。
    /// </summary>
    /// <param name="failures">本次验证累积的失败说明。</param>
    /// <param name="seq">按观察顺序记录的状态路径。</param>
    /// <param name="label">失败日志中用于定位本组序列的名称。</param>
    /// <param name="expected">本次参数组合对应的预期结果。</param>
    private static void RequireInOrder(List<string> failures, List<string> seq, string label, params string[] expected)
    {
        // 每个期望项只能从上一个匹配位置之后查找，保证顺序约束有效。
        int from = 0;
        foreach (string item in expected)
        {
            int index = seq.FindIndex(from, s => s == item);
            if (index < 0)
            {
                failures.Add($"{label}序列在 {string.Join("→", expected)} 中缺少（或顺序错误）{item}");
                return;
            }

            from = index + 1;
        }
    }

    /// <summary>
    /// AI 烟测的阶段状态。
    /// </summary>
    private enum Phase
    {
        /// <summary>
        /// 怪物位于英雄视野外。
        /// </summary>
        OutOfSight,

        /// <summary>
        /// 等待怪物首次命中英雄。
        /// </summary>
        WaitFirstHit,

        /// <summary>
        /// 等待下一次攻击开始。
        /// </summary>
        WaitSecondAttack,

        /// <summary>
        /// 验证出招和收招期间的朝向锁定。
        /// </summary>
        TurnAround,

        /// <summary>
        /// 验证英雄处于空中时的守候行为。
        /// </summary>
        Air,

        /// <summary>
        /// 等待英雄落地并验证恢复攻击。
        /// </summary>
        WaitLanded,

        /// <summary>
        /// 验证目标丢失后的巡逻恢复。
        /// </summary>
        Lose,

        /// <summary>
        /// 等待致命攻击。
        /// </summary>
        WaitKill,

        /// <summary>
        /// 等待死亡动画和实体回收。
        /// </summary>
        WaitEnd,

        /// <summary>
        /// 场景已完成。
        /// </summary>
        Done,
    }
}
