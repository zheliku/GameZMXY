# AGENTS.md —— GameZMXY（造梦西游·同人向学习项目）

> 本仓库最高工程规范，人类与 AI 协作者都必须遵守；规范只由人类发起变更，AI 发现冲突时停下报告，由人类裁决。

## 0. 项目一句话

单人 2D 横版动作 ARPG（造梦西游同人向**学习**项目，**禁止商用与公开发布**）。
Godot 4.7 .NET + C# + GGF 框架，游戏代码**全新编写**；旧项目 `P:\Godot-Project\ZMXY_BHYH` 只作素材库与设计参考。
优先级：**工程规范与可维护性 > 功能数量**。

## 1. 技术栈与环境

| 项 | 约定 |
| --- | --- |
| 引擎 | Godot 4.7 .NET（mono），SDK 4.7.2；可执行文件见 §1.1 |
| 语言 | C# `net10.0`；Newtonsoft.Json（NuGet 13.0.4） |
| 框架 | GGF：`Godot/GodotProject/Framework/`（GameFramework 纯 C# 层 + GodotGameFrameworkCore 桥接层） |
| 游戏工程 | `Godot/GodotProject/TheGame/`（目录名不可改，§3.2） |
| 渲染/物理/视口 | `gl_compatibility`；2D 内置物理；940×590，stretch=`canvas_items` |
| 配表 | Luban：源 `Configs/GameConfig/Datas/*.xlsx`，工具 `Tools/Luban/` |
| 日常构建 | `dotnet build`（工作目录 `Godot/GodotProject`） |
| 新增 .cs 后 | `"<godot_exe>" --build-solutions --path Godot/GodotProject --no-window -q` |
| 打开编辑器 | `"<godot_exe>" --path Godot/GodotProject --editor` |
| 导表 | `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`，或编辑器 TopMenu → Generate File → GameConfig File |

### 1.1 引擎路径

- `S:\Godot4\Godot4CSharp.exe`：编辑器/正常运行；`S:\Godot4\Godot4CSharp_console.exe`：headless（生成器、冒烟测试、`--build-solutions`）。`godot` 不在 PATH，统一写绝对路径。
- 引擎 .NET 数据目录必须与 exe 同级且名为 `GodotSharp/`（Godot 按固定名查找）；改名/删除会使所有 C# 运行报 `.NET: Assemblies not found`。本机该目录名为 `GodotCSharp`，靠 junction `S:\Godot4\GodotSharp → GodotCSharp` 兼容；改回名后应删 junction。
- `godot-mcp`（npm 全局）可用于跑工程/取调试输出。

## 2. 规范文件分布与阅读规则

| 文件 | 管辖范围 |
| --- | --- |
| `AGENTS.md`（本文件） | 全局：架构约束、编码、框架速查、事件/存档、迁移、Git、里程碑 |
| `Godot/AGENTS.md` | Framework/addons 只读规则、docs 索引 |
| `Godot/GodotProject/EditorScripts/AGENTS.md` | 生成器脚本：运行方式、产物禁改、UID |
| `Godot/GodotProject/TheGame/AGENTS.md` | bundle 目录总规则、提交前检查 |
| `TheGame/GameScripts/AGENTS.md` | 代码分层与战斗结算 |
| `TheGame/GameScripts/Entity/AGENTS.md` | 实体继承树、生命周期、状态机、物理层表 |
| `TheGame/GameScripts/GameProto/AGENTS.md` | 生成代码禁改与再生成 |
| `TheGame/MainPack/AGENTS.md` | 主包使用边界 |
| `TheGame/Sprites/AGENTS.md` | 贴图目录/命名/Collection Res |
| `TheGame/Audios/AGENTS.md` | 音频目录/命名/接入 |
| `TheGame/Entitys/AGENTS.md` | 实体场景命名与 node_paths 绑定 |
| `Configs/AGENTS.md` | Luban 表结构与导表流程 |
| `Tools/AGENTS.md` | 开发工具边界 |
| `Docs/AGENTS.md` | 文档维护 |

1. 任何任务先读本文件；改动落在某目录时，再读该目录及其祖先的 `AGENTS.md`（就近优先，未单列的目录遵循根规范 + 最近祖先规范）。
2. 冲突裁决：根规范 > 目录级规范 > AI 推断；规范与代码现实冲突时停下报告，不擅自改规范。

## 3. 仓库结构

