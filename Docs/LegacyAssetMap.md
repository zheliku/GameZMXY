# LegacyAssetMap —— 旧项目素材迁移映射

> 来源：`P:\Godot-Project\ZMXY_BHYH`（只读素材库，见 AGENTS.md §11.1）
> 目标：`Godot/GodotProject/TheGame/`
> 迁移原则见 AGENTS.md §11.2 / §11.3：仅搬当前阶段所需；战斗图集必须重命名为 `<entity>_<state>`（Collection Res 全树 basename 唯一，见 Sprites/AGENTS.md）。

## 当前阶段范围（M1）

`wukong` + `huaguoshan_monkey`（旧 Monster1）+ `demon_monkey`（旧 Monster2，妖猴）+ `Level_1`（花果山）+ `TestArena`（怪物与角色测试关卡）+ `gogo`（旧 `Art/Level/Gogo` 前进提示）。

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
| `Art/Monster/Monster2/Wait.png` | `Sprites/Characters/Monsters/demon_monkey/demon_monkey_idle.png` | 74×114 × 5 帧 |
| `Art/Monster/Monster2/Walk.png` | `Sprites/Characters/Monsters/demon_monkey/demon_monkey_run.png` | 116×105 × 4 帧 |
| `Art/Monster/Monster2/Hit.png` | `Sprites/Characters/Monsters/demon_monkey/demon_monkey_attack.png` | 130×134 × 4 帧 |
| `Art/Monster/Monster2/Hurt.png` | `Sprites/Characters/Monsters/demon_monkey/demon_monkey_hurt.png` | 89×87 × 1 帧 |
| `Art/Monster/Monster2/Death.png` | `Sprites/Characters/Monsters/demon_monkey/demon_monkey_death.png` | 124×108 × 5 帧 |
| `Art/Level/Level_1/19_1.png` | `Sprites/Levels/huaguoshan/level_1_bg_end.png` | 1440×690；旧 `BackGround/End/end2` 背景端块 |
| `Art/Level/Level_1/48.png` | `Sprites/Levels/huaguoshan/level_1_front.png` | 4957×633；旧 `BackGround/front` 前景视差层 |
| `Art/Level/Level_1/183.png` | `Sprites/Levels/huaguoshan/level_1_floor.png` | 原 4812×170；旧 `BackGround/floor3/floor2` 地板视差层。**已裁至 4700×170**：右侧 112px 圆角收尾（x≥4716 顶面下坠、右下透明）会露出背景，相机右界随之定在 4700 |
| `Art/Level/Gogo/1..67.png` | `Sprites/UI/gogo/gogo_sheet.png` + `gogo_animations.tres` | 67 帧 213×92 打包为 8×9 图集；旧 `Role_information` 的 `Gogo`（`AnimatedSprite2D`，speed 25、循环） |
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

每张武器图集对应一个 `wukong_weapon_<name>_animations.tres`（9 个动画，帧序列取自 `Action/RoleEquipment:frame`，与身体层同帧对齐）——这些武器 SpriteFrames 已于 2026-09-30 删除（零引用，见文末说明），帧数据由合并动画库承载。

## 动画映射

源数据为旧 `.tscn` 内嵌 `Animation`/`AtlasTexture`，由 `Tools/LegacyMigration/gen_animations.py` 一次性解析生成，禁止手工重切片（AGENTS.md §11.3.3）。

### wukong —— `Sprites/Characters/Heroes/wukong/wukong_animations.tres`（2026-09-30 已删除，内容并入 `wukong_anim_library.tres`）

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

### demon_monkey（妖猴，旧 Monster_2）—— `Sprites/Characters/Monsters/demon_monkey/demon_monkey_animations.tres`

源：`Scene/Monster/Monster_2.tscn` 的 `AtlasTexture` 切分规格，按固定网格重新生成；身体层与判定盒同原点（不额外下移），动画库见 `Entitys/Animations/demon_monkey_anim_library.tres`。

| 旧动画名 | 新动画名 | 帧数 | 循环 |
| --- | --- | --- | --- |
| `wait` | `idle` | 5 | 是 |
| `walk` | `run` | 4 | 是 |
| `hit1` | `attack_1` | 4 | 否 |
| `hurt` | `hurt` | 1 | 否 |
| `death` | `death` | 5 | 否 |

