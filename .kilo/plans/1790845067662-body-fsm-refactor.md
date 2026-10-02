# 动画纯数据、代码唯一时钟（A 方案）+ 代码统一

在 `feature/body-fsm` 未提交改动上继续（round-3 已交付：AnimationPlayer 直驱 + animation_finished 转发 + OnAnimSound 轨道）。本轮按用户裁决改为 **A 方案：动画彻底不参与逻辑（连时间信号都不回调），代码单入口、单时钟**，并完成全项目代码统一。

## 决策依据（用户疑问 + 调研）

状态机拥有**控制流**，动画只拥有**时间数据**。本项目没有 AnimationTree 状态机，动画层无自己的控制流，可退化为"纯数据 + 视觉播放"：动作时长在初始化时从动画资源**读长度**（数据，编辑改长度自动同步、不可能失步），由身体状态自己计时。调研佐证 A 可行且被警示支持：Unity 社区警告"事件别用于核心玩法机制"（打断时收尾事件永不触发、末帧事件可能不触发，建议代码计时）；Godot 论坛有"末尾方法键偶发不调用"实战帖；Celeste 全代码计时。本项目约束（每角色独立动画库、代码状态机、现有音效全在第 0 帧、Godot 4.3+ 有动画标记）允许 A。

## A. 架构定稿

```
英雄：HeroInput(采样+缓冲) ─→ 身体状态机（唯一逻辑 + 唯一时钟）─→ Velocity / MoveAndSlide
                                   └─→ PlayAnim / RestartAnim ─→ AnimationPlayer（纯播放）
动作时长：OnInit 从动画资源读长度 → Params 快照 → 状态用物理 dt 计时（Elapsed 判到点）
音效：状态钩子触发（BeginAttack 含起手音 / PlayHurtSound / OnDied），音源 ID 查表
判定盒窗口：动画值轨道（M4 裁决不变——"哪一帧的几何"是数据）
将来中途时点（第 N 帧放子弹/脚步声）：Godot 动画标记（GetMarkerTime），代码读时间自己计时
```

**删除整条事件通道**：`AnimationFinished` 订阅、`m_FinishedAnim`、`TakeFinishedAnim`、`Tick` 的 `finishedAnim` 参数、`OnAnimFinished`、`OnAnimSound` + `SoundOfAnim`、两库的全部方法轨道。状态机单入口（Tick），动画从不调用代码。

**动作时长（状态计时，替代"等播完"）**：
- 英雄 Attack：每段时长 = 攻击动画长度（`AttackTimes[]`）；Hurt = hurt 长度；憨笑 = 其动画长度；
- 怪物 Attack：每招时长 = 攻击动画长度；Hurt / Death 同理（Death 播完请求回收一次）；
- Recovery 收招硬直仍按表的 `AiRecovery` 计时（与动画无关的数值）。

**音效钩子（时机 = 状态进入，与原第 0 帧打点完全等价）**：
- `IActorBody.BeginAttack(index)`：设攻击段 + 装填攻击包 + 播 `OwnAttacks[index].SoundId`（None = 无；怪物另做转向目标、计冷却、清移动意图）——round-3 现有钩子只加播音一行；
- `IActorBody.EndAttack()`：归还攻击包、段归 -1（打断/收招都在状态 `Leave`）；
- `IActorBody.PlayHurtSound()`：受击生效时（ApplyHurt）；
- `IActorBody.OnDied()`：死亡生效时——英雄播死亡语音；怪物关受击盒、播死亡音、广播 `MonsterDiedEventArgs`。
- 检查猴子 `AttackConfig.SoundId`：若非 None，round-3 现状已在播（轨道版），钩子版保持一致；若为 None 则无变化。

**行为变化**：收招/硬直结束回到 timer 语义（= 动画长度，±1 物理帧级别，round-2 冒烟验证过的时序）。

## B. 代码统一（全项目同类问题）

