# M6 关卡与流程（Level Design & Workflow）实施计划

> 目标（AGENTS.md §14 M6）：一条能玩的关卡——**选人 → 进关卡 → 打小怪 → 清场出门 → 通关结算**，`LevelDirector`（波次/场上上限/清场开闸）+ 出口 + `GF.Scene` + HUD，通关写入 `GF.Archive`。
> 本计划基于对当前代码库（M5 完成）、旧项目 `ZMXY_BHYH`、GGF 框架文档与行业基准（Unreal GameMode 匹配状态机、XNA 屏幕栈、Vampire Survivors 波次表、事件驱动 WaveSpawner 模式）的完整调研。

## 0. 已确认的架构决策（2026-10-02 用户裁决）

| 决策点 | 结论 |
| --- | --- |
| 顶层流程拓扑 | **新增 `ProcedureBattle` 对局流程**（菜单/对局成为顶层状态；需在 `GameFramework.tscn` 的 Procedures 列表加一行并在编辑器验证） |
| 开始界面 | **`HeroSelectForm` 兼作开始界面**（标题 + 悟空立绘 + 属性 + 开始按钮；不加枚举不加表） |
| MonsterId 2/3 缺失 | **改表全用花果山猴子**（LevelSpawnConfig 35 行 2/3→1，44 只猴子 4 波通关） |
| HUD 范围 | **按现有系统裁剪**：血条 + 等级 + 无双条 + 波次指示 + 清场 gogo 箭头；技能栏/背包/法宝/宠物入口不做占位 |
| BGM | **只做关卡 BGM**（迁 `1_music.mp3`；`LevelConfig.BgmPath` → `BgmSoundId` 列替换） |
| 结算与存档 | **简化结算**：用时/剩余血量/击杀数 + 重试/返回选人；GameData 增 `SelectedHeroId` + 通关记录；星级评价/最高连击留 M7 |

架构基调（行业基准 × GGF 惯例）：**顶层流程 = 显式状态**（Procedure 层）；**对局内 Director 只做波次编排并以事件对外广播**；**UI 纯事件驱动、不持实体引用**；**场景是数据**（出生点/闸门/出口/相机边界都是场景节点，代码不写几何数值）。

## 1. 总体结构（完成后）

```
流程：Launch → Update → Prelode → ProcedureGame(菜单/选人) ⇄ ProcedureBattle(对局)
ProcedureGame:  打开 HeroSelectForm；--smoketest 时跳过菜单直进 sandbox 对局
ProcedureBattle: Loading → GF.Scene.LoadScene(Level_1) → 开 HudForm → LevelDirector.StartLevel
                → 订阅关卡事件 → LevelCleared/LevelFailed → 开 GameOverForm → 重试(重进本流程)/返回(回 ProcedureGame)

LevelDirector(SingletonNode, GameScripts/Manager/): 波次推进(TriggerX) + MaxAlive/SpawnInterval 补怪
                + 清场开闸 + 出口开启 + 击杀/用时统计 → 广播 Wave/Level 事件；流程注入场景节点句柄
```

## 2. 数据与配置变更（先加表再写代码；导表后 `dotnet build` 必须通过）

| 变更 | 内容 | 方式 |
| --- | --- | --- |
| `LevelSpawnConfig` | 第 2~4 波 MonsterId 2/3 → 1（35 行） | xlsx 直接改（数值修复） |
| `LevelConfig` | 列 `BgmPath`(string) → `BgmSoundId`(SoundId)；花果山行填 `Level1Bgm` | 结构变更（LegacyAssetMap 已定稿的设计） |
| `__enums__` SoundId | `+Level1Bgm = 7`（显式写死，禁止自动递增） | xlsx |
| `SoundConfig` | `+Level1Bgm` 行：Group=Music，Path=`res://TheGame/Audios/BGM/level_1.mp3` | xlsx |
| `BattleConfig` | `+WsMax` 列（默认 100，旧项目无双满值语义；若人类反对则 HUD 隐藏 WS 条，不阻塞其余） | xlsx |

