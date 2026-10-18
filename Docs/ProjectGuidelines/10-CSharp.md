# 10 C# 与文档

## 命名

| 成员                         | 规则                                       |
| ---------------------------- | ------------------------------------------ |
| 类型、枚举、方法、属性、事件 | `PascalCase`；接口以 `I` 开头          |
| 字段（含`[Export]`）       | `m_` + `PascalCase`，如 `m_Hp`       |
| 参数、局部变量               | `camelCase`                              |
| 常量                         | `PascalCase`；优先用有名常量表达工程规则 |
| 命名空间                     | `GameLogic.<业务模块>` 或 `GameConfig`；模块映射见 [00-Architecture.md](00-Architecture.md) |

一文件一个主要类型，文件名与类型名一致。标识符使用英文；中文只用于配置文本和用户可见内容。

附属于同一功能的小枚举、结果与统计类型可以和主要类型放在同一文件，保留清晰的 XML 文档与命名空间。例如 `SaveSlotState` 随 `SaveSlotInfo`，`LevelOutcome` 随 `LevelModule.Progress`；按用户要求，`HeroSaveData` 随 `GameData`，同一文件能看清完整存档结构。多处独立使用、有独立职责的类型仍单独存放；不建立全局 `Enums.cs`，不按行数机械拆分或合并。大型节点可按职责使用 `类型.职责.cs` partial，字段和生命周期仍由同一对象拥有。

流程状态机的数据键统一放在 `MainPack/Scripts/Startup/FlowData/FlowKeys.cs`，池化包装放在同目录。领域对象不携带流程键；其他模块的常量仍归所属功能，禁止建立所有常量混杂的全局桶。

### 动画方法入口

原生方法轨道调用的方法统一用 `AnimEvent` 后缀，如 `PlaySoundAnimEvent`、`BeginFlameThrustAnimEvent`；普通信号/事件处理仍用 `On...`，状态内的招式方法使用正常动词，不混用两套命名。入口较多时放在 `类型.AnimationEvents.cs` partial；不为单个声音键新建文件、接口或动作专属包装。重命名时同步原生方法键与导入/校验工具，不保留旧名转发。

### 类型命名后缀

按入口、节点、状态和规则区分，不用泛化的 Manager/System/Model 代替实际职责；GGF 与生成器契约保留。

| 职责 | 命名 | 示例 |
| --- | --- | --- |
| 业务模块唯一入口 | `XxxModule`，可以为普通对象或场景根节点 | `SaveModule`、`LevelModule : Node2D` |
| 模块内部 Godot 节点 | `XxxNode` | `LevelExitNode`、`LevelCameraNode`、`HurtBoxNode`、`MusouAfterimageNode` |
| 实体与窗口/控件 | 保留能说明节点职责的 Entity/Form/Hud/Bar/Button 后缀 | `GorillaEntity`、`SettingsForm`、`EncounterHealthBar` |
| Godot 资源 | `XxxResource` | `LevelMarkerResource` |
| 纯方法或无状态扩展辅助 | `XxxHelper`，不保存会话、计时器、订阅或可变静态字段 | `DamageHelper`、`ProfileSaveHelper`、`BodyFsmHelper`、`LevelCameraHelper`；`SoundHelper` 只适配 GGF 调用 |
| 运行时状态/有状态规则 | 领域名词，不滥用 Helper | `PlayerProfile`、`HeroProgression`、`Vitals`、`ExperienceCurve`、`LevelStageSequence`、`LevelSpawnSchedule` |
| 不可变输入/输出 | 能表达方向的名词 | `HeroLoadout`、`CombatantStats`、`DamageResult`、`MonsterSpawn` |
| 存档 DTO / 配置 | `XxxSaveData` / 生成的 `XxxConfig` | `HeroSaveData`、`MonsterConfig`；框架 GameData 保留 |
| 界面参数 / 表现协调对象 | `<界面>Data` / `XxxPresenter` | `BattleHudData`、`DamagePopPresenter` |

只有常量的 InputActions、HeroAnims、MonsterAnims 保留集合名称。Luban 外部构造契约 `ExternalTypeUtil`、生成的 ConfigSystem、GameData/GameCatalogue、全局流程名与 Ge 窗口类名不机械改名。

模块目录按功能分类，所属节点、纯规则、资源和 `Helper` 放在同一功能目录，通过类型后缀辨别职责。例如 `Level/Camera/` 同时放 `LevelCameraNode` 与 `LevelCameraHelper`，`Level/Spawning/` 放刷怪节点、生成点与调度规则。跨功能共享的空间条目放 `Level/Markers/`。小模块不机械拆空目录；模块入口的 partial 留在根目录。

## XML 文档与实现注释

- 手写 C# 的类型、方法（含私有方法）、构造函数、重写方法、Godot 生命周期回调以及公开/受保护成员都写 XML 文档注释。
- `<summary>` 用一句话说明职责。方法一律补齐影响调用方式的 `<param>`、`<returns>`、`<exception>`，单位写进对应说明。
- XML 文档写调用契约：作用、单位、生命周期、所有权或限制；不要复述方法体，也不要写空泛注释。
- 私有字段和私有常量在声明行尾使用 `//` 简要说明职责、状态含义或重要约束；多行声明把注释放在最后一行。
- 方法内部按逻辑块使用 `//` 注释。验证/索引建立、资源或实体创建、异步等待、状态迁移、清理以及非直观循环等边界应各有一条短注释；注释解释意图、原因或不变量，不逐行翻译代码。
- 简单的属性转发、单步计算和显然的 `if` 分支不强制块注释；局部变量不单独写文档。
- 不以 XML 和行尾 `//` 重复描述同一职责；行尾 `//` 只用于字段与常量。
- 生成代码不补注释；改对应源表、模板或手写逻辑。