旧 `hit1` 判定窗 0.10~0.40s；旧 `hit1` 判定盒圆 r=46.0109 @(−31,−4) → 新 80×80 @(−31,−4)（编辑器调校；攻击与静止同值，库含 RESET 默认动画）。

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
m_EffectPlayer (AnimationPlayer) ← AnimationLibrary: wukong_effect_library.tres   ← M3 中期结构；2026-09-30 该库已删，棍气轨道现由 wukong_anim_library.tres 承载
```

生成物（由 `gen_animations.py` 输出）：

| 文件 | 内容 |
| --- | --- |
| `Sprites/Effects/wukong/wukong_effect_animations.tres` | SpriteFrames：`attack_1..4`（条目序列含 null，逐条对应旧数据）+ `empty`（对应旧空白 `wait`） |
| `Sprites/Effects/wukong/wukong_effect_library.tres` | AnimationLibrary：同名动画，轨道路径 `m_EffectRoot/m_Effect:<属性>`，键时间已换算为真实秒（2026-09-30 已删除） |

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

旧项目音效是"节点 `Sound_` 一次性实例 + mp3 路径"，触发点写在**动画的 method 轨道**（`add_music(idx)`）。新工程走 `GF.Sound.PlaySound(path, group)`（组名来自 `SoundConfig` 表），业务表只引用 `SoundId` 枚举。

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

- 规则一句话：**触发者在哪一行，SoundId 就配在哪一列**（与框架实体/界面表配 `AssetPath` 的先例一致）。
- **框架的统一入口**是 `GF.Sound.PlaySound(资源, 组名)`（`SoundComponent.cs:153`）：组名就是 `SoundGroupRes.tres` 注册的 `Music`/`SFX`/`UI`，各自映射到 Godot 总线（独立音量/静音/代理数）。所以表里直接存框架组名，代码一句 `GF.Sound.PlaySound(cfg.Path, cfg.Group)`（`ActorEntity.PlaySound`）就能播任意组的一次性声音，**不需要自定义组枚举或路由 switch**。
- `SoundId` 的值**显式写死**（0..6）：自动递增会让"中间插一条音效"导致后续 Id 全部漂移。
- 只读查询通过 `ActorEntity.PlaySound` 直接读取 `SoundConfig`；BGM 与关卡流程暂不接入。

未迁移：其余技能音效（`1_/8_/11_/17_/18_/26_/27_/35_/36_/37_/42_/45_/44_` 等）随技能系统接入；BGM（`Music/level`、`Music/MainSceneMusic`）待后续流程设计。

音效命名遗留：当前枚举/文件按**用途**命名（`WukongAttack2` 等），是旧项目"文件名与实际用途不一致"教训的延续——将来被其他角色复用时名字会失真。后续应改为按"声音本身"命名（如 `WukongSwingLight`），用途只留在绑定表。

## 动画架构迁移（2026-09-28：AnimationPlayer + AnimationTree，角色属性驱动）

> **2026-10-01 已被取代**：AnimationTree 与表达式事实面已移除，改为"代码身体状态机（GF.Fsm 物理帧驱动）+ AnimationPlayer 直驱"——最终形态为**动画纯数据**（帧/特效/判定盒值轨道，无方法轨道、从不调用代码）：动作时长 OnInit 读动画长度由状态计时，音效由状态钩子触发（音源查表），两份 `*_animation_tree.tres` 已删除。现行契约见 `GameScripts/Entity/AGENTS.md`「状态与动画」；本节保留作迁移记录。

角色动画从"AnimatedSprite2D + C# 状态机（GF.Fsm）"迁到"AnimationPlayer 属性轨道 + AnimationTree 表达式状态机"，时序/帧数据与旧项目 Role1.tscn 逐帧一致：

- **身体/武器层**：AnimatedSprite2D + SpriteFrames → `Sprite2D(hframes=6, vframes=14)`，帧号即 6×14 网格全局序号——与旧 `Action/RoleBody:frame` / `Action/RoleEquipment:frame` 轨道完全同构，换装改为换 Texture。
- **合并动画库** `Entitys/Animations/wukong_anim_library.tres`（`gen_animations.py` 的 `gen_wukong_library()` 生成）：每个动画含身体帧轨道、武器帧轨道、特效四件套轨道、方法轨道。**轨道完备性**：每个被动画的属性（m_Body:frame / m_Weapon:frame / m_Effect 的 animation/frame/offset/scale）在库内**每个**动画都必须有轨道——AnimationTree 切到不含某属性轨道的动画时会把该属性重置成垃圾值（实测 scale 被写成 1e-05，棍气不可见）。攻击段特效轨道取旧数据（含末尾 null 收招帧、offset），非攻击段统一"切空白 empty + 帧归零 + scale=1"。方法轨道：旧 `add_music` → `OnAttackSwingSound`（hit1..4）/ `OnDeathVoice`（death），触发时机取旧轨道，音源按当前段查 `AttackConfig.SoundId` / `HeroConfig.DeathSoundId`。技能类特效未迁，随技能系统处理。
- **状态机** `Entitys/Animations/wukong_animation_tree.tres`（`EditorScripts/build_wukong_anim_tree.gd` 生成）：**主图分组 + 子状态机收纳动画**（2026-09-28 定稿，取代单层完全图）：
  - 主图只有组与单状态：`Ground`（子机：`Idle`（子机：idle1 / idle2）、walk、run）、`Air`（子机：jump / jump_2 / fall）、`Attack`（子机：attack_1..4）、`Hurt`、`Death`；
  - **进组 = 从 Start 选一个状态**：子机用 ROOT 类型（进组 seek 到第 0 帧重启到 Start），每个状态一条 `Start → 状态` 边；子机可再嵌套（Ground → Idle），层级不改变时序语义；
  - **组内只连真实转移**（不是完全图）：连段只 +1 推进就只有 1→2→3→4 链；各组的边表与"为什么没有某条边"见生成器 `GROUND_EDGES` / `AIR_EDGES` / `IDLE_EDGES` / `_attack_edges()` 注释；
  - **组谓词只写一次**（`P_*` 常量，含 `not Dead` 的优先级链，互斥）：主图边 = 目标组谓词，组内边 = 组内区分项（走/跑、跳/二段/落、段序号）；
  - **进组边必须 `reset=true`**：子机靠"seek 到第 0 帧"启动；缺 reset 子机会停在空 current、永不播放；
  - 时序（4.7.2 源码 + 冒烟测试）：组内转移（走跑切换、连段推进、跳→落）同帧生效；进组那一帧子机在同一 process 内完成"启动 + 选路"（中间混合权重为 0），可见延迟与主图直切相同。
- **属性驱动分工**：
  - C#（`HeroEntity`）只维护**角色属性**并暴露为 `[Export]` 字段（表达式事实面）：`MoveInput / Running / Airborne / Rising / JumpCount / AttackSegment / Hurt / Emoting / Dead`，每物理帧由 `SyncAnimFacts()` 刷新一次（其中 Airborne/Rising/Hurt/Emoting/Dead 由物理与计时器派生）；
  - **基类 `ActorEntity` 不含任何角色事实**，只把 AnimationTree 的表达式基对象指向实体节点；角色属性、状态机资源都归角色自己；
  - 各角色的状态机资源自行把属性映射到自己的动画名（wukong 专属动画名只出现在它的 .tres 与 `WukongEntity.IdleFlavorAnim` 覆写里）；新英雄 = 新动画库 + 新状态机资源 + 新 HeroConfig 行，基类零改动。
- **本引擎版本（4.7.2）状态机实测语义**（探针 + 源码验证，写状态机必须遵守）：
  1. 表达式边必须 `advance_mode=AUTO`；表达式在状态内**持续**评估，命中即转移；
  2. 转移在触发它的那次 process 里**同帧**生效；进组那一帧子机在同一 process 内完成"启动 + 选路"；
  3. 多条边同时为真时取 `priority` 最小者、同值取**后**加入者——本项目用互斥条件规避，不依赖它；
  4. `advance_expression` 读 C# 成员必须走 `[Export]` 字段（普通属性引擎侧不可见），基对象由 `AnimationTree.AdvanceExpressionBaseNode` 指向实体；`and`/`or`/`not` 可用；表达式在 setter 里解析后缓存，逐帧评估不重复解析；
  5. 表达式里引用**引擎自带**数据必须用引擎名：`is_on_floor()` / `velocity.y` 可用；写成 C# 风格的 `IsOnFloor()` / `Velocity` 会静默求值为 null（条件恒假）——迁移后"跑/跳动画失效"的根因就是它（`Ground → Air` 的 `not IsOnFloor()` 恒假，树永远出不了地面组）；本项目改用 C# 属性表达，图上不再出现引擎名；
  6. 子机 ROOT 类型进组重启到 Start；NESTED 类型会恢复上次内部状态（组式写法必须用 ROOT，否则组内边要两两直连才能自纠）。
- **冒烟测试**（自动化验证动画链路，替代"看日志猜"）：`"S:\Godot4\Godot4CSharp_console.exe" --headless --path Godot/GodotProject --quit-after 1500 -- --smoketest`；`TheGame/MainPack/Scripts/Debug/SmokeTestDriver.cs`（autoload，仅 `--smoketest` 时启用）自动注入连打/双击跑/受击/一段跳/二段跳输入，断言 AnimationTree **真正在播**的状态路径（`Attack/attack_1..4` → `Ground/run` → `Hurt` → `Air/jump` → `Air/fall` → `Air/jump_2` → `Ground/Idle/idle1`）、顺序，以及待机时特效层回到空白（动画 empty / 帧 0 / scale 1）；判定看 stdout `SMOKE PASS` / `SMOKE FAIL`（框架关闭流程有既有 bug，偶发段错误冲掉退出码，别只看退出码）。
- 旧 SpriteFrames 三件套（`wukong_animations.tres` / `wukong_weapon_*_animations.tres` / `wukong_effect_library.tres`）场景不再引用，已于 **2026-09-30 删除**（全项目审计零引用；`gen_animations.py` 对应产出步骤同步停用，重跑不会重建）——帧数据由 `wukong_anim_library.tres` 的帧轨道承载（身体姿势映射见表）；`wukong_effect_animations.tres` 仍被 m_Effect 引用、保留。

## 复现方式

```
python Tools/LegacyMigration/gen_animations.py
"S:\Godot4\Godot4CSharp_console.exe" --headless --path Godot/GodotProject --script res://EditorScripts/build_wukong_anim_tree.gd
"S:\Godot4\Godot4CSharp_console.exe" --headless --path Godot/GodotProject --quit-after 1500 -- --smoketest
# 怪物 AI 场景（巡逻/追击/出招/转身/平台守候踱步/丢失目标/受控/死亡，约 20s）：
"S:\Godot4\Godot4CSharp_console.exe" --headless --path Godot/GodotProject --quit-after 2400 -- --smoketest=ai
```

脚本读取旧项目 `.tscn`，输出到 `TheGame/Sprites/...`（SpriteFrames）与 `TheGame/Entitys/Animations/`（动画库 + 状态机；2026-09-30 从 `Sprites/` 与 `Entitys/` 根移入）；旧项目保持只读，不写入任何文件。

**真相源（2026-09-30 人类裁决）**：`gen_animations.py` 与两个 `build_*_anim_tree.gd` 的产物（两个 `*_anim_library.tres`、两个 `*_animation_tree.tres`、`wukong_effect_animations.tres`、`huaguoshan_monkey_animations.tres`）落地后**以 Godot 编辑器保存的版本为准**——判定盒、特效偏移、帧时长直接在 Animation 面板调。生成器默认跳过已存在的产物，只在文件缺失或显式 `--force [文件名]` 时重写（`--force` 会冲掉编辑器调整，用前先提交）。下表"新几何"列是迁移时的初值，当前值以 `.tres` 为准。在编辑器里调判定盒要改**关键帧**（改完点轨道钥匙覆盖），只改节点属性会被 RESET/动画播放洗掉。

## M4 判定帧与战斗资产（2026-09-29）

### 判定盒（2026-09-30 审查修订：几何与开关全部是动画值轨道，同旧项目）

旧项目"一招一次命中"= 攻击动画里 keyframe HitBox 的 **shape / position / disabled**（Role1.tscn 实测）+ `Area2D.area_entered` 只在进入重叠时触发；朝向由 `base_damagebox.scale.x` 翻转。新工程同构：`m_HitBoxRoot`（朝向镜像容器，C# 只翻 scale.x）→ `m_HitBox`（Area2D，**恒在原点**）→ `CollisionShape2D`——三条值轨道全部落在形状节点上（`…/CollisionShape2D` 的 `:disabled` / `:shape` / `:position`，与旧 `base_damagebox/HitBox/HitBox` 逐级对应；容器负缩放会把形状节点偏移一并镜像，冒烟实测命中正常）。库里每个动画带齐三条（非攻击动画写静止值——轨道完备性：AnimationPlayer 直驱不回卷轨道，切到任何动画首帧写回安全值，受击/死亡打断出招时判定盒随之复位），动画里写**原生朝左**坐标（前方 = 负 X）。出招装填/收招的数值包归 C# 身体状态机（2026-10-01 起不再走 `OnAttackBegin`/`OnAttackEnd` 方法轨道）。C# 另按目标去重（`AttackData.TryRegisterHit`）。

判定窗口沿用旧 disabled 轨道；**几何不再照搬旧形状**（旧胶囊 r76/323 宽矩形远大于视觉棒击范围，"没碰到就受击"的主因），纵向与后缘按判定窗内**武器层像素包围盒**推导（各外扩 10px）；**前缘统一放长到 -130"追击线"**——连段期间每次命中受击方被击退 ~17px（旧 hurtBack × 30 同值），不放长第三段起就够不着；旧项目靠超大方形盒（前缘 121~175）吸收同一漂移，这里用显式常量表达。attack_3/4 旋斩含身后来向帧，attack_4 的正向帧 f55 在旧窗开启之前、几何将其并入。推导与实测数值登记在 `gen_animations.py` 的 `WUKONG_HITBOX` / `MONKEY_HITBOX` 注释：

| 新动画 | 旧来源 | 判定窗口（真实秒） | 旧形状 | 新几何初值（尺寸 @ 原生坐标；现值以编辑器 .tres 为准） |
| --- | --- | --- | --- | --- |
| wukong `attack_1` | Role1 `hit1`（speed_scale 3） | 0.0333–0.1667 | 胶囊 r76 h186 @(−45,−17) | 178×114 @(−41, 23) |
| wukong `attack_2` | `hit2` | 0.0667–0.2333 | 矩形 133.5×43 @(−42.25,41.5) | 172×43 @(−44, 40.5) |
| wukong `attack_3` | `hit3` | 0.0333–0.1667 | 矩形 323×57 @(0,31.5) | 233×75 @(−13.5, 23.5) |
| wukong `attack_4` | `hit4` | 0.0667–0.2000 | 矩形 273×74 @(−38.5,38) | 233×96 @(−13.5, 12) |
| monkey `attack_1` | Monster_1 `hit1`（speed_scale 1） | 0.30–0.40 | 圆 r25 @(−24,−10) | 50×50 @(−24, −23)（Y 修正身体层 (0,−13) 偏移） |
| demon_monkey `attack_1` | Monster_2 `hit1`（speed_scale 1） | 0.10–0.40 | 圆 r46.0109 @(−31,−4) | 80×80 @(−31, −4)（编辑器调校；身体层与判定盒同原点） |

数值不再进 `AttackConfig`（表只存玩法数值；2026-09-30 删除 HitBoxOffset/HitBoxSize/Interval 三列）。

### huaguoshan_monkey 动画库

`Entitys/Animations/huaguoshan_monkey_anim_library.tres`（`gen_animations.py` 的 `gen_monkey_library()`；状态机 `EditorScripts/build_huaguoshan_monkey_anim_tree.gd` → `Entitys/Animations/huaguoshan_monkey_animation_tree.tres`）。轨道驱动 `m_Body`（AnimatedSprite2D，复用 `huaguoshan_monkey_animations.tres`）的 animation/frame/offset；旧 SpriteFrames 用"重复条目撑时长"，这里合并为同一帧的停留时长：

| 新动画 | 旧 | 帧:秒 | offset（朝左原值，镜像由父缩放处理） |
| --- | --- | --- | --- |
| idle | wait | 0:0.8（新工程只迁了 1 帧待机图） | (4,0) |
| run | walk | 0..3 各 0.2 | (0,0) |
| attack_1 | hit1 | 0:0.12 1:0.12 2..5 各 0.04 | (−13,0) |
| hurt | hurt | 0:0.16 1:0.12 | (1.5,−0.5) |
| death | death | 0..4 各 0.0667，5 停到 0.8 | (4,0) |

### demon_monkey 动画库

`Entitys/Animations/demon_monkey_anim_library.tres`（`gen_animations.py` 的 `gen_demon_monkey_library()`）。轨道驱动 `m_Body`（AnimatedSprite2D，复用 `demon_monkey_animations.tres`）的 animation/frame/offset，规则同 huaguoshan_monkey：

| 新动画 | 旧 | 帧:秒 | offset（朝左原值，镜像由父缩放处理） |
| --- | --- | --- | --- |
| idle | wait | 0..4 各 0.1 | (−2.5,0) |
| run | walk | 0..3 各 0.2 | (13.5,4) |
| attack_1 | hit1 | 0:0.0667 1:0.0667 2:0.0667 3:0.2668 | (−26.5,−9.5) |
| hurt | hurt | 0:0.28 | (10,5) |
| death | death | 0..3 各 0.1，4 停到 1.2 | (0,0) |

### 伤害数字

`Tools/LegacyMigration/gen_damage_numbers.gd` 把旧 `Art/AllNumber/<样式>/<名>_0..9.png`（每位一张）拼成每样式一张 10 格横条，输出 `Sprites/Number/`：

| 新文件 | 旧来源 | 格尺寸 | 用途 |
| --- | --- | --- | --- |
| `damage_number_monster_physics.png` | `magic/physics_N`（旧放在 magic 目录） | 30×30 | 怪物受物理伤害 |
| `damage_number_monster_physics_crit.png` | `physicscrit/physics_N` | 42×42 | 怪物受物理暴击 |
| `damage_number_monster_magic.png` | `magic/magic_N` | 30×30 | 怪物受魔法伤害 |
| `damage_number_monster_magic_crit.png` | `magiccrit/magic_N` | 32×32 | 怪物受魔法暴击 |
| `damage_number_hero_physics.png` | `monster/physics/physics_N`（旧放在 monster 目录） | 30×30 | 英雄受物理伤害 |
| `damage_number_hero_magic.png` | `monster/magic/magic_N` | 30×30 | 英雄受魔法伤害 |
| `damage_number_real.png` | `real/real_N` | 30×30 | 真实伤害（人怪共用） |
| `damage_number_miss.png` | `miss.png` | 55×24 | 闪避 |

旧 `Physics/`（大写 P）目录旧代码未引用，未迁。飘字时序（旧 `DamageText` "physics"/"Crit" 与 `miss_effect` 动画）作为表现常数写在 `GameScripts/UI/DamagePop.cs`。

```bat
"S:\Godot4\Godot4CSharp_console.exe" --headless --script Tools/LegacyMigration/gen_damage_numbers.gd
"S:\Godot4\Godot4CSharp_console.exe" --headless --path Godot/GodotProject --script res://EditorScripts/build_huaguoshan_monkey_anim_tree.gd
```

## 待办

- `wukong` 的 `hurt` / `death` 在旧动画中仅单帧（旧工程另有 `RoleDeath.png` 等独立节点），后续接入死亡表现时再评估。
- `huaguoshan_monkey` 时序已由动画库按旧 `mr_player` 轨道复刻（见上节）；`huaguoshan_monkey_animations.tres` 里的 0.1s 帧时长不再参与播放。待机图只迁了 1 帧（旧 `Wait.png` 4 帧），M5 补齐。
- 武器层已迁 5 张（含空手）用于换装测试；其余 15 把武器与 25 套防具图集待装备系统阶段按 Luban 表按需迁入。
- 身体层 `wukong_body.png` 的部分动作帧自带默认棍（旧美术遗留）；接武器层后需确认是否与 `wukong_weapon_*` 叠加导致重复，必要时清理身体层里的武器像素。
- 技能特效与技能音效（见上两节"未迁移"）随技能系统一起迁；`Action/SpecialEffect2` 第二特效层同理。
- 打击特效（`MonsterBeHurt_*`、`RoleBeHit`，`Art/StrikeSpecialEffects/`）M4 未迁：命中链路已留 `ActorEntity.OnHurt` 钩子，迁入时同样走 NodePool。`MissEffect` 已由 `damage_number_miss.png` 覆盖。
