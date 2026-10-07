# 经验成长与通用资源条设计

## 调研与适用性（实现前核查）

- [Flare ARPG Avatar.cpp](https://github.com/flareteam/flare-engine/blob/1969baeb89ebf94cf22e2bc2ed4e3d8413714d4f/src/Avatar.cpp)：累计 XP 与下一等级阈值比较，通过 `getLevelFromXP` 得到最终等级，再重算属性。适合当前已有的 `TotalExperience` 存档，能保留余量并处理一次跨多级；不移植其引擎、复活或全局管理器。
- [GDQuest Open RPG 生命条](https://github.com/gdquest-demos/godot-open-rpg/blob/19bd328fae9e4b534d3bb6db380a3d871d6ea58f/src/combat/ui/battler_entry/ui_life_bar.gd) 与同版本 `battler_stats.gd`：数值变更通知驱动显示，刷新前停止旧 Tween。只借鉴表现与订阅边界；当前属性容器继续只通知，钳制和升级仍由实体业务执行。
- 本地旧项目 `BaseRoleProperies.gd:440–478,511–514,847–856`：1–19 级采用现有列表，20 级起 `5000 + 5000 × (Level − 19)`；升级重算属性并补满 HP/MP。`BaseMonster.gd:1259` 限制等级低于 55 才发经验；`Monster_1.gd:26` 为 1，`Monster_2.gd:28` 为 5。现有两条怪物数据已经对应这些数值，不虚构其他怪物行。
- GGF `ArchiveSystem.cs`、事件及 UI 文档与实际代码已核对：死亡参数池化，不能跨等待保存；窗口关闭必须显式退订；存档覆盖 API 不返回磁盘写入结果。

## 决定与契约

1. 数值仍由 Luban 源表提供，最高等级由等级表末行表达，末行 `MaxExp=0` 明确终止成长。不可变 `ExperienceCurve` 在英雄初始化时验证连续等级和正阈值，预计算累计门槛，不持有英雄运行状态。
2. 英雄持有累计经验与等级；`GainExperience` 统一处理非负奖励、满级截断、连升、成长 HP/MP 和升级补满。给 HUD 的本级经验与上限是派生显示值，不持久化。普通属性赋值仍不自动升级。满级经验显示 `MAX`。
3. 旧 `PlayerSaveData.Level/TotalExperience/Gold` JSON 名称保持兼容。历史最小存档允许等级与累计经验不一致：读档至少保留已有等级，将累计经验提升到该等级最低门槛，再由总经验解析最终等级；低于零和超过满级的数值归一化。不得把已经声明为累计经验的字段当作本级余量再重复加门槛。
4. 刷怪服务拥有死亡去重边界：只在移除本会话存活 ID 成功时发放已验证的经验值，再判定清波。控制器将这个普通值交给注入的英雄；不新增全局奖励服务，不在 HUD 结算，不重新消费池化死亡参数，不因尸体隐藏而奖励。单人关卡收益归本会话玩家，与最后攻击来源解耦。
5. 经验变更完成后由流程排队保存普通快照，保存串行执行；离开仍在回收前捕获最终快照，重入等待旧写档。关停不临时启动新写档。实际成功由隔离目录的磁盘读回验证。
6. `ResourceBar` 统一绑定两个属性实例。文本、损失残影、段位 Sprite 都是场景可选项；HP 配残影，MP/EXP 配文本，WS 配段位。纹理、方向和尺寸继续由场景负责。初次绑定及上限变动立即对齐；只有数值下降才播放残影。
7. 手写代码按字段、属性、生命周期、业务入口、内部方法/接口实现排列，删除无用 using 和可由 using 表达的类型全名前缀。保留稳定领域命名空间，生成样板通过源场景与原模板生成，不直接手改。

## 与当前规范状态的差异

遵循集中规范 00 的实体数值/属性通知/UI 数据流、10 的命名文档、20 的数值源与生成流程、30 的生命周期、40 的资源绑定、50 的验证隔离和只读边界。00/20/40/60 中“成长待设计”“没有运行时消费者”“MP/经验隐藏”是本任务将改变的现状登记，不自行修改规范。旧项目升级清零会丢失余量，本实现明确采用累计经验成长。

## 验证

| 命令 | 实际结果 |
| --- | --- |
| `$env:AI_MODE='1'; Configs/GameConfig/gen_code_bin_to_project_lazyload.bat` | Luban 校验与导出成功，等级表末行与对应 `.bytes` / 生成类注释同步。 |
| `python -X utf8 Tools/ProjectMaintenance/regenerate_ui_form.py Godot/GodotProject/TheGame/UIs/BattleHud.tscn` | 由场景根节点的 6 个显式绑定与只读 GGF 模板生成 Ge 样板，业务半类未被覆盖。类型导入也由场景脚本推导。 |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore --verbosity quiet` | 通过，0 错误；完整编译有 5 项既有框架弃用警告。 |
| `dotnet test Tests/BattleTests --no-restore --verbosity quiet` | 89/89 通过，包含 10 项新经验断言用例；NuGet 漏洞数据源有既有 NU1900 网络警告。 |
| `dotnet test Tests/LevelTests --no-restore --verbosity quiet` | 16/16 通过。 |
| `python -X utf8 Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry` | 91 项资源路径、133 项业务脚本 UID 通过；23 个关卡地形节点与任务开始时的暂存版本一致。 |
| `git diff --check` | 通过，Git 提示既有 CRLF/LF 转换策略。 |

引擎为 `S:/Godot4/Godot4CSharp_console.exe`，下列参数均使用该程序：

| 参数 | 实际结果 |
| --- | --- |
| `--headless --editor --import --path Godot/GodotProject --quit --disable-crash-handler` | 两张新纹理导入完成并生成 `.import`；退出码 0，编辑器退出仍有插件释放错误。 |
| `--headless --editor --build-solutions --path Godot/GodotProject --quit --no-window -q --disable-crash-handler` | 最终编辑器构建完成，退出码 0；退出仍有插件释放错误。 |
| `--headless --editor --path Godot/GodotProject --script res://EditorScripts/regenerate_collection.gd --disable-crash-handler` | 调用原插件的 Collection Res，生成 97 项资源常量；插件代码未修改。 |
| `--headless --path Godot/GodotProject --max-fps 120 --quit-after 2400 --disable-crash-handler -- --smoketest` | 英雄动作/动画/命中 `SMOKE PASS`，退出码 0。 |
| 上述运行参数，`--quit-after 4800 -- --smoketest=ai` | 怪物 AI `SMOKE PASS`，退出码 0。 |
| 上述运行参数，`--quit-after 18000 -- --smoketest=level` | 四段刷怪与真实战斗/相机/物理门 `SMOKE PASS`，经验总量为 15，与源表一致；重复死亡事件没有增加奖励。退出码 0。 |
| 上述运行参数，`--quit-after 2400 -- --smoketest=level-cancel` | 在途生成取消与晚到结果回收 `SMOKE PASS`，退出码 `0xC0000005`。 |
| 上述运行参数，`--quit-after 2400 -- --smoketest=ui` | 四类条的纹理/可见性、无双段位帧、跨级余量、55 级封顶、升级 HP/MP 成长补满、窗口/实体池复用、关闭退订、失败恢复、自动保存的实际磁盘读回及流程重入全部 `SMOKE PASS`；退出码 `0xC0000005`。 |

所有烟测存档位于各次生成的 `user://Validation/<模式>/<随机 ID>`。最终 UI 回归目录为 `Validation/Ui/ecd91319543543fb9c9c60aad03dd988`；没有读写玩家实际存档。UI 故障恢复用例会刻意置空必需节点并产生打开失败日志，这是已断言的测试输入；最终运行没有新增纹理加载或场景解析错误。

## 边界与未完成项

- 本轮修改只限业务代码、源表、业务场景、验证工具与记录；新增两张 PNG 的来源见 `LegacyAssetMap.md`。框架、插件、`Tools/Luban/` 和旧项目保持只读。
- 生成产物通过源表/Luban、场景/GGF 原模板、原 Collection Res 生成器更新，未直接编辑生成类或二进制。`ResourceBar` 携带原 `HealthBar` 的 UID，场景引用同步更新。
- 初次编辑器构建因沙箱无法写用户构建日志失败；授权范围内的验证提权后解决。一次中间构建在成员排序期间失败并因插件退出问题挂起，已核对 PID/父进程/命令行后仅结束该验证进程；最终构建通过，用户已有编辑器未关闭。
- 编辑器退出错误仍位于只读 `addons/ComponentInsoector/ComponentInsoector.cs`，其对 RefCounted 调用 `Free`。五种运行烟测在断言结束后都观察到既有 `Framework/GodotGameFrameworkCore/WebRequest/DefaultWebRequestAgentHelper.cs:90` 访问已释放对象；UI 与取消模式退出为原生异常。功能断言通过不代表退出阶段没有错误，本轮未修补依赖。
- MP 本轮接入基础数值、成长及显示；技能耗蓝、回复、装备修正仍随其对应模块设计。存档格式沿用现有 JSON，未引入装备/关卡进度等完整存档内容。
- 请求中的经验闭环、通用条和相关代码整理已完成；既有依赖退出缺陷仍未修复。集中规范的现状登记差异见前文，规范正文未擅改。
