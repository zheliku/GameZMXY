# 00 GGF + QF 架构与使用规范

## 项目边界

- Godot 4.7 .NET / C# net10.0；工程为 `Godot/GodotProject/`，业务位于 `TheGame/`。
- GGF 提供流程、实体、UI、资源、声音、事件、对象池和存档；QF 的思想用于划分业务职责。普通界面使用 ViewController，复杂展示按需要提取 Presenter/ViewModel。
- 主流程 FSM 的 GetData/SetData 唯一持有 Model、System 和 Utility；Architecture 提供按类型访问，Get 返回所属作用域的同一对象。
- `Framework/`、`addons/`、`Tools/Luban/`、生成物及旧项目为只读边界，例外见 [50](50-FrameworkAndTools.md)。旧项目 `P:\Godot-Project\ZMXY_BHYH` 只作玩法和素材参考。
- 本项目用于学习，禁止商用和公开发布。

## 职责与取用能力

| 角色 | 负责 | 可取用 |
| --- | --- | --- |
| Procedure | 装配、注册、导航、异步任务和逆序释放 | 当前流程 FSM 及 GGF 服务 |
| Model | 唯一权威状态、绑定属性和自身数据查询 | Utility |
| System | 业务校验、计算、修改 Model、协调系统 | Model、System、Utility |
| Controller / Entity | 输入、节点、动画、物理、界面显示和订退绑定 | Model、System、Utility、Command、Query |
| Command | 一次玩家业务修改请求与上下文校验 | Model、System、Utility、Query |
| Query | 一次组合读取、筛选或汇总 | Model、System 的只读方法、Query |
| Utility | 纯转换、共享计算或 GGF 平台适配 | 显式参数或构造注入的第三方服务 |

节点和窗口保留 Godot/GGF 继承链，实现 IController。Model/System 使用各自初始化基类。Utility 直接实现 IUtility，按参数工作。纯规则使用确定性输入，随机值由调用方传入。

## 调用规则

| 需求 | 使用方式 |
| --- | --- |
| 读取 HP、经验、灵魂、背包 | GetModel，读取同一数据 |
| 查询已学技能、施放资格 | GetSystem 的只读方法 |
| 组合查询，如整包出售数量和总价 | SendQuery，返回本次值或不可变结果 |
| 穿戴、卸下、出售、学习等玩家业务修改 | Controller → SendCommand → System → Model |
| 分页、悬停、tooltip、关闭本窗口 | 窗口或控件直接处理 |
| 切场景、返回标题、重试 | 向所属 Procedure 提交具名导航请求 |
| 动作、物理、AI、碰撞与已开始的业务推进 | 具体状态、Entity 与 System 直接协作 |

Command/Query 在发送时绑定当前 Architecture，同步执行并返回 bool、件数或既有业务结果。只携带本次请求参数，不保存跨请求的业务状态，也不自带排队、自动回滚或持久化。Command 调用 System 的规则；System 按提交时的状态重新校验。

Query 是可选的查询组织方式：组合多个来源、筛选或汇总的逻辑放所属业务目录；单个属性、现成的只读方法直接 GetModel/GetSystem。Query 返回执行这一刻的结果，不负责绑定，也不建立持续订阅。观察后续变化直接订阅 Model；组合显示需要更新时，由绑定回调重新执行查询。查询结果不预留物品或承诺成交，Command 提交时仍须重新校验。

Controller 对 Model/System 执行读取和订阅，System 执行业务写入。与 QF 源码相同，ICanGetModel/ICanGetSystem 只限定取用能力，不会把返回对象自动变成只读对象；读写边界按职责遵守，不额外复制一套 UI Model。绑定使用普通属性和集合；关联显示数据用完整的不可变值整体赋给 Value。

## 状态与通信

- 持续状态用 Model 的 BindableProperty、BindableList、BindableDictionary；需要刷新组合结果时，在绑定回调里重新查询。
- 同一作用域内用直接调用或拥有者订阅的 C# 事件；窗口导航使用带窗口编号的具名回调。
- 跨模块的一次性事实用 GF.Event，如伤害、怪物死亡、保存成功。事件参数经 ReferencePool 创建，分发期间复制所需值，分发结束归还。
- 订阅与退订放在同一生命周期的开始/结束处：OnOpen/OnClose、Init/Deinit、StartSession/StopSession。
- 绑定同步通知；GF.Event.Fire 在下一轮框架轮询分发。绑定回调优先级用整数，较大值先执行。

