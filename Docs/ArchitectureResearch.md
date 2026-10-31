# 外部框架调研与架构对标审查（2026-10-31）

本轮独立审查文件，回应两个问题：QF 与 GGF 的融合是否朝优秀框架设计看齐；GPT 风格代码的“补丁感”从何而来、用什么纪律消除。既有 [ArchitectureReview](ArchitectureReview.md) 保持原状，本文不并入其中；两者结论相容，本文新增官方 Godot 版、CrazyCar 全层深挖与 star 排序三块证据。

## 0. 结论摘要

1. **骨架不需要推倒重来。** “GGF 管生命周期/流程/实体/UI/资源/声音/事件 + QF 管 Model/System/Command/Query/Bindable 业务组织 + Godot 场景/动画/节点做表现权威”的三边分工，与 QF 官方（含 Godot 版）和最优开源样本（CrazyCar）的实际做法方向一致；本项目的调用契约比它们都严格，属于合法且更紧的项目取舍。
2. **QF 官方立场：四层规则是“理想态”。** 作者亲写的 Godot 架构规范原文明确“通用规则是理想状态下的一套规则，落实实际项目很可能需要修改”。官方示例自己的 Controller 也直接读 Model。因此本项目“Controller 只读订阅、写必经 Command→System、Query 只组合读取”的纪律应定位为项目取舍并用测试守住，不能引用为 QF 强制——这与 ArchitectureReview 第 2 节既有结论一致，本轮补上了官方原文背书。
3. **CrazyCar（1846★）是唯一可溯源的大型开源 QF 项目。** GitHub 全库搜索 QFramework 共 167 个结果，按 star 排序无更高者；商业产品无公开源码，不能推断。CrazyCar 的 README_Dev 规范与实际代码存在差距（Controller 直接读/写 Model、UI 内直接发网络请求），证明“层名不保证调用纪律”。
4. **补丁感的根因不在框架选型，而在判断门缺失。** GPT 式修复倾向就地加转发层、兼容分支、第二份状态，而不是先回答四个问题：数据所有者是谁、写入口是谁、生命周期挂哪个作用域、表现边界在哪。对策是既有第 00 章拆分门 + “优先删除转发层”，本文第 8 节给出可执行的补丁识别清单。
5. **不建议引入的机制：** 第二套 MVC/MVVM 容器、接口优先注册体系（无第二实现）、自动注销框架、嵌套 Command 链、按目录名的 Models/Systems 横切总目录。每项都有外部样本或本项目历史作为反证。

## 1. 动机、方法与边界

- **动机：** 用户判断 QF/GGF 融合不优、GPT 代码像补丁；要求调研 QF/GGF 文档与实际使用它们的开源优秀项目，重点看 QF 项目的设计规范。
- **方法：** 只读取可复核的公开事实——官方 README 与内置文档、固定提交的源码文件、GitHub API 的 star 排序；引用处均给出链接。搜索热度只用于发现候选，不当作质量证明。
- **边界：** 商业产品（《鬼山之下》《当火车鸣笛三秒》等）只有产品清单没有业务源码，不推断其内部设计；GGF 是本仓库内的 UGF 家族移植，外部无同名大型项目，不虚构 GGF 案例，GGF 证据以 `Godot/docs/` 与框架实现为准。

## 2. 调研来源清单

