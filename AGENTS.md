# AGENTS.md —— GameZMXY（造梦西游·同人向学习项目）

> 本文件是本仓库最高工程规范，人类与 AI 协作者都必须遵守。
> 规范只由人类发起变更；AI 有冲突时停下来报告，由人类裁决并更新规范。

## 0. 项目一句话

单人 2D 横版动作 ARPG（造梦西游同人向**学习**项目，**禁止商用与公开发布**）。
引擎 Godot 4.7 .NET + C#（.NET 8）+ GGF 框架，游戏代码**全新编写**。
美术/音频/数值设计参考自开源项目 `P:\Godot-Project\ZMXY_BHYH`（下称**旧项目**），旧项目只作素材库与设计参考。

学习目标优先级：**工程规范与可维护性 > 功能数量**。宁可少做一个怪，不留一处硬编码。

## 1. 技术栈与环境

| 项          | 约定                                                                                                              |
| ----------- | ----------------------------------------------------------------------------------------------------------------- |
| 引擎        | Godot 4.7**.NET**（mono 版）+ Godot .NET SDK 4.7.0；可执行文件：`S:\Godot4\Godot4Sharp.exe`（见 §1.1）         |
| 语言        | C#，目标框架`net8.0`（本机 SDK 10.x 可正常构建）；Newtonsoft.Json 为框架本地 dll                                |
| 框架        | GGF，位于`Godot/GodotProject/Framework/`（GameFramework 纯 C# 层 + GodotGameFrameworkCore 桥接层）              |
| 游戏工程    | `Godot/GodotProject/TheGame/`（沿用框架示例目录名，见 §3.2 决策）                                              |
| 渲染        | `gl_compatibility`（2D 项目首选，与旧项目一致；框架示例的 D3D12/Forward+ 不用）                                 |
| 物理        | 2D 内置物理；不需要 Jolt                                                                                          |
| 视口        | 940×590，stretch =`canvas_items`（与旧项目对齐，便于直接复用其背景/UI 坐标系）                                 |
| 配表        | Luban：源在`Configs/GameConfig/Datas/*.xlsx`，工具在 `Tools/Luban/`                                           |
| 日常构建    | `dotnet build`（工作目录 `Godot/GodotProject`）                                                               |
| 新增 .cs 后 | `"<godot_exe>" --build-solutions --path Godot/GodotProject --no-window -q`                                      |
| 打开编辑器  | `"<godot_exe>" --path Godot/GodotProject --editor`                                                              |
| 导表        | `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`，或编辑器 TopMenu → Generate File → GameConfig File |

### 1.1 引擎路径

- Godot 引擎可执行文件：`S:\Godot4\Godot4Sharp.exe`（已验证 4.7.2.stable.mono）；所有人（含 AI）统一使用这一写法，不依赖 PATH（本机 `godot` 不在 PATH）。
- `godot-mcp`（npm 全局）已安装，可用于跑工程/取调试输出。

## 2. 规范文件分布与阅读规则（先读这节）

规范按"**全局一处、目录就近**"维护，避免同一规则写两份漂移：

| 文件                                        | 管辖范围                                                                |
| ------------------------------------------- | ----------------------------------------------------------------------- |
| `AGENTS.md`（本文件）                     | 全局：红线、编码、框架速查、事件/存档、迁移、Git、里程碑                |
| `Godot/AGENTS.md`                         | 框架目录：Framework/addons 只读规则、系统文档索引                       |
| `Godot/GodotProject/TheGame/AGENTS.md`    | 游戏本体：bundle 资源目录规则（DataTables/Audios/Entitys/UIs/Scenes）   |
| `TheGame/GameScripts/AGENTS.md`           | 游戏代码架构：分层、Battle/、Config/、Manager/ 等目录职责与战斗结算规则 |
| `TheGame/GameScripts/Entity/AGENTS.md`    | 实体：继承树、生命周期、状态机、判定                                    |
| `TheGame/GameScripts/GameProto/AGENTS.md` | 生成代码：禁改清单与再生成流程                                          |
| `TheGame/MainPack/AGENTS.md`              | 主包：框架自带内容的使用边界                                            |
| `TheGame/Sprites/AGENTS.md`               | 贴图：目录/命名/Collection Res 全树索引约束                             |
| `Configs/AGENTS.md`                       | Luban：表结构要求、字段规范、导表流程                                   |
| `Docs/AGENTS.md`                          | 文档：LegacyAssetMap.md / Architecture.md 的维护规则                    |

