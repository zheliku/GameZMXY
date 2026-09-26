# AGENTS.md —— GameZMXY（造梦西游·同人向学习项目）

> 本文件是本仓库最高工程规范，人类与 AI 协作者都必须遵守。
> AI 协作者：开始任何任务前先完整读本文件；有冲突时停下来报告，由人类裁决并更新本文件。
> 规范只由人类发起变更。

## 0. 项目一句话

单人 2D 横版动作 ARPG（造梦西游同人向**学习**项目，**禁止商用与公开发布**）。
引擎 Godot 4.7 .NET + C#（.NET 8）+ GGF 框架，游戏代码**全新编写**。
美术/音频/数值设计参考自开源项目 `P:\Godot_Project\ZMXY_BHYH`（下称**旧项目**），旧项目只作素材库与设计参考。

学习目标优先级：**工程规范与可维护性 > 功能数量**。宁可少做一个怪，不留一处硬编码。

## 1. 技术栈与环境

| 项          | 约定                                                                                                              |
| ----------- | ----------------------------------------------------------------------------------------------------------------- |
| 引擎        | Godot 4.7**.NET**（mono 版）+ Godot .NET SDK 4.7.0；可执行文件：`S:\Godot4\Godot4Sharp.exe`（见 §1.1）   |
| 语言        | C#，目标框架`net8.0`（本机 SDK 10.x 可正常构建）；Newtonsoft.Json 为框架本地 dll                                |
| 框架        | GGF，位于`Godot/GodotProject/Framework/`（GameFramework 纯 C# 层 + GodotGameFrameworkCore 桥接层）              |
| 游戏工程    | `Godot/GodotProject/TheGame/`（沿用框架示例目录名，见 §2.2 决策）                                              |
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

## 2. 仓库结构

```
GameZMXY/                            # 仓库根
├─ AGENTS.md                         # 本文件
├─ README.md  LICENSE                # GGF 框架自带，后续替换为项目说明
├─ Configs/
│  ├─ GameConfig/                    # Luban 配置源（数值的唯一真相）
│  │  ├─ luban.conf  Defines/        # 表定义
│  │  ├─ Datas/__tables__.xlsx / __beans__.xlsx / __enums__.xlsx
│  │  ├─ Datas/<业务表>.xlsx         # 实体 / 界面UI / 怪物 / 技能 / 关卡波次 ...
│  │  ├─ CustomTemplate/             # 生成 ExternalTypeUtil.cs / ConfigSystem.cs
│  │  └─ gen_code_bin_to_project{,_lazyload}.{bat,sh}
│  └─ Localization/本地化.xlsx
├─ Tools/                            # Luban.dll / FileServer
└─ Godot/
   ├─ CLAUDE.md                      # 框架原作者的 AI 约定（可参考，冲突以本文件为准）
   ├─ docs/                          # GGF 框架系统文档（查 API 先查这里）
   └─ GodotProject/                  # Godot 工程根
      ├─ GodotProject.csproj / .sln
      ├─ Framework/                  # GGF 框架 —— 只读（改动走 §11）
      ├─ addons/                     # 框架自带编辑器插件（不动）
      └─ TheGame/                    # 游戏本体（沿用框架目录名，§2.1）
```

### 2.1 TheGame/（游戏本体）内部结构