## 所有权与生命周期

| 作用域 | 所有者 | 创建与结束 |
| --- | --- | --- |
| 应用 | ConfigurationModel、SaveModel、共享 Utility 与系统 | ProcedurePrepare → 应用退出 |
| 档案 | ProfileModel、SkillModel、档案业务系统 | ProcedureLoadGame → 返回标题 |
| 本局 | HeroModel、LevelModel、HeroSystem、LevelSystem | ProcedureLevel → 离关 |
| 实体 | 节点、运动、输入消费记录、身体/AI FSM、局部计时 | GF.Entity 的 Show → Hide |
| 窗口 | 页码、悬停、选择、动画及本次订阅 | OnOpen → OnClose |

流程按依赖顺序注册；依赖先于消费者。离开时先取消任务、关窗退订，再停止场景、隐藏实体并逆序移除系统和模型。初始化失败清理已经取得的资源；池化节点每次显示重置局部状态。

异步操作在等待前捕获本周期服务、引用、取消令牌和身份；等待后确认本周期仍有效。无法取消的框架显示结果由原服务回收。可能同步完成的 API 先订阅结果，再提交请求。

## 数据、配置与表现

- 共享运行时数据归 Model；角色输入消费、动作阶段和物理事实归对应运行实例。FormData 携带窗口所需参数，存档 DTO 保存持久数据。
- 玩法数值来自 `Configs/GameConfig/Datas/*.xlsx`，跨表约束由 ConfigurationSystem 在 Prepare 校验；场景绑定由所属初始化边界校验。
- 动画、判定几何、声音时点和视觉参数在原生场景/AnimationLibrary 编辑；具体角色状态处理招式规则。
- 持久状态通过 SaveSystem → ArchiveUtility → GF.Archive 读写，模型与 DTO 的转换集中于 SaveDataUtility；检查点与回滚见 [70](70-ProfileAndSave.md)。
- 生成代码、bytes、UI 绑定与资源常量经源数据和既有生成入口更新，见 [20](20-Configs.md)、[40](40-ScenesAndAssets.md)、[50](50-FrameworkAndTools.md)。

## UI 与目录

- GGF 窗口统一 XxxForm；脚本放 `GameScripts/UI/<窗口>/`，场景放 `UIs/`。需要打开参数的窗口保留自己的 XxxFormData。
- Form 管绑定和请求，完整内部区域用 Panel，单个控件接收数据并报告交互；复杂程度达到独立职责时再拆脚本。
- 目录按业务功能分组，所属节点、状态、规则和资源就近放置。小型配套类型随主要类型，入口较大时按职责使用 partial。
- `MainPack/` 放启动流程、共享 UI、资源组和对象池；业务模块放 `GameScripts/{Architecture,Config,Battle,Profile,Skills,Equipment,Save,Entity,Level,UI,Input,Event,Utility}`。
- 目录用于浏览，命名空间保持业务边界：Level 各子目录统一 GameLogic.Level，Save 的 Data/Slots 保留子空间；实体使用 GameLogic.Entity、Body、Heroes、Enemies；Enemy 下的小怪与首领共用 GameLogic.Enemies，AI 与身体契约分别用其 AI、Body 子空间。窗口用 GameLogic.UI，内部组件沿用 GameLogic.UI.Widgets。

## GGF 接入约定

- 服务通过 GF 门面取用；允许的共享入口为 GF.*、ConfigSystem.Instance、NodePool.Instance、LayerMask.Instance。
- GameData/GameCatalogue 保持全局类名，流程类保持全局命名空间，以满足 GGF 加载契约。
- 高频对象经 GF.ObjectPool / NodePool 回收；暂停用 GF.Base.PauseGame()，玩法计时由游戏帧推进。
- 测试放 Tests、MainPack/Scripts/Debug 或 EditorScripts，使用隔离存档；通过现有状态和反射观测。

实现入口、初始化位置和实际调用链见 [项目导览](../ProjectTour.md)。QF 契约参考 [官方源码](https://github.com/liangxiegame/QFramework/blob/master/QFramework.cs)，职责与 Query 的可选用法参考 [官方教程第 09、10 节](https://github.com/liangxiegame/QFramework/wiki/%E5%BD%93%E5%89%8D%E7%89%88%E6%9C%AC)。本项目保留 GGF 的注册、流程、服务和事件体系，按上述实际用例采用 QF 的职责划分。