**阅读规则**：

1. 开始任何任务前先完整读本文件。
2. 改动落在某个目录时，**必须再读该目录及其祖先目录中的 `AGENTS.md`**（就近优先）。未单列规范的目录，遵循根规范 + 最近祖先规范。
3. 冲突裁决顺序：根规范 > 目录级规范 > AI 自行推断；发现规范与代码现实冲突时停下来报告，不擅自改规范。

## 3. 仓库结构

```
GameZMXY/                            # 仓库根
├─ AGENTS.md                         # 本文件（全局规范）
├─ README.md  LICENSE                # GGF 框架自带，后续替换为项目说明
├─ Configs/                          # Luban 配置源（规范：Configs/AGENTS.md）
│  ├─ GameConfig/                    # luban.conf / Defines / Datas / CustomTemplate / 导表脚本
│  └─ Localization/本地化.xlsx
├─ Tools/                            # Luban.dll / FileServer
├─ Docs/                             # 项目文档（LegacyAssetMap.md 等，规范：Docs/AGENTS.md）
└─ Godot/                            # 规范：Godot/AGENTS.md
   ├─ CLAUDE.md                      # 框架原作者的 AI 约定（可参考，冲突以根规范为准）
   ├─ docs/                          # GGF 框架系统文档（查 API 先查这里）
   └─ GodotProject/                  # Godot 工程根
      ├─ GodotProject.csproj / .sln
      ├─ Framework/                  # GGF 框架 —— 只读（改动走 §13）
      ├─ addons/                     # 框架自带编辑器插件（不动）
      └─ TheGame/                    # 游戏本体（§3.1；规范：TheGame/AGENTS.md）
```

### 3.1 TheGame/（游戏本体）内部结构

```
TheGame/
├─ MainPack/                        # 主包：随应用打包、不参与热更（规范：MainPack/AGENTS.md）
│  ├─ Scripts/Procedure/            # ProcedureLaunch / Update / Prelode / Game
│  ├─ Scripts/Resources/*.cs        # EntityGroupRes / UIGroupRes / SoundGroupRes / ArchiveSetting / ScriptGenerateRes ...
│  ├─ Scripts/ObjectPool/           # NodePool 等框架通用组件
│  ├─ Scripts/UI/                   # LoadingForm / QuestionTips 等共享 UI Logic
│  ├─ Resources/*.tres              # 上述 Resources 的实例（被 GameFramework.tscn 注入）
│  └─ Fonts/  Themes/  UI/          # 字体 / 主题 / 共享 UI 场景
├─ GameScripts/                     # 游戏代码（规范：GameScripts/AGENTS.md）
│  ├─ Battle/                       # 【纯 C#，禁引 Godot】伤害公式、属性快照、Buff 结算、可单测
│  ├─ Entity/                       # 实体 Logic：Heroes/ Monsters/ Bullets/ Items/（规范：Entity/AGENTS.md）
│  ├─ UI/                           # 界面 Logic
│  ├─ Event/                        # 自定义 GameEventArgs
│  ├─ Archive/                      # GameCatalogue / GameData（存档数据类）
│  ├─ Manager/                      # 玩法管理器（SingletonNode<T>）
│  ├─ Config/                       # 数值配置的强类型包装（读取 Luban 表后的领域对象）
│  └─ GameProto/                    # 【生成目录】GameConfig / EntityGe / UIGe，禁止手改（规范：GameProto/AGENTS.md）
├─ DataTables/                      # 【bundle】Luban .bytes + 本地化 .txt（生成物，禁手改）
├─ Sprites/                         # 【bundle】全部贴图/图集（规范：Sprites/AGENTS.md）
├─ Audios/                          # 【bundle】全部音频（BGM/ 与 SFX/）
├─ Entitys/                         # 【bundle】实体 .tscn（实体表 AssetPath 指向这里）
├─ UIs/                             # 【bundle】游戏界面 .tscn
└─ Scenes/                          # 【bundle】关卡等场景 .tscn
```

`GameProto/ResourcesCollectionConstant.cs` **永不手改**。

## 4. 架构红线（违反一律返工）