```
GameZMXY/                            # 仓库根
├─ AGENTS.md                         # 本文件（全局规范）
├─ README.md  LICENSE                # GGF 框架自带，后续替换为项目说明
├─ Configs/                          # Luban 配置源（规范：Configs/AGENTS.md）
│  ├─ GameConfig/                    # luban.conf / Defines / Datas / CustomTemplate / 导表脚本
│  └─ Localization/本地化.xlsx
├─ Tools/                            # 开发工具（规范：Tools/AGENTS.md）
│  ├─ Luban/                         # vendored 导表工具（不改）
│  ├─ LegacyMigration/               # 旧项目素材/动画一次性迁移脚本
│  ├─ ConfigBootstrap/               # Luban 表种子 / 修复脚本
│  └─ FileServer/                    # 本地静态服务器（热更调试，按需启动）
├─ Docs/                             # 项目文档（规范：Docs/AGENTS.md）
└─ Godot/                            # 规范：Godot/AGENTS.md
   ├─ CLAUDE.md  .claude/            # 框架原作者的 AI 约定与工具配置（可参考，冲突以根规范为准）
   ├─ docs/                          # GGF 框架系统文档（查 API 先查这里）
   ├─ production/                    # 工作流状态（stage/review-mode/session-logs），非项目规范
   ├─ exported_bundles/              # bundle 导出产物（构建产物，不入库）
   └─ GodotProject/                  # Godot 工程根
      ├─ project.godot               # 引擎配置（渲染/视口/物理层/InputMap/插件）
      ├─ GodotProject.csproj / .sln
      ├─ EditorScripts/              # 编辑器生成器脚本（规范：EditorScripts/AGENTS.md）
      ├─ Framework/                  # GGF 框架 —— 只读（改动走 §13）
      ├─ addons/                     # 框架自带编辑器插件（不动）
      └─ TheGame/                    # 游戏本体（§3.1；规范：TheGame/AGENTS.md）
```

### 3.1 TheGame/（游戏本体）内部结构

```
TheGame/
├─ MainPack/                        # 主包：随应用打包、不参与热更（规范：MainPack/AGENTS.md）
│  ├─ Scripts/Procedure/            # ProcedureLaunch / Update / Prelode / Game
│  ├─ Scripts/Resources/*.cs        # EntityGroupRes / UIGroupRes / SoundGroupRes / UpdateSettingRes 等 Res 定义 + EntityGroup / PoolEntry 条目类
│  ├─ Scripts/ObjectPool/           # NodePool 等框架通用组件
│  ├─ Scripts/UI/                   # LoadingForm / QuestionTips 等共享 UI Logic
│  ├─ Scripts/Debug/                # SmokeTestDriver（--smoketest 冒烟测试，平时零开销）
│  ├─ Resources/*.tres              # 实例：EntityGroup/UIGroup/SoundGroup/UpdateSetting 由 GameFramework.tscn 注入；其余经 Collection Res 常量加载
│  └─ Fonts/  Themes/  UI/          # 字体 / 主题 / 共享 UI 场景
├─ GameScripts/                     # 游戏代码（规范：GameScripts/AGENTS.md）
│  ├─ Battle/                       # 伤害公式、属性快照、Buff 结算（可单测；值类型可引 Godot，禁运行时引擎状态）
│  ├─ Entity/                       # 实体 Logic：Heroes/ Monsters/ Bullets/ Items/（规范：Entity/AGENTS.md）
│  ├─ UI/                           # 界面 Logic（按需创建）
│  ├─ Event/                        # 自定义 GameEventArgs（按需创建）
│  ├─ Archive/                      # GameCatalogue / GameData（存档数据类）
│  ├─ Manager/                      # 玩法管理器（SingletonNode<T>，按需创建）
│  ├─ Config/                       # 数值配置强类型包装（含生成物 ExternalTypeUtil.cs）
│  └─ GameProto/                    # 【生成目录】GameConfig / EntityGe / UIGe，禁止手改（规范：GameProto/AGENTS.md）
├─ DataTables/                      # 【bundle】Luban .bytes + 本地化 .txt（生成物，禁手改）
├─ Sprites/                         # 【bundle】全部贴图/图集（规范：Sprites/AGENTS.md）
├─ Audios/                          # 【bundle】全部音频（规范：Audios/AGENTS.md）
├─ Entitys/                         # 【bundle】实体 .tscn（规范：Entitys/AGENTS.md）
├─ UIs/                             # 【bundle】游戏界面 .tscn
└─ Scenes/                          # 【bundle】关卡等场景 .tscn
```

`GameProto/ResourcesCollectionConstant.cs` 永不手改。

### 3.2 关键决策

