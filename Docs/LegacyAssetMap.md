# LegacyAssetMap —— 旧项目素材迁移映射

> 来源：`P:\Godot-Project\ZMXY_BHYH`（只读素材库，见 AGENTS.md §11.1）
> 目标：`Godot/GodotProject/TheGame/`
> 迁移原则见 AGENTS.md §11.2 / §11.3：仅搬当前阶段所需；战斗图集必须重命名为 `<entity>_<state>`（Collection Res 全树 basename 唯一，见 Sprites/AGENTS.md）。

## 当前阶段范围（M1）

`wukong` + `huaguoshan_monkey`（旧 Monster1）+ `Level_1`（花果山）。

## 贴图映射

| 旧路径 | 新路径 | 规格 / 说明 |
| --- | --- | --- |
| `Art/HeroPicture/Role1AllEquipment/Role_1_Body_Empty.png` | `Sprites/Characters/Heroes/wukong/wukong_body.png` | 1200×2800，6 列 × 14 行，200×200 / 帧（84 帧） |
| `Art/HeroPicture/Role1AllEquipment/Role_1_Eq_Empty.png` | `Sprites/Characters/Heroes/wukong/wukong_weapon_empty.png` | 同上网格；**武器层**（空手），见下「武器层」 |
| `Art/Monster/Monster1/Wait.png` | `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_idle.png` | 43×63，1 帧 |
| `Art/Monster/Monster1/Walk.png` | `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_run.png` | 64×88 × 4 帧 |
| `Art/Monster/Monster1/Hit.png` | `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_attack.png` | 91×83 × 6 帧 |
| `Art/Monster/Monster1/Hurt.png` | `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_hurt.png` | 68×86 × 2 帧 |
| `Art/Monster/Monster1/Death.png` | `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_death.png` | 91×88 × 6 帧 |
| `Art/Level/Level_1/19_1.png` | `Sprites/Levels/huaguoshan/level_1_bg_end.png` | 1440×690；旧 `BackGround/End/end2` 背景端块 |
| `Art/Level/Level_1/48.png` | `Sprites/Levels/huaguoshan/level_1_front.png` | 4957×633；旧 `BackGround/front` 前景视差层 |
| `Art/Level/Level_1/183.png` | `Sprites/Levels/huaguoshan/level_1_floor.png` | 4812×170；旧 `BackGround/floor3/floor2` 地板视差层 |
| `Art/HeroPicture/Role1SpecialEffect/Role1Hit1.png` | `Sprites/Effects/wukong/wukong_hit_1.png` | 210×206 × 5 帧；普攻 1 段棍气 |
| `Art/HeroPicture/Role1SpecialEffect/Role1Hit2.png` | `Sprites/Effects/wukong/wukong_hit_2.png` | 159×54 × 2 帧；普攻 2 段棍气 |
| `Art/HeroPicture/Role1SpecialEffect/Role1Hit3.png` | `Sprites/Effects/wukong/wukong_hit_3.png` | 326×66 × 4 帧；普攻 3 段棍气 |
| `Art/HeroPicture/Role1SpecialEffect/Role1Hit4.png` | `Sprites/Effects/wukong/wukong_hit_4.png` | 315×82 × 2 帧；普攻 4 段棍气 |

不拷贝旧 `.import`，由新工程重新导入（AGENTS.md §11.3.1）。

## 武器层（换装测试用）

旧项目武器是**独立叠加层**：节点 `Action/RoleEquipment` ← `Role_1_Eq_<武器键>.png`，运行时由 `BaseHero.onEqchange()` → `ChangeEq()` → `Global.LoadRole1EQ()` 按当前装备切换（同网格 6×14，动画帧与身体层对齐）。本次先迁 5 张代表性图，其余 15 把按需再迁：

| 旧文件 | 新文件 | 旧键 / 名称 |
| --- | --- | --- |
| `Role_1_Eq_Empty.png` | `wukong_weapon_empty.png` | 空手 |
| `Role_1_Eq_ryjgb.png` | `wukong_weapon_golden_cudgel.png` | `ryjgb` 如意金箍棒 |
| `Role_1_Eq_wkjdyhwq.png` | `wukong_weapon_wooden_stick.png` | `wkjdyhwq` 经典原画·木棍 |
| `Role_1_Eq_qld.png` | `wukong_weapon_dragon_blade.png` | `qld` 青龙刀 |
| `Role_1_Eq_zjbtg.png` | `wukong_weapon_purple_gold_cudgel.png` | `zjbtg` 紫金镔铁棍 |