```
TheGame/
├─ MainPack/                        # 主包：随应用打包、不参与热更
│  ├─ Scripts/Procedure/            # ProcedureLaunch / Update / Prelode / Game
│  ├─ Scripts/Resources/*.cs        # EntityGroupRes / UIGroupRes / SoundGroupRes / ArchiveSetting / ScriptGenerateRes ...
│  ├─ Scripts/ObjectPool/           # NodePool 等框架通用组件
│  ├─ Scripts/UI/                   # LoadingForm / QuestionTips 等共享 UI Logic
│  ├─ Resources/*.tres              # 上述 Resources 的实例（被 GameFramework.tscn 注入）
│  ├─ Fonts/  Themes/  UI/          # 字体 / 主题 / 共享 UI 场景
├─ GameScripts/
│  ├─ Battle/                       # 【纯 C#，禁引 Godot】伤害公式、属性快照、Buff 结算、可单测
│  ├─ Entity/                       # 实体 Logic：Heroes/ Monsters/ Bullets/ Items/
│  ├─ UI/                           # 界面 Logic
│  ├─ Event/                        # 自定义 GameEventArgs
│  ├─ Archive/                      # GameCatalogue / GameData（存档数据类）
│  ├─ Manager/                      # 玩法管理器（SingletonNode<T>）
│  ├─ Config/                       # 数值配置的强类型包装（读取 Luban 表后的领域对象）
│  └─ GameProto/                    # 【生成目录】GameConfig / EntityGe / UIGe，禁止手改
├─ DataTables/                      # 【bundle】Luban .bytes + 本地化 .txt
│  ├─ GameConfigs/*.bytes           # Luban 产物
│  └─ Localizations/*.txt           # TopMenu「Generate File → Localization」产物
├─ Sprites/                         # 【bundle】全部贴图/图集（含角色/怪物/UI/特效，结构见 §8.1）
├─ Audios/                          # 【bundle】全部音频（BGM/ 与 SFX/）
├─ Entitys/                         # 【bundle】实体 .tscn（实体表 AssetPath 指向这里）
├─ UIs/                             # 【bundle】游戏界面 .tscn
└─ Scenes/                          # 【bundle】关卡等场景 .tscn
```

### 2.2 目录命名决策（2026-09-26）

**决策：游戏目录保留框架自带的 `TheGame/` 名称，不改名为 GameZMXY。** 仓库名是 GameZMXY，游戏目录名是 TheGame，两者各司其职。

理由：框架有 4 处硬编码路径指向 `res://TheGame/`（下表），改名需要同步的窗口全在框架文件里，收益只是"路径好看"；保留原名 = 与框架文档/示例零偏差，`res://TheGame/` 在本仓库里就是"本游戏"的固定前缀。

| 框架中的 TheGame 引用点（**不要动**）                       | 内容                                                                                                |
| ----------------------------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| `Framework/GodotGameFrameworkCore/Config/GameFolderConstant.cs` | 9 条`res://TheGame/...` 路径常量（Main/Audios/Entities/GameConfigs/Scenes/MainPackResources ...） |
| `addons/TopMenu/GameFrameworkTopMenu.Generate.cs`               | 本地化输出路径、`CollectionResSource = "res://TheGame/"`                                          |
| `addons/ComponentInsoector/ScriptGenerateInspector.cs`          | `ScriptGenerateRes.tres` 路径 + 默认输出路径                                                      |
| `addons/ComponentInsoector/NodePoolInspectorPlugin.cs`          | NodePool 场景扫描根                                                                                 |
| `TheGame/MainPack/Scripts/Resources/ScriptGenerateRes.cs`       | 4 个 Ge/Logic 输出路径                                                                              |
| `Configs/GameConfig/gen_code_bin_to_project*.{bat,sh}`          | `DATA_OUTPATH` / `CODE_OUTPATH` / 两次 `copy` 路径                                            |

若将来真的想改名，按上表同步 6 处 + `Framework/GameFramework.tscn` 4 条 ext_resource + `project.godot` 的 folder_colors，改完：`dotnet build` → TopMenu「Generate File → Collection Res」重生成 → 重跑导表 → 编辑器里确认四个 .tres 注入仍有效。改名不是不可做，只是默认不做。

`GameProto/ResourcesCollectionConstant.cs` **永不手改**。

## 3. 架构红线（违反一律返工）