执行方式：优先经 `Tools/ConfigBootstrap/build_m2_tables.py` 扩展脚本化执行（结构变更需 `force_rows`，不覆盖手工数值）；人工 xlsx 编辑亦可。**数值最终由人类复核。** 导表产物 `GameProto/` + `DataTables/` 一起提交。

## 3. 资产迁移（旧项目只读；不带旧 `.import`；登记 `Docs/LegacyAssetMap.md`）

| 旧路径 | 新路径 | 用途 |
| --- | --- | --- |
| `Art/MainGame/Bg1.png` | `Sprites/UI/main_menu/main_menu_bg.png` | 开始界面背景 |
| `Art/MainGame/ChoosePlayer/ui_juese_wukong01.png` | `Sprites/UI/hero_select/wukong_portrait.png` | 悟空立绘 |
| `Art/HeroPicture/RoleProperiesBox/712/718/720/724.png` | `Sprites/UI/hud/hp_<对应名>.png` | 血条框/底/前景（对照旧 `Role_information.tscn` 参数） |
| `Art/HeroPicture/WSGrey.png / WSBar.png` | `Sprites/UI/hud/ws_grey.png / ws_bar.png` | 无双条 |
| `Art/Level/Gogo/1..67.png` | `Sprites/UI/hud/gogo/gogo_1..67.png` | 前进箭头（SpriteFrames，帧轨道驱动） |
| `Art/Level/Settlement/*.png`（按视觉需要取舍） | `Sprites/UI/game_over/*.png` | 结算界面装饰 |
| `Art/Level/Export.png` | `Sprites/Levels/huaguoshan/level_1_exit.png` | 出口传送门（206×186 × 11 帧） |
| `Music/level/1_music.mp3` | `Audios/BGM/level_1.mp3` | 关卡 BGM |

全部 snake_case、basename 全树唯一；提交前跑 TopMenu「Generate File → Collection Res」验证。

## 4. 新增事件（`GameScripts/Event/`，一文件一类；ReferencePool：`Create()` + `Clear()` + 复制转发构造）

| 事件 | 携带 | 发布方 → 订阅方 |
| --- | --- | --- |
| `HeroVitalsChangedEventArgs` | entityId, hp, maxHp, level, wsValue, wsMax | HeroEntity（OnShow 初始 / OnHurt / OnHitLanded / Heal）→ HudForm |
| `HeroDiedEventArgs` | entityId, killerEntityId | HeroEntity（`IActorBody.OnDied()`，镜像 MonsterEntity 模式）→ LevelDirector → LevelFailed |
| `WaveStartedEventArgs` | levelId, waveIndex, waveTotal | LevelDirector → HudForm（波次文本、隐藏 gogo） |
| `WaveClearedEventArgs` | levelId, waveIndex | LevelDirector → HudForm（显示 gogo） |
| `LevelClearedEventArgs` | levelId, elapsedSeconds, killCount, heroHp, heroMaxHp | LevelDirector → ProcedureBattle（开结算/写档） |
| `LevelFailedEventArgs` | levelId, killerEntityId | LevelDirector → ProcedureBattle（延迟开失败结算） |
| `StartBattleRequestedEventArgs` | heroId | HeroSelectForm → ProcedureGame |
| `RetryBattleRequestedEventArgs` | — | GameOverForm → ProcedureBattle |
| `ReturnToMenuRequestedEventArgs` | — | GameOverForm → ProcedureBattle |

事件参数只携带**值**（不引用实体节点，Fire 后不持有）。

## 5. LevelDirector（`GameScripts/Manager/LevelDirector.cs`，`SingletonNode<T>`，DamagePopManager 先例）

**职责边界**：只管对局内容（波次/刷怪/闸门/出口/统计/关卡 BGM），不管流程（加载 UI、表单开关、切流程、写档——这些归 ProcedureBattle）。与外界只经事件通信；场景节点由流程**显式注入**（红线 8：生成方注入，不做跨模块 GetNode 长链）。