每张武器图集对应一个 `wukong_weapon_<name>_animations.tres`（9 个动画，帧序列取自 `Action/RoleEquipment:frame`，与身体层同帧对齐）。

## 动画映射

源数据为旧 `.tscn` 内嵌 `Animation`/`AtlasTexture`，由 `Tools/LegacyMigration/gen_animations.py` 一次性解析生成，禁止手工重切片（AGENTS.md §11.3.4）。

### wukong —— `Sprites/Characters/Heroes/wukong/wukong_animations.tres`

源：`Scene/Hero/Role_1/Role1.tscn`，轨道 `Action/RoleBody:frame`，`RolePlayer.speed_scale = 2.0`。

| 旧动画名 | 新动画名 | 循环 |
| --- | --- | --- |
| `wait` | `idle` | 是 |
| `run` | `run` | 是 |
| `jump1` | `jump` | 否 |
| `hit1` | `attack_1` | 否 |
| `hit2` | `attack_2` | 否 |
| `hit3` | `attack_3` | 否 |
| `hit4` | `attack_4` | 否 |
| `hurt` | `hurt` | 否 |
| `death` | `death` | 否 |

暂缓（技能/特效类，M1 不迁）：`drop` `hmz__` `hyjj` `hytj` `jdy` `jdy_1` `lyfb` `lys` `qsez` `slz` `zz`。

### huaguoshan_monkey —— `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_animations.tres`

源：`Scene/Monster/Monster_1.tscn` 的 `AtlasTexture` 切分规格，按固定网格重新生成。

| 旧动画名 | 新动画名 | 帧数 | 循环 |
| --- | --- | --- | --- |
| `Wait` | `idle` | 1 | 是 |
| `Walk` | `run` | 4 | 是 |
| `Hit` | `attack_1` | 6 | 否 |
| `Hurt` | `hurt` | 2 | 否 |
| `Death` | `death` | 6 | 否 |

## 棍气特效层（旧 `Action/SpecialEffect`）—— 1:1 迁移

旧项目普攻的"棍气"是英雄场景里的**独立 AnimatedSprite2D**，由旧 AnimationPlayer 的四类**属性轨道**驱动：

| 轨道 | 含义 |
| --- | --- |
| `Action/SpecialEffect:animation` | 切换特效动画名（切换时 SpriteFrames 的 frame 归 0） |
| `Action/SpecialEffect:frame` | 逐帧；值是"条目序号"，**含 null 空白条目** |
| `Action/SpecialEffect:offset` | 特效相对身体原点的位移（绝对值，覆盖节点 offset） |
| `Action/SpecialEffect:scale` | 缩放（普攻无此轨道，沿用之前动画留下的 1） |

关键事实（曾是迁移 bug 的来源，见 `Tools/LegacyMigration/dump_legacy_effect_raw.py`）：
- 每个棍气动画的**末条是 null**（hit1 6 条、hit2 7 条、hit3 6 条、hit4 3 条）——帧号打到最后一条时特效消失，这就是收招"棍气散掉"。丢掉 null 会导致收招时特效不消失、且帧号整体错位。
- 特效的位移写在节点 `offset` 上，而朝向镜像写在父节点 `Action.scale.x = ±1`——**镜像会连带翻转 offset**，所以新工程的镜像必须做在**父容器**上，不能用 `FlipH`。
- 时间轴是内部时间，真实秒 = 按 speed_scale 积分（与身体层同一套换算）。

新工程结构（`Entitys/WukongEntity.tscn`）：

```
m_EffectRoot (Node2D)          ← 朝向镜像：scale.x = ±1（同旧 Action 节点）
└── m_Effect (AnimatedSprite2D) ← SpriteFrames: wukong_effect_animations.tres
m_EffectPlayer (AnimationPlayer) ← AnimationLibrary: wukong_effect_library.tres
```

生成物（由 `gen_animations.py` 输出）：

| 文件 | 内容 |
| --- | --- |
| `Sprites/Effects/wukong/wukong_effect_animations.tres` | SpriteFrames：`attack_1..4`（条目序列含 null，逐条对应旧数据）+ `empty`（对应旧空白 `wait`） |
| `Sprites/Effects/wukong/wukong_effect_library.tres` | AnimationLibrary：同名动画，轨道路径 `m_EffectRoot/m_Effect:<属性>`，键时间已换算为真实秒 |

棍气轨道数据（旧 → 新，真实秒）：