1. **双层分离**：`GameScripts/Battle/` 与 `Framework/GameFramework/` 禁止引用 `Godot.*`。伤害公式、属性计算、概率判定、Buff 结算全部放 `Battle/`，保证可单元测试，也保证可跨引擎复用。
2. **门面调用**：框架能力只通过 `GF.组件`（`GF.Entity` / `GF.UI` / `GF.Event` / `GF.Sound` / `GF.Resource` / `GF.Scene` / `GF.Fsm` / `GF.Archive` / `GF.ObjectPool` 等）访问。禁止自造 Autoload 单例、静态全局变量、上帝类。
3. **生成代码禁止手改**：`GameProto/`（Ge 半类、Luban 产物、`ResourcesCollectionConstant.cs`）由工具生成、重新生成即覆盖。业务逻辑只写在 `*.Logic.cs`。
4. **禁止移植旧 GDScript**：旧项目禁止拷贝任何 `.gd`、`.uid`、`.tscn` 里的脚本逻辑到本仓库；禁止逐行翻译。理解设计后**用 C# 按新架构重写**。
5. **禁止硬编码数值**：装备属性、怪物数值（HP/防御/AI 参数）、技能系数、掉落、刷怪波次、商品价格一律进 Luban 表。代码里出现数值字面量（除 0/1、向量方向等明显常量）即打回。
6. **高频生灭对象必须池化**：子弹、伤害飘字、掉落物、打击特效走 `NodePool` / `GF.ObjectPool`；禁止裸 `Instantiate + QueueFree`。
7. **状态必须是状态机**：实体行为用 `GF.Fsm`（`Fsm<T>` + 每状态一个类）。禁止用多个 bool 标志位拼状态（旧项目最大教训）、禁止巨型 `if/match` 分支链。
8. **禁止跨场景树穿透**：不写 `GetParent().GetParent()` 找对象、不跨模块 `GetNode` 长链。需要互相引用时按 §7 用事件，或由生成方显式注入。

## 4. C# 编码规范

### 4.1 命名

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

### 4.2 async 时序（框架坑，必须遵守）

- `ShowEntityAsync` / `OpenUIFormAsync` 在对象池命中时**同步触发**事件；必须先注册回调/先建 `TaskCompletionSource` 再调用。
- 事件参数用完即回收：不可存字段、不可存闭包、`await` 之后不可再读其属性。
- Godot 组件向 `GF.Event.Fire` 转发 Manager 事件时必须 `XxxEventArgs.Create(e)` **复制**，否则双重归还崩溃。
- 涉及实体死亡/切关的 `async` 流程必须带 `CancellationToken`，否则换场景后回调会打到已释放节点。

## 5. 实体与战斗规范

### 5.1 实体

实体直接继承 Godot 原生类型 + `IEntity`，无中间框架基类：

```
ActorEntity : CharacterBody2D, IEntity        # 阵营/血量/属性快照/受击入口
  ├── HeroEntity → WukongEntity ...
  └── MonsterEntity → <各怪>
BulletEntity : Node2D, IEntity
DropItemEntity / MagicWeaponEntity ...
```

- 生命周期只实现框架接口：`OnInit / OnShow / OnUpdate / OnHide / OnRecycle`。
- 生成/回收：`GF.Entity.ShowEntity(EntityId.Xxx)` / `HideEntity`；`EntityId` 来自 Luban 生成枚举，禁止魔法数字。
- 不要手写脚本骨架：场景搭好后选中根节点 → Inspector「Generate Script」生成 Ge/Logic 双半类。

### 5.2 状态机

- 英雄标准状态：`Idle / Move / Jump / Fall / Attack1..n / Skill_<SkillId> / Hurt / Stun / Death`。
- 怪物：`Idle / Patrol / Chase / Attack / CcLocked / Death`；Boss 的阶段转换也是**状态**，不写巨型 if 链。
- 状态类单独文件，放 `GameScripts/Entity/.../States/`；状态内用 `await ToSignal(m_Anim, AnimationPlayer.SignalName.AnimationFinished)` 等动画，不用裸 `await` 轮询。
- 打断规则集中成一张表（哪些状态可被 Hurt/Stun/Skill 打断），禁止散落赋值。

### 5.3 攻击与判定

- 一次攻击的数据包统一为**纯 C# 强类型**（`GameScripts/Battle/`）：

  ```csharp
  AttackData { int Power; DamageKind Kind; Vector2 Knockback; int WsGain; BuffSpec[] OnHitBuffs; RollRange Crit; }
  ```