```csharp
/// <summary>返回当前生命值。</summary>
public int Hp { get; private set; }

/// <summary>对目标应用一次攻击结算。</summary>
/// <param name="target">受击实体。</param>
/// <returns>最终伤害值。</returns>
public int ApplyHit(ActorEntity target)
{
    // 先校验目标，再按确定性的战斗公式结算。
    // ...
}

private readonly Dictionary<int, IEntity> m_ActiveMonsters = new(); // 当前关卡创建且尚未死亡的实体。

/// <summary>按配方顺序生成阶段内容并维护并存上限。</summary>
/// <param name="stage">要生成内容的阶段配置。</param>
/// <returns>生成完成的异步任务。</returns>
private async Task StartStageAsync(LevelStageConfig stage)
{
    // 先按配置顺序展开配方。
    // 生成期间等待并存名额，避免一次性创建过多实体。
    // 生成完成后标记阶段，允许清除逻辑推进后续阶段。
}
```

## 类型与运行时约定

- 生命、攻防等整数属性用 `int`；倍率、概率用 `float`；避免隐式截断。
- 日志通过 `Log.Debug/Info/Warning/Error/Fatal`；玩法代码不直接写控制台或 `GD.Print`。烟测协议输出例外。
- 使用框架池化接口的对象按所有权及时归还；同一实例不得重复归还。
- `Battle/` 的公式保持确定性；随机值由调用方传入。详细边界见 [30-EntitiesAndCombat.md](30-EntitiesAndCombat.md)。

## 初始化、状态与职责

- 外部数据、场景装配、稳定外键在初始化边界由对应职责所有者校验一次；运行期消费已经验证的索引，不重复查表、不静默换默认值。校验失败须让会话拥有者停止并报告。
- 用互斥状态表达会话和阶段迁移；禁止把 `dependency != null` 包装成 `IsPrepared` 等隐式状态。加载期不订阅玩法事件；需要区域监听时显式启用，在停止时显式解绑。
- 模块入口只协调所属业务的生命周期和状态迁移；空间索引归生成点目录，区域与门归阶段门集合，配方时间与名额归调度，实体所有权归刷怪服务，实际视野归相机。
- 只有实际消费者需要的数据才保留字段。一个当前阶段不需要按阶段堆叠 Started/Finished/Cleared 集合；能从同一权威状态推导的事实不再维护副本。
- 状态对象只暴露 getter 与修改方法；不公开可写字段或可写的可观察容器。修改方法维护不变量（钳制、死亡、升级）并在全部字段更新后发一次 `Changed`。
- 异步操作在等待前捕获所属会话的服务、取消令牌和所有权；等待后不读取可能已被清理或换成新会话的字段。无法取消的框架显示结果在旧令牌取消后到达，仍须由原服务隐藏。
- 可由游戏帧推进的玩法计时不创建轮询计时器或后台任务；暂停时计时停止。异步资源显示保留真实异步边界，并让失败回到会话拥有者。

## 已采用的业务代码风格（2026-10-11）

用户确认入口界面与事件整理版本作为后续标准，新增与修改业务代码按以下规则执行：

- 先明确状态源、作用域所有者、纯规则与表现的职责；窗口只观察状态并提交具名请求，流程拥有导航和异步任务，不为相邻层增加纯转发对象。
- UI 按 Ge 生成绑定、`.Logic.cs` 业务、必要时的 `Data` 参数分工。需要打开参数的 Form 使用自己的 XxxFormData，即使只有一个英雄引用也保留，作为该窗口的参数契约；Data 只持有必要的引用或值，不复制共享 Model。不需要外部参数的窗口不建空 Data。状态源在 `OnOpen` 订阅并立即刷新，`OnClose` 释放订阅和回调，`OnRecycle` 再清理编号。
- 按钮使用具名处理方法，只在新建实例时订阅一次。跨模块点击使用带窗口编号的池化 GGF 事件；流程在分发期间复制所需值，拒绝旧周期事件。可取消等待在进入时捕获本周期令牌和服务。
- 共用代码须有实际共用职责：`MenuProcedure` 统一窗口、音乐、取消和返回标题的保存等待；不要用泛型菜单命令和轮询代替具名事件。只调用基类的空重写删除；GGF 接口要求的生命周期入口保留。
- 无状态、附属于现有类型的操作可使用 C# 14 `extension` 块，例如 `BodyFsmHelper` 提供的 `fsm.Tick(dt)`。状态机的物理帧调度仍独立于单个状态的决策，不为合并文件把调度塞进 `BodyState`。
- 不为测试新增生产 getter、状态名字符串、观测事件或测试路径。测试使用现有状态对象、`IFsm.CurrentState`、节点和必要的反射。只有测试使用且可从权威状态直接读取的冗余 API 删除。
- 独立的主要类型各占一文件，配套小类型按本章开头的归属规则组织；公开契约写 XML，字段用简短行尾说明；不用生成模板的空区域和重复注释掩盖业务职责。修改旧文件时对齐本规则，不机械改动生成代码和框架。