- `TheGame/` 与 6 个 bundle 目录名不可改：框架路径常量、Collection Res 扫描根都围绕它们，改名会散架。
- 框架硬编码链路不可改：`GameFolderConstant`、`ScriptGenerateRes` 的 4 个 Ge/Logic 输出路径、Collection Res 扫描 `res://TheGame/`；确需新输出类型先报告人类。

## 4. 架构约束

> 1–4、6、7 为**红线**，违反一律返工；〔强约定〕两条默认遵守，确需偏离时注释理由并在交付时报告。

1. **双层分离**（2026-09-30 人类裁决：Battle/ 解除 Godot 禁引）：`Framework/GameFramework/` 仍禁止引用 `Godot.*`（纯 C# 框架层）；伤害公式、属性计算、概率判定、Buff 结算全部放 `Battle/`，**可单测**。`Battle/` 允许引用 Godot 的**纯值类型**（`Vector2`/`Mathf` 等，GodotSharp 内为纯 C# 实现），但禁止依赖运行时引擎状态（节点、场景树、`GD.*` 随机/单例）——保持确定性、脱离引擎可测。
2. **门面调用**：框架能力只通过 `GF.组件`（§6）访问；禁止自造 Autoload、可变静态全局状态、上帝类，具名常量（`static readonly`）不受限。
3. **生成代码禁止手改**：`GameProto/`（Ge 半类、Luban 产物、`ResourcesCollectionConstant.cs`）、`GameScripts/Config/ExternalTypeUtil.cs`、生成器 `.tres` 重新生成即覆盖。业务逻辑只写手写代码：UI 为 `*.Logic.cs`，实体为 `GameScripts/Entity/` 下的类。
4. **禁止移植旧 GDScript**：不拷贝旧 `.gd`/`.uid`/`.tscn` 脚本逻辑、不逐行翻译，理解设计后用 C# 按新架构重写。
5. **〔强约定〕数值进表**：可平衡调参的数值（装备属性、怪物数值、技能系数、掉落、波次、价格）一律进 Luban 表；工程常数（超时、缓存上限、物理手感）允许具名常量（`static readonly` + 注释来源）；**裸字面量参与玩法计算即打回**。
6. **高频生灭对象必须池化**：子弹、伤害飘字、掉落物、打击特效走 `NodePool` / `GF.ObjectPool`，禁止裸 `Instantiate + QueueFree`。
7. **状态必须是状态机**：角色行为/动画由该角色自己的 AnimationTree 状态机表达，C# 只维护角色属性、不持有"当前状态"；禁止 bool 标志位拼状态（旧项目最大教训）、禁止巨型 `if/match` 链。`GF.Fsm` 仅用于纯逻辑状态机（怪物 AI）。细节见 `Entity/AGENTS.md`。
8. **〔强约定〕引用不穿透**：禁止跨模块 `GetNode` 长链与连续爬父（`GetParent().GetParent()`）；跨模块走事件（§9）或生成方显式注入；实体自身子树内允许直引用（优先 `[Export]`）。

## 5. C# 编码规范

### 5.1 命名

| 对象 | 规则 | 示例 |
| --- | --- | --- |
| 类/结构/枚举/接口 | PascalCase，接口加 `I` | `WukongEntity`, `IDamageable` |
| 方法/属性/事件 | PascalCase | `TakeDamage`, `MaxHp` |
| 字段（含 `[Export]`） | `m_` + camelCase | `m_Hp`, `m_AttackButton` |
| 参数/局部变量 | camelCase | `elapsedSeconds` |
| 常量 | PascalCase，`static readonly` | `MaxLevel` |
| 命名空间 | `GameLogic.<领域>` / `GameConfig` | `GameLogic.Entity` |
| 标识符 | 只用英文，禁中文/拼音 | 中文只在 `NameCn` 列与 UI 文本 |

- 一文件一主类，文件名 = 类名；UI 为 Ge/Logic 双半类（生成 `GameProto/UIGe/` + 手写 `GameScripts/UI/*.Logic.cs`），实体脚本手写（`Entity/AGENTS.md`）。
- 日志只用 `Log.Debug/Info/Warning/Error/Fatal`（条件编译零开销）；游戏逻辑禁 `GD.Print`（例外：SmokeTestDriver 等 stdout 协议输出）。
- 生命/攻防用 `int`，倍率/概率用 `float`，避免混算截断。

### 5.2 async 时序（框架坑）