1. **双层分离**：`GameScripts/Battle/` 与 `Framework/GameFramework/` 禁止引用 `Godot.*`。伤害公式、属性计算、概率判定、Buff 结算全部放 `Battle/`，保证可单元测试，也保证可跨引擎复用。
2. **门面调用**：框架能力只通过 `GF.组件`（见 §6 速查表）访问。禁止自造 Autoload 单例、静态全局变量、上帝类。
3. **生成代码禁止手改**：`GameProto/`（Ge 半类、Luban 产物、`ResourcesCollectionConstant.cs`）由工具生成、重新生成即覆盖。业务逻辑只写在 `*.Logic.cs`。
4. **禁止移植旧 GDScript**：旧项目禁止拷贝任何 `.gd`、`.uid`、`.tscn` 里的脚本逻辑到本仓库；禁止逐行翻译。理解设计后**用 C# 按新架构重写**。
5. **禁止硬编码数值**：装备属性、怪物数值（HP/防御/AI 参数）、技能系数、掉落、刷怪波次、商品价格一律进 Luban 表。代码里出现数值字面量（除 0/1、向量方向等明显常量）即打回。
6. **高频生灭对象必须池化**：子弹、伤害飘字、掉落物、打击特效走 `NodePool` / `GF.ObjectPool`；禁止裸 `Instantiate + QueueFree`。
7. **状态必须是状态机**：角色的行为/动画状态由**该角色自己的 AnimationTree 状态机**表达（状态按组收进子状态机：Ground / Air / Attack…，主图只留组间流转）——C# 只维护角色属性（输入/跑档/在空中/普攻段/受击…），状态机每条边用 `advance_expression` 判断属性（见 `GameScripts/Entity/AGENTS.md`）。禁止用多个 bool 标志位拼状态（旧项目最大教训）、禁止巨型 `if/match` 分支链。`GF.Fsm`（`Fsm<T>`）仍可用于纯逻辑状态机（如怪物 AI 决策），但不再用于动画选择。
8. **禁止跨场景树穿透**：不写 `GetParent().GetParent()` 找对象、不跨模块 `GetNode` 长链。需要互相引用时按 §9 用事件，或由生成方显式注入。

## 5. C# 编码规范

### 5.1 命名

| 对象              | 规则                                  | 示例                                             |
| ----------------- | ------------------------------------- | ------------------------------------------------ |
| 类/结构/枚举/接口 | PascalCase，接口加`I`               | `WukongEntity`, `IPoolable`, `IDamageable` |
| 方法/属性/事件    | PascalCase                            | `TakeDamage`, `MaxHp`                        |
| 私有/受保护字段   | `m_` + camelCase                    | `m_Hp`, `m_SkillCdDict`                      |
| `[Export]` 字段 | `m_` 前缀（生成器约定）             | `[Export] public Button m_AttackButton;`       |
| 参数/局部变量     | camelCase                             | `elapsedSeconds`                               |
| 常量              | PascalCase`static readonly`         | `MaxLevel`                                     |
| 命名空间          | `GameLogic.<领域>` / `GameConfig` | `GameLogic.Entity`, `GameLogic.Battle`       |
| 标识符            | 只用英文，禁止中文/拼音标识符         | 中文只出现在表的`NameCn` 列、UI 文本资源里     |

- 一个文件一个主要类，文件名 = 类名。Ge/Logic 双半类：`Xxx.cs`（生成）+ `Xxx.Logic.cs`（手写）。
- 日志只用 `Log.Debug / Info / Warning / Error / Fatal`（条件编译零开销），禁止 `GD.Print`。
- 数值类型统一：生命/攻防用 `int`，倍率/概率用 `float`；避免 `int/double` 混算后的隐式截断。

### 5.2 async 时序（框架坑，必须遵守）

- `ShowEntityAsync` / `OpenUIFormAsync` 在对象池命中时**同步触发**事件；必须先注册回调/先建 `TaskCompletionSource` 再调用。
- 事件参数用完即回收：不可存字段、不可存闭包、`await` 之后不可再读其属性。
- Godot 组件向 `GF.Event.Fire` 转发 Manager 事件时必须 `XxxEventArgs.Create(e)` **复制**，否则双重归还崩溃。
- 涉及实体死亡/切关的 `async` 流程必须带 `CancellationToken`，否则换场景后回调会打到已释放节点。

## 6. 框架使用速查（GGF）

框架能力**只**通过 `GF.门面`（定义在 `Framework/GodotGameFrameworkCore/Base/GF.cs`）访问。查 API 细节先读 `Godot/docs/<模块>System.md`（文档断言以代码为准，冲突时报告）。

