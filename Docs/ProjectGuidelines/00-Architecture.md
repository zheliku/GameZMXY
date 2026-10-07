# 00 架构

## 项目与目录

- 单人 2D 横版动作 ARPG 学习项目；禁止商用与公开发布。
- 引擎为 Godot 4.7 .NET，C# `net10.0`；工程根为 `Godot/GodotProject/`，游戏代码与资源位于 `TheGame/`。
- `Framework/` 与 `addons/` 为只读依赖；发现框架问题先定位到具体实现并报告，不在业务目录绕过框架边界。
- `MainPack/` 放启动流程、共享 UI、资源组和通用对象池；`GameScripts/` 放玩法逻辑；其余 bundle 资源目录见 [40-ScenesAndAssets.md](40-ScenesAndAssets.md)。

## 依赖与数据边界

- 框架能力只通过 `GF.*` 门面访问；不新增 Autoload、可变静态全局状态或上帝类。
- `Framework/GameFramework/` 是纯 C# 层，不引用 `Godot.*`。`GameScripts/Battle/` 可用 `Vector2`、`Mathf` 等值类型，但不依赖节点、场景树或引擎全局状态。
- 可调玩法数值的唯一来源是 `Configs/GameConfig/Datas/*.xlsx`；运行时代码读取 Luban 生成表，不复制数值到代码。
- `GameProto/`、`DataTables/`、`ResourcesCollectionConstant.cs`、`Config/ExternalTypeUtil.cs` 和生成器产物只通过源数据与生成流程更新。
- 旧项目 `P:\Godot-Project\ZMXY_BHYH` 只用于玩法、数值与素材核对；不移植 GDScript、场景脚本或旧架构。
- `Framework/`、`addons/`、`Tools/Luban/`、生成物与旧项目均只读。遇到框架缺陷，先查框架文档并核对实现；只有用户明确要求后才做最小框架改动。

## 运行时约束

- 高频创建销毁的实体与表现对象使用 `GF.ObjectPool` / `NodePool`。
- 跨模块通信使用 `GF.Event` 或生成方显式注入；不穿透场景树查找其他模块。
- `GameEventArgs` 使用 `ReferencePool`；分发结束即失效，不保存到字段、闭包或跨 `await` 使用。跨层转发创建新参数实例。
- 框架异步显示 API 可能命中对象池后同步触发事件；先注册回调或创建 `TaskCompletionSource`，再调用 API。切场景或实体死亡相关异步操作使用 `CancellationToken`。
- 实体行为与动画边界见 [30-EntitiesAndCombat.md](30-EntitiesAndCombat.md)；C# 与 XML 文档要求见 [10-CSharp.md](10-CSharp.md)。

## 当前实现边界

- 已落地：悟空、花果山猴子、身体与怪物 AI 状态机、基础伤害结算、伤害飘字池和调试战斗场地。
- `ProcedureGame` 当前加载 `Scenes/Level_1.tscn`；`DebugArena.tscn` 保留为战斗回归场地。关卡实体由 `GF.Entity` 管理，空间锚点由场景（标注集合脚本 + 纯子节点 + 导出条目列表）、阶段与配方由 Luban `LevelConfig`（`LevelId` 主键 + 嵌套多行列表）提供；普通阶段含首波在实际相机右缘抵达阶段右界后激活，特殊阶段可选择区域触发。
- 经验成长、法宝、装备、技能、Buff、关卡门/出口/奖励结算、背包、商店和存档的完整内容（装备、进度等）仍待设计；英雄等级/累计经验/金币由实体直接持有，最小存档读写由已有流程接入，见 [60-GameplayModules.md](60-GameplayModules.md)。

## 业务目录职责

- `GameScripts/Level/` 只在入口放 `LevelController`；`Stage/` 放阶段编排、互斥状态与阶段门/触发区集合，`Camera/` 放相机节点与取景算法，`Spawning/` 放刷怪服务、纯调度与生成点目录，`Markers/` 只放子节点条目与带编辑器标注的抽象集合基类。不要按“所有 Manager”聚合无关领域。
- `Entity/Heroes/Body/` 与 `Entity/Monsters/Body/` 分为 `Core/`（接口、参数、基状态、动画名）、`States/`（具体状态）；英雄输入在 `Body/Input/`。怪物 AI 保持现有 `AI/Core`、`AI/States`、`AI/Attacks`；攻击几何读取在 `Monsters/Attacks/`。
- `Entity/Combat/` 放实体判定组件；`UI/Damage/` 归拢飘字节点与其管理服务。命名空间按领域保持稳定，纯目录移动不迫使调用方改命名空间。
- `GameScripts/Bindable/` 仅保留项目自实现的 `sealed BindableProperty<T>`（`GameLogic.Bindable`）：构造初值、`Value` 与实例事件 `Changed`；不引入 QFramework 模块、只读接口、退订句柄或绑定袋。普通玩家存档快照 `PlayerSaveData` 归 `GameScripts/Archive/`。
- **UI 数据流（定稿）**：连续数值用 `BindableProperty`，UI 订阅具体属性实例，不经过全局事件总线。`ActorEntity` 持有生命/上限，`HeroEntity` 持有等级/累计经验/金币/无双，`LevelController` 持有 `TravelAvailable`；容器公开且只有 getter，业务直接修改 `.Value`。属性只存值、比较和同步通知，不自动执行钳制、死亡或升级；伤害、治疗及命中收益继续由实体业务负责。跨模块业务结果继续用 `GF.Event`。UI 在 GGF `OnOpen` 用具名方法 `+=` 订阅后统一读取初始值，在 `OnClose` 对称 `-=` 退订，不能只靠节点销毁。
- 玩法界面用 GGF `UIForm`：场景放 `UIs/`，脚本放 `GameScripts/UI/`（`Xxx.cs` Ge 样板 + `Xxx.Logic.cs` 业务，可复用子控件放 `UI/Widgets/`）；在 `界面UI.xlsx` 登记 `UIFormId` 后用 `GF.UI.OpenUIForm(UIFormId.Xxx, userData)` 打开。`userData` 显式携带所需实体或关卡引用，简单显示不增加 Model、Session 或 ViewModel 层；HUD 只读取和订阅，属性公开可写不意味着由 HUD 执行玩法结算。
- `MainPack/Scripts/Resources/Groups`、`Pooling`、`Settings`、`Generation` 分别放服务组、池配置、启动设置和生成器设置资源；框架、插件、生成目录及 bundle 根目录保持既有边界。
- 验证代码位于 `Tests/`（纯算法）和 `MainPack/Scripts/Debug/`（实际引擎回归）；`EditorScripts/` 放编辑器/资源验证，不放玩法逻辑。