| 动画 | frame 序列 | offset | 时长 |
| --- | --- | --- | --- |
| `attack_1` | 0,1,2,3,4,5(null) | (-15,-10) → (0,0) | 0.350s |
| `attack_2` | 0,2,2,2,4,4,6(null) | (-40,40) → (0,0) | 0.317s |
| `attack_3` | 0,0,1,2,3,5(null),5(null) | (-10,40) → (0,0) | 0.317s |
| `attack_4` | 2(null),0,0,1,1,2(null) | (-45,40) → (0,0) | 0.317s |

`AttackConfig` 里普攻的 `Interval`（0.35s）与身体动画长度一致，特效与身体同起同收。

未迁移：旧场景还有第二个特效层 `Action/SpecialEffect2`（技能用，SpriteFrames = SubResource 125，含 `hyjj` 等）+ `SpecialEffect` 的技能特效动画（`slz/lys/hytj/lyfb/jdy/jdy_1/zz/zz_2/hyjj_1/hyjj_2`），随技能系统一起迁。

## 音效映射

旧项目音效是"节点 `Sound_` 一次性实例 + mp3 路径"，触发点写在**动画的 method 轨道**（`add_music(idx)`）。新工程走 `GF.Sound.PlaySFX(path)`（SFX 组），路径由 `SoundConfig` 表承载，业务表只引用 `SoundId` 枚举。

接线以旧代码实测为准（**旧文件名与实际用途不一致**）：`Role1.tscn` 的 method 轨道里 hit1→39、hit2→40、hit3→39、hit4→38；死亡→59；受击音 49 在被击逻辑里播（`BaseHero.gd:593`）。

| 旧文件 | 新路径 | `SoundId` | 语义与触发 |
| --- | --- | --- | --- |
| `Music/Hero/40_Role1_hit1AndHit2.mp3` | `Audios/SFX/wukong/wukong_attack_2.mp3` | `WukongAttack2` | 挥棍起手（普攻第 2 段）；旧文件名与实际用法不符，以 method 轨道实测为准 |
| `Music/Hero/39_Role1_hit3AndHit4.mp3` | `Audios/SFX/wukong/wukong_attack_1_3.mp3` | `WukongAttack1And3` | 挥棍起手（普攻 1、3 段共用） |
| `Music/Hero/38_Role1_hit5.mp3` | `Audios/SFX/wukong/wukong_attack_4.mp3` | `WukongAttack4` | 挥棍起手（普攻第 4 段） |
| `Music/Hero/49_Role1_beAttack.mp3` | `Audios/SFX/wukong/wukong_hurt.mp3` | `WukongHurt` | **受害者**的受击语音（`BaseHero.gd:592` 按 `self` 选音） |
| `Music/Hero/59_Role1_dead.mp3` | `Audios/SFX/wukong/wukong_death.mp3` | `WukongDeath` | 死亡语音（旧 death 动画的 `add_music(7)`） |
| `Music/MonsterHurt/6_BeattackByRole1.mp3` | `Audios/SFX/wukong/wukong_hit_impact.mp3` | `WukongImpact` | **攻击者**的命中音（`BaseMonster.gd:652` 按攻击者选音）；旧名 `monster_hurt` 是错的——它不是怪物语音，是"悟空的棍打中东西"的声音 |

**关键语义纠正（2026-09-28 读旧代码确认）**：命中音属于**攻击方**（谁打的），受击语音属于**受害方**（谁挨打）。旧项目把命中音的**选择**写在怪物的被击逻辑里、且用 `is role1` 硬编码链（旧项目要抛弃的模式），但**语义归属是攻击者**。因此 `WukongImpact` 配在攻击表上（`AttackConfig.HitSoundId`），不配在怪物身上；旧项目也没有任何怪物语音素材。

表设计（2026-09-28 定稿，最终版）：

| 表 | 职责 | 关键列 |
| --- | --- | --- |
| `SoundConfig` | **唯一**音频表：音效资产登记 | `Id`(枚举值) / `Key`(枚举名) / `NameCn` / `Desc` / `LegacyId` / `Group`(框架组名 Music/SFX/UI) / `Path` |
| `AttackConfig.SoundId` / `HitSoundId` | 攻击的起手音 / 命中音 | 攻击自己的属性（与 `Animation` 同类） |
| `HeroConfig.HurtSoundId` / `DeathSoundId` | 角色受击/死亡语音 | 受害方向 |
| `MonsterConfig.HurtSoundId` / `DeathSoundId` | 同上（今天填 `None`：旧项目无怪物语音素材，M5 用） | |
| `LevelConfig.BgmSoundId` | 关卡 BGM（替换旧的裸路径列 `BgmPath`，M6 接） | |

