# 00 架构

## 项目与目录

- 单人 2D 横版动作 ARPG 学习项目；禁止商用与公开发布。
- 引擎为 Godot 4.7 .NET，C# `net10.0`；工程根为 `Godot/GodotProject/`，游戏代码与资源位于 `TheGame/`。
- `Framework/` 与 `addons/` 为只读依赖；发现框架问题先定位到具体实现并报告，不在业务目录绕过框架边界。修改例外须用户明确授权，并在 [50-FrameworkAndTools.md](50-FrameworkAndTools.md) 登记。
- `MainPack/` 放启动流程、共享 UI、资源组和通用对象池；`GameScripts/` 放玩法代码；其余 bundle 资源目录见 [40-ScenesAndAssets.md](40-ScenesAndAssets.md)。
- 旧项目 `P:\Godot-Project\ZMXY_BHYH` 只用于玩法、数值与素材核对；不移植 GDScript、场景脚本或旧架构。

## 顶层范式：GGF 分层 + 作用域所有权

不使用 QFramework 式 Architecture / Model / System / Command，也不引入 IOC 容器或服务定位。架构由三条规则组成：

1. **分层**：依赖只能自上而下。

   ```
   表现层   实体节点（英雄/怪物/子弹/掉落）· UI 窗口与控件 · 关卡场景 · 特效
   应用层   流程（MainPack/Procedure）：装配与导航；模块（LevelModule、SaveModule）：各自生命周期与业务
   作用域   GameContext：关联本槽档案与存档模块
   领域层   档案状态（PlayerProfile…）+ 战斗运行时（ActorAttributes、Vitals、MusouGauge…）
   规则     伤害公式 · 属性汇总 · 经验曲线 · 存档映射（纯 C#，确定性，可单测）
   配置     Luban 只读表（启动期集中校验）
   框架     GGF（GF.*）
   ```

   领域层与规则不引用节点、场景树、`GF.UI`/`GF.Entity`；`Battle/Attributes/` 等纯 C# 目录可整体链接进单测。

2. **作用域所有权**：每份可变状态只有一个所有者，经方法修改；其他代码只读并订阅变更。上层作用域创建并销毁下层。

   | 作用域 | 所有者 | 创建者 | 生命周期 | 内容 |
   | --- | --- | --- | --- | --- |
   | App | GGF + 流程 | 引擎 | 进程 | 只读配置、`GF.Setting` 设置 |
   | Profile | `GameContext`（持有 `PlayerProfile`） | `ProcedureLoadProfile` | 读档 → 回标题 | 英雄成长、钱包；以后的背包、装备、关卡进度 |
   | Run | `LevelModule` | `ProcedureLevel` | 进关 → 结局 | 关内结算、关卡事务、本关统计 |
   | Entity | `ActorEntity` 及其战斗运行时 | `GF.Entity` | Show → Hide | 属性汇总、生命魔法、无双；以后的 Buff、冷却 |

3. **显式依赖**：依赖经构造参数、`Initialize`、`userData` 或流程状态机数据传入。流程数据键与池化包装集中在 `MainPack/Scripts/Startup/FlowData/`（`FlowKeys.GameContext` + `GameContextVariable`）；GGF 只接受 `Variable`，领域对象不承担流程传递契约。

## 允许的全局

只有以下全局可访问，不新增 Autoload、单例节点或可变静态状态：

- `GF.*` 框架门面；
- `ConfigSystem.Instance`（只读配置，框架 UI/实体扩展依赖它，名称不可改）；
- `NodePool.Instance`、`LayerMask.Instance`（框架提供的表现层工具）。

`GameData` / `GameCatalogue` 必须保持全局命名空间与类名（`GF.cs` 写死 `ArchiveSystem<GameCatalogue, GameData>`）；流程类保持全局命名空间（`ProcedureComponent` 按短类名加载）。

## 状态变化与通信