- HitBox / HurtBox 是 `Area2D` 组件节点，命中走引擎 `area_entered`；**判定帧的开关由 AnimationPlayer 轨道驱动**（沿用旧项目手调数据），不使用每帧轮询。
- 命中回调里不写公式：把双方属性快照 + `AttackData` 交给 `Battle/DamageCalculator` 结算，再 `GF.Event.Fire(DamageDealtEventArgs)`，飘字 UI 订阅该事件。
- 攻击者识别：HitBox 上挂对宿主的显式引用（生成时注入）或 `IAttacker` 接口，禁止从碰撞对象爬父节点。
- 伤害类型三种：`Physics / Magic / Real`（Real 不吃防御）。减伤曲线沿用旧项目 `x/(x+K)`，但 **K 值、命中/暴击基数进配置表**，不写死；人怪两侧常数不一致的地方在 `DamageCalculator` 注释里显式说明。
- 所有随机取值（暴击/闪避/掉落）用可注入的 `IRandom`，便于单测与复现。

### 5.4 物理层（写进 `project.godot`，代码只允许按层名引用）

禁止魔法数字，统一 `LayerMask.LayerToMask2D("名字")`：

| 层 | 层名          | 用途                               |
| -- | ------------- | ---------------------------------- |
| 1  | World         | 地面/墙体                          |
| 2  | PlayerBody    | 玩家本体                           |
| 3  | EnemyBody     | 怪物本体                           |
| 4  | Platform      | 单向平台（按下键临时关 mask 穿过） |
| 5  | PlayerHitBox  | 玩家攻击判定（扫 EnemyHurtBox）    |
| 6  | EnemyHurtBox  | 怪物受击判定                       |
| 7  | EnemyHitBox   | 怪物攻击判定（扫 PlayerHurtBox）   |
| 8  | PlayerHurtBox | 玩家受击判定                       |
| 9  | MagicWeapon   | 法宝攻击判定                       |
| 10 | Trap          | 陷阱与陷阱检测                     |
| 11 | Item          | 掉落物拾取检测                     |
| 12 | Exit          | 关卡出口                           |
| 13 | Detector      | AI 索敌/射线检测                   |

## 6. 数据与配置（Luban 管线）

- 流程：`Configs/GameConfig/Datas/*.xlsx` → 跑导表 → 生成 `TheGame/GameScripts/GameProto/GameConfig/*.cs` + `TheGame/DataTables/GameConfigs/*.bytes` → 运行时 `ConfigSystem.Instance.Tables.TbXxx` 访问。
- 加新枚举/表的标准动作：改 `__enums__.xlsx`（EntityId / UIFormId 等）或新增业务表并在 `__tables__.xlsx` 登记 → 导表 → 在《实体.xlsx》《界面UI.xlsx》里配 `AssetPath` → 代码里用编译期安全的枚举引用。
- 每张业务表必须含 `NameCn`、`Desc`；多行表（`mode=map`，默认）还必须含 `Id` 作为索引主键，单行全局表（`mode=one`，如战斗常数）不设 `Id`。
- **有旧项目对应物**的表还必须含 `LegacyId`（旧项目 ID 或键名，用于溯源对照，类型随对应物）。没有对应物的表（如等级曲线、战斗常数、波次/刷怪点）不设该列，避免空列噪音。
- 每个字段必须写中文注释（进生成的 C# XML 文档，IDE 悬停可见）。
- 配表 = 数据，代码 = 逻辑。加怪/加装备/加技能先加表再写代码。
- 遗留资产的文件名不参与语义：图标等资源在表里用**显式路径列**（如 `IconPath`）指向，不用字符串拼接派生（旧项目 `load(".../" + name + ".png")` 的做法禁止复刻）。

## 7. 事件 / 引用池 / 存档