- 规则一句话：**触发者在哪一行，SoundId 就配在哪一列**（与框架实体/界面表配 `AssetPath` 的先例一致）。
- **框架的统一入口**是 `GF.Sound.PlaySound(资源, 组名)`（`SoundComponent.cs:153`）：组名就是 `SoundGroupRes.tres` 注册的 `Music`/`SFX`/`UI`，各自映射到 Godot 总线（独立音量/静音/代理数）。所以表里直接存框架组名，代码一句 `GF.Sound.PlaySound(cfg.Path, cfg.Group)`（`ActorEntity.PlaySound`）就能播任意组的一次性声音，**不需要自定义组枚举或路由 switch**。
- `SoundId` 的值**显式写死**（0..6）：自动递增会让"中间插一条音效"导致后续 Id 全部漂移。
- 只读查询：`GameScripts/Config/SoundConfigQuery.cs`（单个 `Get(SoundId)`，`Config/` 只读不写）。BGM 在关卡代码里用框架的 `PlayBGM`（带"停上一首"语义）。

未迁移：其余技能音效（`1_/8_/11_/17_/18_/26_/27_/35_/36_/37_/42_/45_/44_` 等）、BGM（`Music/level`、`Music/MainSceneMusic`）随技能系统与 M6 关卡接入。

音效命名遗留：当前枚举/文件按**用途**命名（`WukongAttack2` 等），是旧项目"文件名与实际用途不一致"教训的延续——将来被其他角色复用时名字会失真。后续应改为按"声音本身"命名（如 `WukongSwingLight`），用途只留在绑定表。

## 动画架构迁移（2026-09-28：AnimationPlayer + AnimationTree，角色属性驱动）

角色动画从"AnimatedSprite2D + C# 状态机（GF.Fsm）"迁到"AnimationPlayer 属性轨道 + AnimationTree 表达式状态机"，时序/帧数据与旧项目 Role1.tscn 逐帧一致：

- **身体/武器层**：AnimatedSprite2D + SpriteFrames → `Sprite2D(hframes=6, vframes=14)`，帧号即 6×14 网格全局序号——与旧 `Action/RoleBody:frame` / `Action/RoleEquipment:frame` 轨道完全同构，换装改为换 Texture。
- **合并动画库** `Sprites/Characters/Heroes/wukong/wukong_anim_library.tres`（`gen_animations.py` 的 `gen_wukong_library()` 生成）：每个动画含身体帧轨道、武器帧轨道、特效四件套轨道、方法轨道。**轨道完备性**：每个被动画的属性（m_Body:frame / m_Weapon:frame / m_Effect 的 animation/frame/offset/scale）在库内**每个**动画都必须有轨道——AnimationTree 切到不含某属性轨道的动画时会把该属性重置成垃圾值（实测 scale 被写成 1e-05，棍气不可见）。攻击段特效轨道取旧数据（含末尾 null 收招帧、offset），非攻击段统一"切空白 empty + 帧归零 + scale=1"。方法轨道：旧 `add_music` → `OnAttackSwingSound`（hit1..4）/ `OnDeathVoice`（death），触发时机取旧轨道，音源按当前段查 `AttackConfig.SoundId` / `HeroConfig.DeathSoundId`。技能类特效未迁，随技能系统处理。
- **状态机** `Entitys/wukong_animation_tree.tres`（`EditorScripts/build_wukong_anim_tree.gd` 生成）：**单层完全图**，13 个动画节点（idle1 / idle2 / walk / run / jump / jump_2 / fall / attack_1..4 / hurt / death）两两相连；**每条边 = 目标动画的完整成立条件**（`advance_expression`，只读角色属性），条件互斥——谁成立就停在谁那。图里没有布尔参数、没有脉冲、没有"状态序号"；"出招期间受击不打断"这类规则直接写在条件里（见生成器 `STATES` 表）。**为什么不用嵌套子机**：4.7.2 实测嵌套子机内部的转移比父级转移**晚一帧**生效（子机条件要等下一帧它自己被 process），攻击连段推进、走跑切换、跳→落会各慢 1 帧；单层图没有这个延迟（实测属性变化当帧动画即切换）。代价是图在编辑器里是一张密网——它是生成物，逻辑看生成器，不要手连。
- **属性驱动分工**：
  - C#（`HeroEntity`）只维护**角色属性**并暴露为 `[Export]` 字段（表达式事实面）：`MoveInput / Running / Airborne / Rising / JumpCount / AttackSegment / Hurt / Emoting / Dead`，每物理帧由 `SyncAnimFacts()` 刷新一次（其中 Airborne/Rising/Hurt/Emoting/Dead 由物理与计时器派生）；
  - **基类 `ActorEntity` 不含任何角色事实**，只把 AnimationTree 的表达式基对象指向实体节点；角色属性、状态机资源都归角色自己；
  - 各角色的状态机资源自行把属性映射到自己的动画名（wukong 专属动画名只出现在它的 .tres 与 `WukongEntity.IdleFlavorAnim` 覆写里）；新英雄 = 新动画库 + 新状态机资源 + 新 HeroConfig 行，基类零改动。