```
StartLevel(int levelId, int heroId, Node2D sceneRoot)
  读 LevelConfig + LevelWaveConfig(按 WaveIndex 排序) + LevelSpawnConfig(按 WaveId 分组)
  订阅 MonsterDied / HeroDied；从注入的 sceneRoot 子树取标记（HeroSpawn/Gate1..3/Exit/BattleCamera）
  生成英雄（GF.Entity.ShowEntityAsync，置 HeroSpawn 位）；开表查 BgmSoundId → GF.Sound.PlayBGM
  立即激活第 1 波（TriggerX=0）；计时开始
_PhysicsProcess Tick（对局结束后直接 return）
  英雄 x ≥ 下一波 TriggerX → 激活该波（先于闸门：闸门位于 TriggerX 之前，波未清完玩家过不去）
  活跃波补怪：队列非空 && 场上 < MaxAlive && 距上次 ≥ SpawnInterval → ShowEntityAsync（位置 userData）
  相机跟随：BattleCamera.GlobalPosition = 英雄位置（限幅/平滑由相机节点属性承担）
MonsterDied → 场上数--、击杀数++；波清（队列空 && 场上 0）→ 开闸（Gate CollisionShape.Disabled=true）
  + 广播 WaveCleared；最后一波清 → 开出口（ExitArea 可见 + Collision 启用）
ExitArea.BodyEntered(英雄) → 广播 LevelCleared(含统计)；对局结束
HeroDied → 广播 LevelFailed；对局结束
StopBattle() → 退订事件、HideEntitySafe 收掉已生成怪物与英雄、复位状态
```

数值全部来自表（MaxAlive/SpawnInterval/TriggerX/坐标）；失败延迟、相机平滑等为**节点/场景属性**或具名常量（`static readonly` + 来源注释）。

## 6. Level_1.tscn（`TheGame/Scenes/`，编辑器内搭建，编辑器为真相源）

结构（坐标以 DebugArena 地板 y≈341 为基准对齐；数值在编辑器里调）：

```
Level_1 (Node2D)
├─ BackGround (ParallaxBackground: level_1_bg_end 静态 / level_1_front 前景 / level_1_floor 地板)
├─ Ground (StaticBody2D, World 层, ~5300 宽) + LeftWall/RightWall
├─ Gate1/2/3 (StaticBody2D+CollisionShape, World 层, x≈1500/2600/3900, 初始启用)
├─ HeroSpawn (Node2D 标记, ≈(300,300))
├─ Exit (Node2D: AnimatedSprite2D 传送门 + ExitArea(Area2D, Exit 层, mask PlayerBody,
│         CollisionShape 初始 disabled/隐藏), x≈5000 地板上)
└─ BattleCamera (Camera2D: LimitLeft/Right/Top/Bottom 在节点属性里配好；位置平滑启用)
```

闸门 X 与 TriggerX 的关系：`Gate_N` 在 `TriggerX_(N+1)` 之前约 100px（波清才开闸 → 玩家必须清完当前波才能触发下一波）。不做斜坡/单向平台（M7+）。

## 7. UI 三个表单（`TheGame/UIs/*.tscn` + 生成 Ge + `GameScripts/UI/*.Logic.cs`）

统一走 GGF 生成器工作流：编辑器搭场景 → 选中根节点 → Inspector「Generate Script」→ Ge 半类（`GameProto/UIGe/`）+ Logic 骨架（`GameScripts/UI/`）。`UIFormConfig` 表的三个 AssetPath 已就位，全部 Normal 组。

| 表单 | 内容 | 事件接线 |
| --- | --- | --- |
| `HeroSelectForm`（兼开始界面） | 940×590 全屏：`main_menu_bg` 背景 + 标题（造梦西游之/八荒湮隳篇·力战七大魔王）+ 悟空立绘卡片（名字/描述从 `TbHeroConfig` 悟空行填，代码填不进场景）+ 「开始战斗」按钮 | 按钮 → Fire `StartBattleRequested(heroId)` |
| `HudForm` | 血条（TextureProgressBar + hp 文本 + Lv 标签）、WS 条（wsMax 满值）、波次 Label「第 X/Y 波」、gogo 箭头（Sprite2D + 帧轨道，右侧屏幕空间） | 订阅 `HeroVitalsChanged` / `WaveStarted` / `WaveCleared`（OnClose 退订；`PauseCoveredUIForm` 由表行配置） |
| `GameOverForm` | 通关/战败两套布局（结算标签：用时 mm:ss / 剩余血量 X/Y / 击杀数）+「再次挑战」「返回选人」按钮；数据经 `OpenUIForm` 的 userData 传入纯数据载荷（不引用实体） | 按钮 → Fire `RetryBattleRequested` / `ReturnToMenuRequested` |