- 事件参数继承 `GameEventArgs`，`Create()` 工厂取自 `ReferencePool`，并实现 `Clear()`。
- Fire 之后不得持有事件对象；转发必须新建实例（见 §4.2）。
- `IReference`（`PhysicsCheck2D` 等）用完必须 `ReferencePool.Release`。
- 跨模块通信只走事件：不写 `HeroEntity` 直接调 `HudForm` 之类。
- 存档统一走 `GF.Archive`（`ArchiveSystem<GameCatalogue, GameData>`，Catalogue/Data 分离）：数据类只放可序列化字段，`Version` 字段必备；已发布字段语义禁改，只能加新字段 + 写迁移逻辑。
- 设置走 `GF.Setting`（`user://settings.cfg`）；**不复刻**旧项目的机器码绑定加密。

## 8. 美术与音频资源规范

### 8.1 目录（`TheGame/` 下，沿用框架 bundle 目录）

```
Sprites/                            # 全部贴图/图集（bundle: SpritesBundle）
├─ Characters/Heroes/<HeroId>/      # wukong / tangseng / bajie / wujing / bailong
├─ Characters/Monsters/<MonsterId>/ # 语义英文 ID，如 huaguoshan_monkey
├─ Equipments/<HeroId>/             # 装备外观图层
├─ Icons/Items/  Icons/Skills/  Icons/Buffs/  Icons/Equipments/
├─ Effects/       # 打击/技能/buff 特效
├─ UI/            # 界面素材（按界面分子目录）
├─ Levels/        # 关卡背景与地块
└─ Number/        # 伤害数字贴图
Audios/                             # 全部音频（bundle: AudioBundle）
├─ BGM/
├─ SFX/<HeroId>/
└─ SFX/Monster/
Entitys/  UIs/  Scenes/             # 实体 / 界面 / 关卡 .tscn（各自 bundle）
```

**为什么不自建 `Assets/`**：框架已经预置了这 6 个带 `*Bundle.tres` 标记的目录作为导出打包单元（见 §8.4），路径常量/`Collection Res` 生成器也围绕 `res://TheGame/` 下这些目录工作；再开一层 Assets/ 只会制造"两个都空一半"的冗余结构。

### 8.2 命名

- **新制作/新整理的资源**：一律 snake_case 英文小写，语义完整。
  - 图集：`<entity>_<state>.png`，如 `wukong_idle.png`、`huaguoshan_monkey_hurt.png`（**basename 必须全局唯一**，理由见 §8.4）
  - 动画名标准：`idle / run / jump / attack_1..n / skill_<skillId> / hurt / death`
  - 图标：`icon_<itemId>.png`、`skill_<skillId>.png`、`buff_<buffId>.png`
  - 音频：`<entity>_<action>.wav|.ogg`
- **从旧项目搬来的资源**：允许保留原文件名（见 §9.3），但**目录必须按 8.1 归位**，并在 `Docs/LegacyAssetMap.md` 登记 `旧路径 → 新路径`。禁止出现新的拼音缩写名。
- 大小写扩展名（`.PNG` / `.TTF`）统一为小写，避免跨平台导出踩坑。

### 8.3 导入与版本控制

- **提交**：`.import`、`.uid` 等边车文件（场景靠 UID 引用）；**忽略**：`.godot/`、`bin/`、`obj/`。
- 资源的移动/重命名**只在 Godot 编辑器 FileSystem 面板内操作**（引用自动修复）；禁止用系统资源管理器直接改名。

### 8.4 `Collection Res` 全树索引约束（重要）

TopMenu「Generate File → Collection Res」会扫描 `res://TheGame/` **全部**非 `.cs`/非 `GameScripts/`/非 `.import`/非 `.uid` 文件生成常量（`Sprites_Characters/Heroes/...` 拼成 `<父目录名>_<文件名>`）：

1. **全树同名文件会导致生成中止**（如出现 `Wait.png` × 145 这类全树重名即触发）。这正是 §8.2 要求图集带实体前缀（`wukong_idle.png` 而非 `idle.png`）的原因——不只是审美，是生成器的硬性约束。
2. 文件名开头是数字会生成 `_数字` 常量（合法但丑）；迁移遗留资源时保留原名的同时，注意旧项目里大量 `1.png / 2.png / 637.png` 会造成全树重名冲突。**处理方法**：搬入后如发现冲突，在编辑器内重命名该文件（生成器会输出具体冲突名单），不要为了"保持原样"牺牲生成器。
3. 生成失败不影响运行（配置走 Luban `AssetPath`、场景走 UID），但本项目把 `Collection Res` 作为常规资源索引手段，视为 CI 的一部分，每次提交前跑一次确保通过。