| 场景 | 机制 | 示例 |
| --- | --- | --- |
| 状态源 → 观察者（UI、同作用域对象） | 所有者的 C# 聚合事件 `event Action Changed`，订阅方收到后**一次读取全部需要的值** | `Vitals.Changed`、`HeroProgression.Changed`、`LevelModule.TravelAvailableChanged` |
| 作用域内协作 | 直接调用，或子对象的 C# 事件由拥有者订阅 | `LevelSpawnerNode.MonsterDefeated` → `LevelModule` 直接奖励经验 |
| 跨模块的一次性业务结果 | `GF.Event` 池化事件参数 | `DamageDealtEventArgs`（飘字）、`MonsterDiedEventArgs`（刷怪名额）、具名菜单点击事件（窗口 → 当前流程） |

- 子组件 → 模块入口 → 实际消费者是允许的事件边界；禁止没有状态变化或独立职责的 Completed → OnCompleted → Ended 等转发链。能在所属模块直接处理的事实就在模块处理。
- 一次修改只通知一次；状态源内部先更新全部字段再通知，观察者看不到中间态（如等级已变而上限未变）。
- `GF.Event.Fire` 在下一轮轮询分发；`GameEventArgs` 用 `ReferencePool`，分发结束即失效，不保存到字段、闭包或跨 `await` 使用。有实际消费者才新增事件类型。
- 订阅与退订成对出现在同一作用域的开始/结束边界（`OnOpen/OnClose`、构造/结束方法）；不靠节点销毁兜底。
- 框架异步显示 API 可能命中对象池后同步触发事件；先注册回调或创建 `TaskCompletionSource`，再调用 API。切场景或实体死亡相关的异步操作使用 `CancellationToken`。

## 数据边界

- 可调玩法数值的唯一来源是 `Configs/GameConfig/Datas/*.xlsx`；代码只保留"公式本身即设计"的部分（伤害结算），系数在表。成长、经验、奖励、建档规则都在表里。
- 配置 / 状态 / 规则 / 表现分离：配置是只读表；状态有唯一所有者；规则是纯函数或无状态服务；表现只读状态并订阅变更。
- 存档只经 `SaveModule`（项目侧）与 `GF.Archive`（框架侧）读写；规则见 [70-ProfileAndSave.md](70-ProfileAndSave.md)。
- `GameProto/`、`DataTables/`、`ResourcesCollectionConstant.cs`、`Config/ExternalTypeUtil.cs` 和生成器产物只通过源数据与生成流程更新。

## 模块入口与目录

- 流程负责创建、装配、导航和逆序释放；每个业务模块只有一个对外生命周期入口。流程可以组合不同模块，不能同时操作同一模块中的多个控制器/运行对象。
- `LevelModule` 统一阶段、奖励、提交与回滚；`SaveModule` 统一目录读取、读档、建档与检查点。内部组件各有实际职责，不再用第二个 owner 包装入口。
- 入口可以用 `类型.职责.cs` partial 分文件，仍是同一个对象和同一份状态；不拆出相互转发事件的平行入口。
- 模块内部按功能分组，节点、规则和资源与所属功能放在一起；不建立汇集整个模块节点的 Nodes 目录。目录是浏览分组，命名空间是稳定的业务边界。`Level/{Camera,Spawning,Stages,Markers,Exit}` 统一用 `GameLogic.Level`；Save 的 `Data/Slots` 保留子命名空间；实体用 `GameLogic.Entity`、`GameLogic.Body`、`GameLogic.Heroes[.Body]`、`GameLogic.Monsters[.Body/.AI]`；UI 表单沿用生成器的 `GameLogic.UI`，Widgets 使用 `GameLogic.UI.Widgets`。Core/States 不再扩展命名空间。
- 纯状态、配置、输入/输出数据可以作为公开只读契约传给 UI；这不意味着它们另有生命周期入口。`SaveSlotDirectory` 仅是 SaveModule 拥有的目录状态，流程不再创建它或调用它读盘。