| 门面              | 用途               | 文档                                                      | 常用入口（含扩展方法）                                                                                                      |
| ----------------- | ------------------ | --------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| `GF.Entity`     | 实体生灭与分组     | `docs/EntitySystem.md`                                  | `ShowEntity(EntityId.Xxx)` / `ShowEntityAsync<T>(EntityId.Xxx)` / `HideEntitySafe(...)`（`EntityExtension.cs`）     |
| `GF.UI`         | 界面打开/关闭/层级 | `docs/UISystem.md`                                      | `OpenUIForm(UIFormId.Xxx)` / `OpenUIFormAsync<T>(UIFormId.Xxx)` / `CloseUIForm` / `HasUIForm`（`UIExtension.cs`） |
| `GF.Event`      | 全局事件总线       | `docs/EventSystem.md`                                   | `Fire(...)` / 订阅取消订阅；参数走 `ReferencePool`，见 §9                                                              |
| `GF.Fsm`        | 有限状态机（纯逻辑） | `docs/FsmSystem.md`                                     | `CreateFsm(owner, states...)` / `DestroyFsm`；每状态一个类；用于 AI 决策等纯逻辑，**动画状态机走角色自己的 AnimationTree**（红线 7） |
| `GF.Sound`      | BGM/SFX/UI 音      | `docs/SoundSystem.md`                                   | `PlayBGM / PlaySFX / PlayUISound / StopBGM / SetVolume`（`SoundExtension.cs`）                                          |
| `GF.Resource`   | 资源加载           | `docs/ResourceSystem.md`                                | 开发期`ResourceMode.Package` + `EnableEditorResLoad`                                                                    |
| `GF.Scene`      | 场景切换           | `docs/SceneSystem.md`                                   | 关卡切换走这里，不裸调 Godot`ChangeScene`                                                                                 |
| `GF.ObjectPool` | 对象池             | `docs/ObjectPoolSystem.md` / `docs/NodePoolSystem.md` | 高频生灭节点（子弹/飘字/特效）必须走池，见红线 6                                                                            |
| `GF.Setting`    | 用户设置           | `docs/SettingSystem.md`                                 | 存`user://settings.cfg`                                                                                                   |
| `GF.Archive`    | 存档               | `docs/ArchiveSystem.md`                                 | `ArchiveSystem<GameCatalogue, GameData>`，见 §9                                                                          |

- 配表运行时入口：`ConfigSystem.Instance.Tables.TbXxx`（生成代码，见 `GameProto/AGENTS.md`）。
- 流程（Procedure）：`MainPack/Scripts/Procedure/`，文档 `docs/ProcedureSystem.md`。
- 使用新框架能力前先读对应文档；文档缺失或与实现不符时报告，不猜 API。
- 确认是框架 bug 而非用法问题时，走 §13 框架例外流程，不直接改。

## 7. 实体与战斗（概览）

> 详细规则：`TheGame/GameScripts/AGENTS.md`（战斗结算）与 `TheGame/GameScripts/Entity/AGENTS.md`（实体与状态机）。

- 实体直接继承 Godot 原生类型 + `IEntity`，无中间框架基类：`ActorEntity : CharacterBody2D, IEntity` → `HeroEntity → WukongEntity...` / `MonsterEntity`；`BulletEntity : Node2D, IEntity` 等。
- 生命周期只实现框架接口：`OnInit / OnShow / OnUpdate / OnHide / OnRecycle`；生成/回收走 `GF.Entity.ShowEntity(EntityId.Xxx)` / `HideEntity`，`EntityId` 来自 Luban 枚举。
- 状态与动画：C# 维护角色属性，动画状态机是角色自己的 `AnimationTree` 资源（主图分组：Ground/Air/Attack…，动画收在子机里；每条边 = 属性表达式）；纯逻辑状态机用 `GF.Fsm`。判定与伤害结算规则见 `GameScripts/AGENTS.md`。
- 物理层 13 层名称固定（World / PlayerBody / EnemyBody / Platform / PlayerHitBox / EnemyHurtBox / EnemyHitBox / PlayerHurtBox / MagicWeapon / Trap / Item / Exit / Detector），代码统一 `LayerMask.LayerToMask2D("层名")`，禁止魔法数字。层表详见 `Entity/AGENTS.md`。

## 8. 数据与配置（概览）

> 详细规则：`Configs/AGENTS.md`（表结构要求、字段规范、导表流程）。