- `ShowEntityAsync` / `OpenUIFormAsync` 池命中时**同步**触发事件：先注册回调/建 `TaskCompletionSource` 再调用。
- 事件参数用完即回收：不存字段、不进闭包、`await` 后不再读。
- 向 `GF.Event.Fire` 转发 Manager 事件必须 `XxxEventArgs.Create(e)` **复制**，否则双重归还崩溃。
- 涉及实体死亡/切关的 `async` 必须带 `CancellationToken`。

## 6. 框架速查（GGF）

门面定义在 `Framework/GodotGameFrameworkCore/Base/GF.cs`，各模块文档 `Godot/docs/<模块>System.md`（断言以代码为准，缺失/不符时报告）。

| 门面 | 用途 | 常用入口（扩展方法所在文件） |
| --- | --- | --- |
| `GF.Entity` | 实体生灭分组 | `ShowEntity(EntityId.Xxx)` / `ShowEntityAsync<T>` / `HideEntitySafe`（`EntityExtension.cs`） |
| `GF.UI` | 界面开关层级 | `OpenUIForm(UIFormId.Xxx)` / `OpenUIFormAsync<T>` / `CloseUIForm` / `HasUIForm`（`UIExtension.cs`） |
| `GF.Event` | 事件总线 | `Fire(...)` / 订阅取消；参数走 `ReferencePool`（§9） |
| `GF.Fsm` | 纯逻辑状态机 | `CreateFsm / DestroyFsm`（每状态一个类）；动画选择走 AnimationTree（红线 7） |
| `GF.Sound` | BGM/SFX/UI 音 | `PlayBGM / PlaySFX / PlayUISound / StopBGM / SetVolume`（`SoundExtension.cs`） |
| `GF.Resource` | 资源加载 | 开发期 `ResourceMode.Package` + `EnableEditorResLoad` |
| `GF.Scene` | 场景切换 | 关卡切换走这里，不裸调 `ChangeScene` |
| `GF.ObjectPool` | 对象池 | 高频生灭节点走池（红线 6） |
| `GF.Setting` | 用户设置 | `user://settings.cfg` |
| `GF.Archive` | 存档 | `ArchiveSystem<GameCatalogue, GameData>`（§9） |

- 配表入口 `ConfigSystem.Instance.Tables.TbXxx`；流程节点 `MainPack/Scripts/Procedure/`（`docs/ProcedureSystem.md`）。
- 确认是框架 bug 而非用法问题时，走 §13 例外流程。

## 7. 实体与战斗（概览）

> 详细规则：`GameScripts/AGENTS.md`（战斗结算）、`GameScripts/Entity/AGENTS.md`（继承树、生命周期、状态机、物理层表）。

- 实体直接继承 Godot 原生类型 + `IEntity`，无中间框架基类：`ActorEntity : CharacterBody2D, IEntity` → `HeroEntity → WukongEntity...` / `MonsterEntity`。
- 生命周期只实现 `OnInit / OnShow / OnUpdate / OnHide / OnRecycle`；生成/回收走 `GF.Entity.ShowEntity(EntityId.Xxx)` / `HideEntity`。
- C# 维护角色属性，动画选择交给角色自己的 AnimationTree（红线 7）；物理层 13 个语义层名固定，统一 `LayerMask.LayerToMask2D("层名")`，禁止魔法数字（层表见 `Entity/AGENTS.md`）。

## 8. 数据与配置

> 详细规则：`Configs/AGENTS.md`。

- 流程：`Datas/*.xlsx` → 导表 → `GameProto/GameConfig/*.cs` + `DataTables/GameConfigs/*.bytes` → `ConfigSystem.Instance.Tables.TbXxx`。
- 加怪/加装备/加技能**先加表再写代码**；需要新数值报人类加表。

## 9. 事件 / 引用池 / 存档

- 事件参数继承 `GameEventArgs`：`Create()` 取自 `ReferencePool` 并实现 `Clear()`；Fire 后不持有；转发必须新建实例（§5.2）；`IReference` 用完必须 `ReferencePool.Release`。
- 跨模块通信只走事件（不写 `HeroEntity` 直接调 `HudForm` 之类）。
- 存档走 `GF.Archive`（Catalogue/Data 分离）：数据类只放可序列化字段；已发布字段语义禁改，只能加新字段 + 迁移逻辑（Version 策略 M6 补）。
- 设置走 `GF.Setting`（`user://settings.cfg`）；不复刻旧项目的机器码绑定加密。

## 10. 资源与 bundle

> 详细规则：`TheGame/AGENTS.md`。