阅读地图与调用链见 [GameplayArchitecture](../GameplayArchitecture.md)。

| 目录 | 层 | 职责 |
| --- | --- | --- |
| `Config/` | 配置 | 启动校验 `ConfigHelper`、Luban 外部类型 |
| `Battle/` | 规则 | 伤害公式、攻击包、结算快照（确定性，随机值由调用方传入） |
| `Battle/Attributes/` | 领域 | 属性汇总 `ActorAttributes`、修正 `AttributeModifier`/`AttributeSourceId`、生命魔法 `Vitals`、无双 `MusouGauge` |
| `Profile/` | 领域 | 档案 `PlayerProfile`、英雄成长、钱包、经验曲线；出战输入 HeroLoadout 位于 Battle/Attributes |
| `Save/` | 规则 / 应用 | 根目录保留 `SaveModule` 与加载结果；`Data/` 为存档 DTO 与映射（`GameData`/`GameCatalogue` 保持全局命名空间），`Slots/` 为标题目录与格子摘要 |
| `GameContext.cs` | 应用 | 关联本槽 PlayerProfile 与 SaveModule，随流程数据传递；没有 Session 目录 |
| `Entity/` | 表现 | 实体节点、身体/AI 状态机、判定组件；规则见 [30-EntitiesAndCombat.md](30-EntitiesAndCombat.md) |
| `Level/` | 应用 / 表现 | 根目录 LevelModule（含 Progress partial）；Camera 为取景，Spawning 为生成，Stages 为阶段与区域，Markers 为共享空间条目，Exit 为出口交互 |
| `UI/` | 表现 | 每个界面一个子目录（`BattleHud/`）；`Widgets/` 被动控件；`Damage/` 伤害飘字 |
| `Input/` | 表现 | 全局输入映射名称 `InputActions`；引擎采样在节点，输入缓冲规则在 `Entity/Heroes/Input/` |
| `Event/Combat/`、`Event/Navigation/` | — | 战斗结果与窗口导航的池化 `GF.Event` 参数 |

新玩法按 [60-GameplayModules.md](60-GameplayModules.md) 的落位表放入对应目录；不新建按"所有 Manager"聚合的目录。

## UI 数据流

- 玩法界面用 GGF `UIForm`：场景放 `UIs/`，脚本放 `GameScripts/UI/<界面>/`（`Xxx.cs` Ge 样板 + `Xxx.Logic.cs` 业务 + `XxxData.cs` 打开参数）；在 `界面UI.xlsx` 登记 `UIFormId` 后用 `GF.UI.OpenUIForm(UIFormId.Xxx, data)` 打开。
- 打开参数只携带状态源引用（实体、档案对象、关卡、标题存档目录），不复制数值、不管理生命周期。
- 窗口 `OnOpen` 订阅状态源的聚合事件后**立即拉取一次**刷新全部显示，`OnClose` 对称退订；收到事件时重新读取一组一致的值。
- 控件是被动的：只接收数值（如 `ResourceBar.SetValue(current, max)`），不订阅状态源、不执行玩法结算。残影等表现由控件比较前后两次输入自行决定。
- 计时类显示（冷却、Buff 剩余时间）在窗口 `OnUpdate` 读取；一次性反馈（飘字、升级特效、获得物品）走 `GF.Event`。
- 不使用 `BindableProperty` 或字段级可写的可观察容器。

## 运行时约束

- 高频创建销毁的实体与表现对象使用 `GF.ObjectPool` / `NodePool`。
- 不穿透场景树查找其他模块；调试与烟测代码除外。
- 暂停用 `GF.Base.PauseGame()`（时间缩放）；`SceneTree.Paused` 会停止 GGF 轮询。
- 验证代码位于 `Tests/`（纯 C#）和 `MainPack/Scripts/Debug/`（真实引擎回归）；`EditorScripts/` 放编辑器/资源验证，不放玩法逻辑。
