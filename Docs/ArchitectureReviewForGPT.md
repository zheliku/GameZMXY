# QF/GGF 融合架构独立审查意见（供 GPT 对齐使用）

> 本文是 2026-10-31 外部调研后的一份**个人审查意见**（Kilo 出具），用途是交给外部 AI（GPT）作为架构对齐与代码生成纪律的依据。与 [ArchitectureReview](ArchitectureReview.md)、[ArchitectureResearch](ArchitectureResearch.md) 并行存在；三者证据相容，但意见冲突处以本文观点为准或交用户裁决。本文是意见与调研文档，不修改 [ProjectGuidelines](ProjectGuidelines/AGENTS.md) 规范正文；规范与用户当前指令仍拥有最终裁决权。

## 0. 结论摘要（我的判断）

1. **融合骨架方向正确，不需要推倒，也不需要再引入第三套框架。** "GGF 管生命周期/流程/实体/UI/资源/声音/事件 + QF 的 Model/System/Command/Query/Bindable 管业务组织 + Godot 场景/AnimationPlayer 做表现权威"这一分工，与 QF 旗舰开源案例 CrazyCar、UGF 官方示例 StarForce 及社区主流融合实践（"GF 筑基、QF 添翼"）方向一致。
2. **本项目的调用纪律比所有被调查的外部样本都严格，这是优势，应保留并守住。** Controller 只读 + 写必经 Command→System、System 持不变量、Query 只组合读取，均严于 QF 官方规则（官方允许 Command 直写 Model、允许嵌套 Command）。
3. **"补丁感"的真实来源不在框架选型，而在四个工程习惯：** ①装配清单分散不可读；②读契约靠 review 纪律而非类型约束；③调用入口判定不成文；④修复倾向加层而不是删层。对策见第 4~6 节。
4. **给 GPT 的核心纪律只有一道门：写代码前先回答四问**——数据所有者是谁、写入口是谁、生命周期挂哪个作用域、表现边界在哪。回答不了就不该新建类型。

## 1. 本轮独立核对的外部证据