### 8.5 bundle（导出子包）规则

- 任意目录放一个 `AssetBundle` 类型 `.tres` 标记（右击 → Create New → Resource → 搜 `AssetBundle`，命名惯例 `<DirName>Bundle.tres`），该目录即成为导出单元：构建时目录下文件**从主 pck 剔除**、打进 `<ExportDir>/subpackages/<标记名>.pck`。
- 标记属性：`export_only_imported=true`（默认，pck 里只放 `.ctex/.fontdata/.sample` 等导入产物，不带源文件）、`pack_external_dependencies=true`（把目录外被引用的依赖一并打入）。
- 当前 6 个标记（`Audio/Sprites/Entitys/UIs/Scenes/DataTables`）已覆盖分类粒度；**不要**新增套娃标记（bundle 判定按路径前缀，内外层会互相打架）。以后确实需要"按英雄分包"再单独列例外记录（§11）。
- 开发期 `ResourceMode.Package` + `EnableEditorResLoad` 下 bundle 与否不影响调试，子包只在实际导出/热更流程生效；**不用"资源放在 A 目录、发布前再拷过去"这种两步搬法**。

## 9. 旧项目（ZMXY_BHYH）使用规范

### 9.1 定位

旧项目是**只读素材库 + 设计参考**：`P:\Godot_Project\ZMXY_BHYH`。允许读取、分析、抄数值设计；禁止拷贝 `.gd` 代码、禁止逐行翻译、禁止把旧 `.tscn` 直接搬进本仓库。

### 9.2 按需迁移

只搬当前开发阶段需要的内容（当前阶段 = `wukong` + `Monster_1(小猴子)` + `Level_1(花果山)`）。全量搬运会带来 3000+ 无人使用的文件和无法收敛的重命名债。

### 9.3 遗留资产的处理原则

旧资产的文件名深度耦合旧代码字典（如 `Art/BackPack/AllItems/<拼音>.png`、`Role1_Body_<拼音>.png`），**逐文件重命名不可行也不必要**：

1. PNG/WAV 源文件拷入 `Sprites/`/`Audios/` 对应目录，**不带旧 `.import`**，让新项目重新生成。
2. 文件名保留原样；语义映射写进 Luban 表（`IconPath` 列）或 `Docs/LegacyAssetMap.md`。
3. 战斗中使用的角色/怪物图集本就是动作名（`Wait/Walk/Hit/Hurt/Death`），搬入后**必须**重命名为 `<entity>_<state>`（如 `wukong_wait.png`），否则 §8.4 的 Collection Res 生成器会因全树重名中止。
4. 动画数据：旧项目把 `SpriteFrames`/`AtlasTexture` 内嵌在 `.tscn` 里。迁移时**不要手工重切片**：
   - 优先在旧项目编辑器里把目标角色的 `SpriteFrames` 另存为独立 `.tres`，再拖入新项目；
   - 或写一次性编辑器脚本按固定帧宽高（如 `Monster1/Walk.png` = 64×88 × 4 帧）批量生成 `SpriteFrames`。
   - 生成的 `.tres` 存到 `Sprites/Characters/.../<entity>_animations.tres`，并在 `LegacyAssetMap.md` 记录源图与帧规格。
5. 旧项目 TexturePacker 图集（`*.sprites/*.tres`）已无源 `.tpsheet`，如要复用必须原样保留 `.tres`，不可重新导入。
6. 旧项目的判定帧坐标、伤害数值、掉落表只作参考，**人工录入** Luban 表，不写脚本自动翻译。

## 10. Git 规范

- `main` 受保护；功能分支 `feature/xxx`，修复 `fix/xxx`，素材迁移 `art/xxx`。
- 提交信息前缀：`feat: / fix: / refactor: / art: / config: / docs: / chore:`，一句话说清意图。
- `.gitignore` 必须包含：`.godot/`、`bin/`、`obj/`、`*.tmp`、`.vs/`、`*.user`。
- 不提交构建产物、`user://` 内容、本地编辑器配置。
- 素材迁移单独成 commit（便于回溯与回滚），不与逻辑改动混在同一个 commit。