- 资源只放 6 个预置 bundle 目录（`Sprites/ Audios/ Entitys/ UIs/ Scenes/ DataTables/`），不自建 `Assets/`。
- 新资源 snake_case 英文小写、basename 全树唯一；`.import`/`.uid` 提交 git；移动/重命名只在 Godot 编辑器内做。
- 提交前跑一次 TopMenu「Generate File → Collection Res」确保通过（全树同名会中止）。

## 11. 旧项目迁移

- 旧项目只读：允许读取/分析/抄数值设计；禁止拷 `.gd` 代码、逐行翻译、直接搬 `.tscn`（红线 4）。
- 只搬当前阶段所需（当前 = `wukong` + 小猴子 + `Level_1` 花果山），不全量搬运。
- 遗留资产处理：
  1. PNG/WAV 拷入对应目录，**不带旧 `.import`**；文件名保留原样，语义映射写进 Luban 表（`IconPath` 列）或 `Docs/LegacyAssetMap.md`。
  2. 例外：战斗图集本就是动作名，搬入后必须重命名 `<entity>_<state>`（如 `wukong_wait.png`），否则 Collection Res 因全树重名中止。
  3. 动画数据内嵌旧 `.tscn`，不手工重切片：另存 `.tres` 或用一次性脚本按帧规格生成，规格登记 `LegacyAssetMap.md`。
  4. 旧 TexturePacker 图集无源 `.tpsheet`，复用必须原样保留 `.tres`，不可重新导入。
  5. 判定帧/伤害/掉落只作参考，**人工录入** Luban 表。

## 12. Git

- `main` 受保护；分支 `feature/xxx` / `fix/xxx` / `art/xxx`；提交前缀 `feat: / fix: / refactor: / art: / config: / docs: / chore:`。
- `.gitignore` 必含 `.godot/ bin/ obj/ *.tmp .vs/ *.user`；不提交构建产物、`user://`、本地编辑器配置。
- 素材迁移单独成 commit，不与逻辑改动混提。

## 13. 框架例外流程

`Framework/` 与 `addons/` 只读。确需修改：① 确认不是用法问题（查 `Godot/docs/`）；② 在本节追加例外记录（日期/文件/原因/影响）；③ 最小化改动 + `// [MODIFIED] 原因` 注释。

### 例外记录

（暂无）

## 14. 里程碑：第一个可玩垂直切片

目标：一条能玩的关卡（选人 → 进关卡 → 打小怪 → 清场出门 → 通关结算），跑通全部链路后再铺内容。

| 阶段 | 内容 | 完成标准 |
| --- | --- | --- |
| M0 工程设置 | `project.godot`（渲染/视口/13 物理层名）；`dotnet build` 通过 | 主场景 `GameFramework.tscn` 能跑 |
| M1 素材进场 | wukong / Monster1 / Level_1 素材 + `SpriteFrames .tres` + `LegacyAssetMap.md` | 可播放 wukong idle/run/attack 与猴子 walk/attack |
| M2 配表落地 | `HeroConfig / HeroLevelConfig / MonsterConfig / AttackConfig / BattleConfig / LevelConfig / LevelWaveConfig / LevelSpawnConfig / SoundConfig`；`EntityId`、`UIFormId` 枚举 | 导表成功，`Tables` 可读 |
| M3 英雄控制器 | `HeroEntity` + AnimationTree 表达式状态机（属性驱动，禁 bool 拼状态） | 能跑能跳能连击，动画与状态一致 |
| M4 判定与伤害 | HitBox/HurtBox 动画轨道驱动判定帧；`Battle/DamageCalculator`（三种伤害 + `x/(x+K)`）+ 单测；飘字走 NodePool | 打猴子掉血飘字，伤害与手算一致 |
| M5 怪物 AI | `MonsterEntity` + FSM（Patrol/Chase/Attack/CcLocked/Death），参数读 `MonsterConfig` | 猴子巡逻、追击、攻击 |
| M6 关卡与流程 | `LevelDirector`（波次/场上上限/清场开闸）+ 出口 + `GF.Scene` + HUD | 一关可通关并写入 `GF.Archive` |
| M7 收口 | 清理调试代码；补 `LegacyAssetMap.md` / `Architecture.md`；评估第二阶段 | 自查 §4 架构约束无违反 |

排序原则：先打通一条最窄链路，再横向铺内容。

## 15. AI 协作约定

- 接任务先读规范（§2），产出满足 §4/§5；涉及某系统先读 `Godot/docs/<模块>System.md`。
- 不擅自扩大改动范围、不顺手重构无关文件；规范与现实冲突时停下报告。
- 每次交付说明：改了什么、依据哪条规范、如何验证（构建/运行/单测结果）。
