# GGF + QF 架构审查与重构路线

## 结论

项目不需要在 GGF、QF、MVC 和 MVVM 之间再引入一层框架。当前最合适的组合是：GGF 负责生命周期、流程、实体、UI、资源、声音和事件；QF 只提供业务作用域中的 Model、System、Command、Query 和 Bindable 状态组织；Godot 场景、动画和节点继续是表现与动作编排的权威来源。

关键原则是同一事实只有一份权威状态，每项业务有明确写入口，观察与生命周期对应。System 按业务修改相关 Model，不要求一份 Model 只能由一个 System 使用：例如 ProfileSystem 发放奖励、EquipmentSystem 完成出售，各自负责对应操作的不变量。

| 类型 | 应拥有的东西 | 允许做的事 | 不应做的事 |
| --- | --- | --- | --- |
| Procedure | 作用域装配、导航、异步任务、退出顺序 | 注册/移除 Model 和 System，关闭窗口，取消任务 | 计算装备、属性、伤害或直接改绑定值 |
| Model | 该作用域的权威运行时数据和 Bindable 状态 | 提供数据查询与可观察值，Controller 按约定只读 | 校验业务交易、访问节点、推进时间 |
| System | 一个业务能力的规则、不变量和写入 | 读取 Model/配置，修改所属 Model，协调其他 System | 持有 Godot 节点、承担动画状态、代替 Procedure 导航 |
| Command | 一次外部业务写请求的参数和结果 | 按本项目约定调用负责不变量的 System，由 System 重新校验当前状态 | 跨请求保存状态；本项目还约定不直接写 Model |
| Query | 一次组合读取的参数和结果 | 读取 Model/System 的只读方法并返回不可变结果 | 修改状态、订阅变化、缓存跨帧结果 |
| Utility | 纯规则或明确的平台适配 | 用显式参数转换、序列化或调用外部服务 | 隐藏全局状态、持有 Model/System、变成万能 Helper |
| Controller/Form | 节点表现、局部交互和绑定生命周期 | 读取/订阅状态，发送 Command/Query，请求 Procedure 导航 | 执行业务公式、拥有共享数据副本 |
| Entity/State | 节点、物理、动画、输入消费和动作阶段 | 读取 System 的结果，消费动画/碰撞时点 | 保存档案数据、实现跨实体交易 |

## 当前差距

### 1. QFramework 能力与本项目调用纪律

QFramework 官方 `ICommand<TResult>` 可以取 Model/System/Utility、发送 Command/Event/Query，Controller 可以直接读 Model/System；这套契约本身允许多种调用方式。当前 GGF 适配的 Command 保留取 Model/System/Utility 和 Query 的能力，未提供嵌套发送 Command/Event：跨模块一次性事实交给 GGF Event，避免把嵌套命令链引进项目。

不把 QF 能力表误当作所有项目都必须采用的调用规则。CrazyCar 的公开开发说明要求 Controller 和 System 的 Model 查询走 Query；但 QFramework 官方 README 与源码允许 Controller 直接读 Model。CrazyCar 的实际 `BuyEquipCommand` 也会在异步网络成功后直接写 `IUserModel.Star`，再发事件。因此 Query 是否强制、Command 是否直接提交 Model，是项目选择，不是 QF 唯一答案。

按现行第 00 章，GameZMXY 采用更窄的业务写入路径：Controller/Form 的共享业务修改发送 Command；分页、悬停、动画等局部状态仍直接处理。Command 持有本次参数，调用负责业务不变量的 System；System 校验并修改 Model；Query 只做组合读取，不订阅、不修改。简单字段直接读 Model，组合多个来源或有筛选、汇总的读取才建 Query。当前仅在战斗开放的装备交易和学习 Command 通过 `LevelSystem.IsRunning` 判断会话，不在每个 Command 重复解析 `LevelModel`。未来地图学技开放时应修改对应入口条件，不能把“必须存在战斗”当作通用 Command 规则。System 写 Model 是本项目取舍，不能描述成 QF 的硬性限制。

### 2. 外部项目调研结论