打开顺序保证 HUD 收到初始事件：先开 HudForm、后 `StartLevel`（英雄 OnShow 广播初始 Vitals、第 1 波 WaveStarted 均在订阅之后到达）。

## 8. 流程实现（`MainPack/Scripts/Procedure/`）

**`ProcedureBattle`（新文件）**——流程间传参用 FSM Data（`VarInt32 BattleLevelId/BattleHeroId`、`VarBoolean BattleSandbox`）：

```
OnEnter:
  打开 LoadingForm → await GF.Scene.LoadSceneAsync(scenePath, Single)
  sandbox 分支（--smoketest 通道）：DebugArena + 英雄(300,300) + 猴子(700,300)，
    不开表单、不启 Director、不播 BGM —— 完整复刻当前 ProcedureGame 行为，冒烟测试零适配
  正常分支：DamagePopManager.Activate(GF.Entity) → await 开 HudForm → LevelDirector.StartLevel
    → LoadingForm.CloseLoading()
  订阅 LevelCleared / LevelFailed / RetryBattleRequested / ReturnToMenuRequested（OnLeave 退订）
  LevelCleared → 开 GameOverForm(胜利载荷)；LevelFailed → 延迟(死亡动画时长, 具名常量) → 开 GameOverForm(失败载荷)
  RetryBattleRequested → ChangeState<ProcedureBattle>（自重进，OnLeave 先完成收尾）
  ReturnToMenuRequested → ChangeState<ProcedureGame>
OnLeave(非 shutdown):
  关 HudForm/GameOverForm；LevelDirector.StopBattle()；DamagePopManager.Deactivate()
  GF.Sound.StopBGM()；GF.Scene.UnloadScene(scenePath)
写档（LevelCleared 时）：更新 GF.Archive.CurrentData 的通关记录 → OverWriteAsync()
```

**`ProcedureGame`（改造）**——从"调试场地入口"变为"菜单流程"：

```
OnEnter: MarkStartupSuccess()（保留）；检测 --smoketest → 写 sandbox Data → ChangeState<ProcedureBattle>
  否则 await OpenUIFormAsync<HeroSelectForm>() → LoadingForm.CloseLoading()
  订阅 StartBattleRequested → 写 BattleLevelId(取 TbLevelConfig 首行)/BattleHeroId → ChangeState<ProcedureBattle>
OnLeave: 关 HeroSelectForm
```

**`GameFramework.tscn`**：Procedure 节点的 `Procedures` 数组追加 `"ProcedureBattle"`（新增 .cs 后先 `--build-solutions`，再编辑器打开验证启动链路，这是框架文档 §6 的标准步骤）。

**async 时序红线**（§5.2）：所有 `await` 在 try/catch 内；先订阅后调用；事件参数不进闭包、`await` 后不再读。

## 9. 存档（`GameScripts/Archive/`）

```csharp
GameData:   +int SelectedHeroId; +List<LevelClearRecord> LevelClearRecords;   // Score/Name 原样保留
LevelClearRecord: +int LevelId; +double BestSeconds; +int ClearCount;         // 新文件，可序列化纯数据
```

写入时机：开始对局时写 `SelectedHeroId`；`LevelCleared` 时更新记录（最好用时取 min、次数 +1）并 `OverWriteAsync()`。已发布字段（Score/Name）语义不动。

## 10. 冒烟测试通道（回归保障）