| 来源 | 我实际读到的内容 | 关键结论 |
| --- | --- | --- |
| [QFramework 官方 README / QFramework.cs](https://github.com/liangxiegame/QFramework) | 四层能力矩阵、Command/Query 契约源码 | Controller 可直读 Model；Command 官方可直写 Model、可嵌套发送——规则是"理想态"，QF 自己不强制 |
| [QF 官方教程第 13 章](https://qf.readthedocs.io/zh-cn/latest/QFramework_v1.0_Guide/02_architecture/13_architecture_pros.html) | 凉鞋自研 IndieGame 的注册清单 | 15 System / 5 Model / 1 Utility；**"可以把 Architecture 本身当做一个架构图"**——注册清单集中可读是 QF 的核心卖点 |
| [CrazyCar](https://github.com/TastSong/CrazyCar)（QF 官方唯一列出的群友开源案例） | `CrazyCar.cs` 全文、Scripts 目录树 | 15 System / 10 Model / 1 Utility；**全部按接口注册**（`RegisterModel<IUserModel>(new UserModel())`）；顶层 Command/Event/Game/Model/Query/System/UI/Utility 目录；基础设施（WebSocket/KCP/Addressables）都做成 QF System |
| [StarForce](https://github.com/EllanJiang/StarForce)（UGF 官方示例） | GameMain/Scripts 目录树 | Procedure/Entity/UI/DataTable/Definition/BuiltinData/Scene/Sound 分目录；业务规则直接写在 Procedure/Form，**无 Model/System 层**——UGF 只管基础设施，业务组织留给项目自建 |
| 社区融合文章（CSDN，谨慎采信） | "用 GameFramework 筑基，用 QFramework 添翼" | GF 管 Resource/Network/Procedure/热更，QF 管业务状态/UI/事件，与本项目分工相同（该文细节疑有 AI 生成成分，只取其方向共识） |
| 既有研究（ArchitectureReview/Research 已固定提交核对） | UnityRpgGame、arlozen/MMORPG、FM-Better/Survivor、Foundation | System 多于 Model 是常态（15/10、15/5、2/5、4/0）；Model 按领域聚合，不与 System 一一配对；Survivor 的静态全局 Bindable 与 MMORPG 的 System 持节点是**反例** |

## 2. 融合定位判断

- QF.cs 核心是纯 C#、全局静态单 Architecture、无作用域概念。本项目 `GameScripts/Arch/FsmArchitectureExtensions.cs` 把注册存进 GGF 主流程 FSM 的数据区、随流程销毁、拒绝池化复用旧视图，**在"应用/档案/本局"三层生命周期隔离上强于原生 QF**，这段自研不应替换成 vendor QF.cs。
- GGF 已提供 Resource/Sound/Setting/Archive/Entity/UI/Event，因此 CrazyCar 式的"基础设施当 QF System"在本项目属于重复建设，**不采纳是正确的**。
- StarForce 证明 UGF 系框架本身不提供业务层——本项目的 QF 层正是 UGF 家族框架缺失的那块拼图，融合思路成立。

## 3. 对当前实现的审查意见

### 3.1 值得肯定（比外部样本更严格，保留）

| 维度 | 外部样本实际行为 | 本项目 |
| --- | --- | --- |
| 写入口 | CrazyCar `HomepageUI` 直写 `IsCompleteGuidance`、`BuyEquipCommand` 直写 `Star` | 五类业务写必经 Command→System，System 重新校验 |
| 生命周期 | 应用期一次注册，节点销毁兜底注销 | 三作用域注册/移除，关窗先于模型移除，OnInit 失败对称清理 |
| 状态所有权 | Survivor 静态 Global、MMORPG System 持实体视图 | 六个顶层 Model 按所有权/生命周期划分；Model 与 System 均不持 Node |
| 事件分层 | QF 全局事件总线 | Bindable 观察状态 / 具名回调管场景协作 / GGF Event 管跨模块一次性事实，三层分工 |

### 3.2 差距（"补丁感"的来源，我的四条意见）

1. **装配清单分散且与异步编排交错，"架构图"优势丢失。** 注册散布在 `ProcedurePrepare.cs:66-77`（应用 9 项）、`ProcedureLoadGame.cs:67-73`（档案 5 项）、`ProcedureLevel.cs:142-148`（本局 5 项）的 OnEnter 中段；`TestArenaController.cs:198-219` 需要手抄 22 行注册才能搭测试场，就是缺少集中装配清单的直接代价。CrazyCar 一个 40 行文件读完整个项目结构，本项目要翻三个 800 行流程文件。
2. **具体类型耦合，读纪律靠约定而非编译器。** 全部消费点是 `GetModel<HeroModel>()`/`GetSystem<ProfileSystem>()` 具体类型；"Controller 只读"靠 `internal` 写方法 + review 守护。CrazyCar 与 IndieGame 的规范是接口注册取用，消费方类型系统上只见契约。注意：这与既有结论"拒绝无第二实现的一接口一实现仪式"不矛盾——我的意见是**只为被 3 个以上窗口消费的 Model 提炼只读视图接口**（见 6.3），不是给所有 System 配接口。
3. **调用入口判定不成文。** 同类入口混用两种风格：技能学习走 `SendCommand`（`SkillForm.Logic.cs:250,272`），而 `MapForm.Logic.cs:392` 直接 `GetSystem<ProfileSystem>().CanEnterLevel`。前者合规、后者其实也合规（只读校验），但判定规则只存在于口头约定，GPT 每次都在猜。
4. **修复文化：加层 vs 删层。** GPT 式代码的通病是就地加转发层、兼容分支、第二份状态（历史上 HeroLoadout/EnemyAttackParams/HeroInputContext 均为此被删）。优秀项目的特征恰好相反：每个补丁出现时先找**可以删除的层**。

### 3.3 目录结构（我的观点：现状可保留）

顶层 `Models/ Systems/ Utilities/ Commands/ Queries/` + 紧密领域子目录（`Systems/Hero`、`Models/Save`），与 CrazyCar 的层优先组织同构；"单文件领域扁平、多文件紧密领域才建目录"的既有规则自洽。唯一建议：`Commands/`、`Queries/` 继续遵循 `EquipmentCommands.cs` 的一领域一文件先例，防止再次横向膨胀。

## 4. 给 GPT 的硬规则（生成代码前必须对齐）

### 4.1 第一道门：四问

新建任何类型/字段/事件前，先回答并写进提交说明：

1. 这个状态的**所有者**是哪个 Model？（已有 Model 内嵌 or 新 Model？）
2. 它的**写入口**是哪个 System？（哪个 Command 调它？）
3. 它的**生命周期**挂哪个作用域？（应用/档案/本局，谁注册谁移除？）
4. 它的**表现边界**在哪？（哪个 Entity/Form/动画轨道读它？）

回答不了任何一问，就不该新建；答案是"转发某个现有类型"就不该存在。

### 4.2 分层职责表

| 角色 | 拥有 | 禁止 |
| --- | --- | --- |
| Procedure | 作用域装配、导航、异步编排、逆序清理 | 业务计算、直改绑定值 |
| Model | 权威状态 + Bindable + 自身数据查询 | 持 Node、校验交易、推进时间、引用其他 Model/System |
| System | 业务规则与不变量、共享事实的唯一写入 | 持 Node、承担动画状态、代替 Procedure 导航 |
| Command | 一次业务写请求的参数；调用 System | 存跨请求状态、**直接写 Model**（严于 QF 官方）、嵌套 SendCommand |
| Query | 一次组合读取 | 修改状态、订阅变化、缓存跨帧结果 |
| Controller/Form | 订阅刷新、发送请求、窗口局部状态 | 业务公式、共享数据副本 |
| Entity/State | 节点、物理、动画、输入消费 | 保存档案数据、跨实体交易 |
| Utility | 纯规则或平台适配（显式参数） | 隐藏全局状态、持 Model/System、万能 Helper |

### 4.3 调用入口判定表

| 需求 | 入口 |
| --- | --- |
| 简单字段读取（HP、灵魂、页码） | `GetModel` 直读 |
| 只读业务校验（`CanEnterLevel` 类） | `GetSystem` 只读方法 |
| 组合/筛选/汇总读取（整包出售件数与总价） | `SendQuery` |
| 玩家发起的共享业务修改（穿戴、出售、学习） | `SendCommand` → System 校验 → 写 Model |
| 分页、悬停、tooltip、关窗 | Form 直接处理 |
| 切场景、返回标题、重试 | 具名导航请求提交给 Procedure |
| 跨模块一次性事实（伤害、怪物死亡、保存成功） | GGF Event（ReferencePool 参数） |
| 持续状态观察 | Bindable `RegisterWithInitValue` / `UnRegister` |

### 4.4 装配与生命周期规则

- 新 Model/System/Utility **只能在对应作用域流程注册**：应用=`ProcedurePrepare`，档案=`ProcedureLoadGame`，本局=`ProcedureLevel`。禁止在 Entity、Form、System 内注册或移除成员。
- 依赖先于消费者注册；离开逆序：取消任务 → 关窗退订 → 停场景 → 隐藏实体 → 逆序移除 System/Model。
- 订阅/退订成对出现在同一生命周期边界（OnOpen/OnClose、Init/Deinit）；不用节点销毁兜底注销。
- 关联字段一次完整赋值给 Value，不逐字段部分更新。

### 4.5 补丁识别清单（出现任一信号即重写）

1. 新类型只转发另一类型的能力、无独立不变量。
2. 同一事实的第二份存储（副本 Model、镜像缓存、兼容旧字段）。
3. 在调用点加判空/防御分支，而不修所有权或生命周期。
4. 为旧行为保留兼容分支，而不是删除完整旧链路。
5. 只有单一消费者的"通用机制"或"未来扩展点"。
6. 用手动逐帧同步代替绑定/事件。
7. `if (IsXxxMode)` 散布分派，代替把规则归属到所有者（角色特例留在角色实体/状态）。
8. UI 内联网络请求、数据解析或业务公式。

修复流程：先假定方案有补丁嫌疑 → 找可以**删除**的层 → 四问过门 → 再写代码。

### 4.6 与 QF 官方的差异声明（GPT 不得"纠正"回官方形态）

- Command 不直写 Model、不嵌套发送（严于官方，项目取舍，有测试守护）。
- 不引入第二套 Architecture、静态全局 Bindable、UIKit/ResKit/EasyFSM（GGF 已覆盖对应职责）。
- 接口注册非强制；仅按 6.3 的条件选择性引入只读契约。
- 不为 QF 目录名对齐而搬迁 Entity/UI/Procedure 代码。

## 5. 代码模式对照

```csharp
// ✔ 正确：Form 只读 + 订阅 + 立即刷新
m_Model.Soul.RegisterWithInitValue(RefreshSoul);

// ✔ 正确：玩家业务修改经 Command
PurchaseResult result = this.SendCommand(new LearnSkillCommand(skill));

// ✔ 正确：组合读取经 Query
SaleSummary summary = this.SendQuery(new EquipmentSaleSummaryQuery(selectedIds));

// ✔ 正确：只读校验直调 System
bool canEnter = this.GetSystem<ProfileSystem>().CanEnterLevel(levelId);
```

```csharp
// ✘ 补丁：Controller 直写 Model（CrazyCar 的真实败笔，本项目禁止）
this.GetModel<ProfileModel>().Soul.Value -= cost;

// ✘ 补丁：判空兜底不修生命周期
if (m_Model != null && !m_Disposed) { Refresh(); }

// ✘ 补丁：第二份全局持有
static class GameCache { public static HeroModel Hero; }

// ✘ 补丁：转发壳
class HeroLoadout { public HeroModel Model => m_Model; }

// ✘ 补丁：UI 内联业务公式
int price = (int)(equip.Price * 0.5f * (1 + level * 0.03f));
```

## 6. 演进建议（供用户裁决，未获批准前 GPT 不得自行实施）

1. **P1 作用域装配清单（Compose）。** 每个作用域提取一个集中注册入口（如 `AppScope.Compose(fsm)` / `ProfileScope.Compose(fsm)` / `LevelScope.Compose(fsm)` 静态方法），流程 OnEnter 只调用清单，注册顺序与逆序清理集中可读；`TestArenaController` 的 22 行手抄注册改为复用同一清单。这是 CrazyCar.cs"架构图"优点的作用域化版本，不产生转发层，只挪装配代码的位置。
2. **P1 调用入口判定表成文。** 把 4.3 表升格进 ProjectGuidelines 第 00 章（需按规范维护流程提交），review 只对违反判据的调用纠正。
3. **P2 选择性只读契约。** 仅为被 3 个以上窗口消费的 Model（当前为 ProfileModel、HeroModel、SaveModel）提炼 `IXxxModelView` 只读接口，Model 实现，消费点逐步切换。收益是"Controller 只读"从纪律升级为编译约束；这不是 CrazyCar 式一接口一实现的仪式，而是有真实多消费者的边界。是否实施由用户定夺。
4. **P3 保持不动的部分。** 不 vendor QF.cs；不加 UIKit/ResKit/EasyFSM/自动注销框架；不追求 Model 与 System 数量对等（外部样本 15/10、15/5、2/5 均不对等）。

## 7. 证据可复核性

- QF 官方：<https://github.com/liangxiegame/QFramework>（README、QFramework.cs）
- QF 官方教程第 13 章（IndieGame 注册清单）：<https://qf.readthedocs.io/zh-cn/latest/QFramework_v1.0_Guide/02_architecture/13_architecture_pros.html>
- CrazyCar：<https://github.com/TastSong/CrazyCar>（`CrazyCarClient/Assets/Scripts/CrazyCar.cs`、README_Dev.md、ProjectAnalysis.md）
- StarForce：<https://github.com/EllanJiang/StarForce>（`Assets/GameMain/Scripts/`）
- 本项目对照代码：`GameScripts/Arch/ArchitectureContracts.cs`、`GameScripts/Arch/FsmArchitectureExtensions.cs`、`MainPack/Scripts/Procedure/{ProcedurePrepare,ProcedureLoadGame,ProcedureLevel}.cs`、`MainPack/Scripts/Debug/TestArenaController.cs`、`GameScripts/UI/*/…Logic.cs`
- 本地研究缓存：`Godot/GodotProject/.godot/validation/qf_domain_research_20261031/`、`qframework_source/`、`qf_binding_review/`

未完成项：本文件为纯意见交付，未改动任何代码与规范正文；未运行验证命令（不涉及）。第 6 节建议均需用户批准后另行实施。