1. **`HeroAnims` 补全 + 常量化**：补 `Idle2`，`static readonly` → `const string`；`MonsterAnims` 同步。`WukongEntity.IdleFlavorAnim => HeroAnims.Idle2`。
2. **公共宿主接口 `IActorBody`**：提取 `IHeroBody`/`IMonsterBody` 重合成员（`Velocity`、`Dead`（统一叫 Dead）、`AttackSegment`、`BeginAttack`、`EndAttack`、`HasPendingHurt`/`TakePendingHurt`、`SetFacing`、`PlayAnim`、`RestartAnim`、`PlayHurtSound`、`OnDied`、`Gravity`）。角色接口只留专属（英雄：Params/Input/OnFloor/JumpCount/ComboIndex/NextRandom；怪物：Params/MoveIntent/TakeAttackRequest/RequestRecycle）。
3. **事实与数据上提 `ActorEntity`**：`OwnAttacks`（替英雄 `m_Combo`/怪物 `m_Attacks`）、`AttackSegment`（public get / protected set）、`m_PendingHurt`（+Take/Has/Clear）。
4. **`BodyState` 共享物理助手**：`where TBody : class, IActorBody`，`ApplyGravity/Stand` 从英雄/怪物状态基类上提；`MonsterBodyState` 只剩打断规则与前缀。
5. **常量化**：`MaxHopsPerFrame`、`TimeEpsilon`、`SecondJumpCount` 改 `const`。
6. **死代码清理**：`TickDown` 确认无调用后删；`GetAnimLength` 复活（Params 读长度）；清未用 using。
7. **测试假宿主去重**：`FakeActorBody` 基类实现 `IActorBody` 公共成员（速度/受击/攻击段/动画记录/音效计数）；`FakeHero`/`FakeMonster` 只留角色差异；`Finish(anim)` 删除，计时用 `Step` 推进。

## 改动清单

**C#**：`Entity/Body/`（BodyState 删事件入口、BodyFsm 回两参 Tick、重力助手上提）、`ActorEntity`（删事件通道与 OnAnimSound、上提事实、保留 PlayAnim/RestartAnim/ArmAttack/ReleaseAttack）、`IActorBody`(新)、`IHeroBody`/`IMonsterBody` 瘦身、`HeroBodyParams`（+AttackTimes/HurtTime/EmoteTime）、`MonsterBodyParams`（+AttackTimes/HurtTime/DeathTime）、英雄/怪物 5+5 状态改计时、`HeroEntity`/`MonsterEntity`（钩子 + Tick 两参）、`WukongEntity`、两个测试文件、冒烟测试（预期不变，`CheckAttackFinishTiming` ≥0.30 仍成立：0.35 长度 + 1 帧 ≈ 0.37）。

**动画库**（一次性临时脚本，改完确认 uid 头不变）：**删除全部方法轨道**（现有 OnAnimSound 键）→ 纯值轨道数据。不加任何新键。

**gen_animations.py**：删方法轨道生成路径（`METHOD_TRACK_MAP`/`_method_track`/`_ensure_sound_track`/`SOUND_ANIMS`/`_attack_lifecycle_tracks` 空壳），旧 `add_music` 轨道直接跳过并提示；注释改为"库 = 纯数据（帧/特效/判定盒值轨道）；时长与音效归代码"。

**validate_anim_libraries.gd**：**禁止一切方法轨道**（动画 = 纯数据）；保留：必需动画、`attack_<i>` 连续、attack/hurt/death/憨笑非循环（视觉卫生）、值轨道完备（含 RESET）。

**规范**：根 `AGENTS.md` 红线 7（"动画由 AnimationPlayer 直接播放，资源只含表现数据、无方法轨道；动作时长OnInit 读动画长度由状态计时；音效由状态钩子触发；将来中途时点用动画标记"）；`Entity/AGENTS.md`「状态与动画」重写（单入口 + 时长数据 + 钩子表 + 标记扩展点）；`EditorScripts/AGENTS.md` 校验规则；`EngineeringStandards.md` §6 对应行；`LegacyAssetMap.md` 注记补一句。

## 验证

- `dotnet build` + `--build-solutions`；`dotnet test` 全绿（计时语义测试：连段间隔 = AttackTimes、硬直 = HurtTime、回收一次等）；
- `validate_anim_libraries.gd` 退出码 0；
- `--smoketest` / `--smoketest=ai` stdout `SMOKE PASS`（hurt 6.38 + 0.28 ≈ 6.66 结束 < 6.70 跳；连段间隔 ≈0.37 ≥ 0.30）；
- grep 无残留：`finishedAnim|OnAnimFinished|TakeFinishedAnim|AnimationFinished|OnAnimSound|SoundOfAnim|BeginAttackSegment|EndAttackSegment`（文档历史段落除外）；
- 编辑器人工确认：Animation 面板无方法轨道、判定盒值轨道齐全；试玩连段/受击/死亡音效与手感。