`SmokeTestDriver` / `MonsterAiSmokeScenario` **零改动**（靠场景树找英雄/猴子、自行重摆位置）。唯一依赖是"启动后 5 秒内英雄在场"——由 `ProcedureGame` 的 `--smoketest` 分支 → `ProcedureBattle` sandbox 模式保证。交付前必须两条都 PASS：
```
"S:\Godot4\Godot4CSharp_console.exe" --headless --path Godot/GodotProject --quit-after 1500 -- --smoketest
"S:\Godot4\Godot4CSharp_console.exe" --headless --path Godot/GodotProject --quit-after 2400 -- --smoketest=ai
```

## 11. 任务顺序（实施清单）

1. **表变更**（§2）+ 导表 + `dotnet build` 通过
2. **资产迁移**（§3）+ `LegacyAssetMap.md` 登记 + Collection Res 通过
3. **事件层**：9 个 EventArgs（§4）
4. **HeroEntity**：OnShow/OnHurt/OnHitLanded/Heal 广播 Vitals；`OnDied` 广播 HeroDied
5. **LevelDirector**（§5）
6. **Level_1.tscn** 场景搭建（§6，编辑器内）
7. **三个 UI 表单**：场景 + 生成 Ge + Logic（§7）
8. **ProcedureBattle 新建 + ProcedureGame 改造 + GameFramework.tscn 注册**（§8）+ `--build-solutions`
9. **存档字段与写档**（§9）
10. **BGM 接线**（SoundConfig 查表 → PlayBGM；流程收尾 StopBGM）
11. **全量验证**（§12）+ 文档更新（LegacyAssetMap 已在第 2 步；`Architecture.md` 留 M7）

## 12. 验证清单

| 项 | 命令/方式 | 通过标准 |
| --- | --- | --- |
| 构建 | `dotnet build`（`Godot/GodotProject`） | 0 错误 |
| 新 .cs 后 | `"S:\Godot4\Godot4CSharp_console.exe" --build-solutions --path Godot/GodotProject --no-window -q` | 成功 |
| 导表 | `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat` | GameProto/DataTables 更新且 build 通过 |
| 冒烟 | §10 两条命令 | stdout `SMOKE PASS` |
| 手动通关 | 编辑器运行 | 选人 → 开始 → 4 波清场开闸（gogo 出现）→ 出口传送门开启 → 触碰 → 结算显示用时/血量/击杀 → 返回选人 |
| 失败路径 | 手动作死 | 英雄死亡动画 → 失败结算 → 重试可完整再战 |
| 存档 | 通关后重启 | SelectedHeroId/通关记录持久（`user://GameData/`） |
| 收尾 | TopMenu「Generate File → Collection Res」 | 全树 basename 无冲突 |
| 启动链 | 编辑器打开 `GameFramework.tscn` 运行 | Procedures 列表含 ProcedureBattle 且启动正常 |

## 13. 风险与边界

- **表结构变更**（LevelConfig 列替换、BattleConfig 加列）属结构变更：ConfigBootstrap 需 `force_rows`，人工复核数值；生成物覆盖即提交。
- **GameFramework.tscn Procedures 列表**是启动依赖：改后必须在编辑器验证（框架文档标准流程）。
- **波次/闸门 X 布局一致性**：Gate1/2/3（1500/2600/3900）必须与 TriggerX（1600/2700/4000）保持"闸在触发点前"关系，场景调整时两侧同步。
- **WS 条依赖 BattleConfig.WsMax 新列**：人类若否决该列，HUD 隐藏 WS 条即可，不阻塞其余交付。
- **多猴子同点刷出**（表坐标相同）为旧项目原状（AI 漫游自然散开），不额外加随机偏移。
- **实体收尾顺序**：重试/返回时先 `StopBattle()`（收怪收英雄、退订）再卸场景，避免 await 续体访问已释放节点（§5.2 框架坑）。

## 14. 范围外（M7+ 评估，本阶段不做）

地图选关（Map 场景）、暂停/设置菜单、技能栏/背包/法宝/宠物 UI、星级评价/最高连击/结算彩蛋、主菜单与结算 BGM、Monster_2/3 迁移、斜坡/单向平台、无双技能消耗、本地化接入、掉落物/经验、`Architecture.md` 补齐。
