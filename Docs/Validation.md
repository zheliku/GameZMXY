# 验证

本页保留当前维护命令、最近覆盖各功能的有效记录及必要失败边界；逐轮尝试、被后续实现替代的结果和旧版“当前限制”已清理，历史可由Git追溯。下列带日期的结果是对应检查点的历史证据，不能自动视为对未来修改已重跑。当前功能与待办见[ProjectTour](ProjectTour.md)。

## 代码去冗余与架构精简（2026-11-11）

按用户新目标“优先复用、合并和删除，保持原功能”继续处理**全项目手写业务C#**。本轮基线为上一轮120份变更已由用户暂存后的工作树，HEAD仍为`c82524b`；不是用上轮块体展开前的源码作比较。用户索引由用户持有，执行者无Git写操作。范围仍为TheGame非生成业务与EditorScripts手写源码；受影响的Debug/测试调用一起迁移。基线/实际源码/同口径Roslyn统计及日志在`.godot/validation/code_reduction_20261111/`，Godot案例原始日志在`movement_presets/code_reduction_*_20261111/`。

先核对GGF ArchiveSystem/ObjectPool原API、调用链和原生节点摘挂，再说明存档注入与节点池收敛方案并实施；依据用户本轮明确的去包装要求，同步第00/70章、ProjectTour和双人计划的当前数据流。没有修改Framework、addons、Luban、生成代码、源配置、场景或正式存档；没有把上轮方法体规范回退成压缩表达式。

### 同范围、同口径结果

文件范围由开工277份手写源码固定，删除文件仍计入开工值，最终为其存活275份。类型按完整声明名称去重partial，区分class/record class、struct/record struct和interface；方法计构造及接口声明，不含访问器、Lambda和局部函数。总行数包含XML/空行；代码行数为Roslyn有效token所在行，排除纯注释/空行；两端均启用DEBUG/TOOLS/日志符号。

| 指标 | 修改前 | 修改后 |
| --- | ---: | ---: |
| C#源码文件 | 277 | 275 |
| 类（含record class，partial去重） | 284 | 280 |
| 结构/record struct | 15 | 15 |
| 接口 | 27 | 24 |
| 方法/构造/接口方法声明 | 1565 | 1540 |
| 声明字段 | 910 | 907 |
| 总行数 | 37374 | 36888 |
| 有代码token的行数 | 23137 | 22881 |
| try语句 / catch子句 | 57 / 51 | 57 / 51 |
| 精确相同方法体组（至少2语句/25token） | 6 | 6 |
| 本轮确认的重复节点归还实现 | 2 | 1 |
| 本轮确认的重复怪物配置索引 | 1 | 0 |
| 本轮确认无调用的非泛型取池/两项全回收入口 | 3 | 0 |

业务范围净减**486行**；包括必要Debug/测试迁移与节点池回归在内，全部受影响C#为**+163/-616，净减453行**。未把文档行数混入代码收益。没有声称项目所有潜在重复或无效代码均为零；具体可删除项以下列已追踪入口为准，精确方法体组保留依据见后文。

### 删除、合并与保留依据

- **删除4个类、3个接口、25项方法/构造/接口方法声明，没有新增生产类型或方法。** 类为`ArchiveUtility<TCatalogue,TData>`、`PoolContainer`、`BindableListExtensions`、`BindableDictionaryExtensions`；接口为`IReadonlyBindableProperty<T>`、`IReadonlyBindableList<T>`、`IReadonlyBindableDictionary<TKey,TValue>`。两个实体文件及对应UID删除，两个扩展类原本随集合类同文件，未拆新文件。
- **存档少一层调用与注册。** `ArchiveUtility`只转发3个属性和5个读写方法，没有校验、转换或独立生命周期。Prepare和独立测试场地将GGF原`ArchiveSystem<GameCatalogue,GameData>`直接传给SaveSystem，删除两处Utility注册及4处GetUtility查找；唯一DeleteAsync调用迁移原`Delete`。GGF继续独占队列、原子写/备份和序列化；SaveSystem继续负责版本、六格目录、取消、检查点和结果提示。无新的IArchive接口/兼容构造/全局单例。烟测中一个目标类型new调用首轮遗漏产生1编译错误，已补参数；原规则夹具的SaveSystem替身保持原状。
- **节点池只保留泛型取用和IPoolable归还。** 删除非泛型`RentNode`、`Release(NodeObject)`、两项无调用`ReleaseAll`、`RentNodeInternal`及`TryGetContainer`。泛型取用吸收原私有流程，错类型也沿同一IPoolable路径归还。三份场景/容器索引收敛为`Dictionary<string,(PackedScene Scene,Node Container)>`，借出映射只存实例号→池名；原生Node替代只存PoolName的子类。保留OnGet→显示→摘挂→登记的顺序，以及GF.Unspawn→OnRelease→隐藏→摘挂→移除归属的正常归还顺序。删除了上轮P1所指的错误包装归还路径，未并存旧接口。
- **绑定保留实现和只读集合视图，删无消费者契约/转换包装。** 三接口只有本实现与测试静态类型在使用，生产Model始终返回具体Bindable；迁移三处测试别名，回调仍接收标准只读视图。列表/字典直接实现标准IReadOnlyList/IReadOnlyDictionary，原只读视图不可写、优先级/重复委托退订/初始化失败撤订的断言全部保留。两个ToBindable扩展仅测试使用，改调用原构造函数，不留转发。`EnemyBodyPresets.Swim`默认重载也只转发泛型方法，唯一测试改为显式`Swim<EnemyAttackState>`，状态迁移断言不变。
- **重复状态/防御删除。** ConfigurationModel的`_enemies`是生成TbEnemyConfig索引的重复副本；启动校验已经要求EnemyId==EntityId且唯一，GetEnemy改直接读原表，删除陈旧“取首条难度档”逻辑。回滚在ValidateEnvelope后保留SequenceEqual与余额检查，去掉其后重复的数量/去重/Contains；ProfileModel构造固定一至两名不重复英雄，SequenceEqual本身校验长度/身份/席位。AbstractSystem构造保证非空绑定，GetArchitecture不再每次兜底；Model/Command/Query存在未绑定阶段，仍保留其检查。FSM Store复用已有EnsureMissing，未删除注册前防止改变对象归属的检查。目录摘要已由私有ReadSlotsAsync创建独立只读快照，发布时不再复制同一数组。
- **必须保留的处理。** 全范围引用扫描覆盖759份实际编译输入，并进一步核对Tests、动画方法轨道、CallDeferred、EditorScripts/GDScript及维护工具；未把“普通C#无引用”误当死代码。原51个catch承担异步void入口、外部IO/配置资源失败转换、独立池条目隔离、取消识别或对称清理；没有仅为降数字改异常语义。6组精确相同方法体分别是Map/Character开窗通知、Start/Map设置请求、Skill/Inventory回收、List/Dictionary初始化订阅，以及Model/System绑定和退出；各自操作不同实例/类型/生命周期。共用它们需要额外基类、委托或生命周期对象，当前保留直接实现，未制造新抽象。IPoolable、ITips、QF能力接口及身体/AI契约有实际框架或多态消费者，保留。

本轮新增生产代码只有现有SaveSystem构造中的GGF服务依赖、节点池合并后的2个字典字段和归还所需直接操作；删除原转发层/3份池索引/重复配置表后，字段声明净减3，没有新增注册、依赖包或继承层。新增的36行Debug节点池回归有实际新覆盖：无业务父节点借用、正常摘挂/隐藏、同实例复用、错类型返回null后GF借用计数及业务父节点清零，随后仍可复用。首轮误从IObjectPool调用只在ObjectPoolBase上的GetAllObjectInfos产生2错误，已改用框架现有非泛型查询返回基类，不新增观测API。另展开1处TOOLS条件内原有单行catch，补齐既定块体格式。

### 验证

| 检查 | 实际结果 |
| --- | --- |
| 构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：**退出0、0错误/5既有警告**（`build_final.log`）。 |
| 原规则套件 | `dotnet test Tests/{ProfileTests,BattleTests,LevelTests}/{同名}.csproj --no-restore -v quiet`：**339/339、197/197、19/19全部通过**；原断言与输入保持，只有已删除内部包装调用迁移。 |
| Godot扫描 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py code_reduction_scan_20261111 --headless --editor --build-solutions --quit --no-window`：**退出0**；保留既有ComponentInsoector/ArchiveSettingInspectorPlugin退出free诊断。 |
| 资源引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出0，2284路径/391UID**，较基线少2个已删除脚本UID，无悬空引用。 |
| 完整双人 | 同runner案例`code_reduction_coop_20261111 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 30000 -- --smoketest=ui --coop-only`：**退出0、完整SMOKE PASS、SMOKE-NODE-POOL PASS**；赠送/满包、技能页绑定/退订、P2键、HUD/无双、相机/全灭/出口/甘露/龙宫/检查点通过。错类型日志为新增断言的预期注入；历史桥接GCHandle诊断及退出13个ObjectDB/3资源保留。 |
| 完整前端 | 同runner案例`code_reduction_frontend_final_20261111`、相同渲染参数、`--quit-after 18000 -- --smoketest=frontend`：**退出0、完整SMOKE PASS**。六格、新旧档读写、删除覆盖、取消/迟到请求、架构失败清理、地图购技/保存返回通过。`code_reduction_frontend_20261111`也PASS，但当时最后一处摘要去重复制尚未重编，最终以前者为准。预期坏JSON/重复注册注入、退出5个ObjectDB/1资源保留。 |
| 完整单人UI | 同runner及渲染参数，案例`code_reduction_ui_final_20261111 --quit-after 30000 -- --smoketest=ui`：**退出0、完整SMOKE PASS及SMOKE-NODE-POOL PASS**；60/120Hz地形、战斗/无双、生命魔法球、暂停、订退/实体复用、旧版本拒绝、失败清理和存档重入通过。保留预期错类型/装配失败注入、历史桥接GCHandle诊断；退出23个Texture RID、8个ShapedText RID、2个Font RID、38个CanvasItem RID、146个ObjectDB/11资源诊断。 |
| 独立场地 | 同runner案例`code_reduction_arena_20261111 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 600 res://TheGame/Scenes/test_arena.tscn`：**退出0**；原出生点Wukong@(120,300)、两怪显示、场地就绪和GGF关停均完成。退出3个Shape2D RID、8个ObjectDB/3资源，与既有场地证据签名一致。 |
| 风格/范围/存档 | `dotnet format whitespace <GodotProject.csproj/ProfileTests.csproj/BattleTests.csproj> --no-restore --include <本轮各自明确绝对路径> --verify-no-changes`：**全部退出0（15/3/1份）**；全275份style以info级IDE0022/21/61/23/27、IDE0011、IDE1006校验**退出0**。Roslyn另启用TOOLS核对块体/注释/参数，均0问题（`style_audit.log`，1543包含3个局部函数，与统计表口径区分）。最终29份增量仅在授权范围（含2个脚本/2个UID删除），UTF-8/CRLF/两种diff --check通过；**正式12/12递归SHA256、HEAD及用户暂存索引散列保持**。 |

所有Godot案例独立APPDATA，未打开正式GameData写入。上轮热更新部分成功仍提交完整清单的问题继续保持待裁决，未在“保持原功能”的精简中改变下载/强制更新政策；此前D0～E1真实键盘及逐关实战待补也保持原状态。

## 全项目手写业务 C# 审查与安全整理（2026-11-11）

本轮按目标文件 `0bd5a2ce-01e4-4007-922c-13c6a53a13e4/goal-objective.md` 及用户确认的“全项目手写业务 C#”执行。开工 HEAD 为 `c82524bc8da0a4718176c0b819e62451b42d582f`，工作区干净。依据集中规范第00/10/30/50/70章；新指令覆盖第10章原单行方法表达式体偏好，已同步该章和 `.editorconfig`。仅检查 TheGame 非生成业务及 EditorScripts 手写 C#，共 **277份、原1564个方法/构造/局部函数**；测试和 Debug 用于回归，不进行无关格式迁移。

覆盖方式为全范围 Roslyn 语法、命名、注释参数及复杂度检查，再沿架构注册/失败回滚、存档校验/提交、装备交易、输入/技能、英雄/首领物理、掉落回池、UI订退和流程异步边界人工核对。保留现有模型、GGF/QF职责、公开API、输入映射、场景和存档v14；不新增类型、接口、运行时兼容层或管理器。证据位于 `Godot/GodotProject/.godot/validation/code_review_20261111/`，Godot原始运行日志在 `movement_presets/code_review_{scan,frontend,coop,ui}_20261111/`；运行器为每例独立设置APPDATA。

### 按优先级的发现与处理

本轮未确认P0。两项P1属于会改变既有行为的修复，已先向用户报告，按目标第7/8节保留原实现并交架构师裁决；未将其记为已修复。

| 级别/状态 | 位置 | 原因、影响及建议 |
| --- | --- | --- |
| **P1，待裁决** | `MainPack/Scripts/Procedure/ProcedureUpdate.cs:317、343、355、855` | 只以 `downloaded == 0` 拒绝下载失败；当 `0 < downloaded < toDownload.Count` 时，强制更新仍进入加载及完整服务端清单提交。加载器记录失败并回滚版本文件后，外层还会写入 `serverVersion`，覆盖回滚结果。静态分支已确认，未连接真实更新服务器复现。建议先明确非强制更新的部分成功政策；强制更新在必要包下载/加载全部成功后才提交清单，并把既有加载结果反馈给总流程，不另建更新管理器。 |
| **P1，条件路径待裁决** | `MainPack/Scripts/ObjectPool/NodePool.cs:267、305` | `Release(NodeObject)` 在节点仍有外部父节点时直接 `container.AddChild(target)`；Godot不会自动改父节点，节点未回容器却继续Unspawn并移除追踪。触发入口是非泛型 `RentNode(scenePath, parent)` 的归还，或泛型类型不匹配后的内部归还。当前正常业务消费者均使用 `IPoolable` 重载，该路径已有RemoveChild，不能把问题扩大为当前掉落必然失效。建议复用既有归还路径/原生摘挂节点，并先核对OnRelease和Unspawn的时序，再验证这两条入口。 |
| **P2，已整理** | `Commands/EquipmentCommands.cs`、`Entity/Drops/GroundItemNode.cs` | 五种装备命令把会话前置条件与实际交易分开；拾取按资格、几何接触、实际入账、表现提交分段。保留原短路顺序、满包重试和首个成功领取者规则，无共享Guard抽象。 |
| **P2，已整理** | `Systems/ConfigurationSystem.cs` | 原OnInit的140行、51分支混合多域校验；复用原类拆成角色/关卡/战斗三个真实业务阶段，OnInit现36行、3分支。各校验表达式、调用顺序、错误汇集/异常文案保持；未按行数继续拆分既有校验器。 |
| **P2，已整理** | `Systems/SkillSystem.cs`、`SaveSystem.cs`、`Input/InputSystem.cs`、`Combat/CombatSystem.cs` | 技能状态查询/蓝量检查明确前置条件和读取顺序；建档DTO选择、空槽识别和快照捕获分步；逐项输入采样与攻击消费顺序保持；战斗等级差局部名称明确，无双收益分出比例和最终取整。无存档/公式/输入采样频次变化。 |
| **P2，已整理** | `UI/SkillForm/SkillForm.Logic.cs`、`Entity/Heroes/Tangseng/TangsengEntity.cs` | 技能窗关闭键改为可逐步调试的席位循环，保留Esc优先、按席位短路和单人P2键拒绝；蓄力未满/满值闪色分开，读取时钟条件和色值保持。 |
| **P3，已整理** | 本批116份手写C#、`.editorconfig`、第10章 | **307处表达式体方法、15处表达式体访问器、11处单行块**统一为完整语句块。保留简单只读属性；目录创建和飞行配方惰性缓存两处带副作用属性使用完整get。补齐DeferredMana及OnHide参数契约，修正水幻影“创建”注释为已装配分身激活。 |

剩余49项长条件主要为启动配置/外部数据校验、装配契约或状态组合；没有为清零复杂度计数而提前求值、拆成无语义布尔变量或增加包装方法。`ProcedureUpdate`下载/重试/版本提交属于上表P1关联行为，未在格式整理中重写其异步生命周期。普通集合操作、框架/Godot回调、生成契约及已有业务术语保留名称。

### 实际验证与边界

| 检查 | 命令与实际结果 |
| --- | --- |
| 开工基线 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出0、0错误/5既有警告；`dotnet test Tests/{ProfileTests,BattleTests,LevelTests}/{同名}.csproj --no-restore -v quiet`：339/197/19全部通过；`baseline_*.log`。 |
| 方法/访问器转换 | SDK `dotnet format style Godot/GodotProject/GodotProject.csproj --no-restore --include <scope.json明确的277份文件> --diagnostics IDE0022 IDE0021 IDE0061 IDE0023 --severity info`，另用IDE0027转换访问器：均退出0。首次未指定info阈值未产生修改，保留日志；实际按info执行。机械方法转换阶段AST等价确认307个原表达式及其余语法不变；访问器阶段另确认15处原表达式及其余语法不变（`accessors_equivalence.log`）。 |
| 全范围风格/契约 | 上述style命令加IDE0027、IDE0011、IDE1006并`--verify-no-changes`：退出0（`style_final.log`）；临时Roslyn审计最终277份/1567方法，表达式体方法0、无括号控制流0、单行块0、受检声明缺失文档0、参数名不匹配/缺项0（`audit_final.log/json`）。原报告将C#14 extension块误计为缺文档，已按声明类型纠正。签名对比仅新增3个private配置阶段方法，原类型/方法/字段/属性及访问器契约保持（`contracts.json`）。 |
| 最终构建/测试 | `dotnet build Godot/GodotProject --no-restore -v quiet`：**退出0、0错误/5既有警告**；三个`dotnet test ... --no-restore -v quiet`：**Profile339/339、Battle197/197、Level19/19**。证据`build_final.log`、`{profile,battle,level}_final.log`；既有NU1900/测试桩字段警告保留。 |
| Godot扫描/引用 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py code_review_scan_20261111 --headless --editor --build-solutions --quit --no-window`：**退出0**；保留既有ComponentInsoector/ArchiveSettingInspectorPlugin的RefCounted/free退出诊断。`python Tools/ProjectMaintenance/validate_game_references.py`：**退出0、2284路径/393UID**。 |
| 完整前端 | 同runner，案例`code_review_frontend_20261111 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：**退出0、完整SMOKE PASS**；六格读写、单/双人建档、架构失败回滚、地图购技/保存返回等原断言通过。预期坏JSON/重复注册失败注入保留；退出5个ObjectDB/1资源诊断。 |
| 完整双人专项 | 同runner，案例`code_review_coop_20261111`使用相同渲染参数、`--quit-after 30000 -- --smoketest=ui --coop-only`：**退出0、完整SMOKE PASS**；赠送/满包、P2技能页/切换订退/两席位关闭键、双HUD/无双/出生/掉落、相机/死亡/出口/甘露/平均生命全通过；退出13个ObjectDB/3资源诊断。 |
| 完整单人UI | 同runner，案例`code_review_ui_20261111`使用相同渲染参数、`--quit-after 30000 -- --smoketest=ui`：**退出0、完整SMOKE PASS**；60/120Hz地形、战斗/技能、暂停、掉落归属、订退复用、加载取消/装配失败清理及存档重入通过。历史已记录的引擎资源加载桥接GCHandle未初始化诊断仍出现；BattleForm装配失败是原烟测注入。退出23个Texture RID、8个ShapedText RID、2个Font RID、38个CanvasItem RID、146个ObjectDB及11资源诊断保留，不宣称运行日志无错误。 |
| 格式/范围/数据 | `dotnet format whitespace Godot/GodotProject/GodotProject.csproj --no-restore --include <本批116份C#绝对路径> --verify-no-changes`：退出0。UTF-8/CRLF/无行尾空白及`git diff --check`通过；正式GameData **12/12递归SHA256保持**，HEAD和索引散列保持。合计120份变更（116份手写C#及4份规范/记录），无场景/资源/配置/测试/Debug/只读依赖变化，无新增/删除业务文件；文件清单见`changed_files.txt`。 |

本轮不增加仅复述实现的测试，复用原规则和完整运行时烟测。未进行真实远程热更新、非泛型池故障入口或真实键盘上手验收；前两项与上表待裁决行为修复一并补验。此前双人D0～E1正式键盘及E1-3全关实战待补状态保持，不将自动烟测记为实机通过。

## 删除确认按钮边框与目标复核（2026-11-11）

用户要求确定/取消看起来像按钮且搭配近黑弹窗；当前修改仅在原save_form场景加入局部StyleBoxFlat，两按钮共用深灰底/1px灰边、4px圆角，悬停/按下转暖橙细边，键盘焦点保留2px橙色框。原100×48尺寸/位置、字体、文案与取消/确认/覆盖业务均不变；没有新资源、公共Theme或业务脚本。既有FrontendSmokeScenario仅增加悬停截图，继续复用原磁盘/取消/确认断言。依据当前用户指令和第00/10/40/50章；用户此前已暂存全部既有修改，执行者保留其内容，不作Git写操作。

| 验证 | 命令与实际结果 |
| --- | --- |
| 构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：**退出0、0错误、5既有警告**，`.godot/validation/dialog_buttons_20261111/build.log` |
| 扫描 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py dialog_buttons_scan_20261111 --headless --editor --build-solutions --quit --no-window`：**退出0**，既有ComponentInsoector/free退出诊断保留 |
| 删除专项 | 同runner案例`dialog_buttons_delete_20261111 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend --save-delete-only`：**退出0、SMOKE-SAVE-DELETE PASS、完整SMOKE PASS**。实际鼠标打开/取消逐字节不变、确认删除隔离主备、其余槽保持、同格重建、读回和迟到确认全部通过；本次stderr为空。正式目录未使用 |
| 引用/格式 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出0、2284路径/393UID**。`dotnet format whitespace Godot/GodotProject/GodotProject.csproj --no-restore --include Godot/GodotProject/TheGame/MainPack/Scripts/Debug/FrontendSmokeScenario.cs --verify-no-changes`：**退出0** |
| 视觉/数据/范围 | 实际查看本轮`screenshots/save_delete_confirmation.png`及`save_delete_confirmation_hover.png`，普通灰边、悬停橙边、焦点橙框和字色清楚，面板仍近黑。**正式12/12 SHA256相同，用户暂存diff逐字节散列相同**；本轮4份未暂存增量为save_form.tscn、FrontendSmokeScenario.cs、WorkingNotes.md和本记录，UTF-8/CRLF/无行尾空白与两种diff --check通过，无新增/删除 |

再次对照当前持久目标（D0～E1）、计划完成判定、当前源码及原始日志：没有发现新的计划内代码开发缺项。原始最终规则 **339/197/19**、完整UI和双人专项PASS记录已读取确认；本轮只改局部前端样式，未重跑全部规则/完整战斗UI，不把旧结果说成本轮执行。目标 **尚未全部完成**，尚缺：

- D0上手1～4：正式旧档可读、原技能/余额/装备、退出重开与截图。
- D1最终上手1～7：人数/选角/重选/返回、覆盖取消、第5/6格建档与读回；原第8条仅P1是D1中间态，已由D2双英雄要求替代。
- D2上手1～8：双英雄出生、HUD、同时键盘输入、技能耗蓝/经验/球/地面归属、本人背包与P2无双、单人回归。
- D3上手1～6：存活者取景/屏幕墙、一死一活/全灭、P2出口与平均生命、甘露绿色回血、地图提示。
- E1上手1～6及E1-3：真实赠送/满包/单人禁用/请求者技能页与扣费，剩余关卡及隐藏龙宫逐关双人实战。原表遗漏第9关鲨之涡流已登记，实际水牢属于该关，不能以原表宣称全关验过。

本轮node_repl/@oai/sky的`list_windows()`复查仍报 **Computer Use native pipe is unavailable / os error2**；无真实键盘输入。既有“允许验收待补继续收尾”授权保持，正式上手及逐关通过证据缺失，未据自动烟测把目标判为完成。D0～E1总交付、接口/文件/逐步错误数和转存备份仍在下方总说明，供架构师审查。

## 双人计划复核与前端统一（2026-11-11）

用户要求仔细核对双人计划，并修改未解锁地点图鉴、开始字体、删除确认样式；随后明确将确认框从灰改黑，选角底部按钮居中/带边框/缩小，**卡片维持原尺寸**。当前指令授权这些前端场景改动，不修改玩法/正式存档。执行前用户已暂存上一轮28份E1修改，执行者保留其索引，未作Git写操作。证据目录：`Godot/GodotProject/.godot/validation/frontend_polish_20261111/`。

### 双人计划逐项核对

| 任务 | 当前实现核对入口 | 未完成项 |
| --- | --- | --- |
| A1 | ProfileModel有序HeroIds/HeroOf/SlotOf；GameData阵容有序，无全局当前英雄 | 无新增代码缺项 |
| A2 | 个人Soul/Bag/Slots；v14个人引用与全档实例池；SaveDataUtility唯一归属与逐人容量校验 | 无新增代码缺项 |
| A3 | Profile规则与两席位往返/非法第二人原子拒绝；SaveSlotInfo全员摘要，单/双人名称均不含lv | 无新增代码缺项 |
| B1 | HeroModel→HeroState容器，HeroSystem/HeroStatusSystem按英雄，效果与订退隔离 | 无新增代码缺项 |
| B2 | SkillSystem按heroId登记/施放/清理；英雄OnHide只清本人 | 无新增代码缺项 |
| B3 | 个人数值/效果/无双/技能/回池/重建订退规则证据，A～B已架构审查 | 无新增代码缺项 |
| C1 | HeroSpawn/Slot参数、校验与池显示覆盖，IHeroBody席位 | 无新增代码缺项 |
| C2 | 最后有效命中者、死亡事件来源、经验与牛心归属，无英雄命中不出球 | 无新增代码缺项 |
| C3 | 球按OwnerEntityId追踪/入账，物品实际接触者，治疗在实际英雄位置 | 无新增代码缺项 |
| C4 | project.godot恰好13个P2动作，InputSystem按实际席位采样/消费，单人忽略P2 | 无新增代码缺项 |
| C5 | 阶段C回归记录、红球夹具授权修复和原断言保持 | 无新增代码缺项 |
| D0 | 离线工具/样例/正式8文件转存和整目录备份记录存在 | 正式上手1～4待补 |
| D1 | 新开/读取分离、人数、选角、保存模式、DeleteSlotAsync覆盖链 | 最终上手1～7待补；原第8条仅P1是D1中间态，已由D2双英雄验收替代 |
| D2 | 阵容实体集合/每次await进入周期检查/P2偏移、双HUD与现有Panel绑定、单窗互斥 | 正式上手1～8待补 |
| D3 | 存活者平均相机、全员墓碑后判负、全体平均生命、任一幸存者出口/入口、球结束统一判断、甘露队友范围/龙王最低等级 | 正式上手1～6待补 |
| E1 | TryGift/Command原实例赠送/满包拒绝；SkillFormData指定请求者、三项来源先退后订与本人扣费；构造/空条件清理及文档 | 上手1～6、E1-3逐关实战待补 |

**未发现新的双人代码缺项；仍不能宣称双人已完成全部验收。** 最新完整规则339/197/19、前端/UI/双人专项等证据见下方E1交付，本轮只修改前端，未重新运行全部规则或完整战斗UI。生产唯一HeroOf(P1)为ProcedureMap的默认技能页；关卡旧单英雄字段/SkillForm默认P1/HUD席位记录已清理。计划第九部分第4节原占位表仍显示“留D2/D3/E1”，本次已同步为实际完成状态，历史批次规格仍保留。

最终验收口径补充：D1上手第8条“仍只出现P1”是D1交付时的迁移检查点。当前ProcedureLevel按profile.HeroIds显示全部英雄，D2上手第1条要求双人同时出现；最终状态应验证D2双英雄，不能为了补D1历史项退回单人。D1当前仍需补1～7的真实键盘路径，第8条标记为被D2最终要求替代；下方各批历史“1～8未执行”保留为当时记录，不代表最终要同时满足两个相反显示状态。

另发现**E1-3原表漏列第9关鲨之涡流**，其“水牢”观察点实际属于该关鲨魔王SharkDemonKingEntity，不属于原表的浅岸/东海/牛魔谷。该关已有场景/敌人实现，但正式双人实战未核验；已在原E1-3表后登记遗漏，原表未擅自改范围，交用户/架构师裁决。恢复实机验收时应明确覆盖该关的水牢/延迟招式归属，不能用原九关表代替全部主线核验。既有computer-use本机管道阻塞与各批明确暂留授权保持。

### 最终前端改动与素材取舍

- MapLocationButton在既有_GuiInput入口检查Disabled，锁定地点不查询/不打开图鉴；复用MapForm已有解锁状态，无第二份资格或事件。
- 开始页全部11个Label/Button本地引用局内`game_font.tres`，字号、主布局和帮助全文/滚动保持；不改其他窗口共享Theme。
- 搜索旧素材及官方包77张候选，核验Commonv3720 SHA256 `53722c6c8615d2542de2e3cc255a68c5d80712c6b8097aaecae6bc7be0a40ab2`，FFDec直接导出位图230/352×201。用户认为灰色不适配后，该灰图替换已还原；最终用原生Panel+StyleBoxFlat近黑底/1px细边，白字20px、开始菜单原Theme的橙色悬停Button，原焦点/取消确认/覆盖流程保留。不新增正式美术文件，不保留本次无消费者素材。
- 选角原重选/返回移动到居中HBoxContainer，按钮94×30/18px、深色底/2px边框；中心X470、Y556。四张卡片维持原235×590、scale1、原卡片图像；缩小卡片的中间实现已按用户明确纠正撤回。单人重选隐藏，返回由同一容器居中，无新增布局脚本。根绑定改Actions路径后运行既定生成器仍6绑定；生成文件内容等同原绑定字段，无手改。

| 验证 | 命令与实际结果 |
| --- | --- |
| 构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：新增图鉴Debug局部变量同名首轮 **2错误**（build.log），改名后及后续黑框/布局最终构建均 **0错误、5既有警告**（build_fixed.log / black_character_build.log / layout_build.log / small_actions_build.log）。最后小按钮首次烟测启动时前一build仍未返回，载入旧程序集导致旧尺寸断言失败，退出1（small_actions_frontend.log）；等待该构建退出0后以相同源码重新运行，不修改断言 |
| 扫描 | 旧局部名冲突时首轮扫描退出1，修复后frontend_polish_scan_fixed及frontend_black_character_scan均 **退出0**，ComponentInsoector/free既有退出诊断保留；最终布局扫描frontend_polish_final_scan_20261111 **退出0**（scan_final.log） |
| 完整前端 | runner规定显示参数，`frontend_polish_frontend_20261111 --smoketest=frontend` **退出0、完整SMOKE PASS**，灰色阶段；黑框/居中大按钮的`frontend_black_character_20261111` **退出0、完整PASS**；卡片缩小中间案`frontend_character_footer_20261111` **退出0、完整PASS**但用户否定，图片不作为最终效果；最终原尺寸卡片/小按钮案例`frontend_small_actions_verified_20261111` **退出0、完整SMOKE PASS、SMOKE-COOP-MENU PASS**，退出5个ObjectDB/1资源（small_actions_verified.log）。新开/人数/选角/重选/返回、覆盖取消/确认、前端各既有路径原断言保持 |
| 删除专项 | `frontend_polish_delete_20261111`灰色阶段 **退出0、完整PASS**；最终黑框`frontend_black_delete_20261111 --smoketest=frontend --save-delete-only` **退出0、SMOKE-SAVE-DELETE PASS及完整PASS**，真实鼠标取消逐字节不变、确认移除隔离主备/其余格保持/同格新建/迟到确认通过。退出5个ObjectDB/2资源，原始日志保留 |
| 图鉴专项 | runner规定显示参数，`frontend_polish_guide_final_20261111 --smoketest=frontend --level-guide-only`：**退出0、SMOKE-LOCKED GUIDES PASS:9个锁定地点拒绝图鉴，原10张卡片/两个窗口尺寸/10地点悬停退出/装备详情/点击进关全部PASS、完整SMOKE PASS**，退出3个ObjectDB。锁定集合另断言非空，避免空循环假通过 |
| 引用/格式 | `python Tools/ProjectMaintenance/validate_game_references.py`首轮 **退出0、2284路径/393UID**；最终黑框引用 **退出0、2284路径/393UID**（references_final.log）；4份手写C#的`dotnet format whitespace Godot/GodotProject/GodotProject.csproj --no-restore --include <本批4份C#> --verify-no-changes` **退出0**（format_final.log） |
| 画面/正式数据 | 开始页及黑框删除确认、原尺寸卡片/小按钮截图已实际查看；最终截图存入本轮目录screenshots。正式目录递归 **12/12 SHA256一致**；11份本轮增量文本UTF-8/CRLF无行尾空白，两种diff --check退出0；用户28份已暂存E1修改保持，无Git写操作。索引及状态记录于scope_final.log。全部烟测使用案例独立APPDATA，未操作正式1～4格 |

本轮相对用户已暂存E1修改的增量：Docs的CoopImplementationPlan/LegacyAssetMap/WorkingNotes/Validation；MapLocationButton.cs、SaveForm.Logic.cs、FrontendSmokeScenario.cs、FrontendSmokeScenario.LevelGuide.cs；start_form.tscn、save_form.tscn、character_form.tscn。无新增/删除正式文件，原PNG与import_frontend_assets.py已还原为原内容。接口只有确认按钮节点类型TextureButton→Button，取消/确认事件与存档签名不变；选角2个Debug节点路径机械加Actions/。UI绑定生成器运行记录6个绑定，生成字段未改变。完整战斗/规则测试沿用前轮证据，不把本轮未执行写成重跑。

## 双人D0～E1总交付（2026-11-11，代码收尾，正式实战待补）

本节是架构师的最终状态入口；各批历史日志、失败和用户裁决保留在下方原记录。阶段A～C基线0523aae；D0提交0307f36，D1/D2和Panel调整合并于ae2a1ba，D3为c63cd1d；本轮E1未执行任何Git写命令。**代码与自动验证收尾，正式键盘/逐关实战未完成**。用户已逐次授权暂留D0～D3验收推进，并明确回复“允许待补，继续E1-4收尾”；这不是实机通过记录，E1原完成判定中的实机项仍待补。

规范依据：第00章所有权/持续绑定/局部同步事件，第10章命名和中文文档，第30章实体/状态机，第50章既有生成/验证入口，第70章个人资产/存档事务；执行边界为实施计划第一、二、七部分及用户明确裁决。没有新Manager/中间转发层，没有默认P1兼容接口，没有改v14、源表、框架或只读依赖。HUD复用既有Panel拥有个人绑定，SkillFormData只携带默认英雄。

### D0　正式v12离线转存

工具先规划/校验全部主备文件，dry-run不写，用户确认后完整备份，再逐个原子替换8份Data文件；Catalogue原字节保持。新增Python CLI而不接入游戏运行时；无运行时接口/格式改动。D0-1/2构建错误数 **0/0**，D0-3无代码变更、确认前最终构建 **0**；规则 **324/197/19**。工具边界10组检查与样例往返通过，首轮样例测试CS0121修复及原日志保留；正式输出和备份见本总说明末尾。

正式上手 **D0-1～4均未执行**（这里编号指D0上手步骤）：原管道缺失，未取得正式键盘截图。后续用户明确允许先D1，旧档转存结果保留。正式写入唯一例外就是本次用户确认的转存，不把后续自动测试写入正式目录。

<details>
<summary>D0实际新增/修改文件（A新增，M修改，无删除）</summary>

```text
M	Docs/CoopImplementationPlan.md
M	Docs/Validation.md
M	Docs/WorkingNotes.md
A	Tests/ProfileTests/Data/save_v12_tangseng.json
A	Tests/ProfileTests/Data/save_v14_tangseng.json
M	Tests/ProfileTests/ProfileTests.csproj
A	Tests/ProfileTests/SaveUpgradeTests.cs
A	Tools/ProjectMaintenance/upgrade_saves_v12_to_v14.py
```

</details>

### D1　入口、双人选角与建档

新开人数→依次选角→保存选格由ProcedureCharacter拥有，读取独立走Load模式；覆盖复用DeleteSlotAsync后重读空格，不另建写入路径。接口：SaveSelectionVariable.Create从单heroId默认参数改为显式int[] heroIds；SaveSystem.LoadOrCreateAsync从initialHeroId标量改为显式IReadOnlyList<int>阵容；StartGameRequested改为NewGameRequested/LoadGameRequested，CharacterConfirmed携带int[]，新增本窗口CharacterFormData/SaveFormData及关闭取消通知。详见下方D1接口表。

五步结束构建错误数 **0/2/8/0/0**，最后清零；其中D1-1/5新增夹具参数/摘要错误修复，原2/1错误日志保留。最终 **324/197/19、扫描0、引用2283/392、完整前端/UI与删除专项PASS**。用户授权无双采样夹具修复、JSON必填断言契约缓存隔离；保留生产动画/校验和原断言。帮助按用户裁决保留全文/19px/原区域，改原生滚动并验证顶部/末尾。

正式上手 **1～8均未执行**。隔离截图在`coop_d1_20261110/screenshots/`，包含标题/人数/保存覆盖/双人摘要/帮助顶部末尾/无双尾段；原裁切图仍保留，不是正式键盘验收。D1→D2获用户明确推进例外。

<details>
<summary>D1实际文件25份（4新增，其余修改，无删除）</summary>

```text
M	Godot/GodotProject/TheGame/MainPack/Scripts/Procedure/FlowData/SaveSelectionVariable.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Procedure/ProcedureTitle.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Procedure/ProcedureCharacter.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Procedure/ProcedureLoadGame.cs
M	Godot/GodotProject/TheGame/GameScripts/Systems/SaveSystem.cs
M	Godot/GodotProject/TheGame/GameScripts/UI/StartForm/StartForm.Logic.cs
M	Godot/GodotProject/TheGame/GameScripts/UI/CharacterForm/CharacterForm.Logic.cs
M	Godot/GodotProject/TheGame/GameScripts/UI/SaveForm/SaveForm.Logic.cs
A	Godot/GodotProject/TheGame/GameScripts/UI/CharacterForm/CharacterFormData.cs
A	Godot/GodotProject/TheGame/GameScripts/UI/CharacterForm/CharacterFormData.cs.uid
A	Godot/GodotProject/TheGame/GameScripts/UI/SaveForm/SaveFormData.cs
A	Godot/GodotProject/TheGame/GameScripts/UI/SaveForm/SaveFormData.cs.uid
M	Godot/GodotProject/TheGame/UIs/start_form.tscn
M	Godot/GodotProject/TheGame/UIs/character_form.tscn
M	Godot/GodotProject/TheGame/GameScripts/UI/StartForm/StartForm.cs
M	Godot/GodotProject/TheGame/GameScripts/UI/CharacterForm/CharacterForm.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Debug/FrontendSmokeScenario.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Debug/MapProgressSmokeScenario.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Debug/LevelSmokeScenario.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.cs
M	Tests/ProfileTests/ProfileTests.cs
M	Docs/CoopImplementationPlan.md
M	Docs/WorkingNotes.md
M	Docs/ProjectTour.md
M	Docs/Validation.md
```

</details>

### D2　阵容实体、双HUD与Panel所有权

Procedure按阵容显示/回收，P2出生右偏60px；LevelViewController借用完整列表，治疗/穿戴订阅对称，地面物品只给首个成功接触者。StartSession的AbstractHeroEntity参数改IReadOnlyList<AbstractHeroEntity>；BattleFormData.Hero改Heroes。菜单请求携带PlayerSlot，MusouStateChanged携带HeroId/活动态，同步暂停并保持同帧排他。

原四步构建错误数 **2/1/9/0**。用户指出SeatBinding像补丁后已比较FormData/事件/订阅句柄/快照/子场景等并授权调整：已有BattleStatusPanel/BattleMenuPanel用具名BindHero/UnbindHero持有个人来源；Form只驱动生命周期与共享表现，删除席位记录及逐人闭包，没有新旧并存。追加重构首轮新增Debug缺命名空间 **3错误→0**；最终 **324/197/19、扫描0、引用2283/392、完整前端/UI/双人专项PASS**，九项个人来源各1/关闭全0、无双同步排他/活动态改绑不重播均通过。

计划漏列烘焙“无双”文字节点，已获用户授权补翻WsGrey/m_WsMax并按纹理宽度保持占位。正式上手 **1～8均未执行**；隔离证据在`coop_d2_20261110/`与`coop_d2_panel_20261111/`，镜像普通/满值截图及出生/P2背包/击杀均已复查，冻结采样不宣称站立实机。用户后续明确允许进入D3。

<details>
<summary>D2含Panel调整的最终修改文件17份（无新增/删除）</summary>

```text
M	Godot/GodotProject/TheGame/MainPack/Scripts/Procedure/ProcedureLevel.cs
M	Godot/GodotProject/TheGame/GameScripts/Entity/Level/LevelViewController.cs
M	Godot/GodotProject/TheGame/GameScripts/Entity/Level/LevelViewController.Loot.cs
M	Godot/GodotProject/TheGame/GameScripts/Entity/Level/LevelViewController.Entrance.cs
M	Godot/GodotProject/TheGame/UIs/battle_form.tscn
M	Godot/GodotProject/TheGame/GameScripts/UI/BattleForm/BattleForm.cs
M	Godot/GodotProject/TheGame/GameScripts/UI/BattleForm/BattleFormData.cs
M	Godot/GodotProject/TheGame/GameScripts/UI/BattleForm/BattleForm.Logic.cs
M	Godot/GodotProject/TheGame/GameScripts/UI/BattleForm/Components/BattleStatusPanel.cs
M	Godot/GodotProject/TheGame/GameScripts/UI/BattleForm/Components/BattleMenuPanel.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.Inventory.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Debug/FrontendSmokeScenario.cs
M	Docs/CoopImplementationPlan.md
M	Docs/WorkingNotes.md
M	Docs/ProjectTour.md
M	Docs/Validation.md
```

</details>

### D3　存活者取景、全灭、出口、队友治疗与分档

相机Follow(Node2D)改Follow(IReadOnlyList<AbstractHeroEntity>)，取存活者X平均；每名死者墓碑完成后全灭才判负，结算生命取全员百分比平均。唐僧本体/分身按既有实体枚举治疗存活者，范围/恢复量按官方规格；龙王取阵容最低等级，地图任一余额达标提示。

D3-1～6每步最终构建错误数 **0/0/0/0/0/0**，规则 **327/197/19**（新增3例存活者转场），扫描0、引用2284/392、完整前端/UI/双人专项及格式通过。隔离唯一死者球复现pending=True/orbs=0仍Running，用户明确授权后把出口续行统一到_Process的TickLoot后，删除OnLootFinished旧判断，不增超时/状态/事件。相同隔离复验PASS，胜利图实际25%=死者0与存活者50的平均。

正式上手 **1～6均未执行**；隔离截图`coop_d3_20261111/screenshots/`证明部分表现/数值，甘露图未拍到绿色恢复数字，不能据此认定该实机项通过。D0～D2待验保持，用户明确允许D3→E1。

<details>
<summary>D3最终修改文件16份（无新增/删除）</summary>

```text
M	Docs/CoopImplementationPlan.md
M	Docs/ProjectTour.md
M	Docs/Validation.md
M	Docs/WorkingNotes.md
M	Godot/GodotProject/TheGame/GameScripts/Entity/Enemies/Bosses/zm1_11_hidden_dragon_palace/DragonKingEntity.cs
M	Godot/GodotProject/TheGame/GameScripts/Entity/Heroes/Tangseng/TangsengEntity.cs
M	Godot/GodotProject/TheGame/GameScripts/Entity/Level/Camera/LevelCameraNode.cs
M	Godot/GodotProject/TheGame/GameScripts/Entity/Level/LevelViewController.Loot.cs
M	Godot/GodotProject/TheGame/GameScripts/Entity/Level/LevelViewController.cs
M	Godot/GodotProject/TheGame/GameScripts/Systems/LevelSystem.cs
M	Godot/GodotProject/TheGame/GameScripts/UI/MapForm/MapForm.Logic.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.Dragon.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.HeroDeath.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.Tangseng.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.cs
M	Tests/ProfileTests/DragonPalaceTests.cs
```

</details>

### E1　赠送、技能页签与清理

赠送复用原菜单/Command/EquipmentSystem/个人背包集合，原实例/属性保持，接收职业不限；已穿戴、满包、非法参与者拒绝。新增TryGift(fromHeroId,toHeroId,Guid)和GiftEquipmentCommand；菜单Open追加canGift，新增GiftRequested。可用态77×31美术只从校验官方Common包sprite367第1帧导出，未重绘。技能OnOpen必须SkillFormData(HeroId)，两个流程打开点显式传入；窗口按原三项来源具名订退，两人页签可切换，学习只扣当前页英雄，阵容实际席位键可关闭。

E1-1/2/4最终构建错误数 **0/0/0**，E1-3无代码构建；E1-2首轮新增Debug错误1、Profile新增用例枚举错误1及夹具失败已修复且保留原日志。最终规则 **339/197/19**（比D3+12），扫描0、引用2285/393、格式13份游戏/Debug+4份测试通过。清理后完整前端与双人专项再次PASS；最终完整UI亦退出0、完整PASS。E1-3及E1上手 **1～6均未执行**，用户已明确允许待补继续E1-4，关卡表见下方E1详细节；没有发现并擅修新的关卡玩法。

E1-4删除无生产消费者的ProfileModel构造soul参数，改为(roster,curve,capacity)，测试构造后设置余额；生产新档每人StartSoul仍由SaveSystem建DTO，读档恢复Checkpoint。SkillSystem七处GetModel<HeroModel>()空条件访问删除，取消/结束/无敌不再吞掉错误生命周期。保留两个真实地图边界：无参ClearSkillDefinitions由系统OnDeinit在地图触发，无战斗模型时仍清登记；IsAvailable是无战斗模型返回false的查询，原地图测试继续验证。地图购买测试把单英雄清登记移到EnterMapContext前以匹配生产生命周期，原断言不变；无其他Debug构造消费者需要改余额。

P1剩余：生产仅ProcedureMap默认技能页 **1处**；Debug **218处**、Tests **172处**均为单人或显式P1夹具，30份C#逐文件行号表及逐行输出在下方E1节/证据目录；Markdown引用为规范代码示例、历史记录和本次交付解释，不是运行消费者，不做语义无关替换。旧SkillForm占位、单英雄关卡字段及HUD席位绑定记录已清理。

<details>
<summary>E1最终文件（A新增，M修改，无删除）</summary>

```text
M	Docs/CoopImplementationPlan.md
M	Docs/LegacyAssetMap.md
M	Docs/ProjectGuidelines/70-ProfileAndSave.md
M	Docs/ProjectTour.md
M	Docs/Validation.md
M	Docs/WorkingNotes.md
M	Godot/GodotProject/TheGame/GameScripts/Commands/EquipmentCommands.cs
M	Godot/GodotProject/TheGame/GameScripts/Models/Profile/ProfileModel.cs
M	Godot/GodotProject/TheGame/GameScripts/Systems/EquipmentSystem.cs
M	Godot/GodotProject/TheGame/GameScripts/Systems/SkillSystem.cs
M	Godot/GodotProject/TheGame/GameScripts/UI/InventoryForm/Components/EquipmentActionsMenu.cs
M	Godot/GodotProject/TheGame/GameScripts/UI/InventoryForm/InventoryForm.Logic.cs
M	Godot/GodotProject/TheGame/GameScripts/UI/SkillForm/SkillForm.Logic.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.Inventory.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.Skills.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Procedure/ProcedureLevel.cs
M	Godot/GodotProject/TheGame/MainPack/Scripts/Procedure/ProcedureMap.cs
M	Godot/GodotProject/TheGame/UIs/Inventory/equipment_actions_menu.tscn
M	Tests/ProfileTests/EquipmentTests.cs
M	Tests/ProfileTests/ProfileTests.cs
M	Tests/ProfileTests/SkillLearningTests.cs
M	Tests/ProfileTests/TestGameScope.cs
M	Tools/LegacyMigration/export_official_inventory_actions.py
A	Godot/GodotProject/TheGame/GameScripts/UI/SkillForm/SkillFormData.cs
A	Godot/GodotProject/TheGame/GameScripts/UI/SkillForm/SkillFormData.cs.uid
A	Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_action_gift.png
A	Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_action_gift.png.import
```

</details>

### 最终命令、正式文件和未验事项

最终命令均从仓库根执行：`dotnet build Godot/GodotProject --no-restore -v quiet`；三条`dotnet test Tests/<ProfileTests/BattleTests/LevelTests> --no-restore -v quiet`；`python Tools/ProjectMaintenance/validate_game_references.py`；`dotnet format whitespace <工程> --no-restore --include <本批明确文件> --verify-no-changes`。Godot使用既有run_godot.py顺序执行案例，每个案例独立APPDATA，未在烟测中构建游戏；最终案例及实际结果见E1收尾补充表。

正式目录递归12份文件相对D3交付逐字节哈希不变；本轮开工根层2份快照遗漏Data子目录已如实记录并以D3递归清单补核，不能把根层2份写成开工12份。E1不写正式目录，未作Git写操作，保留现有索引。新增UID/PNG导入由Godot生成，Form绑定只通过规定生成器；Framework/addons/Luban/源表/bytes均未改。

待补正式验收：D0上手1～4、D1上手1～8、D2上手1～8、D3上手1～6、E1上手1～6及E1-3九关重点。computer-use初始化可用但list_windows仍native pipe缺失/os error2；不能列窗/发真实键盘。恢复连接后只用第5格双人、第6格单人，不覆盖第1～4格，覆盖确认只取消；按既有表补证，不把自动断言或Frozen截图视为实战。其他现有退出RID/ObjectDB/资源占用、资源加载桥接GCHandle诊断单列保留，不在本批扩大修复。

### D0原始输出与备份

以下为已执行的原始输出复录，未重复运行正式转换。dry-run **OK8/ERROR0/written0**：

```text
OK 1791793987.sav version=12 hero=1 soul=2630135 bag=55 worn=4 -> v14 BOM=yes
OK 1791793987.sav.bak version=12 hero=1 soul=2630135 bag=55 worn=4 -> v14 BOM=yes
OK 1793209553.sav version=12 hero=1 soul=0 bag=0 worn=0 -> v14 BOM=no
OK 1793209553.sav.bak version=12 hero=1 soul=0 bag=0 worn=0 -> v14 BOM=no
OK 1793449001.sav version=12 hero=1 soul=2630135 bag=55 worn=4 -> v14 BOM=no
OK 1793449001.sav.bak version=12 hero=1 soul=100000 bag=35 worn=3 -> v14 BOM=no
OK 1793888918.sav version=12 hero=2 soul=225080 bag=24 worn=3 -> v14 BOM=yes
OK 1793888918.sav.bak version=12 hero=2 soul=225080 bag=24 worn=3 -> v14 BOM=yes
SUMMARY OK=8 SKIP=0 ERROR=0 written=0 DRY-RUN
BACKUP none (dry-run; no files changed)
```

用户回复“确认”后的正式执行 **OK8/ERROR0/written8**：

```text
OK 1791793987.sav version=12 hero=1 soul=2630135 bag=55 worn=4 -> v14 BOM=yes
OK 1791793987.sav.bak version=12 hero=1 soul=2630135 bag=55 worn=4 -> v14 BOM=yes
OK 1793209553.sav version=12 hero=1 soul=0 bag=0 worn=0 -> v14 BOM=no
OK 1793209553.sav.bak version=12 hero=1 soul=0 bag=0 worn=0 -> v14 BOM=no
OK 1793449001.sav version=12 hero=1 soul=2630135 bag=55 worn=4 -> v14 BOM=no
OK 1793449001.sav.bak version=12 hero=1 soul=100000 bag=35 worn=3 -> v14 BOM=no
OK 1793888918.sav version=12 hero=2 soul=225080 bag=24 worn=3 -> v14 BOM=yes
OK 1793888918.sav.bak version=12 hero=2 soul=225080 bag=24 worn=3 -> v14 BOM=yes
SUMMARY OK=8 SKIP=0 ERROR=0 written=8
BACKUP C:\Users\zheliku\AppData\Roaming\Godot\app_userdata\GameZMXY\GameData.backup-before-v14-20261110_191354
```

完整备份：`C:\Users\zheliku\AppData\Roaming\Godot\app_userdata\GameZMXY\GameData.backup-before-v14-20261110_191354`。正式转存及后续完整哈希证据见原D0节；原正式1～4格数据不因E1改变。

## 双人E1：赠送与技能页签（2026-11-11，E1-1/2自动验证，E1-3待实机）

用户明确回复“允许继续E1，实机验收待补”，仅解除D3→E1推进门槛，D0～D3正式键盘验收继续待补。开工HEAD **c63cd1d**、工作区与索引干净；证据目录为 `Godot/GodotProject/.godot/validation/coop_e1_20261111/`。依据第00/10/50/70章和实施计划第七部分E1-1/2；未改v14、只读框架、源表或生产玩法数值，没有Git写操作。E1-3正式第5格逐关核验及E1上手1～6未执行；用户现明确回复“允许待补，继续E1-4收尾”，据此执行清理与文档收尾，待验项继续保留。

架构：赠送沿用 **InventoryForm → GiftEquipmentCommand → EquipmentSystem → 双方原背包集合**；原实例先加入接收者再从发送者移除，职业不限制赠送，穿戴中/满包/非法参与者拒绝。菜单清除选择后才触发具名事件，余额与物品不复制。技能使用窗口自己的只读 **SkillFormData(HeroId)**；已有Form拥有当前英雄三项订阅，切页顺序为UnbindHero→改编号→BindHero，窗口打开/关闭/失败共用同一绑定路径。同英雄主动/被动切换不重绑；只接受阵容实际席位的技能关闭键。未增加管理器、绑定记录、通用事件或兼容重载。

| 验证 | 命令与实际结果 |
| --- | --- |
| 官方赠送可用态 | `python Tools/LegacyMigration/export_official_inventory_actions.py --java "S:/Unity Editor/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe" --ffdec "%TEMP%/gamezmxy-ffdec/ffdec.jar" --source Godot/GodotProject/.godot/validation/battle_menu_review/official3/common.swf`：**退出0**；脚本核验还原包SHA256 `53722c6c8615d2542de2e3cc255a68c5d80712c6b8097aaecae6bc7be0a40ab2`，只新增sprite367第1帧77×31 PNG，其余5张既有图片逐字节相同。新PNG SHA256 `3c992eb102eb3825075c523c4d38e232c80989b25c634795c08eab9a196470cc`，`art_export.log`；来源登记于LegacyAssetMap |
| E1-1 构建/规则 | `dotnet build Godot/GodotProject --no-restore -v quiet`：**0错误、5既有警告**（`e1_1_build.log`）；`dotnet test Tests/ProfileTests --no-restore -v quiet`：**336/336**（`e1_1_profile.log`），相对327新增9例，覆盖原实例/属性、异职业、穿戴拒绝、满包、非法参与者、全档唯一和存档往返 |
| E1-2 构建 | 同build命令：首轮新增Debug误用不存在的技能枚举，**1错误**（`e1_2_build.log`），改为真实技能后 **0错误、5既有警告**（`e1_2_build_fixed.log`）；后续夹具/截图构建各 **0错误、5既有警告**，最后 `e1_hover_build.log`。子任务最终错误数E1-1=0、E1-2=0；E1-3未执行，E1-4结果见下文收尾补充 |
| 三套规则 | 三条原test命令：Profile首轮新增上下文测试误用结局枚举造成编译失败（`profile.log`），改为既有LevelSystem.Complete后 **339/339**（`profile_fixed.log`，比开工+12）；Battle **197/197**（`battle.log`）；Level **19/19**（`level.log`）。全部最终退出0，失败/跳过0；既有NU1900保留。新增3例验证未开始/结局后/地图不能提交赠送 |
| Godot扫描 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_e1_scan_20261111 --headless --editor --build-solutions --quit --no-window`：**退出0**，生成新SkillFormData的UID及PNG导入信息；既有ComponentInsoector/free退出诊断保留 |
| 引用/格式 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出0，2285路径/393UID**，`references.log`。`dotnet format whitespace Godot/GodotProject/GodotProject.csproj --no-restore --include <本批11份游戏/Debug C#> --verify-no-changes`：**退出0**（`format_game_final.log`）；ProfileTests.csproj同命令明确包含EquipmentTests.cs：**退出0**（`format_profile.log`，工作区加载警告保留） |
| 双人失败与夹具修正 | `coop_e1_coop_20261111`先因点击唐僧第1行却断言玄冰阵失败；改为实际冰龙波后，`coop_e1_coop_fixed_20261111`/`coop_e1_coop_layout_20261111`在被动断言失败，诊断余额3900、实际需5000。夹具改为从配置读取主动+一级被动所需余额，原断言保留；`coop_e1_coop_cost_20261111` **退出0、完整SMOKE PASS**，退出11个ObjectDB/1资源。仅改本批新夹具，无生产修补 |
| 完整双人专项 | 规定Godot参数加 `--smoketest=ui --coop-only`，`coop_e1_coop_final_20261111` **退出0、完整SMOKE PASS**；新增满包提示/接收者格子验证通过。截图补帧后 `coop_e1_coop_verified_20261111` **退出1**：原格原生tooltip遮住菜单，帧内点击未送达；改为先把视口鼠标移到赠送按钮，等2帧消除tooltip再采样/点击。最终 `coop_e1_coop_hover_20261111` **退出0、SMOKE-COOP GIFT/SKILLS/MAP SKILLS PASS、完整SMOKE PASS**。两人主动/被动扣费、反复切页三项来源各1/旧源0、关闭全0、两席位关闭键/池复用、地图默认P1切P2，以及D2/D3全套规则均通过；退出13个ObjectDB/3资源。未弱化任何原断言 |
| 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_e1_frontend_20261111 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：**退出0、完整SMOKE PASS**。地图单人购技及原全部前端规则通过；坏JSON/注册失败为预期注入。退出5个ObjectDB/1资源 |
| 完整单人UI | 规定Godot参数案例 `coop_e1_ui_20261111 --smoketest=ui`：**退出0、完整SMOKE PASS**，保留单人Gift禁用与非阵容英雄页禁用，新增单人P2技能键不能关闭窗口断言；完整地形60/120Hz、战斗/暂停/解绑/池复用/失败/存档重入均通过。退出诊断22个Texture RID、8个ShapedText RID、2个Font RID、144个ObjectDB及11个资源；另记录资源加载桥接的GCHandle未初始化历史诊断（既有C2记录同签名），未改变退出0及完整PASS事实 |
| 正式文件/范围 | 开工快照`formal_before.json`只含根层2个目录文件，最终这2份均不变；已补递归清单`formal_recursive_after.json`，相对D3交付`formal_final.sha256.json` **12/12哈希相同，文件数相同**。无正式存档写入；HEAD保持c63cd1d。索引字节散列由b03b5064…变ba53b3e8…，暂存差异仍为空，保留索引现状，不作Git写命令；散列变化未据此认定暂存内容变化。两种diff --check通过；清理前检查点范围为17份修改+4份新增，共21份；11+1份手写C#及其他修改文本UTF-8/CRLF/无行尾空白通过，`scope_final.log`，两种diff --check退出0 |
| 正式键盘/逐关 | 按computer-use技能使用node_repl/@oai/sky初始化并调用list_windows，仍报 **native pipe不存在（os error 2）**，`computer_use_check.log`；没有真实键盘输入。E1上手1～6与E1-3全部 **未执行**；隔离烟测/截图不替代正式验收 |

本次文件（相对TheGame）：修改 `GameScripts/Systems/EquipmentSystem.cs`、`GameScripts/Commands/EquipmentCommands.cs`、`GameScripts/UI/InventoryForm/Components/EquipmentActionsMenu.cs`、`GameScripts/UI/InventoryForm/InventoryForm.Logic.cs`、`GameScripts/UI/SkillForm/SkillForm.Logic.cs`、`UIs/Inventory/equipment_actions_menu.tscn`；`MainPack/Scripts/Procedure/ProcedureLevel.cs` / `ProcedureMap.cs`；`MainPack/Scripts/Debug/UiSmokeScenario.cs` / `.Inventory.cs` / `.Skills.cs`。新增 `GameScripts/UI/SkillForm/SkillFormData.cs`及Godot生成的`.uid`、`Sprites/UI/inventory/inventory_action_gift.png`及Godot生成的`.import`；仓库另修改 `Tests/ProfileTests/EquipmentTests.cs`、`Tools/LegacyMigration/export_official_inventory_actions.py`、Docs的LegacyAssetMap/WorkingNotes/CoopImplementationPlan/Validation。无删除，未手改UI绑定生成文件。

接口：新增 `EquipmentSystem.TryGift(int fromHeroId,int toHeroId,Guid instanceId):bool` 与 `GiftEquipmentCommand(...)`；`EquipmentActionsMenu.Open(Guid,Rect2,bool)` → `Open(Guid,Rect2,bool,bool canGift)`，新增本组件GiftRequested；技能窗口 `OnOpen(object)`契约由不使用参数改为必须SkillFormData且英雄在阵容中。全部2个生产打开点已显式传参，无其他直接Debug打开点，未留默认P1兼容分支。

最终隔离截图位于 `coop_e1_20261111/screenshots/`：gift_enabled（赠送黄色、异职业装备灰色）、gift_source、gift_recipient（唐僧持有同一件装备）、gift_full（正确中文提示）、p2_skills（默认唐僧）、switched_skills（悟空已学技能）、map_skills；均加前缀`coop_e1_`。已逐张复查赠送可用/接收者/满包及技能双方截图；赠送图含既有保存提示的瞬间，技能图已等待满包提示消失；不是正式键盘上手证据。首轮旧截图渲染早于数值更新，已用最终通过案例的帧等待采样替换，不把旧截图作为验收。

E1-3正式逐关表（保持未执行，不作“可通关”结论）：

| 关卡 | 复现步骤/重点 | 实际现象 | 期望/结论 |
| --- | --- | --- | --- |
| 桃花源、水帘洞、蝙蝠窟 | 第5格双人推进；平台坡地、机关/隐藏入口、双方尖刺 | 未执行，实机连接不可用 | 待正式实战核验；无新问题结论 |
| 七星洞、迦楼罗之穴 | 毒雾命中者、延迟/范围、蛋态第五击归属 | 未执行，同上 | 待正式实战核验 |
| 浅岸、东海、牛魔谷 | 单人吞噬/释放、水牢、多形态经验归属 | 未执行，同上 | 待正式实战核验 |
| 隐藏龙宫 | 冻结命中者、最低等级分档、旗幡全档一次 | 正式未执行；D3及本次隔离专项仅提供转场/分档/旗幡支持证据 | 待正式实战核验，不能据专项宣称全关通过 |

生产 `HeroOf(PlayerSlot.P1)` 当前只剩 `MainPack/Scripts/Procedure/ProcedureMap.cs:203`，语义就是地图打开技能窗口默认P1；测试/Debug中显式P1夹具不属待迁移生产占位。E1-4已在用户明确授权后实施，最终总交付见本节后续补充；下表为本轮C#逐文件行号清单，完整逐行原搜索输出在`remaining_p1.txt`。

| C#文件（仓库相对路径） | 行号 | 保留语义 |
| --- | --- | --- |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/FrontendSmokeScenario.cs` | 313, 366, 367, 491, 492, 523, 541, 542, 567, 625, 626, 679, 714, 722, 1501, 1502, 1539, 1541, 1547, 1549, 1559 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/LevelSmokeScenario.cs` | 107, 108, 402, 415, 719 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/MapProgressSmokeScenario.cs` | 185 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.Bull.cs` | 388, 395, 404, 405, 407, 411, 426, 427, 459 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.CheckpointFailure.cs` | 27, 28, 43 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.CombatFeedback.cs` | 276, 279, 283, 347, 464 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.Dragon.cs` | 228 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.DragonRules.cs` | 50 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.Equipment.cs` | 65, 87, 106, 229, 231, 238, 240, 242, 245, 248 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.FireDemon.cs` | 125, 126, 265, 266 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.Garuda.cs` | 193, 201, 215 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.Inventory.cs` | 106, 107, 110, 134, 135, 139, 159, 160, 163, 177, 192, 202, 208, 215, 226, 233, 235, 237, 238, 249, 265, 287, 299, 300, 315, 316, 330, 350, 373, 386, 393, 404, 410, 419, 446, 447, 459, 476, 477, 479, 480, 587, 588, 591, 619, 653, 669, 678, 681, 683, 685, 691, 692, 695, 696, 697 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.Passives.cs` | 30, 31, 34, 62, 65, 66, 75, 83, 85, 104, 105 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.Skills.cs` | 52, 173, 207, 215, 825, 870, 871, 878, 952, 983, 1089, 1111, 1116, 1120, 1122, 1128, 1133, 1135, 1248 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.Tangseng.cs` | 101, 116, 117, 155, 156, 157, 185, 186, 187 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.Terrain.cs` | 165 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/UiSmokeScenario.cs` | 244, 247, 255, 688, 689, 696, 701, 703, 709, 710, 711, 713, 714, 716, 721, 722, 723, 728, 729, 730, 737, 760, 1290, 1293, 1327, 1328, 1331, 1373, 1649, 1650, 1651, 1653, 1654, 1657, 1658, 1662, 1664, 1666, 1667, 1668, 1669, 1670, 1673, 1674, 1679, 1692, 1863, 1920, 1936, 1996, 1998, 2012, 2024, 2026, 2027, 2153, 2175, 2178, 2181 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Procedure/ProcedureMap.cs` | 203 | 地图技能窗口默认P1 |
| `Tests/ProfileTests/BindablePropertyTests.cs` | 167 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Tests/ProfileTests/BossEquipmentTests.cs` | 28, 30, 36, 38, 42, 47, 54, 56, 70, 71, 74, 75, 113 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Tests/ProfileTests/CommandTests.cs` | 30, 31, 32, 33, 35, 60, 61, 68, 69, 70, 99, 105, 106, 108, 109, 110, 124, 126, 145, 146 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Tests/ProfileTests/DragonPalaceTests.cs` | 82, 89 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Tests/ProfileTests/EquipmentTests.cs` | 309, 313, 314, 320, 321, 363, 366, 374, 376, 437, 438, 467, 468, 471, 472, 492, 495, 496, 505, 515, 519, 520, 538, 540, 541, 543, 544, 563, 568, 569, 572, 573, 660, 676, 677, 695, 701, 725, 726, 729, 730, 732, 736, 737, 740, 741, 760, 761, 762, 763, 766, 767, 770 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Tests/ProfileTests/LevelModelTests.cs` | 122, 139 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Tests/ProfileTests/LevelRewardTests.cs` | 54, 74, 117, 129, 131 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Tests/ProfileTests/ModelTransactionTests.cs` | 122, 232, 261, 262, 266, 287, 302, 310, 311, 316, 332, 336, 349, 362, 363, 382, 383, 384, 388, 394, 399, 400, 401 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Tests/ProfileTests/PassiveSkillTests.cs` | 146, 149, 161, 169, 173, 212, 230 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Tests/ProfileTests/ProfileTests.cs` | 56, 60, 62, 67, 68, 69, 83, 84, 87, 98, 102, 105, 121, 123, 125, 128, 130, 150, 168, 202, 205, 364, 365, 381, 388 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Tests/ProfileTests/QueryTests.cs` | 31, 38, 40, 41, 42, 58, 59, 76, 77, 114, 115, 118, 119, 136 | 隔离单人或显式P1席位的测试/Debug夹具 |
| `Tests/ProfileTests/SkillLearningTests.cs` | 32, 37, 40, 64, 88, 112, 130 | 隔离单人或显式P1席位的测试/Debug夹具 |


### E1-4授权清理与最终回归补充

用户明确回复“允许待补，继续E1-4收尾”，该项已实施；E1-3/E1上手及D0～D3正式键盘项继续未执行。清理前E1-1/2的21文件证据保持为当时检查点；最终本轮相对c63cd1d为 **24份修改+4份新增，共28份文件，无删除**。补充修改SkillSystem.cs、ProfileModel.cs、Tests/ProfileTests的TestGameScope.cs/ProfileTests.cs/SkillLearningTests.cs、Docs的ProjectTour.md及第70章；无Debug消费者使用已删除构造参数，原三个生产/Debug构造点均保持三参。

| 验证 | 命令与实际结果 |
| --- | --- |
| E1-4构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：**退出0、0错误、5既有警告**，`e1_4_build.log`；删除7处空条件及依赖其null语义的早退；保留IsAvailable地图查询与无参总清理的真实生命周期判断 |
| 最终规则 | 三条原test命令 **全部退出0，339/339、197/197、19/19，失败/跳过0**，`profile_final.log` / `battle_final.log` / `level_final.log`。地图购买测试先清技能登记再进入地图，保留无HeroModel购买及存档/重建原断言 |
| 最终扫描/引用 | 既定runner案例`coop_e1_final_scan_20261111 --headless --editor --build-solutions --quit --no-window` **退出0**；`python Tools/ProjectMaintenance/validate_game_references.py` **退出0，2285路径/393UID**（`scan_final.log` / `references_final.log`） |
| 清理后完整前端 | 既定runner案例`coop_e1_cleanup_frontend_20261111 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：**退出0、完整SMOKE PASS**。退出5个ObjectDB/1资源，预期坏JSON/旧作用域/失败注入保持 |
| 清理后双人专项 | 同runner及既定显示参数，案例`coop_e1_cleanup_coop_20261111 --quit-after 30000 -- --smoketest=ui --coop-only`：**退出0、完整SMOKE PASS、Gift/Skills/Map Skills/D3规则全部PASS**，退出11个ObjectDB/1资源；`coop_cleanup.log` |
| 清理后完整UI | 同runner及既定显示参数，案例`coop_e1_cleanup_ui_20261111 --quit-after 30000 -- --smoketest=ui`：**退出0、完整SMOKE PASS**；原单人Gift禁用、P2键忽略、技能/暂停/复用、60/120Hz坡地与单向地形、生命/球、原生墓碑/检查点/取消/失败路径均通过。退出22个Texture RID、8个ShapedText RID、2个Font RID、144个ObjectDB、11资源；本次最终stderr中GCHandle加载桥接诊断 **0条**；清理前E1完整UI出现同类历史诊断的记录仍保留。`ui_final.log` |
| 最终格式/编码/范围 | `dotnet format whitespace <GodotProject.csproj / ProfileTests.csproj> --no-restore --include <本批13份游戏/Debug / 4份测试> --verify-no-changes`：**两者退出0**（`format_cleanup_game.log` / `format_cleanup_profile.log`）。UTF-8/CRLF/无行尾空白、`git diff --check` / `git diff --cached --check`均通过，最终文件清单与索引在`scope_final.log`。正式目录递归仍 **12/12哈希相同** |

## 双人D3：相机、全灭、出口、治疗与等级规则（2026-11-11，自动验证通过，实机待补）

用户现明确要求“好的，继续下一阶段吧”，据此允许暂留D2真实键盘验收进入D3；既有D0/D1/D2待验不记通过。开工HEAD为 **ae2a1ba**（用户已提交D2及Panel重构），工作区/索引干净；正式12份文件SHA256、HEAD、索引与差异快照存入 `Godot/GodotProject/.godot/validation/coop_d3_20261111/`。按计划D3-1～D3-6顺序，沿用第00/10/30/50/70章；不改v14、源表、场景、框架，不执行Git写操作。

| 验证 | 命令与实际结果 |
| --- | --- |
| D3-1 | `dotnet build Godot/GodotProject --no-restore -v quiet`：**0错误、5既有警告**，`d3_1_build.log`。相机Follow改接英雄列表，以存活者X平均取景，无存活者保留中心但更新屏幕墙/抵达；会话传入完整集合 |
| D3-2 | 同build命令 **0错误、5既有警告**，`d3_2_build.log`。删除单一死亡演出枚举，按死亡英雄保存原Action并退订；不再因一死清空掉落，全员死亡且每个原生墓碑均完成后下一帧判负；剩余生命取全员百分比平均、死者0 |
| D3-3 | 同build命令 **0错误、5既有警告**，`d3_3_build.log`。出口/入口沿用D2的任一存活者，原球等待流程保持；隐藏关业务只拒绝全员耗尽。保留无效会话/入口先返回的短路顺序，再读取所有英雄耗尽状态 |
| D3-4 | 同build命令 **0错误、5既有警告**，`d3_4_build.log`。甘露恢复量仍按原MaxMP/8+8及无双倍率；本体200×200、分身自身中心200×160，使用框架已加载实体列表且排除死者 |
| D3-5 | 同build命令 **0错误、5既有警告**，`d3_5_build.log`。龙王取阵容最低等级，地图技能提示按任一英雄灵魂≥160 |
| D3-6 编译 | 同build命令 `d3_6_build.log`、`d3_coop_build.log` 均 **0错误、5既有警告**；在原双人专项后追加甘露边界、相机/屏幕墙、个人死亡/墓碑、全灭与原龙宫路径复用。原单人死亡球断言改为等待下一关卡帧自然回收，再保留原0球/无收益断言，不再要求被删除的同步ClearLoot副作用 |
| 规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：**327/327**，新增3例覆盖P1耗尽/P2耗尽/双人全耗尽的转场；`dotnet test Tests/BattleTests --no-restore -v quiet`：**197/197**；`dotnet test Tests/LevelTests --no-restore -v quiet`：**19/19**，均退出0、失败/跳过0。`profile.log`、`battle.log`、`level.log`，既有NU1900/CS8632/CS0649保留 |
| 双人首轮/夹具 | `coop_d3_coop_20261111 ... --smoketest=ui --coop-only`：**退出1**，甘露边界/分身与相机/屏幕墙已通过，停在活人球领取；新增夹具把球出生设到10000px，改显示位置仍被球自身上升轨迹覆盖，5秒等待不足。只把活人球出生改回正常飞行距离1000px并移除直接改显示位置，原领取/存活/保留物品断言不变。首轮退出8个ObjectDB/1个资源占用；`d3_coop_fixture_build.log` **0错误、5既有警告**，后续复跑结果见下行 |
| 龙宫夹具复用 | `coop_d3_coop_fixture_20261111` **退出1**，一死一活、死者禁开窗、原生墓碑排他、0%失败与重试均通过，停在龙王生成。单人夹具只驱动P2前进，被双人取景/屏幕墙挡在阶段边界前；改为各参战者一起按自己的右移键，`d3_dragon_fixture_build.log` **0错误**。退出8个ObjectDB/1资源 |
| 双人主路径 | `coop_d3_coop_dragon_20261111`、补充同步出口/活人球实际入账断言后的 `coop_d3_coop_exit_20261111`，均为既定Godot命令加 `--smoketest=ui --coop-only`：均 **退出0、SMOKE-COOP RULES PASS、完整SMOKE PASS**。真实P2水下转场、暂停淡出、最低等级龙王、原生死亡/旗幡拾取、P2↑出口、死者计0的平均生命及磁盘检查点通过；两次退出均13个ObjectDB/3资源。普通球仍在时可经正常领取回调完成出口，不能据此覆盖只有死者球的情况 |
| 最后死者球复现 | 同命令案例 `coop_d3_coop_exit_isolated_20261111`：先等正常球全部领取，仅保留一颗归属死者的球后同步提交P2↑，并断言出口先进入等待；最终 **退出1**。原始stderr第1行：`龙宫本人交互出口没有提交结算（outcome=Running, pending=True, orbs=0, slot=P2）`，最后球已被逐球清理回收，但不触发OnLootFinished的出口续行。退出9个ObjectDB/1资源。`d3_exit_isolated_build.log` 为 **0错误、5既有警告**，不把此前主路径PASS代替此失败 |
| 引用/格式/搜索 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出0、2284路径/392UID**（新增烟测读取既有甘露场景的路径，未新增资源），`references.log`。`dotnet format whitespace <GodotProject.csproj / ProfileTests.csproj> --no-restore --include <各自本批明确文件> --verify-no-changes`：**10份游戏/Debug + 1份规则测试均退出0**，测试工作区加载警告保留，`format_game.log` / `format_profile.log`。生产HeroOf(P1)只剩SkillForm的2处（校验/默认页，留E1-2）；相机/龙王/入口/地图原占位已清零 |
| 范围与正式数据 | `git diff --check`、`git diff --cached --check`：均 **退出0**；HEAD保持ae2a1ba、用户索引SHA256不变；正式目录 **12/12文件SHA256相同**，`formal_at_stop.sha256.json` / `scope_at_stop.log`。没有Git写操作或正式文件增删改 |
| 出口授权修复 | 同build命令：**退出0、0错误、5既有警告**，`d3_exit_fixed_build.log`；`dotnet format whitespace Godot/GodotProject/GodotProject.csproj --no-restore --include <本批11份游戏/Debug C#> --verify-no-changes`：**退出0**，`format_game_final.log`。修复后的相同命令案例 `coop_d3_coop_verified_20261111` **退出0、SMOKE-COOP RULES PASS、完整SMOKE PASS**；保留唯一死者球的隔离条件与出口先等待断言，随后作废球回收并正常结算，成绩 **25%**（死者0、存活者50%）。真实P2转场/↑出口、甘露/相机/墓碑/禁开窗/重试断言全部通过。退出13个ObjectDB/3资源 |
| 修复后规则/引用 | 三条原test命令均 **退出0，327/327、197/197、19/19，失败/跳过0**，`profile_final.log` / `battle_final.log` / `level_final.log`；引用命令 **退出0，2284路径/392UID**，`references_final.log` |
| Godot扫描 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d3_scan_20261111 --headless --editor --build-solutions --quit --no-window`：**退出0**，既有ComponentInsoector/free退出诊断保留 |
| 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d3_frontend_20261111 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：**退出0、完整SMOKE PASS**；旧作用域、坏JSON与重复注册为预期注入；退出3个ObjectDB |
| 完整单人 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d3_ui_20261111 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 30000 -- --smoketest=ui`：**退出0、完整SMOKE PASS**。无双原时间轴、60/120Hz地形/单向平台、恢复与球、HUD解绑、窗口/实体复用、装备、取消/失败清理、原生墓碑暂停等待与存档事务重入均通过；坏主档备份回退为预期注入。既有退出诊断 **22个Texture RID、8个ShapedText RID、2个Font RID、144个ObjectDB、11个资源占用** 保留 |
| 最终范围与数据 | 以HEAD取完整差异核对 **16份文件（12份C#、4份Docs）**，无新增/删除；游戏/Debug最终格式 **11份退出0**，规则测试格式 **1份退出0**。全部C#为UTF-8/CRLF、无行尾空白，两种diff --check退出0。正式 **12/12 SHA256相同**，HEAD保持ae2a1ba；执行中外部暂存全部D3修改，用户索引由开工 `3a63636b...` 变为 `6e9f0e47...`，保留该索引，未作Git写操作（`scope_final.log`）。场景、生成物、框架、表格与v14不变 |
| 正式键盘上手/后续 | D3上手 **1～6均未执行**：双人取景/屏幕墙、一死一活、全灭失败、P2出口/平均生命、甘露绿色恢复、任一人余额提示。按computer-use技能在node_repl初始化@oai/sky成功后，`sky.list_windows()`仍报原生管道不存在（os error 2），无游戏输入（`computer_use_check.log`）。D0/D1/D2待验项保持；D3自动通过不能替代实机，D3→E1尚未获得推进例外，E1未开始 |

用户已明确回复“授权统一出口完成检查并继续D3”，现已应用原提案：将 `_exitRequested && _lootOrbs.Count == 0 && Outcome == Running` 的续行判断从 `LevelViewController.Loot.OnLootFinished` 移到 `LevelViewController._Process` 的 `TickLoot` 之后。正常领取和死者作废共用一处完成条件，删除原回调的旧判断；不新增状态、超时、事件或补偿奖励。实施计划D3-3已登记这项授权，保留先前的隔离失败记录。

本次最终修改文件（相对TheGame）：`GameScripts/Entity/Level/Camera/LevelCameraNode.cs`、`GameScripts/Entity/Level/LevelViewController.cs`、`GameScripts/Systems/LevelSystem.cs`、`GameScripts/Entity/Heroes/Tangseng/TangsengEntity.cs`、`GameScripts/Entity/Enemies/Bosses/zm1_11_hidden_dragon_palace/DragonKingEntity.cs`、`GameScripts/UI/MapForm/MapForm.Logic.cs`；`MainPack/Scripts/Debug/UiSmokeScenario.cs`、`.Tangseng.cs`、`.HeroDeath.cs`、`.Dragon.cs`；仓库 `Tests/ProfileTests/DragonPalaceTests.cs` 和Docs的Validation/WorkingNotes/CoopImplementationPlan/ProjectTour。没有新增/删除跟踪文件。用户授权的出口修复另修改 `GameScripts/Entity/Level/LevelViewController.Loot.cs`，仅删除旧续行检查。

自动截图存于 `coop_d3_20261111/screenshots/`，已用修复后完整双人通过案例更新camera/heal/one_dead/defeat/victory。胜利图实际查看为25%，与死者0和存活者50%的平均一致；先前隔离失败日志保留。一死图确认右侧0血与左侧存活；治疗图是冻结位置的数值夹具采样，未拍到绿色恢复数字，不能据此声称该上手画面已验收。均非正式键盘上手截图。

接口变更：`LevelCameraNode.Follow(Node2D)` → `Follow(IReadOnlyList<AbstractHeroEntity>)`；唯一生产调用已同步。LevelViewController死亡结束处理改为内部按英雄的回调，新增字段保存真正订阅的原委托；无新的公共事件/管理器。唐僧新增私有恢复量/范围方法，保留原公开ApplyHeavenlyDew入口。死亡事件本身仍不携带来源，按计划为每名死亡者保存Action，已与D2删除的HUD绑定记录区分职责。本批旧相机目标字段与单一死亡演出枚举已删除；全生产HeroOf(P1)仅SkillForm.Logic.cs:77的身份校验与:86的默认技能页，均按现行计划留E1-2。

## 双人D2复核：已有Panel持有个人HUD绑定（2026-11-11，自动验证通过）

用户在审查席位绑定记录后，要求比较FormData、事件与其他实现方式，并明确授权“嗯可以的，你开始吧，做好记录给架构师看就行”。本次只调整D2 HUD职责，D2实机仍待本机连接恢复，D3/E1未推进。依据第00章的Panel职责、持续状态绑定与局部C#事件、第10章和用户批准的D2-3修订；未修改公共绑定API或Model/System，未新建类型/节点/场景。证据目录：`Godot/GodotProject/.godot/validation/coop_d2_panel_20261111/`；Godot原始日志在 `movement_presets/coop_d2_panel_<案例>_20261111/`，均使用既有案例APPDATA隔离。

方案取舍：将回调搬入FormData或元组只能移动绑定记录；全量刷新仍不能识别一次性无双来源；GF.Event.Fire排队会延后暂停；修改公共绑定接口返回句柄扩大影响面。复用既有Panel，使同一实例的具名方法直接对应自己的状态源，原每人9项订阅覆盖保持。完整HUD子场景虽可表达同一边界，但本批已有上下两块Panel可复用，无需调整场景树。菜单事件继续携带PlayerSlot适配既有Procedure请求；无双事件携带HeroId选择立绘，两者均为窗口内的同步C#通信。

架构调整：

- `BattleForm`删除私有席位记录、`_seats`及五类逐人闭包/刷新方法，直接使用FormData的Heroes处理输入；仍拥有GGF打开/关闭/回池、绑定失败清理、Boss条/连击/Go/全屏无双及流程请求。
- 复用 `BattleStatusPanel.BindHero(int heroId)` / `UnbindHero()`，面板实例观察本人的Hp、MaxHp、Mp、MaxMp和成长；只保存状态/成长来源引用，具名方法对称订退。
- 复用 `BattleMenuPanel.BindHero(int heroId,PlayerSlot slot)` / `UnbindHero()`，观察本人的Ws、MaxWs、WsActive和技能；三个菜单事件由Action改为Action<PlayerSlot>，新增局部同步Action<int,bool> MusouStateChanged。事件在新窗口实例上连接一次，个人来源随OnOpen/OnClose显式订退，不依赖节点退出树。
- FormData继续只携带英雄列表/关卡引用；数值不复制，个人属性仍只从各自权威Model读取。无双开启态只观察变化，重新绑定不重播一次性演出；同步事件保留“第一人立刻暂停、同帧后到者拒绝”的原规则。
- 删除无人再调用的SetHero/SetResources/SetProgression/SetMusou/SetSkills被动入口，ResetMusou收为菜单内部方法，没有平行的新旧路径。场景位置/镜像、美术、生成绑定文件、存档v14均无本次变化。

| 验证 | 命令与实际结果 |
| --- | --- |
| 构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：首次 **3错误、5既有警告**，均为新增Debug断言漏引SkillId/SkillModel命名空间（`build.log`）；补齐后 `build_final.log`、最终 `build_ready.log` 均 **退出0、0错误、5既有警告**。未用兼容入口维持编译 |
| 三套规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`、`dotnet test Tests/BattleTests --no-restore -v quiet`、`dotnet test Tests/LevelTests --no-restore -v quiet`：均 **退出0，324/324、197/197、19/19，失败/跳过0**；`profile.log`、`battle.log`、`level.log`，既有NU1900保留 |
| Godot扫描 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d2_panel_scan_20261111 --headless --editor --build-solutions --quit --no-window`：**退出0**；既有ComponentInsoector/free退出诊断保留。随后仅调整using顺序和XML说明，最终build_ready重新通过 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出0，2283路径、392UID**，`references.log` |
| 格式 | `dotnet format whitespace Godot/GodotProject/GodotProject.csproj --no-restore --include <本次4份手写C#绝对路径> --verify-no-changes`：**退出0**，`format.log`；using/XML调整后的 `format_final.log` 也 **退出0** |
| 双人专项 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d2_panel_coop_20261111 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 30000 -- --smoketest=ui --coop-only`：**退出0、SMOKE-COOP-PANEL PASS、SMOKE-COOP PASS、完整SMOKE PASS**。新增P2同步暂停/正确立绘来源、P1同帧拒绝且仍满值、活动态OnRefocus不重播、P2仍开启时P1使用本人立绘、九项个人来源逐Panel订阅各1次及离关全0；原双人键位/鼠标背包、P2经验与球、会话/HeroSystem退订断言保留。既有退出诊断 **6个ObjectDB** |
| 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d2_panel_frontend_20261111 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：**退出0、完整SMOKE PASS**；新开/读取、菜单、背包/技能、设置保存返回、旧作用域与装配失败清理均通过。坏JSON、旧作用域和重复注册为预期注入；既有退出诊断 **5个ObjectDB、1个资源占用** |
| 完整单人 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d2_panel_ui_20261111 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 30000 -- --smoketest=ui`：**退出0、完整SMOKE PASS**；无双原时间轴、HUD刷新、60/120Hz地形、单向平台、恢复/掉落、窗口/实体复用、升级、加载取消/失败清理、档案事务与重入通过。坏主档备份回退为预期注入；既有退出诊断 **22个Texture RID、8个ShapedText RID、2个Font RID、144个ObjectDB、11个资源占用** 保留，未扩大范围修复 |
| 最终范围/存档 | `git diff --check`、`git diff --cached --check`：均 **退出0**；4份手写C#为UTF-8/CRLF、无行尾空白，生产HUD与对应Debug中旧席位类型/存储 **0命中**。未暂存差异仅本次8份文件。正式目录 **12/12文件SHA256相同**（`formal_before.sha256.json` / `formal_after.sha256.json`），HEAD未变，用户索引SHA256保持 `cc1da49ae041b801b87c92c7f8e2c020a921d618cefddad955960103987ebc17`，`scope_final.log`；没有执行Git写操作 |
| 未完成/推进边界 | D2正式键盘上手1～8仍 **未执行**，不能用自动输入/截图替代；用户已明确选择“先停在D2，等待恢复实机验收”。本次授权只完成D2设计调整，D3/E1未开始。正式存档v14及D2原定相机/死亡/结算临时规则不变 |

本轮双人运行图已复制到 `coop_d2_panel_20261111/screenshots/`：`coop_d2_spawn_hud.png`、`coop_d2_p2_inventory.png`、`coop_d2_p2_kill.png`、`coop_d2_panel_p2_musou.png`。已查看P2背包归属与P2经验图；无双图取调用后的首帧，该帧尚未显示全屏大立绘，不能以此图声称中段演出已目视验收。演出来源、立即暂停/排他与结束恢复由本轮运行断言证明，完整动画/真实键盘验收仍分别以既有UI烟测和待补上手记录为准。

生产HUD三份手写代码合计 **781→761行（净减20行）**：Form **597→490**，StatusPanel **47→79**，MenuPanel **137→192**；删除一份八字段记录与五类逐人闭包，新增五个借用状态/来源/席位字段，回调目标改为已有节点实例。行数只描述改动，不作为正确性证据。

本次实际修改4份C#：`GameScripts/UI/BattleForm/BattleForm.Logic.cs`、`Components/BattleStatusPanel.cs`、`Components/BattleMenuPanel.cs`、`MainPack/Scripts/Debug/UiSmokeScenario.cs`（相对TheGame）；文档 `Docs/CoopImplementationPlan.md`、`WorkingNotes.md`、本记录，ProjectTour已同步最终职责说明。开工快照、索引哈希、4份源码原文及12份正式文件哈希均保留在证据目录；执行者未作Git写操作，不关闭用户编辑器。无新增/删除跟踪文件，无本次场景、生成文件、只读目录、配置表或正式存档修改。

## 双人D2：双英雄装配与双 HUD（自动验证通过，实机待补，2026-11-10）

用户明确回复“允许继续 D2，实机验收待补”，因此解除 D1→D2 的真实键盘推进门槛；D0/D1 待验项保持，不能记为通过。依据第 00/10/30/40/50/70 章与计划 D2-1→D2-4 实施，未开始 D3。开工 HEAD **0307f36**，用户已暂存 D1 的25份修改，无未暂存差异；完整快照在 `.godot/validation/coop_d2_20261110/`，不作 Git 写操作。检测到用户运行 Godot 编辑器（`--editor`），不关闭其窗口；独立预览和烟测仍使用案例专属 APPDATA。

| 验证 | 命令与实际结果 |
| --- | --- |
| D2-1 | `dotnet build Godot/GodotProject --no-restore -v quiet`：**2错误、5既有警告**，均为计划后续 BattleFormData/LevelViewController 尚未接收列表，`d2_1_build.log`。ProcedureLevel 预载全部英雄，按席位依次显示、P2偏移60px，每次显示后核对进入周期；成功立即登记。额外保留本周期已显示的实体编号，在旧请求晚到时按旧编号回收，避免共享引用已经被新周期复用时误隐藏；不新建管理器/接口 |
| D2-2 | 会话改为借用英雄集合，逐人注册/退订装备与治疗委托，奖励与共享连击接收任一参战者，地面物品按席位领取且成功即停止，入口对双方关闭控制/淡出；仍保持相机P1、首人死亡判负和P1生命比例。编辑替换脚本首次因非唯一匹配中止、未写入该步文件，构建仍2错误（`d2_2_build.log`）；完成后同命令 **1错误、5既有警告**（仅BattleFormData待D2-3），`d2_2_build_final.log` |
| D2-3 绑定生成 | 先在正式 `battle_form.tscn` 复制 StatusPanel/MenuPanel 并镜像到824,44与850,531，按规格翻回数字、头像、状态文字、技能图标与按钮标题，Control使用尺寸中心pivot；根新增 `_statusP2/_menuP2` 后执行 `python Tools/ProjectMaintenance/regenerate_ui_form.py Godot/GodotProject/TheGame/UIs/battle_form.tscn --namespace GameLogic.UI`：**8个绑定**；未手改生成文件 |
| 绑定前普通态预览 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d2_mirror_preview_20261110 --rendering-method gl_compatibility --audio-driver Dummy --script res://.godot/validation/coop_d2_20261110/preview_hud.gd`：**退出0、截图结果0**。预览场景逐字复制正式节点/资源/变换，仅去除根窗口生命周期脚本与根导出属性，使用上次可用程序集的被动子控件显示固定数值；只证明布局，不代替本批生产装配验证。`hud_mirror_before_binding.png` |
| 绑定前满值预览 | 同命令案例 `coop_d2_mirror_full_preview_20261110`、脚本 `preview_hud_full.gd`，两侧无双置满：**退出0、截图结果0**；`hud_mirror_full_before_binding.png`。实际查看两张预览：数字/头像/技能图标正向、资源条镜像；但P2“无双”文字反向 |
| D2-3 规格遗漏上报 | 普通态 `MenuPanelP2/WsGrey` 与满值态 `MenuPanelP2/m_WsMax` 的图像本身含“无双”文字，计划列出的翻回名单未包含它们；照规格镜像后文字反向。这两个节点 `centered=false`，不能只翻scale后任其横向移位。已请求授权将两节点翻回，并按纹理宽度保持原占位；贴图/条体填充方向保持。用户随后明确回复“授权补翻回这两个节点”，见下行修复与复查结果。 |
| 裁决前尺寸核对（历史） | 只读检查：WsGrey贴图 **68×68**；m_WsMax的 **19帧全部158×158**，两节点均centered=false。补翻回若使用scale.x=-1配合原生offset.x=-68/-158，可保持原包围盒，P2条体方向保持；仅核对并记录 `mirror_text_fix_bounds.txt`，尚未修改生产节点或继续HUD绑定 |
| 补翻回后复查（授权） | P2两个节点使用scale.x=-1与offset.x=-68/-158，保持原占位、贴图和条体填充方向。`coop_d2_mirror_corrected_20261110` / `coop_d2_mirror_full_corrected_20261110`，对应 `preview_hud_corrected.gd` / `preview_hud_full_corrected.gd`：均 **退出0、截图结果0**，实际查看 `hud_mirror_corrected_before_binding.png`、`hud_mirror_full_corrected_before_binding.png` 确认“无双”正向，再接HUD数据绑定 |
| D2-3 逻辑 | BattleFormData改借用1～2英雄列表，校验null/人数/空元素；SeatBinding逐席位保存七项本局属性、成长与技能委托，P2单人隐藏，快捷键按请求者控制状态检查，菜单请求带席位，共享连击/对手只订阅一次。同build命令 **9错误、5既有警告**，均为D2-4旧Debug参数/属性待迁移，`d2_3_build.log` |
| D2-4 清零 | 全部BattleFormData调用传列表、Frontend旧Hero断言改核对列表身份，共用AdvanceToLevel按coop-only选双人并依次选两卡；UiSmokeScenario新增真实双人建档进花果山专项。同build命令 **0错误、5既有警告**，`d2_4_build.log`；四步结束错误数 **2/1/9/0** |
| 规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`、`dotnet test Tests/BattleTests --no-restore -v quiet`、`dotnet test Tests/LevelTests --no-restore -v quiet`：均 **退出0，324/324、197/197、19/19，失败/跳过0**；`d2_profile.log`、`d2_battle.log`、`d2_level.log`。既有NU1900、CS8632/CS0649保留 |
| 扫描/引用 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d2_scan_20261110 --headless --editor --build-solutions --quit --no-window`：**退出0**，既有ComponentInsoector/free退出诊断保留；`python Tools/ProjectMaintenance/validate_game_references.py`：**退出0，2283路径、392UID**，`d2_references.log` |
| 双人首轮及命中诊断 | `coop_d2_coop_20261110 ... --smoketest=ui --coop-only` **退出1**，停在P2镜像HUD背包按钮；`coop_d2_coop_probe_20261110`复现，记录GetGlobalRect=(937,529),(47,-40)，其中心落到视口外，请求数0，而真正全局变换X=-1、Y=1。修正现有PushMouseClick夹具为 `GetGlobalTransformWithCanvas() * (Size/2)`，适配负缩放；原鼠标点击断言保持，未改生产按钮。两次退出均5个ObjectDB、1资源占用，日志不删 |
| 双人导航时序 | `coop_d2_coop_final_20261110` **退出1**，P2按钮与击杀归属已通过，保存返回地图未触发。新设置窗口可见先于所属流程导航回调绑定，夹具等既有MapRequested回调为1后才点返回，保持真实点击；退出8个ObjectDB、1资源占用。两次夹具修正后build均0错误，`d2_coop_input_build.log`、`d2_coop_ready_build.log` |
| 双人最终专项 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d2_coop_verified_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 30000 -- --smoketest=ui --coop-only`：**退出0、SMOKE-COOP PASS、完整SMOKE PASS**。真实双人新档/阵容、两实体与60px间距、左右HUD头像/可读方向/独立受伤、同时移动、P1禁止控制时P2背包键、双方背包归属、P2镜像按钮、P2致死一击经验与球归属、双方成长/穿戴/HUD原委托离关后均0订阅通过。退出13个ObjectDB、3资源占用；保存结果和原日志保留 |
| 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d2_frontend_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：**退出0、完整SMOKE PASS**；新开/读取、双人菜单、覆盖取消/确认、技能、设置保存返回与原流程断言通过。坏JSON、旧作用域与重复注册为预期注入；既有退出诊断 **5个ObjectDB、1个资源占用** |
| 完整单人 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d2_ui_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 30000 -- --smoketest=ui`：**退出0、完整SMOKE PASS**；HUD刷新/退订、窗口/实体复用、无双、60/120Hz地形、原生单向平台、恢复/掉落、加载取消、失败清理与存档重入通过。坏主档备份回退为预期注入；既有退出诊断 **22个Texture RID、8个ShapedText RID、2个Font RID、142个ObjectDB、10个资源占用** |
| 最终格式与搜索 | `dotnet format whitespace Godot/GodotProject/GodotProject.csproj --no-restore --include <本批9份手写C#绝对路径> --verify-no-changes`：**退出0**，`d2_format_final.log`；生成UI文件排除。`rg -n '\b_player\b' .../Entity/Level --glob 'LevelViewController*.cs'` 与 `rg -n '\b_hero\b' .../ProcedureLevel.cs`：均 **0命中（rg退出1）**，旧单人字段清零 |
| 正式存档与范围 | 以D2开工 `formal_before.sha256.json` 对比 `formal_final.sha256.json`：**12/12文件SHA256完全相同**，不是10份；没有新增/删除/覆盖正式文件。HEAD保持0307f36，索引SHA256保持 `be910dc22f36397c3183c979db326ee628afe607ec30c55ee5b5570cd5d6bf56`；执行者未作Git写操作，保留用户已暂存D1。差异/编码检查见 `d2_scope_final.log` |
| 正式键盘上手 | D2上手 **1～8均未执行**：出生站立、左右HUD、同时移动跳攻、双方技能扣蓝、经验/球/地面物品、两套背包键与P2按钮、P2无双、单人档。computer-use本机管道仍不可用（此前重查os error 2，用户已说明暂无法恢复），自动输入及烟测截图不替代真实键盘验收；D2→D3未获推进例外，D3未开始 |

本批实际修改15份现有文件，无新增/删除跟踪文件。相对 `Godot/GodotProject/TheGame/`：`MainPack/Scripts/Procedure/ProcedureLevel.cs`；`GameScripts/Entity/Level/LevelViewController.cs`、`LevelViewController.Loot.cs`、`LevelViewController.Entrance.cs`；`UIs/battle_form.tscn`；生成的 `GameScripts/UI/BattleForm/BattleForm.cs`；`GameScripts/UI/BattleForm/BattleFormData.cs`、`BattleForm.Logic.cs`；`MainPack/Scripts/Debug/UiSmokeScenario.cs`、`UiSmokeScenario.Inventory.cs`、`FrontendSmokeScenario.cs`。文档：`Docs/Validation.md`、`WorkingNotes.md`、`CoopImplementationPlan.md`、`ProjectTour.md`。没有改v14、源表、框架或正式存档；预览脚本/场景/截图仅在忽略的 `.godot/validation/coop_d2_20261110/`。

关键接口：`LevelViewController.StartSession(EntityComponent,AbstractHeroEntity,CancellationToken)` → `StartSession(EntityComponent,IReadOnlyList<AbstractHeroEntity>,CancellationToken)`；`BattleFormData(AbstractHeroEntity,LevelViewController)` → `BattleFormData(IReadOnlyList<AbstractHeroEntity>,LevelViewController)`，删除 `Hero`、新增 `Heroes`。ProcedureLevel `_hero` → `_heroes`，新增私有 `HeroAt(PlayerSlot)` / `SpawnPositionOf(PlayerSlot)`；LevelViewController `_player` → `_heroes` / `_heroHandlers`，删除单人治疗/换装回调；BattleForm刷新回调接收SeatBinding，菜单处理接收PlayerSlot。Debug的8处BattleFormData调用及旧标量断言机械迁移；TestArenaController原无相关调用，未修改。计划外处理为用户授权的两处无双文字翻回，以及镜像鼠标命中/窗口回调时序夹具修正，生产玩法与原断言保持。

实际双人截图：`coop_d2_20261110/screenshots/coop_d2_spawn_hud.png`、`coop_d2_p2_inventory.png`、`coop_d2_p2_kill.png`，均已查看。出生图在数值夹具中冻结了半空英雄并含保存提示，只证明运行时装配与HUD，不证明真实键盘站立验收。绑定前布局普通/满值修正图分别为 `hud_mirror_corrected_before_binding.png`、`hud_mirror_full_corrected_before_binding.png`。

遗留与D3前置条件：D0/D1正式键盘验收按用户例外待补；D2自动验证已通过，但必须补完D2上手1～8，或由用户明确授权暂留并进入D3。当前有意保留相机跟P1、首人死亡判负/清掉落、剩余生命按P1计；D3统一修正。技能窗口参数和参战者页签按现行第七部分留E1-2，D2不提前新增参数。

## 双人D1：新开/读取、依次选角与选格建档（2026-11-10）

用户明确授权 D0 真实键盘验收因本机 computer-use 管道缺失暂留，先实施 D1。本批按 D1-1→D1-5 顺序实现，仍为 v14；双人新档此时只生成 P1 战斗实体，D2 尚未实施。依据项目第 00/10/40/50/70 章、实施计划第一/二/七部分 D1 和 Stop Ladder，复用现有 GGF 窗口、目录模型、删除接口与确认场景；未新建管理器、覆写通道或兼容重载。

证据目录：`Godot/GodotProject/.godot/validation/coop_d1_20261110/`。Godot 运行日志在 `.godot/validation/movement_presets/coop_d1_<案例>_20261110/`；案例专属 APPDATA 隔离正式存档。开工快照 HEAD 为 **0307f36**，工作区/索引为空；执行期间用户暂存本批文件，执行者没有运行 Git 写命令，继续保留用户索引。

| 验证 | 命令与实际结果 |
| --- | --- |
| D1-1 阵容接口 | `dotnet build Godot/GodotProject --no-restore -v quiet`：迁移途中 **2 个 CS1503**（未补齐 Debug 调用）保留于 `d1_1_build.log`；补齐后 **0 错误、5 条既有警告**，`d1_1_build_final.log`。读档传 `[]`，建档传有序阵容；已有档忽略请求阵容，默认新档保留配置英雄 |
| D1-2 标题入口 | 场景根先改绑定，再运行 `python Tools/ProjectMaintenance/regenerate_ui_form.py Godot/GodotProject/TheGame/UIs/start_form.tscn --namespace GameLogic.UI`：生成 **11 个绑定**。同构建命令 **2 错误、5 既有警告**，均为计划 D1-4 尚未创建的 SaveFormData/SaveFormMode，`d1_2_build.log` |
| D1-3 依次选角 | 同生成命令替换为 `.../character_form.tscn`：生成 **6 个绑定**。双人锁定已选卡、标记按席位定位、重选/返回；保存页由 ProcedureCharacter 持有。同构建命令 **8 错误、5 既有警告**，均为 D1-4 的类型、事件与回调待迁移，`d1_3_build.log` |
| D1-4 读取/保存 | 新增用途参数，读取空格禁用，保存隐藏删除、覆盖复用确认层；确认后 DeleteSlotAsync→刷新格→建档。关闭回调可能同步离开流程，使用原窗口编号判断是否仍需关闭，避免重复关闭。同构建命令 **0 错误、5 既有警告**，`d1_4_build.log` |
| D1-5 烟测同步 | 同构建命令首轮 **1 个 CS1061**（摘要仅有名称/等级，断言误取 HeroId），改为按名称核对摘要、按真实档案核对编号；最终 **0 错误、5 既有警告**，`d1_5_build.log`/`d1_5_build_final.log`。五步结束错误数 **0 / 2 / 8 / 0 / 0** |
| 规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`、`dotnet test Tests/BattleTests --no-restore -v quiet`、`dotnet test Tests/LevelTests --no-restore -v quiet`：均退出 0，**324/324、197/197、19/19**，失败/跳过均 0；`d1_profile.log`、`d1_battle.log`、`d1_level.log`。既有 NU1900 保留 |
| Godot 扫描 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d1_scan_20261110 --headless --editor --build-solutions --quit --no-window`：**退出 0**，新脚本、场景与 UI 绑定可加载；ComponentInsoector/free 既有退出诊断保留 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出 0，2283 条序列化路径、392 个 UID**；`d1_references.log` |
| 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d1_frontend_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：**退出 0、完整 SMOKE PASS、SMOKE-COOP-MENU PASS**。新增人数菜单返回、双人重复/重选/返回、保存关闭无写入、取消覆盖逐字节保持、确认覆盖建双方阵容、磁盘摘要及读取空格禁用；原技能、设置、音乐、重读、取消/装配失败清理断言保持。坏 JSON、旧作用域拒绝、重复注册为预期注入；退出 **3 个 ObjectDB** |
| 删除专项 | 同 Godot 命令案例改 `coop_d1_delete_20261110`，尾加 `--save-delete-only`：**退出 0、SMOKE-SAVE-ROSTER PASS、SMOKE-SAVE-DELETE PASS、完整 SMOKE PASS**。两种阵容顺序、最大等级摘要、删除确认/取消、主备清除、其余格不变、同格重建唐僧及迟到确认保持；退出 **5 个 ObjectDB、2 个资源占用** |
| 共用进关修正 | 首轮完整 UI `coop_d1_ui_20261110` 停在 Character：快驱动在流程回调挂载前选择，选卡状态已满后重复点击被拒绝。主动停止本轮（子进程退出 4294967295），**无 SMOKE PASS**。AdvanceToLevel 与 MapProgress 的独立按钮表改为等现有 CharacterConfirmed 回调再点卡；没有给生产代码增加观测入口。`d1_smoke_entry_build.log` 为 0 错误 |
| 无双既有夹具失败/授权 | `coop_d1_ui_final_20261110 ... --quit-after 30000 -- --smoketest=ui`：**退出 1**，UiSmokeScenario.cs 原第 1151 行“无双大立绘提前结束或时间轴与官方不一致”，**无完整 SMOKE PASS**；退出 **38 个 CanvasItem RID、21 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、140 个 ObjectDB、11 个资源占用**。用户随后明确授权定位并修复夹具，生产动画与原断言保持 |
| 无双诊断与修复 | `coop_d1_ui_probe_20261110` 仅增加计时日志：输入后 163ms 帧3，中段等待后 613ms 帧14，截图完成 736ms，尾段观察 796ms 帧18（该次原断言通过），证明同步截图实际占用 **123ms**；原夹具把固定 0.45 秒等待、截图编码/写盘插在仅一秒的演出观察中。采样完成后主动停止诊断运行（退出 4294967295），不记为完整通过。仅改 CheckMusouActivationAsync：按原生帧等待中段、先取得图像、PNG 写盘延后到演出结束；后半段可见、24帧/24fps、暂停恢复与隐藏原断言完整保留。`d1_ui_fixture_build.log`：**0 错误、5 既有警告** |
| 完整 UI 最终 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d1_ui_verified_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 30000 -- --smoketest=ui`：**退出 0、完整 SMOKE PASS**；无双原失败点、60/120Hz 地形、恢复/掉落、窗口与实体复用、取消/失败清理、存档重入通过。坏主档回退为预期注入；既有退出诊断 **38 个 CanvasItem RID、22 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、143 个 ObjectDB、11 个资源占用** 保留 |
| 最终前端画面 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d1_frontend_final_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：**退出 0、SMOKE-COOP-MENU PASS、完整 SMOKE PASS**，退出 **2 个 ObjectDB**。新增帮助/须知采样、等待保存提示结束后再采无遮挡菜单证据；共 **17 张** 当前截图已复制到 `coop_d1_20261110/screenshots/`，实际查看标题、人数、保存/覆盖、双人摘要及无双尾段。发现规格原帮助三行超出 254px，右侧裁切，`screenshots/start_help.png`；当时已上报，用户随后裁决保留完整原文、调整显示方式；原裁切截图保留为 `screenshots/start_help_before_scroll.png` |
| 最终规则失败边界 | 最终三条 test 命令退出结果：Profile **323/324，1 失败**（`Archive_RejectsMissingRequiredFields` 第292行未抛异常），Battle **197/197**，Level **19/19**。`d1_final_profile.log`、`d1_final_battle.log`、`d1_final_level.log`；静态比较本批未改该测试/DTO。单独 `--filter FullyQualifiedName~Archive_RejectsMissingRequiredFields -v normal` **1/1 通过**（`d1_profile_failure_detail.log`），完整 `-v normal` 复跑仍 **323/324**（`d1_final_profile_detail.log`），不能用单例通过或先前324代替全套最终结果。已按计划第一部分第2节第7条上报，请求定位/修复隔离授权 |
| Profile 隔离修复（用户授权） | 只修改 `Tests/ProfileTests/ProfileTests.cs` 的 `Archive_RejectsMissingRequiredFields`：为原两条异常断言使用独立 `DefaultContractResolver` / `JsonSerializerSettings`，输入与异常类型不变；生产 DTO、校验、依赖及整套并行设置保持。Newtonsoft 13.0.4 源码 `JsonObjectContract.HasRequiredOrDefaultValueProperties` 的共享惰性标记先设 false 再扫描必填字段，该竞态与单例通过/全套偶发失败一致；源代码证据在 D1 目录。实际两次完整 `dotnet test Tests/ProfileTests --no-restore -v normal` / `-v quiet` 均 **退出0、324/324、失败/跳过0**，`d1_profile_isolated.log`、`d1_profile_isolated_final.log`；NU1900 保留。原323/324与复现日志不删除 |
| 帮助显示修复（用户裁决） | 原文、19px、原254×100区域保持；使用 Godot 原生 `StatusScroll` 包裹 `m_Status`，Label自动换行并可滚动。帮助/须知切换回顶部；根 `_status` 改为 `StatusScroll/m_Status` 后再运行既定生成器，仍 **11个绑定**。未加新窗口/脚本或美术。构建 `d1_help_final_build.log` / `d1_help_input_build.log`：均 **0错误、5既有警告** |
| 帮助扫描/引用 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d1_help_scan_20261110 --headless --editor --build-solutions --quit --no-window`：**退出0**，既有 ComponentInsoector/free退出诊断保留；`python Tools/ProjectMaintenance/validate_game_references.py`：**退出0，2283路径、392UID**，`d1_help_references.log` |
| 帮助首轮回归 | `coop_d1_help_frontend_20261110 ... --smoketest=frontend`：帮助原文/字号/换行与鼠标滚动末尾断言通过，但后续须知按钮未发点击，**退出1、无完整SMOKE PASS**。本批新滚轮夹具只发送按下，未配对松开；已补齐每个滚轮事件的松开，没有改动生产点击逻辑。退出2个ObjectDB、1资源占用；格式检查发现该夹具3处初始化换行，已修正 |
| 帮助截图等待修正 | `coop_d1_help_frontend_final_20261110` 帮助原文/字号/滚轮及后续须知、选角/建档均通过，但原 CaptureAsync 在地图静止反馈截图等待 FramePostDraw，原生进程仍存活而日志与截图连续数分钟不更新。主动停止（退出4294967295），**无完整SMOKE PASS**。复用现有 CaptureCombatAsync 的 `RenderingServer.ForceDraw(false)`，主动绘制后取图，保持原截图内容与其余断言；`d1_capture_build.log`：0错误、5既有警告 |
| 帮助最终前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_d1_help_frontend_verified_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：**退出0、SMOKE-COOP-MENU PASS、完整SMOKE PASS**；实际视口鼠标滚轮到末尾、完整原文/19px/换行、后续须知点击/设置、单/双人新建与覆盖/取消/重读全通过。退出 **4个ObjectDB、1个资源占用**；预期故障注入保留。帮助顶部/底部、须知及其余菜单截图已复制到 D1 的 `screenshots/`，共19张（含原裁切对照） |
| 追加格式 | `dotnet format whitespace Godot/GodotProject/GodotProject.csproj --no-restore --include <FrontendSmokeScenario.cs、StartForm.Logic.cs> --verify-no-changes` 与 `dotnet format whitespace Tests/ProfileTests/ProfileTests.csproj --no-restore --include Tests/ProfileTests/ProfileTests.cs --verify-no-changes`：均 **退出0**；后者工作区加载警告保留，`d1_help_format_final.log`、`d1_profile_format.log` |
| 格式/范围 | `dotnet format whitespace Godot/GodotProject/GodotProject.csproj --no-restore --include <本批手写文件> --verify-no-changes`：退出 0（生成 UI 文件排除）；`d1_format_check.log`。正式目录 **10/10 文件 SHA256 完全相同**（含 Catalogue/备份），`formal_before.sha256.json`、`formal_after.sha256.json`、`d1_scope.log`。未改正式第 1～4 格 |
| 本机连接复查 | 按 computer-use 技能从 node_repl 初始化 `@oai/sky` 成功，`sky.list_windows()` 仍返回 **native pipe 不存在（os error 2）**，未启动游戏、未发送输入、未写正式存档；`d1_manual_connection_recheck.log`。D1→D2 的暂留验收例外请求仍待用户明确答复，未把自动续行视为同意 |
| 正式上手 | D1 上手步骤 1～8 **未执行**：computer-use 本机原生管道仍不可用，用户告知暂时无法恢复。隔离引擎截图不能替代正式键盘验收；没有创建正式第 5/6 格。D0 待验项保持；D2～E1 未开始 |

实际文件（路径相对 `Godot/GodotProject/TheGame/`，无删除）：

- `MainPack/Scripts/Procedure/FlowData/SaveSelectionVariable.cs`、`ProcedureTitle.cs`、`ProcedureCharacter.cs`、`ProcedureLoadGame.cs`：阵容传递、分离新开/读取入口，选角流程持有保存页与取消令牌。
- `GameScripts/Systems/SaveSystem.cs`：建档接收有序阵容，复用原 mapper 校验与写入通道。
- `GameScripts/UI/StartForm/StartForm.Logic.cs`、`CharacterForm/CharacterForm.Logic.cs`、`SaveForm/SaveForm.Logic.cs`；新增 `CharacterForm/CharacterFormData.cs`、`SaveForm/SaveFormData.cs` 及 Godot 扫描产生的各自 `.uid`。
- `UIs/start_form.tscn`、`UIs/character_form.tscn`；只经规定生成器更新 `GameScripts/UI/StartForm/StartForm.cs`、`CharacterForm/CharacterForm.cs`。
- `MainPack/Scripts/Debug/FrontendSmokeScenario.cs`、`MapProgressSmokeScenario.cs`：新菜单路径和新增覆盖、共用入口订阅时序；`LevelSmokeScenario.cs`、`UiSmokeScenario.cs`：重读显式传 `[]`；后者另含用户授权的无双截图夹具修复。
- 用户追加授权修改 `Tests/ProfileTests/ProfileTests.cs`，仅隔离必填字段失败用例的 JSON 契约缓存，原两条断言与其他测试并行保持。
- 仓库 `Docs/Validation.md`、`WorkingNotes.md`、`ProjectTour.md`、`CoopImplementationPlan.md`：当前状态、接口、命令、例外与待验项。

关键接口前后对照：

| 接口 | 原 | 现 |
| --- | --- | --- |
| 流程选择 | `(SlotNumber, UnitId, HeroId)`；`Create(int slotNumber,long unitId,int heroId=0)` | `(int SlotNumber, long UnitId, int[] HeroIds)`；`Create(int slotNumber, long unitId, int[] heroIds)` |
| 建档/读档 | `LoadOrCreateAsync(int,long,CancellationToken=default,int initialHeroId=0)` | `LoadOrCreateAsync(int,long,IReadOnlyList<int> initialHeroIds,CancellationToken=default)` |
| 标题事件 | `StartGameRequested: Action<int>` | `NewGameRequested: Action<int,int>`、`LoadGameRequested: Action<int>` |
| 选角事件/参数 | `CharacterConfirmed: Action<int,int>`，无打开参数 | `Action<int,int[]>`，新增 `CharacterCancelled: Action<int>`、`CharacterFormData(int playerCount)` |
| 存档参数/关闭 | 无模式或关闭通知 | `SaveFormData(SaveFormMode.Load/Save)`、`SaveFormClosed: Action<int>`；选格/删除事件签名不变 |

本批规格细节：人数选项初始隐藏由 `m_PlayerCountMenu.Visible=false` 统一负责，子按钮保持可见，使计划要求的“仅切换两个容器”能正常显示；没有再维护逐按钮显隐状态。保存关闭的同步回调按 GGF 实际关闭生命周期处理。未新增源表、框架修改、存档版本或兼容接口；方法与回调订阅对称。

上手状态与遗留：正式 D1 步骤 1～8 全部待执行；D2 的双实体、双 HUD 尚未开始。用户对 D0→D1 的推进例外不等于 D1→D2 的验收授权。完整 UI 与最终前端均已通过；用户已裁决保留帮助原文并授权修复 Profile 测试隔离，修复后 Profile 两轮324/324；帮助最终完整前端也退出0、SMOKE PASS。当前有效规则为324/197/19；此前完整UI证据对本轮仅标题/测试隔离修改仍有效。正式键盘验收仍未完成，尚不能判定 D1 全部完成。

## 双人D0：正式转存完成，上手验证受阻（2026-11-10）

用户已在 D0-3 明确回复“确认”。核对正式目录与 dry-run 时的 10 文件哈希一致后，工具完成整目录备份和 8 个主/备文件转存，**正式存档已为 v14；上手验证因 computer-use 本机管道缺失未执行，D0 尚未完成，D1～E1 未开始**。运行时格式和代码均不变，按计划先完成 D0 上手验收再推进。

依据为项目规范第 00/10/50/70 章、实施计划第一/二/七部分 D0 与正式存档保护规则。复用标准库、既有 GameData/SaveDataUtility 和 TestGameScope，不添加运行时兼容分支。快照与命令输出位于 `.godot/validation/coop_d0_20261110/`。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工状态与基线 | HEAD **0523aae**；用户已暂存 CoopImplementationPlan/WorkingNotes 两份修改，无未暂存差异。本轮无 Git 写操作。`dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误、0 警告（增量）；三条 `dotnet test Tests/<ProfileTests/BattleTests/LevelTests> --no-restore -v quiet`：**323/197/19**，失败/跳过均 0；`baseline_*.log` |
| D0-1 | 新建 `Tools/ProjectMaintenance/upgrade_saves_v12_to_v14.py`，仅 Python 标准库。先规划全部主档与 .bak，任一校验失败全批不写；13/14 跳过；v12 拆个人钱包/背包，保留实例池顺序、其余字段值、BOM 和 CRLF，正式写前整目录备份，逐文件 tmp+os.replace，Catalogue 不改。子任务后同构建命令：**0 错误、0 警告（增量）**，`d0_1_build.log` |
| 工具边界检查 | 临时目录运行 10 组实际 CLI 检查：BOM/无 BOM 主备对、dry-run、未知版本、零英雄/双英雄、身份不符、穿戴缺失、实例重复、超容量、坏 JSON。有效输出与 13/14 跳过符合预期；8 组拒绝均非零退出，连同前面的有效文件及目录均不写、不建备份；正常执行的完整备份逐字节一致，Catalogue 原字节保持，JSON 根/英雄键序、装备值、BOM、CRLF 通过；`tool_checks.log` |
| D0-2 样例生成 | 只读复制正式格 4 为 `Tests/ProfileTests/Data/save_v12_tangseng.json`；在 `.godot/validation/coop_d0_20261110/sample_upgrade/GameData/` 实际运行 `python Tools/ProjectMaintenance/upgrade_saves_v12_to_v14.py --game-data <该临时目录> --capacity 75`：**退出 0，1 文件转换**，再把实际输出复制为 `save_v14_tangseng.json`，225080 灵魂、24 背包、3 穿戴；临时目录备份路径见 `sample_upgrade.log`，不是正式目录备份 |
| D0-2 正式规则 | 新增 SaveUpgradeTests 的一个用例，经正式校验/恢复与 CaptureSaveData 逐项比较成长、钱包、背包、穿戴、技能、被动、共享进度及实例编号/装备属性，再核对旧钱包与件数推导。首次测试编译 **1 个 CS0121 重载歧义**，显式声明比较数组后修正，`d0_2_profile.log` 保留失败。`dotnet test Tests/ProfileTests --no-restore -v quiet`：**324/324**，失败/跳过 0，`d0_2_profile_final.log`；子任务游戏构建 **0 错误、0 警告（增量）**，`d0_2_build.log` |
| 确认前最终回归 | 重跑上述 build 与三条 test：均退出 0，构建 **0 错误、0 警告（增量）**，规则 **324/324、197/197、19/19**，失败/跳过 0；`preconfirm_*.log`。既有 NU1900 保留；未为此重编译触发无关既有框架警告 |
| 格式与边界 | `dotnet format whitespace Tests/ProfileTests/ProfileTests.csproj --no-restore --include <SaveUpgradeTests.cs 绝对路径> --verify-no-changes`：退出 0，工作区加载警告保留；Python `compile(...)` 语法通过。10 个正式文件哈希与开工相同，v12 样例与正式格 4 原字节相同；HEAD/用户索引未变；`Godot/GodotProject` 下无跟踪文件变化；`preconfirm_scope.log` |
| D0-3 dry-run | `python Tools/ProjectMaintenance/upgrade_saves_v12_to_v14.py --capacity 75 --dry-run`：**退出 0，OK=8、SKIP=0、ERROR=0、written=0**。格 3 主/备内容不同，独立规划；完整输出如下。无正式备份，未写正式存档 |
| D0-3 正式转存 | 用户回复“确认”后，先核对正式 10 文件哈希仍与 dry-run 一致，再执行 `python Tools/ProjectMaintenance/upgrade_saves_v12_to_v14.py --capacity 75`：**退出 0，OK=8、SKIP=0、ERROR=0、written=8**；完整输出与备份路径见下文及 `formal_upgrade.log` |
| 转存后核对 | 备份的 10 文件哈希与开工原文件逐一相同；正式目录只改了 8 个 Data 主/备文件，两份 Catalogue 保持。8 个文件均为 v14，英雄原字段、灵魂、共享进度/旗幡、装备实例及全部属性、实例池/背包顺序和 BOM 全部一致；`formal_upgrade_verify.log`、`formal_after.sha256.json` |
| D0-3 上手验证受阻 | `cua.getState()` 返回本机原生管道不可用：`failed to connect native pipe: 系统找不到指定的文件。 (os error 2)`；重置 CUA 会话后重试同样失败，无法列出窗口。**未发送 UI 输入、未执行真实键盘步骤 1～4、未截图**；`manual_access_failure.log`。这属于计划第一部分第 2 节第 7 类范围外验证阻塞，正式转存结果保留，用户已明确回复“暂时无法恢复连接”，待本机连接恢复后继续；不能以文件核对/规则测试代替上手验收 |
| 尚未执行 | 真实键盘上手 1～4 及截图；D0 完成判定的最终验收。D1～E1 未开始 |

```text
OK 1791793987.sav version=12 hero=1 soul=2630135 bag=55 worn=4 -> v14 BOM=yes
OK 1791793987.sav.bak version=12 hero=1 soul=2630135 bag=55 worn=4 -> v14 BOM=yes
OK 1793209553.sav version=12 hero=1 soul=0 bag=0 worn=0 -> v14 BOM=no
OK 1793209553.sav.bak version=12 hero=1 soul=0 bag=0 worn=0 -> v14 BOM=no
OK 1793449001.sav version=12 hero=1 soul=2630135 bag=55 worn=4 -> v14 BOM=no
OK 1793449001.sav.bak version=12 hero=1 soul=100000 bag=35 worn=3 -> v14 BOM=no
OK 1793888918.sav version=12 hero=2 soul=225080 bag=24 worn=3 -> v14 BOM=yes
OK 1793888918.sav.bak version=12 hero=2 soul=225080 bag=24 worn=3 -> v14 BOM=yes
SUMMARY OK=8 SKIP=0 ERROR=0 written=0 DRY-RUN
BACKUP none (dry-run; no files changed)
```

用户确认后的正式转存完整输出：

```text
OK 1791793987.sav version=12 hero=1 soul=2630135 bag=55 worn=4 -> v14 BOM=yes
OK 1791793987.sav.bak version=12 hero=1 soul=2630135 bag=55 worn=4 -> v14 BOM=yes
OK 1793209553.sav version=12 hero=1 soul=0 bag=0 worn=0 -> v14 BOM=no
OK 1793209553.sav.bak version=12 hero=1 soul=0 bag=0 worn=0 -> v14 BOM=no
OK 1793449001.sav version=12 hero=1 soul=2630135 bag=55 worn=4 -> v14 BOM=no
OK 1793449001.sav.bak version=12 hero=1 soul=100000 bag=35 worn=3 -> v14 BOM=no
OK 1793888918.sav version=12 hero=2 soul=225080 bag=24 worn=3 -> v14 BOM=yes
OK 1793888918.sav.bak version=12 hero=2 soul=225080 bag=24 worn=3 -> v14 BOM=yes
SUMMARY OK=8 SKIP=0 ERROR=0 written=8
BACKUP C:\Users\zheliku\AppData\Roaming\Godot\app_userdata\GameZMXY\GameData.backup-before-v14-20261110_191354
```

备份目录：`C:\Users\zheliku\AppData\Roaming\Godot\app_userdata\GameZMXY\GameData.backup-before-v14-20261110_191354`。本次未启动正式游戏或发送输入，Catalogue 的第 1～4 格时间未因验证改写；正式备份与转存是用户确认后由指定工具执行的唯一正式目录写入。

实际文件：新增 `Tools/ProjectMaintenance/upgrade_saves_v12_to_v14.py`、`Tests/ProfileTests/SaveUpgradeTests.cs`、`Tests/ProfileTests/Data/save_v12_tangseng.json`、`save_v14_tangseng.json`；修改 `Tests/ProfileTests/ProfileTests.csproj`（仅复制样例项）、`Docs/Validation.md`、`Docs/WorkingNotes.md` 与 `Docs/CoopImplementationPlan.md`（仅实际进度，用户原暂存内容保留）。无删除，运行时代码、存档 v14、场景、输入、源表/bytes 与只读依赖均未修改。关键接口没有运行时变化；唯一新外部入口为离线脚本的 `--game-data`、必填 `--capacity` 和 `--dry-run`。

## 双人C4/C5：席位输入与阶段C回归（2026-11-10）

用户明确授权 C4、C5 连续完成后一起交付，覆盖本轮两批之间的通用停止点。用户本条的“不改 project.godot”与 C4-1 冲突，经询问获明确裁决：**只在 [input] 末尾追加计划中的 13 个 P2 动作，P1、ui_* 和其他配置保持**。C4-1 → C4-6 与 C5 已完成最终验证，**停在 C5 等待审查，阶段 D 未开始**。C5 首轮完整 UI 的红球断言失败已报告；用户授权修复既有烟测夹具后，最终完整 UI、花果山、唐僧、掉落、原生输入和前端全部通过，原失败与诊断中止记录保留。仍只生成 P1、存档 v14。

规范依据为实施计划第一、二部分、第六部分共同约束/C4/C5、第九部分占位表、第十部分审查修正，第 00/10/30 章的输入/实体职责、个人状态、命名和注释规则。本批复用原 InputSystem、IInputSource、HeroActionData、技能登记、HUD 事件与窗口互斥，没有新增管理器、接口、类型或默认 P1 转发。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工状态 | 首次读取时 C3 的 8 份修改已暂存、HEAD 为 c489b65；采集基线前用户完成提交 **61de0f2**，随后工作区/索引干净。未执行 Git 写操作；快照与日志在 `.godot/validation/coop_c4_c5_20261110/` |
| 基线 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0，**0 错误、5 条既有框架警告**；`dotnet test Tests/ProfileTests --no-restore -v quiet`、`dotnet test Tests/BattleTests --no-restore -v quiet`、`dotnet test Tests/LevelTests --no-restore -v quiet`：退出 0，**323/323、195/195、19/19**；`baseline_*.log` |
| C4-1 | 用 GodotSharp 4.7.2 的 `Godot.Key` 枚举反射核对 13 个值（`godot_key_values.log`），追加输入动作后 `dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0，**0 错误、0 警告（增量）**；`c4_1_build.log` |
| C4-2 | 统一 InputSystem 必传席位后，同构建命令：**退出 1，5 个 CS7036 迁移错误、5 条既有警告**；分别为 SkillSystem、唐僧实体、动画入口、冰龙和水魔爆的旧调用。`c4_2_build.log`，未在中间态运行游戏 |
| C4-3 | 生产/测试消费者补席位并同步技能输入接口后，同构建命令：退出 0，**0 错误、5 条既有警告**；`c4_3_build.log` |
| C4-4 | 出口按实际英雄席位与组合键交互后，同命令：退出 0，**0 错误、5 条既有警告**；`c4_4_build.log` |
| C4-5 | 开窗事件、库存关闭键与流程校验同步后，同命令：退出 0，**0 错误、5 条既有警告**；`c4_5_build.log` |
| C4-6 | 增加原生输入与焦点证据后，同命令：退出 0，**0 错误、5 条既有警告**；`c4_6_build.log`。六步错误数为 **0 / 5 / 0 / 0 / 0 / 0** |
| C4 最终构建/规则 | 格式化后重跑构建与三条测试命令：退出 0，构建 **0 错误、5 条既有警告**；规则 **323/323、197/197、19/19**，失败 0、跳过 0，新增 2 例席位输入隔离。`c4_final_build.log`、`c4_final_profile.log`、`c4_final_battle.log`、`c4_final_level.log`；既有 NU1900、CS8632/CS0649 保留 |
| C4 编辑器扫描 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c4_scan_20261110 --headless --editor --build-solutions --quit --no-window`：退出 0；保留既有 ComponentInsoector RefCounted/free 退出诊断 |
| C4 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：退出 0，**2283 条序列化路径、390 个 UID**；`c4_references.log` |
| C4 原生映射/焦点 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c4_input_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui --entry-only`：**退出 0，SMOKE-SEAT INPUT PASS 与完整 SMOKE PASS**。实际 InputMap.ActionGetEvents 逐项核对 13 个事件、物理键、device=-1、Keycode=None 与死区；真实键盘事件全部命中 P2 动作，P1 无移动/攻击/跳跃/无双和开窗请求；方向键不获取 HUD 焦点，随后 Enter/ui_accept 不误开窗口。现有场景 5 个菜单按钮均 focus_mode=0，无冲突，未改场景。退出报告 **3 个 ObjectDB 实例** |
| C4 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c4_frontend_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**；两英雄建档/进关、设置返回、地图购技、保存重读、取消/失败清理通过。坏文件、旧作用域拒绝与重复注册为预期注入；退出报告 **3 个 ObjectDB 实例** |
| C4 完整 UI | 首轮 `coop_c4_ui_20261110` **退出 1**：HuaguoshanMonkey 的原攻击命中断言超时；没有 SMOKE PASS。新增输入探针原在完整回归开头推进主英雄真实落地，改变旧夹具的物理起点；已移到独立 `--entry-only` 分支，保留原回归流程及所有命中断言，重编译 0 错误。重跑 `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c4_ui_final_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui`：**退出 0，完整 SMOKE PASS**，原失败位置及后续技能/装备/拾取/窗口、60/120Hz、死亡/存档/清理通过；退出 38 个 CanvasItem RID、20 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、135 个 ObjectDB 实例、10 个资源占用与 GL 明细。首轮失败日志与其 17 个 Texture RID、134 个 ObjectDB、11 个资源占用等诊断保留 |
| C5 构建与规则重跑 | C4 通过后重新运行同样四条 dotnet 命令：**退出 0**，构建 **0 错误、5 条既有警告**；规则 **323/323、197/197、19/19**，失败 0、跳过 0；`c5_build.log`、`c5_profile.log`、`c5_battle.log`、`c5_level.log` |
| C5 扫描与引用重跑 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c5_scan_20261110 --headless --editor --build-solutions --quit --no-window`：退出 0，既有 ComponentInsoector 退出诊断保留；再次 `python Tools/ProjectMaintenance/validate_game_references.py`：退出 0，**2283 路径、390 UID**；`c5_references.log` |
| C5 原生映射/焦点重跑 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c5_input_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui --entry-only`：**退出 0，SMOKE-SEAT INPUT PASS 与完整 SMOKE PASS**，探针隔离到独立案例后同样通过 13 键/焦点/单人无响应检查；退出 **5 个 ObjectDB、1 个资源占用** |
| C5 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c5_frontend_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**；两英雄建档/进关、地图学习、设置保存返回、重读与取消/失败清理通过；预期坏文件/旧作用域拒绝保留，退出 **5 个 ObjectDB、1 个资源占用** |
| C5 完整 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c5_ui_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui`：**退出 1，无最终 SMOKE PASS**；已通过技能/无双、15 件穿戴、拾取/窗口、怪物/实体复用、升级及 60/120Hz 坡地和三类平台。失败于 `CheckManaPickupAsync` 的“红球实际恢复量错误”（CombatFeedback.cs:245），首轮时该段红球生成/回收/断言未改；日志没有实际 HP，根因未确定，不能将自动恢复或受击写成已证实原因。C4 同一最终源码曾完整通过；不以旧 PASS 替代此次失败。保留 **38 个 CanvasItem RID、17 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、136 个 ObjectDB、12 个资源占用**及 GL 明细 |
| C5 掉落/无归属专项 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c5_loot_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui --loot-only`：**退出 0，完整 SMOKE PASS**；无归属只计击败/保留地面掉落，60/120Hz 红蓝黄球恢复/上限/暂停/池复用、指定目标/归属离场和死亡回收通过。独立专项未复现完整 UI 的红球失败；退出 **12 个 ObjectDB、3 个资源占用** |
| C5 花果山原生流程 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c5_level_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=level`：**退出 0，完整 SMOKE PASS**；阶段移动/相机、场外刷怪入屏、守关清场、近远出口与组合键检查、W 出口等待在途球、自动保存、地图可见和磁盘重读通过，累计经验 **204**，出口到地图 **2652ms（含检查点，pending loot=True）**；退出 **12 个 ObjectDB、3 个资源占用**。沿用 LevelSmokeScenario 的本局 +100000 生命和测试攻击清场夹具，保留真实输入/碰撞/阶段/出口/结算链，不称正常数值人工通关 |
| C5 唐僧专项 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c5_tangseng_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui --tangseng-only`：**退出 0，完整 SMOKE PASS**；60/120Hz 分身物理/暂停/复用、普通法弹、部分与完整冰龙蓄力、主动学习、甘露/玄冰/水魔追加及八种声音的真实输入通过；退出 **14 个 ObjectDB、4 个资源占用** |
| C5 红球诊断 | 用户明确授权定位并修复既有烟测夹具，生产玩法和原断言保持。先只补充原红球相等断言的实际/预期 HP、存活/物理处理与恢复节拍信息；`dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0，**0 错误、5 条既有警告**，`c5_red_diagnostic_build.log`；`python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c5_ui_diagnostic_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui`：**runner 达 900 秒上限，报告 exit=124，无 SMOKE PASS**；日志停在敌人 63 出招（大猩猩截图步骤附近），未到红球断言，stderr 为空；终态后核对两个 Godot 进程均已退出。窗口检查的原生管道不可用（`failed to connect native pipe`），随后用户确认窗口可见/桌面解锁；没有证据将最小化写成此次卡住根因。另以 `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c5_red_headless_20261110 --headless --max-fps 120 -- --smoketest=ui` 继续诊断；只用于定位，不能替代 C5 原生 UI 验收。该 headless 路径越过大猩猩截图后停在 CheckDemonChaseAsync 的逐渲染帧等待；核对源码后确认其无 headless 分支，已主动停止对应已核实命令行的进程（runner 报告 4294967295，无 PASS），没有继续等到 900 秒 |
| C5 夹具修复与回归 | 用户授权范围内，只在红球恢复量/上限断言期间临时将主英雄 HurtBox.CollisionLayer 设为 0，finally 恢复原层；球仍用原受击盒几何追踪，原相等/上限/暂停/池复用断言保持。已有场内敌人持续出招，隔离其进入数值断言的真实干扰路径；原失败缺实际 HP，不能将额外受击写成已复现根因。首次包围作用域使后续清理探针读取 health 失去作用域，构建 **1 个 CS0103 错误**（`c5_fixture_build.log`）；把原生命恢复值保留在外层后构建 **0 错误、5 条既有警告**（`c5_fixture_build_final.log`）。三套规则重跑 **323/197/19** 全通过，`c5_after_fixture_*.log`；最终 13/1/2 份 C# 格式检查全部退出 0。`coop_c5_scan_final_20261110` 扫描退出 0，既有 ComponentInsoector/free 诊断保留；`c5_references_final.log` 引用 **2283/390**。原生完整 UI 命令 `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c5_ui_final_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui`：**退出 0，完整 SMOKE PASS**；红球恢复/上限/池复用及后续死亡/检查点/存档重入/备份恢复/取消和失败清理均通过。预期坏文件和 HUD 打开失败注入保留，退出 **38 个 CanvasItem RID、20 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、138 个 ObjectDB、10 个资源占用**及 GL 明细；未出现 GCHandle 诊断 |
| C5 夹具修复后原生映射/焦点 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c5_input_final_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui --entry-only`：**退出 0，SMOKE-SEAT INPUT PASS 与完整 SMOKE PASS**，13 个原生物理键、单人忽略 P2、方向键/Enter 焦点检查通过；退出 **5 个 ObjectDB、1 个资源占用** |
| C5 夹具修复后完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c5_frontend_final_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**；悟空/唐僧建档、进关、地图学习、保存返回与重读、加载取消/失败清理再次通过；退出 **5 个 ObjectDB、1 个资源占用**，坏文件/旧作用域拒绝为预期注入 |
| C5 夹具修复后掉落/无归属 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c5_loot_final_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui --loot-only`：**退出 0，完整 SMOKE PASS**；60/120Hz 无归属边界、红蓝黄球实际数值/上限/暂停/池复用、实际目标与归属离场、死亡回收全部通过；退出 **12 个 ObjectDB、3 个资源占用** |
| 格式 | 最终状态分别执行 `dotnet format whitespace <GodotProject.csproj / ProfileTests.csproj / BattleTests.csproj> --no-restore --include <本批对应 C# 绝对路径清单> --verify-no-changes`：全部退出 0，分别 **13 / 1 / 2 份 C#**；`format_verify_game/profile/battle.log`。测试工程保留工作区加载警告 |
| 最终边界 | **21 份修改、无新增/删除正式文件**；16 份 C# 与 4 份文档均 UTF-8/CRLF，project.godot 保持 LF；删除恰好 13 个新增 P2 块可逐字节恢复基线配置。HEAD 保持 **61de0f2**，索引空且哈希与基线一致。Framework/addons/Luban、生成物、源表/bytes、场景、csproj 均未改；Validation 历史 C3 及之后各节在换行归一后逐字相同。`final_scope_audit.log`；`git diff --check`、`git diff --cached --check` 均退出 0；未执行 Git 写操作 |

原生映射实际核对值，全部为 physical_keycode、device=-1、keycode=0：

| 动作 | 物理键 / Godot.Key | 数值 | 死区 |
| --- | --- | --- | --- |
| move_left_p2 / move_right_p2 | Left / Right | 4194319 / 4194321 | 0.5 |
| move_down_p2 / interact_p2 | Down / Up | 4194322 / 4194320 | 0.2 / 0.5 |
| attack_p2 / jump_p2 | Kp1 / Kp2 | 4194439 / 4194440 | 0.5 |
| skill_1_p2 / skill_2_p2 | Kp4 / Kp5 | 4194442 / 4194443 | 0.5 |
| skill_3_p2 / skill_4_p2 | Kp6 / Kp3 | 4194444 / 4194441 | 0.5 |
| musou_p2 | Kp0 | 4194438 | 0.5 |
| inventory_toggle_p2 / skill_toggle_p2 | KpDivide / KpMultiply | 4194434 / 4194433 | 0.5 |

关键接口前后对照：

| 交付前 | 交付后 |
| --- | --- |
| InputSystem 常量直接查询 P1 名称 | 常量值保持，新增 `static string ActionOf(string action, PlayerSlot slot)`，P1 返回基名、P2 追加 `_p2`；不缓存、不改 ui_* |
| `IsActionPressed/IsActionJustPressed/IsActionJustReleased(string action)` | 统一为 `(string action, PlayerSlot slot)`，先解析再走原 IInputSource |
| `ConsumePress(HeroActionData data, string action)` | 增加末尾必传 PlayerSlot slot；先解析名称，按本人的边沿与 ConsumedActions 消费实际名称；无默认值或旧重载 |
| Sample 只采样 P1 | 签名保持，所有移动/动作边沿按 hero.Slot 查询，原双击、闩锁与冷却不变 |
| `TryActivateInput(int heroId, ISkillActor actor, HeroActionData data)` | 增加末尾 PlayerSlot slot；角色实体传 Slot，唐僧身体技能传 body.Slot；ISkillActor 不新增状态 |
| LevelExitNode.FindEligibleHero 取第一个，读 P1 组合键 | 移除该方法，输入时逐个检查存活/落地的重叠英雄，按本人 Interact 与 Attack/Jump/MoveDown 判断；提示按任一合格重叠者显示，Enable/Disable 保持 |
| BattleForm.InventoryRequested / SkillsRequested 为 Action<int> | 改为 Action<int, PlayerSlot>，按钮传宿主 Slot，快捷键只响应阵容已有席位；当前仍一名英雄，保留宿主 ControlsEnabled 守卫；SettingsRequested 不变 |
| ProcedureLevel.OnInventoryRequested / OnSkillsRequested(int formSerialId) | 增加 PlayerSlot slot，校验 `_hero.Slot == slot`；背包补死亡拒绝，技能保留死亡拒绝，三窗互斥与订退不变 |
| InventoryForm 只用 P1 关闭键 | 使用打开实体的 Slot 解析关闭键；UiCancel 不变。SkillForm 保持 P1，E2 接请求者，已登记第十部分占位 |
| `HeroSpawn(PlayerSlot Slot, Vector2 Position)`、`AbstractHeroEntity.OnShow(object userData)` / `public PlayerSlot Slot { get; private set; }`、`IHeroBody` 的 `PlayerSlot Slot { get; }` | **本批前后签名和生命周期均不改**：OnShow 强制 HeroSpawn、校验本局模型与 HeroOf(spawn.Slot) 对应关系，每次覆盖 Slot 并在原复位段写出生点；不加默认席位或 OnHide 清零。假宿主 Hero 的 Slot 仅在测试允许初始化，用于两席位输入证据 |

新增 Battle 的 `SeatInput_OnlySamplesAndConsumesOwnActions` 两个方向案例，使用正式 InputSystem 与原 FrameInput/Hero：同一来源只改变所属席位的移动、攻击、无双与松键事实；技能边沿只按本人映射消费一次，记录解析后的动作名，另一人保持。原 195 例与 Profile 323 例规则保留，原断言仅机械补传席位。

实际文件清单：**21 份修改（16 份 C#、project.godot、4 份文档），无新增或删除正式文件**。TheGame 路径相对 `Godot/GodotProject/`：

| 目录 | 修改文件 |
| --- | --- |
| `TheGame/GameScripts/Systems/` | `Input/InputSystem.cs`、`SkillSystem.cs` |
| `TheGame/GameScripts/Entity/Heroes/` | `Wukong/WukongEntity.cs`、`Tangseng/TangsengEntity.cs`、`Tangseng/TangsengEntity.AnimationEvents.cs`、`Tangseng/Skills/TangsengIceDragonState.cs`、`Tangseng/Skills/TangsengWaterDemonBurstState.cs` |
| `TheGame/GameScripts/Entity/Level/Exit/` | `LevelExitNode.cs` |
| `TheGame/GameScripts/UI/` | `BattleForm/BattleForm.Logic.cs`、`InventoryForm/InventoryForm.Logic.cs` |
| `TheGame/MainPack/Scripts/` | `Procedure/ProcedureLevel.cs`、`Debug/UiSmokeScenario.cs`、`Debug/UiSmokeScenario.CombatFeedback.cs` |
| `Godot/GodotProject/` | `project.godot`，仅追加 13 个动作 |
| `Tests/` | `BattleTests/HeroBodyTests.cs`、`BattleTests/SkillReuseTests.cs`、`ProfileTests/SkillSystemTests.cs` |
| `Docs/` | `CoopImplementationPlan.md`、`WorkingNotes.md`、`ProjectTour.md`、`Validation.md` |

计划未逐文件列出的调用为 UiSmokeScenario 的关停窗口分支：通过反射调用 OnInventoryRequested/OnSkillsRequested，必须补传 hero.Slot；设置方法仍只传序列号。已机械同步，没有旧签名转发。新增输入证据直接放入既有 `--entry-only` 分支，未新增测试模式或生产入口；首轮放在完整回归开头会改变主英雄落地状态，现已隔离到该独立案例，原完整 UI 断言不变。配置写入曾统一成 CRLF，边界检查发现原文件为 LF 后已恢复；去掉新增 13 块可恢复基线原字节，未改 P1 或其他配置。

最终全仓搜索 `rg -n 'IsActionPressed|IsActionJustPressed|IsActionJustReleased|ConsumePress|TryActivateInput|ActionOf' . --glob '*.cs'`：InputSystem 的查询/消费调用均显式带席位；Godot.Input 与 InputEvent 的原生单参数查询不属于该业务接口，出口/背包/HUD 均先 ActionOf，SkillForm 的 P1 关闭键与 ui_* 依计划保留；见 `input_calls_search.log`。英雄显示搜索仍只有 ProcedureLevel、UiSmokeScenario.ShowHeroAsync、TestArenaController.SpawnHeroAsync 三处，全部传 HeroSpawn，见 `hero_show_search.log`。第十部分后续占位只剩 D2 治疗飘字和 E2 SkillForm，没有 C 阶段待处理项；第九部分 C2/C3 行均已标清零。

C5 首轮失败的原始日志与两次未完成诊断保留。夹具缺口是：CheckGroundCombatAsync 会恢复场内敌人的处理，而红球数值检查将 HP 设为 1 后等待飞行，没有隔离入场攻击；本批经用户授权只对红球恢复/上限检查临时移除受击层，finally 恢复，保留球几何与原相等断言，并扩充失败状态消息。原失败缺少实际 HP/伤害记录，因此不把额外受击或自动恢复写成已复现根因；最终原生完整 UI 和 60/120Hz 掉落专项均已通过，范围内没有未完成项。花果山使用既有强化生命与测试攻击夹具，正常数值全关人工实战未执行。全部烟测使用既有 runner 的案例专属 APPDATA，正式存档未覆盖；存档 v14 不变，不要求重建。既有框架/网络/退出诊断保留，最终 UI 未出现 GCHandle 诊断。当前仍只生成 P1、单 HUD；D2 治疗订阅和 E2 SkillForm 席位占位按计划保留。停在 C5 待审，阶段 D 须待用户接受并明确授权。

## 双人C3：球归属与接触者领取（2026-11-10）

用户接受 C2 并要求继续 C3，按 C3-1 → C3-3 完成逐球归属、实际接触者结算，以及治疗飘字在 D2 处理的登记。当前仍只生成 P1，存档保持 v14，一套输入与 HUD；**全部要求验证通过，停在 C3 等待审查，不开始 C4**。

依据为计划第一、二部分、第六部分阶段共同约束/C3、第九部分占位总表和第十部分审查修正，规范第 00/10/30 章的唯一状态、实体/节点池生命周期与命名注释，以及 GGF NodePool 文档和实际 Release 实现。沿用 LootOrbNode、LevelViewController、GroundItemNode、现有个人奖励接口和既有烟测，不新增类型、文件或系统。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工状态与基线 | 初次读取时 C2 的 15 份修改已暂存、HEAD 为 394b97c；采集基线前用户已提交为 **c489b65**，随后工作区与索引干净。本批未执行 Git 写操作，快照与日志在 `.godot/validation/coop_c3_20261110/`。`dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0，**0 错误、5 条既有框架警告**；`dotnet test Tests/ProfileTests --no-restore -v quiet`、`dotnet test Tests/BattleTests --no-restore -v quiet`、`dotnet test Tests/LevelTests --no-restore -v quiet`：退出 0，**323/323、195/195、19/19**；`baseline_*.log` |
| C3-1 完成 | 球参数、逐球推进/结算、两处直接显示调用同步后，`dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0，**0 错误、5 条既有框架警告**；`c3_1_build.log` |
| C3-2 完成 | 地面领取使用实际接触者后，同构建命令：退出 0，**0 错误、5 条既有框架警告**；`c3_2_build.log` |
| C3-3 完成 | 不改治疗代码，在计划第十部分登记 D2 待办后，同构建命令：退出 0，**0 错误、0 警告（增量）**；`c3_3_build.log`。三步错误数为 **0 / 0 / 0** |
| 最终游戏构建 | 烟测边界与格式化完成后，`dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0，**0 错误、5 条既有框架警告**；`final_build.log` |
| 最终规则测试 | 重跑上述三条 `dotnet test` 命令：退出 0，**323/323、195/195、19/19**，失败 0、跳过 0，测试数量不减；`final_profile.log`、`final_battle.log`、`final_level.log`。未改测试工程或原断言，保留既有 NU1900、CS8632/CS0649，后两项见 Battle 开工重编译日志 |
| Godot 扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c3_scan_20261110 --headless --editor --build-solutions --quit --no-window`：**退出 0**；保留既有 ComponentInsoector 的 RefCounted/free 退出诊断 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：退出 0，**2283 条序列化路径、390 个 UID**，与 C2 相同；`final_references.log` |
| 掉落专项 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c3_loot_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui --loot-only`：**退出 0，完整 SMOKE PASS**。60/120Hz 下原经验/升级动画、红球数量、红蓝黄球数值与钳制、暂停、池复用和死亡清理通过；新增 `SMOKE-ORB OWNERSHIP PASS` 检查指定目标、回池前发奖、取出/归还清零、归属者离场后回收且不冒发 P1。C2 无归属只计数/保留地面掉落的案例也保留。退出报告 **12 个 ObjectDB 实例、3 个资源占用** |
| 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c3_frontend_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**；悟空/唐僧建档、进关、地图学技、设置保存返回、重读、加载取消和部分注册清理通过。坏文件、重复注册与旧作用域拒绝为预期注入；退出报告 **5 个 ObjectDB 实例、1 个资源占用** |
| 完整 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c3_ui_20261110 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui`：**退出 0，完整 SMOKE PASS**；技能/无双、15 件穿戴、地面拾取/满包、窗口和实体复用、逐球归属断言、60/120Hz 坡地/平台、死亡/检查点、存档重入、备份恢复、加载取消与失败清理通过。坏文件与 HUD 打开失败为预期注入；退出报告 **38 个 CanvasItem RID、20 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、138 个 ObjectDB 实例、10 个资源占用**与 GL 纹理明细。所有烟测不设帧数强制退出，使用既有 runner 的 900 秒超时和完整案例完成后自行退出 |
| 格式 | `dotnet format whitespace Godot/GodotProject/GodotProject.csproj --no-restore --include <本批 4 份 C# 绝对路径清单> --verify-no-changes`：退出 0；`format_verify_game.log` |
| 差异与边界 | `git diff --check`、`git diff --cached --check` 均退出 0；4 份 C#、4 份文档为 UTF-8/CRLF，无行尾空白。HEAD 保持 c489b65、索引为空，8 份增量均未暂存，未执行 Git 写操作。Framework/addons/Luban、生成物、源表/bytes、场景、project.godot、csproj、GroundItemNode 与英雄显示契约均无变化 |

关键接口与顺序对照：

| C3 前 | C3 后 |
| --- | --- |
| `LootOrbNode.Show(Vector2 position, LootReward reward, Action<LootOrbNode, LootReward> finished)` | 在 reward 后增加必传 `int ownerEntityId`；新增 `public int OwnerEntityId { get; private set; }`，只使用自动属性的私有后备存储，不另存重复字段；Show 写入，OnGet/OnRelease 清零 |
| `Tick(float delta, Vector2 target)`；关卡统一传 `_player` 的受击盒中心 | **节点签名不变**；TickLoot 按每个 orb.OwnerEntityId 解析存活英雄后取其受击盒中心；解析失败/已死亡则从当前集合移除并回池，不发放奖励。球不持有实体引用，飞行参数不变 |
| OnLootFinished 先 Release，再 `CollectLoot(_player.HeroId, reward)` | 先按 OwnerEntityId 解析英雄，再 Release；仍存活才 `CollectLoot(hero.HeroId, reward)`。原只移除本关拥有的球/一次结算门槛和等待出口逻辑保留，不在回池后读清零编号 |
| TryCollectGroundItem 按 `_player.HeroId` 入账，在 `_player` 位置飘字 | 接收“本会话存活英雄”，当前仍为 `ReferenceEquals(hero, _player) && hero.IsAlive`；使用 `hero.HeroId` 调装备/恢复品领取，在 `hero.GlobalPosition` 飘字。GroundItemNode 与满包返回 false/成功一次机制均不改 |
| `OnHeroHealthRestored(int amount)` / `HealthRestored: Action<int>` | **本批不改**，单人下仍在 `_player.GlobalPosition` 飘字；第十部分新增 D2 待办索引，按参战集合订阅时闭包捕获实际英雄，第九部分总表也保留该项 |
| HeroSpawn、AbstractHeroEntity.Slot / OnShow 契约、IHeroBody.Slot | **全部保持 C1 实现和签名**，没有新增英雄显示点或第二套输入 |

`TickLoot` 开头的主英雄有效/存活守卫按计划保留：单人死亡由 OnHeroDied.ClearLoot 立即回收奖励，D5 再改成逐人存活规则。球的恢复数值仍在生成时按 C2 的归属 HeroState 上限确定，本批不改为领取时计算。地面物品的当前单英雄接触轮询与会话成员判断留待 D2 扩展。

新增证据直接扩展既有 CheckManaPickupAsync / CheckFlyingLootAsync，不新增入口、模式或夹具：正式死亡生成的每颗球检查 OwnerEntityId；原目标探针放在额外节点身边却归属主节点，随后反向指定已有额外节点，检查两次真实轨迹分别趋近各自归属；蓝量恢复证明回池清零前已读出领取者。再生成归属额外节点的灵魂球，隐藏该节点，检查球回收、归属归零且余额不变。额外表现节点与主节点借用同一 HeroId/状态，沿用原烟测装配，只证明实体目标选择，不把它称为双人独立数值实战。

全仓搜索 `rg -n -C 2 'ShowEntityAsync|ShowOwnedEntityAsync' . --glob '*.cs'`：英雄请求仍只有 ProcedureLevel、UiSmokeScenario.ShowHeroAsync、TestArenaController.SpawnHeroAsync 三处，全部带 HeroSpawn；其席位和出生点未变化，输出在 `hero_show_search.log`。球显示只有关卡正式生成与烟测 SpawnOrb 两处直接 Show，均显式传归属；SpawnOrb/SpawnManaOrb 的测试调用也全部补编号，没有默认值/兼容重载，见 `loot_owner_search.log`。`LevelViewController.Loot.cs` 中 `_player.HeroId` 与统一 `_player` 球目标均零命中。

实际文件清单：**8 份修改（4 份 C#、4 份文档），无新增或删除文件**，TheGame 路径相对 `Godot/GodotProject/`：

| 目录 | 修改文件 |
| --- | --- |
| `TheGame/GameScripts/Entity/Drops/` | `LootOrbNode.cs` |
| `TheGame/GameScripts/Entity/Level/` | `LevelViewController.Loot.cs` |
| `TheGame/MainPack/Scripts/Debug/` | `UiSmokeScenario.CombatFeedback.cs`、`UiSmokeScenario.cs` |
| `Docs/` | `CoopImplementationPlan.md`、`WorkingNotes.md`、`ProjectTour.md`、`Validation.md` |

计划未逐文件列出的消费者为烟测的 SpawnOrb：机械同步必传归属编号，并将已有 SpawnManaOrb 和两个 UiSmokeScenario 分支一并补实参；原数值、满包和暂停断言保留。C3-3 的治疗待办原已在第九部分，第十部分没有独立占位节，按本批明确要求新增一行索引，不改事件/订退代码。未发现需改方案、资源或场景的冲突。

当前 C3 范围内无未完成项。现有单人拾取、满包、球飞行与数值均由原断言加新增归属案例再次检查；治疗事件和主英雄死亡守卫按计划留待 D2/D5。正常数值全关人工实战未执行；存档仍为 v14，不要求旧档重建。烟测均使用既有 runner 的案例专属 APPDATA，不覆盖正式存档；既有框架/网络警告及编辑器/运行退出诊断保留，本次完整 UI 未出现上一批记录的 GCHandle 初始化诊断。C4 须待用户接受 C3 并明确授权，本批不改 project.godot，不进入 P2 键位、按席位开窗或双人实体装配。

## 双人C2：经验与球的命中归属（2026-11-10）

用户在 C1 交付后授权继续 C2，按 C2-1 → C2-5 完成最后一次有效英雄命中的记录、死亡事件传递、经验发放、牛心唯一奖励与飞行球生成边界。11 月 9 日开工，完整 UI 回归跨至 11 月 10 日，案例目录沿用开工日期。仍只生成 P1，存档保持 v14；**全部要求验证通过，停在 C2 等待审查，不开始 C3**。

依据：计划第一、二部分、第六部分阶段共同约束与 C2、第九部分占位清单、第十部分审查修正，以及规范第 00/10/30 章的状态所有权、实体生命周期、值事件与中文文档要求。复用 OnHurt、GF.Entity.GetEntity、池化事件、LevelSystem、ProfileSystem 与已有烟测；没有新增类型、文件或系统。

本阶段的明确玩法变化：从未被任何英雄有效命中的怪物仍计击败、照常生成地面物品，**不发经验、不生成飞行球**；结算 AwardedExperience 也不累计未发出的经验。官方 BaseMonster.dropAura 先生成地面物品，再于无 curAttackTarget 时早退；官方还会自行用 AI 选人，所以存在“从未被打但已锁定某人仍给经验/球”的副作用，本项目按计划只认实际命中，归属字段与 AI 目标独立。牛心金钢琢按致死者发放，无归属则不发物品或奖励事件；官方 Monster24.toDead 固定写入 player1.zblist，本项目有意不采用这一点。源码行号与取舍见计划 C2 官方规则及 C2-4。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工基线 | HEAD 为 **394b97c**，工作区与索引干净。`dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0，**0 错误、5 条既有框架警告**；`dotnet test Tests/ProfileTests --no-restore -v quiet`、`dotnet test Tests/BattleTests --no-restore -v quiet`、`dotnet test Tests/LevelTests --no-restore -v quiet`：**320/320、195/195、19/19**，均退出 0。快照与日志在 `.godot/validation/coop_c2_20261109/` |
| C2-1 完成 | 英雄归属记账与鹏魔蛋态同步后执行 `dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0，**0 错误、5 条既有框架警告**；`c2_1_build.log` |
| C2-2 完成 | 死亡事件与两处原调用同步后，同命令：退出 0，**0 错误、5 条既有框架警告**；`c2_2_build.log` |
| C2-3 完成 | 经验接口、当前会话归属解析与规则案例同步后，同命令：退出 0，**0 错误、5 条既有框架警告**；`c2_3_build.log` |
| C2-4 完成 | 牛心唯一奖励按致死者发放后，同命令：退出 0，**0 错误、5 条既有框架警告**；`c2_4_build.log` |
| C2-5 完成 | 飞行球按有效归属分支生成后，同命令：退出 0，**0 错误、5 条既有框架警告**；`c2_5_build.log`。五步错误数为 **0 / 0 / 0 / 0 / 0** |
| 最终游戏构建 | 补齐烟测输入和边界案例、格式化后 `dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0，**0 错误、5 条既有框架警告**；`final_build.log` |
| 最终规则测试 | 重跑三条 `dotnet test` 命令：退出 0，**323/323、195/195、19/19**，失败 0、跳过 0；Profile 新增 3 例，原测试数量与断言不减。`final_profile.log`、`final_battle.log`、`final_level.log`；保留既有 NU1900、CS8632/CS0649，后两项出现在 Battle 开工重编译日志 |
| Godot 扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c2_scan_final_20261109 --headless --editor --build-solutions --quit --no-window`：退出 0，最终源码扫描/构建通过；保留既有 ComponentInsoector RefCounted/free 退出诊断。补烟测前的 `coop_c2_scan_20261109` 也退出 0，最终证据以前者为准 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：退出 0，**2283 条序列化路径、390 个 UID**，与 C1 相同；`final_references.log` |
| 掉落专项 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c2_loot_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui --loot-only`：**退出 0，完整 SMOKE PASS**。新增 `SMOKE-UNOWNED LOOT PASS`：直接消费真实死亡快照，击败数加一、个人经验/AwardedExperience/球集合不变、地面掉落数量符合正式 RollGround。保留 60/120Hz 正常击杀的经验/升级动画、红球数量与拾取、暂停、池复用和死亡清理断言。退出报告 **5 个 ObjectDB 实例** |
| 鹏魔专项 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c2_garuda_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui --garuda-only`：**退出 0，完整 SMOKE PASS**；60/120Hz、双朝向、五招、火焰淡入有效伤害、化卵/复生/死亡、第五击归属与池复用清零通过。退出报告 **7 个 ObjectDB 实例** |
| 牛心专项 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c2_bull_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui --bull-only`：**退出 0，完整 SMOKE PASS**；60/120Hz、五招/双朝向、吞噬释放、心脏显隐与火柱回收、无归属拒绝、致死归属、首次/重复/满包/提示缺席奖励、死亡完成与订阅复用通过。退出报告 **8 个 ObjectDB 实例** |
| 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c2_frontend_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**；两英雄建档/选角、进关、地图购技、设置保存返回、重读、加载取消和部分注册清理通过。坏文件、重复注册与旧作用域拒绝为预期注入；退出报告 **5 个 ObjectDB 实例、1 个资源占用** |
| 完整 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c2_ui_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui`：**退出 0，完整 SMOKE PASS**；技能/无双、拾取出售、15 件穿戴、窗口/实体复用、升级与动画、60/120Hz 坡地/平台、死亡/检查点、备份恢复、存档重入与加载取消/失败清理通过。坏文件与 HUD 打开失败为预期注入。另有 **18 条 Godot GCHandle 初始化诊断**，同签名已在 11 月 6 日历史日志出现，详见下段；退出报告 **38 个 CanvasItem RID、20 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、141 个 ObjectDB 实例、11 个资源占用**与 GL 纹理明细。前端/UI 和所有专项均不设帧数强制退出，复用原 runner 的 900 秒超时与烟测完成后自行退出 |
| 格式 | `dotnet format whitespace <csproj> --no-restore --include <本批 C# 绝对路径清单> --verify-no-changes`：GodotProject **10 份**、ProfileTests **1 份**均退出 0；`format_verify_game.log`、`format_verify_profile.log`，仅测试工作区既有加载警告 |
| 差异与边界 | `git diff --check`、`git diff --cached --check` 均退出 0；11 份 C# 与4份文档为 UTF-8/CRLF，无行尾空白。HEAD 保持 **394b97c**，索引为空，15 份增量均未暂存；未执行 Git 写操作。Framework/addons/Luban、生成物、源表/bytes、场景、project.godot、csproj、HeroSpawn/IHeroBody/英雄 OnShow 与球追踪领取接口均无变化 |

关键接口与生命周期对照：

| C2 前 | C2 后 |
| --- | --- |
| 敌人只有 AI 目标 `_target` | 独立 `_lastHeroAttackerId`，0 表示未被英雄命中；新增 `protected void RecordHeroAttacker(int attackerEntityId)`，只由基类 OnHurt 首条语句与鹏魔蛋态分支调用；新增 `internal int LastHeroAttackerId`，供牛心死亡状态消费 |
| 敌人 OnShow 只清 AI 目标 | 同时清英雄归属；BeginDeath 保留归属用于事件，持续伤害不改写；Miss、无敌或已死亡的攻击仍在原 ReceiveHit 边界拒绝，不进入 OnHurt |
| `EnemyDiedEventArgs.Create(int entityId, int experienceReward, EnemyConfig config, ...)` | 增加 `int killerEntityId`，紧随 experienceReward；新增只读 `KillerEntityId`，Create 写入、Clear 清零。原敌人死亡入口和 LevelSmokeScenario 的重复事件同步实参，掉落专项另创建无归属输入 |
| `bool RecordDefeat(int experienceReward)` | `bool RecordDefeat(int heroId, int experienceReward)`；保留会话门槛，运行中始终计击败；0 不发经验且返回 false，非零才累计已发经验并调用 AddExperience(heroId, ...) |
| OnEnemyDefeated 总给 `_player` 经验与升级动画、按 `_player` 上限生成球 | 解析 KillerEntityId，仅 ReferenceEquals(killer, _player) 为当前会话有效归属；RecordDefeat 返回 true 时调用 killer.PlayLevelUp，球只按该英雄 HeroState 的生命/魔法上限生成。原 `_player.IsAlive` 总守卫按计划保留，D5 再处理死后延迟击杀 |
| 牛心总向 P1 发唯一装备 | 由 owner.LastHeroAttackerId 解析英雄；0 时不调用 GrantUniqueEquipment、不发布 EquipmentRewardEventArgs；延迟两秒死亡/清关时序保持 |
| `HeroSpawn(PlayerSlot Slot, Vector2 Position)`、AbstractHeroEntity.Slot、IHeroBody.Slot、英雄 OnShow 强制参数/席位契约 | **全部保持 C1 接口与实现**；本批不改英雄显示入口或席位装配 |
| LootOrbNode.Show / Tick、球领取与追踪 | **签名与行为保持**，C3 才按球归属追踪/领取；本批只限制生成 |

新增规则证据复用 LevelModelTests 和正式 TestGameScope：`UnownedDefeat_OnlyCountsEnemy` 先给第二英雄记录 25 点已发经验，再以 0/135 记录无归属击败，断言两人完整成长与已发统计保持、击败数加一；`Defeat_AwardsOnlyRequestedHero` 分别以英雄 1、2 为归属，核对真实 135 点升级门槛、返回值、结算统计与另一人完整成长不变。

现有专项的新增检查：鹏魔在无英雄来源化卵、复生后，再以无来源完成前四次破蛋，第五次才用实际英雄来源；检查每击的归属、真正死亡与池复用清零，原动作/暂停/复生/死亡断言保留。牛心新增无归属真实致死情形，随后继续原首次/重复/满包/提示缺席四情形，核对致死一击归属与每次显示清零。

实际文件清单：**15 份修改（11 份 C#、4 份文档），无新增或删除文件**。TheGame 路径相对 `Godot/GodotProject/`：

| 目录 | 修改文件 |
| --- | --- |
| `TheGame/GameScripts/Entity/Enemies/` | `AbstractEnemyEntity.cs`、`Bosses/zm1_06_garuda_cave/PengDemonKingEntity.cs`、`Bosses/zm1_10_bull_demon_valley/BullDemonHeartDeathState.cs` |
| `TheGame/GameScripts/Entity/Level/` | `LevelViewController.Loot.cs` |
| `TheGame/GameScripts/Event/Combat/` | `EnemyDiedEventArgs.cs` |
| `TheGame/GameScripts/Systems/` | `LevelSystem.cs` |
| `TheGame/MainPack/Scripts/Debug/` | `LevelSmokeScenario.cs`、`UiSmokeScenario.cs`、`UiSmokeScenario.Bull.cs`、`UiSmokeScenario.Garuda.cs` |
| `Tests/ProfileTests/` | `LevelModelTests.cs` |
| `Docs/` | `CoopImplementationPlan.md`、`WorkingNotes.md`、`ProjectTour.md`、`Validation.md` |

全仓执行 `rg -n -C 2 'ShowEntityAsync|ShowOwnedEntityAsync' . --glob '*.cs'`：仍只有 ProcedureLevel、UiSmokeScenario.ShowHeroAsync、TestArenaController.SpawnHeroAsync 三处英雄请求，分别传 `HeroSpawn(P1, PlayerSpawnPosition)`、`HeroSpawn(P1, Vector2.Zero)`、`HeroSpawn(P1, _playerStart.GlobalPosition)`；本批没有新增英雄显示点。完整输出见 `hero_show_search.log`。归属字段、事件与 RecordDefeat 调用搜索见 `ownership_search.log`。

实际遇到而计划未逐文件列出的情况：通用 `UiSmokeScenario.DefeatMonsterAsync` 原用无来源攻击包模拟英雄击杀，却断言获得经验；按 C2 新契约应被视为无归属。已机械补传该方法已有的 hero.Id，并新增致死归属断言，原经验、球、升级动画断言全部保留；其他有意无来源的伤害夹具不冒认英雄。掉落、鹏魔和牛心边界均扩展在既有烟测分支/方法内，没有新增模式或生产观测入口。未遇到需改变计划方向的冲突。

完整 UI 的 stderr 中出现 18 条 `System.InvalidOperationException: Handle is not initialized`，栈为 `GCHandle.FromIntPtr → ScriptManagerBridge.SwapGCHandleForType → PackedScene.Instantiate → DefaultSceneHelper`。全验证目录搜索发现 `tangseng_hit_musou_final_20261106`、`skill_grid_full_ui`、`enemy_garuda_shapes` 三份旧日志已有同一 GCHandle/SwapGCHandleForType 错误签名，各 2 条；前者时间为 2026-11-06，原栈经 ResourceLoader.LoadThreadedGet。本次将出现位置与数量如实登记，不能称为日志无错误；未修改引擎、框架、场景或资源加载链，也未为此重复烟测。当前 C2 规则和全部运行断言均通过，该历史诊断与原退出泄漏保留待另批定位。

当前 C2 范围内无未完成项。单人正常击杀的经验、升级动画与球数量由原掉落专项和完整 UI 再次检查；只改变前述无归属边界和牛心奖励归属。正常数值全关人工实战未执行，本批不声称已完成双实体实战。所有烟测使用既有 runner 的案例专属 APPDATA，不覆盖正式玩家存档；存档仍为 v14，无结构变更或旧档重建要求。C3 须待用户接受 C2 并明确授权；球追踪/领取、地面物品归属、P2 输入和双英雄装配未开始。

## 双人C1：英雄实体携带席位（2026-11-09）

用户接受阶段 B 审查并确认开始后，按 C1-1 → C1-4 完成英雄显示参数、实体席位、身体接口和全部三处消费者迁移。当前仍只生成 P1，一套输入与 HUD，存档保持 v14。全部要求验证通过；**本批停在 C1 等待审查，不进入 C2**。

依据为实施计划第一、二部分、第六部分共同约束与 C1、第九部分占位清单及第十部分审查修正，以及规范第 00/10/30 章。复用现有 GF.Entity 显示参数、ProfileModel.HeroOf 和实体池生命周期，不新增系统、兼容入口或默认 P1 回退。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工状态与基线 | HEAD 为 **511f0c5**；已有计划与 WorkingNotes 两份用户未暂存修订，索引原为空。`dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0，**0 错误、0 警告（增量）**；`dotnet test Tests/ProfileTests --no-restore -v quiet`、`dotnet test Tests/BattleTests --no-restore -v quiet`、`dotnet test Tests/LevelTests --no-restore -v quiet`：**320/320、195/195、19/19**，均退出 0。快照与日志在 `.godot/validation/coop_c1_20261109/` |
| C1-1 完成 | 新增 HeroSpawn 后运行 `dotnet build Godot/GodotProject --no-restore -v quiet`：**退出 0，0 错误、5 条既有框架警告**；`c1_1_build.log` |
| C1-2 完成 | 实体新增 Slot、强制参数/席位校验并接管出生位置；同一构建命令：**退出 0，0 错误、5 条既有框架警告**；`c1_2_build.log`。此时旧消费者尚未迁移，未启动游戏；编译通过不等于旧入口可运行 |
| C1-3 完成 | IHeroBody 与三处假实现同步后，同一构建命令：**退出 0，0 错误、5 条既有框架警告**；`c1_3_build.log` |
| C1-4 完成 | 三处显示入口全部传 HeroSpawn 后，同一构建命令：**退出 0，0 错误、5 条既有框架警告**；`c1_4_build.log`。四步编译错误数为 **0 / 0 / 0 / 0** |
| 最终游戏构建 | 格式化后 `dotnet build Godot/GodotProject --no-restore -v quiet`：**退出 0，0 错误、5 条既有框架警告**；`final_build.log` |
| 最终规则测试 | 再次执行上述三条 `dotnet test` 命令：**退出 0，320/320、195/195、19/19**，各自失败 0、跳过 0；`final_profile.log`、`final_battle.log`、`final_level.log`。测试数量及原断言保持，既有 NU1900、CS8632/CS0649 警告保留 |
| Godot 扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c1_scan_20261109 --headless --editor --build-solutions --quit --no-window`：**退出 0**，Godot 正常生成 HeroSpawn.cs.uid；保留既有 ComponentInsoector 的 RefCounted/free 退出诊断 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出 0，2283 条序列化路径、390 个 UID**；比 B3 增加 HeroSpawn 的 1 个 UID，路径数不变；`final_references.log` |
| 测试场地入口 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c1_arena_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 600 res://TheGame/Scenes/test_arena.tscn`：**退出 0**；日志记录 `Wukong @ (120, 300)`，与 PlayerStart 原标记一致，随后显示两只怪并报告就绪，关停前无参数/席位异常。退出报告 **3 个 Shape2D RID、8 个 ObjectDB 实例、3 个资源占用**，如实保留 |
| 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c1_frontend_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**。悟空/唐僧建档、进关、保存重读、地图学技、加载取消与部分注册清理通过。坏文件、重复注册和旧作用域拒绝为预期注入；退出保留 **3 个 ObjectDB 实例**诊断 |
| 完整 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_c1_ui_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui`：**退出 0，完整 SMOKE PASS**。技能/无双、拾取/出售、15 件穿戴、朝向/动画/外观、窗口和实体复用、60/120Hz 坡地/平台、死亡/检查点、存档重入、备份恢复、加载取消和失败清理通过。坏文件及 HUD 打开失败为预期注入；退出保留 **38 个 CanvasItem RID、20 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、140 个 ObjectDB 实例、11 个资源占用**与 GL 纹理明细。前端/UI 均未设帧数强制退出，使用原 runner 的 900 秒超时与烟测完成后自行退出 |
| 格式 | `dotnet format whitespace <csproj> --no-restore --include <本批 C# 绝对路径清单> --verify-no-changes`：GodotProject **6 份**、ProfileTests **1 份**、BattleTests **2 份**，均退出 0；`format_verify_game.log`、`format_verify_profile.log`、`format_verify_battle.log`，仅测试工作区既有加载警告 |
| 差异与边界 | `git diff --check`、`git diff --cached --check` 均退出 0；9 份 C# 为 UTF-8/CRLF，无行尾空白。HEAD 保持 511f0c5；执行中用户暂存的两份原文档修订与开工快照一致，暂存差异 SHA-256 在后续操作中保持；本批增量未暂存，未执行 Git 写操作。Framework/addons/Luban、生成业务代码/资源、源表/bytes、场景、project.godot、csproj 与 FormData、SmokeTestDriver 均无本批变化 |

关键接口前后对照：

| C1 前 | C1 后 |
| --- | --- |
| 无英雄显示参数类型 | 新增 `public readonly record struct HeroSpawn(PlayerSlot Slot, Vector2 Position)`，命名空间 GameLogic.Heroes，位置为世界坐标/像素 |
| AbstractHeroEntity 无席位属性 | 新增 `public PlayerSlot Slot { get; private set; }`，每次 OnShow 覆盖，OnHide 不清空 |
| `OnShow(object userData)` 不消费英雄参数；只检查本局模型已有 HeroId | 签名不变；按顺序检查本局模型已有 HeroId → userData 是 HeroSpawn → `ProfileModel.HeroOf(spawn.Slot) == HeroId` → 写 Slot → base.OnShow。参数类型错误抛 ArgumentException，英雄不匹配抛 InvalidOperationException；非法席位沿用 HeroOf 的 ArgumentOutOfRangeException |
| 流程/测试场地在显示返回后另赋出生位置 | OnShow 的池复位段在 Velocity 旁写 `GlobalPosition = spawn.Position`；删除两处返回后的单独赋值。原朝向、待机、RESET、外观和身体状态机代码保持 |
| IHeroBody 只有 HeroId，无席位 | 在 HeroId 后新增 `PlayerSlot Slot { get; }`；三处既有测试假实现返回 P1 |

全仓搜索执行 `rg -n -C 2 'ShowEntityAsync|ShowOwnedEntityAsync' . --glob '*.cs'`（完整输出见 `hero_show_search.log`），逐项检查显示入口与转发链，仅下列三处创建英雄显示请求，其余为敌人显示、框架实现或注释，无第四处英雄消费者：

| 英雄入口 | 最终参数与位置归属 |
| --- | --- |
| `MainPack/Scripts/Procedure/ProcedureLevel.cs:161` | `ShowEntityAsync<AbstractHeroEntity>(heroConfig.EntityId, new HeroSpawn(PlayerSlot.P1, _levelViewController.PlayerSpawnPosition))` |
| `MainPack/Scripts/Debug/UiSmokeScenario.cs:1659` | `GF.Entity.ShowEntityAsync(definition.EntityId, new HeroSpawn(PlayerSlot.P1, Vector2.Zero))`；具体烟测随后设置测试位置的代码按计划保留 |
| `MainPack/Scripts/Debug/TestArenaController.cs:246` | `ShowOwnedEntityAsync(entities, _heroEntityId, new HeroSpawn(PlayerSlot.P1, _playerStart.GlobalPosition), cancellationToken)`；现有转发方法只改写 EnemySpawn，对 HeroSpawn 原样透传 |

另执行 `rg -n 'new HeroSpawn|GlobalPosition\s*=' Godot/GodotProject/TheGame/MainPack/Scripts/Procedure/ProcedureLevel.cs Godot/GodotProject/TheGame/MainPack/Scripts/Debug/TestArenaController.cs`：只有两处 HeroSpawn 和测试场地原有相机跟随赋值，英雄出生位置不再由消费者重复赋值。SmokeTestDriver 没有显示入口，未修改。

实际文件清单：**14 份（12 份修改、2 份新增），无删除**。下列 TheGame 路径相对 `Godot/GodotProject/`：

| 目录 | 文件 |
| --- | --- |
| `TheGame/GameScripts/Entity/Heroes/` | 新增 `HeroSpawn.cs`、Godot 生成的 `HeroSpawn.cs.uid`；修改 `AbstractHeroEntity.cs`、`Body/Core/IHeroBody.cs` |
| `TheGame/MainPack/Scripts/Procedure/` | 修改 `ProcedureLevel.cs` |
| `TheGame/MainPack/Scripts/Debug/` | 修改 `UiSmokeScenario.cs`、`TestArenaController.cs` |
| `Tests/BattleTests/` | 修改 `HeroBodyTests.cs`、`SkillReuseTests.cs` |
| `Tests/ProfileTests/` | 修改 `SkillStateTests.cs` |
| `Docs/` | 修改 `CoopImplementationPlan.md`、`WorkingNotes.md`、`ProjectTour.md`、`Validation.md`；保留开工已有的用户文档修订 |

任务目标已明确三处英雄调用，计划 C1-4 首句却仍写“两处”，下段才另列 TestArena；按目标与第十部分修正执行，并将该段统一为三行表格，未改变方案。HeroSpawn 无需测试编译项，两个测试工程均正常编译，未改 csproj。除此之外未遇到需改变方向的计划冲突。

单人出生位置沿用原流程/场地标记，朝向、动画和外观初始化逻辑保持，完整前端/UI 与池复用回归通过；正常数值全关人工实战未执行。本批未改存档结构，v14 档案无需重建。烟测均使用既有 runner 的案例专属 APPDATA，不覆盖正式玩家存档；历史退出诊断保留，未在本批处理。

当前 C1 范围内无未完成项。C2 的前置条件为用户接受 C1 并明确授权；经验/致死归属、掉落归属、P2 输入、第二英雄装配、双 HUD 均未开始。

## 双人B3：战斗规则证据与阶段A～B回归（2026-11-09）

用户接受 B2 后授权 B3，按 B3-1 → B3-2 完成双人战斗规则证据与阶段 A～B 完整回归。**只修改测试和进度/验证文档，生产源码与资源无变化**。新增 17 个规则案例，当前 Profile 320、Battle 195、Level 19。仍只装配一名实战英雄，存档保持 v14，无持久结构变化；本批完成后停在 B3，等待审查，不进入 C1。

依据为计划第一、二、五部分 B3 与第九部分，规范第 00/10/30 章的普通状态所有权、订退生命周期、中文注释/格式和身体状态职责。复用 TestGameScope 的双 HeroState 与既有正式业务系统；Battle 的最小 HeroTestScope 增加两人装配选项，没有新增 scope、生产入口或第三套状态。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工基线 | HEAD 为 **e3440ca**，工作区干净。`dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0、0 错误、5 条既有框架警告；`dotnet test Tests/ProfileTests --no-restore -v quiet`、`dotnet test Tests/BattleTests --no-restore -v quiet`、`dotnet test Tests/LevelTests --no-restore -v quiet` 分别 **305/305、193/193、19/19**，均退出 0。日志位于 `.godot/validation/coop_b3_20261109/baseline_*.log` |
| B3-1 完成 | 同样四条命令全部退出 0，游戏构建 **0 错误、0 警告（增量）**，规则 **320/320、195/195、19/19**。日志 `b3_1_build.log`、`b3_1_profile.log`、`b3_1_battle.log`、`b3_1_level.log`。两个测试工程也完成编译、0 错误；本批没有中间编译失败或失败测试 |
| B3-2 最终构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：**退出 0，0 错误、0 警告（增量）**，日志 `b3_2_build.log`；开工重编译时的 5 条既有框架弃用警告未处理 |
| B3-2 档案规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：**退出 0，320/320**，失败 0、跳过 0，较 B2 增加 **15** 例；`b3_2_profile.log` |
| B3-2 战斗规则 | `dotnet test Tests/BattleTests --no-restore -v quiet`：**退出 0，195/195**，失败 0、跳过 0，较 B2 增加 **2** 例；`b3_2_battle.log` |
| B3-2 关卡规则 | `dotnet test Tests/LevelTests --no-restore -v quiet`：**退出 0，19/19**，失败 0、跳过 0；`b3_2_level.log`。原有测试与断言保留，NU1900、CS8632/CS0649 既有警告保留 |
| Godot 扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b3_scan_20261109 --headless --editor --build-solutions --quit --no-window`：**退出 0**。保留既有 ComponentInsoector 的 RefCounted/free 退出诊断 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出 0，2283 条序列化路径、389 个 UID**，与 B2 相同；`b3_2_references.log` |
| 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b3_frontend_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**。两英雄分别建档/选角、进关、学技、保存返回、重读、加载取消和部分注册清理通过；坏文件、重复注册和旧作用域拒绝为预期注入。退出保留 **5 个 ObjectDB 实例、1 个资源占用**诊断 |
| 完整 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b3_ui_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui`：**退出 0，完整 SMOKE PASS**。技能/无双、拾取/出售、15 件穿戴、窗口与实体池复用、60/120Hz 坡地/平台、死亡/检查点、存档重入、备份恢复及加载取消/失败清理通过。坏文件与 HUD 打开失败为预期注入；退出保留 **20 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、138 个 ObjectDB 实例、10 个资源占用**，以及既有 CanvasItem/GL 明细。无帧数强制退出参数，仍使用原 runner 的 900 秒超时，由完整烟测完成后退出 |
| 格式与边界 | `dotnet format whitespace <csproj> --no-restore --include <本批 C# 绝对路径清单> --verify-no-changes`：ProfileTests **5 份**、BattleTests **2 份**均退出 0，仅工作区既有加载警告。`git diff --check`、`git diff --cached --check` 均退出 0；7 份 C# 为 UTF-8/CRLF，无行尾空白。HEAD 保持 e3440ca，索引为空，11 份增量均未暂存；生产源码/资源、Framework/addons/Luban、生成物、源表、bytes、场景无差异 |

B3-1 七项验收与实际证据：

| 计划要求 | 案例与断言 |
| --- | --- |
| ① 两人装备、被动、Buff 隔离；RemoveSource 只影响本人 | `TwoHeroes_KeepEquipmentPassivesAndBuffSourcesIndependent` **2 例**，交换操作英雄；通过正式拾取/穿卸和 Upgrade 为双方建立同类装备、不同被动/同名 Buff 来源，检查本人的实际暴击、攻击及生命上限变化，移除 Buff/卸装后对方完整战斗属性、生命魔法与来源保持原样 |
| ② 两人的冻结、中毒、受击保护、恢复节拍隔离；死亡只清本人 | `TwoHeroes_AdvanceAndClearOnlyPersonalEffects` **4 例**：双方各自推进冻结和毒跳相位，死亡/Reset × 两个英雄方向，其他人的冻结剩时、毒跳数/相位、生命、受击保护、无双蓄量保持；死亡者效果/保护/无双清零。`TwoHeroes_KeepHitChainsAndProtectionClocksIndependent` **2 例**核对五次保护、连击断链与两个独立时钟；`TwoHeroes_KeepRecoveryValuesAndPhasesIndependent` **2 例**核对不同恢复数值、25/24 秒节拍、相位和个人 RemoveSource |
| ③ 无双蓄积、开启、衰减、消耗隔离 | Battle 的 `TwoHeroes_KeepGainActivationAndDrainIndependent` **2 例**：交换主要操作英雄，真实 RecordHit 记账、各自自然扣点小数相位、满值开启、未满拒绝、双方同时开启的小数耗点相位、黄球恢复和耗尽；另一人的状态与相位保持 |
| ④ 同时施放不覆盖，各扣本人蓝量 | `TwoHeroes_CastSpendCancelAndClearIndependently` **2 例**，两人真实登记/学习不同技能后同时激活，核对各自 ActiveSkillId、普通/延迟耗蓝、错误技能延迟扣费拒绝、取消/无敌及另一人再次起手的费用 |
| ⑤ 一人回池清登记后另一人仍可施放 | 同上 **2 例**调用与 AbstractHeroEntity.OnHide 相同的正式 `ClearSkillDefinitions(heroId)`，清理者正在施放且拥有无敌区间，核对本人登记/施放/无敌清除，另一人收招后可再次激活并只扣本人蓝量。B2 的原 `RegistrationAndClear_OnlyAffectRequestedHero` 保留。完整 UI 另验证真实单人实体池回收/复用；生产 OnHide 仍直接传 HeroId。本批不生成第二个实战实体，不把纯规则测试称为双实体实战 |
| ⑥ 重建后旧状态不被订阅更新，逐人对称退订 | `TwoHeroes_RestartDetachesAllOldCallbacksAndStates` **1 例**，连续重建两轮，每人核对三个旧委托已从成长/穿戴/被动的真实 Snapshot 中移除，新委托各注册 1 次；旧容器清空、旧状态不变；随后正式经验/换装/被动操作只更新新状态 |
| ⑦ 非阵容英雄登记拒绝 | `Registration_RejectsHeroOutsideRosterWithoutMutation` **2 例**，使用配置合法但阵容外的另一英雄，确认 ArgumentException、原登记/正在施放的编号与蓝量不变、没有创建额外 HeroState |

两个角色均作为被操作方和被保留方，而不是只验证 P1 修改。测试使用真实成长、被动、装备、技能配置与正式业务；Battle 无双仍复用已有零基础值夹具，只扩展到两份状态，不复制规则实现。

实际接口变化：

| 交付前 | 交付后 |
| --- | --- |
| 测试夹具 `HeroTestScope()` | `HeroTestScope(bool twoHeroes = false)`；默认行为保持，选项只在测试中创建两份 HeroState/技能书并共用正式系统 |
| TestGameScope、HeroModel、HeroState、HeroSystem、HeroStatusSystem、IHeroBody、SkillSystem | **本批全部无接口/源码变化**；TestGameScope 已能按阵容创建两份状态，直接复用 |

本批没有发现需要修改生产代码或改变计划的缺陷。B1 的逐人闭包/死亡 Reset 个人效果清理、B2 的个人登记清理均由新增案例检查。针对旧状态不变这一证据的局限，直接在现有测试内反射读取绑定 Snapshot：旧闭包会重新取 Architecture 当前模型，仅比较旧 HeroState 无法发现其泄漏；因此同时检查实际旧委托消失、新委托唯一。没有为观测增加生产成员、事件或测试辅助类型。

实际文件清单：**11 份修改（7 份测试 C#、4 份文档），无新增、无删除正式文件**。

| 所属目录 | 修改文件 |
| --- | --- |
| `Tests/ProfileTests/` | `PassiveSkillTests.cs`、`FreezeTests.cs`、`HurtProtectionTests.cs`、`RecoveryTests.cs`、`SkillSystemTests.cs` |
| `Tests/BattleTests/` | `HeroTestScope.cs`、`MusouTests.cs` |
| `Docs/` | `CoopImplementationPlan.md`、`ProjectTour.md`、`Validation.md`、`WorkingNotes.md` |

生产代码、生成物、框架、源表、场景和存档版本均未变化。阶段 A 的个人档案/钱包/背包、v14 双人往返和原子恢复案例全部重跑；完整前端/UI 验证当前单人玩法、数值、存档往返与 B2 基线一致。双人规则证据不等于双人实战，第二实体、席位输入和双 HUD 仍待后续批次。正常数值全关人工实战本批未执行。烟测继续使用既有 runner 的案例专属 APPDATA/Validation 存档，不覆盖正式玩家存档；未执行 Git 写操作。

C1 须待用户接受 B3 并明确授权后开始，其范围为实体席位/出生参数，不提前实现 C2 的致死归属或 C4 的第二套键位。当前范围内无未完成项。

## 双人B2：技能登记与施放按英雄（2026-11-09）

用户授权在 B1 基础上完成 B2。SkillSystem 的技能登记改为 HeroId → 技能表，SelectSkills 只替换本人，AbstractHeroEntity.OnHide 只撤销本人的登记、施放和无敌区间；系统退出保留无参全局清理。仍只生成 P1，输入/HUD、技能行为、学技和存档格式 v14 保持原状；**本批完成后停在 B2，不进入 B3**。

依据：实施计划第一、二、五部分 B2 和第九部分；第 00 章所有权/生命周期，第 10 章方法命名、中文注释与格式，第 30 章招式与实体生命周期。沿用原 SkillSystem、HeroState 与 Dictionary，没有新增系统、类型、事件、生产观测入口或兼容重载。规范和计划只同步当前实现事实。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工状态与基线 | HEAD 为 fe0bdbc；B1 的 **99 份差异全部已暂存、无未暂存修改**。开工运行 `dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0、0 错误/0 警告（增量）；三套规则 **304/304、193/193、19/19**，均退出 0。日志 `.godot/validation/coop_b2_20261109/baseline_*.log` |
| 逐步编译 | B2 计划没有编号子任务；技能系统/实体清理实现后 `dotnet build Godot/GodotProject --no-restore -v quiet`：**0 错误、5 条既有框架警告**（`b2_system_build.log`）；夹具与 B2 回归同步后同命令：**0 错误、5 条既有框架警告**（`b2_consumers_build.log`）。本批没有中间编译错误或失败测试 |
| 最终游戏构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：**退出 0，0 错误、0 警告（增量）**，日志 `final_build.log`；重编译时的 5 条既有框架弃用警告未处理 |
| 档案规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：**退出 0，305/305**，失败 0、跳过 0（`final_profile.log`）。新增 1 项 B2 登记替换/清理回归；原 304 项及断言保留 |
| 战斗规则 | `dotnet test Tests/BattleTests --no-restore -v quiet`：**退出 0，193/193**，失败 0、跳过 0（`final_battle.log`）；保留既有 CS8632/CS0649 |
| 关卡规则 | `dotnet test Tests/LevelTests --no-restore -v quiet`：**退出 0，19/19**，失败 0、跳过 0（`final_level.log`）；测试工程 NU1900 网络警告保留 |
| Godot 扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b2_scan_20261109 --headless --editor --build-solutions --quit --no-window`：**退出 0**，保留既有 ComponentInsoector 的 RefCounted/free 退出诊断 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出 0，2283 条序列化路径、389 个 UID**（`final_references.log`），与 B1 相同 |
| 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b2_frontend_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**。建档/选角、进关、地图学技、保存返回、重读、加载取消与部分注册清理通过；坏文件与重复注册为预期注入。退出保留 **5 个 ObjectDB 实例、1 个资源占用**诊断 |
| 完整 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b2_ui_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui`：**退出 0，完整 SMOKE PASS**。技能/无双、拾取出售、15 件穿戴、窗口与实体池复用、60/120Hz 坡地/平台、死亡/检查点、备份回退、存档重入及加载取消/失败清理通过。坏文件与 HUD 打开失败为预期注入；退出保留 **20 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、141 个 ObjectDB 实例、11 个资源占用**以及既有 CanvasItem/GL 明细。使用原 runner 的 900 秒超时，由完整烟测完成后退出 |
| 格式与最终差异 | `dotnet format whitespace <csproj> --no-restore --include <本批 C# 绝对路径清单> --verify-no-changes`：GodotProject **2 份**、ProfileTests **3 份**均退出 0（仅测试工作区既有加载警告）。`git diff --check`、`git diff --cached --check` 均退出 0；5 份 C# 为 UTF-8/CRLF、无行尾空白。10 份增量均未暂存；执行中外部将 B1 提交为 53e9b11，经二进制差异 SHA-256 比对，与开工时暂存的 B1 完全一致，当前索引为空。未执行 Git 写操作；Framework/addons/Luban、生成物、源表、bytes、场景无本批修改 |

关键接口及行为对照：

| B1 交付状态 | B2 交付状态 |
| --- | --- |
| `Dictionary<SkillId, SkillDefinition> _skillDefinitions` | `Dictionary<int, Dictionary<SkillId, SkillDefinition>> _skillDefinitions`，按 HeroId 分表 |
| `void SelectSkills(int heroId, params SkillDefinition[] definitions)` 清空共享登记 | 签名不变；先完整校验，再只取消该 heroId 的施放、替换其登记；同组重登仍不打断 |
| `void ClearSkillDefinitions()` 清全部；实体回池也调用此方法 | 新增 `void ClearSkillDefinitions(int heroId)` 取消本人并移除本人表；无参版本保留，生产调用仅为 OnDeinit |
| `Type GetBodyStateType(int heroId)`、`bool IsAvailable(int heroId, SkillId id)` 读共享表 | 签名不变，读取本人表；没有登记时按既有查询结果返回 null/false |
| `bool CanAfford(int heroId, SkillId id)` 只比较蓝量 | 签名不变，先确认本人表登记，再比较本人蓝量；不能因他人登记或蓝量足够而误报可用 |
| `bool TryActivate(int heroId, SkillId id, ISkillActor actor)` / `bool TrySpendActivatedMana(int heroId, SkillId id)` / `bool TryActivateInput(int heroId, ISkillActor actor, HeroActionData data)` | 签名不变，起手条件、延迟耗蓝和输入枚举均使用本人表；输入仍按登记顺序，失败不排队、不补发 |
| `void CancelSkill(int heroId)` / `void EndSkill(int heroId, SkillId id)` / `void GrantInvulnerability(int heroId, double from, double duration)` / `bool IsInvulnerable(int heroId, double now)` | 签名及按本人 HeroState 处理的行为不变；清除后仍可取消/退出，不人为用登记存在性阻挡清理或独立无敌区间 |
| `AbstractHeroEntity.OnHide` 调 `ClearSkillDefinitions()` | 调 `ClearSkillDefinitions(HeroId)`，真实回池不会清掉另一人的登记或施放 |
| `HeroModel`、`HeroState`、`HeroSystem`、`HeroStatusSystem`、`IHeroBody` | **本批均无接口/源码变化**，沿用 B1 的 GetState(heroId)、普通状态和显式编号契约 |

B1 的两处缺陷已在上一批修正，本批没有重复实现或改动：①HeroSystem 为每位英雄保存三个实际闭包、以同一编号/委托对称退订；②Damage 死亡与 Reset 使用 ClearAll(heroId)，只清本人状态效果。其证据见下方 B1 记录；本批复跑已有规则和单人流程。B2 新修复的是**实体回池清全表**，清理边界已与逐人状态对应。

实际遇到的调用与处理：

- 所有原校验保留：参战阵容包含该编号、非空数组、每个定义非 null、具体 AbstractHeroSkillState 类型、配置归属、技能编号唯一、输入动作非空白且本人组内唯一。先完整校验再修改；不加默认英雄、不增加第二份学习状态。
- 个人表由调用方数组内的不可变 SkillDefinition 复制建立，SelectSkills 仅替换该 heroId 的字典；相同定义序列保持当前施放，两个英雄允许相同输入动作名。TryActivate 在 IsAvailable 确认本人登记后直接读取其定义。
- TestGameScope.RestartHero 在结束数值模型前按 Profile.HeroIds 逐人清理，模拟实体逐人退场。SkillLearningTests 的地图清理也改用个人入口，保留地图无 HeroModel 时的既有行为。SkillStateTests、SkillReuseTests 已在 B1 显式传 HeroId，经核对本批无需额外修改；三套测试全部复跑。
- 新增 `RegistrationAndClear_OnlyAffectRequestedHero`，在现有 TestGameScope 和 SkillSystemTests 内使用正式系统，覆盖两人同名动作、第二人登记/替换不改第一人施放与蓝量、个人清理同时清登记和无敌、第一人之后仍能通过输入继续施放、系统 Deinit 全局清理。未增加测试辅助类型或生产入口；B3 的全面双人属性/效果/无双及实际双实体回池验收仍待授权。

本批文件清单：**10 份修改（2 份生产 C#、3 份测试 C#、5 份文档），无新增、无删除正式文件**。只列相对开工时已暂存 B1 的增量：

| 所属目录 | 修改文件 |
| --- | --- |
| `Godot/GodotProject/TheGame/GameScripts/` | `Systems/SkillSystem.cs`、`Entity/Heroes/AbstractHeroEntity.cs` |
| `Tests/ProfileTests/` | `SkillSystemTests.cs`、`SkillLearningTests.cs`、`TestGameScope.cs` |
| `Docs/` | `CoopImplementationPlan.md`、`ProjectGuidelines/70-ProfileAndSave.md`、`ProjectTour.md`、`Validation.md`、`WorkingNotes.md` |

本批没有持久结构变化，存档继续 v14，不需要因 B2 重建现有 v14 档案。仍只装配一个实战英雄，一套输入/HUD；B3 须待用户接受 B2 并授权后补齐全面隔离证据和阶段 A～B 回归，本批不提前开始。所有引擎验证使用原 runner 的案例专属 APPDATA 与 Validation 存档，未覆盖正式玩家存档。正常数值全关人工实战、双人实战均未执行，退出时既有引擎资源诊断单独记录。

## 双人B1：本局状态按英雄承载（2026-11-09）

按已授权目标与实施计划 B1-1～B1-6 顺序完成本局状态迁移，并响应用户追加的全项目业务方法命名要求。HeroModel 只保存 HeroId → HeroState，个人系统、实体、身体/技能状态和 UI 按本人 HeroId 读写；仍只生成 P1，保留一套输入/HUD。**当前停在 B1 等待审查，不进入 B2。** 存档保持 v14，本批没有持久结构变化，不要求重建现有 v14 开发档；v13 及更早档的既有重建规则不变。

依据：计划第一、二、五、九部分，规范第 00 章的普通子数据/单根 Architecture/所有权，第 10 章的命名与中文注释，第 30 章的实体与战斗职责、第 70 章的档案边界。用户明确授权同步规范和计划；本批同步当前归属与 GetState 等签名，没有改变后续批次方向。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工基线 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误；三套规则测试 **304/304、193/193、19/19**。日志位于 `.godot/validation/coop_b1_20261108/baseline_*.log` |
| 子任务编译 | 每步执行 `dotnet build Godot/GodotProject --no-restore -v quiet`；B1-1～B1-6 错误依次为 **0、597、588、589、444、0**，各步最终日志为同目录 `b1_1_build.log`～`b1_6_build.log`。中间迁移失败按授权保留记录，不加兼容转发；B1-6 清零 |
| 最终游戏构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：**退出 0，0 错误、5 条既有框架弃用警告**。已包含最终方法改名，日志 `renamed_build.log` |
| 档案规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：**退出 0，304/304**，失败 0、跳过 0，日志 `renamed_profile.log` |
| 战斗规则 | `dotnet test Tests/BattleTests --no-restore -v quiet`：**退出 0，193/193**，失败 0、跳过 0，日志 `renamed_battle.log` |
| 关卡规则 | `dotnet test Tests/LevelTests --no-restore -v quiet`：**退出 0，19/19**，失败 0、跳过 0，日志 `renamed_level.log`。测试数量与基线一致，保留 NU1900、CS8632/CS0649，未删减断言 |
| Godot 扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b1_final_scan_20261109 --headless --editor --build-solutions --quit --no-window`：**退出 0**。保留既有 ComponentInsoector 的 RefCounted/free 退出诊断 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出 0，2283 条序列化路径、389 个 UID**，日志 `renamed_references.log`。比 A3 增加 HeroState 的脚本 UID |
| 最终前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b1_final_frontend_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**。覆盖选角、进关、装备/被动/成长观察、保存返回、重读、加载取消与部分注册清理；三个实际回调均验证进入时 1 次、退出后 0 次。旧作用域拒绝、坏文件、重复注册为预期注入；退出有 **5 个 ObjectDB 实例、1 个资源占用**诊断 |
| 最终 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_b1_final_ui_20261109 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui`：**退出 0，完整 SMOKE PASS**。包含全部 18 处命名整理后的版本；验证技能/无双、拾取/出售、15 件穿戴、窗口与实体复用、60/120Hz 坡地及平台、死亡/检查点、存档重入与备份回退、加载取消和失败清理。预期坏文件与 HUD 打开失败注入保留；退出保留 **20 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、141 个 ObjectDB 实例、11 个资源占用**诊断，以及既有 CanvasItem/GL 纹理明细。最终使用原 runner 的 900 秒超时，由烟测完成后退出，不以帧数上限截断 |
| 格式 | 使用 `$verifyFiles = @(git diff HEAD --name-only -- '*.cs' \| Where-Object { $_.StartsWith('<工程目录>/') } \| ForEach-Object { (Resolve-Path -LiteralPath $_).Path })` 取得各工程的明确文件清单，再运行 `dotnet format whitespace <csproj> --no-restore --include $verifyFiles --verify-no-changes`；GodotProject **70 份**、ProfileTests **13 份**、BattleTests **5 份** C# 均退出 0，仅测试工作区既有加载警告。88 份 C# 均为 UTF-8/CRLF，无行尾空白 |
| 差异与边界 | `git diff --check`、`git diff --cached --check` 均退出 0；完整源文件对比证明 HeroState 除类名/说明、只读 HeroId 与构造外和迁移前 HeroModel 逐字一致。只读目录、生成代码/资源、源表、bytes、场景无差异，新增脚本的 .cs.uid 由编辑器正常生成。开工仅两份文档已暂存，执行中外部又暂存 B1 的 74 份差异；命名整理前后索引指纹一致，未执行任何 Git 写操作 |

关键接口前后对照（表内省略访问级别，未变化的参数与默认值仍列出）：

| 所属类型 / 修改前 | 修改后 |
| --- | --- |
| HeroModel 直接持有单个英雄全部数值、效果、计时与施放字段 | 普通 `HeroState(int heroId)` 持有原字段与嵌套 StatusEffectKind/StatusEffectState；HeroId 为构造时确定的只读属性，其他初值/计算/方法体保持原样 |
| 无本局状态索引入口 | `IReadOnlyDictionary<int, HeroState> Heroes`、`HeroState GetState(int heroId)`、`internal HeroState CreateState(int heroId)`、`internal void ClearStates()`；无 Remove、默认英雄或标量转发 |
| IHeroBody 无英雄编号 | 新增 `int HeroId { get; }`，由实体和三处假实现提供 |
| HeroSystem：`float MoveSpeedMultiplier` | `float GetMoveSpeedMultiplier(int heroId)` |
| HeroSystem：`float JumpSpeedMultiplier` | `float GetJumpSpeedMultiplier(int heroId)` |
| HeroSystem：`float GravityMultiplier` | `float GetGravityMultiplier(int heroId)` |
| HeroSystem：`float AttackPowerMultiplier` | `float GetAttackPowerMultiplier(int heroId)` |
| HeroSystem：`bool Damage(int amount)` | `bool Damage(int heroId, int amount)` |
| HeroSystem：`void Heal(int amount)` | `void Heal(int heroId, int amount)` |
| HeroSystem：`void RecordHurt()` | `void RecordHurt(int heroId)` |
| HeroSystem：`void AdvanceHurtProtection(float delta)` | `void AdvanceHurtProtection(int heroId, float delta)` |
| HeroSystem：`int AdvanceRecovery(float delta)` | `int AdvanceRecovery(int heroId, float delta)` |
| HeroSystem：`bool TrySpendMp(int amount)` | `bool TrySpendMp(int heroId, int amount)` |
| HeroSystem：`void RestoreMp(int amount)` | `void RestoreMp(int heroId, int amount)` |
| HeroSystem：`void Reset(int heroId, int level, ActorBaseAttributes baseAttributes, EquipmentData weaponData = null, EquipmentData armorData = null, EquipmentData accessoryData = null, EquipmentData treasureData = null)` | `void Reset(int heroId, int level, ActorBaseAttributes baseAttributes, EquipmentData weaponData = null, EquipmentData armorData = null, EquipmentData accessoryData = null, EquipmentData treasureData = null)` |
| HeroSystem：`void SetGrowth(ActorBaseAttributes baseAttributes, int heroId, int level, EquipmentData weaponData, EquipmentData armorData, bool refill, EquipmentData accessoryData = null, EquipmentData treasureData = null)` | `void SetGrowth(int heroId, ActorBaseAttributes baseAttributes, int level, EquipmentData weaponData, EquipmentData armorData, bool refill, EquipmentData accessoryData = null, EquipmentData treasureData = null)` |
| HeroSystem：`void SetSource(AttributeSourceId source, IReadOnlyList<AttributeModifier> modifiers)` | `void SetSource(int heroId, AttributeSourceId source, IReadOnlyList<AttributeModifier> modifiers)` |
| HeroSystem：`bool RemoveSource(AttributeSourceId source)` | `bool RemoveSource(int heroId, AttributeSourceId source)` |
| HeroSystem：`void RecordHit(DamageResult result)` | `void RecordHit(int heroId, DamageResult result)` |
| HeroSystem：`void ResetWs(int max, float decayPerSecond, float drainPerSecond)` | `void ResetWs(int heroId, int max, float decayPerSecond, float drainPerSecond)` |
| HeroSystem：`void AddWs(int amount)` | `void AddWs(int heroId, int amount)` |
| HeroSystem：`void AddWsFromPickup(int amount)` | `void AddWsFromPickup(int heroId, int amount)` |
| HeroSystem：`bool TryActivateWs()` | `bool TryActivateWs(int heroId)` |
| HeroSystem：`void SetControlsEnabled(bool enabled)` | `void SetControlsEnabled(int heroId, bool enabled)` |
| HeroSystem：`void AdvanceWs(float delta)` | `void AdvanceWs(int heroId, float delta)` |
| HeroSystem：`void ClearWs()` | `void ClearWs(int heroId)` |
| HeroStatusSystem：`bool IsFrozen` | `bool IsFrozen(int heroId)` |
| HeroStatusSystem：`bool IsPoisoned` | `bool IsPoisoned(int heroId)` |
| HeroStatusSystem：`void ApplyFreeze(float seconds)` | `void ApplyFreeze(int heroId, float seconds)` |
| HeroStatusSystem：`void AdvanceFreeze(float delta)` | `void AdvanceFreeze(int heroId, float delta)` |
| HeroStatusSystem：`void ApplyPoison(AttackConfig attack)` | `void ApplyPoison(int heroId, AttackConfig attack)` |
| HeroStatusSystem：`int AdvancePoison(float delta)` | `int AdvancePoison(int heroId, float delta)` |
| SkillSystem：`Type BodyStateType` | `Type GetBodyStateType(int heroId)` |
| SkillSystem：`bool CanAfford(SkillId id)` | `bool CanAfford(int heroId, SkillId id)` |
| SkillSystem：`bool TryActivate(SkillId id, ISkillActor actor)` | `bool TryActivate(int heroId, SkillId id, ISkillActor actor)` |
| SkillSystem：`bool TrySpendActivatedMana(SkillId id)` | `bool TrySpendActivatedMana(int heroId, SkillId id)` |
| SkillSystem：`bool TryActivateInput(ISkillActor actor, HeroActionData data)` | `bool TryActivateInput(int heroId, ISkillActor actor, HeroActionData data)` |
| SkillSystem：`void CancelSkill()` | `void CancelSkill(int heroId)` |
| SkillSystem：`void EndSkill(SkillId id)` | `void EndSkill(int heroId, SkillId id)` |
| SkillSystem：`void GrantInvulnerability(double from, double duration)` | `void GrantInvulnerability(int heroId, double from, double duration)` |
| SkillSystem：`bool IsInvulnerable(double now)` | `bool IsInvulnerable(int heroId, double now)` |
| HeroStatusSystem：`void ClearAll()` 原为单人 | 新增 `void ClearAll(int heroId)` 清本人；无参方法遍历全部状态，仅用于系统退出 |
| AbstractHeroEntity：`private HeroModel Attributes` | `private HeroState Attributes => this.GetModel<HeroModel>().GetState(HeroId)`；反射契约名 Attributes 保留，OnShow 先检查 Heroes.ContainsKey |

HeroSystem 私有 ClearHurtProtection、ReplacePassiveSource、ReplaceEquipmentSources、ApplyFinalAttributes、SetWsState 接收 HeroState，Recalculate 与三个来源回调显式传 heroId。Reset 原本已有 heroId，签名保持；SetGrowth 将原第二位 heroId 移到首位。五个 AttributeSourceId 常量仍用第二项 0，各英雄自己的 Sources 字典提供隔离。

用户追加命名整理（全项目自有 C#、测试与工具入口检索，按编译符号替换，18 处方法声明；不改行为、不留旧名转发）：

| 原名 | 现名与用途 |
| --- | --- |
| HeroModel.Get / Create / Clear（B1 原计划临时签名） | GetState / CreateState / ClearStates，明确状态对象 |
| ItemAppearancesResource.Get（EquipmentId / PickupId 两重载）、私有 Find | GetAppearance / FindAppearance，明确外观资源 |
| EquipmentAttributes.Get、ActorBaseAttributes 的 AttributeExtensions.Get | GetValue，与既有 WithValue 成对，读取指定属性数值；不修改生成类型 |
| ExperienceCurveUtility.Add / Evaluate | AddExperience / EvaluateProgress，明确经验累计与等级进度计算 |
| NodePool.Get<T> / Get、私有 GetInternal | RentNode<T> / RentNode / RentNodeInternal，明确取池并挂接；避免与 Godot.Node.GetNode 混淆 |
| B1 新参数化方法 MoveSpeedMultiplier / JumpSpeedMultiplier / GravityMultiplier / AttackPowerMultiplier / BodyStateType | 各补 Get 前缀，仍为带 heroId 的原查询；IHeroBody 上的只读属性名保留 |

第 10 章新增方法语义规则，计划 B1 与第九部分同步。集合 Add/Remove/Clear、控件自身 Refresh/Clear、静态类型工厂 Create、池回调 OnGet/OnRelease、接口/重写及生成 API 保留既有契约；没有把简单动词机械扩成冗长名称。生产与测试中已无自有裸 Get 声明；Luban 表和 Godot/GGF 查询仍调用原 API。检索 `NodePool.Instance.Get`、`GetInternal`、字面量方法名和旧 Evaluate 调用无残留。

本批修复的两处多人缺陷：

- 三个来源订阅按每个 heroId 建立独立闭包，按 priority 100 注册并把实际委托存入 _subscriptions；退出逐个按同一编号/委托退订，清空订阅与状态容器。复用了既有 Bindable 注册机制。
- Damage 死亡分支与 Reset 使用 ClearAll(heroId)，只清本人的冰冻/中毒；无双和受击保护也按该英雄清理。全局 ClearAll 仅保留在退出路径。

计划未单列的调用与实际处理：

- SkillSystem 的 SelectSkills 保持原签名，内部 CancelSkill 改传其已有 heroId；全局 ClearSkillDefinitions 暂遍历已存在状态取消施放，仍清共享登记表。共享表及 AbstractHeroEntity.OnHide 的全局清理留给 B2，没有提前按英雄拆登记。
- 原 SmokeInspection 按 handler.Target 等于 HeroSystem 统计订阅，闭包迁移后这一观测失效。复用原方法的 object 参数接受实际 Delegate，既有对象匹配保留；前端取得 _subscriptions 内的三个委托，断言各自只订一次且退订为零，没有生产测试入口。
- 窗口关闭已把 _formData/_inventoryData 置空，退订必须使用关闭前捕获的局部 data.Hero.HeroId；刷新仍用本周期字段。早期前端因此失败，修正后最终完整前端通过。
- UI 实体复用原断言把实体的 Attributes 与 HeroModel 直接比较；迁移后分别验证容器仍为原容器、Attributes 仍为容器 GetState(heroId) 的同一对象，同时保留实体、无双和资源断言。早期 UI 在此失败，修正后改名前完整 UI 以及最终版本均重新运行。
- 新建 HeroState.cs 已加入 ProfileTests 显式编译项；BattleTests 的 Models 通配无需改。三处 IHeroBody 假实现补实际夹具编号；HeroTestScope、SkillScope、TestGameScope 的公开 Hero 为 P1 HeroState，私有容器负责注册和重建，没有新增测试辅助类型。Debug 仅方法内局部复用 heroId，无共享取 P1 辅助方法。
- 机械消费者还包括身体/技能、唐僧动画入口、独立技能命中、实际受击目标的冻结/中毒、关卡拾取、库存面板、Debug 反射与烟测；独立技能按 Source.OwnerEntityId 解析本体英雄记账，未改命中规则。只有 B1 负责的两个 P1 占位被清除；龙宫转场仍按计划显式 P1，其他后续批次占位保持。
- 中间构建、Profile 夹具漏传参数、前端闭包观察和 UI 身份比较的失败均有日志，已全部解决；一次旧二进制前端运行发生在构建失败后，未作为通过证据。最终证据为 named 系列及 renamed 日志。
- 改名后首次完整 UI 案例 `coop_b1_named_ui_20261109` 使用 `--quit-after 30000`，退出 0 但停在大猩猩截图阶段，没有最终 SMOKE PASS；强制结束时出现 BattleForm.Unbind 的架构已结束诊断。因此该次不计完整通过，保留日志并另以不带帧数退出参数的案例重跑，仍使用原 runner 的 900 秒超时，不改任何断言。

实际文件清单：**99 份相对 HEAD 的差异（88 份 C#、9 份文档、1 份 csproj、1 份 UID）**，其中 2 份新增（HeroState.cs 及其 Godot 扫描生成的 .cs.uid），无删除。包含用户原有计划/工作笔记修订；其余为本批实现、命名调用方与记录。

| 所属目录 | 文件（新增用“新增”标记） |
| --- | --- |
| `Docs/` | `CoopImplementationPlan.md`、`ProjectGuidelines/00-Architecture.md`、`ProjectGuidelines/10-CSharp.md`、`ProjectGuidelines/30-EntitiesAndCombat.md`、`ProjectGuidelines/60-GameplayModules.md`、`ProjectGuidelines/70-ProfileAndSave.md`、`ProjectTour.md`、`Validation.md`、`WorkingNotes.md` |
| `Godot/GodotProject/TheGame/GameScripts/` | `Entity/Drops/GroundItemNode.cs`、`Entity/Effects/SkillEffectNode.cs`、`Entity/Enemies/AbstractEnemyEntity.Attacks.cs`、`Entity/Enemies/AbstractEnemyEntity.cs`、`Entity/Enemies/Bosses/zm1_05_qixing_cave/LionCamelPoisonCloudNode.cs`、`Entity/Enemies/Bosses/zm1_10_bull_demon_valley/BullDemonHeartEntity.cs`、`Entity/Enemies/Bosses/zm1_11_hidden_dragon_palace/DragonKingEntity.cs`、`Entity/FloatingText/FloatingTextPresenter.cs`、`Entity/Heroes/AbstractHeroEntity.Capture.cs`、`Entity/Heroes/AbstractHeroEntity.Skills.cs`、`Entity/Heroes/AbstractHeroEntity.cs`、`Entity/Heroes/Body/Core/AbstractHeroBodyState.cs`、`Entity/Heroes/Body/Core/AbstractHeroSkillState.cs`、`Entity/Heroes/Body/Core/IHeroBody.cs`、`Entity/Heroes/Tangseng/Skills/TangsengIceDragonState.cs`、`Entity/Heroes/Tangseng/TangsengEntity.AnimationEvents.cs`、`Entity/Heroes/Tangseng/TangsengEntity.cs`、`Entity/Heroes/Wukong/Skills/WukongBlazingFlashState.cs`、`Entity/Heroes/Wukong/Skills/WukongFireDemonState.cs`、`Entity/Heroes/Wukong/Skills/WukongFlameThrustState.cs`、`Entity/Heroes/Wukong/WukongEntity.cs`、`Entity/Level/LevelViewController.Entrance.cs`、`Entity/Level/LevelViewController.Loot.cs`、`Entity/Level/LevelViewController.cs`、`Models/HeroModel.cs`、`Models/HeroState.cs`（新增）、`Models/HeroState.cs.uid`（新增）、`Models/Profile/EquipmentAttributes.cs`、`Models/Profile/ItemAppearancesResource.cs`、`Systems/Combat/AttributeExtensions.cs`、`Systems/ConfigurationSystem.cs`、`Systems/Hero/HeroStatusSystem.cs`、`Systems/Hero/HeroSystem.cs`、`Systems/LevelSystem.cs`、`Systems/ProfileSystem.cs`、`Systems/SaveSystem.cs`、`Systems/SkillSystem.cs`、`UI/BattleForm/BattleForm.Logic.cs`、`UI/InventoryForm/Components/InventoryBagPanel.cs`、`UI/InventoryForm/Components/InventoryCharacterPanel.cs`、`UI/InventoryForm/InventoryForm.Logic.cs`、`UI/MapForm/Components/LevelGuideTooltip.cs`、`Utilities/Utility/ExperienceCurveUtility.cs`、`Utilities/Utility/SaveDataUtility.cs` |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/` | `EnemyAiSmokeScenario.cs`、`FrontendSmokeScenario.cs`、`LevelSmokeScenario.cs`、`SmokeInspection.cs`、`UiSmokeScenario.BatCave.cs`、`UiSmokeScenario.Bosses.cs`、`UiSmokeScenario.Bull.cs`、`UiSmokeScenario.CombatFeedback.cs`、`UiSmokeScenario.Dragon.cs`、`UiSmokeScenario.DragonRules.cs`、`UiSmokeScenario.EastSea.cs`、`UiSmokeScenario.EnemyFidelity.cs`、`UiSmokeScenario.Equipment.cs`、`UiSmokeScenario.FireDemon.cs`、`UiSmokeScenario.Garuda.cs`、`UiSmokeScenario.HeroPresentation.cs`、`UiSmokeScenario.Inventory.cs`、`UiSmokeScenario.Passives.cs`、`UiSmokeScenario.Qixing.cs`、`UiSmokeScenario.Recovery.cs`、`UiSmokeScenario.Sea.cs`、`UiSmokeScenario.Shark.cs`、`UiSmokeScenario.Skills.cs`、`UiSmokeScenario.StatusEffects.cs`、`UiSmokeScenario.Tangseng.cs`、`UiSmokeScenario.cs` |
| `Godot/GodotProject/TheGame/MainPack/Scripts/` | `ObjectPool/NodePool.cs` |
| `Tests/BattleTests/` | `HeroBodyTests.cs`、`HeroModelTests.cs`、`HeroTestScope.cs`、`MusouTests.cs`、`SkillReuseTests.cs` |
| `Tests/ProfileTests/` | `EquipmentTests.cs`、`ExperienceCurveTests.cs`、`FreezeTests.cs`、`HurtProtectionTests.cs`、`LevelRewardTests.cs`、`MusouTests.cs`、`PassiveSkillTests.cs`、`PoisonTests.cs`、`ProfileTests.cs`、`ProfileTests.csproj`、`RecoveryTests.cs`、`SkillStateTests.cs`、`SkillSystemTests.cs`、`TestGameScope.cs` |


当前范围内无未完成项。B2 必须等待用户接受 B1 并授权，处理共享技能登记和一人回池清全表；双人隔离专项按既定 B3 补充，本批未运行双人实战或正常数值全关人工验收。引擎退出诊断保持既有边界，不处理只读依赖；烟测使用 run_godot.py 的案例专属 APPDATA 与 Validation 存档，不覆盖正式玩家存档。未执行 Git 写操作。

## 双人A3：档案规则证据与存档格摘要（2026-11-08）

按用户授权完成 A3-1～A3-2：补齐双人个人数据隔离、完整 v14 往返及恢复前校验的规则证据；六格目录发布按席位排列的只读摘要，catalogue 名以单空格连接全员配置名称。**当前停在 A3 等待用户审查，B1 未开始。** 依据为实施计划 A3、规范第 00 章的状态所有权、第 10 章的只读值/中文注释和格式、第 70 章的目录与检查点契约。

标题宽度曾触发计划的停止条件，现已按用户裁决解决：单/双人统一只显示角色名，不显示等级。项目实际 NameCn 为“悟空”“唐僧”，不新增简称映射；当前双人标题“悟空 · 唐僧”宽 **132px**，加入两人的最低等级仍需 **220px**。沿用 **22 号字、187×30px 标题区**，正文终点早于删除按钮；单人显示“悟空”，双人显示“悟空 · 唐僧”，均不显示等级，日期样式不变。没有修改字体、场景、节点或布局。造三存档格以角色名作为标题的既有官方依据见 OfficialReference；双人摘要始终保留双方等级，只在该窄标题中省略。

开工时 A2 的 48 份文件已由用户暂存，工作区无未暂存差异；执行期间其提交为 `c2f74db`，已按 SHA-256 核对提交差异与开工暂存内容完全一致。本轮 A3 改动均保留在未暂存工作区，未执行 Git 写操作。**存档格式仍为 v14，本批不要求重建现有 v14 档。** v13 及更早开发档仍按现有契约拒绝，无迁移分支。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工基线 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0、0 错误、增量 0 警告；`dotnet test Tests/ProfileTests --no-restore -v quiet`：**292/292**。日志在 `.godot/validation/coop_a3_20261108/baseline_*.log` |
| 子任务与最终构建 | A3-1、A3-2 与最后一次源码修改后均执行 `dotnet build Godot/GodotProject --no-restore -v quiet`：均**退出 0、0 错误、5 条既有框架弃用警告**。日志 `a3_1_build.log`、`a3_2_build.log`、`final_build.log`；统一纯名称后的最终日志为 `final_names_only_build.log` |
| 档案规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：**退出 0，304/304**，失败 0、跳过 0。A3-1 为 303/303，A3-2 增加摘要只读发布证据；相比开工共增加 **12 例**，原有断言保留。最终日志 `a3_2_profile.log` |
| 战斗规则 | `dotnet test Tests/BattleTests --no-restore -v quiet`：**退出 0，193/193**，失败 0、跳过 0；既有 CS8632/CS0649 警告保留，日志 `final_battle.log` |
| 关卡规则 | `dotnet test Tests/LevelTests --no-restore -v quiet`：**退出 0，19/19**，失败 0、跳过 0；各测试工程既有 NU1900 警告保留，日志 `final_level.log` |
| Godot 扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a3_names_only_scan_20261108 --headless --editor --build-solutions --quit --no-window`：**退出 0**，扫描及构建完成；既有 ComponentInsoector 的 RefCounted/free 退出诊断保留 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出 0，2283 条序列化路径、388 个 UID** 通过 |
| 完整前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a3_names_only_frontend_20261108 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**；两英雄分别建档、目录读取、保存返回、磁盘重读、旧作用域拒绝、加载取消与部分注册清理通过。坏文件与重复注册为预期注入；退出保留 **4 个 ObjectDB 实例、1 个资源占用**诊断 |
| 双人摘要与删除专项 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a3_names_only_slots_20261108 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend --save-delete-only`：**退出 0**，出现 `SMOKE-SAVE-ROSTER PASS`、`SMOKE-SAVE-DELETE PASS` 及最终 `SMOKE PASS`。实际写盘并重读两种席位顺序，核对全员名称/等级、catalogue 名、单/双人统一纯名称标题与删除按钮安全间距；确认取消/确认、主备文件删除、其他格不变、同格重建和过期确认拒绝。退出保留 **4 个 ObjectDB 实例、2 个资源占用**诊断 |
| 原生宽度与画面 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a3_summary_final_measure_20261108 --headless --script res://.godot/validation/coop_a3_20261108/measure_save_summary.gd --no-window`：**退出 0**，使用原场景实际字体测得名称 132px、最低等级双人 220px、双 55 级 254px；标题到删除按钮的可用距离 164px。已查看 `.godot/validation/frontend/save_slots_coop_1.png` 与 `save_slots_coop_2.png`：分别显示“悟空 · 唐僧”“唐僧 · 悟空”，同屏单人统一显示“唐僧”、无截断或删除按钮遮挡 |
| 格式 | 实际执行 `$gameFiles = @(git diff --name-only -- '*.cs' \| Where-Object { $_.StartsWith('Godot/GodotProject/') } \| ForEach-Object { (Resolve-Path -LiteralPath $_).Path }); dotnet format whitespace Godot/GodotProject/GodotProject.csproj --no-restore --include $gameFiles --verify-no-changes`；测试用 `$profileFiles = @(git diff --name-only -- 'Tests/ProfileTests/*.cs' \| ForEach-Object { (Resolve-Path -LiteralPath $_).Path }); dotnet format whitespace Tests/ProfileTests/ProfileTests.csproj --no-restore --include $profileFiles --verify-no-changes`：**4 份生产/Debug、3 份测试均退出 0**，仅测试工作区既有加载警告 |
| 最终差异 | `git diff --check` 退出 0；**7 份 C# 均为 UTF-8/CRLF**；`rg -n 'HeroName|slot\.Level' Godot/GodotProject/TheGame Tests --glob '*.cs'` 无旧摘要标量消费者；只读目录、生成物、源表、bytes、场景无差异，12 份改动均未暂存 |

A3-1 必需规则与实际证据：

| 计划要求 | 实际覆盖 |
| --- | --- |
| 六类个人状态完全隔离 | 新增 `PersonalData_ChangesOnlyTheRequestedHero` 两个方向，正式业务分别修改成长、钱包、背包、穿戴、主动/被动；另一人的完整英雄 DTO 保持原样 |
| 全档实例唯一 | 复用 A2 `PersonalAssets_RejectInvalidInstanceOwnership` 跨背包重复案例，新增 case 6 覆盖一人背包与另一人穿戴占用同一实例；已有运行时拾取/穿戴排重保留 |
| 非法阵容构造拒绝 | 复用 `Profile_RejectsInvalidHeroSets`，已覆盖空、超过两人、重复、零和负编号及非法席位 |
| 完整 v14 双人往返 | 新增 `TwoHeroCheckpoint_RoundTripsAllPersonalData` 两种席位顺序，实际 Capture → JSON → Validate → RestoreCheckpoint；包括不同余额/经验、各人背包、四槽穿戴/随机属性、不同主动和被动等级、共享进度/旗幡，并比较完整捕获结果 |
| 第二人非法时第一人不变 | 新增 `RestoreCheckpoint_RejectsInvalidSecondHeroWithoutMutation` 六例：余额、经验、引用不存在、跨人重复归属、错误英雄主动技能、非法被动等级；全部状态与调用前一致、第一人绑定零通知、容器身份不变 |
| 孤立实例拒绝 | 复用 `PersonalAssets_RejectInvalidInstanceOwnership` case 4，直接调用 ReadItems 拒绝池中无归属实例 |

关键接口变化：

| 修改前 | 修改后 |
| --- | --- |
| `SaveSlotInfo(int slotNumber, long unitId, SaveSlotState state, string heroName = "", int level = 0, DateTimeOffset? savedAt = null)` | `SaveSlotInfo(int slotNumber, long unitId, SaveSlotState state, IEnumerable<SaveHeroSummary> heroes = null, DateTimeOffset? savedAt = null)` |
| 存档格的单个 `HeroName` / `Level` 标量 | `IReadOnlyList<SaveHeroSummary> Heroes`，构造时复制，只读发布并保留席位顺序 |
| 无参战者摘要值类型 | 按计划在同文件新增 `readonly record struct SaveHeroSummary(string Name, int Level)`，不写入存档 DTO |
| 读取/写入/窗口生命周期公共方法 | 签名均不变；目录读取由首位改为全员摘要，写入 catalogue 名由首位改为全员 NameCn 单空格连接 |

本批文件清单：**12 份修改（7 份 C#、5 份文档），无新增/删除正式文件**。唯一新增类型为计划要求的同文件 SaveHeroSummary，没有新的系统、接口、配置或测试辅助类型。

| 所属目录 | 修改文件 |
| --- | --- |
| `Docs/` | `CoopImplementationPlan.md`、`ProjectGuidelines/70-ProfileAndSave.md`、`ProjectTour.md`、`Validation.md`、`WorkingNotes.md` |
| `Godot/GodotProject/TheGame/GameScripts/` | `Models/Save/SaveSlotInfo.cs`、`Systems/SaveSystem.cs`、`UI/SaveForm/SaveForm.Logic.cs` |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/` | `FrontendSmokeScenario.cs` |
| `Tests/ProfileTests/` | `EquipmentTests.cs`、`ModelTransactionTests.cs`、`ProfileTests.cs` |

实际遇到的情况及处理：

- 标题宽度与计划原示例冲突时先报告并停止；用户明确授权按官方风格缩减文字，并最终要求单/双人统一；全部标题只显示角色名，不显示等级。实际使用 NameCn，删除单双人显示分支，未硬编码角色简称或组合，没有运行时字体缩放/宽度回退机制；完整名称已能在现有位置显示。
- A3-1 首次两例往返测试因新增夹具余额不足以购买二级被动而失败；仅增加该往返夹具初始余额，保留扣费规则及所有断言，随后 303/303、最终 304/304 通过。
- 计划未单列的机械消费者为 `FrontendSmokeScenario` 的唐僧摘要断言，改为读取唯一的 Heroes 摘要。双人建档入口尚未开发，因此在既有删除专项内经反射调用既有私有 WriteSnapshotAsync 写入双人快照，随后走正式目录重读与原生窗口显示；没有生产测试入口，也不装配第二个战斗英雄。普通前端不执行删除分支，本批另跑 `--save-delete-only`，没有用通用尾句替代专项证据。
- A3-2 增加 `SaveSlotSummary_PreservesOrderAndOwnsReadonlySnapshot`，验证来源集合修改和消费者写入均不能改变已发布摘要，空格和不可读格为空摘要。

用户最终要求单/双人统一后，已删除专门的显示分支，两个模式均只以同一条 string.Join 按席位拼接名称；最终构建、编辑器扫描、完整前端与存档/删除专项均重新通过，截图已核对。

本批要求已完成，无范围内遗留。B1 须在用户接受 A3 并授权后开始；仍为单个 HeroModel、一套输入/HUD，当前建档/关卡装配只有一人。引擎退出诊断沿用既有边界保留。所有烟测使用 runner 的案例专属 APPDATA，未覆盖正式玩家存档。完整战斗 UI 烟测和双人实战本批未执行；本批按 A3 计划验证档案、存档页及前端流程，双人战斗留给后续阶段。

## 双人A2：灵魂与背包归个人（2026-11-08）

A1 已由用户接受并提交为 `d2c3d13`；本轮按实施计划顺序完成 A2-1～A2-4，钱包、背包、拾取、穿卸、出售和学技均显式指定 HeroId，唯一奖励与实例排重覆盖全档。本局仍只装配 P1，单个 HeroModel、输入和 HUD 保持原状。构建、规则测试、扫描、引用、完整前端/UI 烟测与牛心唯一奖励专项均通过；当前停在 A2 等待用户审查，不进入 A3。

依据为实施计划 A2、规范第 00 章的状态所有权与订退生命周期、第 10 章的中文注释/格式、第 70 章的存档与检查点规则。用户已授权同步规范和计划原文；本批更新其当前事实，不提前实现后续阶段。

存档 v13 → v14：删除根级共享余额；每个 `HeroSaveData` 必需保存 `Soul` 与 `BagInstanceIds`，`EquipmentItems` 改为包含全部背包和穿戴的全档实例池。同一实例必须且只能被一处引用，拒绝缺失引用、重复归属、孤立实例、个人超容量及负余额。仍只接受当前格式，**旧 v13 及更早开发档需重建**，没有兼容或迁移分支。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工基线 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误、5 条既有框架弃用警告；三套规则测试分别为 **282/282、193/193、19/19**，均退出 0 |
| 子任务编译 | 每步执行 `dotnet build Godot/GodotProject --no-restore -v quiet`。用户明确允许 A2-1～A2-3 暂留本批迁移错误；错误数依次为 **122、126、130**，A2-4 全部消除：**0 错误、5 警告、退出 0**。步骤日志为 `.godot/validation/coop_a2_20261108/a2_1_build.log` 至 `a2_4_build.log`；中间失败不作为最终通过证据 |
| 最终游戏构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：**退出 0，0 错误、5 条既有框架弃用警告** |
| 档案规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：**退出 0，292/292**，失败 0、跳过 0。比 A2 开工增加 10 例：个人容量/出售收益隔离、排序归属与原格子顺序往返两项 Fact，6 例非法实例归属，2 例必需英雄字段缺失；原有断言保留 |
| 战斗规则 | `dotnet test Tests/BattleTests --no-restore -v quiet`：**退出 0，193/193**，失败 0、跳过 0 |
| 关卡规则 | `dotnet test Tests/LevelTests --no-restore -v quiet`：**退出 0，19/19**，失败 0、跳过 0。测试工程既有 NU1900，Battle 的 CS8632/CS0649 警告保留 |
| Godot 扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a2_scan_final_20261108 --headless --editor --build-solutions --quit --no-window`：**退出 0**，最终代码扫描与构建完成；既有 ComponentInsoector 的 RefCounted/free 退出诊断保留 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：**退出 0，2283 条序列化路径、388 个 UID** 通过 |
| 正式前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a2_frontend_20261108 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：**退出 0，完整 SMOKE PASS**；两英雄建档、选角、地图主动/被动购技、钱包刷新、保存返回、磁盘重读、加载取消和部分注册清理通过。旧作用域拒绝、坏文件与重复注册为预期注入；退出有 **5 个 ObjectDB 实例、1 个资源占用**诊断 |
| 完整 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a2_ui_20261108 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 30000 -- --smoketest=ui`：**退出 0，完整 SMOKE PASS**；拾取/满包/过期与池复用、单件/批量出售、钱包观察、学习扣费、15 件装备穿卸、60/120Hz 坡地与平台、死亡/检查点回滚、备份恢复、加载取消/失败清理、存档重入通过。坏文件与 HUD 打开失败为预期注入；退出保留 **38 个 CanvasItem RID、20 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、141 个 ObjectDB 实例、11 个资源占用**诊断，并有 20 条 GL 纹理泄漏明细 |
| 牛心唯一奖励专项 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a2_bull_reward_20261108 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=ui --bull-only`：**退出 0**，`SMOKE-BULL TREASURE PASS` 与最终 `SMOKE PASS` 均出现；真实致死攻击、首次直接入包、重复击败排重、满包拒绝、提示缺席仍完成死亡、提示池复用及订退通过。另有 60/120Hz 原生招式与牛心演出 PASS；退出保留 **10 个 ObjectDB 实例、1 个资源占用**诊断。唯一奖励由此既有专项执行，普通完整 UI 不覆盖此分支 |
| 退役接口搜索 | 从仓库根执行 `$oldBag = 'Equipment' + 'Datas'; rg -n -- $oldBag .`，以及 `$oldWallet = ('Profile' + 'Model') + '.' + ('So' + 'ul'); rg -n -F -- $oldWallet .`、`$oldRootWallet = ('Game' + 'Data') + '.' + ('So' + 'ul'); rg -n -F -- $oldRootWallet .`：均退出 1、**0 命中**，含源码、规范、计划和历史说明；同时逐项核对剩余 `.Soul`，只属于英雄 DTO、掉落枚举或显示行，不再访问旧共享钱包。遵循仓库忽略规则，Git 对象、构建缓存和验证日志不属于源码文档验收 |
| 格式与边界 | `dotnet format whitespace <csproj> --no-restore --include <本批文件绝对路径清单> --verify-no-changes`：Godot 工程 **31 份**、ProfileTests **12 份** C# 文件均退出 0；仅既有工作区加载警告。`git diff --check` 退出 0；43 份修改 C# 均验证为 UTF-8/CRLF。无新增/删除类型或正式文件，Framework、addons、Tools/Luban、生成代码/资源、源表、bytes、场景无本批修改；未执行 Git 写操作 |

关键接口变化（返回类型未变化的行省略返回类型）：

| 修改前 | 修改后 |
| --- | --- |
| ProfileModel 共享钱包与共享背包属性 | 删除；改为 `BindableProperty<int> GetSoul(int heroId)`、`BindableList<EquipmentData> GetBag(int heroId)`；构造的 `soul` 表示每人初始余额，`Capacity` 表示每人容量 |
| `AddSoul(int amount)` / `TrySpendSoul(int amount)` | `AddSoul(int heroId, int amount)` / `TrySpendSoul(int heroId, int amount)` |
| `TryCollectEquipment(EquipmentData data)`（ProfileSystem/LevelSystem） | `TryCollectEquipment(int heroId, EquipmentData data)` |
| `GrantUniqueEquipment(EquipmentId equipmentId, Func<float> random)` | `GrantUniqueEquipment(int heroId, EquipmentId equipmentId, Func<float> random)` |
| `TrySell(Guid instanceId)` / `SellByQuality(EquipmentQuality quality)` | `TrySell(int heroId, Guid instanceId)` / `SellByQuality(int heroId, EquipmentQuality quality)` |
| `CollectLoot(LootReward reward)` / `TryCollectPickup(PickupId id)` | `CollectLoot(int heroId, LootReward reward)` / `TryCollectPickup(int heroId, PickupId id)` |
| `EquipmentSaleSummaryQuery(EquipmentQuality quality)` | `EquipmentSaleSummaryQuery(int heroId, EquipmentQuality quality)` |
| `SellEquipmentCommand(Guid instanceId)` / `SellEquipmentByQualityCommand(EquipmentQuality quality)` | 两个构造函数均在首位增加 `int heroId` |
| `InventoryBagPanel.Refresh(bool resetPage = false)` | `Refresh(int heroId, int capacity, IReadOnlyList<EquipmentData> items, int soul, bool resetPage = false)`；Form 注入同一英雄的只读显示数据，关闭时清除借用引用 |
| `ReadItems(GameData data, int capacity)` 返回共享数组与逐人穿戴 | 返回 `(Dictionary<int, EquipmentData[]> Bags, Dictionary<int, EquippedItems> Slots)`；先完整校验，再逐人恢复 |
| Debug 的 `RestoreSoul(int soul)` / `RestoreItems(IEnumerable<EquipmentData> datas)` | 两个入口均在首位增加 `int heroId`，只修改目标英雄 |

本批文件清单：**48 份修改（43 份 C#、5 份文档），无新增、无删除**。下表逐项列出各目录内的相对文件名。

| 所属目录 | 修改文件 |
| --- | --- |
| `Docs/` | `CoopImplementationPlan.md`、`ProjectGuidelines/70-ProfileAndSave.md`、`ProjectTour.md`、`Validation.md`、`WorkingNotes.md` |
| `Godot/GodotProject/TheGame/GameScripts/` | `Commands/EquipmentCommands.cs`、`Entity/Enemies/Bosses/zm1_10_bull_demon_valley/BullDemonHeartDeathState.cs`、`Entity/Level/LevelViewController.Loot.cs`、`Models/Profile/ProfileModel.cs`、`Models/Save/GameData.cs`、`Queries/EquipmentSaleSummaryQuery.cs`、`Systems/EquipmentSystem.cs`、`Systems/LevelSystem.cs`、`Systems/ProfileSystem.cs`、`Systems/SaveSystem.cs`、`Systems/SkillSystem.cs`、`UI/InventoryForm/Components/InventoryBagPanel.cs`、`UI/InventoryForm/InventoryForm.Logic.cs`、`UI/MapForm/MapForm.Logic.cs`、`UI/SkillForm/SkillForm.Logic.cs`、`Utilities/Utility/SaveDataUtility.cs` |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/` | `FrontendSmokeScenario.cs`、`LevelSmokeScenario.cs`、`ProfileTestData.cs`、`UiSmokeScenario.Bull.cs`、`UiSmokeScenario.CheckpointFailure.cs`、`UiSmokeScenario.Dragon.cs`、`UiSmokeScenario.Equipment.cs`、`UiSmokeScenario.FireDemon.cs`、`UiSmokeScenario.Garuda.cs`、`UiSmokeScenario.Inventory.cs`、`UiSmokeScenario.Passives.cs`、`UiSmokeScenario.Skills.cs`、`UiSmokeScenario.Tangseng.cs`、`UiSmokeScenario.cs` |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Procedure/` | `ProcedureLoadGame.cs` |
| `Tests/ProfileTests/` | `BossEquipmentTests.cs`、`CommandTests.cs`、`DragonPalaceTests.cs`、`EquipmentTests.cs`、`LevelModelTests.cs`、`LevelRewardTests.cs`、`ModelTransactionTests.cs`、`PassiveSkillTests.cs`、`ProfileTests.cs`、`QueryTests.cs`、`SkillLearningTests.cs`、`TestGameScope.cs` |

计划之外的实际情况与处理：

- 中间构建门槛与删除旧接口的顺序冲突，已上报并取得用户裁决；保留原顺序且不增兼容视图，A2-4 编译错误清零。
- 背包显示按集合下标取格子；仅依照排序后的归属编号恢复会改变原单人背包顺序。本批沿用实例池的原始顺序来恢复各人的背包，归属编号仍按计划排序；没有增加持久字段或排序状态，反向 GUID 插入的 JSON 往返测试已验证。
- 计划未单列的机械消费者：`SaveSystem` 新档把初始余额写入首个英雄记录，空槽检查和快照回写删除根级钱包访问；`ProcedureLoadGame` 与 `TestGameScope.FromSaveData` 构造时使用默认零余额，再由现有 RestoreCheckpoint 恢复逐人钱包；烟测的根字段比较删除共享余额，完整 Heroes 比较已覆盖个人余额。
- `BossEquipmentTests.Save_RejectsAlteredProbabilitiesAndInvalidAccessoryOwnership` 原夹具直接把包内饰品移到穿戴位置，v14 必须同时移除原背包引用；首次规则测试因此失败，修正夹具后全套 292/292 通过，原有断言未弱化。A2-4 早期构建失败也已清零，失败日志保留在忽略的验证目录。
- `Tests/BattleTests` 经核对无需改源码或断言；本批未新增类型、正式文件、事件、配置或生产测试入口。

遗留与下一批前置条件：A3 须在用户接受 A2 并授权后开始，补齐完整双人档案规则证据和全员存档格摘要；当前仍只显示阵容首位摘要。单份本局模型、技能登记、输入/HUD、P1 开窗和牛心奖励本阶段显式归 P1，分别留给既定 B/C/D/E 批次。引擎退出诊断保留，不在本批修改只读依赖或处理无关生命周期。实机烟测使用既有 runner 的案例专属 APPDATA 和 Validation 存档，正式玩家存档未覆盖；正常数值下全关人工实战未执行，烟测不替代该验收。

## 双人A1：阵容取代单一出战英雄（2026-11-07）

按用户目标文件及 CoopImplementationPlan 第四部分顺序实施 A1-1～A1-8。当前仍只建立一名战斗英雄，单人输入与界面布局不变；钱包和背包归属留给 A2，本局模型留给 B1。依据项目规范第 00/10/30/50/70 章执行分层、中文注释、对称订退和隔离存档验证，2026-11-08 用户进一步要求文档也清零并授权同步规范和计划，已补齐文档当前状态。

存档 v12 → v13：删除单一出战英雄字段，以 `GameData.Heroes` 和 `ProfileModel.HeroIds` 的顺序表示席位（下标 0=P1、1=P2）；阵容人数限 1～2，英雄编号唯一。捕获不重新排序，回滚必须匹配同一有序阵容。其余持久字段和数值规则不变。只接受当前格式，**旧 v12 开发档需重建**，没有迁移或兼容分支。

| 验证 | 命令与实际结果 |
| --- | --- |
| 开工基线 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0，0 错误、5 条既有框架弃用警告；三套 `dotnet test`：279/279、193/193、19/19 |
| 子任务编译 | 每步均执行 `dotnet build Godot/GodotProject --no-restore -v quiet`。用户已明确允许 A1-1～A1-7 暂留本批接口迁移错误；各步错误数依次为 194、208、218、208、208、189、135，A1-8 全部消除，退出 0、0 错误、5 条既有警告。日志在 `.godot/validation/coop_a1_20261107/a1_*_build.log` |
| 最终游戏构建 | 八步结束后重执行 `dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0，0 错误；增量构建显示 0 警告，A1-8 的完整编译保留原 5 条弃用警告，无新增警告 |
| 档案规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：退出 0，**282/282**，失败 0、跳过 0。比基线新增 3 例：反序阵容席位/JSON 往返、非法阵容信封、交换席位快照拒绝且数据不变；原有测试全部保留并迁移参数 |
| 战斗规则 | `dotnet test Tests/BattleTests --no-restore -v quiet`：退出 0，**193/193**，失败 0、跳过 0；既有 CS8632/CS0649 警告保留 |
| 关卡规则 | `dotnet test Tests/LevelTests --no-restore -v quiet`：退出 0，**19/19**，失败 0、跳过 0；三个测试工程的既有 NU1900 网络警告保留 |
| Godot 扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a1_scan_20261107 --headless --editor --build-solutions --quit --no-window`：退出 0，扫描和构建完成；既有 ComponentInsoector 的 free/退出诊断保留 |
| 引用 | `python Tools/ProjectMaintenance/validate_game_references.py`：退出 0，**2283 条序列化路径、388 个 UID** 通过 |
| 正式前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a1_frontend_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：退出 0，完整 `SMOKE PASS`，覆盖两英雄建档/选角、地图购技、保存返回、目录重读、加载取消及失败清理；预期旧作用域拒绝、坏文件与重复注册注入单列，退出有 3 个 ObjectDB 诊断 |
| 完整 UI | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_a1_ui_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 30000 -- --smoketest=ui`：退出 0，完整 `SMOKE PASS`；HUD/背包/技能、15 件装备穿卸、实体/窗口解绑复用、60/120Hz 坡地/平台、拾取/死亡、检查点重入、备份回退、加载取消/失败清理通过。坏文件与 HUD 打开失败为预期注入；退出保留 38 个 CanvasItem RID、20 个 Texture RID、8 个 ShapedText RID、2 个 Font RID、141 个 ObjectDB 实例及 11 个资源占用诊断 |
| 旧接口搜索 | 2026-11-08 从仓库根执行 `$retiredField = 'Active' + 'HeroId'; rg -n -- $retiredField .`：退出 1，**0 命中**，包含源码、规范、计划与历史说明；遵循仓库忽略规则，Git 对象、构建缓存和验证日志不属于源码文档验收。命令中分段构造搜索词，以免记录本身重新引入旧名称 |
| 目标复核 | 2026-11-08 新目标文件仍指定 A1。对照当前 A1-1～A1-8 实现、接口、数据流、测试、8 份步骤构建日志及交付记录核验，未重做已完成实现。重新执行 `dotnet build Godot/GodotProject --no-restore -v quiet`：退出 0、0 错误、增量 0 警告；`dotnet test Tests/ProfileTests --no-restore -v quiet`：282/282；`dotnet test Tests/BattleTests --no-restore -v quiet`：193/193；`dotnet test Tests/LevelTests --no-restore -v quiet`：19/19；`python Tools/ProjectMaintenance/validate_game_references.py`：2283 路径/388 UID 通过，均退出 0。非 Docs 目录工作区差异 SHA-256 仍为 `6629b8ff93d0ebb2bf7cb60bb8e76356261979b5f3ed8f7e759b714f6f65b3d6`，与完整扫描/前端/UI 验证后的代码一致；已检查现存扫描完成与两项最终 PASS 日志，本轮未重复执行引擎烟测。旧名称搜索与 `git diff --check` 再次通过，继续停在 A1 待审 |
| 文档补齐 | 2026-11-08 仅更新上述 6 份文档；`git diff --check` 退出 0。前后对 Git 索引及非 Docs 目录工作区差异执行 SHA-256 比较，两项均相同；本轮构建、规则与引擎烟测未重跑，沿用上一轮未变代码的验证结果 |
| 格式与边界 | `dotnet format whitespace <csproj> --no-restore --include <本批文件绝对路径清单> --verify-no-changes`：Godot 工程 38 文件、ProfileTests 22 文件、BattleTests 1 文件均退出 0；仅工作区加载既有警告。`git diff --check` 通过；61 份修改的 C# 文件均为 UTF-8/CRLF，文档保留原换行格式。Framework、addons、Tools/Luban、生成代码/资源、源表、bytes、场景无本批差异；未执行 Git 写操作，既有四份暂存文档保留 |

关键接口变化（类型、返回类型不变的行省略返回类型）：

| 修改前 | 修改后 |
| --- | --- |
| `ProfileModel(int activeHeroId, IEnumerable<(int HeroId, int TotalExperience)> heroes, ExperienceCurveUtility curve, int equipmentCapacity, int soul = 0)` | `ProfileModel(IEnumerable<(int HeroId, int TotalExperience)> roster, ExperienceCurveUtility curve, int equipmentCapacity, int soul = 0)` |
| 单一出战英雄及其成长/穿戴便捷成员 | 删除；使用 `GetProgress(int heroId)`、`GetSlots(int heroId)`，新增 `int HeroOf(PlayerSlot slot)`、`PlayerSlot SlotOf(int heroId)` |
| `AddExperience(int amount)` | `AddExperience(int heroId, int amount)` |
| `Learn(SkillId skillId)` / `Upgrade(PassiveId id)` | `Learn(int heroId, SkillId skillId)` / `Upgrade(int heroId, PassiveId id)` |
| `IsLearned(SkillId skillId)` / `IsLearnable(SkillId id)` / `IsAvailable(SkillId id)` | 对应方法均在首位增加 `int heroId` |
| `TryEquip(Guid instanceId)` / `TryUnequip(EquipmentSlot slot)` | `TryEquip(int heroId, Guid instanceId)` / `TryUnequip(int heroId, EquipmentSlot slot)` |
| `EquipEquipmentCommand(Guid instanceId)` / `UnequipEquipmentCommand(EquipmentSlot slot)` / `LearnSkillCommand(SkillId skillId)` / `UpgradePassiveCommand(PassiveId passiveId)` | 四个构造函数均在首位增加 `int heroId` |
| `StartSession(int heroId)`（LevelSystem） | `StartSession(IReadOnlyList<int> heroIds)` |
| `InventoryCharacterPanel.Refresh(AbstractHeroEntity hero)` | `Refresh(AbstractHeroEntity hero, HeroProgressionState progress, EquippedItems items)` |
| `ActiveSkillsPage.Refresh()` / `PassiveSkillsPage.Refresh()` | `Refresh(int heroId)`；由窗口传入本周期显示的英雄 |
| `TestGameScope(int activeHeroId, IEnumerable<(int HeroId, int TotalExperience)> heroes, int soul, int capacity, bool initializeHero = true)` | 删除首个身份参数，阵容首位决定本阶段的战斗英雄 |

本批文件清单：67 份修改（61 份 C#、6 份文档），无新增、无删除。以下目录内逐项列出相对文件名；此外修改 `Docs/Validation.md`、`Docs/WorkingNotes.md`、`Docs/CoopImplementationPlan.md`、`Docs/ProjectTour.md`、`Docs/ProjectGuidelines/10-CSharp.md`、`Docs/ProjectGuidelines/70-ProfileAndSave.md`。2026-11-08 文档补齐只涉及这六份文档；用户原已暂存的 OfficialReference 保持不动，Git 索引未改。

| 所属目录 | 修改文件 |
| --- | --- |
| `Godot/GodotProject/TheGame/GameScripts/` | `Commands/EquipmentCommands.cs`、`Commands/LearnSkillCommand.cs`、`Entity/Enemies/Bosses/zm1_11_hidden_dragon_palace/DragonKingEntity.cs`、`Entity/Heroes/AbstractHeroEntity.cs`、`Entity/Heroes/Wukong/WukongEntity.cs`、`Entity/Level/LevelViewController.cs`、`Models/Profile/ProfileModel.cs`、`Models/Save/GameData.cs`、`Systems/EquipmentSystem.cs`、`Systems/Hero/HeroSystem.cs`、`Systems/LevelSystem.cs`、`Systems/ProfileSystem.cs`、`Systems/SaveSystem.cs`、`Systems/SkillSystem.cs`、`UI/BattleForm/BattleForm.Logic.cs`、`UI/InventoryForm/Components/InventoryCharacterPanel.cs`、`UI/InventoryForm/InventoryForm.Logic.cs`、`UI/SkillForm/Components/ActiveSkillsPage.cs`、`UI/SkillForm/Components/PassiveSkillsPage.cs`、`UI/SkillForm/SkillForm.Logic.cs`、`Utilities/Utility/SaveDataUtility.cs` |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Debug/` | `FrontendSmokeScenario.cs`、`LevelSmokeScenario.cs`、`MapProgressSmokeScenario.cs`、`TestArenaController.cs`、`UiSmokeScenario.CombatFeedback.cs`、`UiSmokeScenario.Dragon.cs`、`UiSmokeScenario.DragonRules.cs`、`UiSmokeScenario.Equipment.cs`、`UiSmokeScenario.FireDemon.cs`、`UiSmokeScenario.Inventory.cs`、`UiSmokeScenario.Passives.cs`、`UiSmokeScenario.Skills.cs`、`UiSmokeScenario.Tangseng.cs`、`UiSmokeScenario.Terrain.cs`、`UiSmokeScenario.cs` |
| `Godot/GodotProject/TheGame/MainPack/Scripts/Procedure/` | `ProcedureLevel.cs`、`ProcedureLoadGame.cs` |
| `Tests/ProfileTests/` | `BindablePropertyTests.cs`、`BossEquipmentTests.cs`、`CommandTests.cs`、`DragonPalaceTests.cs`、`EquipmentTests.cs`、`FreezeTests.cs`、`HurtProtectionTests.cs`、`LevelModelTests.cs`、`LevelProgressTests.cs`、`LevelRewardTests.cs`、`ModelTransactionTests.cs`、`MusouTests.cs`、`PassiveSkillTests.cs`、`PoisonTests.cs`、`ProfileTests.cs`、`QixingConfigurationTests.cs`、`QueryTests.cs`、`RecoveryTests.cs`、`SkillLearningTests.cs`、`SkillStateTests.cs`、`SkillSystemTests.cs`、`TestGameScope.cs` |
| `Tests/BattleTests/` | `SkillReuseTests.cs` |

计划之外的实际情况与处理：

- 中间步骤编译门槛与删除接口的先后顺序冲突，开工前已上报并取得用户裁决；保留原子任务顺序，逐步记录错误，A1-8 清零。
- 机械消费者补齐：`AbstractHeroEntity.RefreshAppearance` 按实体 HeroId 读取穿戴；`WukongEntity` 的升龙斩学习判断补 HeroId；`SkillSystem.TryActivate` 内部调用新增签名时显式传本阶段的 P1；`PassiveSkillsPage` 由窗口传 HeroId；`SaveSystem.IsUnclaimedSlot` 与快照回写移除已删除字段；Debug 页面刷新、反射字段比较及所有相关规则调用一并更新。
- `LevelSystem.StartSession` 的实际调用位置是 `LevelViewController.StartSession`，本批在实际调用处传 `profile.HeroIds`，未移动流程职责。`MapForm`、`ProfileTestData`、`EnemyAiSmokeScenario`、`HeroTestScope`、`BattleArchitecture` 经核对无需改动；SmokeInspection 的 `Attributes` 反射契约保留。
- 2026-11-08 用户明确要求文档也清零，并授权同步规范和计划。第 70 章、ProjectTour 与计划现状已更新为 v13、A1 已实现待审；历史记录保留原版本含义，以描述旧字段职责代替旧名称。此前的搜索范围待裁决项已解决。

当前仍保留共享灵魂/背包、单份 HeroModel/技能登记和 P1 开窗；这些是后续计划边界。A2 前置条件为用户审查接受本批 A1；本轮不开始 A2。引擎烟测使用既有 runner 的案例专属 APPDATA 和 Validation 存档；未覆盖正式玩家存档。正常数值下全关人工实战：未执行；本次烟测不替代该验收。

## 微软C#命名与格式迁移（2026-11-07）

按用户要求从规范配置推进到实际项目代码，并排除GGF/第三方。40个枚举去E（21手写、19Luban），1014个字段按可见性与静态归属命名；12份UI绑定、项目Luban加载器/总表模板从各自生成入口更新。脚本UID、枚举成员/值、配置bytes、存档v12与玩法保持。主工作区不应用双人02A草稿。

Framework/addons/Tools/Luban共937份跟踪文件逐字节核对HEAD无变化，Git索引保持原样。GGF扩展的旧EEntityId/EUIFormId引用仅通过GodotProject.csproj的两个编译别名绑定正式新类型，不新增兼容枚举或复制框架API。`.editorconfig`的C#规则也限定在项目自有源文件路径。

以下从仓库根执行可复用格式检查；目录路径必须保留末尾`/`，SDK 10.0.303在不带斜杠时可能没有选中任何文件仍返回0。已用故意不规范样例验证两种参数的差别。真正应用时移除`--verify-no-changes`，保留输入/排除范围，不传`--include-generated`。

```powershell
Push-Location Godot/GodotProject
dotnet format whitespace GodotProject.csproj --no-restore --include TheGame/ EditorScripts/ --exclude TheGame/GameScripts/GameProto/ --verify-no-changes
dotnet format style GodotProject.csproj --no-restore --include TheGame/ EditorScripts/ --exclude TheGame/GameScripts/GameProto/ --severity info --diagnostics IDE0007 IDE0008 IDE0011 IDE0021 IDE0022 IDE0023 IDE0025 IDE0026 IDE0027 IDE0049 IDE0065 IDE0161 --verify-no-changes
Pop-Location
```

小批次可把目录换成具体文件。测试和工具在各自csproj按同一方式指定本批文件；需要特殊生成契约的ExternalTypeUtil手写入口可明确指定单文件。命名是独立重构，不能以格式化代替场景导出/动画属性/反射/源表迁移。旧CSharpStyle的固定成员重排和块命名空间不再用于写入。

| 验证 | 命令与实际结果 |
| --- | --- |
| 配置源表与生成 | 9张表56格经Artifact Tool编辑、重算、逐值核验及前后渲染后，只转入原生XML文本。9份最终xlsx与858f22a加E前版本逐字节一致，无关ZIP部件、共享字符串与样式保留。`$env:AI_MODE='1'; cmd /c Configs\GameConfig\gen_code_bin_to_project_lazyload.bat`最终schema/data/validation/code完成；Luban工具本体未改；全部bytes对HEAD无差异 |
| UI生成 | `python Tools/ProjectMaintenance/regenerate_ui_form.py <场景> --namespace <原命名空间>`逐个重生成10份业务Form与LoadingForm/QuestionTips；生成器只在项目侧转换模板字段，不修改Framework模板。代码/资源引用同步 |
| 类型与资源保持 | 40个枚举成员序列/值逐项一致；19份配置枚举与PlayerSlot的原UID保留。96份场景/资源与操作前快照独立比较，仅序列化属性名替换，节点名/路径、几何、动画轨道和数值原样；正式存档未写入 |
| 编译与命名检查 | `dotnet build Godot/GodotProject --no-restore -v quiet -p:EnforceCodeStyleInBuild=true`最终0错误、0条IDE1006；保留5条既有框架弃用警告。只在本命令临时启用风格分析，没有修改全仓构建门槛 |
| 规则与工具 | `dotnet test Tests/ProfileTests --no-restore -v quiet`279/279，BattleTests同命令193/193，LevelTests19/19；`python -m unittest discover -s Tests/ToolTests -v`26/26。既有NU1900及Battle的CS8632/CS0649警告保留；12份变更Python均py_compile通过 |
| 格式 | 按显式手写文件清单应用SDK style/whitespace；上述有效目录命令的whitespace与全部列出的style诊断`--verify-no-changes`最终均退出0，四组测试/工具显式清单复核也退出0。简单自动属性保留单行，using/命名空间/表达式体/var按EditorConfig，初始化次序不重排 |
| 实际Godot扫描 | 既有runner案例`microsoft_style_import_20261107 --headless --editor --build-solutions --quit --no-window`退出0、完成扫描/构建；退出仍有既有ComponentInsoector/free诊断，未修改插件 |
| 实际导出绑定 | runner案例`microsoft_style_bindings_verified_20261107 --headless --script res://.godot/validation/microsoft_style/validate_bindings.gd --no-window`退出0：91场景、1110个序列化属性实际实例化取值通过。NodePath解析到真实节点；Resource的local_to_scene复制按存储内容比较，不误判为丢绑定 |
| 正式前端 | runner案例`microsoft_style_frontend_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`退出0、完整SMOKE PASS；包括建档/选角、目录、地图、关内保存返回、备份/失败及重新加载。预期失败注入与退出5个ObjectDB/1条资源诊断分别保留 |
| 完整UI | runner案例`microsoft_style_ui_final_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 30000 -- --smoketest=ui`退出0、完整SMOKE PASS；HUD/背包/技能、实体/窗口复用、60/120Hz地形/平台、拾取、死亡、备份回退、加载取消与失败清理通过。预期坏文件/HUD失败注入单列；退出仍有20个Texture RID、8个ShapedText、2个Font RID、140个ObjectDB/11条资源诊断 |
| 引用与边界 | `python Tools/ProjectMaintenance/validate_game_references.py`2283路径/388UID通过；`audit_equipment_appearances.py`38装备/38外观通过。`git diff --check`通过，索引未改，隔离02A草稿不动 |

临时符号迁移/核验脚本和报告均在忽略目录`.godot/validation/microsoft_style/`，未引入正式迁移工具或生产测试API。首次UI尝试发现烟测元组内保存的旧反射字段名，已修复后重新完整执行；早期失败不作为最终通过证据。以下E前缀章节只保留历史实施证据，其规范已被本节及第10章替代。

## 全项目枚举E前缀（2026-11-07）

按用户本次要求统一项目自有枚举类型：21个手写枚举（含私有/调试状态）与19个Luban配置枚举全部加E前缀。通过C#符号区分类型和同名字段，公开接口、状态机、UI、测试及工具引用一起迁移；字段/属性、枚举项及持久键不加前缀。独立枚举文件同名迁移并携带原UID；EPlayerSlot.P1/P2保持1/2。按第10章更新命名规范，第20章同步配置类型说明；框架两份项目适配扩展仅6处配置枚举参数同步，例外登记第50章。双人02A未开始。

| 验证 | 命令及结果 |
| --- | --- |
| 源表/生成 | Artifact Tool编辑并逐值检查9张表56个类型单元格，查看__enums__/__beans__前后预览；只转入这些单元格的原生XML文本，所有其他单元格、样式、关系、共享字符串、工作簿部件及ZIP条目顺序保持。`$env:AI_MODE='1'; cmd /c Configs\GameConfig\gen_code_bin_to_project_lazyload.bat`：最终schema/data/validation/code完成；原19份声明清理、新文件保留原UID。未手改生成C#或bytes，未声称操作了用户XLSX Diff插件 |
| 名称与数值 | TheGame内40/40枚举均为E前缀；对照隔离的改名前快照，40个成员序列和赋值逐项一致；全部配置bytes无diff。439份C#还原类型前缀后与改名前内容一致，后续仅另同步一处注释类型名；框架差异验证仅6个类型引用，无逻辑变更 |
| 构建/规则 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有框架弃用警告；`dotnet test Tests/ProfileTests --no-restore -v quiet`：279/279；BattleTests同命令193/193；LevelTests同命令最终19/19（补齐3个显式源码文件名引用）。`python -m unittest discover -s Tests/ToolTests -v`：最终26/26；未弱化原断言。规则项目NU1900及Battle既有CS8632/CS0649警告保留 |
| Godot扫描 | 既有runner案例`enum_prefix_import_20261107 --headless --editor --build-solutions --quit --no-window`：退出0，扫描和构建完成；既有ComponentInsoector/free退出诊断保留 |
| 正式前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py enum_prefix_frontend_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：退出0，完整SMOKE PASS，含建档/选角、目录、地图学技、战斗、设置保存返回、备份/失败及重新加载；预期失败注入保留，退出5个ObjectDB/1条资源诊断 |
| 完整UI回归 | 同runner案例`enum_prefix_ui_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 30000 -- --smoketest=ui`：退出0，完整SMOKE PASS；技能/无双、HUD/背包绑定、实体/窗口复用、60/120Hz地形/平台、拾取、死亡结算、备份回退、加载取消及失败清理通过。预期坏文件/HUD失败注入与退出诊断分开：20个Texture RID、8个ShapedText、2个Font RID、138个ObjectDB/10条资源诊断保留 |
| 引用/格式/索引 | `python Tools/ProjectMaintenance/validate_game_references.py`：2283序列化路径/388UID通过。CSharpStyle审计362个自有文件，XML齐全；165个原有格式建议不扩为本次全项目格式化。`git diff --check`通过；既有Git暂存内容散列保持，未暂存/提交 |

## 双人01：玩家身份枚举（2026-11-07）

按CoopImplementationPlan的01执行，仅新增`TheGame/GameScripts/Models/PlayerSlot.cs`及Godot扫描的UID；`Tests/ProfileTests/ProfileTests.csproj`增加一行源码引用。枚举在GameLogic命名空间，P1=1、P2=2；BattleTests已有Models通配引用。依据第10章的类型命名/XML与第50章新增C#扫描要求。未改变模型、业务API、输入、场景或v12存档；未增加只验证枚举字面值的测试。本批完成后暂停等待审查，02A未开始。

| 检查 | 命令与结果 |
| --- | --- |
| 游戏构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出0，0错误，5条既有框架弃用警告 |
| 独立源码引用 | `dotnet build Tests/ProfileTests --no-restore -v quiet`：退出0，0错误；NuGet漏洞服务不可达的既有NU1900警告保留；本次没有运行规则测试 |
| Godot扫描/构建 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py coop_stage01_player_slot_scan_20261107 --headless --editor --build-solutions --quit --no-window`：退出0，dotnet_build_project及update_scripts_classes完成，生成`PlayerSlot.cs.uid`。既有ComponentInsoector退出时Can't free a RefCounted/nonexistent free诊断保留，不宣称编辑器无错误 |
| 范围 | `git diff --check`通过；本批实现差异3个文件（含UID），另更新计划/导览/沟通/验证状态；既有暂存文档内容散列保持，未自动暂存/提交 |

## 双人实现撤回与重新规划（2026-11-07）

用户要求恢复逐步审查，已撤回本次双人实现：169个受影响的已跟踪文件（含格式触及）恢复到858f22a原字节；移除10个本次新增实现/测试/UID文件。源码、测试、规范、场景与当前v12格式均恢复，Git索引内容保持，未暂存或提交。只保留规划文档和官方只读依据，并新增CoopImplementationPlan作为详细计划。

| 检查 | 结果 |
| --- | --- |
| 回退核验 | 逐文件读取HEAD原字节与恢复文件比较，全部相同；`git diff --stat`只包含文档，原新增实现文件已不存在 |
| 回退后构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出0、0错误，5条既有框架弃用警告 |
| 边界 | `PlayerSlot`枚举尚未实现，v13未保留，正式玩家存档未修改；此前实现的规则/引擎结果不作为回退后功能证据 |

后续按[详细计划](CoopImplementationPlan.md)逐批执行；不因历史验证通过而跳过审查，也不在本次规划后自动开始01。

## 双人规划的现状验证（2026-11-07）

本次是先审查、制定分阶段计划，只改ProjectTour/OfficialReference/WorkingNotes及本记录；未实现双人、修改源表/场景/框架/存档格式，未写入玩家存档。入口时工作区干净，审阅基线提交858f22a。

| 检查 | 本轮命令及结果 |
| --- | --- |
| 实际架构/调用链 | 读取project.godot/csproj、Arch/FsmArchitectureExtensions、ProcedurePrepare/LoadGame/Level/Character、Profile/Hero/Skill/Level/SaveModel及系统、GameData/SaveDataUtility、InputSystem、攻击来源/伤害/死亡事件、敌人索敌、Loot/GroundItem、相机/入口、HUD/Inventory/SkillForm/Musou、甘露/分身与龙王。证据及待改契约在CoopImplementationPlan，不以额外英雄烟测冒充双人独立数据 |
| 官方只读核对 | 读取同一主程序SelectRole、MemoryClass、KeyBoardControl、BaseMonster/BaseAura、RoleInfo、PackThings/FallEquipObj、MainGame/GMain、ViewControllor、Role2/Role2Shadow、Monster26；核实不可重复选角、同槽双方、奖励归属/赠送/治疗范围、全灭和最低等级分档 |
| 构建基线 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出0，0错误/5条既有框架弃用警告 |
| 档案/技能/掉落基线 | `dotnet run --project Tests/ProfileTests --no-restore -- -parallel none`：279/279通过 |
| 战斗基线 | `dotnet run --project Tests/BattleTests --no-restore -- -parallel none`：193/193通过；测试项目既有CS8632与Godot导出字段CS0649警告保留 |
| 关卡基线 | `dotnet run --project Tests/LevelTests --no-restore -- -parallel none`：19/19通过 |

三个规则项目均有NU1900（NuGet漏洞数据服务不可达），不影响本轮断言。此次未执行双人引擎烟测，也没有以单人基线测试证明双人方案已经实现；实际玩法、输入、美术及生命周期验收按ProjectTour各阶段执行。

## 红色回血球与官方掉落数值（2026-11-07）

遵循第20/30/40/50章，扩展既有LootSystem/LootOrbNode/LevelSystem。Monster_v1 sprite3原始渲染确认为红色，auraGreen只是类名，先前“绿色回血球”已纠正。红/蓝恢复量在生成时固定为上限5%向下取整、最低10；黄色每颗5无双。三类各自独立12%，1/2/3颗各4%。

| 验证 | 命令与结果 |
| --- | --- |
| 原帧/源表范围 | `python Godot/GodotProject/.godot/validation/health_orbs_20261107/verify_sources.py`：退出0；19张health PNG与独立FFDec导出的sprite3逐字节相同，57张旧球PNG与用户索引相同。Combat仅AC:AF第1/3/5/6行共16格变化；其余单元格和ZIP部件（含sheet2、共享字符串、样式、关系）原字节保持。Artifact Tool已编辑并检查前后预览，未声称已操作用户XLSX Diff插件。 |
| 生成 | `$env:AI_MODE='1'; cmd /c Configs\GameConfig\gen_code_bin_to_project_lazyload.bat`：最终schema/data/validation/code均完成，只更新对应BattleConfig/TbBattleConfig及bytes。新增共用生命球概率、数量、最低量和上限比例，蓝/黄球数值沿用既有Enemy/Combat配置。 |
| 构建/规则 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有框架弃用警告；`dotnet run --project Tests/ProfileTests --no-restore -- -parallel none`：279/279，覆盖4%分布、恢复最低量/取整、跨升级保持生成收益、满血、失效会话拒收；`dotnet run --project Tests/LevelTests --no-restore -- -parallel none`：19/19。规则项目有NuGet漏洞源不可达的NU1900警告。 |
| 实际掉落/拾取 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py health_orbs_capture_final_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=ui --loot-only`：退出0，60/120Hz真实击杀红球掉落、19帧动画、追踪、暂停、接触恢复、满血回收、红蓝共池复用、清理不发放均通过；死亡在途红球回收且不复活。种子仅测试侧选择，生产概率不改。 |
| 菜单既有修正 | 同runner案例`health_orbs_map_buttons_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend --map-buttons-only`：退出0、SMOKE-MAP-BUTTONS PASS；技能单次展开保持、保存悬停换图、设置轮廓对齐/字号字体、保存/失败保留/重读/复用通过。预期保存失败注入另记，退出2个ObjectDB/1条资源诊断保留。 |
| 导入/引用 | 同runner案例`health_orb_import_20261107 --headless --editor --script res://EditorScripts/regenerate_collection.gd --no-window`：退出0、997资源；官方红帧最终重导入案例`health_orb_final_import_20261107 --headless --editor --import --quit --no-window`退出0。编辑器既有ComponentInsoector/free与RID/ObjectDB退出诊断保留。`python Tools/ProjectMaintenance/validate_game_references.py`：2283路径、387UID通过。 |
| 格式/范围 | `dotnet run --project Tools/ProjectMaintenance/CSharpStyle -- . Godot/GodotProject/.godot/validation/health_orbs_20261107/style_final.json`：361自有文件通过；`python -m py_compile Tools/LegacyMigration/export_official_combat_effects.py`、`git diff --check`通过。保留用户索引，本次未暂存/提交；存档版本和只读框架不变。 |

已查看官方四种球色板与实机`health_orbs_120hz.png`中的红球；60Hz首只怪物在视口外，不将该截图当红球可见证据。19帧/24fps、透明边缘与原点沿用官方原帧，无调色shader。专项退出0仍有12个ObjectDB/3条资源退出诊断，功能PASS不代表无关停诊断；此次不代表自然数值下全关实战验收完成。

## 项目清理、官方完整性核对与后续路线（2026-11-07）

依据第00/10/20/30/40/50/70章，按用户本轮“清理+重构+规划”完成。删除30个已跟踪遗留文件：临时xlsx三份、构建/会话日志与旧审计产物、五份已被现规范替代的计划、重复研究/局部README、无消费者的空debug_arena和迦楼罗冗余天空、SpatialExtensions及四份被通用入口替代的地图脚本（含UID）。保留正式test_arena、已注册数据/动态资源、原应用图标、有效规则/引擎测试及官方导出链，不改源表、Framework/addons/Luban、正式存档或Git索引。资源常量只经既有生成器更新。

ProjectTour重写为当前功能矩阵、单人待办和双人分阶段方案；ArchitectureReview合并两份重复研究；唐僧现状/源规则并入OfficialReference；WorkingNotes从逐轮账本收敛为当前意图和有效取舍；Validation清理被后续记录替代的尝试。Docs正文由约980KB减至约400KB，历史仍可由Git查阅。第20/30/40/60/70章只对齐用户已确定的配置实例取用、源表类型行、敌人/两英雄现状、独立背包和存档字段，不引入新玩法规则。

| 验证 | 精确入口与结果 |
| --- | --- |
| 官方版本/功能链 | 重新下载OfficialReference列出的v25928/Role_v7/OtherMat_v9，SHA及恢复后SHA与基线一致；只读摘要`.godot/validation/project_cleanup_20261106/official_downloads.json`及`official_content_audit.json`。核对13条实际调用链：绿球生成/恢复、法宝装备、背包离线兑换、牛心战功及结尾、片头/当前活动、双人输入/死亡/存档/竞技/治疗。AllEquipment去重44种，Items实际38种，六种差额逐件列出；不把定义或旧包嵌入脚本当可达玩法证据。 |
| 全关卡来源 | `python Tools/LegacyMigration/audit_official_level_dependencies.py --repo . --work Godot/GodotProject/.godot/validation/remaining_levels`：退出0，重新验证5包SHA、11关、24种递归关联敌人、各生成流/阶段/音乐；更新的是忽略的调查清单，未重写正式关卡。 |
| 构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：最终状态退出0、0错误/5条既有框架弃用警告。 |
| C#规则 | `dotnet run --project Tests/ProfileTests --no-restore -- -parallel none`：274/274；`dotnet run --project Tests/BattleTests --no-restore -- -parallel none`：193/193；`dotnet run --project Tests/LevelTests --no-restore -- -parallel none`：19/19。删除空间扩展后同步移除BattleTests项目链接及失效using；首次遗留using构建失败已修正，不计作通过。 |
| Python工具 | `python -m unittest discover -s Tests/ToolTests -v`：最终26/26。新增两个真实边界回归：应用图标仅通过UID引用，删除图片但残留.import仍应报错；Autoload UID不可由别处的ext_resource或旧.godot缓存冒充定义。`python -m py_compile Tools/ProjectMaintenance/validate_game_references.py Tests/ToolTests/test_game_references.py`通过。 |
| 地图作者入口合并 | 在忽略目录的完整副本执行`Godot4CSharp_console.exe --headless --editor --path <副本> --script res://EditorScripts/register_official_map_location.gd -- <key> <旧节点> <字段> <题签> 19`，依次bianfuku/Location4/m_Bianfuku、qixingdong/Location5/m_Qixingdong、jialuoluo/Location6/m_Jialuoluo：均退出0。随后原生比较脚本`verify_map_authoring.gd`退出0，三地点位置/尺寸、hover留白、缩放、业务ID/子节点数量、轨道路径及19帧保持；map与3个库UID相同。修复了通用入口对自身模板重建及重算缩放覆盖人工几何的问题，正式map/动画无变化。最终比较脚本/日志保留于`project_cleanup_20261106`，验证副本已清除。 |
| 正式前端 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py cleanup_frontend_final_20261107 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：退出0，完整前端PASS；两英雄建档/技能、设置/音量、地图/战斗、三次返回后直接读盘一致、失败清理/复用通过；没有缺失UID或图片载入错误。退出5个ObjectDB及1条资源诊断保留。 |
| 菜单专项及F6 | 同runner案例`cleanup_map_buttons_20261107`，同OpenGL参数并加`--smoketest=frontend --map-buttons-only`：单次展开/保存hover/设置字体/保存失败保留/重试及读盘通过；该次同时暴露图标误删，修正后的最终完整前端见上行，不能称此旧运行无资源错误。案例`cleanup_test_arena_20261107 --headless --scene res://TheGame/Scenes/test_arena.tscn --audio-driver Dummy --quit-after 360`退出0，悟空/两怪实际就绪；8个ObjectDB/3资源及形状RID退出诊断。 |
| 资源与引用 | 同runner案例`cleanup_collection_final_20261107 --headless --editor --script res://EditorScripts/regenerate_collection.gd --no-window`退出0、978项正式资源；既有编辑器ComponentInsoector/free、RID/ObjectDB诊断保留。`python Tools/ProjectMaintenance/validate_game_references.py`：最终2245序列化路径/387UID，并检查project.godot的UID-only设置。`python Tools/LegacyMigration/audit_equipment_appearances.py --output Godot/GodotProject/.godot/validation/project_cleanup_20261106/equipment.json`：38种外观/图标通过。 |
| 格式与范围 | `dotnet run --project Tools/ProjectMaintenance/CSharpStyle -- . Godot/GodotProject/.godot/validation/project_cleanup_20261106/style.json`：361自有文件、0格式差异/缺XML；18份入口/Docs Markdown本地链接无缺失；`git diff --check`通过，受保护依赖/源表无改动、Git索引仍为空。 |

本轮中间发现bhpic.png是project.godot通过UID配置的应用图标，已按HEAD原字节与原.import恢复，最终保留；引用检查由此增加真实边界覆盖。图标曾误删的早期frontend/map-buttons运行只支持其实际断言，不替代修正后的完整运行。作者比较脚本一次NodePath类型错误已在夹具中修正，最终退出0的独立比较才是证据。编辑器/运行时退出诊断与预期旧档案、坏文件、注册/保存失败注入分别保留。

清理阶段完成的是清理/重构/规划；该阶段尚缺的回血球现已按上节后续实装。片头/结尾、自然数值下完整通关、战功兑换和双人仍以ProjectTour待办为准。当前单人主线已可玩，不代表官方全部内容或正常难度已最终验收。

## 当前限制与验收边界

- 当前v12本地存档；本次双人实现已撤回，悟空/唐僧单人及红色回血球均已接入。片头/结尾、战功兑换和双人未实现，详细范围见ProjectTour，不能用旧“唐僧未实装”描述现状。
- 正常数值下两角色完整自然战斗通关尚缺统一验收；主线进度夹具提高生命并用攻击包清场，技能命中专项另验。
- 构建保留框架弃用警告。长流程可出现ObjectDB/资源/RID退出诊断，编辑器另有ComponentInsoector/free诊断；按具体记录解释，不以业务PASS宣布无泄漏。
- 跨槽/旧档案/损坏文件、注册或HUD装配失败属于显式失败注入；须确认预期拒绝/清理后测试继续通过。测试只操作隔离Validation存档，正式玩家进度和用户Git索引不动。
- 既有DefaultWebRequestAgentHelper.Reset关停异常已按2026-10-31授权修正，不能继续把更早异常列为当前未解决事实；若再次出现按新日志定位。
- 官方SWF/XML与截图、原始日志位于忽略目录；可重跑作者工具和规则/引擎测试保留。版本源与SHA见OfficialReference。


## 返回主菜单提交当前档案与选关可见间距（2026-11-06）

地图设置原先只等待`PendingWrite`，没有提交地图上新产生的修改。现在地图与关内设置都调用既有`SaveSystem.SaveManuallyAsync`，成功后才导航，失败保留当前档案与页面。全部正式`ProcedureTitle`入口已检查：`ProcedurePreload`是无档案的首次启动；`ProcedureMap`提交当前档案；`ProcedureLevel`原本已提交后再`EndSession(false)`。未增加导航或存档抽象，未把写盘放进标题销毁档案的阶段。

本节布局结果覆盖下节的初次位置：原生透明渲染按alpha>128测量完整按钮图案，普通态技能/保存/设置的横向中心分别为75.5/76/75px；两处垂直可见边界间隔从91/37px改为64/64px。保存整体上移27px，设置图案和题签分别校正；技能展开时保留官方扩展轮廓，不按动态包围盒移动按钮。设置20号海报字、官方技能单次展开和保存悬停换图均保留。

| 验证 | 命令与结果 |
| --- | --- |
| 选关专项与失败重试 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py map_return_save_and_spacing_20261106 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend --map-buttons-only`：退出0、`SMOKE-MAP-BUTTONS PASS`。技能普通态静止、悬停展开15帧后保持末帧且持续0.8秒不重播、保存真实鼠标悬停切图、设置字体/固定尺寸/提亮/复用均通过。购买后再增加未手动保存的17灵魂/3经验；隔离档案编号故意不匹配时拒绝返回并保留状态，恢复编号后实际点击返回、重读磁盘与重新进图通过。 |
| 全部返回路径 | 同runner案例`all_title_returns_save_20261106`，参数`--rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend`：退出0、完整前端PASS；悟空地图、唐僧地图、关内设置三次`SMOKE-RETURN-SAVE PASS`。返回前独立捕获快照，标题出现后直接通过`EasySave.LoadFromUserAsync<GameData>`读取对应文件，逐项比较版本、出战角色、灵魂、所有英雄的经验/学习/穿戴、装备实例/属性、关卡进度及东海旗标，不以内存检查点代替写盘证据。原有标题、六格档案、手动保存、暂停/回滚与失败清理回归通过。 |
| 可见边界 | 同runner案例`map_visible_bounds_aligned_20261106`，参数`--rendering-method gl_compatibility --audio-driver Dummy --script res://.godot/validation/map_correction_20261106/render_map_bounds.gd`：退出0、`MAP VISIBLE BOUNDS RENDER PASS`。三张单按钮透明图在alpha>128下的包围盒为技能(41,59)..(110,103)、保存(11,167)..(141,259)、设置(21,323)..(129,436)（右/下边界不含），两处边界间隔均64px。 |
| 实际画面 | 同runner案例`map_equal_visible_gaps_preview_20261106`，参数`--rendering-method gl_compatibility --audio-driver Dummy --script res://.godot/validation/map_correction_20261106/render_map.gd`：退出0、`MAP NATIVE RENDER PASS`。实际查看`.godot/validation/map_correction_20261106/map_native_idle.png`与`map_native_hover.png`；使用正式地图场景，确认普通态排列及展开末帧。独立渲染夹具设根尺寸时有一条anchors警告。 |
| 构建/格式/引用 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出0、0错误/5条既有框架弃用警告；`dotnet run --project Tools/ProjectMaintenance/CSharpStyle -- . Godot/GodotProject/.godot/validation/title_return_spacing_style_20261106.json`：362文件、0格式差异/缺XML；`python Tools/ProjectMaintenance/validate_game_references.py`：2248路径/388UID；`git diff --check`通过。 |

选关专项中的档案身份错误和拒绝返回日志是上述故意失败注入；退出仍有2个ObjectDB及1条资源诊断。完整前端保留既有旧档案、坏文件和注册失败注入，退出3个ObjectDB诊断。均未修改Framework/addons或用户Git索引。下方只等待`PendingWrite`以及按控件中心等距的旧记录不代表当前实现。

## 选关停帧、保存悬停与设置标题纠正（2026-11-06）

用户明确技能不应循环。此前把它当成故障描述，又把hover声明帧数当循环依据，两处结论都不成立：实际488/487帧脚本的frame15均stop。现恢复普通态1帧静止、悬停15帧单次展开后保持，移出回普通态；491的19帧可购黄光是源文件独立规则。保存502的BUTTONRECORD明确up/down=499、over=501，新增同注册画布的官方悬停PNG。设置标题15号改20号海报字、金色黑边/白外边，按源字图中心约75.5对齐并向右修正3.5px；使用单个原生Label的outline/shadow_outline，不额外加文字节点。

| 验证 | 命令与结果 |
| --- | --- |
| 官方导出 | `python Tools/LegacyMigration/export_official_map_combo_ui.py --source Godot/GodotProject/.godot/validation/skills-source/official_othermat.swf --java <已安装Java> --ffdec <已安装ffdec.jar> --work Godot/GodotProject/.godot/validation/map_correction_20261106/export --map-only`：源SHA校验通过，只更新地图资源；manifest记录488/487停帧、502/499/501和画布原点。 |
| 构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出0、0错误/5条既有框架弃用警告。 |
| 实际交互 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py map_official_controls_final_20261106 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend --map-buttons-only`：退出0，普通态静止、展开到14号帧停止并继续悬停0.8秒仍不重播、移出复位、可购发光/购买、保存真实鼠标悬停切图、标题对齐/字号/描边、设置导航/磁盘重读/复用通过；2个ObjectDB及1条资源退出诊断。 |
| 完整前端 | 同runner，案例`map_official_frontend_final_20261106`，去掉`--map-buttons-only`：退出0，标题/两英雄建档重读、地图技能购买、手动保存、地点命中、设置导航/暂停/回滚/复用和失败清理全部通过。故意旧档案/坏档/注册失败注入日志保留，退出3个ObjectDB诊断。 |
| 实际画面 | 已查看`frontend/map_buttons_aligned.png`、`map_save_official_hover.png`、`map_skill_entry_hover.png`及原生字体并排比较。保存图案没有裁切，标题同列，设置字号/金色与黑白轮廓已调整。 |
| 资源登记 | runner案例`map_official_resources_20261106 --headless --editor --script res://EditorScripts/regenerate_collection.gd --no-window`：退出0，980资源生成；PNG导入完成，既有ComponentInsoector/free、RID/ObjectDB退出诊断保留。 |
| 引用/格式 | `python Tools/ProjectMaintenance/validate_game_references.py`：2248路径/388UID；CSharpStyle报告`map_official_controls_style_20261106.json`：362文件、0格式差异/缺XML；作者工具py_compile、`git diff --check`通过。 |

## 身体、攻击来源、技能与官方背包展示收尾（2026-11-07）

本节覆盖当前12项目标及随后“两英雄都改用官方背包展示”的要求，覆盖下方11月6日关于战斗待机预览、SecondJumpTime和玄冰原点不动的历史结论。依据第00章单一状态所有权、第30章原生动画/基础形状、第40章原点/遮挡及第50章生成边界完成；第40章共用战斗/背包图像条款按用户当前要求作独立官方图例外，未改规范正文。

runner为 `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py`；下表案例命令的 `...` 表示 `--rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 --`。日志位于 `.godot/validation/movement_presets/<案例>/{stdout,stderr}.log`，APPDATA独立；烟测里的通用UI PASS只代表实际执行分支。

| 目标 | 当前实现与实际证据 |
| --- | --- |
| 1 幻影外观大小 | 以用户修改的本体为准，同用88.4高胶囊；按源幻影比本体高且偏低的差异，Sprite设(0,-33)/0.9。查看`hero_alignment_20261107/clone_body_aligned.png`及真实`tangseng_clone_platform_120_1.png`，未用角色根位移抵消姿态。 |
| 2 身体脚底高度 | 本体/幻影胶囊中心均(0.5,-6.2)，局部底边38；正式平台召唤后两者Y差小于0.1px，双向60/120Hz检查通过。 |
| 3 二跳时钟 | 删除SecondJumpTime和重复截止时刻，HeroAirState读取原生播放器完成情况；沿用GetActionDuration的complete标记/长度。两英雄实际60/120Hz翻身、切下落、落地通过；BattleTests保留两个有效频率用例，不重复四个无效参数组合。 |
| 4 攻击来源 | AttackSource记录OwnerEntityId、CasterType/CasterInstanceId及EmitterInstanceId；ReceiveHit只消费AttackData，事件复制同一值快照。实机区分本体/幻影共同归属和不同施放、判定节点；攻击包池化测试核对来源复制/清空。 |
| 5 无双归属 | 删除DamageBeforeDefense公开字段；CombatSystem在伤害计算中就地选定身体/离身的阶段伤害并算AttackerWsGain，HeroSystem只按当前死亡/激活状态接收。规则回归覆盖官方特殊招式分母、减伤与舍入；实机普通/未满每次+2，冰龙多次逐次增长。 |
| 6 甘露居中 | 源亮核多阶段位于中心附近，去掉-4.5px横向偏移，保留-24.5纵向、34帧加法和治疗时间。已查看`tangseng_cure_full.png`，深浅底加法既有检查继续有效。 |
| 7 玄冰范围/接地 | 幼柱42×56、成柱70×100、双柱148×100/154×100，原生轴在左右出生/长成/淡出时切换；无双按真实脚底上移30.4px。9个阶段实际物理点查询通过柱心有效/已去除外缘无效，真实目标技能命中通过，形状叠图和无双截图已查看。保留少量矩形近似，不声称Flash像素碰撞完全一致。 |
| 8 进关等待 | 沿用GF.Resource异步接口，把英雄预加载与场景/HUD并行，无永久缓存或新管理器。时长记录见下；取消/迟到实体回收专项通过。 |
| 9 重复普通音 | 未满冰龙释放沿用起手已播的普通音；满蓄保留专用音。真实点击/未满/满蓄/贫魔、分身创建换位、玄冰单声、双甘露、二次L、命中及受击全部通过。官方会重播未满普通音，这里按用户要求主动调整。 |
| 10 平台召唤 | 平台夹具复制正式单向平台到当前波次，不传送过门触发下一波。本体实际落地后从正式CreateWaterClone生成；两频率双向均停留平台。完整8秒寿命、墙/落地、暂停、辅助动作、取消复用继续通过。 |
| 11 EXP粗于框 | 原生Frame贴图独立画框，填充内缩为233×16；上下九宫格边距改2。两英雄正式背包0/50/100%截图已查看，满条仍在框内。 |
| 12 全Boss死亡 | 猕猴王、禺狨王、狮驼王及石像补原生闪烁/第25帧旋涡；七位普通死亡目标复用AbstractBoss入口和已有状态/效果，60/120Hz共14例通过回收/可见复用。源脚本逐个检查后保留大猩猩尸体、牛魔形态链及龙王25帧无旋涡特例，详见OfficialReference。 |
| 追加 两英雄官方背包 | backpack_v2悟空252为62帧短循环，没有立棍；唐僧283为1帧静态姿势，嵌套帧是装备selector。26张专用紧凑图、两份预览外观复用既有两层/字典/AnimationPlayer。两英雄自然播放和各15件正式GUI穿卸通过；已查看裸装、唐僧全装、经验条截图。战斗100/91帧wait继续只用于战斗。 |

| 验证 | 实际命令/结果 |
| --- | --- |
| 构建/扫描 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误/5条既有框架弃用警告；`run_godot.py hero_alignment_scan_final_20261107 --headless --editor --build-solutions --quit --no-window`退出0，完成脚本扫描/构建；退出仍有既有ComponentInsoector/free诊断。 |
| 档案与配置 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：274/274，退出0。 |
| 战斗 | `dotnet test Tests/BattleTests --no-restore -v quiet`：193/193，退出0，含来源快照清空；二跳删除两个重复参数组合，非减少行为覆盖。 |
| 工具 | `python -m unittest discover -s Tests/ToolTests -q`：24/24，退出0。 |
| 唐僧完整技能/幻影 | `run_godot.py tangseng_alignment_full_final_20261107 ... --smoketest=ui --tangseng-only`：退出0；正式选角/建档/入关、两种频率完整幻影生命周期、平台召唤、普攻/冰龙、无双、来源、五技能及八音频通过。stderr仅5个ObjectDB退出诊断。 |
| 官方背包/二跳/水魔 | `run_godot.py inventory_native_final_wukong_20261107 ... --smoketest=ui --hero-presentation-only`及 `inventory_native_final_tangseng_20261107 ... --smoketest=ui --tangseng-only --hero-presentation-only`：均退出0，分别SMOKE-PREVIEW-NATIVE 62/1帧；双向普攻根位置、60/120Hz二跳、EXP截图及唐僧细柱内外真实目标通过。退出分别5/6个ObjectDB，悟空1条资源占用。 |
| 两英雄正式GUI穿卸 | `inventory_native_wukong_wardrobe_final_20261107 ... --smoketest=ui --equipment-only`、`inventory_native_tangseng_wardrobe_20261107 ... --smoketest=ui --tangseng-only --equipment-only`：均退出0，各15件源表可穿装备、分页/战斗与专用预览换装/卸装复位/存档通过。悟空完整UI回归同时通过；退出ObjectDB/资源/RID诊断保留，见各stderr。 |
| 普通首领死亡 | `run_godot.py boss_death_statue_final_20261107 ... --smoketest=ui --boss-death-only`：退出0、14例PASS，实际旋涡播放到可见帧后截图，检查闪烁、唯一效果、完成回收及复用恢复。查看`boss_death_TamarinKing_120.png`及石像截图；stderr仅5个ObjectDB退出诊断。 |
| 牛魔形态回归 | `run_godot.py boss_death_bull_regression_20261107 ... --smoketest=ui --bull-only`：退出0，冲撞/卫兵、五招双向60/120Hz、心脏淡入淡出/手掌火焰、致死赠宝/重复/满包/死亡完成/订退通过；9个ObjectDB退出诊断。 |
| 龙王攻击回归 | `run_godot.py boss_death_dragon_regression_20261107 ... --smoketest=ui --dragon-only`：退出0，两难度/双向/两频率全部攻击、控制与效果取消通过；6个ObjectDB及1条资源退出诊断。 |
| 龙王死亡与传送 | `run_godot.py boss_death_dragon_flow_final_20261107 ... --smoketest=ui --dragon-flow-only`：退出0，真实下水/暂停淡入淡出/GGF传送、收益保持、首次旗幡拾取、原生死亡、W传送门、磁盘检查点与返回地图通过；11个ObjectDB及3条资源退出诊断。 |
| 玄冰原生范围 | `run_godot.py dark_ice_shapes_final_20261107 --rendering-method gl_compatibility --audio-driver Dummy --script res://.godot/validation/hero_alignment_20261107/render_dark_ice_shapes.gd`：退出0、stderr空；源1/5/6/11/16/30/35/40/45帧物理查询/形状叠图通过，已查看`dark_ice_shapes_montage.png`。 |
| 加载取消 | `run_godot.py hero_loading_cancel_final_20261107 ... --smoketest=level-cancel`：退出0，中途显示取消、迟到实例回收、不推进关卡通过；8个ObjectDB及1条资源退出诊断。 |
| 资源登记 | `run_godot.py hero_alignment_collection_final_20261107 --headless --editor --script res://EditorScripts/regenerate_collection.gd --no-window`：退出0、979项资源由既有生成器更新；编辑器退出仍有ComponentInsoector/free、RID/ObjectDB诊断。 |
| 引用/格式/作者工具 | `python Tools/ProjectMaintenance/validate_game_references.py`：2246路径/388UID；`dotnet run --project Tools/ProjectMaintenance/CSharpStyle -- . Godot/GodotProject/.godot/validation/hero_alignment_style_applied_20261107.json --write`：362自有文件、0缺XML，token保持整理9文件。四个变更Python作者工具py_compile及`git diff --check`通过。 |

加载实测的场景/HUD资源就绪、英雄显示、HUD就绪分别为：原串行`hero_alignment_baseline_20261107` 1259/2246/2277ms，并行初跑`hero_alignment_pipeline_20261107` 1062/1106/1152ms；后者之后在旧“分身缩放必须为1”的断言失败，不能称整组技能通过，但该加载阶段测量有效。最终完整唐僧场景为393/445/489ms，属于后续缓存条件，不拿它与首次串行数字直接计算性能提升。性能收益来自缩短串行资源等待，不承诺消除所有首次磁盘/纹理导入延迟。

背包导出复现接口为 `export_official_inventory_preview.py --swf <恢复后的backpack.swf> --xml <backpack.xml> --work <忽略目录> --java <java.exe> --ffdec <ffdec.jar> --godot <Godot控制台> --manifests <已审阅装备manifest目录...>`；源SHA和selector见LegacyAssetMap。重用已审核的衣装身份/深度分层与紧凑打包，不手改原图或生成常量。初次将自定义Resource属性写在script前导致null，修正脚本声明顺序后以本节最终两英雄专项为准；旧测试要求战斗与预览纹理同一引用，也已同步改成对应专用资源。

## UI与唐僧表现13项收尾（2026-11-06）

以下逐项对应活动目标。依据第30章原生动画与少量基础判定形状、第40章源注册/实际渲染和第70章存档边界，复用已有状态、池、飘字及GGF窗口；来源和实现结论见OfficialReference、LegacyAssetMap。runner为 `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py`，日志在同级案例目录；运行参数中的 `...` 均为 `--rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 --`。

| 目标 | 最终结果与证据 |
| --- | --- |
| 1 技能按钮循环 | SelectPLace showBuySkill的普通态1帧静止，hover15帧、available19帧按24fps循环；`map_buttons_token_20261106 ... --smoketest=frontend --map-buttons-only`实际hover/out/可学态通过。 |
| 2 保存按钮动画 | 官方是SimpleButton，没有持续循环；保留其静态/交互态，不虚构技能式循环。与地图同一专项核对真实保存行为。 |
| 3 设置令牌 | 复用原map_return_title令牌，标题“游戏设置”，原生AnimationPlayer悬停缩放1↔1.04，移出复位；地图专项与截图`frontend/map_settings_token_hover.png`通过。 |
| 4 存档删除与面板 | 旧项目棕色确认框/金色按钮/原字体；小叉24px、默认42%透明度，仅可读存档显示。原图、真实鼠标、取消/删除/主备文件/重读均通过，精确命令见下一节。 |
| 5 背包角色位置 | 官方backpack_v2裸装注册比较后只在唐僧预览补 `(2,6)`，共用锚点不动。`tangseng_preview_sampling_final_20261106 ... --smoketest=ui --tangseng-only --hero-presentation-only`、`wukong_preview_natural_final_20261106 ... --smoketest=ui --hero-presentation-only`均退出0；0/0.5/2.7/3/3.6秒真实渲染采样已查看。 |
| 6 命中效果 | Role_v7的MonsterBeHurt1/2八帧闪光，唐僧hue+160、幻影原色；`tangseng_effects_release_final_20261106 ... --smoketest=ui --tangseng-only --effects-only`通过普攻/冰龙实际闪光，已查看`tangseng_normal_hit_feedback_60.png`。`enemy_lethal_flash_final_20261106 ... --smoketest=ui --enemy-death-only`六组60/120Hz致死闪光、抛飞、落地回收及一次死亡收益通过，退出0、6个ObjectDB诊断。 |
| 7 二跳下落 | HeroAirState从原生complete标记取翻身时长；两英雄表现专项60/120Hz实际双跳均完整翻身、及时切下落并落地。源hit1/jump2/jump3同原点逐帧图与实机截图已查看。四项规则测试通过。 |
| 8 冰龙蓄力条 | 原生ProgressBar复用官方ExceedPower尺寸/位置、绿条红框、满蓄闪色和半秒渐变。`tangseng_physics_charge_heal_musou_20261106 ... --smoketest=ui --tangseng-only`两种物理频率实际蓄力/暂停/取消/复用通过；已查看`tangseng_charge_60_full.png`。 |
| 9 全英雄无双 | HeroSystem消费AttackData/DamageResult，按getBasePower的普通/特殊阶段基准、本体/离身减伤规则结算。`dotnet test Tests/ProfileTests --no-restore -v minimal`274/274，`dotnet test Tests/BattleTests --no-restore -v minimal`195/195；唐僧实际普通命中+2、冰龙多次命中逐次增长，60/120Hz专项通过。 |
| 10 普攻前移 | 官方hit1的探身保留；两英雄表现专项60/120Hz、左右四组各逐物理步检查，根横坐标变化均0，未人为补偿源姿态。 |
| 11 幻影重力 | CharacterBody2D共用本体普通重力/身体基线及地形层，施法停住；`tangseng_physics_charge_heal_musou_20261106`两种物理频率的下落/地面/墙、暂停、施法、八秒寿命和回池复用通过，已查看`tangseng_clone_gravity_120.png`。 |
| 12 甘露数字 | 实际Heal恢复量进入既有FloatingTextPresenter，本体及幻影显示绿色20；同一专项和最终effects专项通过，已查看`tangseng_dew_heal_number.png`、`tangseng_clone_dew_heal_number.png`。 |
| 13 全技能判定 | 普通/冰龙基础形状修正，玄冰46帧左右生长/淡出阶段及无双缩放完整复核，甘露无攻击形状。水魔原动画11–20帧的250×420大矩形改为细柱+上环+水花，Tang表现专项四组真实目标验证柱心命中、侧空白不命中、上环/水花命中及池复用；最终effects专项确认实际二次L流程正常。判定是基础形状近似，不宣称Flash像素级一致。 |

追加的预览立棍核对：原生backpack252是62帧短循环，项目按用户要求复用Role_v7完整100帧战斗wait。`wukong_preview_natural_final_20261106`在不Seek的正式窗口自然播到立棍并循环回普通姿态，`SMOKE-PREVIEW-NATURAL PASS`，查看`wukong_preview_standing_pole.png`；唐僧3秒低头姿势同样可见。先前同一回调连续Seek后的ForceDraw截图会读到旧渲染，已经改为等待真实帧；旧静止截图不作为完整循环证据。

追加的玄冰无双位置核对：`dark_ice_registration_native_final_20261106 --rendering-method gl_compatibility --audio-driver Dummy --script res://.godot/validation/hero_presentation_20261106/render_dark_ice_registration.gd`退出0、stderr空。Role2.SpecialAttack的源x/y与角色相同，只在无双绕原点缩放 `(1.26,1.8)`；第21源帧同锚点原生图/正式场景的alpha>64边界普通均为 `(130,92,501,196)`，无双均为 `(81,46,548,233)`。已查看`hero_presentation_20261106/dark_ice_normal_musou_registered_comparison.png`；底边下延属于源缩放，无额外Y偏移。`tangseng_dark_ice_position_final_20261106 ... --smoketest=ui --tangseng-only --effects-only`再次通过实际本体/幻影普通及无双施放、共同根坐标与全部效果回归，退出0、仅6个ObjectDB退出诊断；已查看`tangseng_dark_ice_{normal,musou}_registered.png`两张实际战斗截图。

最终实机专项均退出0；唐僧表现、悟空自然预览分别只有6/2个ObjectDB退出诊断，`tangseng_effects_release_final_20261106`有14个ObjectDB及4条资源占用诊断。未将通用“SMOKE PASS”的文字当成未执行分支的证据。951资源登记由既有Collection Res生成器完成；`dotnet build Godot/GodotProject --no-restore -v quiet`0错误/5个既有框架警告；CSharpStyle `hero_ui_closeout_style_20261106.json`361个自有文件通过、token保持；`python Tools/ProjectMaintenance/validate_game_references.py`2178路径/386UID通过，三份修改的迁移脚本py_compile和`git diff --check`通过。`hero_ui_closeout_scan_20261106 --headless --build-solutions --quit --no-window`退出0、完成脚本扫描与解决方案构建，并生成新partial的UID；退出仍有既有ComponentInsoector/free诊断，未修改只读插件。

## 存档删除与原项目面板（2026-11-06）

按第40章旧资源迁入、第70章SaveSystem/ArchiveUtility边界及用户本轮美术要求完成。旧 `choose_scene.tscn` 的五张PNG与华康宋体W3字体原样迁入，删除入口只显示于可读存档，24×24、默认42%透明度、鼠标移入提亮；删除确认复用原面板和按钮字图，清理先前自绘StyleBox和额外关闭叉号。

| 验证 | 命令/结果 |
| --- | --- |
| 构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：退出0，0错误、5个既有框架弃用警告。 |
| Godot资源导入 | `run_godot.py import_delete_confirm_assets_20261106 --headless --editor --quit --no-window`、`import_delete_confirm_font_20261106`：均退出0，完成六项导入；编辑器退出仍有既有ComponentInsoector/free诊断。 |
| 实机删除与视觉 | `run_godot.py save_delete_original_art_20261106 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend --save-delete-only`：退出0，`SMOKE-SAVE-DELETE PASS`；真实鼠标验证空槽隐藏、24px暗色图标、悬停提亮/离开复位、原图载入、取消不删、确认删除主备文件、其他格保留、同槽新建唐僧、标题磁盘目录重读与关闭后的迟到确认。此运行stderr为空。 |
| 截图 | 已查看`.godot/validation/frontend/save_delete_slots.png`、`save_delete_hover.png`、`save_delete_confirmation.png`，确认棕色原面板与原按钮字图、空槽无叉号、正文无遮挡。截图前等待造档成功提示自然结束。 |
| 最终资源与格式 | `run_godot.py save_delete_resources_20261106 --headless --editor --script res://EditorScripts/regenerate_collection.gd --no-window`：退出0、916资源生成，编辑器退出仍有已知插件/free及RID诊断；`python Tools/ProjectMaintenance/validate_game_references.py`：2106路径、385UID通过；CSharpStyle `style_final.json`：358文件、0格式差异、0缺失文档；源5PNG+字体逐字节核对与`git diff --check`通过。 |

最初`save_delete_official_ui_20261106`虽有逻辑PASS但新PNG未导入、字体未迁入，发生资源解析错误；此案例不算视觉通过，最终以`save_delete_original_art_20261106`为准。本节只覆盖存档专项，完整13项结果见上方收尾记录。

## 动画方法入口与角色相关表现收尾（2026-11-05）

按第10章动画入口命名、第30章状态与表现分层和Stop Ladder复核全项目 `*AnimEvent` 调用链。`AnimEvent` 仍只作为AnimationPlayer方法轨道键；普通业务使用无后缀实现，轨道方法只转发：唐僧音效、SkillEffect攻击刷新/结束、敌人攻击刷新和虾兵弹体均已收敛。烟测不再直接调用动画事件名，改为真实播放 `attack_1` 的第3源帧。

| 检查 | 命令与结果 |
| --- | --- |
| 编译/格式/引用 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有框架弃用警告；`dotnet run --project Tools/ProjectMaintenance/CSharpStyle -- . .../animation_event_boundaries_style_final_20261105.json`：358文件、0格式变更、0缺失文档；`validate_game_references.py`：2098路径、385 UID。 |
| 唐僧/海怪事件边界 | `animation_event_boundaries_tangseng_20261105`、`animation_event_boundaries_sea_20261105`：退出0；唐僧技能/八段声音/玄冰与水魔效果、虾兵真实弹体命中/消散/暂停/池复用通过。退出期仅有既有ObjectDB/资源诊断。 |
| 悟空/彭魔王事件边界 | `animation_event_boundaries_wukong_20261105`、`animation_event_boundaries_garuda_20261105`：退出0；悟空五技能/独立效果/声音与实际命中，彭魔王四招/冲刺/复生/回池通过。 |
| 共用功能唐僧 | `tangseng_shared_physics_status_20261105`：退出0；官方平台双跳、狮驼王六招/中毒、60/120Hz冰冻控制锁与死亡清理通过。 |
| 吞噬角色选择 | `tangseng_capture_bull_20261105` 与 `wukong_capture_bull_20261105`：退出0；两角色60/120Hz、双朝向、源五次脉冲、释放和selector图集选择通过。唐僧专属图集导入扫描 `tangseng_capture_import_20261105` 退出0。 |

退出期仍报告ObjectDB/RID/资源占用；编辑器扫描另有已知插件free诊断。未据专项通过声称这些诊断已解决，也未修改只读插件。

## 唐僧完整流程、目录与悟空共用功能对照（2026-11-05）

| 检查 | 精确入口与结果 |
| --- | --- |
| 唐僧完整生命周期 | `run_godot.py tangseng_complete_role_validation_20261105 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 24000 -- --smoketest=ui --tangseng-only --tangseng-lifecycle-only`：退出0；正式新档、五主动学习/施放、15件实际拾取与GUI穿卸、60/120Hz无双/受击保护、25次被动购买、平地/坡面死亡重试、地图/标题/同槽磁盘重读、装备GUID/随机属性/槽位/技能与被动保持，再次穿卸通过。退出25个ObjectDB及5条资源占用诊断。 |
| 十关主线 | `tangseng_chapter_gate_filtered_20261105` 用户参数 `--smoketest=progress --tangseng-only`：真实完成1..8，每关经标题重读；外部runner在900秒终止（124），保留原日志。复制这份隔离APPDATA到`tangseng_chapter_resume_disk_20261105`，以同一档案 `Validation/Progress/c55712374bf247d0ba774bc84938c197` 续跑；日志明确 `next=9, hero=2, read from actual disk profile`，实际完成9、10并再次标题重读，退出0。现有可重现续跑参数为 `--smoke-run-id=c55712374bf247d0ba774bc84938c197`，固定在Validation目录，不修改正式存档。最终18个ObjectDB及3条资源占用诊断。 |
| 隐藏龙宫 | `tangseng_dragon_role_regression_20261105`，用户参数 `--smoketest=ui --tangseng-only --dragon-flow-only`：退出0，实际下水/暂停淡出/GGF转场/奖励保留/首次旗幡拾取/龙王死亡/W出口/磁盘/地图通过；退出11个ObjectDB及3条资源占用诊断。 |
| 悟空既有流程 | `wukong_role_final_20261105`，用户参数 `--smoketest=ui --terrain-level5`：退出0；全部背包/技能、HUD退订/实体复用、七星洞坡面、事务/失败/当前格式读写及备份重入通过。退出138个ObjectDB、10条资源占用及RID诊断。 |
| 悟空无双与墓碑 | `wukong_musou_role_regression_20261105 -- --smoketest=ui --musou-hurt-only`、`wukong_tombstone_role_regression_20261105 -- --smoketest=ui --hero-death-only`：均退出0；悟空专属立绘、白光/霸体、8帧受击闪光和60/120Hz平地/坡面墓碑、重试/地图重入通过。分别退出3与5个ObjectDB，后者1条资源占用诊断。 |
| 目录与生成 | `musou_activation/{wukong,tangseng,shared}`、`hero_hurt/{wukong,tangseng}`、唐僧技能素材 `Effects/tangseng/skills`、悟空预览动画 `Animations/Heroes/Wukong`；全部携UID更新引用与作者工具。`hero_resources_collection_closeout_20261105 --headless --editor --script res://EditorScripts/regenerate_collection.gd --no-window`：退出0，明确 `Collection complete. 910 resources saved`；退出有既有编辑器插件/free与RID诊断。 |
| 规则与工具 | `dotnet test Tests/ProfileTests --no-restore -v minimal`：260/260；`python -m unittest discover -s Tests/ToolTests -v`：24/24（包含唐僧就绪门）；第40章的可玩角色装备门已将唐僧从跳过名单改为身份/动作/GUI三项必验。 |

所有引擎案例均通过同一个 `Godot/GodotProject/.godot/validation/movement_presets/run_godot.py` 执行，除单独列出的headless导入/生成外使用OpenGL兼容渲染及Dummy音频。角色功能/技能命中由专项覆盖；主线清场夹具使用提高HP和直接攻击包清怪，十关证据证明移动/刷怪/门/结算/解锁/保存流程，不声称全程用唐僧技能击败每个Boss。

失败夹具的修正：悟空技能官方透明身体格91仅允许在烈焰闪/火魔斩的对应阶段；敌人1/2/3旧编号改为实体枚举；阶段门探针只取双方原有PlayerBarrier掩码，避免世界高台混入。原失败日志保留，不修改正式素材或放宽实际游戏规则来迎合测试。

## 唐僧官方音频与输入触发（2026-11-05）

按第20章声音事件编码、第30/50章GGF/原生方法轨道和当前Stop Ladder复用要求执行。已有官方技能音频导出器增加`--hero tangseng`，8段来自登记的Music.swf原MP3载荷。新增声音源表仅Resources与__enums__；不新增死亡音、不改悟空声音或运行期音频架构。

| 检查 | 命令与结果 |
| --- | --- |
| 官方导出 | `python Tools/LegacyMigration/export_official_skill_audio.py --source Godot/GodotProject/.godot/validation/wukong-skills-export/audio/music.swf --work Godot/GodotProject/.godot/validation/tangseng_audio_20261105/source --java "S:/Unity Editor/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe" --ffdec "$env:TEMP/gamezmxy-ffdec/ffdec.jar" --output Godot/GodotProject/TheGame/Audios/SFX/tangseng --hero tangseng`：退出0，既有固定源SHA检查通过，原样复制8段MP3。 |
| 作者化与原包 | `.godot/validation/tangseng_audio_20261105/author.mjs --edit`由Artifact作者化并重算、inspect/零公式错误扫描和前后render；两个视图已查看。preserve.py逐表核对全部值/公式后仅局部添加8行及共享字符串；原ZIP的样式/关系/其他Sheet和元数据部件逐字节保留。 |
| XLSX Diff | 同目录`verify_diff.mjs`使用已安装1.6.4真实worker：Resources普通/公式均56格、__enums__均24格；全部右侧Sheet非空。此结果不代替用户VS Code面板恢复确认。 |
| Luban | `$env:AI_MODE='1'; cmd /c Configs\GameConfig\gen_code_bin_to_project_lazyload.bat`：退出0；只更新SoundId.cs和sound_tbsoundconfig.bytes。 |
| 构建/纯测 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有框架弃用警告；`dotnet test Tests/ProfileTests --no-restore -v minimal`：260/260，NU1900网络警告保留。 |
| Godot导入 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py tangseng_audio_import_20261105 --headless --editor --quit --no-window`：退出0，8段导入完成；退出仍有既有ComponentInsoector/free诊断。 |
| 正式输入与声音 | 同run_godot.py入口案例`tangseng_audio_verified_20261105`，参数`--rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=ui --tangseng-only --effects-only`：退出0，8段经GGF SFX解码/无循环/停止，60/120Hz短按/未满/满蓄/低蓝复用、分身创建与换位、普通/无双玄冰仅本体发声、甘露分身两次起手、水魔无追加无声音/追加一次、真实命中与受击全部PASS。原生方法键核对普攻0、玄冰14/24、水魔下落13/24秒。 |
| 引用/格式 | `python Tools/ProjectMaintenance/validate_game_references.py`：1958路径、384UID；`python -m py_compile Tools/LegacyMigration/export_official_skill_audio.py`与`git diff --check`通过。两份改动C#通过既有CSharpStyle的token/注释保持格式化。 |

初轮普攻在伤害发生的同帧检查GGF派发前的命中音计数而失败；等待实际成功播放事件后修正。后续把异步成功播放时的动画位置当作原生键时间也失败，已改为检查实际动画键并分别观察播放结果，没有修改生产时点以迎合测试。最终专项退出仍有14个ObjectDB及4条资源占用诊断，未声称退出无错误。证据目录`.godot/validation/tangseng_audio_20261105/`及`movement_presets/tangseng_audio_*_20261105/`。全部装备实际穿卸、共用被动/状态、完整通关与磁盘重读仍待，原目标保持。

## Stop That Shit 安装与项目规则（2026-11-05）

用户要求安装插件并作为项目规范。按固定tag的INSTALL_FOR_AGENTS执行Codex原生安装，未克隆仓库、直接编辑宿主配置或绕过Hook信任；工程规则纳入ProjectGuidelines/AGENTS.md，安装/使用边界归第50章。

| 检查 | 命令与结果 |
| --- | --- |
| 宿主 | `codex --version`：0.154.0；`node --version`：v24.14.0。 |
| Marketplace | `codex plugin marketplace add lennney/stop-that-shit --ref 0.2.5 --json`：退出0，注册stop-that-shit marketplace，固定tag。 |
| 安装 | `codex plugin add stop-that-shit@stop-that-shit --json`：退出0，version0.2.5，安装目录为`C:/Users/zheliku/.codex/plugins/cache/stop-that-shit/stop-that-shit/0.2.5`。 |
| 状态 | `codex plugin list --marketplace stop-that-shit --json`：installed=true、enabled=true、Git来源为指定仓库。 |
| 包与文档 | 已阅读安装版SKILL.md及hooks/codex-hooks.json；`node --check <安装目录>/hooks/stop-that-shit.cjs`退出0。`git diff --check`通过；只修改中央规范与当前记录。 |

尚未完成：重启后的宿主发现、CLI `/hooks`用户审阅/信任、显式任务模式后的实际Guard拒绝/允许烟测。上游安装说明要求用户自行审阅并信任，未用直接执行Hook、修改信任文件或伪造用户prompt代替。当前证据只支持插件已安装启用及工程规则已落地，不支持Guard已在本会话拦截。原唐僧目标仍继续，音效表工作稿未写回。

## ConfigurationSystem实例初始化与注册依赖（2026-11-05）

按用户当前要求移除静态ValidateTables、ValidateSkillCombat、ValidateSkillCatalog对外入口和Utility可空参数/兜底构造。ConfigurationSystem.OnInit只借用所属架构中已注册的ConfigurationModel、ExperienceCurveUtility、ConfigIdUtility；全部内部校验方法为私有实例方法。经验表验证仍由工具构造时完成，Procedure注册失败沿原失败出口终止。测试通过现有TestArchitecture按同一依赖顺序装配并执行ICanInit，未新增生产测试入口。第20章旧ValidateTables方法名尚未据此改写规范正文。

| 检查 | 命令与结果 |
| --- | --- |
| 构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有框架弃用警告。 |
| 同生命周期规则测试 | `dotnet test Tests/ProfileTests --no-restore -v minimal`：260/260；正式表初始化通过，编号/重复基础身份/攻击预算/英雄输入非法配置均由实例OnInit拒绝。测试包保留NU1900联网警告。 |
| 依赖/旧入口检查 | 全项目C#引用检查无外部静态ConfigurationSystem校验调用；ConfigurationSystem内无static、new Utility、可空工具参数或工具缺失兜底。`git diff --check`通过。 |
| 实际启动 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py config_system_registered_init_20261105 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=frontend --level-guide-only`：退出0，正式Prepare注册/配置OnInit、十地点图鉴、悬停/两种尺寸及真实点击进关PASS。退出仅剩3个既有ObjectDB诊断，无资源占用错误。 |

本次涉及的配置系统与测试文件通过CSharpStyle审计，0格式差异、0缺失XML。上一节编号专项的历史通过不替代本次初始化链路验证。唐僧声音表仍未编辑，后续继续原目标。

## 配置ID工具统一与注册链路（2026-11-05）

按用户本轮明确要求，ConfigIdUtility位于`TheGame/GameScripts/Utilities/Utility/`，实现现有IUtility，在ProcedurePrepare和独立TestArena注册。编号公式与范围判断只保留在该实例工具中；实体、图鉴、启动校验与烟测通过GetUtility取用，独立规则测试借用同一实现。未为本项改表、重编枚举、修改存档或改写规范正文。第00章的纯静态规则描述不覆盖用户此次指定的注册型工具形式。

| 检查 | 命令与结果 |
| --- | --- |
| 构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误，5条既有框架弃用警告。 |
| 规则/配置/图鉴 | `dotnet test Tests/ProfileTests --no-restore -v minimal`：260/260通过，含固定编号样本、跨实体/环境误归属启动拒绝、实体范围边界、毒云与龙王高等级档。NuGet漏洞数据查询仍有NU1900网络警告。 |
| Godot扫描 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py config_id_utility_instance_scan_20261105 --headless --build-solutions --quit --no-window`：退出0、扫描完成；退出仍有既有ComponentInsoector与ArchiveSettingInspectorPlugin的free诊断。 |
| 鲨魔王/鲨鱼 | 同run_godot.py入口，案例`config_ids_shark_20261105`，参数`--rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=ui --shark-only`：退出0；60/120Hz、双朝向、两阶段八个出招选择、水牢警示/取消、鲨鱼斩击和召唤/死亡/复用全部PASS。 |
| 毒云 | 案例`config_ids_qixing_20261105`，同上参数改`--qixing-only`：退出0，六招双朝向、飞刀/毒云命中、持续中毒、暂停/复用PASS。 |
| 龙王 | 案例`config_ids_dragon_20261105`，同上参数改`--dragon-only`：退出0；普通/高等级四招、60/120Hz、双朝向及控制/暂停/弹体取消PASS。 |
| 地图图鉴 | 案例`config_ids_guide_20261105`，同上用户参数改`--smoketest=frontend --level-guide-only`：退出0；十张真实图鉴、首领顺序、两种尺寸、嵌套悬停与点击进关清理PASS，验证Query取用已注册的工具。 |
| 引用/重复规则 | `python Tools/ProjectMaintenance/validate_game_references.py`：1958路径、384UID通过。全项目手写C#/GDScript/Python搜索确认实体攻击/首领池乘法、攻击解析和敌人范围分组不再散落；固定源表枚举、场景引用及独立期望值保留。`git diff --check`通过。 |

上述四项实际引擎案例退出时仍分别有9/9/7/5个ObjectDB实例与各1条资源占用诊断；不以专项PASS宣称退出期无错误。CSharpStyle审计357个手写文件、0缺失XML；新工具与更新的编号测试单独通过token/注释保持格式化，未批量改动其余历史格式差异。证据目录为`.godot/validation/config_id_utility_20261105/`及`movement_presets/config_ids_*_20261105/`。这些结果不替代仍在进行的唐僧声音、完整穿卸/状态/章节通关/磁盘重读验收。

## 唐僧瞄准圈注册、特效层级与技能运动（2026-11-05）

依第30章具体状态负责运动/分支、第40章完整父级矩阵和原生资源定位、第50章实际渲染验证执行。水魔圈补回官方body矩阵，与此前相比下移19.95px、横向补12.45px并随朝向镜像。唐僧离身场景z=2，血条层保留。玄冰本体/分身同步起手、各自朝向、无双缩放/额外刷新区别与最终整数威力对齐；删除错误统一冻结五招的中间基类，恢复冰龙空中运动与瞬发水幻影动量。

| 检查 | 结果 |
| --- | --- |
| 官方完整父级 | `python Godot/GodotProject/.godot/validation/water_ball_position_review_20261105/prepare_reference.py`：退出0，核验SWF/XML散列，生成hit4完整body父级41源帧双朝向参考；第10/20/30/41帧与修正前后Godot截图已查看。 |
| 正式场景注册 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py water_ball_all_registered_frames_20261105 --rendering-method gl_compatibility --audio-driver Dummy --quit-after 300 --script res://.godot/validation/water_ball_position_review_20261105/render.gd`：退出0、0错误；第10–41共32源帧×双朝向实际global_transform与官方注册点全部一致。像素不是全等：原生FFDec与Godot滤镜边缘不同，未据坐标通过宣称整幅像素相同。 |
| 正式选角/学习/技能 | 同run_godot.py入口，案例`tangseng_full_fidelity_verified_20261105`，参数`--rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=ui --tangseng-only`：退出0；分身walk/hit3/hit5/暂停/取消复用/192帧寿命，J短按/未满/满蓄/低蓝回退，空中蓄力/松方向/重力/瞬发分身动量/空中水魔拒绝，普通与无双玄冰同步/朝向/缩放/威力/独立刷新/层级，甘露末帧治疗、水魔圈/超时/追加独立命中均PASS，前四组覆盖60/120Hz。四张最新战斗截图已查看。 |
| 清理后复跑 | 案例`tangseng_cleanup_verified_20261105`，同上参数加`--effects-only`：退出0；删除玄冰本体空判定轨道、甘露重复绑定与无用回调场景参数后，J输入、空中运动、普通/无双玄冰、甘露和水魔链再次全部PASS。此次退出剩5个既有ObjectDB诊断，无资源占用错误。 |
| 构建与引用 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有框架弃用警告。`python Tools/ProjectMaintenance/validate_game_references.py`：1958路径、383UID通过。`git diff --check`通过。 |

首轮无双案例在激活演出的暂停中发送输入而超时，测试改为等待演出释放暂停后重跑；空中夹具首轮在MoveAndSlide刷新传送前落地缓存前设置速度，改为进入Air后设定待测速度，未修改生产运动迎合错误夹具。参考渲染初版遗漏scaleY导致全透明，已修正重导；旧Seek采样值轨道的下一键问题改用原生Advance后复核。最终完整专项退出仍有既有8个ObjectDB实例和1个资源占用诊断。证据在`.godot/validation/water_ball_position_review_20261105/`和`movement_presets/tangseng_full_fidelity_verified_20261105/`。完整装备穿卸、全部状态/被动、章节通关及磁盘重读仍待，不据通用SMOKE尾句认定已验收。

## 唐僧特效全帧清理与透明图集（2026-11-05）

依据第40章源注册点与第50章实际渲染验证约束执行。用户明确要求去掉玄冰阵蓝色弧光；源604父轴完整核对为第5–11帧，595/597/599/601/603五张形状，已全部排除。593冰柱及其碎冰、滤镜和淡出保留。甘露仅对源703的ADD图做颜色/alpha等效转换，保留ADD材质，关闭该贴图fix_alpha_border。四组图集复用compact_registered_atlas与AtlasTexture.margin；不重新居中、删除动画帧或修改伤害判定。水魔球素材源注册offset=(-0.85,-11.3)；正式场景叠加hit4父级矩阵后的修正见上节。

| 检查 | 命令与结果 |
| --- | --- |
| 官方重导 | `python Tools/LegacyMigration/export_official_tangseng_effects.py --role Godot/GodotProject/.godot/validation/wukong-skills-export/role.swf --xml Godot/GodotProject/.godot/validation/action_origin_review/fresh_role.xml --java "S:/Unity Editor/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe" --ffdec "$env:TEMP/gamezmxy-ffdec/ffdec.jar" --work Godot/GodotProject/.godot/validation/tangseng_effects_20261105/export --only dark_ice water_demon_fall cure_role water_demon_ball --install-to Godot/GodotProject/TheGame/Sprites/Effects/tangseng`：退出0，Role_v7 SHA校验通过，125源帧。 |
| 帧/原点回读 | `python Godot/GodotProject/.godot/validation/tangseng_effects_20261105/verify.py`：125帧通过；从正式SpriteFrames回读region/margin与源画布逐帧比对，普通混合可见RGBA相同，甘露ADD光量误差≤1/255。普攻/冰龙单图与原始导出逐字节相同。 |
| 存储裁切 | 玄冰阵2862×1536→2048×980；甘露2892×1662→1024×2090；水魔下落1602×2160→1024×1972；水魔球1176×332→4096×91。水魔下落22–25及玄冰阵45–46空帧仍保留原时长，共用1px透明存储；重复帧只共用贴图。 |
| 实际GPU对比 | `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py tangseng_effect_gpu_verified_20261105 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --script res://.godot/validation/tangseng_effects_20261105/render.gd`：退出0、0错误、250张深浅背景双栏对比。逐图比较左右550×500区域，玄冰/球/下落最大通道差0，甘露最大差1；gpu_report.json已记录。 |
| 正式技能回归 | 同run_godot.py入口，案例`tangseng_effects_verified_runtime_20261105`，参数`--rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=ui --tangseng-only --effects-only`：退出0，正式学习控件、34帧甘露末帧治疗、玄冰真实命中、移动roundMc、无追加输入收招、二次释放落点及独立命中/回池PASS；实际截图已查看。退出仍有既有8条ObjectDB泄漏与1条资源占用诊断。 |
| 构建与引用 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0警告、0错误；`python Tools/ProjectMaintenance/validate_game_references.py`：1957路径、384UID通过；`python -m py_compile Tools/LegacyMigration/export_official_tangseng_effects.py`及`git diff --check`通过。 |

首轮GPU脚本的Vector2/Vector2i比较解析错误已修复。后续对比发现最近邻采样需对齐像素中心，以及甘露fix_alpha_border会在新alpha编码后改写半透明颜色；均修正后按上表最终案例复跑，未以最初截图代替通过结果。编辑器导入退出仍有既有ComponentInsoector/ArchiveSettingInspectorPlugin释放诊断，未修改只读插件。证据根为`.godot/validation/tangseng_effects_20261105/`；本项不代替唐僧全技能、全部状态、正式穿卸、完整通关与磁盘重读验收。

## 配置身份、攻击、掉落池与声音编码（2026-11-04）

依据 `ProjectGuidelines/20-Configs.md`，本轮统一 EntityId/EnemyId、去除实体资源重复主键，按实体/动作/段号编码攻击，按品质或 Boss 身份编码掉落池，并整理声音事件段。HeroId、EquipmentId、SkillId、PassiveId 等持久身份未重编。

| 检查 | 实际命令与结果 |
| --- | --- |
| 正式导表 | `$env:AI_MODE='1'; cmd /c Configs\GameConfig\gen_code_bin_to_project_lazyload.bat`：成功，`bye~`；C# 与 bytes 仅经此流程生成。 |
| 正式构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误，5 条既有框架弃用警告。 |
| 配置/档案测试 | `dotnet test Tests/ProfileTests/ProfileTests.csproj --no-restore -v quiet`：246/246；最终复跑通过，另有无法获取 NuGet 漏洞数据的 NU1900 网络警告。 |
| 战斗测试 | `dotnet test Tests/BattleTests/BattleTests.csproj --no-restore -v quiet`：191/191。 |
| Schema/可读性 | `audit_config_schema.py`：17 个映射 Sheet、0 错误；`audit_config_readability.py`：11 本表、24 个枚举列，英文值/缺失别名/错误均为0。 |
| 引用/包格式 | `validate_game_references.py`：2072 路径、378 UID 通过；`normalize_xlsx_namespaces.py`：0 本待修复；`git diff --check` 通过。 |
| XLSX Diff 解析 | 已安装 1.6.4 worker 对 Resources/Enemy/Combat/Items/__enums__/__tables__ 的值与公式两模式均返回全部非空 Sheet；右侧变化格分别103/26/677/76/162/1（值模式）。表值/公式与审核稿逐项相同，未编辑 ZIP 部件逐字节保留。Artifact 预览检查实体主键、英雄攻击与掉落分组。 |
| Godot 扫描 | `S:/Godot4/Godot4CSharp_console.exe --headless --editor --path Godot/GodotProject --build-solutions --quit --no-window`：退出0；退出期仍有既有 ComponentInsoector RefCounted/free 诊断。 |
| 实际 OpenGL 音乐 | 隔离 APPDATA 的 `run_godot.py identity_music_20261104 --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 18000 -- --smoketest=ui --chapter-music-only`：退出0；洞窟/海底/牛魔谷播放、进度推进、暂停停止及洞窟场景绑定通过；退出期3条既有 ObjectDB 泄漏诊断。 |
| 悟空实际回归 | 同入口 `identity_hero_20261104 ... -- --smoketest`：退出0，39次状态迁移、5次真实猴子受击、连击 HUD 显示；退出期8条 ObjectDB 与1条资源诊断。 |

证据位于 `.godot/validation/identity_layout_20261104/` 和 `movement_presets/identity_*_20261104/`。worker 检查不能代替 VS Code 面板显示验证；用户此前确认 Hero 正常、其他表空白，目前尚无实际面板恢复的证据。此前对字符串类型或缓存的解释没有完成界面复现，不能认定为根因。唐僧五技能、完整通关与磁盘重读继续验收，未在此标记完成。

## XLSX Diff 共享字符串回退复核（2026-11-04）

此前将三张表所有文本统一为 `t="s"` 共享字符串后，插件 worker 仍能读到差异，但 VS Code 面板仍显示空白。已确认 Hero 使用原生 `str`/`inlineStr` 混合存储；本次从已逐格核对的原生 ZIP 保留包恢复 `__enums__`、Resources、Combat 的文本存储，再仅规范化 SpreadsheetML 默认命名空间。值和公式未变化。

| 检查 | 实际命令与结果 |
| --- | --- |
| 值/公式核对 | 三表与恢复候选包逐格值/公式相同；`Hero` 也保持相同。 |
| 命名空间 | `python Tools/ProjectMaintenance/normalize_xlsx_namespaces.py <四表>`：0 workbook(s) need repair。 |
| XLSX Diff | `node Godot/GodotProject/.godot/validation/tangseng_20261103/config/check_diff.mjs`：退出0，值/公式两模式均通过，Hero 951、`__enums__` 3、Resources 5、Combat 16 格。 |
| 生成与构建 | `cmd /c Configs\\GameConfig\\gen_code_bin_to_project_lazyload.bat`：退出0、`bye~`；`dotnet build Godot/GodotProject --no-restore -v quiet`：0警告、0错误。 |
| 包结构 | `__enums__` 使用 `inlineStr`；Resources/Combat 使用原生 `str`/`inlineStr` 混合存储，不再全量使用 `t="s"`。 |

VS Code 旧对比页可能保留此前空模型，需关闭该页后重新打开文件；本轮没有改变配置内容或设计。

## 更小的怪物列表图鉴与漩涡三态（2026-11-02）

依据第00/10/40/50章，继续使用地图拥有的单一浮层和只读Query；怪物直接由原生ItemList排布，不增加逐怪物脚本。最终208px宽、177～253px高，怪物28px图＋名称两列、守关对手浅金排首位，装备32px透明图；相对上一版面积减少约33%～56%。同名牛魔王形态去重，蝙蝠/鲨鱼召唤物补入名单，掉落规则不变。外置`brown_guide_panel.tres`沿用原棕金样式，仅收窄内边距。地图场景和三态素材保持用户暂存版本。

证据目录为`Godot/GodotProject/.godot/validation/guide_compact_20261102/`。

| 检查 | 命令与结果 |
| --- | --- |
| 官方漩涡 | 直接读取已核验OtherMat_v9源XML及元件37_109.frame1：wrapper478第1帧只有alpha=0的shape453并stop，第2帧474、第3帧477；直接提取JPEG388对比确认底图亦无漩涡。用户明确保留原版透明常态，未添加常驻图片。原图对比为`background_compare.png`，依据补入OfficialReference。 |
| 原生作者/构建 | `Godot4CSharp_console.exe --headless --editor --path Godot/GodotProject --script res://EditorScripts/author_level_guide.gd`输出PASS并保留UID。`dotnet build Godot/GodotProject --no-restore -v quiet`为0错误、5条既有警告；`--headless --path Godot/GodotProject --build-solutions --quit --no-window`扫描/构建退出0。新样式初次未登记UID而产生路径回退，正式扫描后最终运行已无此警告。 |
| 十关图鉴与名称 | `python <证据>/run_guide.py`退出0。十卡片尺寸、两列怪物、名称身份和实际文字宽度、21份图像裁切、透明装备图标通过；截图`cards/1..10.png`及`cards_montage.png`逐张查看。BOSS在前，牛魔谷只一项牛魔王；全部38件装备详情及六品质仍通过。 |
| 原生鼠标与漩涡 | 同一专项通过十地点悬停→移入装备→嵌套详情→离开关闭、940×590和1175×738窗口边界、真实点击花果山及HUD就绪清理。另实测漩涡普通图透明、最新关48帧/24fps实际运动、悬停图、离开恢复及通关后透明，注册位置和画布大小保持；`frontend/guide_whirlpool_{normal,current,hover}.png`已查看。 |
| 查询与引用 | `dotnet test Tests/ProfileTests --no-restore -v quiet`为232/232，覆盖名单包含实际配方、顺序/去重及召唤物；仅有既有NuGet漏洞源不可达警告。`python Tools/ProjectMaintenance/validate_game_references.py`为1885路径/379UID通过，`git diff --check`通过。 |

验证不接触正式存档和Git索引。最终图鉴运行退出时仍有5个ObjectDB/1资源存活诊断；原生编辑器保留既有ComponentInsoector释放诊断，未修改只读插件。未复跑无改动的整套战斗和背包流程；共用装备详情已在本次图鉴专项逐件验证。

## 龙王血条、施法规则与攻击范围（2026-11-01）

依据第10/30/40/50章：专属规则留在龙王状态，范围归原生动画，测试侧观测不扩充生产API。源为[Monster3_v3.swf](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/Monster3_v3.swf)，SHA256与OfficialReference一致。以下`<证据>`为`Godot/GodotProject/.godot/validation/dragon_review_20261101`，`<源>`为`.godot/validation/remaining_levels`；Godot脚本及参数路径使用`res://`。正式存档、用户Git索引和只读依赖未改。

| 检查 | 命令与实际结果 |
| --- | --- |
| 源范围作者化 | `python Tools/LegacyMigration/prepare_enemy_attack_geometry.py --dragon-only --output <证据>/attack_geometry.json`→`repair_dragon_hitbox_nodes.gd`→编辑器`repair_new_enemy_attack_geometry.gd -- <报告>`。七根源冰柱独立，暴雪保持大范围，水波/水柱排除透明和ADD零光、保留遮罩。重复禁用占位资源合并，身体库517个矩形收敛到14个，波12个、柱40个；不改贴图、声音、攻击编号和时长。 |
| 原生几何 | `Godot4CSharp_console.exe --headless --path Godot/GodotProject --script res://EditorScripts/validate_new_enemy_attack_geometry.gd -- <报告>`：840帧/区域样本、1224双向包含/排除查询、48柱间空隙查询及收招复位通过。`validate_dragon_resources.gd -- <报告>`另通过300源帧与房间/入口/出口/音乐绑定；旧clipped_geometry单并集报告不再作为此工具输入。 |
| 源时序不变 | `validate_dragon_art.gd -- <源>/dragon_art/actors/manifest.json <源>/dragon_art/source_rules.json`：315身体帧、complete标记、六次新攻击编号、三段无敌、效果创建与冰块绑定通过，退出0。 |
| 真实四招 | `python <证据>/run_ui.py dragon-only`：32组（普通/高级×60/120Hz×双向×四招）、五次受击后的英雄连击保护、无双状态免疫、暂停和活动水波取消全部通过，退出0。初次运行在第24组截图等待超时；测试截图改为ForceDraw当前姿势后完整重跑通过，未改变游戏逻辑来避过失败。 |
| 实际规则与空隙 | `python <证据>/run_ui.py dragon-rules-only`：20/21级×60/120Hz×双向8组通过。真实AI覆盖99.9/100/100.1/450/450.1px、无高度门、单/双技能就绪、冷却、50%选招及冷却中1/1.5秒普攻欲望；四招以±96水平速度起手不滑行，技能保持原朝向。真实冻结约1.983～2.000/4.983～5.000秒、暂停不扣时。普通档四组另将完整悟空受击盒放入柱间，停留不扣血，再移入柱内真实扣血。 |
| 共享名称 | OpenGL `validate_encounter_name_alignment.gd`：11个名称渲染通过，包括蝙蝠洞石像；名称固定右端、长名向左扩展、覆盖条左cap，整体控件顶部居中。已查看龙王/牛魔王/五字名称、冰环/暴雪/水波/水柱叠加图与实际战斗截图。 |
| 隐藏关与共享效果 | `python <证据>/run_ui.py dragon-flow-only`：真实S+K下水、暂停淡出、GGF转场、收益保留、旗幡拾取、死亡后开门、W出口、磁盘检查点与返回地图通过；`effect-shapes-only`：60/120Hz第二形状单独命中、共用去重/预算、暂停、取消、复用通过，均退出0。 |
| 构建/规则/引用 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有弃用警告；`dotnet test Tests/{ProfileTests,BattleTests,LevelTests} --no-restore -v quiet`：228/228、178/178、19/19；`python -m unittest discover -s Tests/ToolTests`：23/23。`validate_game_references.py`为1852路径/376UID通过，`git diff --check`通过。`Godot4CSharp_console.exe --headless --path Godot/GodotProject --build-solutions --quit --no-window`完成扫描/构建、退出0；保留下述既有插件释放诊断。 |

范围是对官方像素命中的基础形状近似，矩形内部仍有少量留白，不称逐像素等价。`run_ui.py`隔离APPDATA；通用末尾汇总文案不代表运行了完整UI生命周期。dragon-only和rules退出各报告5个ObjectDB；flow为11个ObjectDB/3资源，effect-shapes为5个ObjectDB/1资源。编辑器作者化仍有ComponentInsoector的RefCounted.Free及RID诊断，原始日志`geometry_author_compact.log`保留；未扩大修改框架/插件。

## 牛魔谷与火魔斩六项核对（2026-11-01）

依据第30/40/50章：具体首领规则与生命周期归本角色，帧序、范围与启闭归原生动画；只用少量基础形状，不新增通用表开关、像素多边形或生产观测API。原文件为OtherMat_v9及Monster2_v4（实际SHA256 `42a301a5ae4222aec54dfdc4c14efced48b9ad4f979902471011ff226d8cd09b`）。原始XML/脚本、PNG及新的失败/通过证据位于`Godot/GodotProject/.godot/validation/bull_review_20261101/`及既有remaining_levels目录。Framework、addons、Luban和正式存档未改；之前补齐的第1格及法宝修复保留。

六项结果：牛魔谷恢复1:1最新提示和官方原点；火魔池化复用700px误伤已复现并修复；人形438个正式渲染帧与官方源图相同，普通第二挥的黑斧风与hit2变牛冲锋保留；原版hit4确为变形震地，判定改为六阶段本体/地刺范围；心脏秒数保留官方规则，修复Stop恢复旧hide起点造成的错误提前显现；火柱改用喷火帧的240～286px高度，并按第36帧先命中后回收。

以下`<证据>`为上述bull_review_20261101目录；`<源>`为`.godot/validation/remaining_levels`，路径传给Godot时使用`res://`。

| 检查 | 命令与实际结果 |
| --- | --- |
| 地图实际画面和输入 | `Godot4CSharp_console.exe --path Godot/GodotProject --rendering-method gl_compatibility --audio-driver Dummy --script res://EditorScripts/validate_bull_map_layout.gd`：退出0，19帧原比例、原点、真实鼠标进入/扩展图内停留/点击/离开通过。已查看`map/current_09.png`与`hover_05.png`，无巨大牛角覆盖地图。 |
| 火魔修复前后 | `python <证据>/run_ui.py fire-overlap-only`：修复前`fire-overlap-before.log`明确失败“60Hz池化九连误伤了700px以外的旧目标”；修复后60/120Hz均通过旧缓存拒绝与本地真实伤害。未降低寻敌130px或禁止近邻范围伤害。 |
| 火魔真实连招 | `python <证据>/run_ui.py fire-knockback-only`：退出0，猴子/大猩猩×左右×60/120Hz共8次真实输入，九连、下冲、落地均实际命中，击退朝向正确。`effect-shapes-only`同样退出0，两频率的第二形状单独命中、共用去重/预算、暂停、取消和复用通过。 |
| 正式贴图与源帧 | `audit_registered_actor_packing.py --manifest <源>/bull_art/actors/manifest.json --output <证据>/source_audit`：644源帧像素/注册点通过。OpenGL `validate_registered_actor_atlas.gd -- <同manifest> bull_demon_king res://TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_king_official_frames.tres <证据>`：438正式渲染帧通过，含两段挥斧、冲撞与震地；三份源动作联系图已查看，未发现缺帧、错绑或坐标移位。 |
| hit4六阶段范围 | `prepare_enemy_attack_geometry.py --bull-only --output <证据>/attack_geometry.json` → 编辑器`repair_new_enemy_attack_geometry.gd` → `validate_new_enemy_attack_geometry.gd`：54帧、140项双向包含/排除及旋转复位通过。键位在20/30/33/37/41/53源帧，初段约153×166，地刺完全扩散约451px；叠加图`bull_demon_king_attack_4_review.jpg`已查看。这里是基础矩形近似，包含内部空隙。 |
| 心脏修复前后 | `python <证据>/run_ui.py bull-heart-timing-only`：修复前复用时预期隐藏5秒，但0.008秒已alpha=1，证据`bull-heart-timing-before.log`。修复后60/120Hz均通过：首次隐藏5～9秒、两秒淡入、完整显现2～5秒、两秒淡出、下一隐藏5～11秒；首个完全显现物理步可受伤，半透明时拒绝伤害，受伤不重启周期。 |
| 火柱高度与真实伤害 | `repair_bull_fire_geometry.gd -- <证据>/fire_bounds.json` → `validate_bull_fire_geometry.gd`：36源帧、预警无伤害、上部相交/上界外排除均通过。上述`bull-heart-timing-only`另实测两频率下-350px处不受伤、可见火柱-220px处真实扣血；六阶段叠加图`fire_geometry_review.jpg`已查看。阶段形状不可变，多根火柱不共享可变尺寸。 |
| 全资源与实机首领 | `validate_bull_resources.gd -- <源>/bull_art/actors/manifest.json <源>/bull_art/monsters/manifest.json <源>/all_levels.json <证据>/attack_geometry.json`：1324源帧双向样本通过。最终`python <证据>/run_ui.py bull-only`退出0：兽形/护卫、60/120Hz双向五招、五次吞噬与释放、心脏暂停、手/火池清理、金钢琢首次/重复/满包及复用全部通过。 |
| 构建、纯规则及引用 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有弃用警告。`dotnet test Tests/{ProfileTests,BattleTests,LevelTests} --no-restore -v quiet`分别228/228、178/178、19/19；仅NuGet审计服务不可达警告。`python Tools/ProjectMaintenance/validate_game_references.py`为1853路径/375UID通过；`git diff --check`通过。 |

`run_ui.py`每个模式使用隔离APPDATA和`--smoketest=ui --<模式>`，测试范围以上表为准，不将通用末尾汇总文案当作完整UI生命周期验收。最终bull-only退出仍有10个ObjectDB和1个资源存活；心脏专项9个ObjectDB/2资源，火魔/多形状专项也有存活资源诊断。原生编辑器作者化退出另复现ComponentInsoector的`RefCounted.Free`/不存在free及RID泄漏，原始输出保存在`geometry_author.log`；没有修改只读插件或用业务PASS声称这些诊断已解决。

## 全装备与法宝穿戴（2026-10-31）

第1格已备份后更新为 v12：保留原有进度，装备集合补齐当前配置 1～38（40 个实例，原有重复实例保留），金钢琢写入悟空独立法宝槽。第2/3格补齐 `TreasureInstanceId` 空字段并升到 v12。

| 检查 | 命令与结果 |
| --- | --- |
| 正式构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误，5条既有 Framework/生成代码弃用警告。 |
| 纯规则测试 | Profile `222/222`、Battle `178/178`、Level `19/19` 通过；法宝测试覆盖穿戴、属性变化、保存、重建与卸下。 |
| 存档读回 | 正式 `SaveDataUtility.Validate` 验证第1/2/3格全部通过；第1格 `equipment=40`、金钢琢 `TreasureInstanceId` 非空。备份目录为 `GameData.backup-before-treasure-slot-20261031_214739`。 |
| UI 烟测 | `--smoketest=ui --equipment-only`：`SMOKE-TREASURE GUI PASS`，跨页选择、独立法宝槽、tooltip、穿戴、属性替换和法宝替换通过；退出业务码0。 |

## 框架关停生命周期（2026-10-31）

根因是 `BaseComponent` 在 `OnPreDestroy` 才调用框架 Shutdown，此时 Godot 已先释放 `HttpRequest`、声音辅助器和部分 UI 节点；任务池随后访问已释放对象，分别产生 WebRequest `ObjectDisposedException`、声音代理异常和窗口模型空引用。修复将既有 `GameEntry.Shutdown(ShutdownType.None)` 移到 `BaseComponent.OnExitTree`，并让标题、选角、地图、关卡流程在关停时先关闭自己拥有的窗口；普通导航继续使用原淡出，关停立即停止音乐。背包确认框只在仍在场景树内时释放焦点。框架授权登记在第50章。

- `validate_framework_shutdown.py`：本地回环 GET/POST、超时后复用、4条在途+2条排队请求、显式重复关闭、同进程重建，`FRAMEWORK SHUTDOWN PASS`；无 `ObjectDisposedException`、无对象/资源泄漏诊断。
- `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet`：0错误、5个既有生成代码弃用警告。
- `--smoketest=frontend`：退出0，开始/存档/选角/地图流程通过；存档损坏、旧作用域和预期失败注入仍按测试设计输出错误日志。
- `--shutdown-window=inventory|skills|settings`：三种真实暂停窗口均能在退出前完成退订；背包焦点错误不再出现。
- `dotnet test`：Battle 178/178，Profile 211/211；C# 风格346文件通过，引用1852路径/374 UID通过，`git diff --check`通过。

剩余限制：Godot 原生 `AudioStreamPlayer` 在独立、无 GGF 的 MP3 播放脚本中也会报告退出时 `AudioStreamPlaybackMP3`/资源仍在使用，属于引擎退出行为；本轮没有用业务层强行释放或修改只读插件。编辑器 `ComponentInsoector` 的 `Free()` 错误是另一个插件生命周期问题，不属于本次运行时框架修复，仍按既有插件记录保留。

## 全敌人完整性与官方行为（2026-10-30）

本轮使用v25928官方主程序及Monster1/2/3缓存，散列见OfficialReference。只修改项目自有代码、源表及经既有作者工具保存的资源；玩家正式存档和Git索引保留。规范依据为第10/20/30/40/50章；第30章的旧首掷延迟/通常1～2形状与用户本轮官方行为、四冰柱要求有差异，本轮以用户要求为准，规范未改。

在仓库根执行，Godot的脚本参数是资源路径，末尾`--`后为用户参数：

```powershell
$project = 'P:/Godot-Project/GameZMXY/Godot/GodotProject'
$work = "$project/.godot/validation/enemy_fidelity_20261030"
$runner = "$project/.godot/validation/movement_presets/run_godot.py"
$env:AI_MODE = '1'
cmd /c Configs\GameConfig\gen_code_bin_to_project_lazyload.bat
dotnet build Godot/GodotProject/GodotProject.csproj --no-restore
dotnet test Tests/BattleTests/BattleTests.csproj --no-restore
dotnet test Tests/ProfileTests/ProfileTests.csproj --no-restore
dotnet test Tests/LevelTests/LevelTests.csproj --no-restore
python Tools/ProjectMaintenance/audit_config_schema.py
python Tools/LegacyMigration/audit_official_enemy_fidelity.py --output "$work/source_audit_final.json"
python $runner enemy_final_import --headless --editor --import --build-solutions --quit
python $runner enemy_final_geometry --headless --script res://EditorScripts/validate_enemy_fidelity.gd -- "$work/ice_regions.json"
python $runner enemy_boundaries --headless --script res://EditorScripts/validate_enemy_boundaries_and_knife.gd -- "$work/knife_source_frames.json"
python $runner enemy_final_visual --rendering-method gl_compatibility --audio-driver Dummy --script res://EditorScripts/render_enemy_grounding.gd -- "$work/native_final"
```

Luban成功只覆写`enemy_tbenemyconfig.bytes`；构建0错误，5条既有依赖弃用警告。Battle232、Profile211、Level19项均通过，配置17张映射表0错误。源码报告24种、1066格、71条攻击；可映射的数值无差异，扩展/旧预设列有明确分类，不把它们计为官方已证实值。Enemy.xlsx共39格变化（11丢失时限、飞刀间隔、灯笼鱼视野、24受伤后延迟、2基础魔抗），原ZIP其余部分/样式保留，已安装XLSX Diff 1.6.4 worker在数值和公式模式均读到39格。

原生检查：13个小怪装配完整；四冰柱360个形状样本、102个真实shape间隙排除，左右与idle复位通过。11关86处玩家门允许敌人穿过、22处外墙仍阻挡，飞刀27源帧几何/位移与官方逐帧一致。24种敌人429个原生动作截图含死亡全部生成并人工查看；最终形态牛魔王另外查看`bull_heart_visible.png`的完整实机背景、心脏、双手。最后完整导入后的几何和渲染stderr为空，原先SpriteFrames缓存UID/旧纹理尺寸问题已消除。

真实回归入口如下，均使用隔离APPDATA和Validation存档。可用上面的runner替换Godot启动命令；`--enemy-hierarchy-only`、`--bat-cave-only`、`--dragon-only`需要普通OpenGL窗口观察画面，其余可headless。每次指定唯一case目录，不与玩家档混用。

```powershell
python $runner <case> --headless --audio-driver Dummy --max-fps 60 -- --smoketest=ui <专项参数>
python $runner enemy_ai_official_ready --headless --audio-driver Dummy --max-fps 60 -- --smoketest=ai
python $runner enemy_bosses_final --headless --audio-driver Dummy --max-fps 60 -- --smoketest=bosses
```

| 专项参数 / 实际case | 验证内容与结果 |
| --- | --- |
| `--enemy-fidelity-only` / enemy_fidelity_ready | 24种原点索敌两侧严格横界/含纵界、视野外保留目标；6种近战60/120Hz自主追击/节拍，飞刀实测1→5秒投刀；PASS、退出0 |
| `--enemy-knockback-only` / enemy_knockback_live | 首中末战斗门、两朝向、60/120Hz实际ReceiveHit/身体推进穿越；PASS、退出0 |
| `--effect-shapes-only` / enemy_effect_shapes | 仅第二形状命中、共享去重/预算、暂停、取消、复用、自然回收；PASS、退出0 |
| `--monster-health-only` / enemy_health_final | 全13小怪实际生命上限（含大猩猩守关900）、两秒淡出、治疗、暂停、连续受伤、头顶位置、死亡、隐藏及池复用；PASS、退出0 |
| `--sea-ai-only` / enemy_sea_ai_clock | 虾兵追退/40%出招，蟹150px/十秒重击，龟400px/60%出招，高处/暂停，60/120Hz；PASS、退出0 |
| `--east-sea-ai-only` / enemy_east_ai_clock | 鲛魔王远近分支与灯笼鱼自主行为，两频率；PASS、退出0 |
| `--shark-ai-only` / enemy_shark_ai_clock | 召唤鲨鱼自主追踪、折返节流、速度上限与近身选招，两频率；PASS、退出0 |
| `--garuda-ai-only` / enemy_garuda_ai_ready | 自主出招、共享冷却、起飞/落下、退避及受击连续性，两频率；PASS、退出0 |
| `--qixing-only` / enemy_knife_regression_fix | 六招双向、刀/云实际命中、毒、旋转无敌、暂停与复用；PASS、退出0 |
| `--shark-only` / enemy_shark_shapes | 两档生命、两频率、双向，水牢预警、独立效果、召唤死亡/隐藏取消及复用；PASS、退出0 |
| `--garuda-only` / enemy_garuda_shapes | 两频率双向五招、淡出时持续命中、蛋/复生/死亡与复用；业务PASS、退出0；有两条资源加载GCHandle异常，单列在下方 |
| `--bull-only` / enemy_bull_shapes | 冲撞/休息、牛妖双向、两频率五招、五次吞噬、心脏/双手/火柱、直接奖励、满包与复用；PASS、退出0；实机截图已查看 |
| `--dragon-only` / enemy_dragon_shapes_fix | 两等级、两频率、双向四招、水波/水柱位置、七段冰环及英雄第五次后保护、冰冻/无双免疫、暂停和弹体取消；PASS、退出0 |
| `--smoketest=ai` / enemy_ai_official_ready | 猴子受击激怒、追击、出招锁向、恢复、高处不可达、重新落地、视野外保留、受击/死亡/回池；PASS、退出0 |
| `--smoketest=bosses` / enemy_bosses_final | 前两位首领五招实际命中、无敌、风刃独立时钟、隐藏和复用；PASS、退出0 |

补充最终整合：`enemy_bat_complete`（蝙蝠追踪/空中攻击/受击/收招/死亡、石像与落刺）、`enemy_sea_complete`（浅岸三物种四招双向两频率、弹体击散/淡出/暂停/复用）、`enemy_east_complete`（东海四招双向两频率、背防、死亡/复用，冰柱真实四次命中）均PASS、退出0。层级/声音回归及妖猴逐绘制帧检查在`enemy_hierarchy_verified`全部PASS、退出0。此前声音夹具将池实例的物理推进冻结，现测试侧恢复，左右自主追击与高处目标均无一帧举棒；没有给生产代码添加测试兜底。

复现作者化：

```powershell
python Tools/LegacyMigration/prepare_grounded_enemy_art.py --manifest "$project/.godot/validation/remaining_levels/sea_art/actors_registered/manifest.json" --extra-manifest "$project/.godot/validation/action_origin_review/ground_monsters_export/manifest.json" --output "$work/grounded"
python $runner enemy_ground_author --headless --editor --script res://EditorScripts/apply_grounded_enemy_art.gd -- "$work/grounded/manifest.json"
python Tools/LegacyMigration/prepare_enemy_fidelity_geometry.py --xml "$project/.godot/validation/official_continuation/monster2_full.xml" --manifest "$project/.godot/validation/remaining_levels/sea_art/actors_registered/manifest.json" --output "$work/ice_regions.json"
python $runner enemy_shapes_author --headless --editor --script res://EditorScripts/repair_enemy_fidelity.gd -- "$work/ice_regions.json"
python $runner enemy_bars_author --headless --editor --script res://EditorScripts/repair_monster_health_bars.gd
python $runner enemy_detection_author --headless --editor --script res://EditorScripts/apply_enemy_detection_ranges.gd -- "$work/source_audit_final.json"
# 之后必须完整import，再执行原生几何、渲染与真实命中回归。
```

初次回归的失败与修正：海怪右向第二招因冻结英雄身体连带冻结五连击保护，夹具改为每次独立施放前清零保护；正式受击规则不改。龙王冰环测试此前错误期待冻结英雄连吃7次，现同时检查6次原生刷新与5次真实扣血，最后两段被官方保护免疫。血条夹具最初忽略大猩猩守关900；索敌夹具暴露旧21px偏移，实际场景修正；多项旧测试的Move/SkyMonsterMove旧名和旧站位已改用当前Walk/Fly及明确落地条件。飞刀1→6秒投刀失败促成冷却请求起点与源步末扣减修正，最终1→5秒通过。

依赖诊断独立记录：编辑器退出仍有ComponentInsoector的RefCounted.Free/Invalid call和RID清理；运行退出仍有WebRequest已释放HttpRequest的ObjectDisposed及少量ObjectDB/resource泄漏。enemy_garuda_shapes在GGF的LoadThreadedGet链路出现两次`SwapGCHandleForType: Handle is not initialized`，但各招断言及退出0；这是仍存在的引擎/依赖资源桥接异常，不能称运行日志全净，本轮未修改addons/Framework。专项业务结果与这些诊断分别报告。

## 剩余造一关卡统一交付（2026-10-28）

依据第00/20/30/40/50/60章，按官方v25928资料完成六关当前指纹验收。全部测试使用隔离APPDATA和存档；正式玩家档、Git索引、框架/插件/旧工程源码与Luban本体未改。日志相对`.godot/validation/remaining_levels/current_final/`。

| 命令 / 检查 | 当前结果 |
| --- | --- |
| `python .../current_suite.py --resume`；`record_current_suite.py` | `final_20261028.log`、`imagery_20261028.log`及`commands_levels.json`记录79项当前成功命令；六关每道门引用实际命令、退出码、日志SHA和工程指纹，不刷新历史收据冒充重跑 |
| 官方依赖清单；`audit_source_recipes.py` | 11个官方标签、24种动态敌人；82条出生流身份/数量/首只等待/后续间隔一致。独立核对发现浅岸16条首只等待少2秒，已按`delay+interval`修正LevelConfig的S111:S126，后续间隔不变 |
| Artifact作者化、原ZIP局部转存、XLSX Diff、Luban | `shore_timing/`保留前后工作簿/预览；普通与公式模式均只有16格差异，其他ZIP部件保持。既有入口重建关卡bytes；`build_after_generation.log`0错误 |
| `validate_wukong_wardrobe_motion.gd`；`validate_official_wukong_attacks.gd`；`--equipment-only` | 38件外观、15件悟空装备、2158源格、23904原生播放样本、5208普攻样本及真实跨页穿卸通过。像素审计改为无图形阶段，全部组合播放/截图另在OpenGL执行；覆盖不减 |
| 原生图像与六关全景 | 32组敌人/效果检查通过：31组原生动画绘制对照及虾兵19×13单帧弹体。前景/地形全幅像素对照通过；动态天空、陷阱、出口由场景和实机检查。两张ADD预览首次因目录缺失未写入，创建隔离输出目录后重跑对应两项通过，PNG已查看 |
| 2-3、3-1、3-2、3-3、4-1、9-1场景与实机专项 | 全部BOSS/AI/判定/掉落/暂停/结算/失败重试/地图返回/磁盘重读通过；五主线完整通关。隐藏龙宫覆盖真实下水、1秒淡出、两档龙王四招、首次旗幡、死亡后出口与主线进度隔离 |
| `--chapter-music-only`及各关Music断言 | Cave/Sea/Bull三首章节曲的GGF代理、循环、播放推进、暂停策略和停止通过。官方无专属招式音的敌人不虚构事件，保留真实英雄命中反馈。测试使用Dummy音频驱动，不声明做过扬声器听感验收 |
| Profile/Battle/Level；ToolTests | 208/223/19/20项通过，合计470。工具新增拒绝漏算首次interval、错误怪物/流集合、旧清单缺逐流证据；关卡就绪审计持久集成同一校验 |
| `python Tools/LegacyMigration/audit_remaining_level_readiness.py --inventory Godot/GodotProject/.godot/validation/remaining_levels/dependency_inventory.json --work Godot/GodotProject/.godot/validation/remaining_levels --output Godot/GodotProject/.godot/validation/remaining_levels/readiness_final_20261028.json --strict` | 退出0，**6/6 complete、每关0 open gates**；源SHA和82条配方均无错误 |
| `git diff --check`；`git diff --cached --check` | 通过，Git索引未改 |

当前工程指纹为`4eaccece3f58372dbfa6aab022204b9f8fe3f67c572dc1d640acb781b2045ef5`；官方清单SHA为`f9b841f512fb0a551cc4c484fd0d85389bd4b810118484025c2958732d48048e`。首次GPU纹理读回发生内存分配崩溃的日志、配方对比首次失败日志均保留。最终游戏进程仍有既有WebRequest关停ObjectDisposedException、ObjectDB/RID/资源存活诊断，退出码和玩法断言通过；没有修改只读依赖来消除这些诊断。法宝按用户已定范围仅获取/背包/存档，主动玩法未开放；唐僧等未开放角色不属于本次关卡制作。

## 常用命令

```powershell
# 改源表后先导出；AI_MODE=1 跳过脚本末尾 pause
$env:AI_MODE = '1'
cmd /c Configs\GameConfig\gen_code_bin_to_project_lazyload.bat
dotnet build Godot/GodotProject --no-restore -v quiet
dotnet test Tests/BattleTests --no-restore -v quiet
dotnet test Tests/ProfileTests --no-restore -v quiet
dotnet test Tests/LevelTests --no-restore -v quiet
python Tools/ProjectMaintenance/validate_game_references.py

# 真实 UI、物理、流程；写入隔离 Validation 存档
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility -- --smoketest=ui
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility -- --smoketest=level
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility -- --smoketest=frontend
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility -- --smoketest
# F6 独立测试场地，不启动正式流程或读写玩家档案
& 'S:/Godot4/Godot4CSharp_console.exe' --headless --path Godot/GodotProject --scene res://TheGame/Scenes/test_arena.tscn --quit-after 360
```

按修改范围选用 `--smoketest=level-cancel`、`ai`；规则单测无需打开编辑器。场景装配用 `--headless --script res://EditorScripts/validate_level_scene.gd -- --resize --fractional`，编辑器 Tool 再加 `--editor`；触发区用 `validate_level_triggers.gd`，动画库用 `validate_anim_libraries.gd`。有渲染器时，`validate_musou_flash.gd` 检查原版无双 19 帧透明动画；`validate_skill_layout.gd` 检查技能面板坐标、45px 图标、造三五入口及无双黑底板的空/半满/满值装配，`validate_level_up.gd` 检查升级蓝线裁剪、画面和注册尺寸。

XLSX Diff 1.6.4 的解析器无法读取带 `x:` 前缀的 SpreadsheetML 标签，会错误地显示零张表。导表工具仍能读取这类文件。遇到该问题时，用 `python Tools/ProjectMaintenance/normalize_xlsx_namespaces.py --write <文件.xlsx>` 将标签改为等价的默认命名空间；脚本逐项验证 XML 内容并原子替换，不重新生成单元格或样式。不带 `--write` 时只检查。已暂存的文件需要重新暂存修复后的版本，工作区修复不改 Git 索引；历史版本若含相同前缀，插件仍无法读取历史侧。

引擎截图写入忽略的 `.godot/validation/`；无双开场及后半段分别为 `musou_activation.png`、`musou_activation_tail.png`，升级为 `level_up.png`、`level_up_tail.png`，原帧/裁剪帧对照为 `level_up_frames.png`，BOSS 条为 `encounter/featured_health_bar.png`。通过 PASS/FAIL、退出码、错误与画面共同判断结果。

战斗背包为 `inventory_form.png`，品质上拉菜单为 `inventory_quality_sale.png` / `inventory_quality_sale_scaled.png`，出售确认框为 `inventory_sale_confirmation.png` / `inventory_sale_confirmation_scaled.png`，多属性详情为 `inventory_tooltip_attributes.png`，失败结算为 `settlement_failure.png`，成功结算为 `encounter/settlement_success.png`。成功页截图在真实检查点完成后立即采集，可能带有短暂的保存成功提示；提示按真实时间淡出，背包和结算暂停不会将它冻结。

装备背包与实际详情为 `inventory_equipment.png`，操作菜单为 `inventory_actions.png`，武器/防具同时穿戴为 `inventory_wearing.png`，绿色/蓝色恢复数字为 `recovery_numbers.png`；无名称的地面恢复品和装备为 `ground_pickups.png`；地图默认边缘闪烁的两端画面为 `frontend/map_current_low.png`、`frontend/map_current.png`。

## 悟空造一资源复现与逐帧检查

官方源包地址与散列见 [OfficialReference](OfficialReference.md)，需先准备 Java、FFDec 26.3 和原始 Role_v7.swf；工具自行恢复包头并核对 SHA。以下更新悟空基础美术与五段攻击，不重生成其他角色或基础动作时序。重跑基础导出后，继续执行下节技能导出，恢复完整的共享图集与技能特效序列。

```powershell
$workPath = Join-Path $env:TEMP 'gamezmxy-wukong-export'
python Tools/LegacyMigration/export_official_wukong.py `
  --java 'S:/Unity Editor/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe' `
  --ffdec (Join-Path $env:TEMP 'gamezmxy-ffdec/ffdec.jar') `
  --godot 'S:/Godot4/Godot4CSharp_console.exe' `
  --source (Join-Path $env:TEMP 'gamezmxy-official-role.swf') --work $workPath `
  --output Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong `
  --effects-output Godot/GodotProject/TheGame/Sprites/Effects/wukong/wukong_attack_effects.png
python Tools/LegacyMigration/import_official_wukong_attacks.py (Join-Path $workPath 'manifest.json')

New-Item -ItemType Directory -Force Godot/GodotProject/.godot/validation/appdata | Out-Null
$env:APPDATA = (Resolve-Path Godot/GodotProject/.godot/validation/appdata).Path
& 'S:/Godot4/Godot4CSharp_console.exe' --headless --editor --path Godot/GodotProject --script res://EditorScripts/regenerate_collection.gd
dotnet build Godot/GodotProject --no-restore -v quiet
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility --script res://EditorScripts/validate_official_wukong_attacks.gd -- (Join-Path $workPath 'manifest.json')
```

逐帧检查覆盖全部 62 个源攻击帧 × 四种穿戴 × 两个朝向，共 496 次实际绘制；核对 24 fps 秒制关键帧、身体/武器帧号、固定金箍棒棒影已移除、原版弧光保留、朝向、根坐标及矩形覆盖。独立读取武器与剑气像素验证范围、3px 余量和 14px 最小厚度；检查判定窗口未早于可见剑气结束。真实 Area2D 验证近端命中、武器之外的剑气及残留命中、矩形外和关闭窗口不命中。带矩形轮廓的图片在 `.godot/validation/official_attacks/attacks_armor*_weapon*_facing*.png`；轮廓仅由验证脚本绘制。真实输入用 `--smoketest` 检查五段连击、独立输入冷却、无后续输入时按动画长度收招、空中第三段下劈和近距离命中；`--smoketest=ui` 检查全部 13 个玩法动作、四种穿戴及背包。

## 悟空技能资源复现

Python 环境需要 Pillow、NumPy；`--source` 使用同版 Role_v7，音频使用 Music.swf。后三个步骤依次消费前一步 manifest，所有解包文件留在忽略目录。

```powershell
$skillWork = Join-Path (Resolve-Path Godot/GodotProject/.godot/validation).Path 'wukong-skills-export'
$javaPath = 'S:/Unity Editor/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe'
$ffdecPath = Join-Path $env:TEMP 'gamezmxy-ffdec/ffdec.jar'
python Tools/LegacyMigration/export_official_wukong_skills.py `
  --java $javaPath --ffdec $ffdecPath `
  --source (Join-Path $env:TEMP 'gamezmxy-official-role.swf') --work $skillWork
& 'S:/Godot4/Godot4CSharp_console.exe' --headless --path Godot/GodotProject `
  --script res://EditorScripts/import_official_wukong_skills.gd -- (Join-Path $skillWork 'manifest.json')
python Tools/LegacyMigration/import_official_wukong_skills.py (Join-Path $skillWork 'manifest.json')
python Tools/LegacyMigration/export_official_skill_audio.py `
  --java $javaPath --ffdec $ffdecPath --source (Join-Path $env:TEMP 'gamezmxy-official-music.swf') `
  --work (Join-Path $skillWork 'audio') --output Godot/GodotProject/TheGame/Audios/SFX/wukong/skills
& 'S:/Godot4/Godot4CSharp_console.exe' --headless --editor --path Godot/GodotProject `
  --script res://EditorScripts/regenerate_collection.gd
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility `
  --script res://EditorScripts/validate_official_wukong_skills.gd -- (Join-Path $skillWork 'manifest.json')
```

当前结果：122 个源动作帧 × 四种穿戴 × 两朝向，共 976 次检查 PASS、退出 0；五个独立特效逐帧双朝向及真实 Area2D 近端/范围外/关闭窗口 PASS。验证直接读取导入后的武器、能量或透明探针像素，检查实际矩形覆盖；截图带青色判定轮廓，位于 `.godot/validation/official_skills/`。技能资源更新后，原五段普攻 496 次检查再通过；四张图的原有 348 格像素逐一与 HEAD 对比相同。只有根证书读取错误，未发生资源解析或断言错误。
