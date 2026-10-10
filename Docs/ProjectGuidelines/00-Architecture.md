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
   应用层   流程（MainPack/Procedure）+ 作用域所有者（GameContext、LevelRun）：创建、装配、检查点
   领域层   档案状态（PlayerProfile…）+ 战斗运行时（StatSheet、Vitals、MusouGauge…）
   规则     伤害公式 · 属性汇总 · 经验曲线 · 存档迁移（纯 C#，确定性，可单测）
   配置     Luban 只读表（启动期集中校验）
   框架     GGF（GF.*）
   ```

   领域层与规则不引用节点、场景树、`GF.UI`/`GF.Entity`；`Battle/Stats/` 等纯 C# 目录可整体链接进单测。

2. **作用域所有权**：每份可变状态只有一个所有者，经方法修改；其他代码只读并订阅变更。上层作用域创建并销毁下层。

   | 作用域 | 所有者 | 创建者 | 生命周期 | 内容 |
   | --- | --- | --- | --- | --- |
   | App | GGF + 流程 | 引擎 | 进程 | 只读配置、`GF.Setting` 设置 |
   | Profile | `GameContext`（持有 `PlayerProfile`） | `ProcedureLoadProfile` | 读档 → 回标题 | 英雄成长、钱包；以后的背包、装备、关卡进度 |
   | Run | `LevelRun` | `ProcedureLevel` | 进关 → 结局 | 关内结算、关卡事务、本关统计 |
   | Entity | `ActorEntity` 及其战斗运行时 | `GF.Entity` | Show → Hide | 属性汇总、生命魔法、无双；以后的 Buff、冷却 |

3. **显式依赖**：依赖经构造参数、`Initialize`、`userData` 或流程状态机数据传入。流程间共享的作用域对象放进流程数据（键 `GameContext.DataKey`，GGF 只接受 `Variable`，用 `GameContextVariable` 包装）。

## 允许的全局

只有以下全局可访问，不新增 Autoload、单例节点或可变静态状态：

- `GF.*` 框架门面；
- `ConfigSystem.Instance`（只读配置，框架 UI/实体扩展依赖它，名称不可改）；
- `NodePool.Instance`、`LayerMask.Instance`（框架提供的表现层工具）。

`GameData` / `GameCatalogue` 必须保持全局命名空间与类名（`GF.cs` 写死 `ArchiveSystem<GameCatalogue, GameData>`）；流程类保持全局命名空间（`ProcedureComponent` 按短类名加载）。

## 状态变化与通信

| 场景 | 机制 | 示例 |
| --- | --- | --- |
| 状态源 → 观察者（UI、同作用域对象） | 所有者的 C# 聚合事件 `event Action Changed`，订阅方收到后**一次读取全部需要的值** | `Vitals.Changed`、`HeroProgression.Changed`、`LevelController.TravelAvailableChanged` |
| 作用域内协作 | 直接调用，或子对象的 C# 事件由拥有者订阅 | `LevelSpawner.MonsterDefeated` → `LevelController` → `LevelRun` |
| 跨模块的一次性业务结果 | `GF.Event` 池化事件参数 | `DamageDealtEventArgs`（飘字）、`MonsterDiedEventArgs`（刷怪名额） |

- 一次修改只通知一次；状态源内部先更新全部字段再通知，观察者看不到中间态（如等级已变而上限未变）。
- `GF.Event.Fire` 在下一轮轮询分发；`GameEventArgs` 用 `ReferencePool`，分发结束即失效，不保存到字段、闭包或跨 `await` 使用。有实际消费者才新增事件类型。
- 订阅与退订成对出现在同一作用域的开始/结束边界（`OnOpen/OnClose`、构造/结束方法）；不靠节点销毁兜底。
- 框架异步显示 API 可能命中对象池后同步触发事件；先注册回调或创建 `TaskCompletionSource`，再调用 API。切场景或实体死亡相关的异步操作使用 `CancellationToken`。

## 数据边界

- 可调玩法数值的唯一来源是 `Configs/GameConfig/Datas/*.xlsx`；代码只保留"公式本身即设计"的部分（伤害结算），系数在表。成长、经验、奖励、建档规则都在表里。
- 配置 / 状态 / 规则 / 表现分离：配置是只读表；状态有唯一所有者；规则是纯函数或无状态服务；表现只读状态并订阅变更。
- 存档只经 `SaveService`（项目侧）与 `GF.Archive`（框架侧）读写；规则见 [70-ProfileAndSave.md](70-ProfileAndSave.md)。
- `GameProto/`、`DataTables/`、`ResourcesCollectionConstant.cs`、`Config/ExternalTypeUtil.cs` 和生成器产物只通过源数据与生成流程更新。

## 目录 = 命名空间

`GameScripts/` 下目录与命名空间一一对应（`GameLogic.<目录路径>`）；纯 C# 与节点代码分目录，UI 按界面分子目录。

| 目录 | 层 | 职责 |
| --- | --- | --- |
| `Config/` | 配置 | 启动校验 `ConfigValidator`、Luban 外部类型 |
| `Battle/` | 规则 | 伤害公式、攻击包、结算快照（确定性，随机值由调用方传入） |
| `Battle/Stats/` | 领域 | 属性汇总 `StatSheet`、修正 `StatModifier`/`StatSource`、生命魔法 `Vitals`、无双 `MusouGauge` |
| `Profile/` | 领域 | 档案 `PlayerProfile`、英雄记录与成长、钱包、经验曲线、出战装配 `HeroLoadout`/`HeroStatBuilder` |
| `Save/` | 规则 | 存档 DTO、版本迁移 `SaveMigrator`、映射 `ProfileMapper`、存档入口 `SaveService`；全局 `GameData`/`GameCatalogue` |
| `Session/` | 应用 | 作用域所有者 `GameContext`、`LevelRun` |
| `Entity/` | 表现 | 实体节点、身体/AI 状态机、判定组件；规则见 [30-EntitiesAndCombat.md](30-EntitiesAndCombat.md) |
| `Level/` | 表现 | 关卡控制器；`Stage/` 阶段编排与门，`Camera/` 相机，`Spawning/` 刷怪与调度，`Markers/` 空间条目 |
| `UI/` | 表现 | 每个界面一个子目录（`BattleHud/`）；`Widgets/` 被动控件；`Damage/` 伤害飘字 |
| `Event/` | — | 跨模块 `GF.Event` 参数 |

新玩法按 [60-GameplayModules.md](60-GameplayModules.md) 的落位表放入对应目录；不新建按"所有 Manager"聚合的目录。

## UI 数据流

- 玩法界面用 GGF `UIForm`：场景放 `UIs/`，脚本放 `GameScripts/UI/<界面>/`（`Xxx.cs` Ge 样板 + `Xxx.Logic.cs` 业务 + `XxxData.cs` 打开参数）；在 `界面UI.xlsx` 登记 `UIFormId` 后用 `GF.UI.OpenUIForm(UIFormId.Xxx, data)` 打开。
- 打开参数只携带状态源引用（实体、档案对象、关卡），不复制数值、不管理生命周期。
- 窗口 `OnOpen` 订阅状态源的聚合事件后**立即拉取一次**刷新全部显示，`OnClose` 对称退订；收到事件时重新读取一组一致的值。
- 控件是被动的：只接收数值（如 `ResourceBar.SetValue(current, max)`），不订阅状态源、不执行玩法结算。残影等表现由控件比较前后两次输入自行决定。
- 计时类显示（冷却、Buff 剩余时间）在窗口 `OnUpdate` 读取；一次性反馈（飘字、升级特效、获得物品）走 `GF.Event`。
- 不使用 `BindableProperty` 或字段级可写的可观察容器。

## 运行时约束

- 高频创建销毁的实体与表现对象使用 `GF.ObjectPool` / `NodePool`。
- 不穿透场景树查找其他模块；调试与烟测代码除外。
- 暂停用 `GF.Base.PauseGame()`（时间缩放）；`SceneTree.Paused` 会停止 GGF 轮询。
- 验证代码位于 `Tests/`（纯 C#）和 `MainPack/Scripts/Debug/`（真实引擎回归）；`EditorScripts/` 放编辑器/资源验证，不放玩法逻辑。
