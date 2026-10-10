# 小怪血条与状态事件实现说明

本说明记录当前实现，不替代 `ProjectGuidelines/` 项目规范。

## Changed 的用途

`Changed` 是状态源发给外部观察者的同步聚合通知。观察者收到后读取 getter，取得更新完成后的全部所需值。它不表示一次受击、获得金币或交易成功。

| 状态源 | 当前业务订阅者 | 读取内容 |
| --- | --- | --- |
| `Vitals.Changed` | `BattleHud`、绑定了小血条的 `MonsterEntity` | 当前生命/魔法及上限 |
| `MusouGauge.Changed` | `BattleHud` | 当前无双、上限与满值状态 |
| `HeroProgression.Changed` | `BattleHud` | 等级、本级经验及上限 |
| `Wallet.Changed` | 暂无 | 后续货币显示可读取 `Gold` |
| `StatSheet.Changed` | 暂无 | 后续属性显示等可读取最终属性 |

当前消费者不需要旧值、变化原因或差值，因此保留 `event Action Changed`，遵循第 00 章「状态变化与通信」。将来某项业务确实需要操作结果时，再按作用域选择直接返回结果、专用 C# 事件或 `GF.Event`；不把这些信息塞入通用刷新通知。

本轮补充了事件 XML 契约，并修复 `Wallet.AddGold` 在余额已达 `int.MaxValue`、实际余额未变化时仍发送通知的问题。`Vitals.SetMaximums`、`MusouGauge.Reset` 保留显式同步/重置通知的现有语义。

## 旧项目参考与新项目落位

已核对旧项目 `Script/Base/BaseMonster.gd` 的受击创建与 `little_blood_bar_control`，以及 `Global.gd:add_monster_blood`、`Scene/MonsterBlood/monster_blood_bar.tscn/.gd`。旧项目受击时动态实例化子场景，由怪物每物理帧写入比例，并在血条脚本中尝试计时淡出。

另已核对 `Script/MemoryClass/main_set.gd`、`Script/Level/Role_information.gd:set_value_Hp`、`Script/Monster/BossBlood.gd:on_HpChange` 与小血条的 `on_Hpchange`：旧项目有可选残影代码，但 `HpBarDelay` 默认 `false`，关闭时三类血条都将残影值清零。此证据仅描述本地旧项目。

按用户最新要求，新项目采用即时更新的头顶红色小条，不默认加入旧项目关闭的残影表现：

- 共享场景：`TheGame/UIs/Widgets/monster_health_bar.tscn`，根节点使用已有被动控件 `ResourceBar`。
- 当前接入：`HuaguoshanMonkeyEntity.tscn`、`DemonMonkeyEntity.tscn`，通过导出的 `m_HealthBar` 绑定直接子节点。
- `MonsterEntity.OnShow` 在配置和生命重置完成后订阅 `Vitals.Changed`，并立即读取初值；`OnHide` 对称退订并隐藏。
- 血条作为怪物子场景随 GGF 实体池整体复用，没有独立生成/销毁、独立池、全局管理器或每帧生命轮询。
- 位置使用 `HeadPosition`（受击盒顶边中心），物理移动后更新位置；`m_HealthBarOffset` 在检查器调节。血条位于身体镜像容器外，转向不反转填充方向。
- 采用「存活且未满血时显示」：首次掉血显示；部分治疗刷新；补满、生命归零或实体隐藏时隐藏。没有接入旧项目的显示设置与超时淡出。
- 控件只读输入，不参与扣血、治疗、死亡或战斗结算。归零显示判断使用 `Vitals.IsDepleted`，因为其通知早于 `ReceiveHit` 更新 `Dead`。

这是世界空间控件，不是独立玩法窗口，因此不新增 `UIFormId` 或 UI 配置行。场景留在现有 UIs bundle；Collection Res 通过既有生成入口更新，未手改生成代码。

给新小怪接入时，将共享场景拖到实体根下，并绑定 `m_HealthBar`；修改共享场景可统一调整颜色和尺寸。未绑定血条的怪物维持原有行为，Boss 界面另按实际需求设计。

## ResourceBar 的职责整理

- 删除残影节点引用、补间、`immediate` 参数和无实际任务可清理的 `ResetPresentation`；英雄及小怪场景中的残影节点同时移除。
- `ResourceBar` 仅保留即时 `SetValue(current, maximum)`、可选文字和经验满级的零上限显示配置。
- 原 `m_FullAnimation` 实际用于无双蓄满提示，并非废弃字段。该引用及动画控制已移至 `BattleHud`，窗口关闭、改绑、消费或零上限时停止复位，满值时持续循环。
- `BattleHud.cs` 由 `regenerate_ui_form.py --namespace GameLogic.UI` 从根节点绑定重生成，新增 `m_WsMax`，未手改生成字段。

第 40 章仍记录此前的残影行为与三参数 `SetValue`；本轮用户明确要求优先，实施移除。规范正文依入口要求保持未改，此处报告该描述与当前实现的差异。

## 血条接入阶段的验证与边界

- `dotnet build --no-restore`：通过，0 错误；存在框架既有的过时 API 警告。
- `dotnet test Tests/ProfileTests --no-restore`：24/24，通过，包括钱包通知时机与封顶无变化的用例。
- Godot `--headless --build-solutions --quit --no-window -q`：构建退出码 0；编辑器插件退出时仍出现第 50 章已登记的 RefCounted 释放异常。
- `EditorScripts/regenerate_collection.gd`：101 项资源生成完成，包括新增血条场景；该文件还包含此前未登记到常量的成长表、档案表与无双 shader，均由生成器扫描现有资源得到。
- `EditorScripts/validate_anim_libraries.gd`：现有校验器覆盖的悟空与花果山猴子通过；此校验器未覆盖妖猴动画库。本轮未修改动画。
- `-- --smoketest=ui`：输出 `SMOKE PASS`，覆盖 HUD 即时刷新，两种小怪的首次/连续受伤、部分/完整治疗、位移转向、真实致命受击、隐藏退订与实体/血条池复用，以及无双满值循环、消费/关闭/改绑/零上限复位。
- 使用真实 OpenGL 渲染器检查英雄 HUD 与两种怪物的即时血条，检查图在忽略目录 `.godot/validation/monster_health_preview.png`。
- `python Tools/ProjectMaintenance/validate_game_references.py`、`git diff --check`：通过。

该阶段烟测退出仍出现第 50 章已登记的 `DefaultWebRequestAgentHelper.Reset` 访问已释放节点异常；与业务断言通过分别记录。该阶段未修改配置源表。后续官方尺寸核对、关卡配表、角色无双与字体迁移见 [OfficialBattlePolish.md](OfficialBattlePolish.md)。框架、插件与旧项目保持只读，原有暂存的 `Docs/ProjectTour.md` 修改保持原样。