- 流程：`Configs/GameConfig/Datas/*.xlsx` → 导表 → 生成 `TheGame/GameScripts/GameProto/GameConfig/*.cs` + `TheGame/DataTables/GameConfigs/*.bytes` → 运行时 `ConfigSystem.Instance.Tables.TbXxx`。
- 配表 = 数据，代码 = 逻辑。加怪/加装备/加技能**先加表再写代码**；涉及数值先查表，需要新数值时报给人类加表。

## 9. 事件 / 引用池 / 存档

- 事件参数继承 `GameEventArgs`，`Create()` 工厂取自 `ReferencePool`，并实现 `Clear()`。
- Fire 之后不得持有事件对象；转发必须新建实例（见 §5.2）。
- `IReference`（`PhysicsCheck2D` 等）用完必须 `ReferencePool.Release`。
- 跨模块通信只走事件：不写 `HeroEntity` 直接调 `HudForm` 之类。
- 存档统一走 `GF.Archive`（`ArchiveSystem<GameCatalogue, GameData>`，Catalogue/Data 分离）：数据类只放可序列化字段，`Version` 字段必备；已发布字段语义禁改，只能加新字段 + 写迁移逻辑。
- 设置走 `GF.Setting`（`user://settings.cfg`）；**不复刻**旧项目的机器码绑定加密。

## 10. 资源与 bundle（概览）

> 详细规则：`TheGame/AGENTS.md`（bundle 目录与导出规则）、`TheGame/Sprites/AGENTS.md`（贴图命名与 Collection Res 约束）。

- 资源只放框架预置的 6 个 bundle 目录（`Sprites/ Audios/ Entitys/ UIs/ Scenes/ DataTables/`），**不自建 `Assets/`**。
- 新资源 snake_case 英文小写、语义完整、basename 全树唯一；`.import`/`.uid` 边车文件提交进 git；资源移动/重命名只在 Godot 编辑器内做。
- 每次提交前跑一次 TopMenu「Generate File → Collection Res」确保通过（生成器有全树同名即中止的硬约束）。

## 11. 旧项目（ZMXY_BHYH）迁移规范

### 11.1 定位

旧项目是**只读素材库 + 设计参考**：`P:\Godot-Project\ZMXY_BHYH`。允许读取、分析、抄数值设计；禁止拷贝 `.gd` 代码、禁止逐行翻译、禁止把旧 `.tscn` 直接搬进本仓库。

### 11.2 按需迁移

只搬当前开发阶段需要的内容（当前阶段 = `wukong` + `Monster_1(小猴子)` + `Level_1(花果山)`）。全量搬运会带来 3000+ 无人使用的文件和无法收敛的重命名债。

### 11.3 遗留资产的处理原则

旧资产的文件名深度耦合旧代码字典（如 `Art/BackPack/AllItems/<拼音>.png`、`Role1_Body_<拼音>.png`），**逐文件重命名不可行也不必要**：

1. PNG/WAV 源文件拷入 `Sprites/`/`Audios/` 对应目录，**不带旧 `.import`**，让新项目重新生成。
2. 文件名保留原样；语义映射写进 Luban 表（`IconPath` 列）或 `Docs/LegacyAssetMap.md`。
3. 战斗中使用的角色/怪物图集本就是动作名（`Wait/Walk/Hit/Hurt/Death`），搬入后**必须**重命名为 `<entity>_<state>`（如 `wukong_wait.png`），否则 Collection Res 生成器会因全树重名中止（见 `Sprites/AGENTS.md`）。
4. 动画数据：旧项目把 `SpriteFrames`/`AtlasTexture` 内嵌在 `.tscn` 里。迁移时**不要手工重切片**：
   - 优先在旧项目编辑器里把目标角色的 `SpriteFrames` 另存为独立 `.tres`，再拖入新项目；
   - 或写一次性编辑器脚本按固定帧宽高（如 `Monster1/Walk.png` = 64×88 × 4 帧）批量生成 `SpriteFrames`。
   - 生成的 `.tres` 存到 `Sprites/Characters/.../<entity>_animations.tres`，并在 `LegacyAssetMap.md` 记录源图与帧规格。
5. 旧项目 TexturePacker 图集（`*.sprites/*.tres`）已无源 `.tpsheet`，如要复用必须原样保留 `.tres`，不可重新导入。
6. 旧项目的判定帧坐标、伤害数值、掉落表只作参考，**人工录入** Luban 表，不写脚本自动翻译。