- **本引擎版本（4.7.2）状态机实测语义**（探针 + 源码验证，写状态机必须遵守）：
  1. 表达式边必须 `advance_mode=AUTO`；表达式在状态内**持续**评估，命中即转移；
  2. 转移在触发它的那次 process 里**同帧**生效（单层图）；嵌套子机内部转移晚一帧（见上）；
  3. 多条边同时为真时取 `priority` 最小者、同值取**后**加入者——本项目用互斥条件规避，不依赖它；
  4. `advance_expression` 读 C# 成员必须走 `[Export]` 字段（普通属性引擎侧不可见），基对象由 `AnimationTree.AdvanceExpressionBaseNode` 指向实体；`and`/`or`/`not` 可用；表达式在 setter 里解析后缓存，逐帧评估不重复解析；
  5. 表达式里引用**引擎自带**数据必须用引擎名：`is_on_floor()` / `velocity.y` 可用；写成 C# 风格的 `IsOnFloor()` / `Velocity` 会静默求值为 null（条件恒假）——迁移后"跑/跳动画失效"的根因就是它（`Ground → Air` 的 `not IsOnFloor()` 恒假，树永远出不了地面组）；本项目改用 C# 属性表达，图上不再出现引擎名；
  6. 嵌套子机重入**不回到 Start**（沿用上次的内部状态）——组式写法必须自带"完全图自愈"，本项目直接不用嵌套。
- **冒烟测试**（自动化验证动画链路，替代"看日志猜"）：`Godot4CSharp_console.exe --headless --path Godot/GodotProject --quit-after 1500 -- --smoketest`；`TheGame/MainPack/Scripts/Debug/SmokeTestDriver.cs`（autoload，仅 `--smoketest` 时启用）自动注入连打/双击跑/一段跳/二段跳输入，断言 AnimationTree **真正在播**的动画节点序列（attack_1..4 → run → jump → fall → jump_2 → idle1）、顺序，以及待机时特效层回到空白（动画 empty / 帧 0 / scale 1）；退出码 0=通过 1=失败（失败时打印完整观察序列）。已验证：连段、双击跑、一段跳、二段跳、落地回待机、憨笑、特效归位全部通过。
- 旧 SpriteFrames 三件套（`wukong_animations.tres` / `wukong_weapon_*_animations.tres` / `wukong_effect_library.tres`）场景不再引用（effect 的 SpriteFrames 仍被 m_Effect 使用），保留作为帧序列数据参照。

## 复现方式

```
python Tools/LegacyMigration/gen_animations.py
S:\Godot4\Godot4CSharp_console.exe --headless --path Godot/GodotProject --script res://EditorScripts/build_wukong_anim_tree.gd
```

脚本读取旧项目 `.tscn`，输出到 `TheGame/Sprites/...` 与 `TheGame/Entitys/`；旧项目保持只读，不写入任何文件。

## 待办

- `wukong` 的 `hurt` / `death` 在旧动画中仅单帧（旧工程另有 `RoleDeath.png` 等独立节点），后续接入死亡表现时再评估。
- `huaguoshan_monkey` 帧时长当前统一取 0.1s（旧 `AnimatedSprite2D.speed` 语义），M5 调 AI 时按手感回填。
- 武器层已迁 5 张（含空手）用于换装测试；其余 15 把武器与 25 套防具图集待装备系统阶段按 Luban 表按需迁入。
- 身体层 `wukong_body.png` 的部分动作帧自带默认棍（旧美术遗留）；接武器层后需确认是否与 `wukong_weapon_*` 叠加导致重复，必要时清理身体层里的武器像素。
- 技能特效与技能音效（见上两节"未迁移"）随技能系统一起迁；`Action/SpecialEffect2` 第二特效层同理。
- 旧项目角色/怪物另有 `MonsterBeHurt_*`、`MissEffect`、`RoleBeHit` 等打击/未命中特效（`Art/StrikeSpecialEffects/`、`Scene/hittest/`），M4 做命中判定时迁。
