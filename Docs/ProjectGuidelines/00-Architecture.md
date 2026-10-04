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
- `ProcedureGame` 当前加载 `Scenes/DebugArena.tscn`，不代表正式关卡流程。
- 经验成长、法宝、装备、技能、Buff、关卡、背包、商店和完整存档接入仍待设计；见 [60-GameplayModules.md](60-GameplayModules.md)。