| 来源 | 读取内容 | 链接 |
| --- | --- | --- |
| QFramework 官方 README | 四层架构规则、能力矩阵、产品/群友案例清单 | [README](https://github.com/liangxiegame/QFramework) |
| QFramework 官方 Godot 版 | 仓库内 `QFramework.Godot4.5+`（GDScript）：框架结构、作者亲写架构规范、counter_app 示例 | [目录](https://github.com/liangxiegame/QFramework/tree/master/QFramework.Godot4.5%2B) |
| QF 作者架构文章 | 目录组织建议（功能优先 vs 层优先）与编码规范观点 | [Unity 官方专栏](https://developer.unity.cn/projects/5e6b4711edbc2a0022ab759c) |
| GitHub 全库搜索 | `QFramework` 关键词 167 个结果按 star 排序 | [搜索 API](https://api.github.com/search/repositories?q=QFramework&sort=stars) |
| CrazyCar | README_Dev 规范、`CrazyCar.cs`、System/UI/Game 目录文件树、`HomepageUI.cs` 全文 | [仓库](https://github.com/TastSong/CrazyCar) |
| 既有研究 | CrazyCar 命令/模型、UnityRpgGame、arlozen/MMORPG、Survivor、Foundation、StarForce、UGF | ArchitectureReview 第 2 节及 2026-10-31 补充 |
| 本项目代码 | `ArchitectureContracts.cs`、`LevelSystem.cs`、`ProcedureLevel.cs`、`HeroModel.cs`、`EnemyAiPresets.cs` 等 | 仓库内 |

## 3. QF 官方证据

### 3.1 四层规则与能力矩阵（Unity README 与 Godot 版一致）

- 表现层 Controller：可取 System/Model（Godot 版补注“Utility 不推荐”）、可发 Command/Query、可监听 Event。
- 系统层 System：可取 System/Model/Utility、可收发 Event。
- 数据层 Model：可取 Utility、可发 Event。
- 工具层 Utility：基础设施与第三方集成，“啥都干不了”。
- Command：可取 System/Model/Utility、可发 Event/Command、不可有状态。Query：可取 System/Model、可发 Query、不可有状态。
- 通用规则：Controller 改 System/Model 状态必须用 Command；状态变更通知 Controller 必须用 Event 或 BindableProperty；上层可直接获取下层，下层不能获取上层；下层向上用事件。

本项目的 `ArchitectureContracts.cs` 与此矩阵逐条对齐，并有意识收窄：`ICommand<TResult>` 不提供嵌套 `ICanSendCommand`，跨模块一次性事实交给 GGF Event；`IQuery<TResult>` 明确不订阅不修改。能力矩阵一致、调用纪律更严。

### 3.2 作者立场：规则是理想态，项目可裁剪

Godot 版《[架构规范 与 推荐用法](https://github.com/liangxiegame/QFramework/blob/master/QFramework.Godot4.5%2B/addons/qframework/doc/07.%20Architecture/13.%20%E6%9E%B6%E6%9E%84%E8%A7%84%E8%8C%83%20%E4%B8%8E%20%E6%8E%A8%E8%8D%90%E7%94%A8%E6%B3%95.md)》原文（要点）：

> 通用规则是理想状态下的一套规则，但是落实到实际项目，很有可能需要对以上规则做一些修改。……如果是打算马上用 QFramework 做项目，那么可以在保持原有开发习惯的基础上，一点点引入 QFramework 的概念……直到能完全掌握全部概念，最终能修改和定制 QFramework 源码。

这是对本项目最有分量的外部背书：QF 官方自己把四层规则定位为“可裁剪的理想态”，本项目按关卡制 2D 游戏收窄写入路径（Controller 只读、写必经 Command→System）正是该立场下的合法裁剪。

### 3.3 官方 Godot 4.5+ 版的结构与引擎适配

- 框架本体：`addons/qframework/framework/architecture/`（abstract_command/model/query/system/utility + architecture）与 `framework/corekit/`（easy_event：bindable_property/easy_event/or_event/un_register；easy_fsm）。约 2000 行 GDScript，与 C# 版同构。
- **引擎适配的核心思路是把事件注销挂到节点生命周期**：官方文档专章覆盖“自动注销时机是 exit tree”“当 UI 隐藏时自动注销”“当 Node2D/3D 隐藏时自动注销”。Godot 的可见性/入树信号成为绑定生命周期的锚点。
- 本项目对应做法：Form 的 OnOpen 订阅 + OnClose 对称退订，且顺序要求比官方更强（关窗先于模型移除，OnRecycle 不访问已结束作用域）。方向同构；本项目因作用域注册/移除（进关→离关）而需要显式顺序，不能只用节点销毁兜底。

### 3.4 官方 counter_app 示例的调用纪律

[示例](https://github.com/liangxiegame/QFramework/blob/master/QFramework.Godot4.5%2B/user_guide_code/05.architecture/01.counter_app/counter_app_controller.gd)目录为功能优先：`01.counter_app/` 内分 `commands/ models/ systems/ utilities/`，加 `counter_app.gd`（Architecture 入口）与 `counter_app_controller.gd`。Controller 的实际写法：

- `_ready` 中 `get_model` 后缓存为 `_model`，按钮回调 `send_command(IncreaseCountCommand.new())`；
- 展示层用 `register_with_init_value(...)` 刷新，注销用 `un_register_when_created_child_node_destroyed(self)` 挂节点销毁；
- `_exit_tree` 把 `_model` 置空。

即官方自己的 Controller 也直接读 Model、不设 Query 层——与 Unity 版 README 的严格规则存在实施差距，进一步证明边界是项目取舍。

## 4. CrazyCar 深挖（唯一大型可溯源 QF 项目）

### 4.1 规模与注册入口

[CrazyCar.cs](https://github.com/TastSong/CrazyCar/blob/master/CrazyCarClient/Assets/Scripts/CrazyCar.cs)：单 Architecture 注册 15 个 System、10 个 Model、1 个 Utility，接口优先（`RegisterSystem<IPlayerManagerSystem>(new PlayerManagerSystem())`）。全部为应用期注册，无作用域概念——对局状态靠事件重置 Model，而不是注册/移除。本项目按应用/档案/本局三段生命周期注册移除成员，是关卡制游戏必要的更强约束，CrazyCar（菜单驱动的赛车游戏）不需要也不提供。

### 4.2 README_Dev 规范与实际代码的差距

[README_Dev](https://raw.githubusercontent.com/TastSong/CrazyCar/master/README_Dev.md) 的规则表与 QF 官方基本一致并微调（System 可取 Utility、Command 可发 Command），还声明“ViewController 对 Model 的查询用 Query 实现，从而使得查询变成充血模型”。但 [HomepageUI.cs](https://github.com/TastSong/CrazyCar/blob/master/CrazyCarClient/Assets/Scripts/UI/Homepage/HomepageUI.cs) 实际代码：

- 直接 `this.GetModel<IUserModel>()` 读昵称/星星/VIP；
- `matchBtn` 回调里直接 `await this.GetSystem<INetworkSystem>().Post(...)` 并解析数据、开窗；
- `joinGameBtn` 回调里直接 `this.GetModel<IUserModel>().IsCompleteGuidance.Value = true;`——Controller 直写 Model；
- 事件用 `RegisterEvent<UpdateHomepageUIEvent>(...).UnRegisterWhenGameObjectDestroyed(gameObject)` 自动注销。

结论与 ArchitectureReview 第 2 节（BuyEquipCommand 直写 `IUserModel.Star`）一致：**层名和 README 不保证单一写入者；纪律要靠调用链审查与测试守住，本项目已建立这两样。**

### 4.3 目录组织与可借鉴点

| CrazyCar 做法 | 评价 | 本项目决策 |
| --- | --- | --- |
| 顶层横切层目录（Command/Event/Game/Model/Query/System/UI/Utility），功能内再分子目录 | 与官方“功能优先”建议相反的层优先组织 | 维持用户确认的功能目录 + 领域内配套（官方 Godot 示例即功能优先；QF 作者文章也推荐“先按业务功能划分，再按 MVC 划分”，高内聚） |
| 接口优先注册（`IXxxSystem` + 实现） | 依赖倒置，但 CrazyCar 每个接口只有一个实现 | 不采纳：无第二实现的接口是仪式开销；本项目测试用真实类型 + 构造注入 `IArchitecture`，与“拒绝无消费者抽象”的既有方向一致 |
| System 内领域子结构：`SkillSystem/` 下 Commen/Deployer/ImpactEffects/Selector | 复杂能力在 System 目录内再分子层，而不是拆成一堆顶层 System | 已等价：技能归 `SkillSystem` + 各招式 GGF 状态；战斗公式归 `Systems/Combat`。结构思路相同，落点更 Godot 化 |
| UI 功能目录 + `XxxUI` + `XxxItem` 行组件 + 共享 Alert/Loading | 功能聚组、行组件就近 | 已等价：`XxxForm` + 窗口 `Components/` 约定 |
| `Game/`：MonoBehaviour Controller（MPlayer/CarControl/AIController…）+ `Launch/` 自建启动状态机（InitConfigState/InitNetworkState…） | 玩法实体为引擎对象；全局流程需要 FSM | 已被 GGF 覆盖：Procedure 链即启动状态机，Entity 系统管玩法实体，不自建 |
| `RegisterEvent(...).UnRegisterWhenGameObjectDestroyed(gameObject)` | 注销挂节点销毁，方便但有泄漏窗口（销毁前一直收事件） | 保留显式对称退订：窗口语义（OnClose）先于节点销毁，配合作用域移除顺序更严 |

### 4.4 不值得照搬的部分

- Controller 直写 Model、UI 内直接网络请求与数据解析（DataParseSystem 本应是唯一解析口，却被 UI 绕过调用链）。
- 应用期一次性注册、无作用域清理：菜单型游戏可接受，关卡制 2D 游戏会造成跨局状态残留。
- `GameHelper.cs`（约 10KB 静态工具桶）正是本项目规范禁止的万能 Helper 方向。

## 5. QF 使用者全景（star 排序）

GitHub 全库搜索 `QFramework`（167 结果，2026-10-31）按 star 降序的前列：QFramework 本体 5498★、CrazyCar 1846★、UnityRpgGame 82★、QFramework_HybridCLR 41★、Survivor 24★、qframework-hotfix 23★、VomitLib 13★、QFrameworkHotUpdate 11★。其余均为个位数 star 的演示/热更模板。

判定：**开源世界里不存在比 CrazyCar 更大的、可读源码的 QF 业务项目。** QF 官方产品清单中的商业作品无公开源码，只能证明“能做游戏”，不能证明其团队架构。因此“向优秀 QF 项目看齐”的可操作上限就是：官方文档 + 官方示例 + CrazyCar + 既有研究中的中小样本（UnityRpgGame/MMORPG/Survivor/Foundation），本轮已全部覆盖。

## 6. GGF / UGF 侧证据

- GGF 的模块族（Procedure/Fsm/Event/UI/Entity/Resource/Sound/Setting/Archive/ObjectPool/NodePool/Scene/DataTable/DataNode/Debugger…，见 `Godot/docs/`）与 EllanJiang UnityGameFramework 一一对应；GGF 是该体系在 Godot/C# 的落地。
- UGF 官方示例 StarForce 的证据（Procedure 先订阅开窗结果再开窗、退出退订关窗；MenuForm 提交请求、Procedure 切流程）已在 ArchitectureReview 第 2 节记录，本项目的 Procedure/Form 协作与之一致（SettingsRequested/LevelRequested 具名回调 + 流程拥有导航）。
- GGF 无外部大型案例可引；本项目对 GGF 的用法以 `Godot/docs/` 与框架实现为依据，不虚构案例。这一点维持 ArchitectureReview 的既有边界。

## 7. 与本项目的对标

### 7.1 已对齐且更严格的部分

| 维度 | 官方/CrazyCar | 本项目 |
| --- | --- | --- |
| 能力矩阵 | IController 可读写混合，实际松散 | `ArchitectureContracts.cs` 小接口收窄：Controller 只读订阅；Command 无嵌套发送；Query 只组合读取 |
| 写入口 | Controller 直写 Model 实例存在 | 五类业务写必经 Command→System，System 重新校验 |
| 生命周期 | 应用期注册 + 节点销毁兜底注销 | 应用/档案/本局三段作用域注册移除；关窗先于模型移除；OnRecycle 不碰已结束作用域 |
| 状态所有者 | Survivor 静态 Global、MMORPG System 持节点等反例 | 六个顶层 Model 按所有权/生命周期划分；Model 不持节点；System 不持节点 |
| 启动流程 | CrazyCar 自建 Launch FSM | GGF Procedure 链，不自建 |
| 事件 | QF Event 全局总线 | Bindable 观察状态、具名局部回调管场景协作、GGF Event 管跨模块一次性事实，三层分工明确 |

### 7.2 评估过但不采纳的点（含理由）

见 4.3 表末列：接口优先注册、层优先目录、注销挂节点销毁、CrazyCar 式 System 数量（15 个平铺）。另有官方 Godot 版 EasyFSM——本项目身体/AI 状态机已用 GGF FSM 且有真实故障注入测试，不叠加第二套 FSM。

### 7.3 差距不在结构，在执行一致性

CrazyCar 能当范例不是因为它的规则表，而是每条主链（按钮→Command→System→Model→Event→UI）都能从头追到尾；它的败笔恰恰是链被 UI 内联网络调用打断。本项目的审查方法（完整功能链跟踪：穿戴、批量出售、技能学习、关卡重入）就是对该标准的执行，应继续作为合入门槛。

## 8. 补丁识别清单（对策 GPT 补丁感）

判定为**补丁**的信号（出现任一条即要求重写而不是接受）：

1. 新增类型只转发另一类型的能力，无独立不变量（历史上已删的 HeroLoadout/PlayerModel/SaveLoadResult 包装、EnemyAttackParams 转换层、HeroInputContext 即此类）。
2. 同一事实出现第二份存储：副本 Model、镜像缓存、为兼容保留的旧字段。
3. 修 bug 时在调用点加判空/打补丁而不修所有权（参照 WorkingNotes 关停异常一轮：只给 HttpRequest 加有效性判断会继续暴露声音节点异常，最终改为生命周期修复——这是正确范例的对照）。
4. 为兼容旧行为保留分支，而不是删完整旧链路（参照敌人 AI 两轮：先删旧预设链路再立新规则）。
5. 只有单一消费者的“通用机制”（无消费者采样扩展、空 PauseState/DeathState 曾被删除）。
6. 用手动逐帧同步代替绑定或事件，制造两份状态。
7. 以 `if (IsXxxMode)` 散布分派代替所有权归属（悟空特有规则留在 WukongEntity、霸体留角色层的既有约定）。

判定为**框架对齐**的信号：

1. 一次请求从输入→Command→System→Model→绑定→Form 退订全程可定位，无内联捷径。
2. 生命周期成对出现：Open/Close、注册/移除、Enter/Exit、订阅/退订。
3. 删除旧链路后全量测试仍绿（验证命令见 Validation）。
4. 新增类型能一句话回答四问：所有者/写入口/生命周期/表现边界。

执行门：合入前用第 00 章拆分门四问 + 上表过一遍；GPT 产出的修复先假定有补丁嫌疑，优先找可以删除的层而不是可以增加的层。

## 9. 复核方式

- QF 官方 README：https://github.com/liangxiegame/QFramework
- Godot 版架构规范原文：`QFramework.Godot4.5+/addons/qframework/doc/07. Architecture/13. 架构规范 与 推荐用法.md`
- counter_app 示例：`QFramework.Godot4.5+/user_guide_code/05.architecture/01.counter_app/`
- star 排序：`https://api.github.com/search/repositories?q=QFramework&sort=stars`（2026-10-31 共 167 结果）
- CrazyCar：README_Dev.md、`CrazyCarClient/Assets/Scripts/`（CrazyCar.cs、UI/Homepage/HomepageUI.cs、System/、Game/）
- 本项目对照代码：`GameScripts/Arch/ArchitectureContracts.cs`、`Systems/LevelSystem.cs`、`MainPack/Scripts/Procedure/ProcedureLevel.cs`

未完成项：无代码改动，纯调研与规范对齐文档；未运行验证命令（不涉及）。规范正文（ProjectGuidelines）未修改——若要把第 8 节补丁识别清单升格为规范条款，需按规范维护流程另行提交。
