# 10 C# 与文档

## 命名

| 成员                         | 规则                                       |
| ---------------------------- | ------------------------------------------ |
| 类型、枚举、方法、属性、事件 | `PascalCase`；接口以 `I` 开头          |
| 字段（含`[Export]`）       | `m_` + `PascalCase`，如 `m_Hp`       |
| 参数、局部变量               | `camelCase`                              |
| 常量                         | `PascalCase`；优先用有名常量表达工程规则 |
| 命名空间                     | `GameLogic.<目录路径>`（与 `GameScripts/` 下目录一致）或 `GameConfig`；例外见 [00-Architecture.md](00-Architecture.md) |

一文件一个主要类型，文件名与类型名一致。标识符使用英文；中文只用于配置文本和用户可见内容。

### 类型命名后缀

按职责命名，不用含义模糊的 `Manager`、`System`、`Model`、`Helper` 后缀（GGF 自身类型除外）：

| 职责 | 命名 | 示例 |
| --- | --- | --- |
| Luban 生成配置 | `XxxConfig`（生成） | `HeroConfig`、`MonsterConfig` |
| 存档 DTO | `XxxSaveData` | `ProfileSaveData`、`HeroSaveData` |
| 运行时状态（有唯一所有者） | 领域名词 | `PlayerProfile`、`Wallet`、`HeroProgression`、`StatSheet`、`Vitals` |
| 纯规则 / 无状态服务 | 职责名 | `DamageCalculator`、`ExperienceCurve`、`HeroStatBuilder`、`SaveMigrator` |
| 作用域所有者 | 作用域名 | `GameContext`、`LevelRun` |
| 不可变输入快照 | 名词 | `HeroLoadout`、`CombatantStats`、`MonsterDefeat` |
| 节点 | `XxxEntity`、`XxxController`、窗口/控件名 | `WukongEntity`、`LevelController`、`ResourceBar` |
| 界面打开参数 | `<界面>Data` | `BattleHudData` |
| 表现服务（普通对象） | `XxxPresenter` | `DamagePopPresenter` |

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
- 控制器只协调生命周期和领域事件；空间索引归生成点目录，区域与门归阶段门集合，配方时间与名额归调度，实体所有权归刷怪服务，实际视野归相机。
- 只有实际消费者需要的数据才保留字段。一个当前阶段不需要按阶段堆叠 Started/Finished/Cleared 集合；能从同一权威状态推导的事实不再维护副本。
- 状态对象只暴露 getter 与修改方法；不公开可写字段或可写的可观察容器。修改方法维护不变量（钳制、死亡、升级）并在全部字段更新后发一次 `Changed`。
- 异步操作在等待前捕获所属会话的服务、取消令牌和所有权；等待后不读取可能已被清理或换成新会话的字段。无法取消的框架显示结果在旧令牌取消后到达，仍须由原服务隐藏。
- 可由游戏帧推进的玩法计时不创建轮询计时器或后台任务；暂停时计时停止。异步资源显示保留真实异步边界，并让失败回到会话拥有者。