## 11. 框架例外流程

`Framework/` 与 `addons/` 原则上只读。确需修改框架：

1. 先确认不是用法问题（查 `Godot/docs/<模块>System.md`）；
2. 在本节追加一条例外记录：`日期 / 文件 / 原因 / 影响范围`；
3. 最小化改动，并在改动处注释 `// [MODIFIED] 原因`。

### 例外记录

（暂无）

## 12. 里程碑：第一个可玩垂直切片

目标：**一条能玩的关卡**（选人 → 进关卡 → 打小怪 → 清场出门 → 通关结算），跑通全部架构链路，之后再批量铺内容。

| 阶段          | 内容                                                                                                                                              | 完成标准                                                      |
| ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------- |
| M0 工程设置   | `project.godot`：渲染 `gl_compatibility`、视口 940×590（stretch=`canvas_items`）、13 个 2D 物理层名（§5.4）；`dotnet build` 通过        | 编辑器能打开工程，主场景`GameFramework.tscn` 能跑起来       |
| M1 素材进场   | 搬`wukong`（Role1 精灵图集 + 装备外观按需）、`Monster1`、`Level_1` 背景/地块；生成 `SpriteFrames .tres`；写 `LegacyAssetMap.md`         | 新工程内可播放 wukong 的 idle/run/attack 与猴子的 walk/attack |
| M2 配表落地   | Luban 新增：`HeroConfig` / `MonsterConfig` / `AttackConfig` / `LevelConfig(波次)` / `SkillConfig`；`EntityId`、`UIFormId` 枚举      | 导表成功，`ConfigSystem.Instance.Tables` 能读到数值         |
| M3 英雄控制器 | `HeroEntity : CharacterBody2D, IEntity` + FSM（Idle/Move/Jump/Attack1..n/Hurt/Death）；输入经 InputMap 的语义动作名；**不用 bool 拼状态** | 键盘能跑能跳能连击，动画与状态一致                            |
| M4 判定与伤害 | HitBox/HurtBox + 动画轨道驱动判定帧；`Battle/DamageCalculator`（三种伤害 + `x/(x+K)`）+ 单测；飘字走 NodePool + 事件                          | 打猴子掉血飘字，伤害数字与手算一致                            |
| M5 怪物 AI    | `MonsterEntity` + FSM（Patrol/Chase/Attack/CcLocked/Death），参数全部读 `MonsterConfig`                                                       | 猴子会巡逻、发现玩家后追击攻击                                |
| M6 关卡与流程 | `LevelDirector`（读 `LevelConfig` 波次，按玩家 x 切波、场上上限、清场开闸）；出口 Area2D；`GF.Scene` 切场景；`HUDForm` 显示血条与连击     | 完整一关可通关并写入`GF.Archive`                            |
| M7 收口       | 清理临时调试代码；补`Docs/LegacyAssetMap.md` 与 `Docs/Architecture.md`；评估第二阶段内容                                                      | 提交前自查 §3 红线全部无违反                                 |

排序原则：**先打通一条最窄的链路，再横向铺内容**。不要先做 5 个英雄或 20 只怪。

## 13. AI 协作约定

- 接到任务先读本文件；涉及某系统时再读 `Godot/docs/<模块>System.md`（该文档断言以代码为准，冲突时报告）。
- 产出代码必须满足 §3 红线与 §4 规范；实体/UI 脚本优先用编辑器「Generate Script」的模板结构，不手写样板。
- 不擅自扩大改动范围；不顺手重构无关文件；发现规范与现实冲突时**停下来报告**，由人类更新本文件后再继续。
- 涉及数值时先查表（`Configs/`）而不是在代码里加系数；确实需要新数值时报给人类加表。
- 每次交付说明三件事：改了什么、依据哪条规范、如何验证（构建/运行/单测结果）。