本次设计参考了以下公开项目和官方资料，结论是采用职责而不是再叠加一个 MVC/MVVM 容器：

| 来源 | 实际核对到的规则 | 在本项目的落点 |
| --- | --- | --- |
| [QFramework](https://github.com/liangxiegame/QFramework) 的 README 与 [`QFramework.cs`](https://github.com/liangxiegame/QFramework/blob/master/QFramework.cs) | Controller、System、Model、Command、Query、Utility 是可组合能力；Controller 改状态经 Command，绑定/Event 观察状态。Command 能力较宽，QF 没有规定所有写入只能经过 System | 保留 GGF 的 Procedure/Entity/UI 生命周期；本项目明确选择 System 作为业务不变量与共享 Model 写入者，不复制第二个 Architecture |
| [CrazyCar](https://github.com/TastSong/CrazyCar) 的 [`README_Dev.md`](https://github.com/TastSong/CrazyCar/blob/513932d9f5641ea97082fae0ed206af67129bcfd/README_Dev.md)、[`BuyEquipCommand`](https://github.com/TastSong/CrazyCar/blob/513932d9f5641ea97082fae0ed206af67129bcfd/CrazyCarClient/Assets/Scripts/Command/BuyEquipCommand.cs#L23-L29) 与 [`UserInfoQuery`](https://github.com/TastSong/CrazyCar/blob/513932d9f5641ea97082fae0ed206af67129bcfd/CrazyCarClient/Assets/Scripts/Query/UserInfoQuery.cs) | QF 官方列出的开源赛车案例。开发说明规定查询走 Query；实际购买 Command 在异步网络成功后直接改 Model 并发事件，Query 只在执行时复制用户信息。它展示 QF 的灵活性，也说明仅看层名不能保证单一写入者 | 借鉴业务请求与组合查询分开；交易规则集中在 GameZMXY 的 Equipment/Skill System，Query 不承担绑定。这里的 UserInfo 包含引用字段，不能据此宣称 Query 返回值天然不可变 |
| [On Wasteland / UnityRpgGame](https://github.com/llyy12333/UnityRpgGame/tree/eea9dbbaff8c0e9ef66f3327cbcc1fd3df431a56)（README 链接到 [Steam Demo](https://store.steampowered.com/app/2111070/_On_Wasteland/)） | 作者说明使用 QFramework，涵盖 RPG 背包、战斗、任务、商店等；抽查 [`PlayDataManager`](https://github.com/llyy12333/UnityRpgGame/blob/eea9dbbaff8c0e9ef66f3327cbcc1fd3df431a56/Assets/Scripts/DataManager/PlayDataManager.cs) 仍有静态单例和 `BinaryFormatter`，因此能证明 QF 用于较完整 RPG Demo，不能当作当前存档或架构规范 | 不照搬旧实现；保留本项目严格当前格式、显式存档系统和 GGF 作用域 |
| [Unity Game Framework](https://github.com/EllanJiang/UnityGameFramework) 官方仓库 | Procedure、Entity、UI、Resource、Sound、Event、FSM、DataTable 是生命周期和基础设施模块；Procedure 用 FSM 解耦全局流程 | GGF 管流程、实体、UI、资源、声音、事件和 FSM；玩法规则仍由本项目 System/Model 持有 |
| [nn555k/unity-game-foundation](https://github.com/nn555k/unity-game-foundation) 的 [`Architecture.md`](https://github.com/nn555k/unity-game-foundation/blob/a83c7ac039db35384d5e755fca01e8edef3d1f13/Docs/Architecture.md#L32-L50) 与能力路由文档 | 这是社区 QF 基础设施项目，不是 Unity 官方产品。它把可复用 Core/Save/HotUpdate/UI 包和项目自己的 Models/Systems/Commands/玩法分开；公共能力要求有替换边界，并明确先验证真实复用再提取接口 | 借鉴基础设施与游戏规则分离；GGF 已提供资源/UI/声音，不再复制其模块。存档迁移策略仍按 GameZMXY 的当前格式规范 |
| [UGF StarForce](https://github.com/EllanJiang/StarForce) 的 [`ProcedureMenu`](https://github.com/EllanJiang/StarForce/blob/master/Assets/GameMain/Scripts/Procedure/ProcedureMenu.cs) 与 [`MenuForm`](https://github.com/EllanJiang/StarForce/blob/master/Assets/GameMain/Scripts/UI/MenuForm.cs) | 官方示例。Procedure 先订阅开窗结果、再发起开窗，退出时退订和关窗；MenuForm 接收流程引用并提交开始请求，由 Procedure 更新 FSM 和切换流程 | 导航继续归 Procedure；业务交易才使用 Command，不将开关 tooltip、每帧运动或每次导航都包装成业务命令。FormData 按本项目既有规范保留 |
| [Godot 场景与脚本文档](https://docs.godotengine.org/en/stable/getting_started/step_by_step/scripting.html) | 场景声明节点组合，脚本承担行为；节点只有进入 SceneTree 后才获得处理、输入和渲染能力 | 场景/AnimationPlayer/碰撞轨道是表现时点权威，数据模型不引用 Node |

这些公开资料没有证明“QF 的能力越收紧越好”，也没有证明“类越多越规范”。能跨项目借鉴的证据是：基础设施和玩法有清楚边界、Model 有唯一生命周期所有者、玩家交互有明确写入入口、查询结果不伪装成持续绑定。GameZMXY 采用的严格 System 写入规则是本项目取舍，后续需用实际调用和测试守住。

2026-10-31 再次联网核对上述 QF 官方 README/源码、CrazyCar 固定提交和 Foundation 固定提交，并读取 StarForce 实际代码。本地只读研究副本位于忽略目录 `.godot/validation/{crazycar_qf,qf_rpg,qf_foundation}`。QF 官方产品清单还列有《盒子面包坊》《当火车鸣笛三秒》等已发布作品，但未取得这些作品的公开业务源码；产品清单只能证明应用案例，不能证明其团队规模、内部架构或代码规范。现有证据不足以声称研究了大型商业 QF 项目的内部设计。GGF 部分以仓库对应版本的 `Godot/docs/` 和框架实现为依据，不虚构 GGF 大型案例。

据此采用的审查方法是跟踪完整功能链，而非照搬目录：输入到 Command 的一次请求、System 的业务校验、Model 的状态变化、Form 的刷新与退订，以及离关后的注册释放都要能定位。当前穿戴、批量出售、技能学习和关卡重入用于验证这条链。


### 本轮补充：模型数量、嵌套与 Architecture（2026-10-31）

本轮通过 GitHub 搜索筛选实际使用 QF 的公开项目，再读取固定提交源码。搜索热度只用于发现候选，不当作质量证明；商业产品清单没有公开内部源码，不能推断其团队架构。

| 项目与具体源码 | 实际观察 | 采用与保留的边界 |
| --- | --- | --- |
| CrazyCar [`CrazyCar.cs`](https://github.com/TastSong/CrazyCar/blob/513932d9f5641ea97082fae0ed206af67129bcfd/CrazyCarClient/Assets/Scripts/CrazyCar.cs)、[`UserModel`](https://github.com/TastSong/CrazyCar/blob/513932d9f5641ea97082fae0ed206af67129bcfd/CrazyCarClient/Assets/Scripts/Model/UserModel.cs)、[`EquipModel`](https://github.com/TastSong/CrazyCar/blob/513932d9f5641ea97082fae0ed206af67129bcfd/CrazyCarClient/Assets/Scripts/Model/EquipModel.cs) | 一个 Architecture 注册10个 Model、15个 System；UserModel 保存多个相关 Bindable 和普通 EquipInfo，EquipModel 拥有独立装备目录。没有每个 System 必配一个 Model，也没有 UserModel 嵌套注册 EquipModel | 借鉴按业务边界组合普通数据，不追求 Model/System 数量相等；不复制其网络/UI基础设施 |
| UnityRpgGame [`PlayerDataModel`](https://github.com/llyy12333/UnityRpgGame/blob/eea9dbbaff8c0e9ef66f3327cbcc1fd3df431a56/Assets/Scripts/UI/Player/PlayerDataMessageManager.cs)、背包 Model | 玩家模型聚合 hp/mp/攻击/等级等，背包单独；也把协程/规则放进 Model，有静态数据管理。因此它是实际玩法案例，不能照搬为严格边界标准 | 保留聚合思想，仍按本项目规范把规则放 System、节点/动作放 Entity |
| arlozen/MMORPG [`GameApp`](https://github.com/arlozen/MMORPG/blob/a264612f7c47a15595ec16d35905b2167760cd72/MMORPG/Assets/Scripts/Global/GameApp.cs)、[`UserModel`](https://github.com/arlozen/MMORPG/blob/a264612f7c47a15595ec16d35905b2167760cd72/MMORPG/Assets/Scripts/Model/UserModel.cs) | README 明确为练习 Demo；QF 客户端一个 Architecture 注册2个 Model、5个 System，账号/地图分开，PlayerManagerSystem 直接持有实体视图 | 证明 Model/System 不要求一一对应；GGF 已负责实体生命周期，本项目不让 System 持有 Node。其服务端 Actor.BuffManager 不使用 QF，不能冒充 QF 官方 Buff 模式 |
| FM-Better/Survivor [`Global`](https://github.com/FM-Better/Survivor/blob/e8c09f9e9af782b61781aaa55a4ad23755488612/Assets/Scripts/Global.cs)、[`ExpUpgradeSystem`](https://github.com/FM-Better/Survivor/blob/e8c09f9e9af782b61781aaa55a4ad23755488612/Assets/Scripts/System/ExpUpgrade/ExpUpgradeSystem.cs) | 小型2D案例；一个 Architecture 注册4个 System，生命/升级/技能状态直接为静态 Bindable，没有独立 Model | 用作反例：QF 不自动保证所有权清楚，不能因同为2D就照搬静态 Global，更不能声称它采用了领域 Model |
| Foundation [`ProjectLayout`](https://github.com/nn555k/unity-game-foundation/blob/a83c7ac039db35384d5e755fca01e8edef3d1f13/Docs/ProjectLayout.md) | 社区模板按 Models/Systems 分层、要求唯一 Architecture；也建议缓存依赖。这些是该项目约定 | 借鉴单一入口与职责；本项目按既有规范和用户浏览习惯采用功能目录、保留即时 Get，不把不同约定说成统一行业标准 |

研究缓存位于忽略目录 `Godot/GodotProject/.godot/validation/qf_domain_research_20261031/`；固定提交与已取回源码可复核。已有 `.godot/validation/` 中的三份研究副本继续保留。这些证据支持“领域状态聚合、System 按规则拆分、单一入口”，不支持“大 Model 嵌套多个已注册 Model”或“为控制类数另建多个 Architecture”。

### 3. Model 按状态所有者和生命周期划分，不按字段分类

`HeroModel` 继续作为当前本局英雄的一份权威战斗状态，容纳最终属性、HP/MP/WS、受击节拍、恢复节拍、当前技能 ID 和技能无敌区间。这些值都属于同一个在场英雄，受同一关卡重建和死亡规则管理；再按生命、魔法、无双、攻击拆成多个 Model 会制造同步和所有权成本。

中毒/冻结拥有独立规则，但当前只有英雄一个宿主，所以数据归 `HeroModel.StatusEffects`，规则归 `HeroStatusSystem`。它与 `HeroSystem` 共用同一 HeroModel，不另造效果 Model，也不因合并数据而把不同规则全塞进一个 System。效果数据是普通集合和小类型，不实现 IModel、不注册、不复制。

技能的 `Type` 和 `Func<ISkillActor, bool>` 登记也已从 `HeroModel` 移出，由 `SkillSystem` 在当前英雄显示周期中持有，并在实体隐藏/系统结束时清理。`HeroModel` 只保留当前激活技能 ID 和时钟事实；具体状态仍由英雄 Entity/FSM 提供。这避免数值 Model 引用节点状态类型和可执行委托，也不额外创建 SkillRuntimeModel。

### 4. System 粒度应按业务能力划分

当前 `HeroSystem` 仍负责成长/装备属性汇总、生命魔法、无双、受击保护与恢复；最大生命/魔法变化会同时调整当前资源，无双与死亡/受击规则也互相影响。当前先保留单一写入口，不因 700 行文件本身立即拆成多个互相转发的 System。只有当属性、资源或无双有明确独立所有权、生命周期和独立测试，再做窄切片迁移。

已独立的能力是：`HeroStatusSystem` 负责冻结/中毒的施加、刷新、秒制推进和清理；`SkillSystem` 负责学习、费用、激活校验以及当前技能状态登记；`CombatSystem` 负责纯战斗公式与攻击快照，不拥有英雄 Model。具体动画动作仍归英雄状态。

### 5. 状态效果不是身体状态

Walk、Hurt、Attack 等是互斥的动作状态；中毒、冻结、无双保护、减速和护盾是可叠加或可并存的状态效果。状态效果不进入身体 FSM，也不在 `AbstractHeroEntity._PhysicsProcess` 中直接写规则。

统一生命周期：

```text
命中/技能/道具
    -> HeroStatusSystem.ApplyFreeze/ApplyPoison(config)
    -> HeroModel.StatusEffects 保存效果计时和持续伤害状态
    -> AbstractHeroEntity 按游戏 delta 调用推进
    -> 冻结查询限制控制；毒伤返回本步伤害，由 HeroSystem 扣血
    -> Entity 播放冰块/中毒视觉
```

当前效果状态只保存实现所需的类别、剩余秒数、毒伤、跳数和下次触发间隔，不提前泛化来源/层数等未使用字段；配置只保存已有策划输入，刷新规则属于 System。暂停不推进，死亡/离关由作用域拥有者清空。视觉节点由 Entity 读取状态结果，System 不访问节点。

### 6. Bindable 只表示“可观察状态”，不是通用事件总线

- 单值或不可变组合值使用 `BindableProperty<T>`；Model 通过一次完整值替换保持关联字段一致。
- 集合使用 `BindableList<T>`/`BindableDictionary<TKey,TValue>`，仅当 UI 或其他业务确实需要观察集合变化时使用。
- 逐项事件只用于局部 UI/列表增删，不替代业务事件。
- 纯内部计时、临时缓存和 FSM 状态不做 Bindable；它们没有 UI 观察者。
- `RegisterWithInitValue` 只在打开窗口或建立观察时使用；System 之间优先直接调用或显式事件，不为了“方便”把所有字段暴露成 Bindable。
- 优先级只解决同一状态源的确定性刷新顺序，不能用来编排跨系统业务。

### 7. Utility 分成纯规则和适配器

当前 `ExperienceCurveUtility` 是纯规则，`SaveDataUtility` 是 DTO 转换，`ArchiveUtility` 是 GGF 存档适配，`SpatialUtility` 是空间规则；`IUtility` 是取用能力的标记，不意味着每个纯静态函数都须注册。保留能表达现有职责的名称；后续出现独立功能时才考虑以下命名：

- `...Rule` / `...Calculator`：纯函数或可测试规则，显式传参，不注册也可以；
- `...Mapper` / `...Serializer`：DTO/存档转换，不访问节点；
- `...Gateway` / `...Adapter`：外部框架或平台适配，拥有明确的注入依赖；
- 不新增无领域名的万能 `Helper`/`Utility`；现有类型只有在职责不清或真实变更时才重命名，不机械引入 Mapper/Gateway/Adapter 一整套层。

## UI 标准

每个 Form 遵循同一条链：

```text
Procedure.OpenUIForm(FormData)
    -> Form.OnOpen：保存本窗口参数，订阅 Model，立即刷新
    -> 输入：局部状态由 Form/Panel 持有，业务修改 SendCommand，组合读取 SendQuery
    -> Form.OnClose：先退订和取消本窗口任务，再清除参数
    -> Form.OnRecycle：只清除节点局部状态，不访问已结束作用域
```

UI 不直接写 Model；简单读取可以直接 `GetModel`，组合读取才建 Query；Query 不负责绑定。FormData 是打开参数契约，不是第二份 Model，也不把整个架构塞进 Data。

## Godot/GGF 边界

- GGF 的 Procedure、Entity、UI、Resource、Sound、Event、ObjectPool 和 Archive 保留现有生命周期，不在业务层复制一套管理器。
- Godot 场景、AnimationLibrary、CollisionShape2D 和方法轨道是表现编排权威；System 不替代动画轨道维护帧时点。
- Entity 拥有节点和池化复位；Model/System 不保存 Node 引用。
- 跨模块一次性事实使用 GGF Event；同一作用域的业务结果优先直接返回或回调，避免把所有变化都提升为全局事件。
- 每个 FSM 作用域按领域聚合 Model/System/Utility。领域内部的子数据不单独注册；只有拥有独立生命周期、独立观察者或独立业务不变量时才提升为顶层 Model/System。应用、档案、本局和实体的生命周期不能混用。

## 迁移顺序和完成门

1. **调用纪律与生命周期**：保留当前 Command 取 Model/System/Utility/Query 能力；事件使用 GGF。按现行规范保持业务写入口，Query 只读且无订阅。已修复 Model/System 初始化失败和退出失败的 FSM 清理路径，纯规则测试与真实 GGF FSM 故障注入均通过，证据见 Validation。
2. **英雄聚合**：效果数据归 HeroModel.StatusEffects，HeroStatusSystem 与 HeroSystem 分别负责效果和数值规则；删掉效果 Model 的注册、查询、退出和测试分支，不保留两份状态。
3. **Model 职责收敛**：技能 Type/起手委托已从 HeroModel 移入 SkillSystem；属性与资源暂留一个 HeroSystem，除非后续证据能支持独立所有权，不创建兼容转发层。
4. **Utility 归类**：已区分纯规则、数据转换和平台适配；保留当前有实际职责的类型，不为命名统一批量新增接口或转发层。
5. **UI 一致性**：已审查 Inventory、Skill、Battle 和菜单窗口的绑定/请求生命周期；简单字段直接读，组合出售摘要走 Query。运行结论与未通过的尝试分别记录于 Validation。
6. **删除过渡层**：每完成一个切片，删除旧字段、旧转发方法和重复观测 API；不保留两套状态效果或两套注册容器。

每一步必须通过对应纯规则测试、作用域生命周期测试、真实 Godot 烟测和 `validate_game_references.py`。不以全项目编译通过代替职责迁移完成。

## 当前落地与下一次扩展的标准

英雄领域位于 `GameScripts/Models/` 与 `GameScripts/Systems/Hero/`：`HeroModel` 保存本局数值、当前技能事实和 StatusEffects；`HeroSystem` 维护属性/资源/无双/受击规则，`HeroStatusSystem` 维护冻结/中毒刷新与时钟，`SkillSystem` 管学习和施放规则。Model/System 不是一一配对，也不要求每个 System 独占一个 Model。每项状态只有一份数据，每条业务规则有明确写入口。

当前其余顶层模型保持：ConfigurationModel（应用配置）、SaveModel（应用存档操作）、ProfileModel（档案资产/进度）、SkillModel（独立学习记录观察契约）、LevelModel（本局流程）。不为了减少数量合并存档 DTO、运行时模型和节点，也不制造 GameModel 包一切。普通小数据可以内嵌或随主要模型放同文件；仅转发其他 Model 的外壳应删除。

本次顺序为：核对公开项目实际代码 → 确认数据/规则所有权 → 更新集中规范 → 完整迁移领域代码、注册与测试 → 引擎扫描及回归 → 更新导览与验证。以后新增功能同样先确定数据所有者、写规则、生命周期和表现节点，再决定是否拆 Model/System；以调用链和行为验证决定边界，不以类数或文件行数决定。

主游戏仍只有主流程 FSM 的一个 Architecture。应用、档案、本局是注册成员的不同生命周期，不是三个相互取用的 Architecture；F6 等独立运行世界才使用单独 FSM。目录按业务功能分组，领域内把 Model、System 和配套类型放一起；不引入横向 Models/Systems 总目录，也不改变已有 UI/Entity 场景组织。