## 12. Git 规范

- `main` 受保护；功能分支 `feature/xxx`，修复 `fix/xxx`，素材迁移 `art/xxx`。
- 提交信息前缀：`feat: / fix: / refactor: / art: / config: / docs: / chore:`，一句话说清意图。
- `.gitignore` 必须包含：`.godot/`、`bin/`、`obj/`、`*.tmp`、`.vs/`、`*.user`。
- 不提交构建产物、`user://` 内容、本地编辑器配置。
- 素材迁移单独成 commit（便于回溯与回滚），不与逻辑改动混在同一个 commit。

## 13. 框架例外流程

`Framework/` 与 `addons/` 原则上只读。确需修改框架：

1. 先确认不是用法问题（查 `Godot/docs/<模块>System.md`）；
2. 在本节追加一条例外记录：`日期 / 文件 / 原因 / 影响范围`；
3. 最小化改动，并在改动处注释 `// [MODIFIED] 原因`。

### 例外记录

（暂无）

## 14. 里程碑：第一个可玩垂直切片

目标：**一条能玩的关卡**（选人 → 进关卡 → 打小怪 → 清场出门 → 通关结算），跑通全部架构链路，之后再批量铺内容。

| 阶段          | 内容                                                                                                                                              | 完成标准                                                      |
| ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------- |
| M0 工程设置   | `project.godot`：渲染 `gl_compatibility`、视口 940×590（stretch=`canvas_items`）、13 个 2D 物理层名（§7）；`dotnet build` 通过          | 编辑器能打开工程，主场景`GameFramework.tscn` 能跑起来       |
| M1 素材进场   | 搬`wukong`（Role1 精灵图集 + 装备外观按需）、`Monster1`、`Level_1` 背景/地块；生成 `SpriteFrames .tres`；写 `LegacyAssetMap.md`         | 新工程内可播放 wukong 的 idle/run/attack 与猴子的 walk/attack |
| M2 配表落地   | Luban 新增：`HeroConfig` / `MonsterConfig` / `AttackConfig` / `LevelConfig(波次)` / `SkillConfig`；`EntityId`、`UIFormId` 枚举      | 导表成功，`ConfigSystem.Instance.Tables` 能读到数值         |
| M3 英雄控制器 | `HeroEntity : CharacterBody2D, IEntity` + FSM（Idle/Move/Jump/Attack1..n/Hurt/Death）；输入经 InputMap 的语义动作名；**不用 bool 拼状态** | 键盘能跑能跳能连击，动画与状态一致                            |
| M4 判定与伤害 | HitBox/HurtBox + 动画轨道驱动判定帧；`Battle/DamageCalculator`（三种伤害 + `x/(x+K)`）+ 单测；飘字走 NodePool + 事件                          | 打猴子掉血飘字，伤害数字与手算一致                            |
| M5 怪物 AI    | `MonsterEntity` + FSM（Patrol/Chase/Attack/CcLocked/Death），参数全部读 `MonsterConfig`                                                       | 猴子会巡逻、发现玩家后追击攻击                                |
| M6 关卡与流程 | `LevelDirector`（读 `LevelConfig` 波次，按玩家 x 切波、场上上限、清场开闸）；出口 Area2D；`GF.Scene` 切场景；`HUDForm` 显示血条与连击     | 完整一关可通关并写入`GF.Archive`                            |
| M7 收口       | 清理临时调试代码；补`Docs/LegacyAssetMap.md` 与 `Docs/Architecture.md`；评估第二阶段内容                                                      | 提交前自查 §4 红线全部无违反                                 |

排序原则：**先打通一条最窄的链路，再横向铺内容**。不要先做 5 个英雄或 20 只怪。

## 15. AI 协作约定

- 接到任务先读本文件；改动落在某个目录时再读该目录的 `AGENTS.md`（§2 阅读规则）；涉及某系统时读 `Godot/docs/<模块>System.md`（文档断言以代码为准，冲突时报告）。
- 产出代码必须满足 §4 红线与 §5 规范；实体/UI 脚本优先用编辑器「Generate Script」的模板结构，不手写样板。
- 不擅自扩大改动范围；不顺手重构无关文件；发现规范与现实冲突时**停下来报告**，由人类更新规范后再继续。
- 每次交付说明三件事：改了什么、依据哪条规范、如何验证（构建/运行/单测结果）。
