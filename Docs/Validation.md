# 验证

## 地图购技、tooltip 与连击闭环（2026-11-03）

| 检查 | 实际结果 |
| --- | --- |
| 纯规则测试 | `dotnet test Tests/ProfileTests/ProfileTests.csproj --no-restore --verbosity minimal`：242/242；`LevelTests`：19/19；`BattleTests`：191/191。各命令有既有 NU1900 网络源告警，无测试失败。 |
| C# 构建 | `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore --verbosity minimal`：0错误，5条既有框架/生成代码弃用警告。Godot headless build-solutions 编译阶段也报告 DONE；编辑器退出期 ComponentInspector 仍有只读插件释放诊断。 |
| 只读结构检查 | `validate_game_references.py`：1885 serialized paths / 373 UIDs；MapForm 15/15、BattleForm 6/6 场景绑定与现有生成字段一致；CSharpStyle 346 owned files、0格式差异、0缺XML；`git diff --check`通过。UI生成器复跑被自动审批按生成物只读边界拒绝，改为只读比较现有字段与NodePath，未追加生成物写入。 |
| 地图购技 OpenGL | `S:\Godot4\Godot4CSharp_console.exe --path Godot/GodotProject --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=frontend`：退出0。`SMOKE-MAP-SKILLS PASS`；地图按钮进入技能窗、主动/被动购买、余额/按钮刷新、关闭重开、无英雄/关卡模型、保存重载和进关生效均通过。截图：`Godot/GodotProject/.godot/validation/frontend/map_skill_active.png`、`map_skill_passive.png`。 |
| Tooltip OpenGL | 完整 UI 烟测的 `SMOKE-TOOLTIP PASS`、`SMOKE-ACCESSORY PASS` 通过；属性和价格均为同一主题16px、全角中文冒号、数值左贴齐；一项/三项属性、长说明换行及底部6px间距通过。截图：`Godot/GodotProject/.godot/validation/inventory_tooltip_attributes.png`、`inventory_tooltip_wrapped.png`。 |
| 连击规则与真实命中 | 前端 OpenGL smoke 的 `SMOKE-COMBO-LIFECYCLE PASS` 通过：HUD同步、暂停不推进、恢复后超时清零、新连击、离关退订/清空、重入归零。英雄真实攻击 `--smoketest` 退出0：实际受击5次、连击峰值2、HUD=True；普攻段号独立变化。截图：`Godot/GodotProject/.godot/validation/frontend/frontend_combo.png`。 |
| 完整 UI 回归 | 使用隔离 APPDATA：`--smoketest=ui --terrain-level5` 退出0，`SMOKE PASS：UI 数值、解绑复用、实体复用、加载取消、失败清理与存档重入全部通过`。无隔离用户设置时的同一回归曾在既有结算音乐音量前提失败（当前音量约68%，-3.35dB），未修改音乐逻辑；隔离运行通过。退出期仍有既有 RID/ObjectDB/资源泄漏诊断。 |

证据日志集中于 `Godot/GodotProject/.godot/validation/map_skill_combo_20261102/`；Framework、addons、Tools/Luban源码未改。工作树中本需求已有的MapForm/BattleForm绑定字段变更已通过只读结构检查和实际装配验证，本轮未追加生成物写入。

## 可读性整理续轮（2026-11-02）

本轮针对`MapForm.Logic.cs`按旧版阅读路径整理：地点声明、装配校验、事件绑定、点击/悬停具名处理分开；保留单份地点表、场景按钮`LevelId`身份来源和隐藏关入口契约。全项目同类检查覆盖346个手写C#、131份GDScript、70份Python；修改只涉及手写运行时代码、编辑器/迁移脚本和文档，不碰Framework、addons、Luban、生成物、配置或场景。

| 检查 | 实际结果 |
| --- | --- |
| C#构建 | `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet`：0错误，5条既有框架/生成代码弃用警告。 |
| 纯规则测试 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：240/240；`BattleTests`：191/191；`LevelTests`：19/19。 |
| 格式与XML | 既有CSharpStyle最终扫描346文件，0格式差异、0缺XML；唯一`LevelGuideQuery.cs`差异由工具token保持写回后清零。`git diff --check`及暂存差异检查通过。 |
| Python/GDScript | `python -m compileall -q Tools/ProjectMaintenance Tools/LegacyMigration`通过；另外逐文件`compile`检查覆盖全部70份Python。Godot加载8份修改的作者GDScript的`parse-authors`退出0，没有实例化或运行导入器。 |
| 引用/索引 | `validate_game_references.py`：1885路径/373UID；新旧Godot C# UID齐全，Git暂存条目与本轮开始快照一致，未执行add/commit。 |
| 实机回归 | frontend、level-guide、ui三个游戏进程最终均退出0，含前端、十地点图鉴、完整UI（暂停掉落、失败结算、存档重读），无`SMOKE FAIL`。ai、bull、dragon-rules、shark、shark-ai、garuda-ai、sea-ai、bat-cave八个游戏进程也均退出0。首次编排器本身因摘要编码退出1，原因与补跑范围见后段，不将编排错误冒称原批次通过。 |

可读性整理保留了有明确来源的复杂表达：作者脚本仍按源几何选择父节点，迁移脚本仍按源版本/资源身份处理；运行时代码的随机调用条件、配置校验和状态时序未改变。所有实机退出诊断与预期损坏档/HUD注入仍单独记录，不能视为零诊断。

证据目录为`Godot/GodotProject/.godot/validation/readability_20261102/`。`source-coverage-final.json`登记547份手写源码及最终散列；实际检查范围为255份运行时代码、38份调试代码、134份编辑器代码、53份测试、7份维护工具、60份迁移脚本。初筛指标只用于定位，不据行长/语法命中机械改写；复核后整理23份C#、8份GDScript、5份Python：多层选择展开、索引构造分步、局部捕获改显式参数，校验/订退按职责写清。保留了状态工厂、实时只读事实委托、简短组合查询、测试观察断言及数据/正则字串，避免用额外类型或通用包装替换合理代码。

可复现的实机命令为`python <证据>/batch.py frontend level-guide ui`，分别使用`--smoketest=frontend`、`--smoketest=frontend --level-guide-only`、`--smoketest=ui --terrain-level5`，均为OpenGL、Dummy音频和隔离APPDATA。UI为完整会话，坡面按既有参数只重复第五关8条路线；没有声称重跑全部地形。敌人命令`python <证据>/batch.py ai bull dragon-rules shark shark-ai garuda-ai sea-ai bat-cave`使用独立headless进程，覆盖巡逻、选招、手部运动、高低生命动画、自由运动和感知装配。一次性摘要最初误用控制台编码，导致编排器在输出结果时报错；已核对原生result.json/完成标记并仅补跑未启动模式，游戏失败数不因此伪记或重复验证。原始日志和每项`*.checked.json`均保留。

前端与图鉴退出各5个ObjectDB/1资源；八项敌人专项退出5～8个ObjectDB。完整UI退出20纹理RID/8排版RID/2字体RID、38 CanvasItem、141 ObjectDB和11资源；它们作为既有退出期诊断保留。最新地图图鉴截图已查看。本轮没有触碰用户修改的根AGENTS.md或暂存条目。作者工具只做语法/真实加载验证，没有重跑会写正式资源的导入和几何修复。此限制不被描述成资源生成回归已通过。

## 架构审计续轮：全部条目实施与裁决（2026-11-02）

本节接续下方首批记录。A01—A14及GLM附加P3建议的最终处理见[审计报告第10节](ArchitectureAudit-20261102.md)。依据第00/10/30/50/70章，共享既有身体状态、注入AI事实、合并重复Engage、复核身体命中形状、集中地图绑定、提取英雄私有表现及处理faulted检查点任务；数值采样和物种无敌计时经核实后保留。不改集中规范、配置/场景、Framework/addons/Luban、玩家正式档案或用户暂存内容。

证据为`Godot/GodotProject/.godot/validation/architecture_audit_20261102/`，下称`<证据>`。`run.py`每组使用新APPDATA及既有SmokeTestDriver唯一隔离目录，记录命令、stdout/stderr和result.json。`batch.py`只编排独立测试进程。以下专项以专用断言为准，通用`SMOKE PASS`尾句不能作为完整UI验收。

| 检查 | 实际命令、结果与证据 |
| --- | --- |
| 正式编译 | `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet`：`build-checkpoint-fixed.log`、`build-final-fixture.log`均0错误/5条既有只读代码弃用警告。`build-final.log`为后续未改源码的增量构建0错误/0警告，不据此称全量无警告。 |
| 三组纯规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet`为240/240；`Tests/BattleTests`为191/191；`Tests/LevelTests`为19/19，均退出0。日志`ProfileTests-final.log`、`BattleTests-final.log`、`LevelTests-final.log`。新增覆盖自定义行走返回、死亡等待/一次回收/旧回调、注入距离/实时阶段/冷却/飞行高度及退避；NuGet审计源不可达和Export字段等既有警告保留。 |
| 身体误命中修前/后 | `python <证据>/run.py body-before --headless --audio-driver Dummy --max-fps 120 --quit-after 12000 -- --smoketest=ui --body-overlap-only`修前退出1，真实新攻击仍误伤700px外旧候选。修后同参数`body-fixed`退出0，60/120Hz旧目标不伤、移回真实命中、同招去重通过；退出5个ObjectDB诊断。 |
| 敌人集中回归 | `python <证据>/batch.py checkpoint-failure bull dragon dragon-rules east-sea east-sea-ai shark shark-ai garuda garuda-ai qixing sea sea-ai bat-cave enemy-death effect-shapes fire-overlap freeze`：18项均退出0，证据`continue-<模式>/`。实际参数`--headless --audio-driver Dummy --max-fps 120 --quit-after 36000 -- --smoketest=ui --<模式>-only`，各有专用完成断言。 |
| 行走、死亡及家族特例 | bull覆盖60/120Hz双向五招、兽形/护卫、心脏/奖励/清理；dragon为两档×双频×双向四招32组及控制免疫；east-sea含双频双向四招/背击/原生死亡/复用；shark含高低生命/首领与召唤物/死亡与隐藏取消；garuda含双频双向五动作/化卵/复生/终态死亡/复用。enemy-death为六组致死抛飞、空中循环、落地回收及一次死亡收益。 |
| AI与单份垂直意图 | east-sea-ai、shark-ai、garuda-ai、sea-ai覆盖双频自主选招/冷却/阶段连续/追踪退避；bat-cave覆盖飞行追踪/出招/受击回飞/死亡。整理冗余Reset后复跑bat-cave及shark-ai仍退出0。 |
| 共用几何与英雄表现 | effect-shapes覆盖双频第二形状、去重/预算、暂停/取消/复用/完成；fire-overlap覆盖700px旧目标排除及真实本地命中。freeze覆盖双频键盘/UI门禁、原生冰块、无排队、暂停/解冻/死亡；qixing包含毒雾、中毒十跳、暂停/复用。 |
| faulted检查点任务 | `continue-checkpoint-failure/`退出0：真实失败结算按钮等待faulted任务后返回地图，内存收益保留，手动保存并读盘一致；`checkpoint failure probe`错误日志是预期注入。首次`checkpoint-failure/`失败因窗口可见但流程尚未绑定回调；校正夹具等待MapRequested绑定，无生产测试API。 |
| 前端与地图OpenGL | `python <证据>/batch.py frontend level-guide`：依次用`--rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 36000 -- --smoketest=frontend`（图鉴加`--level-guide-only`），两项退出0。前端含架构身份/故障清理/重注册、取消加载、音量/保存及损坏档回滚；故障日志属于预期。图鉴含十地点、漩涡普通透明/48帧当前/悬停三态、38装备详情、十次嵌套悬停退出、两窗口尺寸及真实点击进关；最新map_form与guide_hover_niumogu截图已查看。 |
| 数值只读核实 | `python <证据>/audit_sampling.py`：71条攻击，55条敌方范围全为固定整数，非固定FlatPower仅悟空普攻1001—1005的0..4，无非固定PowerScale。完整表行、9处敌方显式Lerp及裁决存`sampling-audit.json`，表SHA256为`8227527771153a61a67ad3444b6e061203c25344dc0e8aff7b2c11f12a487433`。首次脚本未处理空vector而失败，按表默认0,0修正后断言通过，未写源表。保持调用次数/顺序，不声称排除了所有未来数值问题。 |
| Godot脚本扫描 | `python <证据>/run.py scan-continuation --headless --build-solutions --quit --no-window`：退出0，四个新增C#的UID由原生扫描登记。仍有ComponentInsoector对RefCounted调用Free/不存在free的既有退出错误。较宽的`--editor --import`先被自动审批以“生成物只读”拒绝；核对第50章后，带明确依据的原文扫描命令获准执行，没有绕过审批改写生成物。 |
| 格式/XML与引用 | 既有CSharpStyle对手写范围做token/注释保持整理，`style-continuation-write.json`为346文件/26个格式差异/无缺XML；补齐ArchitectureLifecycleTests。`style-final-continuation.json`为346文件/0格式差异/0缺XML。`python Tools/ProjectMaintenance/validate_game_references.py`为1885路径/373UID通过；相对首批380UID净减7，与删11份状态、加4份脚本一致。 |
| 差异与索引 | `git diff --check`、`git diff --cached --check`通过；新Godot C#均有UID，没有修改只读源码/规范。未执行git add/commit，`git ls-files --stage`与`index-semantic-baseline.txt`一致；只读git status可能刷新索引stat元数据，核对后SHA256稳定为`D07311E1EAD1959C95C7DFCA1A516D96738DE46CA5D7CE3825F7AF083141755B`。 |
| A15暂停当帧旧delta | 完整`continue-ui-opengl-fixed/`通过升级、48条坡面、平台/恢复球及换装重入后，在真实飞行球暂停断言失败。`python <证据>/run.py loot-pause-before --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 12000 -- --smoketest=ui --loot-pause-only`修前退出1，暂停后既有_Process(1/60)仍推进关卡计时。只在LevelViewController统一帧入口读取现有暂停事实，`loot-pause-fixed`用同模式/16000帧上限跑60/120Hz均PASS、退出0：计时和原生飞行球位置冻结、恢复跟随并真实拾取；退出6个ObjectDB。正式构建`build-pause-fixed.log`0错误/5条既有警告。 |
| 最终完整UI会话 | `python <证据>/run.py continue-ui-final --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 90000 -- --smoketest=ui --terrain-level5`：全部业务断言通过、退出0。覆盖背包完整四槽恢复、技能/真实命中/声音、无双、全部小怪血条/回池、成长门槛/连升/补满、平台、恢复品、换装检查点重入、两次真实击杀飞行球暂停与领取、放弃回滚、死亡提交、结算重试/回地图、备份回退、格式拒绝、加载取消及HUD失败清理。坡面本次按既有参数限定第五关8条；此前`continue-ui-opengl-fixed/`已独立通过全部48条，未把局部套件泛称全地形重跑。备份损坏与HUD缺绑定为预期注入，仍记录20纹理RID/8排版RID/2字体RID、38 CanvasItem、138 ObjectDB及10资源的关停诊断。 |
| 最后源码审计 | 新暂停修复与夹具修改后再执行既有CSharpStyle，`style-final-pause.json`仍为346文件/0格式差异/0缺XML；引用1885路径/373UID通过。所有已删除状态无C#/工程引用，无临时等级调试入口；保留的`--loot-pause-only`为真实缺陷的双频回归。没有继续扩大或重复无关测试。 |

完整UI的失败尝试保留：`continue-ui-full/`误用headless，在妖猴截图等待FramePostDraw，核对自建进程身份后停止，不能计PASS。`continue-ui-opengl/`及加诊断的`continue-ui-levelup-probe/`均退出1，定位到背包夹具只恢复物品列表、却留下EastSeaBanner：4级实际280/160正确包含各50法宝加成，断言裸身预期230/110。已修复夹具同时恢复完整Slots，并让既有测试Reset传入四个真实穿戴部位；未改正式成长/装备规则或放宽断言。随后发现的暂停缺陷A15及最终完整结果见上表，失败日志没有覆盖或改写成PASS。

退出诊断与业务断言分开：18专项有3～12个ObjectDB存活；fire-overlap另有2资源、freeze和sea-ai各1资源；前端/图鉴各5个ObjectDB/1资源。失败OpenGL完整尝试另有纹理/排版/字体RID、132～133个ObjectDB及10～11资源。只读插件和引擎退出期问题本轮不修，不能称零诊断；远程热更等可选建议没有冒称经过实机验证。

## 全面架构审计与首批整改（2026-11-02）

按目标文件先审计、对照GLM再实施；报告为[ArchitectureAudit-20261102](ArchitectureAudit-20261102.md)，规范依据第00/10/30/50/70章。首批修复全部穿戴的实例排重，将牛心唯一奖励规则移至现有ProfileSystem、由死亡状态按原时点提交，结果经GGF值事件给应用提示；技能提示改按资源路径取窗，无攻击包时跳过重叠枚举。奖励仍在两秒退场开始时直接入包，末尾死亡/清关事件时点保持。未改变存档v12、配置、场景几何、只读依赖或Git索引。

证据目录：`Godot/GodotProject/.godot/validation/architecture_audit_20261102/`（以下简称`<证据>`）。该目录的`run.py`为一次性隔离启动器，每组使用新的APPDATA，另由SmokeTestDriver创建唯一`user://Validation/<模式>/<编号>`，不使用正式档案。源码改动只有业务、测试与本文档；新增事件的UID由Godot自动扫描生成。

| 检查 | 实际命令与结果 |
| --- | --- |
| 引擎版本 | `S:/Godot4/Godot4CSharp_console.exe --version`：`4.7.2.stable.mono.official.ed1daf0bf`。SDK为4.7.2，net10.0；GGF未声明可信上游Tag/Commit，报告以当前仓库/Framework Git tree标识源码快照。 |
| 资产缺陷修前复现 | `dotnet test Tests/ProfileTests --no-restore -v quiet --filter FullyQualifiedName~ProfileCollection_RejectsAlreadyEquippedInstance`：7项中4失败/3通过；两种法宝在出战及非出战英雄穿戴中均可被重复领入背包，原三部位通过。 |
| 正式构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有框架/生成代码弃用警告。最终新增烟测脚本由下列Godot扫描再次构建成功；日志`build-final.log`、`scan-fixed/`。 |
| 档案/战斗/关卡 | `dotnet test Tests/ProfileTests --no-restore -v quiet`为240/240；`dotnet test Tests/BattleTests --no-restore -v quiet`为178/178；`dotnet test Tests/LevelTests --no-restore -v quiet`为19/19，均退出0。新增覆盖全部穿戴排重、非出战法宝、拒绝后保存重建、唯一奖励重复不抽属性、满包不抽属性/不消耗资格、回滚重新领取。NU1900为NuGet审计服务不可达，Battle另有既有CS8632/Export字段警告。 |
| Godot扫描与构建 | `python <证据>/run.py scan-fixed --headless --editor --import --build-solutions --quit --no-window`：退出0，完成新事件脚本/UID扫描与构建。stderr仍有ComponentInsoector对RefCounted调用Free/不存在free的既有退出错误，不能称为无错误扫描。 |
| 牛魔真实专项 | `python <证据>/run.py bull --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 48000 -- --smoketest=ui --bull-only`：退出0。60/120Hz双向五招、兽形/护卫、吞噬、心脏显隐/暂停与拥有的手/火柱清理通过；`SMOKE-BULL TREASURE PASS`覆盖真实致死后首次/重复/满包、提示关闭仍完成死亡回池、重开订阅唯一且不播放过期奖励。退出10个ObjectDB和1个资源诊断。 |
| 完整牛魔谷与磁盘 | `python <证据>/run.py level10 --headless --audio-driver Dummy --max-fps 120 --quit-after 24000 -- --smoketest=level10`：退出0。真实阶段、三形态、自动通关、地图与磁盘读回通过，经验1598；`SMOKE-BULL TREASURE DISK PASS`确认同一金钢琢实例和已抽属性写入实际检查点。含待飞行奖励的回地图耗时3942ms，符合该场景断言。退出9个ObjectDB。 |
| 前端保存与导航 | `python <证据>/run.py frontend --headless --audio-driver Dummy --max-fps 120 --quit-after 12000 -- --smoketest=frontend`：退出0，六格/选角/地图/保存提示/保存后返回与部分档案装配失败后重读通过；坏档、跨槽和半初始化错误为既有预期注入。退出3个ObjectDB。 |
| 背包与技能提示 | `python <证据>/run.py inventory-skills --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 60000 -- --smoketest=ui --inventory-skills-only`：退出0。背包、法宝及15种换装、技能提示窗关闭时拒绝学习且不扣费、重开后不足提示、五招真实输入与命中/声音/轨迹通过。该模式提前结束，不把通用尾句所称的加载取消等算成本次覆盖。退出有17纹理RID、8排版RID、2字体RID、GL纹理、131个ObjectDB与10资源诊断。 |
| 被动第二提示入口 | `python <证据>/run.py passive --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 12000 -- --smoketest=ui --passive-only`：退出0；余额不足、25次购买、满级拒绝、五项属性、磁盘及重试重入全部通过。退出5个ObjectDB和1个资源。 |
| 风格与引用 | `dotnet run --project Tools/ProjectMaintenance/CSharpStyle --no-restore -- P:/Godot-Project/GameZMXY <证据>/style-final.json`扫描353个手写文件；本轮10个C#文件无格式差异/缺XML。20个既有格式差异文件和ArchitectureLifecycleTests的既有XML缺项保留，不扩散格式化；只对本轮文件的副本用既有工具做token保持格式化。`python Tools/ProjectMaintenance/validate_game_references.py`为1885路径/380UID通过；`git diff --check`通过。 |

未通过尝试也保留：第一次Battle构建因测试项目尚未链接ProfileSystem新依赖的正式EquipmentSystem而失败，补全链编后178项通过；首次`scan`因新增技能烟测缺少UI扩展命名空间导致编译失败，`build-after-smoke.log`记录CS0308，补using后`scan-fixed`退出0。未以旧程序集烟测替代最终构建。测试复现、失败的构建尝试与插件退出诊断分别记录，没有修改只读框架/插件来隐藏日志。

未完成：报告第二至四阶段尚未实施；身体候选形状复核、数值舍入收敛等仍需新增实证，不宣称已修。插件释放及各模式ObjectDB/RID/资源退出问题继续列为待定位，既有日志不能证明本轮新增或已排除所有泄漏。集中规范未改，后续文档规范维护另按用户明确要求处理。

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

## 图片图鉴与装备详情对齐（2026-11-02）

依据第00/10/40/50章，地图仍拥有一份浮层；只读Query提供配置，图鉴和背包共用装备详情脚本。260px宽卡片改为短介绍、84px高的守关图像区域和48px透明装备格；删除尾句、隐藏关入口及查询字段，隐藏关战斗/掉落逻辑未改。装备详情显式使用共用字体，品质/类型行垂直居中，80×2渐隐线从19px上移至18px。原生作者保存已有场景UID，地图场景保持暂存版本，未新增生产测试接口或改正式档案。

证据目录为`Godot/GodotProject/.godot/validation/guide_visual_20261102/`。

| 检查 | 命令与结果 |
| --- | --- |
| 原生作者与扫描 | `Godot4CSharp_console.exe --headless --editor --path Godot/GodotProject --script res://EditorScripts/author_level_guide.gd`、`repair_equipment_tooltip_layout.gd`均输出PASS；作者默认只更新子场景，首次绑定地图可加`-- --bind-map`。`--headless --path Godot/GodotProject --build-solutions --quit --no-window`退出0，完成UID扫描和构建；独立`dotnet build Godot/GodotProject --no-restore -v quiet`为0错误、5条既有警告。 |
| 十关图片与交互 | `python <证据>/run_guide.py`退出0。十卡片宽260px、高252～323px；逐图验证atlas区域不越界、无透明留白，牛魔谷对应兽形身份。十地点真实进入→移入透明图标→原生嵌套详情→离开关闭通过；940×590和1175×738视口边界、实际点击花果山、HUD加载后浮层清理通过。无Footer、隐藏入口及概率尾句。`cards/1..10.png`和`cards_montage.png`已目视核对。 |
| 全装备详情 | 同一专项逐件渲染38种正式装备、全部六品质；标题/数值共用字体并同排居中、两条线位置、背景包围文字、动态尾部6px余量均通过。图鉴普通防具保留20～40范围；`equipment/1..38.png`及`equipment_montage.png`已查看，长说明/负数/百分比均正常。初次断言错误要求两类Label外框等高，已改为检验Godot收缩布局后的同排中心并保留失败日志；未改生产布局去迎合错误判据。 |
| 共用背包回归 | `python <证据>/run_ui.py equipment-only`退出0。15件悟空穿戴素材、真实分页/穿卸/预览、法宝保留及实际属性详情通过；头部固定、可变属性、长说明换行、首次原生弹窗底部6px余量通过。最终`inventory_tooltip_attributes.png`、`inventory_tooltip_fitted.png`及`inventory_tooltip_wrapped.png`已查看，修改前图另留`equipment_before.png`。 |
| 查询与资源 | `dotnet test Tests/ProfileTests --no-restore -v quiet`为231/231；移除原隐藏关图鉴专项，保留十个主线守关/掉落查询测试。`python Tools/ProjectMaintenance/validate_game_references.py`为1874路径/379UID通过；`git diff --check`通过。NuGet漏洞源网络警告不影响测试执行。 |

最终运行保留退出诊断：图鉴为5个ObjectDB/1资源；背包为162个ObjectDB/16资源及RID/GL纹理存活提示。编辑器仍报告只读ComponentInsoector对RefCounted调用Free的既有错误，未修改插件。以上PASS指具体断言与业务退出码，不代表无诊断退出，也不把烟测通用末尾文案当作完整前端/UI套件已运行。

## 紧凑地图图鉴与装备集合审查（2026-11-01）

依据第00/10/20/40/50章，图鉴按一次组合Query读取，地图拥有一份可停留浮层，装备详情共用现有控件。最终260px宽、32px图标，父卡短介绍/守关/掉落，子详情显示配置属性范围；没有新增交易或存档字段。用户明确授权11格玩家介绍和既有Luban生成。以下证据目录为`Godot/GodotProject/.godot/validation/level_catalog_20261101/`，正式档案及Git索引未改。

| 检查 | 命令与结果 |
| --- | --- |
| 官方与第一格装备 | 逐条读同版`remaining_levels/main_scripts/scripts/my/AllEquipment.as`，按fillName去重44种，与Items的38种对照；6种未接入详见ProjectTour。大圣战铠/锦襕袈裟为史诗且showid=1，OtherMat符号中无独立对应图；传说防具未发现。第一格只读核对59件/38种、防具23血海魔甲；不是存档漏塞已接入装备。证据`official_equipment_comparison.json`。 |
| 现有装备外观 | `python Tools/LegacyMigration/audit_equipment_appearances.py --output <证据>/equipment_audit.json`：38种身份/38项唯一透明背包与地面图通过。修复作者审计未跳过`##`注释行而把“装备编号”当装备的问题，未修改源装备表。 |
| 介绍与XLSX Diff | Artifact作者化11条介绍，保留原ZIP其他部件与所有非目标单元格；`check_diff.mjs`经已安装XLSX Diff插件worker，普通/公式模式均准确11格。`gen_code_bin_to_project_lazyload.bat`成功，仅LevelConfig.xlsx和level_tblevelconfig.bytes变化，配方/数值不变。 |
| 正式构建与扫描 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有弃用警告；Godot `--headless --path Godot/GodotProject --build-solutions --quit --no-window`扫描新增脚本UID并构建、退出0，保留既有插件释放诊断。 |
| 真实图鉴交互与画面 | `python <证据>/run_guide.py`（隔离APPDATA，OpenGL，`--smoketest=frontend --level-guide-only`）：11卡片渲染、10地点真实进入/移入图标/嵌套装备详情/离开关闭通过；水帘洞↔龙宫真实按钮切换、普通装备20～40范围、940×590及1175×738窗口边界、实际点击花果山及HUD加载后浮层清理全部通过，退出0。静态图`cards/1..11.png`与`frontend/guide_hover_*`、`guide_hidden_palace`、两种缩放图已目视核对。 |
| 背包回归 | `python <证据>/run_ui.py equipment-only`：退出0，15件悟空装备穿卸、战斗/背包预览、法宝保留、分页及实例属性详情通过；图鉴没有随机抽取实际装备，也没有使装备格提交交易。 |
| 纯规则/工具/引用 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：232/232，包括新增守关物种、三形态、隐藏入口与条件奖励查询。`python -m unittest discover -s Tests/ToolTests`：23/23。`validate_game_references.py`：1860路径/379UID；`git diff --check`通过。 |

迭代失败保留在开发记录中：旧冒烟只允许装备tooltip、静态卡片夹具在布局前取坐标、末波末行误当守关、原生tooltip尚未关闭时同帧点击、进关加载中途退出。均分别修正测试时序或实际归属后完整重跑，未把旧PASS当新小浮层完成依据。最终图鉴退出仍报告5个ObjectDB/1资源；背包退出为107个ObjectDB/9资源及既有RID/GL纹理诊断。编辑器作者化仍有ComponentInsoector释放诊断，未修改只读插件。通用烟测末尾文案不代表执行了全部UI生命周期。

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

## 第1格补装与法宝换装修复（2026-11-01）

依据第00/10/70章，在现有装备System中只更新请求部位，不新增解锁字段、版本或生产测试接口。第1格`1791793987`补入当前缺少的定义1～8及38，现为38/38种、57件；保留原有48件实例、125462经验、2629812灵魂、最远通关9、全部英雄记录及穿戴。法宝槽仍为东海旗幡，新增金钢琢在背包。其他格及其备份散列与操作前一致。

证据目录：`Godot/GodotProject/.godot/validation/slot1_equipment_20261101/`。一次性维护工具复用正式`Roll`及`SaveDataUtility.Validate`，只在忽略目录使用；正式代码不含维护入口。备份位于`%APPDATA%/Godot/app_userdata/GameZMXY/GameData.backup-before-slot1-all-equipment-20261101_194425`，8个原文件逐一校验散列，见`applied.json`。首次应用被自动审批以“未提供执行命令”拒绝，未执行；只读复核脚本及源指纹后，以明确的`pwsh -NoProfile -File .../apply_slot1.ps1`执行成功。

| 检查 | 命令与结果 |
| --- | --- |
| 缺陷复现及纯规则 | `dotnet test Tests/ProfileTests --no-restore -v quiet --filter FullyQualifiedName~Treasure_PersistsAcrossOtherEquipmentChanges`：修复前6/6失败；修复后`dotnet test Tests/ProfileTests --no-restore -v quiet`为228/228通过，只有NuGet审计服务不可达警告。覆盖两种法宝、武器/防具/饰品的首次穿戴、替换、卸下、属性和保存重建。 |
| 正式构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误、5条既有Framework/生成代码弃用警告。 |
| 正式档读回 | `dotnet run --no-build --project <证据目录>/SaveMaintenance.csproj -- validate <第一格.sav或.sav.bak> Godot/GodotProject/TheGame/DataTables/GameConfigs`：主档及.bak均输出`SAVE VALIDATION PASS`，38/38种、57件、容量75、东海旗幡引用有效，经验/灵魂/进度均保持。 |
| 实机背包 | `python <证据目录>/run_ui.py`：隔离APPDATA，调用`S:/Godot4/Godot4CSharp_console.exe --path Godot/GodotProject --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 --quit-after 24000 -- --smoketest=ui --equipment-only`，退出0。`ui.log`含`SMOKE-TREASURE GUI PASS`、15件悟空武器防具真实穿卸及分页/详情/属性回归通过；未运行其汇总文案所称的全部UI生命周期场景。 |

本次实机退出仍报告2个纹理RID、8个排版RID、1个字体RID、38个CanvasItem、111个ObjectDB和9个存活资源，并有两条GL纹理泄漏诊断。业务断言通过不代表这些退出诊断已解决；本轮未扩大修改框架或插件。

## 全装备与法宝穿戴（2026-10-31）

第1格已备份后更新为 v12：保留原有进度，装备集合补齐当前配置 1～38（40 个实例，原有重复实例保留），金钢琢写入悟空独立法宝槽。第2/3格补齐 `TreasureInstanceId` 空字段并升到 v12。

| 检查 | 命令与结果 |
| --- | --- |
| 正式构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误，5条既有 Framework/生成代码弃用警告。 |
| 纯规则测试 | Profile `222/222`、Battle `178/178`、Level `19/19` 通过；法宝测试覆盖穿戴、属性变化、保存、重建与卸下。 |
| 存档读回 | 正式 `SaveDataUtility.Validate` 验证第1/2/3格全部通过；第1格 `equipment=40`、金钢琢 `TreasureInstanceId` 非空。备份目录为 `GameData.backup-before-treasure-slot-20261031_214739`。 |
| UI 烟测 | `--smoketest=ui --equipment-only`：`SMOKE-TREASURE GUI PASS`，跨页选择、独立法宝槽、tooltip、穿戴、属性替换和法宝替换通过；退出业务码0。 |


## 设置保存后返回与技能回归（2026-10-31）

设置界面的返回地图/标题沿正常流程处理：先调用已有 `SaveSystem.SaveManuallyAsync`，保存成功后由现有 `LevelSystem.EndSession(rollback: false)` 结束本局事务，再切换到 `ProcedureMap` 或 `ProcedureTitle`。没有新增 `CommitEarlyExit` 或第二套存档提交接口；未保存的普通离开仍按既有关卡规则回滚进关快照。

| 检查 | 命令与结果 |
| --- | --- |
| 正式构建 | `dotnet build Godot/GodotProject --no-restore -v quiet`：0错误；5条 Framework/生成代码既有弃用警告。 |
| 纯规则测试 | `dotnet test Tests/ProfileTests --no-restore -v quiet`：222/222；`dotnet test Tests/BattleTests --no-restore -v quiet`：178/178；`dotnet test Tests/LevelTests --no-restore -v quiet`：19/19。三组均只有既有 NU1900 审计服务警告。 |
| 前端烟测 | `S:/Godot4/Godot4CSharp_console.exe --headless --path Godot/GodotProject --quit-after 2400 -- --smoketest=frontend`：退出0，设置返回地图保留关内经验，返回标题保留新增灵魂，菜单、六格存档、选角、地图、提示复用和 GGF 声音全部通过。跨槽/坏档日志是烟测预期注入；退出仍有3个 ObjectDB 泄漏诊断。 |


## GGF + QF 架构边界与状态效果（2026-10-31）

依据第00/10/50/60章和用户要求先调研再重构。公开源码与适用限制集中于 [ArchitectureReview](ArchitectureReview.md)，实际所有权/初始化入口见 [ProjectTour](ProjectTour.md)。本轮将冻结、中毒数据聚合到 HeroModel.StatusEffects，由 HeroStatusSystem 维护效果规则；技能状态类型和起手委托归 SkillSystem；装备/学习 Command 复用 LevelSystem.IsRunning。保留现有 QF 取用能力、Value 赋值、GGF 服务和 Godot 表现，不增加通知事务包装，不更改存档格式。

证据目录为 `Godot/GodotProject/.godot/validation/architecture_20261031/`。引擎入口为实际存在的 `S:/Godot4/Godot4CSharp_console.exe`；以下 `run.py` 为忽略目录中的隔离 APPDATA 启动器，不接触正式档案。代码和本轮工具输出没有修改 Framework/addons/Luban，也未操作 Git 索引。

| 检查 | 命令与结果 |
| --- | --- |
| 正式构建 | `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet`：0错误、5条已有框架/生成代码弃用警告。 |
| 档案/业务测试 | `dotnet test Tests/ProfileTests/ProfileTests.csproj --no-restore -v quiet -- xUnit.ParallelizeTestCollections=false`：216/216。包含初始化/清理失败、Command 会话、技能登记、冻结/中毒24/60/120/144Hz、并存、死亡/重建清理和跨作用域隔离；保留原保存/交易测试。NU1900为无法读取NuGet审计服务，另有测试替身Export字段未赋值警告。 |
| 战斗/关卡规则 | `dotnet test Tests/BattleTests/BattleTests.csproj --no-restore -v quiet`：178/178；`dotnet test Tests/LevelTests/LevelTests.csproj --no-restore -v quiet`：19/19。仍有NU1900与既有CS8632警告。 |
| 原生扫描 | `python <证据目录>/run.py scan --headless --editor --import --build-solutions --quit --no-window`：退出0，完成脚本扫描和UID登记；编辑器仍输出ComponentInsoector的RefCounted.Free错误，不能称为无错误日志。 |
| 真实FSM与前端 | `python <证据目录>/run.py frontend --headless --audio-driver Dummy --max-fps 120 -- --smoketest=frontend`：退出0、`SMOKE-ARCHITECTURE PASS`及前端PASS。真实GGF FSM验证同一引用、作用域隔离、重复注册拒绝、初始化失败/退出失败仍移除、后续重新注册。跨槽/坏档/加载失败为预期注入；退出报告3个ObjectDB实例。 |
| 冻结 | 同一启动器 `freeze --headless --audio-driver Dummy --max-fps 120 -- --smoketest=ui --freeze-only`：退出0，60/120Hz、键盘/UI无双门禁、解冻无排队、暂停、死亡清理PASS；退出报告3个ObjectDB实例。仅覆盖该专项，通用UI尾句不代表全套。 |
| 中毒 | 同一启动器 `qixing --headless --audio-driver Dummy --max-fps 120 -- --smoketest=ui --qixing-only`：退出0，六招双方向、刀/毒雾实际命中、中毒十跳、暂停和复用PASS；退出报告9个ObjectDB实例及1个资源仍使用。 |
| 龙王 | 同一启动器 `dragon --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui --dragon-only`：退出0，两档×60/120Hz×左右×四招共32组PASS；无双状态免疫、暂停、离身攻击取消PASS；退出报告5个ObjectDB实例。 |
| 完整UI | `python <证据目录>/run.py ui_opengl_final --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui`：退出0，技能面板、五招真实输入、飘字/暂停、装备/背包、坡面与平台、换装重入、失败结算重试/回地图、音乐和存档回归全部业务PASS；HUD 缺绑定是预期失败注入。退出仍报告 Godot RID/ObjectDB/资源泄漏，不能称为零诊断。 |
| F6 独立场地 | `python <证据目录>/run.py arena_final3 --headless --audio-driver Dummy --quit-after 360 --scene res://TheGame/Scenes/test_arena.tscn`：退出0，修正 `m_Enemies` 场景绑定后玩家与两只怪物正常显示；场地在 `EventComponent.TreeExiting` 回调中先停止飘字/实体/本地FSM，再由框架关停，已消除“事件473不存在”的退出异常。仍有8个ObjectDB、3个RID和3个资源泄漏诊断，具体来源待定位；本轮未改只读框架。 |
| 引用与风格 | `python Tools/ProjectMaintenance/validate_game_references.py`：1852路径/376UID通过；`dotnet run --project Tools/ProjectMaintenance/CSharpStyle --no-restore -- <仓库根> <证据目录>/style_final.json`：349手写文件通过，格式修正均经token保持校验，无缺失XML文档；`git diff --check`通过。Bindable、Equipment/Profile/Save System没有语义差异。 |

未通过的尝试保留：`ui` headless 首轮在恢复飘字暂停取样处失败，且失败退出定时器被零时间缩放冻结，启动器240秒超时（124）；测试现先等待本帧已计算 delta 消费完，并使退出定时器忽略时间缩放，正式飘字逻辑未改。`ui_final` headless 已通过该断言、背包/技能/实体复用和怪物血条，但停在妖猴 `FramePostDraw` 等待；该套含真实绘制检查，headless 不适用，已终止确切进程，不能计为 PASS。`ui_opengl` 首轮正常推进到坡面却被240秒总时限截断（124）；最终改为900秒上限的 OpenGL 完整执行，不删坡面或渲染断言来凑通过。F6 首轮的 `m_Monsters`/`m_Enemies` 序列化名称不一致及飘字退出顺序也已保留在 `arena_final` 日志，`arena_final3` 为修复后结果。各次原日志和退出结果均保留。

剩余限制：新状态效果当前只服务本局英雄，没有宣称实现多目标/叠层/通用Buff编辑器。当前UI属于直接Controller绑定Model的实现，没有额外MVVM框架。编辑器插件释放、MP3/资源/ObjectDB退出诊断与业务断言分别记录；本轮不扩大只读依赖修补。

## 框架关停生命周期（2026-10-31）

根因是 `BaseComponent` 在 `OnPreDestroy` 才调用框架 Shutdown，此时 Godot 已先释放 `HttpRequest`、声音辅助器和部分 UI 节点；任务池随后访问已释放对象，分别产生 WebRequest `ObjectDisposedException`、声音代理异常和窗口模型空引用。修复将既有 `GameEntry.Shutdown(ShutdownType.None)` 移到 `BaseComponent.OnExitTree`，并让标题、选角、地图、关卡流程在关停时先关闭自己拥有的窗口；普通导航继续使用原淡出，关停立即停止音乐。背包确认框只在仍在场景树内时释放焦点。框架授权登记在第50章。

- `validate_framework_shutdown.py`：本地回环 GET/POST、超时后复用、4条在途+2条排队请求、显式重复关闭、同进程重建，`FRAMEWORK SHUTDOWN PASS`；无 `ObjectDisposedException`、无对象/资源泄漏诊断。
- `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet`：0错误、5个既有生成代码弃用警告。
- `--smoketest=frontend`：退出0，开始/存档/选角/地图流程通过；存档损坏、旧作用域和预期失败注入仍按测试设计输出错误日志。
- `--shutdown-window=inventory|skills|settings`：三种真实暂停窗口均能在退出前完成退订；背包焦点错误不再出现。
- `dotnet test`：Battle 178/178，Profile 211/211；C# 风格346文件通过，引用1852路径/374 UID通过，`git diff --check`通过。

剩余限制：Godot 原生 `AudioStreamPlayer` 在独立、无 GGF 的 MP3 播放脚本中也会报告退出时 `AudioStreamPlaybackMP3`/资源仍在使用，属于引擎退出行为；本轮没有用业务层强行释放或修改只读插件。编辑器 `ComponentInsoector` 的 `Free()` 错误是另一个插件生命周期问题，不属于本次运行时框架修复，仍按既有插件记录保留。

## 火魔斩左右击退与末段命中（2026-10-31）

按第10/30/50章将具体招式规则留在悟空状态，不增加通用开关、表字段或生产观测API。官方依据为同版 `Role1.addHit10And1`、`BaseMonster.beMagicAttack`、`BaseObject.beAttackBack`：九连中心与目标重合时默认右推，下冲跟随角色朝向。用户明确选择修正该原版偏置；仅删除火魔九连、下冲、落地的中心参数，沿已有出招方向击退，伤害、几何、速度大小、时序和其他招式均不改。证据与源文件备份在 `.godot/validation/fire_demon_knockback_20261031/`。

- 修改前真实输入复现 `baseline`：60/120Hz×左右×猴子/大猩猩共8组，目标仅关闭AI、保留完整物理。朝左四组首击仍为右推，落地末段四组均漏打；朝右四组末段均命中。旧轨迹测试冻结目标身体，不能证明连招运动正确。该次诊断运行完成记录，关停发生原生GC `0xC0000005`，退出3221225477，不计为通过。
- `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet`：0错误、5个既有框架警告。
- `dotnet test Tests/BattleTests/BattleTests.csproj --no-restore -v quiet`：178/178；`dotnet test Tests/ProfileTests/ProfileTests.csproj --no-restore -v quiet -- xUnit.ParallelizeTestCollections=false`：211/211。技能状态断言覆盖三阶段不再提供中心参数，原并行时钟、取消及落地复用测试保留。
- `python Godot/GodotProject/.godot/validation/fire_demon_knockback_20261031/run_formal.py final --rendering-method gl_compatibility --audio-driver Dummy --max-fps 120 -- --smoketest=ui --fire-knockback-only`：退出0、8组PASS；逐物理步检查每次受击横向与施放朝向一致，九连/下冲/落地实际命中，使用隔离APPDATA。`traces/`保留逐步位置、速度、待消费击退及生命变化，`baseline/traces/`保留修复前对照。
- `dotnet run --project Tools/ProjectMaintenance/CSharpStyle --no-restore -- <仓库根> <证据目录>/style_audit.json`：345文件、0格式差异/0缺失文档；`python Tools/ProjectMaintenance/validate_game_references.py`：1852路径/374UID通过；`git diff --check`通过，Git索引SHA不变。

范围限制：本次8组是有目标的普通火魔连招，不宣称所有地形或全部技能均已重跑。最终运行仍有既有GGF WebRequest关停 `ObjectDisposedException`、12个ObjectDB实例和3个资源在退出时未释放，业务断言与进程退出0单独记录，未修改只读依赖。

## 小怪血条静态布局（2026-10-31）

依据第10/30/40章的表现布局、节点装配及原生场景约定，将13种小怪已在场景中实例化的血条改为本地位置，不再运行时覆盖。证据和修改前备份在`.godot/validation/static_health_bars_20261031/`；Git索引SHA保持。

- `repair_monster_health_bars.gd -- --author-positions`：Godot原生作者化13场景，保留有效UID；随后只保留4个offset差异，删除重存附加的继承默认值，未改其他节点、碰撞或动画。
- `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet`：0错误/5个既有框架警告；CSharpStyle覆盖345文件。
- `python <证据目录>/run_formal.py monster_health --rendering-method gl_compatibility --audio-driver Dummy --max-fps 60 -- --smoketest=ui --monster-health-only`：退出0，13种小怪的比例/治疗/淡出/暂停/死亡/实体池复用通过；新增人工移动血条后再扣血及推进真实物理帧，确认本地位置不被覆盖。妖猴、蝙蝠、虾兵真实截图已查看。
- `native_final`运行原生布局检查：13场景均50×5、直接父节点跟随、位置与原运行时公式一致；资源差异归一检查确认只变血条offset。`validate_enemy_ai_tools.gd`：20脚本解析通过；3个Python导入器AST解析通过。
- `python Tools/ProjectMaintenance/validate_game_references.py`：最终1852路径/374UID通过；`git diff --check`通过。未改BOSS顶部HUD锚点、表、存档或只读依赖；原有编辑器释放和运行关停诊断仍单列保留。

## 造一无双受击、设置音量与功能清单（2026-10-31）

证据目录：`Godot/GodotProject/.godot/validation/musou_settings_20261031/`。按第00/10/30/40/50章分工，官方Role1/BaseHero/BaseAddEffect为行为依据。隔离完成后，用户明确授权应用41个已备份的项目自有文件；`delivery.json`与`backup/`记录前后SHA和回滚来源，表格、正式存档、Framework/addons及Git索引保持。

| 检查 | 命令与结果 |
| --- | --- |
| 官方素材 | 既有`export_official_combat_effects.py --java <Java> --ffdec <FFDec> --role <Role_v7> --monsters <Monster_v1> --group hero_hurt`：散列校验后原生导出8帧HeroBeHurt，保留源画布/注册点；没有重导身体、武器和伤害库。 |
| 无双真实受击 | `python <证据目录>/run_formal.py formal_musou --rendering-method gl_compatibility --audio-driver Dummy --max-fps 60 -- --smoketest=ui --musou-hurt-only`：正式退出0、60/120Hz业务PASS。开场结束后检查仍扣血、非零击退不生效、普攻继续原进度、8帧闪光、白光优先、第五击保护及无双结束恢复角色配色；两向截图已查看。霸体规则从共享HeroSystem移到WukongEntity，普通英雄不自动继承。 |
| 原受击保护 | 隔离同运行器`ordinary_hurt --headless --audio-driver Dummy --max-fps 60 -- --smoketest=ui --hurt-protection-only`：退出0、60/120Hz第五击、技能无敌重叠、暂停和红光到期清理通过。 |
| 设置与前端 | 隔离`frontend_disk_final`及正式`formal_frontend`使用`--rendering-method gl_compatibility --audio-driver Dummy --max-fps 60 -- --smoketest=frontend`。覆盖地图入口、原生鼠标拖动、音乐独立、SFX/UI联动、静音恢复、零/满量程、ConfigFile实际读盘、重开恢复、战斗暂停/继续/导航、池复用。正式退出0且全部业务PASS，标题/地图/战斗截图已按最终版本查看。用户最后要求滑条收窄后，正式narrow_frontend再验证退出0/全部PASS，最终两条均为160px。 |
| 视觉约束 | 实机断言面板326px、两条滑条160px、共同中心线、同字体尺寸与白色；帮助/导航按可见内容自适应。最初奇数内容宽度产生0.5px中心差异，修为272px后通过；不扩大断言容差掩盖对齐问题。金色初版已撤掉，最终资源为白/灰轨道与白/橙抓手。 |
| 纯规则 | 隔离`dotnet test Tests/BattleTests/BattleTests.csproj -v quiet`：178/178；`dotnet test Tests/ProfileTests/ProfileTests.csproj -v quiet -- xUnit.ParallelizeTestCollections=false`：211/211。仅调整已迁出System的霸体断言，原倍率测试保留；没有增加生产观测API。 |
| 构建及引用 | 正式Godot`--headless --editor --import --build-solutions --quit --no-window`：退出0、完成扫描；隔离build_delivery.log为0错误/5个既有依赖警告。`python Tools/ProjectMaintenance/validate_game_references.py`：1852路径/374UID通过；CSharpStyle覆盖345文件，0格式差异、0缺失文档；`git diff --check`通过。 |
| 玩法缺项 | `Docs/ProjectTour.md`为唯一可勾选清单：以同版SelectRole/SelectPLace、User、Role2、GameInfo/Batter、PackThings、AllEquipment对照当前角色/技能/穿戴/装备登记，不将后续作品系统或未开放占位算成造一现成玩法。 |

限制与失败记录：第40章仍记原固定高度341px，本轮依用户新增两行及适配高度的要求实现，规范正文未改。早期无双夹具未恢复身体物理导致未落地，已修测试；早期滑条缺少最小宽度、内容尺寸被缩到最小值，截图发现后修正，不将首轮失败计为通过。前端布局失败时游戏已暂停，测试驱动无法结束；只终止已确认失败的隔离进程，测试catch补对称恢复暂停后最终重跑。自动审批曾两次拒绝正式写入，用户明确批准后才应用。前端的旧作用域检查点错误为既有故障注入预期；运行关停仍有GGF WebRequest ObjectDisposedException、编辑器退出仍有ComponentInsoector释放错误及RID/ObjectDB诊断，未修只读依赖，不把业务PASS称为无错误日志。

## 敌人行为命名、原版空间与碰撞显示（2026-10-30）

本轮证据目录：`Godot/GodotProject/.godot/validation/enemy_ai_review_20261030/`。隔离工程先完成修改与回归，再经正式Godot编辑器应用；`delivery_manifest.json`、`formal_backup/`保留本轮100条文件操作及修改前内容。Git索引SHA与起点相同。

| 验证 | 命令与结果 |
| --- | --- |
| 独立源码依据 | `python Tools/LegacyMigration/audit_official_enemy_ranges.py --output <证据目录>/official_ranges_recheck.json`：24物种，校验原SWF哈希、BaseMonster共同表达式、物种独立分支和源文件路径。正式重跑与隔离参考逐字节相同；不从Godot当前形状反推期望值。 |
| 原生范围与装配 | Godot `--headless --script res://EditorScripts/validate_enemy_spatial_rules.gd -- <official_ranges_recheck.json>`、`validate_enemy_perception.gd`：正式`formal_spatial/`、`formal_perception/`均退出0，24场景、178边界/语义检查通过；没有invalid UID警告。 |
| 作者工具 | `--headless --script res://EditorScripts/validate_enemy_ai_tools.gd`：`formal_tools/`退出0，19个导入/验证/显示工具解析通过；重导默认值复用同一原生颜色作者工具。 |
| 纯规则 | 隔离`dotnet test Tests/BattleTests/BattleTests.csproj --no-restore -v quiet`：178/178；`dotnet test Tests/ProfileTests/ProfileTests.csproj -v quiet -- xUnit.ParallelizeTestCollections=false`：211/211；`dotnet test Tests/LevelTests/LevelTests.csproj -v quiet`：19/19。新增检查当前攻击档的初始/释放冷却分别采样、配置不被改写、实例冷却独立。测试反射仅在夹具。 |
| 迁移与构建 | `python <证据目录>/run_formal.py formal_build_scan --headless --editor --import --build-solutions --quit --no-window`：退出0，完成扫描；`dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet`：正式增量构建0错误/0警告，隔离完整编译0错误/5个既有依赖警告。6个脚本与UID一同移动；有效场景UID保持。 |
| 几何及动画保持 | 原生场景文本按形状内容与节点路径归一比对：98个非AI身体/受击/攻击形状相同；55份动画库逐字节不变。范围修改只涉及AI空间，颜色重存没有扩大实际伤害盒。 |
| 原生视口 | Godot `--editor --rendering-method gl_compatibility --script res://EditorScripts/render_enemy_collision_styles.gd -- <visuals目录>`：妖猴、蝙蝠、虾兵、彭魔王、牛魔王人形、龙王六图已查看。低透明度轮廓保留身体可见性；这是引擎原生编辑器视口渲染，非自画示意图。本机桌面控制工具初始化失败，未冒称完成UI操控。 |
| 资源与风格 | `python Tools/ProjectMaintenance/validate_game_references.py`：1844路径/374UID通过；`dotnet run --project Tools/ProjectMaintenance/CSharpStyle --no-restore -- <仓库> <证据目录>/style_formal.json`：345个手写文件，0格式差异、0缺失XML文档；`git diff --check`通过。 |

正式引擎专项使用`python <证据目录>/run_formal.py <case> --headless --audio-driver Dummy --max-fps 60 -- --smoketest=ui --<专项>-only`，AI入口为`--smoketest=ai`。结果见`formal_regression_results.json`，隔离追加覆盖见`regression_results.json`：范围边界60/120Hz、24物种双侧索敌/保留目标、六种普通近战自主追击/出招、海怪、彭魔王、狮驼王、鲨鱼、牛魔王三形态及龙王两档双向攻击均有业务PASS。正式7组（enemy-ranges、ai、enemy-fidelity、enemy-hierarchy、sea-ai、dragon、bull）全部退出0且业务PASS；隔离8组也全部通过。不将旧未import而失败的批次算作通过。

日志限制：Godot编辑器退出仍报既有ComponentInsoector对RefCounted调用Free及部分RID/ObjectDB泄漏；实际游戏退出仍有GGF WebRequest的ObjectDisposedException。这些与业务断言及退出码分别记录，本轮未改Framework/addons；启动和资源验证未见新的GCHandle或无效UID错误。早期未import批次因主场景UID未解析退出1，完成导入后重新运行通过，不冒称所有尝试均成功。第30章旧AI/首掷延迟/BuildAttackParams文字仍过时，仅按本轮明确要求补颜色规范。

## 敌人 AI 职责与配置收敛（2026-10-30）

删除空AI死亡/控制状态、旧追击—站定—守候链及巡逻抽象层；普通敌人使用Patrol/Engage，物种只实现实际选招和特殊运动。死亡停止AI时钟、清目标并销毁AI FSM，身体继续负责尸体、奖励和回池。依据第00/10/20/30/40/50章；第30章仍描述旧状态、首次延迟和伤害盒并集估距，与本轮明确要求冲突，未擅改规范。

24个场景的Perception原生形状拥有Detection首次索敌、Engage交战和Patrol左右折返；专属退避/近身/水柱/吞噬/践踏留在各物种。彭魔王保留800px追踪高度，与400px圆形选招、150px近身半径分开；牛魔王人形无限高度的规则留具体状态。预设只组合状态类型，运行时不读表覆写范围，也不合并动画伤害盒估距。

Enemy两页删除SightRange、BehitCalmTime、PatrolInterval、PatrolIdleChance、PatrolRadius、AttackFirstDelay、AttackRangeSlack、LoseTargetTime、PaceRange；Combat删除AiWeight、AiPriority、AiRange；__beans__同步。AttackInterval、AttackDesire、AiCooldown/AiInitCooldown/AiRecovery仍有实际消费者。Artifact作者化后保留原ZIP其他部件和样式；已安装XLSX Diff数值/公式模式三表均PASS（270/222/27差异格）。仅经既有Luban入口更新两个配置类型及两份bytes。

| 验证 | 命令与结果 |
| --- | --- |
| 导表、构建 | `$env:AI_MODE='1'; & Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`；`dotnet build Godot/GodotProject/GodotProject.csproj --no-restore`：退出0，0错误、5条既有依赖警告 |
| 纯规则 | `dotnet test Tests/BattleTests/BattleTests.csproj`：177/177；`dotnet test Tests/LevelTests/LevelTests.csproj`：19/19；`dotnet test Tests/ProfileTests/ProfileTests.csproj -- xUnit.ParallelizeTestCollections=false`：211/211 |
| 原生装配 | `Godot --path Godot/GodotProject --headless --script res://EditorScripts/validate_enemy_perception.gd`：24场景绑定、矩形/圆边界及专属范围PASS；`validate_enemy_ai_tools.gd`：15个作者/校验脚本解析PASS，退出0且日志无错误 |
| 表格审计 | `python Tools/ProjectMaintenance/audit_config_schema.py`：17页、0错误；`python Tools/LegacyMigration/audit_official_enemy_fidelity.py --output <报告>`：24物种、850格、71攻击，0数值差异、0待修正格 |
| 官方行为与血条 | 隔离工程`--headless --audio-driver Dummy --max-fps 60 -- --smoketest=ui --enemy-fidelity-only / --monster-health-only`：24种双侧索敌/保留目标、6种近战60/120Hz、飞刀1→5秒、13种血条暂停/死亡/复用PASS |
| 物种自主AI | 同上`--sea-ai-only / --garuda-ai-only / --lion-ai-only / --east-sea-ai-only / --shark-ai-only`：距离、选招、冷却、暂停、受击连续性、形态和复用PASS |
| 身体与效果 | `--dragon-only / --bull-only / --enemy-death-only / --effect-shapes-only`：龙王32次左右/难度/频率组合、牛魔王五招/三形态、6种死亡抛飞回收、多形状命中与取消复用PASS |
| 正式整合 | 正式工程隔离APPDATA运行`--smoketest=ai`与`--smoketest=ui --enemy-fidelity-only`：PASS/退出0；AI另查死亡后FSM释放、时钟停止，`--enemy-death-only`检查死亡奖励和尸体回收 |
| 正常窗口 | 正式OpenGL `--smoketest=ui --enemy-hierarchy-only`：落地资格、出招/受击、复用、声音轨道及妖猴左右追击逐渲染帧无举棒闪帧PASS；最终彭魔王60/120Hz自主AI再验证PASS |

证据和应用前备份在`.godot/validation/enemy_ai_design_20261030/`，包括日志、表格预览、Diff、源码审计及逐文件应用清单。导入器经共用作者工具装配新草稿，不自动重写已有场景；跨工程Perception脚本UID经原生重存绑定，最终装配无无效UID警告。现有有效根UID保留，两份鲨鱼场景原来的`uid://<invalid>`由Godot生成有效UID。正式存档未触及，应用前后72项暂存内容的二进制补丁哈希一致，未操作Git索引。

夹具修正：牛妖旧测试在RequestAttack重复检查距离，现检查场景范围，真实动画命中仍保留；狮驼王日志目录补建。首次双引擎并行运行狮驼王冷却断言失败，独立复跑通过，未扩大容差。Profile默认并行有既有Json全局设置竞争，缺字段断言偶发不抛异常；该用例单独及全套串行均通过，未改存档正式逻辑或无关测试。

依赖诊断单列：编辑器退出仍有ComponentInsoector的RefCounted.Free/Invalid call与RID释放报告；运行退出仍有WebRequest对已释放HttpRequest的ObjectDisposed及少量资源泄漏。业务PASS不表示依赖日志已解决。Framework/addons/Tools/Luban未修改；部分dotnet还报告网络受限导致NuGet漏洞元数据无法读取，不影响构建/测试。

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

## 结算声音、被动按钮与沿海地图（2026-10-30）

依据第10/40/50章及用户要求，修正透明点击与完整原点/滤镜，而非用图片包围盒替代官方坐标。第40章“地图不使用固定TextureClickMask”的默认规则与原版透明输入形状不同：浅岸/东海/鲨之涡流使用原生mask并合并当前悬停可见范围，其余地点保留动态alpha命中；规范正文未自行修改。技能图标按用户本轮视觉对齐要求由历史源坐标微调为格内居中，未改字号或字形缩放。正式存档、Git索引、Framework、addons与Luban未改动，生成常量仅经既有Collection入口更新。

引擎用例由 `python Godot/GodotProject/.godot/validation/movement_presets/run_godot.py <用例名> <Godot参数>` 启动，日志位于同目录各用例文件夹；每次使用隔离APPDATA。

| 命令 / 检查 | 当前结果 |
| --- | --- |
| `--rendering-method gl_compatibility --audio-driver WASAPI -- --smoketest=level` | `victory_wasapi_final` PASS、退出0。真实通关后暂停的结算只有一个GGF胜利音乐代理，播放位置0.534→1.033秒；AudioEffectCapture实际采到24064帧、峰值0.586172、RMS 0.134725，Music/Master均0dB。源MP3也经FFmpeg解码确认非静音；Dummy驱动同类采样通过。未发现漏播，不新增第二个播放请求或抬高游戏音量。 |
| Windows CoreAudio只读检查 | 临时 `ui_feedback_20261029/AudioVolumeProbe.cs` 检查默认输出与运行中的Godot会话：系统音量0.120000206、Godot应用音量1，均未静音。12%的系统总音量是已确认的衰减来源；未修改系统设置，不声明完成扬声器听感验收。Computer Use因Node runtime不可启动而未能读取UI，改用只读CoreAudio接口。 |
| 官方被动按钮来源 | `OtherMat_v9.swf`位图569、按钮571。FFDec原生导出的48×26字图与正式normal有效区域逐像素完全一致；共用48×31画布，hover仅向下5px，pressed与normal一致。没有手工调色；满级隐藏按钮与官方PassiveSkill一致。 |
| `--rendering-method gl_compatibility --audio-driver Dummy -- --smoketest=ui --passive-only` | `passive_button_visual_final` PASS、退出0；原生鼠标普通/悬停/按下状态、三张截图、余额不足、25次购买、满级拒绝、属性更新、暂停、磁盘与重入通过。截图已查看。 |
| `python Tools/LegacyMigration/export_official_coastal_map.py --source .../skills-source/official_othermat.swf --java <既有Java> --ffdec <既有FFDec> --work .../ui_feedback_20261029/coastal --output .../Sprites/UI/frontend` | 源SHA-256核对为 `fa7681aad99fb06f50228530b7f70f6334a1d7052a7bb6ef2ce3c3d81867fd5e`；按SelectPlace.s3_1/s3_2及三状态矩阵导出。manifest登记浅岸根(460,373.15)、画布337×312、偏移(-141,-151)；东海根(612.7,418.85)、画布742×265、偏移(-378,-115)。两处19帧逐帧检查可见像素距画布边界至少12px，完整20px悬停光晕保留。 |
| `--headless --editor --script res://EditorScripts/apply_official_coastal_map.gd -- res://.godot/validation/ui_feedback_20261029/coastal/manifest.json` | `coastal_apply_complete`退出0且有AUTHOR PASS；普通状态透明，mask由形状453经原始状态矩阵渲染，hover/current共用注册画布并保持1:1。旧通用导出与登记工具拒绝沿海键，整理工具跳过两处，避免再覆盖原点。 |
| `--rendering-method gl_compatibility --audio-driver Dummy -- --smoketest=frontend --map-coast-only` | `coastal_registered_runtime` PASS、退出0；从官方透明输入区真实移动鼠标，覆盖整轮hover，点击实际进入浅岸7/东海8，再经设置返回地图。两个地点的完整岸线截图已查看，无此前居中缩小与直线截边。 |
| `--rendering-method gl_compatibility --audio-driver Dummy --script res://EditorScripts/validate_map_location_alignment.gd` | `map_origins_visual_checked` PASS、退出0且stderr为空；十地点原点、透明留白、alpha命中、图集尺寸与实际GPU画面通过。普通八地点原像素仅增加12px透明外边，源显示比例保持；shader不把画布外采样钳成边缘线。花果山、鲨之涡流与沿海画面已查看。 |
| `--rendering-method gl_compatibility --audio-driver Dummy -- --smoketest=frontend` | `frontend_feedback_final` PASS、退出0；原有花果山放大区域保持悬停、透明角拒绝输入、地图提示、选角、保存、取消加载与返回回滚通过。 |
| 鲨之涡流完整父变换 | 用户进一步指出字形仍横窄，旧中心/留白审计不足。`coastal_full/manifest.json`现登记原版s3_3父(766.55,500.15)及Y缩放0.5322113，烘入子状态后恢复源图伸长的文字比例；hover画布386×164、偏移(-193,-73)，current保留48帧。旧hover_registered图片及其导入已移除；三个海域共86帧逐帧留白至少12px。 |
| `--smoketest=frontend --map-coast-only`；`validate_map_location_alignment.gd` | 当前 `coastal_all_three_final` 与 `map_shark_visual` 均PASS、退出0；覆盖浅岸、东海、鲨之涡流9的真实点击/悬停/返回，十地点完整画面与原点。鲨之涡流恢复后的文字、椭圆旋涡及两条鲨鱼截图已查看；不再把原生伸长字图整体缩进小按钮。 |
| `align_skill_table_cells.gd`；`capture_skill_table_layout.gd` | `skill_table_icons_centered` / `skill_table_capture_centered`退出0。按原表PNG实际两像素线定位：表头34px、五行内高76/78/79/78/76px，列宽排除分隔线。悟空、唐僧主动与被动三页原生940×590截图已逐页查看；55个文字区域的可见白色墨迹中心最大横偏差1px、纵偏差2px，记录于 `ui_feedback_20261029/skill_layout/ink_alignment.json`。字形保持1:1，无非等比字体拉伸。 |
| `--smoketest=ui --passive-only`；`--smoketest=ui` | 布局修正后的 `skill_grid_passive_runtime`、`skill_grid_full_ui` 均PASS、退出0；实际主动/被动绑定、图标学习、V开关、暂停、钱包、池复用、25次被动购买及完整战斗/UI/磁盘重入通过。 |
| `--headless --editor --script res://EditorScripts/regenerate_collection.gd`；`python Tools/ProjectMaintenance/validate_game_references.py` | 最终 `feedback_all_collection`退出0，既有生成器收集803资源；1801路径、381UID通过。不能只用进程退出0代替Collection完成输出。 |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet`；`python -m unittest discover -s Tests/ToolTests -q` | 最终构建0错误、5个既有框架警告；23项工具测试通过。本轮正式修改为UI与资源，未用未运行的纯规则测试计数充数。 |
| `dotnet run --project Tools/ProjectMaintenance/CSharpStyle --no-restore -- <仓库> .../ui_feedback_20261029/style_final.json`；定向 `git diff --check` | 352手写文件，0格式变化、0缺失文档；本轮修改空白检查通过，只读依赖与规范正文无工作树改动，Git索引保持。 |

失败与边界：首次沿用普通居中缩放虽然点击通过，但截图与用户反馈证实东海位置/大小仍错，已整体替换为官方矩阵导出；FFDec首次导出各状态带不同滤镜边距，先归一到共同原点后才拼图。作者工具带`--quit`会在异步扫描完成前退出，已去掉并确认AUTHOR/Collection完成输出。原点验证初次对浮点数精确比较失败，改为0.01px容差后通过，未放宽定位范围。文字墨迹测量首次把最后一行外叠加的灵魂徽章计入价格，限定到文字实际所在中心区域后修正；三页截图仍逐页人工查看，不以数值审计替代。中途自动审批断连的命令均未计作运行证据。初次成功结算采样与WASAPI回归的玩法PASS后曾在Godot Array终结时0xC0000005退出，最终同一WASAPI回归退出0；保留失败日志。最终完整UI关停仍有既有WebRequest异常、96个ObjectDB/38个CanvasItem/4个资源及纹理存活诊断，编辑器仍有只读ComponentInsoector.Free/RID诊断；本轮不宣称无异常关停。

## 官方反馈与共用墓碑（2026-10-29）

依据第 10/30/40/50 章与用户最新要求：墓碑作为所有英雄共用的独立场景，自有动画、下落和贴地碰撞；英雄身体胶囊在死亡时停用，复活恢复。关卡等待实际墓碑动画结束后结算。第 40 章仍记载地图播放器 0.4 倍速，与本轮恢复官方速度的要求冲突；按用户要求恢复默认 1 倍，规范正文未自行修改。正式玩家存档、Git 索引、框架和插件未改动。

以下引擎日志位于 `Godot/GodotProject/.godot/validation/movement_presets/`，经 `run_godot.py <用例名> <Godot 参数>` 在隔离 APPDATA 中运行；实机使用 OpenGL 与 Dummy 音频。

| 命令 / 检查 | 结果 |
| --- | --- |
| 官方调用链核对 | `Role1.__added` 与 `Role2.__added` 均登记 father 保护，非悟空独有；`BaseAddEffect.updateFather` 在第五次有效直接命中后保护 48 源帧，即 2 秒；命中间隔达到 1 秒重置计数，第五次仍受伤。悟空红光、唐僧蓝光，无双白光优先。共享计时归 HeroModel/HeroSystem，颜色归角色场景，未增加存档字段或未来角色空实现。来源与限定见 OfficialReference。 |
| `--smoketest=frontend` | `feedback_frontend_map_speed` PASS、退出 0；地图恢复 1 倍后按原动画 24fps 播放，普通地点 19/24 秒、鲨之涡流 48/24 秒一轮。悬停、建档/读档、保存、进入关卡及返回回滚通过。 |
| `--smoketest=ui --hurt-protection-only` | 最终 `shared_feedback_hurt_final` PASS、退出 0；60/120Hz 均覆盖第五击触发、第六击无伤、技能无敌重叠、身体/武器两层红光、暂停与到期。最新截图已查看；shader 对图集单格采样，防止相邻姿态串入。 |
| `--headless --editor --script res://EditorScripts/extract_shared_hero_tombstone.gd --quit` | `shared_tombstone_extract` 退出 0；以原生作者工具抽离 78 个时间键、3.25 秒动画，角色库仅保留隐藏/停用身体与死亡声音。图片迁至 `hero_death/hero_tombstone_atlas.png`，与原件 Git blob 散列均为 `c6d6ae9236e2764c5f4bda364e6ea17b92663400`，导入 UID `uid://baqeue3j6frh0` 保留。 |
| `--smoketest=ui --hero-death-only` | `shared_tombstone_runtime` PASS、退出 0；实际花果山平地 x=200、45°坡面 x=1035，各以 60/120Hz 验证贴地、清除上升速度、动画末帧与暂停、死亡音播放、失败结算、重试及返回地图再入。复活恢复英雄胶囊，墓碑隐藏且停止物理推进。坡面截图已查看。 |
| `--script res://.godot/validation/hero_feedback_20261029/probe_tombstone.gd` | 最终 `shared_tombstone_geometry_final` 退出 0；独立墓碑在 0/15/30/40/45°坡面非透明底边与地面的间隙分别为 -0.83/-0.77/-0.52/-0.20/-0.01px，均无悬空；小于 1px 的负值表示轻微压入地面。 |
| `--smoketest=ui` | 最终 `shared_tombstone_full_ui` PASS、退出 0；完整 UI、技能、暂停墓碑、失败重试、模型/实体复用与磁盘重入通过。 |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet`；`dotnet test Tests/ProfileTests --no-restore -v quiet`；BattleTests；LevelTests | 构建 0 错误、5 个既有框架警告；211/225/19 项全部通过。新增规则覆盖断链、无效命中、两英雄共用规则及 24/60/120Hz 计时；测试可反射观察，未增加生产测试 API。NU1900 与测试链接字段/nullable 警告单列。 |
| `--headless --editor --script res://EditorScripts/regenerate_collection.gd --quit`；`python Tools/ProjectMaintenance/validate_game_references.py` | `shared_tombstone_collection_final` 退出 0，经既有生成器收集 798 资源；最终引用检查 1796 路径、381 UID 通过。墓碑图片用 atlas 后缀避免 Collection basename 冲突；旧生产节点/图片路径与临时胶囊锚点已移除。 |
| `dotnet run --project Tools/ProjectMaintenance/CSharpStyle --no-restore -- <仓库> <审计JSON>`；定向 `git diff --check` | `hero_feedback_20261029/style_final.json`：352 个手写文件，0 格式变化、0 缺失文档；本轮文件空白检查通过。只读目录与规范正文无工作树改动，既有索引保留。 |

失败与修正保留：最初依附英雄胶囊的小锚点方案在 120Hz 实际坡面仍会卡住，已整体替换为独立 CharacterBody2D；shader 首版向函数传内置采样器导致诊断，改为单格内直接采样后通过；唐僧测试夹具曾读取尚未实现的成长表，已仅在夹具中提供共享基础数据；直接命中夹具的默认等级造成取整零伤害，已改为同级出招快照。没有为通过测试放宽生产规则。

引擎关停仍有既有只读依赖诊断：编辑器 ComponentInsoector.Free 与 RID/ObjectDB，游戏 WebRequest ObjectDisposedException；最终完整 UI 另报 93 个 ObjectDB、38 个 CanvasItem、3 个资源及纹理存活。业务断言 PASS 和退出 0 不代表无异常日志；本轮没有修改依赖处理这些问题。Dummy 音频验证代理及播放推进，不声明扬声器听感验收。

## 存档、Tooltip 与代码风格收尾（2026-10-29）

| 检查 | 当前结果 |
| --- | --- |
| 第 1 格旧档一次性转存 | 正式 `GameData` 目录 6 个原文件已备份到 `%APPDATA%/Godot/app_userdata/GameZMXY/GameData.backup-before-v11-20261029_114704`，逐文件散列核对通过；只更新 `Data/1791793987.sav` 与 `.bak`，目录摘要和第 2 格保持原字节。正式模型校验输出 `SAVE V11 VALIDATION PASS: version=11, soul=7563, equipment=13, heroes=1, progress=5`；保留经验 8871、灵魂 7563、13 件装备、5 项技能与关卡进度，无兼容代码。 |
| 辅助 Tooltip | 删除 17 处场景文本及地图、存档、战斗菜单、技能页、出售菜单运行时赋值，同步删除作者工具的旧赋值。保留 `EquipmentCell` / `EquipmentTooltip` 自定义详情、样式和原有测试。最终 `style_frontend_final_20261029` 与 `style_ui_final_20261029` 均 PASS、退出 0；递归 Tooltip 断言、保存成功提示、动态装备属性/价格详情和技能页均通过，装备详情与主档背包截图已查看。 |
| C# 风格与文档 | `dotnet run --project Tools/ProjectMaintenance/CSharpStyle --no-restore -- <仓库> <审计JSON>`：覆盖 350 个手写项目文件，排除 Framework、addons、Tools/Luban、生成代码与 `.godot`。Roslyn 验证 token、注释与初始化顺序保持；统一 4 空格、独占大括号、块命名空间和成员顺序。`formal_delivery_audit.json` / `formal_delivery_contracts.json`：0 格式变化、0 XML 参数/返回/泛型契约缺项。23 文件的多行字段缩进已在隔离副本复查并修正，工具重复执行不再累加缩进。 |
| 构建与纯规则 | `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet`：0 错误、5 个既有框架警告；ProfileTests 206、BattleTests 225、LevelTests 19 全部通过；`python -m unittest discover -s Tests/ToolTests -q` 23 项通过。 |
| Godot 扫描 | `python .../movement_presets/run_godot.py style_scan_20261029 --headless --editor --build-solutions --quit --no-window`：退出 0，完成脚本扫描。 |
| 正式主档引擎读入 | 把正式 `GameData` 复制到隔离 APPDATA 后由 GGF 真实启动、读第 1 格、进入花果山并打开背包：`MIGRATED SAVE ENGINE PASS: slot=1; highest=5; soul=7563; level=14`，退出 0。原档未被烟测写入；关停 WebRequest 诊断仍按依赖问题记录。 |
| 引用与 Git | `python Tools/ProjectMaintenance/validate_game_references.py`：1789 路径、379 UID 通过。`git diff --check` / `git diff --cached --check` 的现有空白警告涉及生成 `GameProto` 文件，生成代码与 Git 索引保留。 |

格式与文档审计在 `.godot/validation/code_style_20261029/`；离线转存校验器在 `config_review_20261028/save_v11/`，正式备份的 `manifest.json` / `applied.json` 记录原件及写入结果；OpenGL 回归使用 `movement_presets/style_*_20261029/` 隔离 APPDATA。第 70 章的 v9 描述与当前 v11 实现差异已报告，未自行修改规范。

Godot 编辑器关停仍有只读 `ComponentInsoector` 的 `free` 调用错误；游戏仍有既有 WebRequest `ObjectDisposedException` 与 ObjectDB 存活诊断，最终 UI 为 93 个 ObjectDB、38 个 CanvasItem 和 3 个资源，前端为 5 个 ObjectDB 和 1 个资源；不能称为无异常退出。新增 Tooltip 测试的首次构建因测试命名空间误写失败，已修正为实际 `GameLogic.UI.EquipmentCell`，后续构建通过。主档引擎夹具首次在窗口初始化完成前发出按钮事件而超时，增加信号绑定/初始化等待后通过；失败日志保留，未改正式导航逻辑。

## 配置整理、移动预设与新敌人判定（2026-10-29）

按用户最新决定执行：Enemy公共命名、Walk/Fly/Swim同级身体预设、小怪/BOSS编号区间、原生Excel布尔与可见类型行、关卡专属数值迁为物种`.tres`，以及技能特殊规则归具体状态。第20/30/40章仍有旧表名、无类型行、配方倍率等描述；遵循入口约束，只在ProjectTour/WorkingNotes报告差异，规范正文保留本轮开始时内容。正式存档与Git索引未写入；框架、插件、Luban本体未改。

以下日志与审阅产物位于`.godot/validation/movement_presets/`，实机在隔离APPDATA中运行。

| 命令 / 检查 | 当前结果 |
| --- | --- |
| `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`（`AI_MODE=1`） | 源表和生成物同步；删除专属Luban类型及装备固定Kind字段。两处生成注释从源表修正后生成，未直接修改生成代码 |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | 0错误、5个框架既有弃用警告 |
| `dotnet test Tests/ProfileTests --no-restore -v quiet`；BattleTests；LevelTests | 206/206、225/225、19/19通过；NU1900与测试链接字段/nullable警告单列 |
| `python -m unittest discover -s Tests/ToolTests -v` | 23项通过，含clipDepth遮罩与作用范围到期的回归 |
| `audit_config_schema.py`；`audit_config_readability.py` | 17张业务Sheet所有列类型/中文说明/原生bool存储0错误；11本表24枚举列0英文值、0缺失别名 |
| `node .../movement_presets/verify_all_xlsx.mjs` | 用户已安装XLSX Diff1.6.4实际worker对11本表的普通/公式22次读取全部通过；只从原ZIP局部转存授权单元格及结构 |
| `regenerate_collection.gd`；`validate_game_references.py` | 既有生成器登记Enemy bytes和专属Resource，清理8个已删生成脚本的孤立UID；最终收集795资源；引用审计1789路径/379UID通过 |
| `prepare_enemy_attack_geometry.py --output .../attack_geometry_review.json`；`repair_new_enemy_attack_geometry.gd` | 核对同版Flash探针曲线、透明位图与遮罩；5个库136源帧的基础形状原生作者化。飞行妖改窄斜矩形，潮妖排除未遮罩960×576画布，灯笼鱼用圆排除矩形外角，彭魔王两招/三团火收紧空白；未改帧图、声音、攻击编号或威力 |
| `validate_new_enemy_attack_geometry.gd -- .../attack_geometry_review.json` | `geometry_native_final`退出0；166帧/区域、536次双朝向形状物理查询及普通动作旋转复位通过；火焰0时刻仍有效 |
| `repair_map_location_alignment.gd`；`validate_map_location_alignment.gd` | 10地点普通/悬停画布中心、透明命中及OpenGL截图通过；截图已查看。鲨之涡流源图另有非对称画布，保留源像素在透明画布左侧补15px、上侧补37px对齐涡流原点 |
| `run_runtime_final.ps1` | `final/runtime_results.json`的8项全部退出0并有各自业务PASS：garuda、shark-ai、shark、east-sea、bull、dragon、hero-death、passive；含两频率、双朝向、死亡/回池与25次被动购买/磁盘重入 |
| `run_flows_final.ps1` | `flows/runtime_results.json`的4项全部退出0并有业务PASS：Sea守关/基础数值池复用、花果山、蝙蝠洞、浅岸完整波次/守关/W出口/成功音乐/保存及地图重读 |
| `--smoketest=frontend` | `frontend_delivery`退出0，真实地图悬停/放大轮廓点击、选角、建档/读档、保存提示、关卡导航和放弃回滚通过 |
| `git diff --check`；`git diff --cached --check` | 工作树通过；索引仍有此前已暂存、当前工作树已删除的8个生成文件空白，未修改Git索引或只读生成器模板 |

首次garuda回归失败确实检出作者脚本把火焰首键误放到0.558秒；修正为0时刻后完整8项重跑通过，失败日志保留在`current/garuda/`。生成注释直改和整文件规范还原均被自动审批拦截；随后分别走源表生成与明确的逐处反向补丁，已完成纠正。Godot编辑器关停仍有既有ComponentInsoector.Free/RID诊断，游戏仍有既有WebRequest ObjectDisposedException及ObjectDB存活诊断；AtlasTexture方案曾因GetImage不保留margin导致命中掩码断言失败，现改为透明PNG画布注册并通过；原图可见RGB和全部alpha逐像素不变，失败日志保留。业务PASS与退出0不等于无异常日志。Dummy音频验证GGF代理与播放推进，不声明扬声器听感验收。

## 配置中文化、专属配置与Sea分类（2026-10-28）

依据第10/20/30/50章及用户“所有海怪归Sea，保留原行为”的确认。源表由Artifact Tool作者化，仅转存已审阅单元格/结构并保留其余ZIP部件；生成物经既有Luban/Collection Res入口更新。以下日志位于`.godot/validation/config_review_20261028/`。

| 命令 / 检查 | 当前结果 |
| --- | --- |
| `python Tools/ProjectMaintenance/audit_config_readability.py --report .../readability.after.json` | 11本源表、25个枚举引用列，0英文枚举值、0缺失中文别名、0缺失中文列说明。代码标识、资源路径、框架组名、LegacyId仍保留原技术含义 |
| `author_readability.mjs`；`apply_readability.py`；`check_readability_diff.mjs` | 8本源表726格、3处必要列宽；改前/改后预览已检查，逐格变化与计划一致。XLSX Diff1.6.4实际worker普通/公式模式共16次通过，分别35/1/46/142/214/252/30/6格差异。首次空单元格匹配导致enum校验失败，已从原包重新转存6个别名并复核全部编号未改 |
| `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`（`AI_MODE=1`） | `readability.luban.log`完成schema、数据校验及导出；仅PickupId/PickupEffect的生成注释变化，所有正式bytes与编辑前SHA一致 |
| `author_level_specials.mjs`；`preserve_level_specials.py`；`check_specials_diff.mjs` | 正式删除通用关卡末6列及bean字段，新增花果山/浅岸/蝙蝠洞/隐藏龙宫4个中文专属Sheet与注册。普通/公式模式均通过：LevelConfig763格、beans18格、tables24格；已查看五页目标预览 |
| `author_skill_controls.mjs`；`preserve_skill_controls.py`；`check_final_diff.mjs` | SkillConfig删除J:L三项悟空专属动作规则，其他单元格/Sheet/ZIP部件保留；实际XLSX Diff普通/公式模式均只有39格目标删除，已查看预览。Resources五条Sea路径、Enemy一条旧说明分别只有5/1格变化，两种模式均通过 |
| Luban既有批处理；`--headless --editor --script res://EditorScripts/regenerate_collection.gd` | `level_specials/luban.formal.log`及`sea_resources/luban.log`完成生成；4个具名专属表、Level/Skill/Monster/Entity产物与源表一致。Sea移动57文件携UID，`sea_resources/collection.log`重扫退出0、收集796资源；不手改生成常量 |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | `sea_resources/build.final.log`退出0，0错误/5个既有框架警告。技能字段删除后的首轮失败来自烟测残留诊断引用，已删除，不恢复生产API |
| `dotnet test Tests/ProfileTests --no-restore -v quiet`；BattleTests；LevelTests | 最新203/223/19项全部通过；删除旧倍率/技能表字段镜像测试，保留具体官方时点、专属配置、主线与隐藏进度边界。NU1900和测试链接字段/nullable警告单列 |
| `python -m unittest discover -s Tests/ToolTests -q` | 22项全部通过；包括全源表中文引用、同名枚举别名隔离，以及原有装备/关卡审计 |
| `run_runtime.ps1`，隔离APPDATA，OpenGL、Dummy音频 | 下列9个最终场景全部退出0且有对应业务PASS；日志为`level_specials/*_final.log`、stderr和exit文件，不以通用末尾文案替代专项覆盖 |
| `--smoketest=ui --enemy-hierarchy-only`；`--sea-only` | `hierarchy_final`五地面物种/空中禁招/受击/复用通过；`sea_final`实际覆盖奖励、60/120Hz、双向四招/弹体/暂停/回池通过 |
| `--smoketest=ui --shark-ai-only`；`--shark-only` | `shark_ai_final`垂直追踪、固定转向节拍与速度上限、60/120Hz自主出招通过；`shark_final`首领/召唤物双向两频率招式、无敌/暂停/隐藏警示取消/回池通过 |
| `--smoketest=ui` | `ui_final`完整UI/数值绑定/技能/输入/受击/升级/事务/存档重入通过；包括124个整数源帧取消窗口取样、五招及无双专项 |
| `--smoketest=level`；`--smoketest=level4`；`--smoketest=level7` | `huaguoshan_final`204经验、`bat_cave_final`702经验、`shore_final`3638经验；真实阶段、相机/门、守关目标、W出口、成功结算、自动保存与地图/磁盘重读通过 |
| `--smoketest=ui --dragon-flow-only` | `dragon_flow_final`真实下水、一秒淡出及暂停、GGF转关保留已得收益、首次旗幡拾取、原生死亡/W出口/磁盘检查点/地图通过；另有`dragon_current`两档四招专项通过 |
| `python Tools/ProjectMaintenance/validate_game_references.py`；工作树/索引`diff --check` | 1782路径、382UID及两项空白检查通过；正式资源/导入元数据/工具无旧Ground/sea或Sky/shark_spawn路径引用 |

引擎专项stderr仍有既有WebRequest关停ObjectDisposedException、ObjectDB/RID/资源存活诊断；Collection Res仍出现ComponentInsoector的RefCounted/free关停错误，断言通过和退出0不等于无异常日志。可空奖励Override在死亡事件中保留特殊值，消费时回退基础配置；0概率和None池不作为缺省处理。正式玩家存档、Git索引、框架与插件未修改。

本轮用户要求优先于第20章“仅单一关卡表”及第30/40章“配方倍率”的旧描述，差异已在ProjectTour/WorkingNotes记录，未自行改写规范。以下剩余关卡历史集中收据对应旧指纹，本轮只声明上述受影响范围回归，不刷新历史收据冒充重跑。

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

## 龙王特例归位与通用变体清理（2026-10-28）

依据第00/10/20/30/50章，将仅龙王使用的入场等级选择留在具体实体与状态中。正式存档、Git索引和只读依赖源码未改；生成物只经既有Luban入口更新。下列日志相对`.godot/validation/remaining_levels/`。

| 命令 / 检查 | 结果与边界 |
| --- | --- |
| Artifact作者化`remove_enemy_variants.mjs`；`preserve_removed_variants.py`；`check_removed_variant_package.py` | 移除Enemy两Sheet的AU:BC变体列及bean对应10行；保留其他单元格值、公式、样式和所有非工作表ZIP部件。已检查前后预览；未扩大到其他源表 |
| `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`（`AI_MODE=1`） | `remove_enemy_variants_luban.log`退出0；重建怪物bytes、MonsterConfig，并由生成器删除EnemyLevelVariant；随后清除孤立UID。无手改生成C#或bytes |
| `node .../check_removed_variants.mjs` | `remove_enemy_variants_diff.log`退出0；已安装XLSX Diff1.6.4普通/公式模式共4次通过，Enemy47格、bean31格差异；通用变体标签消失，龙王身份保留 |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | `remove_enemy_variants_build_final.log`退出0，0错误、5个既有框架警告 |
| `dotnet test Tests/ProfileTests --no-restore -v quiet`；`dotnet test Tests/BattleTests --no-restore -v quiet` | `remove_enemy_variants_profile.log`208/208；`remove_enemy_variants_battle_final.log`223/223。新增4个龙王入场概率边界用例，宿主基础概率保持100以拒绝错误地读取通用配置；既有NU1900与测试编译警告单列 |
| `S:/Godot4/Godot4CSharp_console.exe --path Godot/GodotProject --rendering-method gl_compatibility -- --smoketest=ui --dragon-only`，隔离APPDATA | `remove_enemy_variants_dragon_final.log`退出0；实际32组两档×60/120Hz×双向四招、七次冰环命中、2/5秒冰冻、无双免疫、暂停与活动效果回池通过。末尾泛化UI文案不代表运行完整UI生命周期套件 |
| `python Tools/ProjectMaintenance/validate_game_references.py`；`git diff --check`；`git diff --cached --check` | 1782条路径、373个UID通过，两项空白检查通过；生产源码/测试不再引用VariantAttacks、LevelVariants、EnemyLevelVariant或agent.AttackDesire |

最终引擎stderr仍有既有WebRequest关停ObjectDisposedException和5个ObjectDB存活诊断，进程退出0；不把玩法PASS称为无错误日志。此次只完成特殊机制清理，之前的剩余关卡集中验收和历史失败原因不在本次通过范围内。

## 隐藏龙宫接入（2026-10-27，集中重跑前）

依据第10/20/30/40/50/70章；使用隔离APPDATA与Validation存档。正式玩家档案尚未转存，Git索引与只读依赖源码未改。日志相对`.godot/validation/remaining_levels/`。

| 命令 / 检查 | 结果与边界 |
| --- | --- |
| `validate_dragon_art.gd -- <dragon actors manifest> <source_rules.json>` | `dragon_art_timing_verified.log`退出0；315身体帧、六次攻击刷新、三段无敌、独立效果创建时点与冰块绑定通过 |
| `validate_registered_actor_atlas.gd`，正式龙王/冰光/水波/水柱/冰块五组 | `dragon_registered_*.log`均退出0，合计549帧；ADD在有色背景上合成，未烘入黑色底板 |
| `validate_dragon_resources.gd -- <clipped_geometry.json>` | `dragon_native_geometry.log`退出0，424源碰撞帧、关卡/出口/音乐/水下入口绑定通过；clipDepth交集后才求基础矩形，排除隐藏画布 |
| OpenGL `-- --smoketest=ui --dragon-only` | `dragon_verified_runtime.log`退出0；两档×60/120Hz×双向四招32组、冰环七次命中、2/5秒冰冻、无双免疫、暂停与活动水波回池通过 |
| OpenGL `-- --smoketest=ui --dragon-flow-only` | `dragon_flow_final_runtime.log`退出0；真实水面不误触发、S+K下水、一秒淡出可暂停、GGF重入11、保留17魂、首次旗幡拾取、完整死亡后出口、W结算、磁盘与地图通过 |
| OpenGL `-- --smoketest=ui` | `dragon_final_ui.log`已轮询确认PASS、退出0；完整UI生命周期回归。此次新增的死亡盒断言和龙宫全部装备专项仍待集中复跑 |
| Profile/Battle/LevelTests；ToolTests | `dragon_final_profile_tests.log`208/208、`dragon_ai_pure_verified.log`219/219、`dragon_level_tests.log`19/19、工具17/17，合计463 |
| Collection Res；构建；引用/空白；XLSX Diff | `dragon_collection_verified.log`退出0、792资源；`dragon_final_build.log`0错误/5个既有框架警告；1782路径/372UID通过，工作树/索引空白检查通过。7本源表在已安装XLSX Diff普通/公式模式共14次读取通过；部分.before已被作者步骤覆盖，不把0差异当成与改动前相同 |
| 准备的第1格v9→v10档案 | `dragon_save_v10/validation.log`通过正式SaveDataUtility校验、TestGameScope装配和完整集合往返；只改版本与旗幡资格，7847经验、5621魂、37装备、5技能、通关5保留。正式档尚未写入，按第70章等待本份档案授权 |

先前两次隐藏流程失败分别是未指定播放器动画名、夹具早于导航订阅点击结算；失败日志保留，修正后完整重跑通过。既有编辑器插件free调用、游戏关停WebRequest ObjectDisposed与ObjectDB/RID诊断仍存在，不以玩法PASS掩盖。第70章的v9说明与当前v10实现差异已报告，未擅自修改规范。六关当前指纹收据与修复后全组合攻击仍待集中完成。

用户随后明确授权指定第1格的备份转存。`dragon_save_v10/backup_20261027_204823/GameData/`完整保留原目录4个文件，`manifest.json`逐文件SHA校验通过；仅指定主档及其`.bak`更新为准备文件。`applied.json`登记路径/散列，`formal_validation.log`使用旧备份和正式新档运行离线校验器，`SAVE V10 VALIDATION PASS`、退出0，7847经验/5621魂/37装备/5技能/通关5全部保留。首次PowerShell调用File.Replace的空备份路径被拒绝，主档未变；改用同目录覆盖移动成功，未扩大写入范围。

## 全装备补齐与法宝获取（2026-10-27，较早检查点）

依据第00/10/20/30/40/50/70章；正式存档、Git索引和只读依赖源码未改。下述日志均在`.godot/validation/remaining_levels/`。

| 命令 / 检查 | 实际结果与范围 |
| --- | --- |
| Artifact作者化Items/__enums__，原ZIP目标范围转存，既有Luban入口 | 十件官方装备/法宝及Treasure生成成功；原样式和其他ZIP部件保留。`check_final_xlsx.mjs`使用XLSX Diff1.6.4，普通/公式模式分别识别Items226格、枚举33格；命名空间0项需修复 |
| `audit_equipment_appearances.py`、`audit_official_wukong_wardrobe.py` | 38件/38条唯一透明外观；15件悟空装备、13张独立图集身份通过。新三图集各166格，唐僧仍只登记自己的图标/数据 |
| OpenGL `validate_wukong_wardrobe_motion.gd`，输入`final_wardrobe_identity.json` | `final_wardrobe_motion.log`退出0：2158源格、867基础动作层帧、23904全组合播放，0失败；run_01组合图已查看 |
| `repair_official_attack_geometry.gd -- <wukong-skills-export/manifest.json> --new-equipment-only`；`validate_official_wukong_attacks.gd -- <hud_entry_attack_crop/export/manifest.json> --weapon-coverage-only` | 全组合初测5208帧检出新武器漏判定；仅扩展10个有效姿态，时点/事件不变。修正后`final_weapon_coverage.log`退出0，全部7套独立武器双向868帧及刀尖/弧光/远端物理查询通过。修正后的全防具×武器攻击复跑曾误停，不计通过；完整组合动作另由上一行验证 |
| OpenGL `--smoketest=ui --equipment-only` | `final_equipment_runtime.log`退出0：38件地面图/恢复品透明底边贴合、15件跨页穿卸、战斗/预览及动作、两法宝类型/提示/禁用穿戴通过。非末尾泛化文案所列全部UI生命周期 |
| `dotnet run --project Tests/{ProfileTests,BattleTests,LevelTests} --no-restore -- -parallel none`；ToolTests | 199、214、19、17项通过；法宝保存/重建保留实例和属性且不加成，金钢琢整百分比随机、如意金箍棒15%闪避核对 |
| `validate_bull_resources.gd`、`audit_registered_actor_packing.py`、八组`validate_registered_actor_atlas.gd` | `bull_suite_native.log`1324源帧/朝向通过；打包644+83源帧通过；八组正式图集实际渲染均退出0，画布与根坐标一致。心脏光晕/双掌/火柱截图已查看 |
| OpenGL `--smoketest=ui --bull-only` | `bull_suite_runtime.log`退出0：兽形冲撞/停步/反向/暂停、牛妖110px双向攻击、五招60/120Hz、吞噬五次伤害、心脏显隐/火柱清理，金钢琢首次/重复/满包/复用通过 |
| OpenGL `--smoketest=ui --bull-flow-only`、`--smoketest=level10` | `bull_suite_equipment_flow.log`、`bull_suite_level.log`退出0：四件精良实际拾取、穿卸、失败保存、重试/地图；30护卫/三形态、1598经验、自动胜利、唯一音乐、金钢琢检查点与磁盘重读通过。全物品夹具曾排到短关卡外，仅修改测试摆放后重跑 |
| OpenGL `--smoketest=level2` | 首次`bull_suite_portal_regression.log`末波180秒超时；未改生产行为，增加测试诊断后`portal_diagnostic_runtime.log`退出0，五波/312经验、W组合拒绝、出口、胜利和重读通过。原因未确认，保留失败证据 |
| Collection Res、C#构建、引用与空白检查 | 既有生成器退出0；构建0错误/5个既有框架警告；最终1725路径/365UID通过；工作树/索引空白检查通过 |

一次Godot`--headless --editor --build-solutions --quit --no-window`完成扫描后关停挂起，终止自建验证进程，退出-1，不记为冷启动通过。常规生成器和游戏测试正常结束，仍有既有ComponentInsoector释放、游戏退出WebRequest ObjectDisposedException及资源存活诊断，未修改依赖。第40章默认要求出口，本轮官方终关确实无出口，差异在此报告，未自行改规范。9-1龙宫实际场景、资格标记、掉落和返回仍未完成；全关卡最终就绪收据待集中重跑。

## 新增精良装备与全装备实机检查（2026-10-27）

按第10/30/40/50章，正式存档、Git索引、Framework和addons未改。日志相对`Godot/GodotProject/.godot/validation/remaining_levels/`；引擎为`S:/Godot4/Godot4CSharp_console.exe --path Godot/GodotProject`，实机使用OpenGL、隔离APPDATA与Validation存档。

| 命令 / 检查 | 结果与边界 |
| --- | --- |
| `audit_equipment_appearances.py --output <remaining_levels>/equipment_appearances_20261027.json` | 初次正确拒绝4件精良装备缺外观，以及悟空服/棍缺动作绑定；补齐后28件/28条唯一外观通过。透明度检查新增拒绝全不透明背景，未接入龙宫的装备不计在此数量内 |
| `--headless --editor --script res://EditorScripts/register_bull_equipment.gd` | 退出0，`BULL EQUIPMENT PASS`；4套透明图登记，服/棍按AllEquipment共用selector3。`bull_equipment_registration_20261027.log`保留编辑器既有UID/插件关停诊断。首次误用非editor模式已纠正，脚本现明确拒绝该模式 |
| `audit_official_wukong_wardrobe.py`，输入上一轮10份源manifest与当前源表，输出`bull_art/wardrobe_inventory.json` | 12件悟空穿戴身份、10张独立图集（含默认层）通过；12件尚未可玩唐僧装备单列 |
| OpenGL `validate_wukong_wardrobe_motion.gd`，使用`bull_art/wardrobe_inventory.json` | `bull_wardrobe_motion_20261027.log`退出0：1660源格、714基础动作源层样本、49组合双向16268原生播放样本、0失败；跑步组合图已查看。其余参数与下方换装复现命令一致 |
| OpenGL `validate_official_wukong_attacks.gd -- <hud_entry_attack_crop/export/manifest.json>` | `bull_equipment_attacks_20261027.log`退出0，3100普攻样本与真实物理覆盖通过；相同selector不重复图像组合 |
| OpenGL `-- --smoketest=ui --equipment-only` | `all_equipment_runtime_20261027.log`退出0；所有28件装备/恢复品地面贴合、满包/领取/池复用、12件悟空跨页GUI穿卸、战斗/预览/完整动作同步通过。包含原背包分页、tooltip和出售回归，不能据末尾泛化UI汇总文案声称运行了完整UI生命周期套件。仍有既有WebRequest关停异常、59个ObjectDB与6资源存活诊断 |
| `python -m unittest discover -s Tests/ToolTests -v`；`git diff --check` | 17项通过、空白检查通过；就绪收据现在要求`--equipment-only`实际专项，缺少它即拒绝装备完成声明 |
| `--headless --editor --script res://EditorScripts/regenerate_collection.gd` | 退出0，既有生成器收集715项资源，新装备资源常量同步；没有手改生成物 |
| 最终`dotnet build Godot/GodotProject --no-restore -v quiet`；ProfileTests/BattleTests；`validate_game_references.py`；工作树/索引`diff --check` | 构建0错误、5个既有框架警告；Profile 195/195、Battle 214/214；1596条路径、364个UID通过；两项空白检查通过。日志`equipment_build_20261027.log`、`equipment_profile_tests_20261027.log`、`equipment_battle_tests_20261027.log`、`equipment_tools_20261027.log` |

牛魔谷当前实体骨架构建0错误；4-1/9-1没有当前就绪收据，仍需场景/三形态/龙王/完整关卡流程及法宝范围处理。旧3-3等收据已被后续正式代码/工具改动失效，最终全关卡需重新集中验证，不改写历史指纹。

## 牛魔谷正式关卡与地图入口（2026-10-27）

依据第10/30/40/50章，正式存档、Framework、addons、Luban本体未改；运行使用隔离APPDATA和Validation存档。

| 命令 / 检查 | 结果与边界 |
| --- | --- |
| `import_official_bull_actors.py` + `register_bull_actor_resources.gd` | 牛魔王兽形/人形/心脏、牛妖及心脏、双手、火柱、背景分别写入正式角色目录；四场景和火柱池绑定通过。官方角色图集打包审计`REGISTERED PACKING PASS`分别为644、83源帧 |
| `validate_bull_resources.gd -- <bull actors> <bull monsters> <all_levels.json>` | `BULL RESOURCE 4-1 PASS`：1324源帧/双向样本，attack_1/2/3/4/5的原生事件、attack_5五次吞噬、心脏淡入/淡出、3条出生流、2个战斗区域/3道门、无出口和地图19帧绑定通过 |
| OpenGL `-- --smoketest=ui --bull-only` | `SMOKE-BULL ATTACKS 4-1`在60/120Hz均通过；五招双向、五次固定250伤害、英雄捕获/释放、暂停、心脏显隐、双手/火柱独立池清理通过。末尾GGF WebRequest关停异常单列，未计为玩法失败 |
| OpenGL `-- --smoketest=level10` | `SMOKE PASS：关卡 10`；30只牛妖、兽形→人形→心脏完整交接、阶段相机/门、顶部生命条、1598经验、唯一牛魔谷音乐、自动结算、胜利音乐、地图与存档重读通过。正式终关无`World/Exit`，测试不伪造W出口 |
| `export_official_map_hints.py --only niumogu` + `register_official_map_location.gd` + UI生成/构建 | 按`SelectPlace.s4_1`字段确认来源；19帧透明提示、m_Niumogu节点、Ge绑定、动画库和入口构建通过。导出器修复RGB透明扩边模板；10个地点隔离对照显示东海/鲨之涡流重导出，其余图集逐像素保持 |

4-1仍未通过严格就绪门，因为官方直接奖励金钢琢 `jgz` 尚未进入正式法宝数据/存档/表现系统；不把法宝塞入饰品槽。9-1隐藏龙宫仍待正式场景和龙王流程，当前不宣称剩余关卡全部完成。

## 全量装备与鲨之涡流最终检查（2026-10-27）

按第20/30/40/50章使用隔离APPDATA和存档，正式存档、Git索引、Framework、addons与Luban本体未改。日志均相对 `Godot/GodotProject/.godot/validation/remaining_levels/`。3-3在集中检查点通过严格就绪；后续导出工具变化使旧收据不再代表当前指纹，最终六关仍需统一重跑。

| 命令 / 检查 | 结果与边界 |
| --- | --- |
| `run_shark_final.ps1` | 实际进程退出0，`shark_suite.log`包含`SHARK 3-3 FINAL SUITE PASS`；扫描/构建、全部规则测试、场景、动作/特效、装备、真实AI/拾取/流程及引用/表格检查依次通过 |
| `dotnet build Godot/GodotProject --no-restore -v quiet`；Profile/Battle/LevelTests；ToolTests | 集中套件195/214/19/14项通过，构建0错误、5个既有警告；本次依赖工具补充后16项工具测试通过 |
| `validate_sea_level.gd -- all_levels.json 3-3` | 16条流、五个战斗区域、9道门、3个地面、音乐、出口与地图48帧旋转提示通过 |
| `validate_sea_actor_animations.gd`、`audit_registered_actor_packing.py`、OpenGL `validate_registered_actor_atlas.gd` | 754身体动作/朝向样本、569身体/特效源帧打包通过；两类身体与六种独立效果均经过原生绘制比较 |
| OpenGL `--smoketest=ui --shark-only`、`--shark-ai-only`、`--shark-flow-only` | 双频率/双向、高低生命、一次召唤、六类独立效果、死亡/暂停/警示取消/池复用、六件实际掉落拾取/穿卸/失败保存/重试通过；没有新增物理查询回调修改形状错误 |
| OpenGL `--smoketest=level9` | 五波73只、GGF海域音乐、守关血条、真实出口/成功结算、检查点、地图与磁盘重读通过 |
| 全量外观与换装再次复跑 | `equipment_recheck/appearances.log`、`identity.log`、`motion.log`、`attacks.log`均退出0：24件身份/外观、10件悟空装备绑定、10张独立图集（含默认层）、1660源格、11952原生播放样本、3100普攻物理样本通过；跑步和二段跳组合截图已查看 |
| 严格就绪 `audit_remaining_level_readiness.py --level 3-3 --strict` | 集中套件结束时退出0、0个开放门；历史输出`readiness_3-3_verified.json`，之后工具修改已使该指纹失效，没有改写旧收据冒充重跑 |

ProfileTests完整并行运行曾偶发在`LevelProgressTests.InvalidProgress_IsRejected(-1)`的JSON必需字段断言失败；独立类与串行全套通过。测试现用JObject删除字段，仍未消除并行偶发，根因未确认。集中套件使用`dotnet run --project Tests/ProfileTests --no-restore -- -parallel none`，其他C#套件同法，不声称修复了并行问题。完整UI之前对官方负防御/负回血的错误拒绝已修复，`shark_equipment_ui.log`含最终SMOKE PASS；启动时保留固定非概率负属性，概率仍限0..1。GGF关停WebRequest ObjectDisposedException、ObjectDB/资源存活及编辑器既有释放诊断仍单独保留。

## 牛魔谷与隐藏龙宫素材作者化（2026-10-27）

此节只表示隔离素材与作者工具验证，正式Level10/11、实体状态、表格、结算和入口仍待完成。

| 命令 / 检查 | 结果与边界 |
| --- | --- |
| `audit_official_level_dependencies.py --work <remaining_levels>` | 11个官方标签、24类敌人；新增直接构造的Fire与直接赠送jgz依赖，不把工具类Point等当素材。16项工具回归通过 |
| `export_official_final_actors.py ... --group all` | 三形态、牛妖、龙王及独立心脏/手/火/背景/波/冰龙/加法冰光已暂存；心脏保留父级Glow和矩阵，吞噬角色帧选择当前悟空；正式资产未替换 |
| `audit_registered_actor_packing.py --manifest <bull/dragon manifest> ...` | 牛魔王644、牛妖83、龙王514源帧像素与注册点通过；联系图已查看，黑底加法图在预览中按ADD合成 |
| OpenGL `validate_registered_actor_atlas.gd -- <manifest> <name> --staged <review>` | 心脏23、龙王身体315、冰龙68、冰光90实际绘制帧通过，ADD在有色背景上的截图已查看、无黑色矩形。`--staged`验证原始纹理与打包，不替代正式导入/场景/攻击验证 |
| 海怪默认导出回归 | 3类、272源帧与此前`sea_art/actors_registered`的PNG字节、身份、几何、帧事件和结束标记一致；`sea_export_regression_check.log`含PASS |
| `export_official_equipment.py --only <14件新装备> --output <final_equipment>` | 28张透明背包/地面图已暂存，依原文件SymbolClass登记；法宝不误归饰品，悟空新selector仍需完整动作及正式绑定验收 |
| `stage_dragon_wardrobe.ps1`、`review_final_equipment.py` | 玄武甲身体selector5、青龙刀武器selector6、如意金箍棒武器selector5分别完成166格原点保留的素材，共498层格；14件装备图与SymbolClass身份对应且透明，走跑跳/攻击组合联系图已查看。仅暂存，尚未计入正式24件装备审计或关卡就绪 |
| `validate_registered_actor_atlas.gd -- <shark manifest> shark_king_shark_wave <正式frames> <output>` | 新增暂存读取与ADD预览之后，既有正式资源入口51帧绘制比较通过，退出0，日志`final_native_validator_regression.log` |

首次临时原生夹具把JSON数值格号读为`0.0`字符串导致失败，已转为整数后重跑通过；失败日志`dragon_native_torrent.log`保留，不作PASS证据。后续实际通过日志为`dragon_native_dragon_king*.log`。4-1/9-1没有就绪收据，当前未宣称可玩或完成。

## 东海与全部装备续检（2026-10-27）

依据第20/30/40/50章，经正式源表、Luban、Ge、Collection Res和Godot ResourceSaver接入；Framework、addons、正式存档和Git索引未改。日志位于 `.godot/validation/remaining_levels/`，游戏使用隔离APPDATA与Validation存档。

| 命令 / 检查 | 结果与边界 |
| --- | --- |
| 六本源表Artifact作者化、目标范围转存原ZIP，直接Luban命令 | `east_sea_luban.log`退出0；Enemy、Resources、Combat、Items、LevelConfig、枚举与生成物同步，保留非目标内容、样式和默认XML命名空间。血海妖壳为饰品，不增加虚假角色穿戴层 |
| `export_official_sea_monsters.py --only 10 20` → `import_official_sea_monsters.py` → `register_east_sea_resources.gd` | 两类身体245帧、龙波25帧、背击10帧；280帧源像素与注册点比对0失败。鲛魔王图集由5032×7502压为2048×3863，region/margin保持相同虚拟画布与固定原点，16px透明隔离避免边缘污染 |
| OpenGL `validate_registered_actor_atlas.gd`、`validate_sea_actor_animations.gd` | `east_suite_registered_render.log`：158个实际渲染帧与完整源画布精确一致；`east_suite_actor_native.log`：490个动作/朝向样本通过秒制时序、结束标记、矩形判定窗口及嵌套攻击刷新检查 |
| `import_official_sea_level.gd -- <all_levels.json> 3-2 8 zm1_08_east_sea east_sea`、`validate_sea_level.gd -- <all_levels.json> 3-2` | 正式第八关五段24流、69只、九门、七矩形含四单向平台、原图原点/天空/出口与地图绑定通过。地点467为19帧官方光晕；修复透明扩边模板误选位图的问题，避免东海光晕夹带其他UI |
| OpenGL `--smoketest=ui --east-sea-only` | 四招60/120Hz双向实际命中：旋转四段、灯笼鱼两段，龙波生成方向及位置、身体收招、背击1伤害与正面正常规则、身体/独立效果硬直差异、十帧反馈、暂停、完整死亡旋涡及池复用通过。旋转收招标记早于45源帧无敌结束，夹具必须等待实际无敌结束再验背击 |
| OpenGL `--smoketest=ui --east-sea-ai-only` | 双频率自主追击/扑击、远距旋转、近距优先龙波与冷却中普攻、十秒/六秒冷却、45源帧无敌及暂停通过 |
| `audit_equipment_appearances.py`、`audit_official_wukong_wardrobe.py`、OpenGL换装与普攻验证 | 22件装备图标/地面图、9件悟空身份与9张独立图集、1494源格、9960原生动作样本、2480普攻实际判定样本通过；跑步组合、血海妖壳背包、守关条与出口截图已查看 |
| OpenGL `--smoketest=ui --east-sea-flow-only` | 22件物品非透明底部贴地、血海妖壳真实接触拾取、GUI穿卸、生命/防御/回血三项与售价80、失败保存实际实例属性、同关重试、返回地图通过 |
| OpenGL `--smoketest=level8` | 正式地图进入、五波69只、3930经验、灵魂范围3698～7396、GGF海域音乐代理、24000生命守关条、W出口、成功结算、检查点、保存提示与磁盘重读通过；`east_sea_level_flow.log`退出0 |
| `dotnet build`；Profile/Battle/LevelTests；工具测试 | 构建0错误，188/214/19项C#规则与11项工具测试通过；测试使用原生FSM、节点和反射，无新增生产观测API |
| `run_east_sea_final.ps1` → `audit_remaining_level_readiness.py ... --level 3-2 --strict` | 集中套件全部退出0，`east_sea_suite.log`含`EAST SEA 3-2 FINAL SUITE PASS`；当前指纹的东海10项门全部通过，六本XLSX均无需命名空间修复 |

最终集中脚本全部结束后才登记当前指纹的严格就绪收据。早期龙波绑定缺失、夹具未计等级压制、未等完整无敌和实体下一帧回收的失败日志保留，不作PASS证据。退出仍有已登记插件/框架释放与ObjectDB诊断，业务断言和实际退出码分别记录。3-3、4-1、9-1尚未制作完成；历史关卡收据随最终全关卡复跑重新生成。

## 浅岸与全部装备续检（2026-10-27）

依据第20/30/40/50章，使用官方导出、原生ResourceSaver、既有Luban/Ge/Collection流程；Framework、addons、正式存档与Git索引未改。日志位于 `.godot/validation/remaining_levels/`，以下专项的泛化UI汇总文案不代表完整UI套件。

| 命令 / 检查 | 结果与边界 |
| --- | --- |
| `export_official_sea_monsters.py` → `import_official_sea_monsters.py` → `register_sea_actor_resources.gd` | 三物种272动作帧，父级矩阵、嵌套探针、完整注册点及像素比对0失败，联系图已查看。潮汐妖未调用的throwKnife不接入；虾兵独立弹体保留击散语义 |
| 源表Artifact作者化、直接Luban命令（配置目录工作路径） | 四件优秀装备、优秀掉落池、海怪池引用和单人并存6接入；原ZIP的样式、合并与非目标区域保留，默认SpreadsheetML命名空间检查通过；`sea_final_luban.log`退出0。售价40及数值来自AllEquipment/MyEquipObj |
| `register_sea_equipment.gd`、`audit_equipment_appearances.py`、`audit_official_wukong_wardrobe.py` | 21件装备各有独立外观条目、透明背包/地面图，9件悟空身份与9张独立官方selector图集通过；唐僧9件不套用悟空动作 |
| OpenGL `validate_wukong_wardrobe_motion.gd` | `sea_wardrobe_final.log`退出0：1494个源姿态格、561基础动作层样本、全部30组合双向9960原生播放样本，0失败；包含裸身与默认武器。跑步及背包优秀套装截图已查看 |
| OpenGL `validate_official_wukong_attacks.gd`、`repair_official_attack_geometry.gd -- <技能manifest> --new-equipment-only` | 月戟20个有效帧缺少戟刃覆盖，原生几何已局部扩展；最终2480帧/装备/双向真实物理样本通过（`sea_all_weapon_attacks_final.log`，退出0）。差异检查确认113条非几何轨道、收招标记、时长和判定窗口未变；不运行历史全树重建 |
| `import_official_sea_level.gd`、`register_official_map_location.gd -- qianan Location7 m_Qianan 浅岸`、`validate_sea_level.gd -- <all_levels.json>` | 16生成流、9门、9个官方矩形（含6单向平台）、前景/地形原点、出口与地图绑定通过；天空修为官方海域位图17，非七星洞天空。浅岸当前提示保留19个原始光晕帧 |
| OpenGL `--smoketest=ui --sea-only` | `sea_rewards_combat.log`退出0：守关/普通龟丞相交替复用、4800/1600生命、200/75经验、60/30每球灵魂与0/15%装备概率，死亡只通知一次且共享配置不变；四招在60/120Hz双向命中、收招、虾兵实际攻击碰撞击散、淡出、暂停和回池通过 |
| OpenGL `--smoketest=ui --sea-ai-only` | `sea_ai_height.log`退出0：三物种两频率自主追退、选招、决策间隔、潮汐妖10秒冷却、暂停；潮汐妖/龟丞相不攻击高度不可达的目标。龟丞相使用36源帧即1.5秒决策，非统一1秒 |
| OpenGL `--smoketest=ui --sea-flow-only` | `sea_equipment_flow_validated.log`退出0：21件地面图贴地、优秀四件实际接触拾取、GUI穿卸、战斗/预览同步、唐僧限制、失败保存全部实际装备属性、同关重试、返回地图通过。驱动停止在专项期间重复自动导航；随机装备按实际实例属性断言 |
| OpenGL `--smoketest=level7` | `sea_level_flow.log`退出0：正式地图进关、五波67只、3638经验、灵魂范围3468～6936、GGF海域音乐代理、守关4800生命条、W出口及组合拒绝、成功结算、检查点、保存提示和磁盘重读通过。出口画面已查看；规则夹具提高英雄生命但保留真实受伤/击退 |
| `dotnet build Godot/GodotProject --no-restore -v quiet`；Profile/Battle/LevelTests；工具测试 | 构建0错误；188/214/19项C#规则及11项工具测试通过。新增海怪与每实例掉落没有增加测试专用生产API |
| Collection Res、`validate_game_references.py`、`normalize_xlsx_namespaces.py`、`git diff --check` | 644资源重新登记；引用检查开始覆盖生成资源常量，不再漏过GameProto中的旧断链；四本表无需命名空间修复，空白检查通过 |
| `run_sea_final.ps1` → `audit_remaining_level_readiness.py ... --level 3-1 --strict` | 全套集中复跑退出0，`sea_suite.log`包含`SEA 3-1 FINAL SUITE PASS`；当前指纹下浅岸10门全部通过。装备门同时要求身份、完整动作和普攻实际判定覆盖，缺任一命令的收据会被拒绝；不以本关通过代替其余关卡 |

早期`sea_all_weapon_attacks.log`的月戟漏判、`sea_equipment_flow*.log`的测试导航/随机值断言以及`sea_native_validation.log`的“无陷阱组却要求存在”均保留为失败记录，不作通过证据。插件退出仍有已知RefCounted释放、GGF WebRequest ObjectDisposedException与ObjectDB资源诊断；成功退出码与业务断言分别核对。其余3-2、3-3、4-1、9-1未完成；历史2-3收据因全项目指纹变化须在最终统一复跑，不能直接沿用旧严格通过数。

## 第六关与全量装备（2026-10-26）

依据第10/20/30/40/50章，新增内容经源表、既有Luban/节点池/Collection入口及原生资源作者工具接入；没有修改Framework、addons、正式存档或Git索引。日志位于 `.godot/validation/remaining_levels/`，游戏使用隔离APPDATA与 `user://Validation`。

| 命令 / 检查 | 结果与边界 |
| --- | --- |
| 直接 `dotnet Tools/Luban/Luban.dll -t client -c cs-bin -d bin ...`，配置目录工作路径 | 真实退出0、load/validation结束；六本源表和对应产物同步。源表沿用原ZIP样式、列宽、合并及非目标工作表；原有单元格值对照保留。唐僧四个SkillHitId均保留，SpreadsheetML默认命名空间可解析 |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | 0错误；既有5个框架弃用警告及离线NuGet漏洞查询警告 |
| `dotnet test Tests/BattleTests --no-restore -v quiet`（ProfileTests、LevelTests同法）；`python -m unittest discover -s Tests/ToolTests -v` | 分别214、188、19、11项通过；表格断言加入两种新敌人，不在生产代码增加测试接口 |
| `validate_garuda_level.gd -- <all_levels.json>` | 原生装配通过：22个出生点加玩家点、九个门、七个矩形/平台、39尖刺、官方坐标与出口、两类实体、火焰阵营层和三条分离矩形、地图19帧提示 |
| `audit_equipment_appearances.py`、`audit_official_wukong_wardrobe.py`、OpenGL `validate_wukong_wardrobe_motion.gd` | 17件装备各有唯一透明背包/地面图；7件悟空穿戴身份、7张独立图集、1162源格、357基础动作源帧、3984原生播放器样本通过。跑步组合及月戟背包画面已查看；唐僧7件只登记本角色图标，不借用悟空动作 |
| OpenGL `--smoketest=ui --garuda-only` | 五个动作在60/120Hz、双方向均实际命中；火焰淡入期间有伤害、三条火焰独立区域；化卵保留受击体、五次破蛋、七秒复生、暂停、完整死亡旋涡与池复用通过 |
| OpenGL `--smoketest=ui --garuda-ai-only` | 两频率自主选择冲刺/生火/近刺、地空共享冷却、起飞无敌和150px下落、飞行魔自主攻击/三秒退避通过。飞行魔按本实例固定折返间隔，受伤重入不重抽、不重置退避截止时间，超出视野后继续追踪 |
| OpenGL `--smoketest=level6` | 正式地图进入、第六关五波61只、1718经验、灵魂区间、GGF CaveTheme实际代理、守关血条、W组合拒绝、出口、成功结算、检查点、地图保存提示及磁盘重读通过。边界夹具提高本局生命但保留真实受伤/击退，连续地刺用真实双击跑跳通过；短过渡按实际追近距离判定可否居中，未修改相机实现。此前慢走夹具会被连续地刺反复击退并超时，失败日志不计入通过证据 |
| OpenGL `--smoketest=ui --garuda-flow-only` | 正式第六关17件地面图贴地、两件天煞实际接触拾取、背包命令穿卸、战斗/预览同步、官方属性、角色限制、失败保存两件装备及实际属性、重试同关与返回地图通过 |
| `validate_game_references.py`、`git diff --check`、关卡就绪 `--level 2-3 --strict` | 742条路径、321个UID与空白检查通过；最终整套复跑全部退出0，`garuda_suite.log` 含 `GARUDA 2-3 FINAL SUITE PASS`。当前指纹下2-3严格就绪通过，本批六关为1/6；其余五关不具备完整证据 |

原生编辑器扫描仍出现既有ComponentInsoector的RefCounted释放错误；正常游戏退出仍有GGF WebRequest的ObjectDisposedException、ObjectDB诊断，根证书读取错误也保留。上述业务断言与进程退出码分别检查，没有为其扩大插件修改。重复天空贴图的清理被自动审批拒绝（生成资源常量仍引用），当前保留该图。其余五个待接入关卡仍未完成，不能用本关PASS代替。

## 剩余关卡统一就绪审计（2026-10-26）

| 命令 / 检查 | 结果与边界 |
| --- | --- |
| `audit_official_level_dependencies.py` | 复核 v25928 的 11 个场景标签、六关待接入的 22/16/24/16/3/1 条出生流、阶段数量、几何和 24 种直接/脚本敌人依赖；源包 SHA 已核对 |
| `export_official_remaining_level_layers.py` | 六关前景/地形、天空和 bg2/bg3/bg4 已在 `.godot/validation/remaining_levels/art` 暂存；未直接写入正式场景 |
| `audit_remaining_level_readiness.py` | 复核5份当前源包SHA；六关按官方掉落核对正式装备。报告还输出9项 `production_requirements`，并逐关列出官方动作帧数、动态子对象和源声音调用。新增 `playable_equipment_identity_and_motion` 门：2-3当前配方掉落普通行者服和行者棍，必须有当前源表下的悟空 selector 与完整动作播放器证据；未知可穿戴角色无法跳过此门。2-3另缺tsyj/tsyp装备登记，不能用17件现有装备的全局PASS代替新关完整性 |
| `Tests/ToolTests/test_level_readiness.py` | 7项关卡就绪工具测试通过，含可穿戴所有者筛选、未知角色拒绝及动作/selector收据检查。全套 `Tests/ToolTests` 还需 Pillow；当前环境缺该依赖，联系图/图集像素测试未重跑 |
| `audit_remaining_level_readiness.py ... --level 2-3 --strict` | 预期退出1；新关未接入，不能交付。无 `--strict` 的退出0只代表报告生成成功 |
| `python -m py_compile ...`、`python Tools/ProjectMaintenance/validate_game_references.py`、`git diff --check` | 语法、695条序列化路径/309个UID和空白检查通过；工具规则变更会使旧收据失效。本轮不声称新关实机已通过 |

审计输出将 `prerequisites_present`（文件、登记与素材可读取）和 `checks`（对应关卡已验证）分开。音乐文件存在不代表招式声音完成，实体登记不代表动画/判定完成，场景目录存在不代表几何与相机完成。装备从当前源表及资源实时读取，不沿用旧全局报告；原版怪物号通过Enemy.LegacyId关联项目EntityId，不假定两者数值永远相同。可穿戴证据随关卡验证收据检查，收据命令必须同时包含角色身份映射审计和完整动作验证；关卡指纹覆盖 `Tools/LegacyMigration`，工具规则变更会使旧收据失效。

除源包散列外，各门要求 `work/validation/<官方标签>/evidence.json` 中的关卡专属命令记录：顶层 `official_label`、`project_sha256`、`inventory_sha256` 必须匹配本次报告；`checks` 下每门记录 `command`、`exit_code=0`、相对 `log`、`log_sha256` 及包含本关标签和PASS的 `pass_marker`。正式表、脚本、场景或资源改变即使旧记录失效。该记录只防止串关、过期与失败日志被误用，仍须人工核对命令实际覆盖范围；不能用一个泛用PASS证明整关完成。当前六关没有这样的完整记录。`--level` 可重复指定逐关检查，完整交付仍需全六关 `--strict`。

## 装备外观身份与贴图完整性（2026-10-26）

| 命令 / 检查 | 结果与边界 |
| --- | --- |
| `python Tools/LegacyMigration/audit_equipment_appearances.py --repo . --output Godot/GodotProject/.godot/validation/current_equipment_audit/equipment_appearances.json` | `EQUIPMENT APPEARANCE PASS`：17 个 `EquipmentId` 与17个唯一外观条目一一对应；每件装备的背包图、地面图均存在且可读取，透明区域均有可见 alpha；同时检查悟空身体/武器图集在普通动画和技能动画引用的148个帧，当前4套身体与5套武器绑定的有效 alpha 覆盖均与基础层一致 |
| 30 张装备贴图联系图 `equipment_asset_audit_contact.png` | 逐项查看粗糙/普通装备、饰品和家传装备；没有发现黑底、错图或破碎图集。粗糙/普通的四组地面前景像素相同是官方素材事实，背包图仍分别保留，不人为染色 |
| `audit_official_wukong_wardrobe.py` + `validate_wukong_wardrobe_motion.gd` | 7件悟空装备按官方selector、1162个有效姿态与3984个组合双向播放器样本通过。动作校验现从正式外观资源动态枚举全部身份，并按各自manifest核对基础动作、所有组合双朝向；未来新增绑定会自动加入。唐僧装备保持未接入，不借用悟空图集 |
| 本轮最终动态换装检查 | 关卡工具测试7/7，退出0；现有OpenGL原生校验 receipt 覆盖1162完整格、3984真实播放器组合样本且0失败。重看跑步组合图与月戟背包图通过；仍有系统根证书读取错误 |

以后每个新增关卡在登记掉落池前都要先通过装备外观审计；若装备归已可玩角色，再追加该角色的官方动作图集审计。只提取图标或把普通装备图复制到新身份都不能视为完成。

## 章节音乐整理与迦楼罗之穴素材准备（2026-10-26）

| 检查 | 结果与边界 |
| --- | --- |
| `Resources.xlsx` / `__enums__.xlsx` 组织性编辑、源值对照、`normalize_xlsx_namespaces.py --write` | SoundConfig 22个固定ID按菜单地图、第一至第四章、结算、UI、战斗音效集中排列；名称/描述统一，SoundId同序；原有键、路径、分组及其他工作表保持不变。XLSX Diff命名空间可读 |
| Luban `gen_code_bin_to_project_lazyload.bat` | 退出0；SoundId和SoundConfig bytes重新生成 |
| `register_official_chapter_music.gd` | 三首官方MP3循环导入通过；第四、第五关只改 `m_MusicId 102→106`，逐节点/属性对照确认无场景补丁扩散 |
| OpenGL `--smoketest=ui --chapter-music-only` | `SMOKE-CHAPTER-MUSIC PASS`：Cave/Sea/BullDemon三首实际GGF Music代理起播、唯一代理、播放推进、战斗暂停继续和请求停止；`SMOKE-CHAPTER-SCENES PASS`：两座洞窟选择CaveTheme。退出时仍有既知框架ObjectDisposedException/ObjectDB诊断，业务断言通过 |
| `export_official_garuda_cave.py` | 退出0；Monster2_v4：Monster9彭魔王11动作223帧、Monster15飞刀魔5动作85帧、Boss4Bullet1嵌套火焰15帧、Monster1共享BossDead 12帧；源矩阵、探针和帧脚本保留，输出只在 `.godot/validation/remaining_levels/garuda_art` |
| `garuda_art/source_contact.png` | 已查看联系图，角色、火焰和首领死亡旋涡素材完整；BossDead并非英雄墓碑，没有把未作者化素材写入正式Entity或场景 |
| `prepare_official_garuda_level.py` | 退出0；生成22流/5阶段/4种敌人的 `level_plan.json`，纠正实体表外键读取后确认仅 Monster9 与 Monster15 的 EntityId/EntityConfig/场景仍缺；不修改正式表 |
| 扩展后的同一准备工具、`garuda_art/check_packing.py` | 均退出0；`native_manifest.json` 保留各动作受击盒、父级完整变换、嵌套探针帧、刷新/生火/结束源帧，并打包四张隔离图集。335个源帧可见像素及注册点逐项一致，`packing_report.json`；联系图 `native_contact.png` 已查看。死亡旋涡的空白末帧不再扩大所有格，格宽从945降至167，未删可见像素；最终玩法判定仍待作者化 |

本轮源码确认：飞行魔攻击第10帧重建攻击编号，第20帧收招；彭魔王火焰的0.6秒淡入只暂停子动画，BaseBullet仍每帧调用checkAttack，不能自行改成无伤害预警。天煞月戟为战士武器selector4，天煞羽袍为法师防具selector6，完整构造数据及源SHA已加入level_plan。正式实体、装备表、关卡和新关实机验证仍未完成。


## 换装基础动作修复与剩余关卡准备（2026-10-26）

依据第10/30/40/50章：身体/武器继续独立，保留统一原点、网格、动画及UID。猿甲与宝剑的120..170格此前误用了行者服/棍；本次各自按官方selector4/7导出，0..119格及末尾空格与修复前逐字节相同，其余四张图不变。旧“306源层帧通过”使用了错误的装备来源映射，由本节校验替代。

| 命令 / 检查 | 结果与边界 |
| --- | --- |
| `export_official_wukong_equipment.py --kind body --selector 4 ...`、`--kind weapon --selector 7 ...` → `import_official_wukong_equipment.gd` | 两次均退出0；各166个有效姿态含51个完整基础动作，6×29网格；既有115姿态不变。输出与manifest在 `.godot/validation/wardrobe_motion_fix/{earth_fiend,heirloom}/` |
| 新 `validate_wukong_wardrobe_motion.gd` 修复前运行 | 退出1；306源层帧中准确检出猿甲与宝剑的102格错误，其他204格与各自原文件一致。未沿用旧的跨装备来源别名 |
| 同脚本OpenGL修复后运行 | 退出0，306个源层帧及其实际导入纹理、918个真实播放器样本通过；3身体×3武器×双向×51帧，另输出5张组合截图。跑步、二段跳对照已查看；`motion_after.log`、`captures/report.json` |
| `audit_official_wukong_wardrobe.py` + 扩展后的同一OpenGL校验 | 退出0；从Items、枚举、AllEquipment和原生外观资源核对全部6件悟空武器/防具、六张独立图集的996个有效格与导入纹理；遍历基础、待机、五段普攻、五技能和死亡/RESET的实际帧键，九种组合双向共2988样本通过。6件尚未可玩唐僧的装备单列，不冒充已完成换装；`.godot/validation/wardrobe_complete_audit/{inventory.json,complete.log,captures/report.json}` |
| OpenGL `--smoketest=ui --inventory-skills-only` | 业务断言及退出码通过，捕获正常离关注销未订阅死亡动画信号的错误；已按现有Playing阶段修复，未新增订阅包装或观测接口。`inventory_skills.log` |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | 退出0、0错误、5个既有框架弃用警告；`build.log` |
| OpenGL `--smoketest=ui --terrain-level5` 修复后 | 退出0、SMOKE PASS；含真实背包穿卸、全部动作采样、技能、复用、死亡墓碑暂停/完成、结算、重试、返回、失败清理和读档。原注销错误为0；坡面限第五关。HUD缺绑定错误为主动失败注入；`ui_final.log` |
| `python Tools/ProjectMaintenance/validate_game_references.py`、`git diff --check`、三个本轮Python工具的 `py_compile` | 693路径、308UID及语法/空白检查通过 |
| `audit_official_level_dependencies.py --work Godot/GodotProject/.godot/validation/remaining_levels` | 退出0；全11个场景标签、24种直接及脚本召唤敌人、六类源表快照；本轮重跑包含可达链接脚本。动态对象与活动条件仍需人工复核 |
| `render_official_level_reference.py --levels 2-3 3-1 3-2 3-3 4-1 9-1`、`export_official_remaining_level_layers.py ...` | 先前隔离导出均退出0；本轮查看六关整幅缩略，前景/地形无主体破碎缺块。12张分层PNG、3张天空、3首MP3仍只在staging；新关卡未接入、未声称可玩验证通过 |

换装专项可重跑（需已生成的官方源向量目录）：

```powershell
$wardrobeWork = (Resolve-Path 'Godot/GodotProject/.godot/validation/wardrobe_motion_fix').Path
$wardrobeSource = (Resolve-Path 'Godot/GodotProject/.godot/validation/action_origin_review/full_wukong_export').Path
$wardrobeAll = (Resolve-Path 'Godot/GodotProject/.godot/validation/wardrobe_complete_audit').Path
$env:APPDATA = "$wardrobeWork/earth_fiend/profile"
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility `
  --script res://EditorScripts/validate_wukong_wardrobe_motion.gd -- $wardrobeSource `
  "$wardrobeWork/earth_fiend/manifest.json" "$wardrobeWork/heirloom/manifest.json" "$wardrobeAll/captures" `
  "$wardrobeAll/inventory.json"
```

全量检查没有再发现串用、空格或未覆盖动作。四张早期图集的10个技能格来自原800px画布；200px重新栅格化时仅有1～5/255的抗锯齿差异。校验分别从同一官方SVG精确重建两种既有画布，并要求其中一种逐像素相等，没有放宽颜色容差、重写这些素材或把差异当成装备缺失。后续新增悟空装备须将其独立导出manifest传给身份审计器；缺少正式表绑定、官方selector、源格或动画帧覆盖时直接失败。

日志默认位于 `.godot/validation/wardrobe_motion_fix/`。原生重导入已完成两张贴图及新脚本扫描，但编辑器关停触发既有ComponentInsoector释放错误及GC `0xC0000005`，该次导入命令退出非0（`import.log`），不能算无错误扫描；后续独立加载、像素/渲染及完整UI均退出0。系统根证书读取、GGF WebRequest关停ObjectDisposedException与8个ObjectDB泄漏仍存在，未修改只读依赖。正式玩家存档和Git索引未动。

## 当前动作核对结果（2026-10-26，最终注册点）

本节替代下方早期火魔原点与地面怪帧数结果。依据第10/30/40/50章；没有修改Framework、插件、正式存档或Git索引。日志除节点装配外均在 `.godot/validation/current_actor_fidelity/`，运行使用隔离APPDATA和Validation存档。

### 本轮追加：源帧结束、死亡运动与结算同步

| 检查 | 结果 |
| --- | --- |
| `export_official_body_events.py` + `repair_official_body_events.gd` | 从 Role/Monster1/Monster2 的帧脚本核出42个动作，40个实际结束点和6个源帧停止运动点；原生动画库写入 `complete` / `air_loop` 标记，未重写贴图、声音或判定轨道。`body_events.json`、`body_events_author_final.log` |
| `validate_official_body_events.gd` | PASS、退出0；40个真实播放器结束标记、6个 C# `StopMotionAnimEvent`、3个第9帧尸体循环及下一动作分段复位 |
| `dotnet test Tests/BattleTests` | 214/214；新增致死击退轨迹和空中死亡循环，保持24/60/120Hz受击轨迹 |
| OpenGL `--smoketest=ui --enemy-death-only` | PASS、退出0；三类小怪×60/120Hz致死抛飞、上升约315px、空中循环、落地回收与一次死亡收益事件 |
| OpenGL `--smoketest=ui --inventory-skills-only` | PASS、退出0；124个悟空整数源帧取消样本、突刺四段独立命中、五招、音效、技能窗口与复用 |
| OpenGL `--smoketest=bosses` / `--smoketest=ui --qixing-only` | PASS、退出0；BOSS/七星洞招式和受击行为未受结束点改动影响 |
| OpenGL `--smoketest=ui --terrain-level5` | PASS、退出0；完整 UI 生命周期、暂停墓碑不提前结算、实际死亡动画结束后失败页、重试/返回地图及六座坡面路线 |

这批源脚本核对还确认：妖猴/大猩猩的 `afterHurt` 只是时间轴标签，没有实际调用方；运行时不建立该额外状态。地面怪死亡在空中会保留致死击退并循环官方死亡后半段，避免固定动画时长提前消失。失败结算由 `LevelViewController` 订阅悟空 `AnimationFinished`，不再使用独立的78/24秒计时器；旧日志中的死亡结算超时已由这项竞态修复覆盖。

最后一轮复核：`--fire-motion-only` 20组仍PASS，`--inventory-skills-only` 在新增整数帧窗口检查下PASS，敌人1239层源像素与注册点PASS；C#构建0错误，Battle214、Profile188、Level19通过。系统根证书、GGF WebRequest关停、插件RefCounted/ObjectDB泄漏仍是环境诊断，业务断言与退出码已分开记录。

| 命令 / 检查 | 结果与边界 |
| --- | --- |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | 0错误、5个既有框架弃用警告；`build_collection_final.log` |
| 三组 `dotnet test Tests/<项目> --no-restore -v quiet` | Battle210、Profile188、Level19通过；本轮Profile包含突刺四段独立攻击，Level19通过 |
| 生产节点查询扫描、实际场景绑定 | 固定节点字面量查询为0；关卡标记NodePath和动画根推导保留。48场景409处绑定通过，`bindings_final.log` |
| OpenGL `--smoketest=ui --fire-motion-only` | 20组PASS、退出0；60/120Hz×双向×五种目标边界，起手、搜敌、自然推进/无目标跳段、下冲、真实根落点、无反弹、扣蓝、命中及收招；`fire_motion_final.log`。锚点容差只允许两侧SafeMargin内约0.135px的物理接触恢复，另独立要求Origin为零 |
| OpenGL `validate_official_wukong_attacks.gd` | 1116样本PASS、退出0；三身体×三武器×双向×62源帧，弧光/残留帧、近端和范围外实际Area检查；`attacks_final.log` |
| OpenGL `validate_official_wukong_skills.gd` | 976样本PASS、退出0；五招本体和四个独立效果的源帧、注册、实际Area检查；`skills_render_after.log`。前奏已并入角色，不计为第五个池化效果 |
| OpenGL `validate_official_wukong_idle.gd` | 400源层帧、100背包帧和四组合双向绘制PASS、退出0；`idle_final3.log`。最初只有每轮首帧失败，原因是夹具重复Seek同一循环未Stop，修复夹具后通过，未重写图集 |
| `validate_official_enemy_frames.gd` 加四份源manifest | 1239完整身体/特效层帧PASS、退出0；涵盖当前全部10种敌人和BOSS，精确比较原始RGBA及真实AnimationPlayer取样原点；`body_enemy_pixels.log`。移除没有运行时调用方的after_hurt后重新核对，没有放宽像素比较 |
| `audit_actor_registration.gd` 加 `action_origin_review/source_inventory.json` | 11实体身体/受击盒源坐标、完整动作时长PASS、退出0；`actor_roots_final.log` |
| `validate_anim_libraries.gd` / `validate_collision_geometry.gd` | PASS、退出0；11角色库方法绑定与状态轨道、50场景239基础形状节点及六座整体坡体；死亡终态专用轨道由death/RESET及OnShow复用检查负责。`libraries_final2.log`、`collision_final.log` |
| `validate_official_attack_geometry.gd` 加技能manifest | PASS、退出0；40技能源探针角、42九连有效帧、下冲/落地源偏移、588九连点查询及2260毒雾点查询。禺狨王/飞刀魔的既有人工矩形作为回归基线，未称为官方像素判定、未重写正式几何；`geometry_final2.log` |
| OpenGL `--smoketest` / `--smoketest=ai` | PASS、退出0；33次英雄动作切换、第四段松手不补第五段，以及敌人完整AI/受控/死亡回收；`hero_final.log`、`ai_final.log` |
| OpenGL `--smoketest=bosses` / `--smoketest=ui --qixing-only` | PASS、退出0；两位BOSS五招、七星洞六招双向、风刃/毒雾、无敌、AI与回池；`bosses_final.log`、`qixing_final.log` |
| OpenGL `--smoketest=ui`，未加子集参数 | 完整PASS、退出0；真实学习/施放、突刺同一沙包四次独立命中、三类怪各自官方前摇、妖猴走攻切换、无双/换装/受击/死亡/复用/存档与结算，全六座坡体48条60/120Hz双向走跑停跳路线；`ui_full_final2.log`。首轮旧前摇断言失败保留在 `ui_full_final.log`，未修改生产时序去迎合测试 |
| 既有 `regenerate_collection.gd` / 资源引用 / 空白检查 | 原生成器收集584项，含六个新增地面怪图集/帧资源，退出0；未手改生成常量。`validate_game_references.py` 的693路径、308UID与 `git diff --check` 通过 |

重跑源帧比较：

```powershell
& 'S:/Godot4/Godot4CSharp_console.exe' --headless --path Godot/GodotProject `
  --script res://EditorScripts/validate_official_enemy_frames.gd -- `
  res://.godot/validation/action_origin_review/ground_monsters_export/manifest.json `
  res://.godot/validation/stages_2_3/bosses/manifest.json `
  res://.godot/validation/official_continuation/assets/manifest.json `
  res://.godot/validation/official_continuation/qixing/manifest.json
```

实际起手/升空/下冲/落地截图及汇总 `fire_contact_final.png` 已查看。源帧一致、游戏命中与视觉近似是不同证据；本轮没有宣称Godot矩形与Flash像素相交算法完全等同。`repair_official_attack_geometry.gd` 的全量历史重建段仍含旧敌人近似，不用于覆盖当前人工资源；三种新地面怪由自己的原生导入器管理。

已知诊断仍单独保留：系统根证书读取失败、GGF WebRequest关停ObjectDisposedException、插件Free/RefCounted与RID/ObjectDB泄漏。地面怪重导入保存成功后曾在编辑器关停出现GC `0xC0000005`（`ground_reimport.log`），退出非0；后续独立1239帧加载和完整UI均退出0，不能把该导入过程写成无错误退出。HUD缺绑定为UI主动失败注入。迁移到忽略目录的 `misplaced_appdata_20261026` 仅是之前相对APPDATA误落进资源树的测试档与日志，没有删除存档。

## C# 代码风格规范化（2026-10-25）

依据用户确认的 GGF/UGF 风格，对手写业务、主流程、作者工具和测试夹具 C# 统一四空格缩进、独占大括号、普通块体和多行 XML 文档。生成的 `GameProto`、Framework、插件和其他只读目录未改动。

| 检查 | 结果 |
| --- | --- |
| `dotnet format Godot/GodotProject/GodotProject.csproj --no-restore --verify-no-changes --severity warn --diagnostics IDE0021 IDE0022 IDE0027 IDE0055 --include TheGame/GameScripts TheGame/MainPack/Scripts EditorScripts` | PASS；245 份业务/主流程/作者工具 C# 的表达式主体改为普通块体；`[ExportToolButton] SyncChildEntries` 按 Godot `GD0111` 要求保留表达式属性 |
| `dotnet format` 三个测试项目的代码风格诊断 | PASS；53 份测试夹具没有表达式方法/属性主体；链接的 Framework `ByteBuf` 既有 IDE0025/行尾告警按只读边界排除 |
| XML 文档结构扫描 | PASS；298 份可维护 C# 中单行 `<summary>` 为 0，未发现未闭合的 `param`/`typeparam`/`returns` 标签；生成文档构建对业务、作者工具和测试代码无 XML 错误 |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | PASS；0 错误、0 警告 |
| 三组 `dotnet test ... --no-restore -v quiet` | PASS；Battle 209、Profile 188、Level 19，共 416 项 |
| `git diff --check` | PASS |

## 火魔斩根坐标与地面怪官方动作（2026-10-26）

本批先修复官方坐标语义，再迁移三类地面小怪的完整源时间轴。官方 Role1 的火魔前奏、搜敌、下冲和落地都直接使用角色根 `x/y`；移除此前悟空 `(0,28)` 技能动作原点后，独立特效与实体根重合，目标边界也按官方 130px 距离重新核对。

| 检查 | 结果与证据 |
| --- | --- |
| OpenGL `--smoketest=ui --fire-motion-only` | `SMOKE-FIRE-MOTION PASS`、退出 0；60/120Hz、左右方向、无目标/中心/上下边界/猴子目标共 20 组，升空、下冲、落地、无二次上升、一次耗蓝、命中与收招通过；`action_origin_review/fire_root_coordinate_diagnose2.log` |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | 0 错误、5 个框架既有弃用警告；`action_origin_review/ground_monsters_build2.log` |
| 官方地面怪导出/导入 | `Monster1` SHA 校验通过；花果山猴、妖猴、大猩猩分别导入 69/78/85 个源帧，攻击 stick 判定随官方帧轨道写入原生动画资源；`ground_monsters_export/manifest.json`、`ground_monsters_import5.log` |
| OpenGL `--smoketest=ai` | `SMOKE PASS`、退出 0；猴子巡逻、追击、攻击、受击、死亡回池及官方攻击范围通过；`ground_monsters_ai3.log` |
| OpenGL `--smoketest=ui --qixing-only` / `--smoketest=bosses` | `SMOKE-QIXING PASS`、`SMOKE-BOSSES PASS`；七星洞六招双向、两位 BOSS 原生招式/无敌/独立效果/回池通过；`ground_monsters_qixing.log`、`ground_monsters_bosses.log` |

补充修正：蝙蝠官方受击动作是 13 帧，正式动画长度已从错误的 `0.5s` 改为 `13/24s`；其余当前蝙蝠动作长度与 v25928 资源清单一致。完整蝙蝠逐帧像素叠合仍待单独补做。

编辑器导入过程仍有已知 ComponentInsoector 释放诊断，未修改只读插件；蝙蝠及所有已接入演员的完整源帧逐像素比对仍未完成。

## 固定节点引用与场景装配（2026-10-25）

依据第 10/40/50 章，悟空墓碑、两种技能页、战斗技能栏与六格存档改为导出节点引用；SaveForm 绑定经既有 Ge 工具生成。没有新增行/格脚本，场景布局、动画与判定轨道不变。日志位于 `.godot/validation/node_bindings/`，APPDATA 和测试存档按运行隔离。

| 命令 / 检查 | 结果与证据 |
| --- | --- |
| `python Tools/ProjectMaintenance/regenerate_ui_form.py Godot/GodotProject/TheGame/UIs/save_form.tscn --namespace GameLogic.UI` | 生成 20 个根节点绑定；未手改生成代码 |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` | PASS、0 错误、5 个既有框架弃用警告；`build.log` |
| Godot `--headless --script <node_bindings/validate_bindings.gd>` | PASS、退出 0；80 个绑定逐项核对到目标节点，包括两个角色十行主动、五行被动、HUD 五图标和六格名称/日期；`bindings.log` |
| Godot 独立线程加载技能场景 | PASS、退出 0；实际线程加载并实例化后十项名称绑定有效 |
| OpenGL `--smoketest=frontend` | PASS、退出 0；六格目录、建档/读档、关闭复用、地图保存提示及失败注入回滚；`frontend.log` / `.exit` |
| OpenGL `--smoketest=ui --inventory-skills-only` | PASS、退出 0；主动/被动全部数据覆盖、学习与 HUD 图标同步、V 开关、钱包绑定/退订/池复用、背包穿脱及五招命中和 GGF 音效；`ui_inventory_skills.log` / `.exit` |
| OpenGL `--smoketest=ui --terrain-level5` | 最终 PASS、退出 0；完整 UI 生命周期、无双、英雄与敌人复用、七星洞八条坡面路线、穿戴后进关、死亡掉落立即清理、墓碑后结算、重试/返回地图、存档重读和 HUD 失败清理；`ui.log` / `.exit`。该参数仅将坡面范围限制到第五关，其余 UI 流程完整运行 |
| 生产节点路径审计 / `validate_game_references.py` / 本轮 `git diff --check` | 固定 `GetNode("...")` 查询为 0；只保留关卡资源配置及原生动画根路径的动态查询；687 条序列化资源路径、308 个 UID 和空白检查通过 |

本轮复核了 `TheGame/GameScripts` 与 `MainPack/Scripts/UI` 的全部 C#：悟空的武器和墓碑均由 `[Export]` 字段绑定，`WukongEntity.OnInit` 会在初始化边界检查两项专属表现绑定；未发现正式代码中按字符串查找固定节点。`AbstractLevelMarkersNode` 读取的是场景资源中的 `NodePath`，`AbstractEnemyEntity.Attacks` 读取的是 `AnimationPlayer.RootNode` 计算动画轨道路径，两者都不是业务节点名硬编码。

最新只读复查：业务与主包流程/UI 的固定节点字面量扫描仍为0；`python Tools/ProjectMaintenance/validate_game_references.py` 退出0，693条路径、308个UID通过；`git diff --check` 与 `git diff --cached --check` 均退出0。本次没有补改节点逻辑、重新构建或运行游戏，以下构建与场景实例化结果属于此前验证。

2026-10-26 节点复查（节点逻辑无需补改）：业务、主包与作者工具的固定节点字符串查询无遗漏。`dotnet build Godot/GodotProject --no-restore -v quiet` 退出 0、0 错误、5 个既有框架弃用警告；`python Tools/ProjectMaintenance/validate_game_references.py` 通过 687 条资源路径和 308 个 UID；`git diff --check` 通过。以隔离 APPDATA 运行 `S:/Godot4/Godot4CSharp_console.exe --headless --path Godot/GodotProject --script res://.godot/validation/node_reference_audit_20261026/check_bindings.gd`，实际实例化全部 48 个业务实体/UI/关卡场景，逐项比较序列化路径与 C# 节点字段及数组，409 处引用通过，悟空武器与墓碑另核对为 `Sprite2D`，退出 0；火魔原点修正后同样通过，日志为同目录 `bindings.log` / `bindings_after.log`。仍报告既有系统根证书读取错误，不称为无错误运行。

## 节点绑定与火魔斩朝向复核（2026-10-25）

2026-10-26 补查火魔落地注册点：按官方 Role1、BaseBullet 与导出源坐标，修正共同+28Y原点被二次缩放的问题；普通/无双播放映射本来正确，没有互换。下列新日志位于 `.godot/validation/node_reference_audit_20261026/`；没有改动用户已有的悟空主动画库。

| 命令 / 检查 | 新结果 |
| --- | --- |
| Godot `--headless --script res://EditorScripts/repair_fire_demon_registration.gd -- res://.godot/validation/wukong-skills-export/manifest.json` | 原生资源修正成功，位置与判定一起调整；根、依赖UID和源PNG不变；10个非空落地源帧与正式图集逐像素一致；`fire_registration.log` |
| OpenGL `--script res://.godot/validation/node_reference_audit_20261026/render_fire_landing.gd` | PASS、退出0；13源帧×双朝向×普通/无双，共52组独立源坐标与判定中心断言，8张渲染；`fire_landing_after.log`、`fire_landing.png` 已查看。单独渲染只在内存副本禁用方法轨道，声音/结束仍由真实游戏专项执行 |
| OpenGL `-- --smoketest=ui --fire-motion-only` | 修正前后各8组PASS、退出0；60/120Hz、双向、有/无目标、落地锚点、命中、单次耗蓝及收招通过；实测升空速度±480/-1200、下冲±720/480px/s，与24fps源值换算一致。`fire_motion_after.log` / `.exit`、`fire_motion_summary.json`；不是完整UI回归 |
| Godot `--headless --script res://EditorScripts/validate_anim_libraries.gd` | 退出1：悟空普通动作缺少墓碑专属的4个值轨道；这些轨道当前只在death/RESET，复用由OnShow复位。本次未修改该主动画库，未将此项计作PASS；`animation_libraries.log` |

独立渲染最初的夹具未屏蔽声音方法，因没有启动框架而报空引用；已仅在夹具的动画副本禁用方法轨道后重跑。编辑器作者化的一次运行退出0但扫描被中止，并报告已知ComponentInsoector释放与RID/ObjectDB关停诊断，不作为无错误扫描。正式实机仍报告已知根证书读取、WebRequest关停异常与6个ObjectDB泄漏；未改只读依赖。

继续核对时发现旋转落地矩形放在 `(1.4,2)/(2.1,3)` 不等比缩放层下会产生剪切，导致视觉框外额外命中；已按每个源阶段改为轴对齐矩形、关闭该效果的旋转轨道。40 个双朝向/普通无双边缘查询通过，`landing_edges_after.log`。技能完整 OpenGL 复核通过：976 个身体/武器源帧、五个独立效果双向、实际 Area2D 命中与范围外排除，退出 0，`all_skills_render_after.log`。这一修复保留官方时序和贴图，仅改变适用于 Godot 不等比缩放的基础判定表达。

11 个当前角色/怪物场景按三份 SHA 校验后的 Role/Monster SWF 重新解码，源受击盒尺寸和动作注册逐一比对；技能定位使用实体根坐标，与官方直接使用角色 x/y 的语义一致。原始数据和当前值在 `.godot/validation/action_origin_review/source_inventory.json`、`current_actors.json`、`origin_comparison.json`；`SOURCE INVENTORY` 与 `FRESH SOURCE ORIGIN PASS` 通过。悟空移动动画同时恢复官方 24fps 的走/跑周期：run=8/24秒、walk=16/24秒；以前分别是0.291667和0.8秒，造成慢走/停步手感偏差。构建 0错误，BattleTests 209/209；ProfileTests 188/188、LevelTests 19/19 仍通过此前结果。其余怪物的动作图像逐帧内容和专属攻击还需要继续按同一源清单核对，不能由本节的原点/时长检查替代。

2026-10-26 又从同一 Role_v7 的完整子时间轴导出悟空基础动作：walk 440/16帧、run 452/8帧、jump 533/1帧、jump_2 539/10帧、fall 543/1帧、hurt 553/15帧。追加 51 个正式 200px 图集格到身体/武器六套外观，更新 AnimationLibrary 的帧轨道和 24fps 时长；身体、武器、装备预览的 vframes 同步到 29 行。306 个源层样本逐像素比较通过，`validate_full_base_actions.gd`；悟空基础动作实时烟测 33 次状态切换通过，`normal_input_after_full_base3.log`。这次修复消除了此前用压缩动作格替代完整源时间轴的问题。攻击五段和五项技能继续使用原有已核验源格，分别由 `OFFICIAL-ATTACKS` 和 `OFFICIAL-SKILLS` 检查。

| 命令 / 检查 | 结果与证据 |
| --- | --- |
| `S:/Godot4/Godot4CSharp_console.exe --headless --path Godot/GodotProject --script res://EditorScripts/align_fire_demon_orientation.gd --quit --no-window` | PASS；九连与落地库没有根节点强制 `.:scale` 轨道，效果由 `SkillEffectNode.Show` 按场景原始镜像和角色方向计算；资源 UID 保持 |
| `S:/Godot4/Godot4CSharp_console.exe --headless --path Godot/GodotProject --script res://EditorScripts/validate_skill_animation_sections.gd --quit --no-window` | PASS；五项技能原生入口、分段方法键、暂停/恢复和 RESET 均通过 |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | PASS；0 错误，5 个框架既有弃用警告 |
| 生产 C# 固定节点路径扫描 | PASS；`GameScripts` 与非 Debug `MainPack/Scripts/UI` 中 `GetNode("...")`、`FindChild("...")`、`HasNode("...")` 均为 0；动态资源标记和动画根路径保留 |
| OpenGL `--smoketest=ui --fire-motion-only` | PASS、退出 0；正常窗口 OpenGL 运行覆盖 60/120Hz × 左右朝向 × 有/无目标共 8 次，真实检查下冲、落地、九连/落地效果朝向、目标锚点、命中、单次耗蓝、无反弹和收招；`appdata_fire_final` 输出含 `SMOKE-FIRE-MOTION PASS` |
| 官方伤害数字逐像素核对 | PASS；`damage_number_monster_physics`/`hurtnum`、`damage_number_monster_physics_crit`/`bnum`、`damage_number_hero_magic`/`pnum` 的 10 个格子全部与官方源 PNG 逐字节一致；蓝色只保留给 MP 恢复 |
| 七星洞倒置 T 台静态几何 | PASS；`Surface06` 与 `Surface07` 世界边界和官方 `later_levels.json` 的 `2-2` 分别一致，最大误差小于 0.02px；场景的 60/120Hz 双向跳跃回归此前已通过 |
| `dotnet test Tests/BattleTests/BattleTests.csproj --no-restore --filter FullyQualifiedName~HeroBodyTests` | PASS；55 项通过，含 24/60/120Hz 官方跳跃轨迹与上坡速度 |
| `dotnet test Tests/BattleTests/BattleTests.csproj --no-restore --filter FullyQualifiedName~MonsterAiTests` | PASS；50 项通过 |

火魔斩专项必须使用正常 OpenGL 窗口运行，因为 60Hz 分支会截取过程图并等待 `RenderingServer.FramePostDraw`；`--headless` 不提供该信号，不能作为此专项的运行方式。烟测夹具现在对起手落地使用最多 240 个物理帧的有限等待，地面绑定异常会报告失败而不会永久等待。正常 OpenGL 运行已覆盖 8 组双向/目标组合并通过。

最初 UI 检查在技能入口超时，实际原因是既有纯画面采样 `Seek(time, true)` 重复执行声音方法轨道，导致资源代理占满、排队超过 1200 个请求。只将测试采样与恢复改用 `updateOnly: true`；游戏的动画方法及音效入口不变。默认完整 UI 曾因 240 秒执行上限在坡面阶段中止，不能计为完整通过；保留 `ui_full_timeout.log` / `.exit`。随后限定第五关坡面的生命周期回归还检出墓碑延时把掉落回收也延后：`ui_death_loot_before.log` 退出 1。死亡回调补回立即 `ClearLoot()`，失败界面仍等待墓碑，最终按上表完整跑通 UI 生命周期。

前端与 UI 仍有系统证书读取、GGF WebRequest 关停 `ObjectDisposedException` 及 ObjectDB 泄漏；默认完整 UI 另记录两条 `SwapGCHandleForType` 异常。前端坏档、跨槽和部分注册失败为既有主动失败注入，与本轮断言分开记录。未修改 Framework、addons、正式存档或 Git 索引。

## 死亡墓碑与火魔落地点（2026-10-25）

| 命令 / 检查 | 结果与证据 |
| --- | --- |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | PASS，0 错误；仅保留框架既有 5 项弃用/联网警告 |
| `git diff --check`（本轮目标文件） | PASS |
| 悟空死亡资源静态核对 | PASS；`RoleDeath.png` 为 1832×1830、8×5 图集；死亡动画长度 3.25 秒，墓碑帧轨道含 78 个源帧并保持末帧，`RESET` 恢复身体/武器显示并隐藏墓碑，死亡第 0 秒仍调用 `PlaySoundAnimEvent(5)` |
| 死亡动画逐帧探针 | PASS；真实加载 `wukong_entity.tscn` 后播放 `death`，身体/武器隐藏、墓碑显现，78 次推进后停在第 39 帧 |
| `--smoketest=ui --fire-motion-only` | `SMOKE-FIRE-MOTION PASS`，60/120Hz、左右方向、有/无目标共 8 组；下冲、落地恢复、命中、单次耗蓝、落地特效与悟空真实世界坐标重合、无再次上升均通过 |
| `--smoketest=ui --qixing-only` | `SMOKE-QIXING PASS`；蛮力怪/飞刀魔/狮驼王六招双向实际伤害、离身飞刀/毒雾、旋风无敌、中毒暂停与复用、狮驼王 150px 索敌高度和持续追踪、普通怪死亡保留受击体均通过 |
| 独立场景加载 | 已启动真实 Godot 进程并成功注册火魔斩四个效果资源；完整 UI 烟测被既有 `SaveSlot` 夹具失败中止，未将其计入本轮业务 PASS |
| Godot 编辑器扫描 | 构建完成；编辑器退出仍报告已知 ComponentInsoector `Free()`/设置权限错误，未报告本轮资源语法错误 |

本轮不修改 `EquipmentDropPoolId.Common`：它是 `Items.xlsx` 中“普通装备”池的稳定 ID，蝙蝠、蛮力怪和飞刀魔确实引用该池；“Common”与装备品质枚举同名但属于不同枚举域，不是重复配置。

## 坡面、普攻收紧与美术巡检（2026-10-25）

按用户最新手感要求及第30/40/50章，在原生资源中修复；不增加运行时爬坡补丁或生产观测接口。七星洞三段矩形拼坡改为一个连续六点坡体；洞窟相机右界按完整地面裁至4696/4681并重算远景视差。普攻不再以官方隐藏探针为下限，武器与弧光共用判定，保留时序和威力。补齐三类小怪15条旋转复位轨道，飞刀默认判定关闭；前三关地面各修复92个裂缝像素，尺寸和原点保持。

引擎为 `S:/Godot4/Godot4CSharp_console.exe --path Godot/GodotProject`，实机加 `--rendering-method gl_compatibility`；每次运行独立APPDATA和Validation存档。本节日志、基线和截图位于 `.godot/validation/presentation_polish/`。

| 命令 / 检查 | 结果与证据 |
| --- | --- |
| `--smoketest=ui --terrain-only --terrain-level5` | 修改前在X≈1271被Surface03侧边卡住，退出1；修改后60/120Hz×双向×走跑共8条路线PASS、退出0，含坡中停步/再起步、停步收招和主动跳跃；`qixing_slope_before.log`、`qixing_slope_after.log` |
| 扩大的 `--terrain-only` 专项 | 前两关全部5座坡的40条路线通过，`slopes_all_after.log`；随后七星洞因夹具保留尖刺进入Hurt而退出1。夹具移除自身副本的陷阱后单独完成上述8条路线，共覆盖6座坡48条路线；花果山末坡只验证墙内可玩部分，未移动正式陷阱 |
| OpenGL `validate_official_wukong_attacks.gd -- <hud_entry_attack_crop/export/manifest.json>` | 最终版本PASS、退出0；三套身体×三套武器×双向×62源帧，共1116次绘制与真实Area近端/范围外/弧光命中检查；`normal_render_final.out/.err/.exit` |
| 几何基线比较 / `repair_official_attack_geometry.gd -- <技能manifest> --normal-attacks-only` 重跑 | 五段有效帧平均面积减少25.5%/25.4%/9.5%/37.9%/25.8%，无一帧面积扩大；美术、方法键、窗口和时长完全不变。重跑0处几何变化、文件SHA稳定；`range_compare_final.log`、`range_stability.log` |
| OpenGL `--smoketest` / `--smoketest=ai` / `--smoketest=ui --qixing-only` | 全部PASS、退出0；33次真实身体切换、普攻命中、怪物AI，以及七星洞六招双向、飞刀/毒雾、无敌、中毒、暂停及复用；`normal_game`、`ai_final`、`qixing_combat_final` 的 `.out/.err/.exit` |
| OpenGL `--smoketest=level4` / `--smoketest=level5` | 最终相机与坡体下，两关五波、出口、结算音乐、检查点和地图磁盘重读均PASS、退出0；`level4_final`、`level5_final` 的 `.out/.err/.exit` |
| `.godot/validation/presentation_polish/review_art.gd` | 14个实体/效果场景、75动画、1773姿态样本，固定原点巡检与294张动作截图；五关按真实相机边界补做16张取景图。已查看角色拼图、坡体及洞窟终点；`art/audit.json`、`art_review.out`、`level_views_sealed.out` |
| `export_official_level_art.py --xml <stages_2_3/main.xml> --java <Java> --ffdec <FFDec> --work <seam_reexport> --output <seam_reexport/output>` | 完整重导退出0；29张PNG与正式资源逐字节一致。前三关仅图内x=3779各92个像素改变；桃花源原漏背景位置实测RGB从(176,221,217)变为地面色(185,162,111)；`seam_reexport.log`、`ground_seams.json`、`art/level3_line_fixed.png` |
| `validate_anim_libraries.gd` / `validate_collision_geometry.gd` / `audit_attack_geometry.gd` | 全部退出0；11类演员的方法与轨道完整；50场景、239基础体节点、6座整体坡、375形状键；相邻重复几何键0。两个关闭的动画占位形状不误报为重复有效判定；对应 `*_final.log` |
| `validate_qixing_level.gd` / `validate_bat_cave_level.gd` | 均退出0，关卡绑定、坡体、相机完整地面右界及原有出生流/门/尖刺检查通过 |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` / C# whitespace | 构建退出0、0错误、5个既有框架警告；只格式化本轮Terrain夹具。`build_closeout.log`、`format_fix.log` |
| `validate_game_references.py` / whitespace verify / `git diff --check` | 最终均退出0；686条资源路径、307个UID通过，PNG像素逐一对照确认每张仅92点变化；`ground_pixel_validation.json` |

旧 `validate_official_attack_geometry.gd` 全项精确比较仍退出1：当前禺狨王雷电阶段和飞刀魔普攻的已有尺寸不同于其中的旧固定值，未改动这两份动画范围；英雄技能40个源探针角点先行通过。不能把该检查记为全PASS，也不据此覆盖现有范围。原生场景/动画审计和本轮实机结果与其区分记录。运行仍有既有证书读取、GGF WebRequest关停异常，编辑器导入仍有ComponentInsoector释放及RID/ObjectDB关停诊断；未修改Framework、addons、正式存档或Git索引。

## 全部现有攻击范围与特效（2026-10-24）

依据第30/40/50章，覆盖当前11个角色/怪物/BOSS场景与7个独立效果；石像没有攻击。共32个动作、999个阶段样本，双向检查；未实现的唐僧等角色不制造空判定。修正音波、三类早期小怪、旋风/雷击、毒雾与悟空全部现有武器的共享范围，保留伤害、攻击编号、动画时长和美术。毒雾的淡出空帧关闭判定；其余启闭时点保持。

| 检查 | 结果与证据 |
| --- | --- |
| `repair_bat_sonic_hitboxes.gd` / OpenGL `validate_bat_sonic_hitboxes.gd` | 均退出0；11个非冗余阶段，源帧1/3/5/7/9/10/11/13/15/16/18。20帧全部包含淡环范围、每边阶段余量≤6px，第10/15帧单独对齐，双向及idle清理通过；`bat_final_author.log`、`bat_verified.log/.err` |
| `repair_official_attack_geometry.gd -- <技能导出manifest>` | 退出0；九连源矩形、禺狨王阶段、猕猴王两个矩形及早期小怪源探针；毒雾241帧压缩为114个有实际变化的阶段，形状/位置各自去重。武器按当前3套外观并集扩展原有判定，保留原范围下限；`author_weapons.log`、`author_wind_final.log` |
| OpenGL 全树审查 `.godot/validation/attack_geometry_review/review_all.gd` | 两向各999样本；全部现有武器544个有效样本的实体像素与真实刀尖点查询通过，含家传宝剑。全树最终左向999样本、右向悟空184样本，关键帧已人工查看；`full_final.log`、`review_after.json`、`right_final.log`、`subset_right.json`。不把所有特效像素都当伤害 |
| 官方源核对 | Role_v7恢复后SHA-256与登记一致。`BaseMonster.beMagicAttack`先范围粗筛，再调用`HitTest`对整个特效做alpha偏移255后的像素相交。112→111显示列表没有额外隐藏探针或圆；旧Godot的134.134圆不能称为官方。`fire_demon_combo_bounds`直接从源XML形状和父矩阵读取；详见OfficialReference |
| `--headless --script res://EditorScripts/validate_official_attack_geometry.gd -- <技能导出manifest>` | 退出0；204个官方身体/技能探针角点没有被当前范围缩小；九连588次Area查询、44帧×双向；毒雾2260次查询、241帧×双向、30个淡出/空白样本关闭、边缘余量≤6px。`probe_minimum_final.log` |
| `--headless --script res://EditorScripts/validate_boss_attack_areas.gd` | 退出0；38次命中、54次排除，双向、乌云/外侧空气排除、中断RESET，以及旋风风冠/风柱接合；`wind_physics_final.log` |
| OpenGL `-- --smoketest=ai` / `--smoketest=bosses` / `--smoketest=ui --qixing-only` | 均PASS、退出0；真实AI、猴子命中/死亡、两位BOSS全部招式/无敌/风刃回池，以及七星洞六招双向、飞刀/毒雾/十跳中毒/暂停/复用；日志在`official_continuation/attack_review_{ai_final,bosses_final,qixing}.{out,err,exit}` |
| OpenGL `-- --smoketest=ui --bat-sonic-only` / `--inventory-skills-only` | 最新结果见`official_continuation/attack_review_{bat_final,skills_final}.{out,err,exit}`；验证音波60/120Hz×双向×边缘/上下留白，以及五招、火魔两分支、实际伤害/声音/打断/复用 |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` / `dotnet test Tests/BattleTests --no-restore -v quiet` | 均退出0；构建0错误、5个框架既有警告；205项测试通过，另有既有NU1900/CS8632。`build_final.log`、`battle_final.log` |
| `audit_attack_geometry.gd` / 非几何轨道比对 / 引用检查 | 全树相邻重复几何键为0，`key_audit_final2.log`；艺术帧、声音/方法轨道与长度不变；保留UID，无战斗多边形。引用检查和diff检查见下文收尾结果 |

引擎为`S:/Godot4/Godot4CSharp.exe --path Godot/GodotProject`，独立APPDATA；OpenGL加`--rendering-method gl_compatibility`。本节日志默认在`.godot/validation/attack_geometry_review/`。技能manifest含`fire_demon_combo_bounds`，本轮用`wukong-skills-export/attack_geometry_source.json`。独立几何夹具只在自己的动画副本中禁用方法键，真实回调走完整GGF流程；没有生产测试API。旧AI烟测曾因仍要求50×50旧框退出1，已按Monster1源探针修正测试常量后重跑通过；纯规则的合成测试框保持独立。既有证书错误和WebRequest关停ObjectDisposedException保留，与断言/退出码分开记录；未改Framework/addons、正式存档或Git索引。

范围规则依据第30/40/50章：先确认官方使用的攻击对象与机制，再说明项目基础体近似；不能把导出画布、旧移植数据或可见像素拟合称为官方精确判定。只在有效阶段变化时增加关键帧，保留内部间隙并排除云层等装饰；独立特效与角色身体的判定各自拥有，不用逐像素多边形。

## 火魔斩运动、尖刺射线与地图悬停（2026-10-24）

依据第10/30/40/50章，受伤语音统一为m_HurtSoundId并保留原值4；尖刺使用向下RayCast2D触发、刺身Area结算伤害；蝙蝠窟悬停居中并同步命中变换。火魔斩改为原生分段精确起播、方法键/标记精度对齐，起手和退出清除旧分段，避免Seek重放旧键。未增加生产测试API，未改Framework、addons、正式存档或Git索引。证据目录为 `Godot/GodotProject/.godot/validation/fire_demon_review/`。

引擎使用 `S:/Godot4/Godot4CSharp.exe --path Godot/GodotProject --log-file <日志>`、独立APPDATA；实机加 `--rendering-method gl_compatibility`，原生脚本加 `--script res://EditorScripts/<文件>`。

| 命令 / 检查 | 结果与证据 |
| --- | --- |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` | 退出0，0错误、5个既有框架弃用警告及1个NU1900；`build_closeout.log` |
| `dotnet test Tests/BattleTests --no-restore -v quiet`、ProfileTests、LevelTests | 205 / 188 / 19项全部通过，共412项，各退出0；`BattleTests.log`、`ProfileTests.log`、`LevelTests.log` |
| `--headless --script res://EditorScripts/validate_skill_animation_sections.gd` | 退出0，实际调用C#入口；三轮起手/下冲/落地恢复各执行对应方法一次，不重放旧起手和升空；五招从零起播，退出无分段残留。`sections_verified.log` |
| OpenGL `-- --smoketest=ui --fire-motion-only` | 退出0，真实O输入、同一英雄八次施放，60/120Hz×左右×有无目标；逐物理步确认实际下冲、无反弹、落地不再抬升、前奏不重播、九连分支、落地效果/伤害、一次扣蓝和收招。`motion_grounded.log`、`after_grounded/*.json`；八份首样本均在地面；两分支过程图 `fire_grounded_False.jpg` / `fire_grounded_True.jpg` 已查看并按Role_v7/Hit10And2帧规则核对 |
| OpenGL `-- --smoketest=ui --inventory-skills-only` | 退出0，五招真实输入、两分支及GGF声音；受伤语音、死亡、中断RESET、复用和装备界面通过。`inventory_skills.log` |
| OpenGL `-- --smoketest=ui --bat-cave-only` | 退出0，射线排除旁边/上方英雄、正下方触发；地刺不启用射线；固定30伤害、无敌重试、一秒周期、完整预警、重力576、暂停及复位通过。`spike_ray.log` |
| OpenGL `--script res://EditorScripts/validate_bat_cave_hover.gd` | 退出0，普通/悬停中心一致，最小/最大缩放时可见像素命中、上方透明区排除；`map_hover.log`，三张渲染及 `map_comparison.jpg` 已查看 |
| `--headless --editor --build-solutions --quit --no-window` | 扫描/构建完成，退出0，新脚本UID生成；`editor_scan.log`。三份资源随后经原生API补存依赖UID、保留原资源UID；`dependency_uids.log` |
| `python Tools/ProjectMaintenance/validate_game_references.py` / 定向 `dotnet format whitespace` / `git diff --check` | 均通过，688条序列化路径、307个UID；旧受伤字段及尖刺绑定无遗留；不格式化生成代码 |

旧测试只看阶段和最终命中，会漏掉速度反转或前奏重播。早期探针暴露标记精度漏键；`motion_verified.log`的部分样例因瞬移后读取旧IsOnFloor而从空中起手。夹具已先等待物理刷新并稳定落地，地面八组以 `motion_grounded.log` 为准；历史Seek探针仅证明方法被调用，其方案已由本节替代。

仍有既有系统根证书错误、GGF WebRequest关停ObjectDisposedException及编辑器插件RefCounted/free错误，未修改只读依赖。依赖UID补存立即退出还有Scan thread aborted警告，完整扫描以editor_scan的DONE为准；不将上述PASS与退出0表述为零错误运行。

## 敌人职责与官方动作复核（2026-10-24）

本轮在已有迁移工作区上修正Enemy / Monster / Ground / Sky职责，核对原版Monster1/2/3/7和狮驼王攻击2。Godot正式资源保持原UID；动画压缩仅删除相邻相等离散值，没有改变帧长/事件/连续插值。测试使用独立Validation档案，不改正式存档或Git索引。日志位于 `Godot/GodotProject/.godot/validation/actor_review/`。

| 检查 | 结果 |
| --- | --- |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` | 退出0，0错误、6项既有框架/联网警告；`build_final.log` |
| `dotnet test Tests/BattleTests/BattleTests.csproj --no-restore -v quiet` / `Tests/ProfileTests` / `Tests/LevelTests` | 205 / 188 / 19 全部通过，共412；分别见 `battle_final.log`、`profile_final.log`、`level_final.log` |
| Godot OpenGL `--smoketest=ui --enemy-hierarchy-only` | 退出0；五种地面怪限制空中请求、落地出招/恢复和池复用；猴子受击激怒、重新索敌及复用清理；攻击者打击音/接收方语音与所有怪物类型通过。`hierarchy_final2.log` |
| Godot OpenGL `--smoketest=ui --bat-cave-only`、`--smoketest=ai`、`--smoketest=bosses`、`--smoketest=ui --qixing-only` | 均退出0；尖刺原生Area触发、飞行身体、普通AI、两位Boss五招、飞刀/毒雾、无敌和离身攻击回池通过。`bat_final.log`、`ai_final.log`、`boss_final.log`、`qixing_final.log` |
| Godot OpenGL `--smoketest=level4` / `--smoketest=level5` | 均退出0；真实五波、出口、结算、检查点、存档读回通过。`level4_final.log`、`level5_final.log` |
| `capture_actor_comparisons.gd` 与 `verify_actor_pixels.py` | 狮驼王攻击2的1..47帧双朝向与官方完整父级渲染对照；预乘RGBA平均误差0.005708，最大帧36为0.016028，保留未校正的约1px亚像素边缘差异。悟空与石像不透明重叠2682像素，合成图全部为悟空前景；`lion_frame_verification.json`、`lion_comparison_montage.png` |
| `repair_animation_uids.gd` / `optimize_animation_keys.gd` | 七个动画资源恢复原UID，含四份引用资源；重跑压缩为0库、0键变化，证明改动稳定。早期保存触发的UID警告见 `animation_uid_repairs.log`，修复后正式场景/资源引用与缓存验证见最终引用检查 |
| 已安装 XLSX Diff 1.6.4 worker | `Resources.xlsx` 路径编辑普通值/公式两种模式均识别恰好6格；`xlsx_diff.log` |
| 定向 `dotnet format whitespace` / `git diff --check` | 均退出0；30份本轮相关手写C#文件格式检查，未格式化生成代码 |

Godot每次运行仍报告系统根证书读取失败；关停时GGF WebRequest可报告既有ObjectDisposedException。编辑器加载只读addon还报告 `ArchiveSettingInspectorPlugin.free` 方法不存在；未修改 Framework/addons。以上和业务烟测断言及进程退出码分开记录。

## 火魔斩与官方第四关（2026-10-22，当前）

依据第10/20/30/40/50章实施，源为同一v25928及加载器指定的Monster2_v4。场景/动画经Godot原生API保存，源表经Luban生成，资源常量及UI绑定经既有生成器更新。未改Framework、addons、正式存档或Git索引。日志根目录为 `Godot/GodotProject/.godot/validation/official_continuation/`。

| 命令 / 检查 | 结果与证据 |
| --- | --- |
| 官方提取 `extract_official_levels.py --levels 2-1 2-2 2-3 3-1 3-2 3-3 4-1` | 退出0；第四关为2-1。默认前三关输出与此前清单相同；Monster2_v4散列见OfficialReference |
| 七源表作者化、直接Luban client导出 | 退出0；保留既有ZIP部件/格式，仅增加第四关、家传装备、蝙蝠/石像及陷阱定义；尖刺击退最终为(240,240)，`luban_spike_fixed.log` |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | 退出0，0错误、5项框架既有弃用警告；`build_final.log` |
| `dotnet test Tests/BattleTests --no-restore -v quiet` | 205/205，退出0 |
| `dotnet test Tests/ProfileTests --no-restore -v quiet` | 180/180，退出0；含目标出口拒绝早期/错误/重复事件、最新关卡进度及火魔并行/退出输入锁 |
| `dotnet test Tests/LevelTests --no-restore -v quiet` | 19/19，退出0；含39只有限蝙蝠、三条999流与石像目标，末段出口不要求耗尽持续流 |
| 原生第四关装配 | 17生成点、9门、6矩形地形（3单向）、35尖刺。修复先设置父owner导致碰撞未pack的问题；20尖刺帧打包为190×256图集及外置SpriteFrames |
| OpenGL `--smoketest=level4` | PASS、引擎退出0，`cave_complete_final.out/.err/.exit`；真实五波、39只蝙蝠+石像、经验702、灵魂在合法624..1248范围内、3只活蝙蝠时开放出口、W组合拒绝、成功音乐/结算、地图保存提示及磁盘重读。W至地图约1.5s，包含未到达球体拾取及检查点 |
| OpenGL `--smoketest=ui --bat-cave-only` | PASS、引擎退出0，`cave_features_final.out/.err/.exit`；尖刺加载期无伤害、30真实伤害、无敌后重试、一秒编号周期、完整预警、加速度576、暂停、隐藏/复位及停止清引用；蝙蝠空中追击/攻击/受击与收招返回飞行/死亡回池；石像6000生命、受击不位移且无攻击 |
| OpenGL `--smoketest=ui --inventory-skills-only` | PASS、引擎退出0，`skills_inventory_final.out/.err/.exit`；家传宝剑GUI穿脱、战斗/预览同纹理、双朝向全部动作、手镯属性与唐僧装备拒绝；火魔两分支、第47帧下冲、实际伤害、落地并行、五招输入及GGF音效。冷却细节收尾复测为 `skills_complete.out/.err/.exit` |
| 官方整幅地形与完整远景对照 | OpenGL退出0；`cave_art.out` 前景全景平均误差0.00211、严重差异0.0275%；`cave_views.out` 起点/中段/终点完整FloorBg+前景平均误差0.00245/0.00250/0.00326，严重差异0.0463%/0.0447%/0.0716%。三张完整取景及实际装备/出口截图已查看 |
| `regenerate_collection.gd` | 退出0，最终539项，无重复basename；含尖刺图集/frames、7张装备图与洞窟远景，无已删loose帧引用；`collection_final.out` |
| 已安装XLSX Diff 1.6.4 worker | 七张源表普通/公式两种模式均通过；变化格数为3/18/38/90/60/196/10，`xlsx_diff.log` |

## 官方 2-2 七星洞接入（2026-10-23，当前）

依据同一 v25928 的 GameSence 第 20 帧、Monster_v1/Monster2_v4 角色类和第 20/30/40/50 章实施。正式资源、场景、源表与生成物均已接入；第四关仍只使用已匹配的 2-1 资料。

| 命令 / 检查 | 结果与证据 |
| --- | --- |
| 七张源表作者化及直接Luban CLI（既有cs-bin/bin参数） | 退出0；20条配方的数量、4/12/17秒等待、2秒间隔与原生成流逐条核对，补齐六招和独立中毒记录、72/72/96px/s移速及30%天煞骨链池；源表与产物同步。`qixing_tables_final.log`、`qixing_luban_final.log` |
| `import_official_qixing_art.gd` / `create_qixing_actor_scenes.gd` | PASS；图层拥有frames_path；切换派生脚本后显式绑定继承层Export节点，身体、播放器、受击/攻击盒、镜像根、感知和血条完整；不再继承Gorilla的受击盒偏移 |
| `export_official_qixing_level_art.py` / `import_official_qixing_level.gd -- <later_levels.json> <qixing_terrain/layout.json>` | PASS；20生成点、五阶段/九门登记、8几何体、三独立地形层和35尖刺；首门由官方918.25扩至940以满足项目一屏约束，保留21.75px差异 |
| `enable_qixing_map.gd`、UI绑定与Collection Res生成器 | PASS；m_Qixingdong复用MapLocationButton，正式引用外置悬停动画，移除重复Glow和按钮内置悬停纹理；Collection最终572项，`qixing_collection_final.log` |
| Godot `--headless --path Godot/GodotProject --script res://EditorScripts/validate_qixing_level.gd` | PASS、退出0；20生成点、9门、8形状、三地形层、35尖刺、三个角色的节点/动画绑定和地图全部轨道通过；最终复核 `qixing_validate_closeout.log` |
| `dotnet build Godot/GodotProject --no-restore -v quiet` / Godot `--headless --editor --build-solutions --quit --no-window` | 均退出0；构建0错误、6项既有框架/联网警告；`qixing_build_final.log`、`qixing_scan_final.log` |
| `dotnet test Tests/BattleTests --no-restore -v quiet` / `Tests/ProfileTests` / `Tests/LevelTests` | 205 / 186 / 19项通过，共410项；`BattleTests.log`、`qixing_profile_final.log`、`LevelTests.log` |
| OpenGL `--smoketest=ui --qixing-only` | PASS、引擎退出0；六招双向共12次、飞刀/毒雾真实命中、旋风无敌、中毒十跳/刷新/暂停和复用；实际伤害34、44、59、59、198、40，符合英雄基础防御。`qixing_attacks_clean.log` |
| OpenGL `--smoketest=ui --inventory-skills-only` | PASS、引擎退出0；天煞骨链透明图标、GUI穿脱、四属性与售价80；家传换装、V技能面板、火魔斩两分支及五招命中/GGF声音、中断RESET继续通过。`qixing_inventory_skills.log` |
| OpenGL `--smoketest=level5`（最终配方、地形及尖刺） | PASS、引擎退出0；完整五波数量12/13/14/17/1、守关与W组合拒绝、经验1344、灵魂1798（合法1172..2344）、成功音乐/结算、检查点提示和地图磁盘重读。`qixing_full_final.log` |
| OpenGL `--script res://.godot/validation/official_continuation/validate_qixing_views.gd` | PASS、引擎退出0；起/中/终的完整前景、平台、35尖刺及FloorBg对照平均误差0.004086/0.004285/0.003328，严重差异0.02398%/0.03101%/0.02272%；取景已查看。最终 `qixing_views_closeout.log`，无地形UID缓存警告 |
| 已安装XLSX Diff 1.6.4 worker | 七张源表普通/公式模式均可读；默认命名空间及既有ZIP部件保留。`qixing_xlsx_diff_final.log` |
| 定向 `dotnet format whitespace ...GodotProject.csproj --include <本轮C#绝对路径>` / `git diff --check` | 均退出0；只检查手写修改，不手改生成物或Git索引 |

早期场景可加载时仍缺Export绑定和攻击记录，关卡初始化失败；烟测随后访问已清理模型产生次生NRE。已修复源场景和招式，烟测会话失效时直接Fail；最终三组实机日志没有该NRE。早期装配/画面对照中的三张新地形UID缓存警告在最终Godot扫描后消失，保留旧日志并以closeout复核区分。不能用装配PASS替代上述真实战斗与通关结果。

采用真实主引擎 `S:/Godot4/Godot4CSharp.exe` 并等待其退出，烟测使用隔离APPDATA与Validation档案。早期探针碰到外墙、读回地图时访问已卸载模型以及控制台启动器进程误判的失败记录保留在忽略目录；表中只列最终完整证据，不将“加载HUD成功”算作通关。

运行仍报告系统根证书读取失败、GGF WebRequest退出ObjectDisposedException及少量ObjectDB泄漏；编辑器作者化/Collection另有既有RefCounted/free、RID关停诊断和框架入口UID提示。以上与业务PASS及进程退出码分开记录，未修改只读依赖。第四关与 2-2 已接入；2-3 及之后只有来源提取，尚未移植。第20章仅清场/第40章仅三关的旧描述未自行改写。

## HUD 预载、攻击 3 裁切与嵌套配置（2026-10-22，当前）

本轮只修改业务流程、官方攻击 3 特效图集、两张配置源表及其 Luban 产物；Framework、addons、Tools/Luban、BOSS 编辑器改动和正式存档均未覆盖。下冲特效场景通过 Godot 打包 API 仅恢复 `CollisionShape2D.disabled=true`，保留已有位置和旋转。

| 命令 / 检查 | 结果 |
| --- | --- |
| Luban 直接 CLI（`Configs/GameConfig`，`cs-bin`） | 退出 0；`PassiveConfig` 为 5 个父记录 × 5 级，`HeroGrowthConfig` 为 1 个英雄记录 × 55 级；源表、生成 C# 与 bytes 同步 |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` | 退出 0、0 错误；框架既有弃用警告不由本轮引入 |
| `dotnet test Tests/{BattleTests,ProfileTests,LevelTests}/... --no-restore -v quiet` | 205 / 179 / 18 项通过 |
| OpenGL `--smoketest=ui --entry-only` | PASS、退出 0；HUD 与关卡并行准备，HUD 就绪后才开始会话 |
| OpenGL `--smoketest=ui` | PASS、退出 0；预期 HUD 缺绑定失败注入完成清理，随后 UI、存档重入、装备外观、技能、平台和怪物回归通过 |
| OpenGL `--smoketest=level-cancel` / `frontend` / `level` | 均 PASS、退出 0；在途取消、菜单/地图、五波关卡、守关血条、出口和检查点通过 |
| `validate_wukong_attack_effect.gd -- renders/wukong_attack_effects.png` | PASS、退出 0；88 个实际姿态、9 个原生滤镜帧、2 个空帧、四种穿戴和双朝向，根坐标稳定 |
| `validate_game_references.py --compare-staged-geometry` | PASS；598 条序列化路径、293 个 UID、17 个 staged 几何节点 |

入口对照日志中角色就绪到 HUD 可见从约 416ms 降到 22ms；完整结果位于 `.godot/validation/hud_entry_attack_crop/`。正式工程的 `--headless --editor --import --quit` 已完成资源扫描但在只读 ComponentInsoector 插件退出阶段以 `0xC0000005` 结束；隔离工程的实际运行和攻击像素验证均退出 0，因此没有扩大插件修改范围。所有 OpenGL 日志仍可能包含系统根证书读取失败、故意注入的 HUD/存档错误和框架关停 `WebRequest`/ObjectDB 诊断；这些与业务断言分开，未修改只读框架。

## 基础判定与关卡编号目录（2026-10-22，当前）

按用户最新要求及第 10/30/40/50 章实施：攻击采用少量基础形状，土坡保留简单整体六点凸形；专属 BOSS 与三关资源使用 `zm<作品>_<两位关卡>_<名称>`。`zm` 前缀避免 GGF 将纯数字目录名生成为非法 C# 标识符。上轮精细多边形方案及两个专用工具已删除。原生资源经 Godot 保存和移动，生成物经既有工具生成；Framework、addons、旧工程、正式存档和 Git 索引未修改。

日志位于 `.godot/validation/simple_collisions/`，运行使用 `skills_runtime/project` 隔离工程，每个进程独立 APPDATA 与 Validation 存档。Resources.xlsx 只改 EntityConfig 的 D10/D11，LevelConfig.xlsx 只改 F5/F19/F37；表格工具验证后保留原 ZIP 结构，其余内容和格式不变。

| 命令 / 检查 | 结果 |
| --- | --- |
| Godot `--headless --editor --script res://EditorScripts/simplify_collision_geometry.gd -- <baseline目录>` | 禺狨王 74 个多边形节点改为一个分阶段矩形；猕猴王至多两个基础体；普通地形转矩形，五座整体坡保留；final_authoring.log |
| Godot `--headless --script res://EditorScripts/validate_collision_geometry.gd -- <baseline目录>` | PASS、退出 0；40 个场景、111 个基础体节点、5 座简单整体坡、213 个动画形状键，36 个地形覆盖均与修改前快照一致；audit_zm.log |
| 正式工程同上巡检 / `--headless --script res://EditorScripts/validate_anim_libraries.gd` | 均退出 0；正式 40 个场景与 36 个地形对照、六类角色动画轨道与方法绑定通过；audit_actual_complete.log、animations_actual_complete.log |
| OpenGL `--script res://EditorScripts/validate_boss_attack_areas.gd -- --snapshots` | PASS、退出 0；左右朝向 22 个命中 / 38 个排除点，闪电间隙命中、云层与外侧空气排除、中断 RESET 关闭；areas_render.log。五张碰撞叠图已查看 |
| `move_game_resources.gd` / Collection Res / NodePool / Luban 直接 CLI | 原生迁移与标准生成通过；safe_moves.log、collection_final.log、pools_final.log、luban_final.log |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` | 退出 0，0 错误、5 项既有框架警告；build_actual_complete.log，正式编辑器已关闭 |
| 同方式构建隔离工程 | 退出 0，0 错误；最终平台观测适配后也通过 |
| OpenGL `--smoketest=ui`（完整流程） | PASS、退出 0；ui_complete.log。16 条两关坡面路线覆盖 60/120Hz、双向走跑；三块单向平台与 S+K 下穿、英雄五招、三类小怪、装备提示框、绑定/池复用、失败清理与存档重入通过 |
| OpenGL `--smoketest=bosses` | PASS、退出 0；两位 BOSS 五招实际伤害，旋风 12 次、雷云 6 次，风刃、无敌及回池；bosses_final.log |
| OpenGL `--smoketest=progress` | PASS、退出 0；三关实际通关、胜利音乐、出口、检查点、标题/磁盘重读与连续解锁；progress_final.log |
| `dotnet test Tests/{BattleTests,ProfileTests,LevelTests}/…csproj --no-restore -v quiet` | 205 / 179 / 18 项通过，合计 402 项；battle_tests.log、profile_tests.log、level_tests.log |
| `python Tools/ProjectMaintenance/validate_game_references.py` / 迁移清单审计 | PASS；598 个序列化路径、291 个脚本 UID；60 个最终文件、37 个已有 UID 保留；495 份原生资源与隔离工程相同，96 个根资源 UID 无重复 |
| `dotnet format whitespace . --folder --include <本轮手写C#> --verify-no-changes` | 退出 0；平台测试等手写修改按 CRLF 复核 |
| Godot `--headless --check-only --script <import_official_levels.gd / align_official_level_art.gd>` | 两者退出 0，已同步基础形状读取；import_parse.log、align_parse.log |

完整官方数据检查仍明确报告桃花源 Surface02/03 与源文件不同（levels_final2.log）；这两处在修改前 Git 索引中已手调，不能把本轮简化变成还原用户地形。当前矩形保留其原覆盖，全部 36 个修改前地形对照通过；47 条刷怪配方和其余官方空间检查通过，不宣称官方精确地形检查全 PASS。

早期 ui_final 在普通平台的旧 CollisionPolygon2D 测试强转处失败，已改为读取 CollisionShape2D 的世界边界，完整 ui_complete 重跑通过；更早的 progress 运行被中途路径迁移影响，最终 progress_final 重跑通过。ui_complete 中 BattleForm 绑定缺失错误和损坏主档回退警告来自既有故障注入，用于验证失败清理与备份恢复，不是正常启动失败。用户关闭旧编辑器时重建的三份平铺关卡已先备份到 editor_recreated 再移除，最终仅保留新路径。日志中的既有证书读取错误、WebRequest 关停 ObjectDisposedException 和编辑器 ObjectDB/RID 泄漏分别记录，未扩大框架修改。

## Tooltip、BOSS 雷电与角色资源归属（2026-10-21）

以下为历史验证；其中精细雷电多边形和旧目录方案已由上方 2026-10-22 方案替代，历史 PASS 不代表当前判定规则。

依据第 10/30/40/50 章的职责、原生动画和验证边界，以及用户本轮的多边形命中和角色目录要求。第 30 章对单形状范围读取的旧限制、第 40 章旧目录描述保留，差异已在 WorkingNotes 报告。Framework、addons、旧工程、正式存档和 Git 索引未改。

日志与对照图在 `.godot/validation/boss_review/`。资源通过隔离 Godot 编辑器原生保存/移动；资源常量、池条目和表产物由既有生成器生成。Resources.xlsx 只更新 EntityConfig 的 D6/D10/D11：先经表格工具编辑导出核对，再将这三个字符串值写回原 ZIP，其他 ZIP 成员、单元格类型与格式保持，避免破坏 XLSX diff。

| 命令 / 检查 | 结果 |
| --- | --- |
| Luban 直接 CLI（现有 cs-bin/bin 参数） | 退出 0；3 条实体路径指向角色目录，luban.log |
| Godot `--headless --editor --script res://EditorScripts/move_game_resources.gd -- <moves.json>` | 17 项移动通过，保留已有 UID；moves.log。没有根 UID 的四个效果不伪称有原 UID |
| Godot `--headless --editor --script res://EditorScripts/repair_tamarin_lightning.gd -- <runtime.json>` | 退出 0；74 个按需启用的多边形节点，连续相同关键帧合并；原矩形删除，geometry_holes_fixed.log |
| OpenGL `--script res://EditorScripts/validate_boss_lightning.gd -- <runtime.json> --snapshots` | PASS、退出 0；两招 106 个源帧、交替左右朝向，163 个雷电点、5,223 个透明区域点、581 个云层点；lightning_render_final.log。三张真实碰撞叠图已查看 |
| 原 sprite485 完整 76 帧 vs 已注册身体/效果图层合成 | 第 35～76 帧预乘 RGBA 完全一致；1～34 帧分层栅格化边缘有余差（最大单通道差 137/255），不能声称全帧像素一致。第 30/32/34/35/36/40 帧对照已看，源文件本身第 35 帧换大龙卷；tornado_pixels.log、tornado_comparison.png |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` | 退出 0，0 错误、5 项既有框架警告；build_actual.log。编辑器已关闭 |
| `dotnet build <隔离工程>/GodotProject.csproj --no-restore -v quiet` | 退出 0、0 错误；build_isolated_final.log |
| 正式工程 `--headless --script res://EditorScripts/validate_anim_libraries.gd` | 退出 0；六类角色完整轨道、方法绑定与迁移路径通过；actual_animation.log |
| OpenGL `--smoketest=ui` | 完整回归 PASS、退出 0；ui.log。首次并行共享 APPDATA 出现 hotupdate 标记占用，另以独立 APPDATA 重跑相关范围 |
| OpenGL `--smoketest=ui --inventory-skills-only` | PASS、退出 0；ui_final.log。原生 PopupPanel 的戒指/猿甲/短武器均按内容收缩、底距 6px；前奏固定原地且结束隐藏、五招输入与火魔两分支命中通过 |
| OpenGL `--smoketest=bosses`（最终独立 APPDATA） | PASS、退出 0；bosses_final.log。五招实际伤害，旋风 12 次、雷云 6 次，原版无敌、风刃与池复用通过 |
| `dotnet test Tests/BattleTests/BattleTests.csproj --no-restore -v quiet` / ProfileTests | 205 / 179 项通过、各退出 0；battle_tests.log / profile_tests.log |
| `python Tools/ProjectMaintenance/validate_game_references.py` | PASS：597 个序列化路径、291 个脚本 UID；根资源 UID 无重复，17 个旧路径均已移除 |
| `dotnet format whitespace . --folder --include <本轮手写 C#> --verify-no-changes` / `git diff --check` | 均退出 0；先修正本轮补丁的 CRLF，再完成格式复核，format_final.log |

密集透明点检查先揭示闭合电弧孔洞被 BitMap 外轮廓填实，修成按需递归分片后通过；物理点用像素中心，避免以分片公共边界测试像素内部。编辑器关闭时写回过一份旧路径禺狨王动画库，已备份为 `tamarin_library_saved_by_old_editor.tres` 后移除；随后核对正式文件、所有移动路径及根 UID，并完成正式编译与动画验证。

既有证书读取失败、WebRequest 关停 ObjectDisposedException 和部分编辑器 ObjectDB/RID 泄漏仍在日志，未扩大框架或插件修改。最终独立运行没有 hotupdate 文件占用，BOSS 烟测先清理生成任务，未再出现首次关停时的 TaskCanceledException。

在仓库根执行。实际引擎：`S:/Godot4/Godot4CSharp_console.exe`；可换成本机对应路径。

## 官方三关画面与两种 HUD 填充对齐（2026-10-20，后续复核）

按本轮用户要求重新核对；不把之前只查碰撞/坐标的 PASS 扩称为完整视觉一致。修复三关远山 -100Y、花果山旧前景/0.92 调淡与历史手调出怪点。桃花源背景图、四块石墙的完整变换、15 条出怪流及五个阶段右界与官方文件相同，保留这些正确坐标。无双黄条左移 5px，BOSS 红色填充上移 3px，保留整体控件位置。

遵循第 40/50 章：场景经隔离 Godot 原生保存，路径/资源 UID 保留；原文件只读，框架/插件和 Git 索引未修改。检测到正式编辑器打开，本轮构建与运行只在 `skills_runtime/project` 完成；现有悟空场景的用户编辑保留。日志目录 `.godot/validation/official_alignment/`，烟测只写唯一 Validation 存档。

| 命令 / 范围 | 结果 |
| --- | --- |
| `extract_official_levels.py --xml <main.xml> --script <GameSence.as> --output <official_levels.json>` | 三关 14/18/15 条生成流；新增绘制深度、矩阵、裁图原点及稳定节点映射 |
| `render_official_level_reference.py --xml <main.xml> --java <java> --ffdec <ffdec.jar> --work <忽略目录>` | PASS、退出 0；直接读取原 GameSence/bgClips 显示列表，未读取 Godot 场景或提取清单，生成三张独立官方整幅对照 |
| OpenGL `--script res://EditorScripts/validate_level_art_render.gd -- <独立对照 png 目录>` | PASS、退出 0；每关 2,737,600 像素覆盖全高、所有石墙。预乘颜色/alpha 平均差 0.3884% / 0.2467% / 0.2140%，严重差异像素 0.01286% / 0.00153% / 0.00011%，均在原生 Flash/Godot 亚像素渲染余差内；independent_pixels.log |
| OpenGL `--script res://EditorScripts/validate_official_levels.gd -- <official_levels.json> --snapshots` | PASS、退出 0；三关全部贴图矩阵/原点、完整地形、47 条真实 Luban 配方的敌人/数量/首次等待/重复间隔、生成点、阶段与导出绑定通过。单次配方无后续间隔，不因无效值制造差异；scene_final.log，15 张取景图已抽查两组桃花源石墙 |
| OpenGL `--script res://EditorScripts/validate_hud_bar_alignment.gd` | PASS、退出 0；真实 BattleForm 中 10%/50%/100% × 1/1.25/2 倍缩放，共九组，黄条与红条露出底框的像素数均为 0；bars_fixed.log，满/半值及实际守关截图已查看 |
| `dotnet build <隔离工程>/GodotProject.csproj --no-restore -v quiet` | 0 错误、5 项既有框架警告；build_final.log。新增 C# 仅为编辑器只读配方审计，不增加玩法观测 API |
| `dotnet test Tests/LevelTests/LevelTests.csproj --no-restore -v quiet` | 18 项通过，level_tests.log；本轮未改规则或数值表 |
| OpenGL `--smoketest=progress` | 三关实际通关、胜利音乐、出口、检查点、标题重读与连续解锁 PASS、退出 0；progress.log |
| 最终花果山 `--smoketest=level` | 恢复并按配方配对官方坐标后再次 PASS、退出 0；五波、物理门、守关血条、出口与地图重读通过，level1_final.log |

早期配方审计发现花果山第二波稳定 ID 的显示顺序与怪物配方顺序不同，已按猴子五只/妖猴单只配对恢复，最终 47 条审计通过。独立画面比对首次还发现前景 0.92 透明度，恢复后通过；不以放宽断言覆盖问题。一次原生保存完成打印 PASS 后在 Godot Array.Finalize 关停崩溃（-1073741819，save_recipes.log），最后原生保存退出 0（save_local_registration.log）；运行回归无该崩溃。既有证书读取、WebRequest 关停 ObjectDisposedException、ObjectDB/RID 泄漏继续单列，未扩大插件修改。

## 结算音乐、光晕、提示框与三关地形（2026-10-20）

依据第 10/30/40/50 章及本轮视觉、碰撞校正要求实施。三关场景经隔离 Godot 编辑器原生保存，保留 13 份现有场景/资源 UID；未修改 Framework、addons 或 Git 索引。第 40 章关于正式花果山旧地板路径的描述与本轮替换不符，实际资源与原点登记在 OfficialReference，规范正文保留。

日志集中在 `.godot/validation/terrain_presentation/`。引擎使用 `skills_runtime/project` 隔离工程、隔离 APPDATA 和每次唯一的 Validation 存档；正式开发档本轮没有再次转存。

| 命令 / 验证范围 | 结果 |
| --- | --- |
| `dotnet test Tests/BattleTests/BattleTests.csproj --no-restore -v quiet` | 205 项通过；新增 60/120Hz 上坡速度不累计为起跳的回归，battle_final.log |
| 同方式 `Tests/ProfileTests` / `Tests/LevelTests` | 179 / 18 项通过，合计 402 项 |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` | 0 错误，6 项既有依赖/联网警告；formal_build_final.log。隔离工程构建同样通过 |
| OpenGL `--smoketest=ui` | PASS、退出 0，ui_convex.log。两关 × 60/120Hz × 双向 × 走跑共 16 条实际英雄 FSM 路线通过；全程贴地、不切 Air，停步、主动跳跃、三块单向平台及 S+K 下穿正常。长装备说明实际换行、穿脱/技能/绑定/池复用/失败清理通过；失败结算唯一 GGF Music 代理播放失败曲 |
| OpenGL `--smoketest=progress` | PASS、退出 0，progress_convex.log。新版连续地形下实际通关三关，出口、唯一胜利音乐、结算暂停、自动保存、返回地图及标题重读后进度 1/2/3 全部通过 |
| OpenGL `--smoketest=bosses` | PASS、退出 0，bosses_snap.log；两 BOSS 五招、实际伤害、无敌、独立风刃及回池通过 |
| `--script res://EditorScripts/validate_official_levels.gd -- <official_levels.json> --snapshots` | PASS、退出 0；三关源几何/连续土坡、局部顶点、平台、水体、导出绑定与出生流核对；levels_convex.log，截图已查看 |
| 编辑器 `--script res://EditorScripts/validate_level_scene.gd` | PASS、退出 0；Tool 条目、保留的项目出生点、相机、门及新连续地形通过，level_slots_final.log |
| `--script res://EditorScripts/audit_actor_registration.gd` | PASS、退出 0；六类角色的身体/受击范围符合源 colipse，312 个待机/走跑姿态记录在 actor_audit_final.log |
| OpenGL `validate_musou_flash.gd` / `validate_skill_layout.gd` | PASS、退出 0；19 帧全部像素 alpha 与 GPU 一致，158×158 每条边完全透明；最亮帧光晕、造三五入口、无双黑底板和技能位置已看图核对 |
| `export_official_level_art.py --xml <main.xml> --java <java> --ffdec <ffdec.jar> --work <忽略目录> --output <忽略目录>` | 重跑退出 0，22 张地形/出口 PNG 与正式资源逐字节相同；repro.log |
| Luban / Collection Res | 源表经直接 Luban 导出成功；原生 Collection Res 生成 518 项资源。旧 11 帧出口及旧导入入口删除 |
| 已安装 XLSX Diff worker，值/公式两模式 | Resources.xlsx 均识别 14 格、__enums__.xlsx 均识别 6 格；默认命名空间检查通过 |
| `validate_game_references.py` / C# whitespace verify / `git diff --check` | 604 条资源路径、291 个脚本 UID 通过；C# 格式与差异空白检查通过 |
| 正式编辑器 `--headless --editor --path Godot/GodotProject --quit-after 120 --no-window` | 冷启动退出 0，formal_cold.log；既有 ComponentInsoector 的 RefCounted/free 关停错误仍在 |

最初分离的坡面/平台会卡侧边；仅焊顶点、额外 ApplyFloorSnap 均未消除完整回归中的偶发失去接触，最终改为每座土坡一个连续凸轮廓，并撤掉额外运行时贴地代码。最终 UI 和三关回归以上表完整结果为准，不计之前失败日志为通过。

正式编辑器“构建完成后立即 --quit”有一次在关停时出现 Godot Array.Finalize 原生崩溃（退出 -1073741819，formal_editor.log），隔离保存中也曾出现；最终原生保存退出 0，常规冷启动退出 0。未将其当作正常退出，也未扩改插件。根证书读取、WebRequest 关停 ObjectDisposedException 和 ObjectDB/RID 泄漏仍单列；UI 的坏档/缺绑定是既有预期故障注入。PASS 不表示依赖日志零错误。

## 敌人继承、进关外观、关卡进度与受击音效（2026-10-20）

依据第 00/10/30/40/50/70 章及本轮用户明确的 Enemy → Monster/Boss 继承要求整理。公共身体与 AI 契约归 Enemy；小怪概率决策、首领共同时钟各归实际分支，具体 BOSS 保留专属招式。编辑器移动代码/场景/动作/贴图并保留 UID，引用同步到源表、生成配置、资源常量和节点池。未修改 Framework/addons 或 Git 索引。

当前日志集中于 `.godot/validation/progress_audio/`；真实引擎使用隔离工程和隔离 APPDATA，烟测存档为 `user://Validation/<模式>/<唯一编号>`。

| 验证命令 / 范围 | 结果 |
| --- | --- |
| `dotnet test Tests/BattleTests --no-restore -v quiet` | 203 通过；共同身体状态、AI 预设及首领决策 |
| 同方式 `Tests/ProfileTests` / `Tests/LevelTests` | 179 / 18 通过，合计 400 项；新增连续解锁、历史重玩不倒退、失败/放弃不推进、进度存盘/同绑定恢复及缺字段拒绝 |
| `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` | 0 错误、5 项既有依赖警告；actual_build.log |
| 正式工程 `Godot4CSharp_console.exe --headless --editor --path Godot/GodotProject --build-solutions --quit --no-window` | 扫描/构建完成，退出 0；没有 GCHandle 或资源丢失，关停仍有既有 ComponentInsoector 的 RefCounted/free 错误；actual_editor.log |
| 隔离工程 `--rendering-method gl_compatibility -- --smoketest=ui` | PASS、退出 0；complete_ui.log。六类实体零击退/致死受击均发声、hurt 动画不重复；粗糙行者服穿戴后保存并重新进关的 OnShow 通过，既有技能、穿脱、交易、UI 绑定、暂停、结算及失败清理通过 |
| 同方式 `--smoketest=bosses` | PASS、退出 0；两位 BOSS 五招实际命中、旋风 12 次/雷云 6 次、无敌、风刃寿命及池复用；complete_bosses.log |
| 同方式 `--smoketest=progress` | PASS、退出 0；实际通关三关并分别退出标题重读，进度 1/2/3、最新地点 2/3/3，逐个检查仅最新光效可见、未来地点直接信号也被拒绝；complete_progress.log |
| 同方式 `--smoketest=frontend` | PASS、退出 0；开始/六格档案/选角/地图/返回、保存提示和 GGF 声音；complete_frontend.log |
| `--headless --path <隔离工程> --script res://EditorScripts/validate_anim_libraries.gd` | 六类角色通过、退出 0；BOSS 非特效动作补齐 animation/frame 默认轨道，原招式时点不变；final_animations.log |
| `--headless --editor --path <隔离工程> --script res://EditorScripts/validate_hero_appearance.gd` | PASS、退出 0；冷加载→原生保存→重载保留 3 件防具、2 件武器映射；appearance_roundtrip.log |
| Collection Res / 既有 NodePool 扫描器 | 495 项资源和现有池配置重新生成；删除两份无引用的同名旧动作库，避免收集被 basename 冲突中断 |
| 已安装 XLSX Diff worker（普通/公式模式） | 两模式准确识别 Resources.xlsx 的 5 格场景路径变更，其余表值不变；Luban 直接运行退出 0；luban.log |
| `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry` | 582 条路径、290 个 UID、23 个原有关卡几何节点通过；额外与索引核对的 72 个迁移标识不变 |
| 变更手写 C# `dotnet format whitespace . --folder --include <文件> --verify-no-changes`；`git diff --check` | 67 个变更手写文件通过；77 条文档本地链接存在 |

正式第 1 格按用户本轮确认“备份转存，从花果山解锁”处理：完整备份位于 `%APPDATA%/Godot/app_userdata/GameZMXY/GameData.backup-before-v9-20261020-132525`。主档与 .bak 同步为 v9、HighestClearedLevelId=0，原累计经验 2040、灵魂 131、5 件装备、4 个已学技能及原穿戴 Guid 保留；正式 SaveDataUtility 只读校验两份均通过。转存前 SHA256 与已审原件一致，除此两新增/更新字段外 JSON 数据相同，无正式运行时兼容分支。证据 save_v9/applied.json、actual_validation.log、backup_validation.log。

额外将这份已转存的正式档复制到隔离 APPDATA，走真实标题→已有第 1 格→地图→花果山冷启动：只开放花果山，8 级悟空已有行者服与武器正确显示，HUD 正常打开，RESUME PASS、退出 0；resume_actual.log / migrated_profile_ingame.png。原件不由烟测写入。

回归初次失败曾来自测试仍按 Monster 前缀识别状态、夹具在关卡回滚前准备装备，以及致死伤害夹具未计等级减伤；已修正测试的真实前提并重新完成上述完整回归，未添加生产测试 API。既有根证书读取错误、WebRequest 关停 ObjectDisposedException 和 ObjectDB/RID 泄漏仍单列保留；UI 的损坏档与缺绑定为预期故障注入，PASS 不等于无错误关停。

## 第二、第三关、换装与目录（2026-10-19）

以下引擎命令均使用隔离工程 `Godot/GodotProject/.godot/validation/skills_runtime/project`（以 `<隔离工程>` 代称）与隔离 APPDATA/Validation 存档，未触发正式编辑器热重载。日志集中在正式工程 `.godot/validation/stages_2_3/`。

| 验证命令 / 范围 | 结果 |
| --- | --- |
| `dotnet test Tests/BattleTests --no-restore -v quiet` | 203 通过；新增两 BOSS 原版距离、秒制选招、冷却、受控恢复和死亡优先级 |
| 同方式 `Tests/ProfileTests` / `Tests/LevelTests` | 174 / 18 通过；装备随机范围、3% 直接概率、饰品所有权与存档、配方及阶段规则 |
| `dotnet build <隔离工程>/GodotProject.csproj --no-restore -v quiet` | 0 错误；NU1900 和框架声音弃用共 6 项既有警告 |
| `Godot4CSharp_console.exe --headless --editor --path <隔离工程> --build-solutions --quit --no-window` | 扫描和构建完成，退出 0；编辑器关停仍有既有 RID/ObjectDB 泄漏 |
| `Godot4CSharp_console.exe --path <隔离工程> --rendering-method gl_compatibility -- --smoketest=level2` | PASS、退出 0；水帘洞五波、48 只、312 经验、猕猴王血条、门与相机、W 出口、成功结算、检查点保存与地图重读 |
| 同方式 `--smoketest=level3` | PASS、退出 0；桃花源五波、49 只、361 经验、禺狨王血条及相同完整保存返回链路 |
| 同方式 `--smoketest=level` / `frontend` | 均 PASS、退出 0；花果山五波与入口导航回归 |
| 同方式 `--smoketest=bosses` | PASS、退出 0；两 BOSS 共五招均造成实际伤害，旋风 12 次、雷云 6 次；无敌、50 帧独立风刃寿命和池复用通过 |
| 同方式 `--smoketest=ui` | PASS、退出 0；技能身体/独立效果命中音经过 GGF；通用饰品的 3% 标签和 tooltip、卸下清理；猿甲战斗与背包纹理同步、全部基础动作逐帧、交易、暂停、重入和失败清理通过 |
| `validate_official_levels.gd` 与官方提取数据 | PASS；水帘洞 18 条出生流 / 25 个碰撞面，桃花源 15 条 / 6 个碰撞面；出生坐标与重叠战斗边界相符 |
| `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry` | 577 路径、285 UID、23 个既有几何节点通过；原生编辑器迁移脚本/场景，UID 保持 |
| 已安装 XLSX Diff 的 diff-load-worker，普通/公式两种模式 | 8 份变更工作簿均通过，含 Enemy 两张 Sheet 与 __tables__ 合并输入；中文、字段和单元格变化可读 |
| Collection Res / 既有 NodePool 扫描器 | 495 个资源；共享效果、风刃、掉落节点正确登记，仅经生成器更新 |
| 52 份变更手写 C# 的 `dotnet format whitespace . --folder --include <文件> --verify-no-changes` / `git diff --check` | 通过；生成的 MapForm.cs 保留 Ge 输出格式，不手动排版 |

已检查原生游戏截图：水帘洞与桃花源、顶部居中 BOSS 条、两 BOSS 的动作特效、地煞猿甲背包预览。图集检查修复烈焰闪残影误入身体层造成的矩形，保留独立特效；原点和既有帧号未改变。最初水帘洞烟测把低洼旧 Y 传送进较高地面导致掉落，另有出口斜坡缺少跳跃；修正测试摆放与输入后完整流程通过，未改官方碰撞面来迁就测试。桃花源无渲染器模式在成功结算暂停后未结束，已停止该次运行；上述通过结果来自实际 OpenGL 渲染模式。

经本轮用户明确授权，第 1 格 `1791793987.sav` 从 v7 一次性转存 v8：2040 经验、131 灵魂、5 件装备、4 个技能和穿戴保持，原有四项概率全为 0，无单位歧义。原目录完整备份到 `user://GameData.backup-before-v8-20261019-233717`；主档与 `.bak` 均经正式 `SaveDataUtility.Validate` 校验通过，目录摘要未改变，正式代码没有迁移分支。

运行日志仍报告既有根证书错误和框架 WebRequest 关停 ObjectDisposedException，预期 HUD/存档失败注入也保留记录；PASS 不表示零错误日志。唐僧尚未可玩，地煞权杖仅有官方图标、掉落和数值，唐僧换装随其实体实现；地图解锁进度、被动购买本轮未接入。

## GGF + QF 架构巡检（2026-10-19）

QF 使用准则复核：对照官方源码及教程第 09、10 节，确认 Query 可选、返回一次结果，持续绑定由 Model 提供；现有五类界面业务修改入口均走 Command。此次仅补清第 00 章、ProjectTour 和架构类型的说明，未改运行逻辑；重新执行 `dotnet test Tests/ProfileTests --no-restore -v quiet`，170 项通过，仍有 NuGet 离线审计及测试替身未赋值警告。本次未重跑下列既有引擎验证。

规范集中在第 00 章，命名、配置、实体、场景和存档章节同步当前契约；ProjectTour 记录六个 Model 的所有者、注册点及实际调用链，WorkingNotes 提炼设计判断。职责检查覆盖业务 Architecture、Model/System、UI、输入/身体/AI/技能、关卡/掉落、存档/流程及资源生成工具入口。

五类玩家修改请求经 Command 调用 System，整包出售汇总使用同步 Query；绑定独立观察变化。施放临时状态归本局 HeroModel，SkillModel 只留学习记录；拾取收益归 LevelSystem。窗口订退和池复用对称，装备格移除重复 ID，技能栏缓存固定节点并清空旧内容。SaveSystem 捕获异步周期引用、检查取消，ProcedureLoadGame 装配失败回收部分档案对象；关卡未初始化也可重复清理。存档必需 HeroId / TotalExperience 严格读取，版本保持 7。

| 命令 / 场景 | 结果 |
| --- | --- |
| `dotnet test Tests/ProfileTests --no-restore -v quiet` | 170 通过，包含 Command/Query、学习/施放作用域、拾取/回滚和缺失存档字段 |
| 同命令运行 `Tests/BattleTests`、`Tests/LevelTests` | 194 / 18 通过，合计 382 项 |
| `dotnet build Godot/GodotProject/.godot/validation/skills_runtime/project --no-restore -v quiet` | 0 错误、6 个既有依赖/网络警告；`architecture_lifecycle/build_final.log` |
| 引擎 `--path <隔离工程> --rendering-method gl_compatibility -- --smoketest=frontend` | PASS、退出 0；菜单、保存反馈、绑定、回滚、取消读档、部分注册失败回收与重读；`frontend_verified.log` / `.exit.txt` |
| 同方式 `--smoketest=ui`，正式完整驱动 | PASS、退出 0；穿脱/出售/查询、技能/输入、无双、拾取/物理、结算/重试、订退复用、打开失败清理及存档重入；`ui_verified.log` / `.exit.txt` |
| 同方式 `--smoketest=level-cancel`、`level` | 均 PASS、退出 0；未初始化重复清理、在途取消/晚到回收、五波关卡、出口保存及地图重读 |
| 引擎 `--headless --path <隔离工程> --script res://EditorScripts/validate_anim_libraries.gd` | 四类演员的动作、方法入口和轨道契约通过、退出 0；`animations.log` |
| `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry` | 511 条路径、279 个 UID、23 个暂存几何节点通过 |
| 变更手写 C# 的 `dotnet format whitespace . --folder --include <文件> --verify-no-changes`、`git diff --check` | 通过；初次行尾检查失败后按 CRLF 格式化 |
| 文档本地链接与标题锚点检查 | 13 份文档、73 条本地引用通过 |

隔离项目先同步正式业务文件，等待完整构建退出，再启动引擎；APPDATA 为 `.godot/validation/migration_userdata`，存档为 `user://Validation/<场景>/<唯一编号>`。本轮详细日志位于 `.godot/validation/architecture_lifecycle/`；前一阶段日志保留在 `architecture_review/`，其中 `ui_complete_final.log` 才是当时完整驱动，其他专项日志按其标记解释。

本轮 `ui_complete.log` 首次因测试反射读取已删除的重复 ID 失败；夹具改读 EquipmentData.InstanceId 后，完整 `ui_verified.log` 通过，未恢复生产冗余字段。原生标记起点方法键的复现及 Seek 修复证据保留在 `architecture_review/marker_probe.log`、`marker_seek_probe.log`。

预期故障包括损坏档、过期档案写入、重复 EquipmentSystem 注册和缺失 BattleForm 绑定。根证书读取、WebRequest 关停 ObjectDisposedException 与关停 ObjectDB 泄漏警告仍存在，属于既有环境/依赖限制；未修改只读 Framework/addons、生成物、正式存档或 Git 索引。

## 场景路径大小写缓存（2026-10-18）

依据第 40 章命名与 UID 约定、第 50 章本地编辑器状态边界检查。正式资源均使用 level_1；旧路径来自 `.godot/editor/editor_layout.cfg` 的 open_scenes、project_metadata.cfg 的最近记录及旧名折叠状态。用户关闭编辑器后已备份并迁移相关缓存，保留标签、布局和已有新版折叠状态；不改场景、业务表、框架或插件，不提交本地缓存。

- 隔离副本复制实际打开/最近记录后，Godot `--headless --editor --quit-after 120` 冷启动复现恰好两条 Level_1 大小写警告；修正缓存后的同命令为零，两次退出均为 0。日志 `.godot/validation/scene_case/reproduce.log` / `isolated_fixed.log`；再次检查修复计划为空。
- 正式工程同命令连续冷启动两次，大小写警告均为 0，进程退出均为 0；退出后旧路径未写回，修复计划仍为空。日志 `scene_case/actual_fixed_1.log`、`actual_fixed_2.log` 及对应 `.exitcode`；缓存备份 `scene_case/before_editor_actual_20261018_163717/`。九个改名场景、project.godot 及用户正在调整的 item_appearances.tres 前后 SHA256 一致，UID 和内容保留。
- `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry`：511 条资源路径、274 个 UID、23 个暂存几何节点通过，旧名重复场景不存在。工具增加按磁盘真实名称检查路径各段大小写；正确路径、错误文件名大小写、错误目录大小写及不存在资源四种检查通过。
- 编辑器退出仍有既有插件释放错误和环境根证书读取错误；它们与本次大小写告警分开记录，未扩大依赖修补范围。

## Form 参数、快捷键与配置边界复核（2026-10-18）

依据第 10 章 FormData 契约、第 20/50 章源表与正常生成入口、第 40 章原生场景绑定实施。恢复 InventoryFormData，并为结算窗口增加 SettlementFormData；战斗 V 经 InputMap 和既有窗口请求开关技能页。九个业务场景改为 snake_case，保留 UID；框架直接绑定的 LoadingForm / QuestionTips 保留原名。未修改 Framework、addons、Tools/Luban 或玩家正式存档。

配置复核删除配方 EquipmentDropPoolOverride / UseMonster 哨兵、只影响血条的 FeaturedOpponentId 及未使用的 Rank / MonsterRank。奖励直接读 MonsterConfig：当前大猩猩基础装备池为 None，花果山仍为 3 倍生命、25 经验；血条由关卡场景绑定 stage_5_a。Schema、源表、事件和消费端同步，生成代码/bytes 只由 Luban 更新。

| 验证 | 结果 |
| --- | --- |
| `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat` | 退出 0，自动清理 MonsterRank 和 UID，更新关卡/怪物二进制；`form_navigation/boundary_luban.log` |
| `dotnet test Tests/BattleTests --no-restore -v quiet`、ProfileTests、LevelTests | 最新配置变更后 194 / 142 / 18，共 354 项通过，包含当前守关无装备奖励与真实序列化容量边界；`battle_final.log` / `profile_final.log` / `level_final.log` |
| `dotnet build Godot/GodotProject/.godot/validation/skills_runtime/project/GodotProject.csproj --no-restore -v quiet` | 恢复完整正式 UI 驱动后 0 错误、6 个既有框架/网络警告；`build_final.log`。构建隔离副本，避免触发正在打开的正式编辑器重载 |
| 实际 `--smoketest=ui` 专项 | V 开关、暂停输入、长按重复拒绝、关窗不重开、重映射通过；FormData 背包、五招/命中/音效/RESET、拾取、无双、窗口/实体复用、死亡动画结算与重试/地图通过，真实进程退出 0；`runtime_probe.log` / `.exitcode`。该专项发生在本次三个配置字段清理之前 |
| 最新实际 `--smoketest=level` | 五波、生成点/相机/物理门、场景绑定的居中血条、W 出口、成功页、地图重读通过，经验 204；真实进程退出 0，`level_runtime_final.log` / `.exitcode` |
| 原生场景保存与内容对照 | Godot 编辑器脚本生成顶部生命条绑定，仅合入该属性，保留原 UID、出口继承与几何；`bind_scene.log` |
| 原生 `validate_level_scene.gd` / `validate_anim_libraries.gd` | 最新场景绑定、Tool 条目、地形/相机及四类演员动画调用通过，退出码均 0；`scene_final.log` / `animations_final.log` |
| XLSX Diff 1.6.4 实际 worker | 全部 11 本在值/公式两种模式均可读；本次四本的前后 diff 均非空，变化格数 LevelConfig 23、Enemy 6、beans 10、enums 18，两模式一致。按行/列删除对照所有保留格 typed values 通过，其他 ZIP 部件保留；`check_xlsx_diff.mjs --boundaries` |
| `normalize_xlsx_namespaces.py <四本>` | 结构编辑后用既有工具恢复默认命名空间，最终 0 本需修复；未重建样式或其他 Sheet |
| `dotnet format whitespace ... --verify-no-changes` / `git diff --check` | 通过。当时引用扫描被旧名 WukongEntity.tscn 阻止；该重复场景现已不存在，后续大小写缓存修复已完成最终引用检查，结果见本页首节 |

日志、临时脚本和预览集中在 `.godot/validation/form_navigation/`。已查看表格及实际血条截图。首次 UI 专项因键盘 Device 的错误测试假设失败，已改为检查实际键位；另一次火魔斩等待超时，增加仅限隔离副本的诊断后复测通过，未更改玩法，不能声称修复了一个已定位的技能问题。专项 PASS 不代表完整 UI 所有旧失败已消失。

引擎仍有环境根证书读取及既有 WebRequest 关停异常；编辑器脚本退出仍有 ComponentInsoector 释放异常，分别记录，未修补只读依赖。正式编辑器保留旧打开路径时会重新生成旧名 WukongEntity.tscn，已请求关闭后清理；不将该重复文件当成新的正式资源登记。

## 动画入口与包装清理（2026-10-18）

按用户授权修订第 10/20/30/60 章：方法键统一 `AnimEvent` 后缀，声音直接调用 `PlaySoundAnimEvent(SoundId)`；状态仍负责分支、死亡收益及安全退出。删除 SkillData、音效宿主接口、施放身份转发与 InventoryFormData 单引用包装，补齐悟空场景遗漏的五段普攻列表和命中声。未修改框架、插件、生成物、源表或美术图集。

实际执行与结果：

- `dotnet test Tests/BattleTests --no-restore -v quiet`：194 通过；ProfileTests 142、LevelTests 18，共 354。连续受击改查动画重启，声音由真实引擎断言；保留旧落地回调不能改变复用状态的测试。
- `dotnet build Godot/GodotProject/.godot/validation/skills_runtime/project --no-restore -v quiet`：恢复完整正式调试代码后 0 错误，6 个既有依赖/网络警告。构建与运行都在隔离副本，不触发用户编辑器重载。
- 隔离项目执行 `--headless --script res://EditorScripts/validate_anim_libraries.gd`：四类演员的普通/技能/独立效果方法目标及后缀通过，退出码 0。旧的禁止方法轨道断言已删除，官方技能画面校验同步实际 Visual 层级。
- 原生对照 `check_callback_invariants.gd`：2424 个原值键、动画长度/循环/标记及 48 个原方法键时点和参数不变；只重命名入口，并为 hurt/death 各增加一个零时点声音键；原资源 UID 保留。
- 60Hz `--smoketest=ui` 专项：五招、七段命中×三类怪物、输入、真实 GGF 音效、连续两次受击/一次死亡语音、技能中断 RESET/普通动画恢复、拾取、背包操作/退订/复用、无双演出、死亡动画结算/重试/返回地图通过，进程退出码 0。专项仅在忽略的验证副本重排入口；技能命中夹具结束后重新冻结怪物并等待旧飘字结束，避免与后续恢复数字计数互相干扰，不改正式测试断言或玩法。
- `--smoketest`：33 次真实状态切换及全部断言通过，进程退出码 0，包含第四段松手不追加第五段。
- `validate_game_references.py --compare-staged-geometry`：511 条路径、273 个脚本 UID、23 处暂存几何通过；`dotnet format whitespace . --folder --include <本轮手写 C#> --verify-no-changes`、`git diff --check` 通过。

日志与退出码在 `.godot/validation/animation_callbacks/`。引擎仍报告环境根证书读取失败和既有 `DefaultWebRequestAgentHelper` 退出异常；编辑器导入退出仍有 ComponentInsoector 的 RefCounted Free 错误。以上与断言和进程码分别记录，本轮未扩大只读依赖修改范围，也不将专项结果称为完整 UI 全链路回归。

## 常用命令

```powershell
# 改源表后先导出；AI_MODE=1 跳过脚本末尾 pause
$env:AI_MODE = '1'
cmd /c Configs\GameConfig\gen_code_bin_to_project_lazyload.bat
dotnet build Godot/GodotProject --no-restore -v quiet
dotnet test Tests/BattleTests --no-restore -v quiet
dotnet test Tests/ProfileTests --no-restore -v quiet
dotnet test Tests/LevelTests --no-restore -v quiet
python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry

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

## 配置与原生动画迁移完成（2026-10-18）

依据第 10 章的职责/命名、第 20/50 章的源表与原生成入口、第 40 章的外置资源约定实施。用户本轮明确允许原生方法轨道，覆盖第 30 章旧禁令；规范正文未改。Framework/addons/Tools/Luban 与 Git 索引未改，存档测试使用独立 APPDATA 下的 `user://Validation/<模式>/<Guid>`。

完成删除 SkillAction/SkillEffect/SkillTimeline/Drop 四表，SkillHit 合入 Attack 后删除；五招、独立效果、普通攻击、声音、掉落、物品外观及 UI/结算时序的正式消费者同步迁移。24 个原 xlsx 归为 11 个工作簿、16 张业务 Sheet；Hero / Enemy 按用户最新要求分开。三张 Schema 继续独立，生成类型和保留数据 ID 不改名。

| 命令/证据 | 结果与覆盖范围 |
| --- | --- |
| `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat` | 最终 Luban 导出退出 0，自动清理五表 bytes、旧类/四个编排枚举及相应 UID；未手改生成物。`config_rework/final_luban.log` |
| `python .../.godot/validation/config_rework/audit.py` | 8767 格逐格 typed values、Sheet 绑定与嵌套合并表头通过；原数值保留，击退两轴统一 ×24 为 px/s。Profile 原文件未重写 |
| `node .../.godot/validation/config_rework/check_plugin.mjs` | 实际安装的 XLSX Diff 1.6.4 值/公式两种模式共 22 次通过：每个工作簿可读，所有 Sheet 名/数量和非空行列符合预期。`normalize_xlsx_namespaces.py <全部 xlsx>` 检查 0 份需修复 |
| `dotnet test Tests/BattleTests --no-restore -v:q`、ProfileTests、LevelTests | 194 / 142 / 18，共 354 项 PASS。删除旧表解释器测试，保留数值、支付、输入隔离、GGF 状态/中断/身份/回调回归；新增技能伤害不进入普通段索引检查。日志 `battle_migration_final.log` / `profile_migration_final.log` / `level_migration.log` |
| `dotnet build Godot/GodotProject/.godot/validation/skills_runtime/project/GodotProject.csproj --no-restore -v:q` | 恢复完整正式 UI 驱动后的最终构建：0 错误、1 个 NU1900 警告（此前完整编译另有既有框架警告）。已清除隔离副本里的 41 个过期源文件，避免误编译旧类。`skills_runtime/final_migration_build.log` |
| 原生 `check_skill_values.gd` / `restore_skill_uid.gd` | 1424 个原值键的时点、帧、几何、形状与动画长度通过；五个零时点声音键存在。22 个修改资源的原 UID 对照通过（主技能库 `j3uaow5vlljx`、普攻库 `dpdyqe7blxbqj`）；新增 ItemAppearancesResource UID 由 Godot 扫描生成后同步 |
| 60Hz、120Hz 实际引擎技能专项 | 两次 `SMOKE-SKILL-SYNC PASS`、退出 0：五招输入与单次扣蓝、持续 W+J、火魔有/无目标、落地等待和真实伤害、学习/图标/暂停/钱包/池复用、七段技能击退×三种怪物与六条实际轨迹、GGF 音效及原生方法绑定。最终日志 `skills_runtime/migration_combined_verified.log` / `migration_skills_120_verified.log`；进程对象 WaitForExit 后保存真实退出码于对应 `.exitcode` |
| 原生普攻专项 `--smoketest` | PASS、退出 0，观察 33 次切换：五段普攻、第四段松手不补第五段、走跑跳、命中/死亡及飘字。`skills_runtime/migration_normal_verified.log`；真实退出码 `migration_normal.exitcode` 为 0 |
| 表现与死亡动画专项 | `SMOKE-PRESENTATION PASS`、`SMOKE-DEATH-ANIMATION PASS`、退出 0：实际装备/恢复品贴地与外观、拾取/暂停/满包/过期/复用、背包穿戴/出售/分页、无双演出结束恢复暂停、小怪血条退订复用；正式死亡动画结束前不结算，完成后重试/返回地图/重入通过。`skills_runtime/migration_combined_verified.log`；真实退出码 `migration_combined.exitcode` 为 0 |
| Godot 隔离 `--headless --editor --import --quit` | 扫描完成、退出 0；C# 绑定和新资源可用，仍有既有 ComponentInsoector 退出释放异常。`skills_runtime/final_migration_scan.log` |
| `validate_game_references.py --compare-staged-geometry` / `git diff --check` / `dotnet format whitespace ... --verify-no-changes` | 511 个资源路径、274 个 UID、23 个关卡几何节点通过；空白与本轮 51 个手写 C# 格式检查通过。旧表/枚举/解释器的正式 C# 与场景引用扫描为零 |

技能专项用忽略目录的 `skill_knockback/prepare_impact_runtime.py` 选择现有技能检查；表现专项用 `prepare_presentation_runtime.py` 选择现有掉落/背包/HUD/复用和死亡检查，未添加生产观测 API。二者均在隔离副本运行，驱动最后的通用 UI PASS 文案不代表没有执行的完整 UI 部分通过。最终构建前已还原完整正式场景驱动。这里不声称此前“出生下落时出招”等无关完整 UI 失败已修复。

已查看实际学习页、九连斩、穿戴背包、无双演出后半段，以及 Hero / Enemy 表的预览。根证书读取错误、WebRequest 退出 ObjectDisposedException 和插件退出释放错误仍存在；均记录于上述日志，未扩大到只读依赖修补。支持脚本、临时表与截图均留在忽略的 `.godot/validation/`。

## 配置职责复核与原生动画方案（2026-10-18）

遵循第 10/20/30/50 章的职责、源表和隔离验证要求；本轮报告第 30 章禁止方法轨道与用户最新方向冲突，未直接修改规范正文。正式代码仅补齐具体技能向独立特效传入击退中心，通用节点不再默认所有效果都按中心击退，回池清空该数据。全表审查、动画迁移与工作簿合并方案见 ProjectTour；尚未实施这些配置迁移，也没有提前删除资源或手改生成文件。

| 验证命令 | 结果 |
| --- | --- |
| `dotnet test Tests/BattleTests --no-restore -v quiet`、ProfileTests、LevelTests | 195 / 157 / 18，共 370 项 PASS；在既有火魔目标/下冲/落地样例中检查中心坐标，拖尾不强制中心击退。已有 NU1900/CS8632 警告 |
| `dotnet build Godot/GodotProject/.godot/validation/skills_runtime/project/GodotProject.csproj --no-restore -v quiet` | 同步本轮正式脚本到既有隔离副本后 0 错误、6 个框架/NU1900 警告；不构建正式编辑器输出 |
| `python Godot/GodotProject/.godot/validation/skill_knockback/audit_source.py`、同目录 `check_xlsx_diff.mjs` | 官方 11 段字典、删列保值及其他 ZIP 部件通过；五张表的实际 XLSX Diff 值/公式两模式 10 项通过。本轮没有进一步改表 |
| Godot `--path Godot/GodotProject/.godot/validation/skills_runtime/project --rendering-method gl_compatibility -- --smoketest=ui --skills-only` | 技能学习、五招、两分支、七段击退×三类怪物、实际轨迹、音效及资源关联断言通过；驱动未识别 `--skills-only`，仍继续整套 UI，随后在 `CheckGroundCombatAsync` 的“怪物 1 出生下落时允许出招”断言 FAIL、退出 1。整套不计通过 |
| Godot `--headless --path Godot/GodotProject/.godot/validation/animation_plan --log-file <同目录>/engine.log --script res://probe.gd` | 独立原生 AnimationPlayer 探针 8 项 PASS、退出 0：零时点、24/60/120Hz 顺序、两种 Seek、停止中断、跨多个方法键、Deferred 时序。只验证原生 API，不代表正式技能已迁移；无框架或玩家存档依赖 |
| `dotnet Tools/Luban/Luban.dll --version` | 本地 4.10.2；官方文档确认 `SheetName@file.xlsx` 语法。正式多 Sheet 合并和合并后的导表/插件核对尚未执行 |
| `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry`、`git diff --check` | 472 路径、285 UID、23 个几何节点通过；空白检查通过 |

引擎日志为 `.godot/validation/skill_knockback/runtime_skill_rule_review.log`，原生探针与 JSON 报告在 `.godot/validation/animation_plan/`。首次沙箱探针有日志目录/证书读取错误；指定工作区日志并在完整本机环境重跑后无该错误。正式引擎退出仍有已记录的 WebRequest ObjectDisposedException 与 4 个 ObjectDB 泄漏；Framework/addons 未改。出生下落断言失败与此前整套 UI 的已知限制一并保留，未为此次规划改动无关怪物行为。

## 技能击退同步与规则归位（2026-10-17）

依据第 10/30 章状态职责、20 章源表及 50 章生成/验证边界，按用户最新要求让动作选择归具体状态，数值和资源留表。删除新加的 KnockbackFromCenter（SkillHitConfig 回到本轮开始时的完整原文件）；并清理 GroundOnly、ConsumesSecondAirJump、时间轴 Condition/Direction/Anchor 及三个无用枚举。地面资格沿用悟空登记的 CanStart；空中跳跃、风暴击退中心、火魔寻敌/落地窗口和生成位置由具体状态执行，公共时间游标不再判断落地。Monster.SuperArmor、UI 框架覆盖行为与素材镜像具有实际配置职责，保留。

原始技能字典无误，偏差在执行链：击退换算从旧项目 X=25/30、Y=15 改为两轴 24；三种怪物重力 980→864；补齐官方横向 0.8/源步的秒制衰减；三个 hurt 动画经原生 ResourceSaver 将 0.28→0.5 秒，UID/其余轨道不变。首步积分校正重现官方先位移后加重力，24/60/120Hz 的纵向位移均与源公式吻合；不添加质量系数。

| 验证命令 | 结果 |
| --- | --- |
| `$env:AI_MODE='1'; cmd /c Configs/GameConfig/gen_code_bin_to_project_lazyload.bat` | Luban 导出同步；三个废弃枚举及 UID 由原有生成流程清理，未手改生成代码 |
| `python Godot/GodotProject/.godot/validation/skill_knockback/audit_source.py` | 11 个官方伤害阶段的击退、伤害类型、命中预算和间隔一致；原脚本重力/减速路径无质量系数；删列后其余值及五张表其他 ZIP 部件不变 |
| `node Godot/GodotProject/.godot/validation/skill_knockback/check_xlsx_diff.mjs` | 实际 XLSX Diff 1.6.4 值/公式两种解析共 10 项 PASS；五表默认命名空间检查通过 |
| `dotnet test Tests/BattleTests --no-restore -v quiet`、ProfileTests、LevelTests | 195 / 157 / 18，合计 370 项 PASS；包括三档帧率的减速/纵向源轨迹、中心击退、状态指定方向与火魔落地窗口 |
| `dotnet build Godot/GodotProject/.godot/validation/skills_runtime/project/GodotProject.csproj --no-restore -v quiet` | 最终完整隔离副本 0 错误、6 个既有框架/NU1900 警告；此前专项截断副本另有不可达代码警告 |
| Godot `--path <隔离副本> --rendering-method gl_compatibility -- --smoketest=ui --skills-only` | `SMOKE-SKILL-SYNC PASS`、退出 0：五招真实输入、火魔两分支/命中、图标学习同步、声音与资源关联；七段悟空伤害×三种怪物实际入口及六条真实物理轨迹通过 |
| Godot `--path <隔离副本> --rendering-method gl_compatibility -- --smoketest` | 最终版本 PASS、退出 0：五段普攻、第四段松键不补第五段、走跑跳、实际命中/死亡与池化飘字通过 |
| `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry`、`dotnet format whitespace . --folder --include <本轮手写 C#> --verify-no-changes`、`git diff --check` | 472 路径、285 UID、23 个关卡几何节点通过；格式检查通过 |

真实引擎采样：猴子、妖猴、大猩猩的升龙挑高分别为 172.560 / 172.574 / 172.569px，源步理论顶点约 172.5px；突刺的轻微上浮均约 2.5px，横移约 140.09px。轨迹只比较无干扰单次受击；正式火炎突击仍有四次判定，实战距离由实际命中次数、地形和目标行为决定，不把一次命中样本当成整招距离。

证据在 `Godot/GodotProject/.godot/validation/skill_knockback/`，含 `source_audit.json`、`runtime_final.log` 和表格核对工具；六条轨迹在隔离工程 `.godot/validation/skill_impacts.json`。专项仅在忽略副本选取原有技能断言和新增击退断言，驱动器原来的整套 UI 汇总文案不代表未执行部分通过。退出仍有已登记的 WebRequest ObjectDisposedException 和 4 个 ObjectDB 泄漏；未修改 Framework/addons，也未修改 Git 索引或玩家正式档案。

## 左下菜单与无双底板（2026-10-17）

依据第 40 章资源装配、第 50 章原工具生成流程及用户明确的版本边界，保留造三左侧五入口，恢复 718.png 黑色长底板和旧条缩放；造一技能栏移至它上方。默认隐藏满值动画，运行时继续由 BattleMenuPanel.SetMusou 控制。本次没有修改战斗规则或新增生产观测 API。

| 验证命令 | 结果 |
| --- | --- |
| `python Godot/GodotProject/.godot/validation/battle_menu_review/audit_menu.py` | PASS：五个入口及题签节点与暂存版本一致；15 张入口图片与旧工程字节相同；底图、五题签与造三官方 OtherMatv3570 导出像素相同；黑底板与旧 718.png 字节相同 |
| Godot `--path Godot/GodotProject/.godot/validation/skills_runtime/project --rendering-method gl_compatibility --script res://EditorScripts/validate_skill_layout.gd` | PASS、退出 0：技能页、45px 空槽/已学图标、五入口、黑底板及空/半满/满值真实渲染；逐张查看画面 |
| 隔离编辑器 `--headless --editor --script res://EditorScripts/regenerate_collection.gd` | 原有 CollectionRes 生成器登记 462 项，新增黑底板资源常量；退出 0，仍有既有插件 Free/RefCounted 和资源泄漏日志 |
| `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry` | PASS：472 条路径、288 个 UID、23 个关卡几何节点 |

官方造一、造三完整 RoleInfo 导出、像素核对报告和引擎日志位于 `Godot/GodotProject/.godot/validation/battle_menu_review/`。本次画面位于隔离工程 `.godot/validation/skills_layout_hud_0.png`、`skills_layout_hud_1.png`、`skills_layout_hud_full.png`；只去掉测试实例业务脚本，按空/半满/满值设置显示，不读写玩家存档。这是场景装配验证，不替代下节已运行的真实技能学习专项；未重新运行无关的整套规则测试。

## 数据与战斗窗口统一命名（2026-10-17）

依据第 10 章命名、第 30 章攻击池所有权和第 50 章生成/验证流程，按用户本轮要求将 AttackInstance / EquipmentInstance 改为 AttackData / EquipmentData，并贯通参数、持有字段、集合及测试反射。InventoryBagPanel 使用 m_EquipmentDatas；ProfileModel 的唯一背包为 EquipmentDatas。保存字段、实例 Guid 和格式版本不变；配置范围校验收回 SaveDataUtility。GGF 窗口统一 Form，BattleForm 同步脚本/目录/场景、打开参数、UI 枚举、源表与资源常量；组件继续按 Panel / Bar 命名。规范旧示例未改写。

| 验证命令 | 结果 |
| --- | --- |
| `dotnet test Tests/BattleTests --no-restore -v quiet`、ProfileTests、LevelTests | 186 / 154 / 18，合计 358 项通过 |
| `dotnet build Godot/GodotProject/.godot/validation/skills_runtime/project/GodotProject.csproj --no-restore -v quiet` | 隔离副本 0 错误、5 个既有框架警告及 NU1900；未构建正在使用的正式编辑器输出 |
| `python Tools/ProjectMaintenance/regenerate_ui_form.py Godot/GodotProject/TheGame/UIs/battle_form.tscn --namespace GameLogic.UI`、Luban 原有导表入口、CollectionRes 原有生成器 | Ge 生成五项根绑定；Luban 保留枚举数值 ID；CollectionRes 生成 461 项，正式资源常量只新增名称/路径替换及生成时间 |
| `node Godot/GodotProject/.godot/validation/check_battle_form_xlsx.mjs` | XLSX Diff 1.6.4 的值/公式两种模式共四项通过：界面UI.xlsx 两格、__enums__.xlsx 一格变化；其他 ZIP 部件不变，保留默认命名空间 |
| Godot `--path <隔离副本> --rendering-method gl_compatibility -- --smoketest=frontend` | SMOKE PASS：真实启动、六格存档、选角、地图、BattleForm、回滚、提示复用与 GGF 声音通过 |
| Godot `--path <隔离副本> --rendering-method gl_compatibility -- --smoketest` | SMOKE PASS、退出 0：五段普攻、第四段松手不补第五段、走跑跳、实际命中与死亡、池化飘字通过 |
| Godot `--path <隔离副本> --rendering-method gl_compatibility -- --smoketest=ui --skills-only` | 技能专项 PASS、退出 0：五项学习即时同步 BattleForm 彩色图标；五招输入、持续升龙、跑攻、火魔斩两分支/命中、声音与资源关联通过，仅隔离副本选取原有技能断言 |
| `python .godot/validation/prepare_battle_icon_check.py` | 正式图集 11 格逐像素等于官方元件 328；命名核对阶段场景仅名称和换行变化，后续底板与布局修复见上节 |
| `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry` | 471 路径、288 UID、23 个几何节点通过；改名保留 UID |
| `dotnet format whitespace . --folder --include <本轮手写 C#> --verify-no-changes`、`git diff --check` | 通过；UTF-8 无 BOM、CRLF 检查通过 |

本轮引擎日志在 `Godot/GodotProject/.godot/validation/`：`battle_form_frontend.log`、`data_names_combat.log`、`battle_icons_skills.log`；已查看隔离工程 `.godot/validation/battle_icons_unlearned.png` 与 `battle_icons_learned.png`。官方 RoleInfo.setSkillIcon 对未学习技能保留灰色空槽，学习后切彩色；没有把空槽改成自制灰度技能图。后续已确认底板缺失与技能栏下移，修复及验证见上节。

技能专项按 `SMOKE-BATTLE-ICONS PASS` 判断覆盖范围，驱动器原有整套 UI 汇总文案不代表未执行断言也已通过。三次引擎日志仍有既有 WebRequest 关停 ObjectDisposedException，攻击/技能各有 4 个退出泄漏；前端的坏槽读取告警属于失败注入。CollectionRes 编辑器退出仍有已登记的 ComponentInsoector Free/RefCounted 与资源泄漏告警。完整 UI 的既有超时/恢复数字暂停失败继续保留下节记录；本轮未改 Framework、addons 或 Git 暂存内容。

## 技能输入、激活与具体状态分工（2026-10-17）

依据第 10 章职责与配套类型合并、第 30 章角色与状态分工，以及本轮 [沟通理解](WorkingNotes.md)，输入决策移出 IHeroBody。当前状态 TryGetActionInput 提供输入窗口，实体 MatchActionInput 匹配角色规则，状态消费请求并进行 GGF 转换。TryDropThroughPlatform 保留物理碰撞与位移能力；S+K 识别归输入系统。

本轮按后续反馈继续简化：删除 InputActionState / ButtonInput 快照，InputSystem 直接查询 Godot 的按下、按住和松开；消费仍按角色隔离。InputEventAction 是原生事件，本次物理帧采样无需先查 Input 再构造事件。Skill / SkillInstance / SkillExecution 行为基类已删除，原正式五招未实现其生命周期；只有测试派生类在使用，不能以这些测试为理由保留空壳。SkillData 与 SkillDefinition 随 SkillModel 定义，前者仅保存施放身份与四项数据，后者登记状态、动作和条件。技能工厂、活动集合、后台调度与空生命周期一并移除；ISkillActor 保留节点能力契约并继承原脚本 UID。具体行为和后续输入归 GGF 状态，独立特效由节点推进。未修改配置、存档格式或生成内容。

| 验证命令 | 结果 |
| --- | --- |
| `dotnet build Godot/GodotProject/.godot/validation/skills_runtime/project/GodotProject.csproj --no-restore -v quiet` | 隔离副本 0 错误；5 个既有框架警告及 NU1900。当前工程编辑器打开，未构建其输出目录 |
| `dotnet test Tests/BattleTests --no-restore -v normal` | 186/186；第五绑定、消费隔离、普攻规则、真实 GGF 五招与多阶段交接通过；阶段输入直接读取原生查询契约 |
| `dotnet test Tests/ProfileTests --no-restore -v normal` | 154/154；登记所有权、错误类型/重复绑定的原子拒绝、单次扣费、取消和新旧施放身份、同帧双边沿查询、正式五招时间轴与火魔斩旧回调隔离通过 |
| `dotnet test Tests/LevelTests --no-restore -v quiet` | 18/18；合计 358 项通过，未跳过测试。移除仅验证旧行为壳派生实现的样例，真实玩法回归保留 |
| Godot `--headless --editor --path <隔离副本> --build-solutions --quit --no-window` | 脚本扫描和构建均 DONE；退出触发既有 ComponentInsoector 的 Free/RefCounted 错误并滞留。核对 PID 63776 与完整工程路径后仅停止该验证进程；不记为正常退出通过 |
| Godot `--path <隔离副本> --rendering-method gl_compatibility -- --smoketest=ui --skills-only` | 技能专项 PASS、退出 0：现有技能页/学习断言、五招真实输入、持续 W+J、跑攻、火魔斩两分支/命中/输入不排队、七段 GGF 音效和 47 条事件资源关联均通过。仅隔离副本增加截断条件，保留原断言，不执行其余 UI 测试 |
| `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry` | 471 路径、288 UID、23 个几何节点通过 |
| `dotnet format whitespace . --folder --include <本轮手写 C#> --verify-no-changes`、`git diff --check` | 通过；UTF-8 无 BOM / CRLF 检查通过 |

本轮日志在仓库根 `.godot/validation/skills_runtime/`：`skill_data_build.log`、`skill_data_battle.log`、`skill_data_profile.log`、`skill_data_level.log`、`skill_data_scan.log`、`skill_data_engine.log`、`skill_data_format.log`。专项从原 UiSmokeScenario 中仅选择 CheckSkillsAsync；准备脚本为 `.godot/validation/prepare_focused_skills.py`，隔离补丁为 `focused_skills.patch`。正式调试脚本只同步取消方法名称，不增加测试开关或生产 API。

本轮技能专项通过，仍不能声称整套 UI 通过。此前 `input_names_ui.log` 在升级动画 Frame ≥ 22 等待处超时，退出 1；`input_names_ui_headless.log` 在恢复数字暂停断言失败后未正常退出，原因仍未定位。既有 Framework WebRequest ObjectDisposedException 与 4 个 ObjectDB 退出泄漏仍在本轮技能专项退出时出现。没有修补框架或插件，也未修改 Git 暂存内容；规范正文未改写。

唐僧目前只有目录和伤害定义，八戒、沙僧尚未接入正式招式；输入和技能机制测试不等于这些角色已经制作完成。

## 悟空技能实战与左下栏恢复（2026-10-16）

- 五招按官方源时间轴接入 SkillSystem/SkillModel：学习与不足提示、扣蓝、升龙取消和持续 W+J、跑攻、U/I/O、独立特效与火魔斩两分支。火魔斩禁用输入时仍推进游戏秒数，输入不排队。UI 观察唯一技能书，GameData v7 只新增已学技能 ID；无旧版运行时兼容。
- 技能面板对照 OtherMat_v9 完整渲染恢复原坐标与 FZCuYuan 字体；战斗图标来自独立 45px 元件。五个旧菜单按钮、题签、底图和位置恢复，按钮及题签与暂存版本逐块相同，法宝/宠物保留；仅无双与五个主动技能图标换官方资源。移除旧无双 shader/图集及确认未引用的旧页码等素材，保留应用图标。素材表与导出工具同步。
- `dotnet test Tests/ProfileTests/ProfileTests.csproj --no-restore -v quiet` 140/140；BattleTests 165/165；LevelTests 18/18。主工程 `dotnet build Godot/GodotProject/GodotProject.csproj --no-restore -v quiet` 0 错误、5 个框架警告；隔离副本另有 NU1900 网络警告。
- OpenGL `--smoketest=ui`、`level`、`frontend` 均 PASS、退出 0；涵盖购买、HUD/技能页同步、五招真实输入、火魔斩命中/缺失、七段 GGF 声音、五波通关、结算与当前格式重读。怪物前摇夹具只隔离已显示的旁观怪物，避免技能测试后场上其他怪物干扰伤害计时；池中隐藏实体不被冻结。日志位于 `.godot/validation/skills_runtime/`。火魔斩输入锁最终复测同样 PASS、退出 0，期间的 J/U 按键不补发；资源重扫后应用图标 UID 错误已消失。
- `validate_skill_layout.gd` 与 `validate_musou_flash.gd` OpenGL PASS、退出 0，原版 19 帧全部像素 alpha 与 GPU 输出一致。最亮帧角落低 alpha 柔光保留，不当成不透明底色。Collection Res 经既有生成器更新；资源检查为 471 条路径、284 个 UID，23 个几何节点与暂存版本一致；XLSX 默认命名空间检查及 `git diff --check` 通过。主工程与隔离副本扫描完成，既有插件 Free/RefCounted 与关停 WebRequest 错误仍单独记录，不声称无错误运行。
- 正式开发档已按授权备份至 `GameData.backup-before-v7-20261016-183208`，一次性保留累计经验 1428、灵魂 1179、11 件装备与穿戴引用；主档和 `.bak` 均经正式 DTO/SaveDataUtility 校验通过。只添加空学习记录，目录元数据不变。

依据第 10/40/50 章执行代码、资源与验证约定；第 40 章旧 shader、第 70 章旧版本描述由用户后续指令覆盖，规范正文未自行修改。

## 地面物品透明留白贴地（2026-10-16）

`GroundItemNode` 按非透明区域底边与实际碰撞体底边对齐，考虑图标缩放，通过 Offset 保留领取动画的 Position 轨道。袈裟/行者服原先悬空约 4.9 像素；原图、32×40 碰撞盒、接触范围和领取规则不变。规则依据第 40 章的资源与动画职责、第 50 章的真实引擎验证；没有修改规范正文或只读资源。

当前工程编辑器打开，以下构建及引擎检查在 `.godot/validation/ground_alignment/project` 隔离副本执行，避免 C# 热重载；使用烟测独立存档。

| 验证 | 结果 |
| --- | --- |
| `dotnet build <隔离副本> -v quiet` | 0 错误、5 个既有框架警告 |
| Godot `--path <隔离副本> --rendering-method gl_compatibility -- --smoketest=ui` | SMOKE PASS、退出 0；逐像素检查全部 8 种装备、3 种恢复品与池复用的可见底边；落地、拾取上移淡出、暂停、满包、过期及背包回归通过 |
| `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry` | 441 路径、278 UID、23 几何节点通过 |
| `dotnet format whitespace . --folder --include <本轮两个 C# 文件> --verify-no-changes` | 通过 |

已人工检查 `ground_alignment/ground_pickups.png`，各图标贴地；日志为同目录 `build.log`、`ui.log`。UI 缺绑定仍为预期失败注入；既有 Framework WebRequest 退出释放异常及 4 个 ObjectDB 泄漏仍在，本轮未修改框架。贴地修复无未完成项。

## Bindable 简化与 QFramework / GGF 核对（2026-10-16）

依据用户最新要求，删除全部手动补发通知 API 和旧/新双值回调；HP、最终属性、无双、换装、出售、回滚均使用正常赋值和集合操作。Property、List、Dictionary 的 Register / RegisterWithInitValue 统一为单参数，整数 priority 默认 0，越大越先执行，同优先级按注册顺序。只共用内部订阅列表，没有 BindableEvent、优先级枚举或第二个全局事件系统。

对照 QFramework 官方核心和 BindableKit 源码恢复列表标准操作、Move 和逐项通知，补充泛型 BindableDictionary、键比较器、转换方法及实时只读回调视图。集合不再为每次通知复制前后快照；字典输入复制，避免外部写入绕过通知。不移植 Unity/EasyEvent、旧格式化序列化或 QFramework IOC。Model/System/Utility 仍由 GGF 主流程 FSM GetData/SetData 持有与访问。

HeroSystem 成长/穿戴同步的优先级为 100，UI 默认 0；新增 UI 先注册的实测，证明派生属性先同步。删空 AbstractUtility，工具直接实现 IUtility；SaveDataUtility.Capture 显式接收 ProfileModel，不查询 Architecture；ExperienceCurveUtility 独立注册，不在 ConfigurationModel 重复持有。扫描 195 份手写游戏 C#，核对 5 个 Model、10 个 System、4 个 Utility 及流程注册/移除：未发现 Model 查询 Model/System 或 Utility 反向查询架构。

| 验证 | 结果 |
| --- | --- |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | 0 错误，5 个既有框架警告 |
| `dotnet test Tests/BattleTests --no-restore -v quiet` | 165/165；死亡、无双重置、属性重算与动作规则通过 |
| `dotnet test Tests/ProfileTests --no-restore -v quiet` | 118/118；整数极值优先级、稳定顺序、重复退订、分发中增删/嵌套赋值、初始化失败清理、标准列表/字典操作、只读视图、坏输入和真实换装/回滚通过 |
| `dotnet test Tests/LevelTests --no-restore -v quiet` | 18/18；共 301 项通过 |
| OpenGL `--smoketest=ui` | SMOKE PASS、退出 0；数值、换装、出售、无双、暂停、退订与池复用通过 |
| OpenGL `--smoketest=frontend` | SMOKE PASS、退出 0；启动、六格档案、地图、保存、回滚、作用域与生命周期通过 |
| OpenGL `--smoketest=level` | SMOKE PASS、退出 0；五波、结局导航、自动保存与地图重读通过 |
| F6 `--headless --scene res://TheGame/Scenes/test_arena.tscn --quit-after 360` | 玩家与两只怪物就绪，独立装配正常，退出 0 |
| `validate_game_references.py --compare-staged-geometry` | 441 路径、278 UID、23 几何节点通过 |
| `dotnet format whitespace . --folder --include <手写文件清单> --verify-no-changes` | 214 份手写 C# 格式通过；UTF-8/CRLF、文档链接与 git diff --check 通过 |

代码全仓确认没有手动通知 API、双值绑定、BindableEvent、AbstractUtility 或旧字段反射依赖；烟测反射适配在测试侧完成，没有新增生产测试接口。459 个框架、插件和生成文件 SHA-256 未变；没有暂存或提交。第 00/10 章仍描述旧架构及整批 Changed 约定，本轮依据用户最新要求采用 Model/System 和单值同步绑定，未自行修改规范正文。多个独立状态源依业务顺序通知，不再承诺它们在每个属性回调中都同时改变。

日志在忽略的 `.godot/validation/qf_binding_review/`。既有 NU1900、CS8632 与 Framework WebRequest 关停 ObjectDisposedException 仍在；UI/frontend/level/arena 分别有 4/3/4/3 个退出对象泄漏。编辑器扫描退出 0，仍有 ComponentInsoector 的 RefCounted 释放报错；未修改插件。坏存档、旧档案写入拒绝和 HUD 缺绑定为失败注入，不将完整日志称为零错误。

以下条目保留历史验证结果；当前绑定契约以上节和 ProjectTour 为准。

## Model 写入职责与全项目 C# 整理（2026-10-16）

Model 只保存数据与查询，System 直接修改绑定属性。删除 Profile/Hero/Level/Save/Configuration Models 中的业务修改方法与对应纯转发；保留完整事务提交顺序。关卡规则测试调用正式 LevelSystem，仅用测试侧 SaveSystem 替身隔离磁盘；未增加生产测试 API。

| 验证 | 结果 |
| --- | --- |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | 0 错误，5 个既有框架警告 |
| `dotnet test Tests/BattleTests --no-restore -v quiet` | 165/165；新增无双三字段提交后通知与相等值不重复通知 |
| `dotnet test Tests/ProfileTests --no-restore -v quiet` | 106/106；正式 System 奖励/穿戴/回滚、关卡计时与冻结、重复开始/结局保护 |
| `dotnet test Tests/LevelTests --no-restore -v quiet` | 18/18；合计 289 项通过 |
| Godot `--headless --editor --build-solutions --quit --no-window` | 脚本扫描及构建完成，退出 0 |
| OpenGL `--smoketest=frontend` / `ui` / `level` / `level-cancel` | 四组 SMOKE PASS、退出 0；保存回滚、背包绑定与复用、整关结算和加载取消通过 |
| F6 `--headless --scene res://TheGame/Scenes/test_arena.tscn --quit-after 360` | 玩家与两只怪物就绪，独立模型/系统装配正常，退出 0 |
| `validate_game_references.py --compare-staged-geometry` | 441 路径、278 UID、23 几何节点通过 |
| `dotnet format whitespace . --folder --include <手写文件清单> --verify-no-changes` | 212 份手写 C# 全部通过；UTF-8 无 BOM、CRLF 通过 |

成员按字段、事件、属性、构造、生命周期、其他方法与嵌套类型归类，共整理 78 个类型。Roslyn 保留成员内容并核对语法标记；11 组跨字段/属性初始化顺序变化均无依赖（测试实例独立初始化、SaveModel 常量），构建与运行验证通过。相对暂存版本，180 个文件语法标记不变，31 个为上述业务及测试调整，1 个为测试磁盘替身；459 个框架/插件/生成文件的 SHA-256 与 Git 索引均不变。

日志与手写清单在忽略的 `.godot/validation/model_writes_{build,battle,profile,levelrules,scan,frontend,ui,level,cancel,arena,format,editorconfig,token_audit}.log`、`layout_manifest.json`。UI 测试同步改读现有内部关卡属性，不再反射已移除的旧字段。

仍有既有 NU1900 网络警告、BattleTests CS8632 警告、ComponentInsoector 编辑器退出释放错误及 Framework WebRequest 关停 ObjectDisposedException；frontend/UI/level/cancel/F6 分别报告 3/4/4/4/3 个退出泄漏。HUD 装配失败、坏存档和跨槽拒绝是预期失败注入，不将完整日志称为零错误。本轮遵循集中规范的只读边界和验证要求；第 00/10/70 章旧 Model 修改方法、架构及绑定约定与用户最新方向冲突，按用户指令实现，规范正文未自行更改。

## Model 按生命周期归并与系统职责（2026-10-16）

本轮实现以用户最新确认的合并方向为准：本局属性、HP/MP/WS 合入 HeroModel；成长、穿戴、背包、灵魂直接合入 ProfileModel，不保留子 Model 或兼容别名。ConfigurationModel、SaveModel、LevelModel 因数据归属和生命周期独立而保留。主流程 FSM 仍是唯一注册持有者。

HeroSystem 承担属性汇总、成长/装备联动、资源与无双规则；ProfileSystem 承担奖励、装备归属校验和检查点回滚；SaveSystem 接管手动保存的状态与过期结果校验。Model 保留内部 CRUD 和完整提交，对外只读，UI/Entity 修改经系统。删除正式 Model 中仅测试使用的三个恢复入口，夹具移到 Debug/ProfileTestData.cs，纯测试链接同一文件。导航回调与直接 GGF UI 调用的区别见 ProjectTour。

| 验证 | 结果 |
| --- | --- |
| `dotnet build Godot/GodotProject --no-restore -v:q -nologo` | 0 错误、5 个既有框架警告 |
| `dotnet test Tests/BattleTests --no-restore -v:q -nologo` | 164/164；含死亡时 HP/无双完整提交、无双倍率开启/结束 |
| `dotnet test Tests/ProfileTests --no-restore -v:q -nologo` | 106/106；含换装、升级、已穿戴实例重复领取拒绝、回滚完整性与绑定身份 |
| `dotnet test Tests/LevelTests --no-restore -v:q -nologo` | 18/18；三组共 288 项通过 |
| Godot `--headless --editor --build-solutions --quit --no-window` | 扫描和构建完成、退出 0 |
| OpenGL `--smoketest=frontend` | SMOKE PASS、退出 0；六格保存重读、跨槽拒绝、模型同一引用、入关订阅与离关退订、回标题清理 |
| OpenGL `--smoketest=ui` | 最终构建 SMOKE PASS、退出 0；生命/魔法/无双绑定、穿脱与出售、预览、结算重试、取消、失败清理与重入 |
| OpenGL `--smoketest=level` | SMOKE PASS、退出 0；五波、守关条、出口、检查点和地图重读 |
| OpenGL `--smoketest=level-cancel` | SMOKE PASS、退出 0；在途取消及迟到实体回收 |
| F6 `--headless --scene res://TheGame/Scenes/test_arena.tscn --quit-after 360` | 最终构建退出 0；玩家及两只怪物正常显示，无启动/运行空引用 |
| `validate_game_references.py --compare-staged-geometry` | 441 条序列化路径、278 个 UID、23 个几何节点通过 |
| 本地 Markdown 链接、UTF-8/CRLF、`git diff --check` / `git diff --cached --check` | 通过；本轮未修改 Framework/addons/生成物/配置/规范正文，原暂存区保留 |

F6 初次验证发现合并注册块时漏掉配置、输入、战斗、AI、掉落系统，导致玩家 OnShow 空引用；补回注册并修正清理分支的括号后，重跑通过。日志保留最终成功运行。

日志：`Godot/GodotProject/.godot/validation/model_merge_{build,battle,profile,levelrules,scan,frontend,ui_final,level,cancel,arena}.log`。

已知异常单独记录：UI 故意破坏 HUD 绑定以验证失败清理；frontend 故意注入坏档和跨槽旧档请求；编辑器退出仍有 ComponentInsoector Free/RefCounted 错误，运行退出仍有 DefaultWebRequestAgentHelper.Reset ObjectDisposedException 和 ObjectDB 泄漏警告。这些不是零错误日志。包源不可达时单测有 NU1900，BattleTests 另有原有 CS8632。未修改只读框架或插件处理上述问题。第 00/10/70 章旧架构描述与本轮用户指令冲突，已报告并保留规范正文。

## 英雄逐项属性与 UI 回调（2026-10-16，历史记录）

按用户最新要求，HeroRuntimeModel 更名为 HeroAttributeModel，删除 ActorAttributes、HeroLoadout 和装配工厂。等级、HP/MP/WS 与全部 16 项最终数值各自可绑定；Model 管来源、重算与钳制，HeroAttributeSystem 协调成长和穿戴。升级/回滚同时更新基础和装备，只重算一次；属性通知读取完整结果。Entity 即时 GetModel，Show 不传数据，Hide 仅清理表现。SaveDataUtility.Capture / RestoreCheckpoint 改为调用期间借用 IArchitecture。

删除 MutateWithoutEvent；保留内部静默提交与显式通知，用于事务一致性。UiRequestKind / UiRequestEventArgs 删除，窗口通过具名回调交给流程；仍由 GGF 打开窗口。关闭清空回调，拒绝迟到通知重新绑定，池复用保持唯一绑定。未添加生产测试接口，测试通过现有属性和反射检查生命周期。

| 验证命令 | 结果 |
| --- | --- |
| `dotnet build Godot/GodotProject --no-restore -v:q -nologo` | 0 错误，5 个既有框架警告 |
| `dotnet test Tests/BattleTests --no-restore -v:q -nologo` | 162/162；16 项属性通知、相等值、修正公式、完整重算与已发攻击冻结 |
| `dotnet test Tests/ProfileTests --no-restore -v:q -nologo` | 105/105；换装/升级自动同步、Buff 保留、检查点回滚观察一致性；同时恢复等级/装备时属性只发布最终结果 |
| `dotnet test Tests/LevelTests --no-restore -v:q -nologo` | 18/18；三组共 285 项通过 |
| Godot `--headless --editor --build-solutions --quit --no-window` | 扫描、脚本注册及编译完成，退出 0 |
| OpenGL `--smoketest=ui` | SMOKE PASS、退出 0；最终代码下的穿脱、升级、资源绑定、结算重试、失败清理与重入通过 |
| OpenGL `--smoketest=frontend` | SMOKE PASS、退出 0；补充关闭零回调、重开唯一回调、迟到绑定拒绝；属性系统入关唯一订阅、离关完全退订 |
| OpenGL `--smoketest=level` | SMOKE PASS、退出 0；五波、守关条、出口、结算保存与地图重读 |
| OpenGL `--smoketest=level-cancel` | SMOKE PASS、退出 0；在途显示取消与迟到回收 |
| F6 `--headless --scene res://TheGame/Scenes/test_arena.tscn --quit-after 360` | 独立 FSM 的属性模型/系统就绪，玩家和两只怪物正常显示，退出 0 |
| `validate_game_references.py --compare-staged-geometry` | 441 条序列化路径、278 个 UID、23 个关卡几何节点通过 |
| 本地 Markdown 链接、UTF-8/CRLF、`git diff --check` / `git diff --cached --check` | 通过；暂存区保留，Framework/addons/生成代码与配置无本轮改动 |

日志位于 `Godot/GodotProject/.godot/validation/attributes_{build,battle,profile,levelrules,scan,ui,frontend,level,cancel,arena}.log`。测试仍有 NU1900 网络警告；编辑器既有 ComponentInsoector 退出释放错误、游戏关停 Framework WebRequest ObjectDisposedException 和 ObjectDB 泄漏未纳入修改。HUD 缺绑定、跨槽和损坏档案仍为预期失败注入，不将完整日志称为零错误。第 00/10/70 章旧架构约定与最新要求冲突，按用户指令实施，规范正文未自行改写。

## Model 归并与数据所有权（2026-10-16，历史记录）

以下是上一轮结果；ActorAttributes 容器、HeroLoadout 和 UI 请求事件已由上方实现取代。

按用户最新要求将英雄最终属性、等级、HP/MP/WS 合并为关卡注册的 HeroRuntimeModel，删除 Vitals、MusouGauge 和私有 ActorModel。档案分别注册 HeroProgressionModel、InventoryModel、HeroEquipmentModel；灵魂并入背包资产，不再保留 PlayerModel 聚合层或 Wallet。怪物保留普通实例状态，扣血直接刷新自身血条。初始化与借用边界见 [ProjectTour](ProjectTour.md#所有者与初始化位置)。

集合采用 BindableList，换装、出售和回滚先提交全部状态再通知；新增数量不变的装备替换和跨模型回滚一致性测试。SaveDataUtility 与 ExperienceCurveUtility 为共享 Utility；加载只返回 DTO 与来源，删除 SaveLoadResult。十二个导航/保存结果事件归并，持续状态走绑定，一次性跨职责请求/事实走 GGF Event。地面物品改为场景预设 32×40 盒；蓝球只回蓝，地面恢复品继续显示数字。

| 验证命令 | 结果 |
| --- | --- |
| `dotnet build Godot/GodotProject --no-restore -v:q -nologo` | 0 错误，5 个既有框架警告 |
| `dotnet test Tests/ProfileTests --no-restore -v:q -nologo` | 106/106，含集合身份与事务观察一致性 |
| `dotnet test Tests/BattleTests --no-restore -v:q -nologo` | 145/145，英雄资源与无双秒制规则通过 |
| `dotnet test Tests/LevelTests --no-restore -v:q -nologo` | 18/18 |
| Godot `--headless --editor --build-solutions --quit --no-window` | 完成扫描与编译，退出 0 |
| OpenGL `--smoketest=ui` | SMOKE PASS、退出 0；背包穿脱/出售/分页、资源绑定、怪物血条、恢复品/蓝球、池复用和失败清理通过 |
| OpenGL `--smoketest=frontend` | SMOKE PASS、退出 0；新档/读档、地图保存提示、放弃回滚、模型移除与重建通过 |
| OpenGL `--smoketest=level` | SMOKE PASS、退出 0；五波、守关怪、W 出口、结算保存和地图重读通过 |
| OpenGL `--smoketest=level-cancel` | SMOKE PASS、退出 0；在途显示取消、迟到怪物回收通过 |
| F6 `--headless --scene res://TheGame/Scenes/test_arena.tscn --quit-after 360` | 玩家与两只怪物就绪，退出 0；独立 FSM 注册模型、系统和工具 |
| `validate_game_references.py --compare-staged-geometry` | 441 条路径、279 个 UID、23 个关卡几何节点通过 |
| `git diff --check`、`git diff --cached --check` | 均通过；保留原暂存区，Framework/addons/生成代码无本轮改动 |

日志位于忽略目录 `.godot/validation/models_{build,scan,ui,frontend,level,cancel,arena}.log`。已查看最新 `ground_pickups.png`，图标底部贴地且没有名称或阴影。纯测试仍有 NU1900 网络警告和既有可空注释警告。编辑器退出仍有 ComponentInsoector 的 RefCounted/free 错误；游戏关停仍有 Framework WebRequest ObjectDisposedException 和 ObjectDB 泄漏。HUD 缺绑定、跨槽和损坏档属于预期失败注入，不能将完整日志称为零错误。第 00/10/70 章旧架构条款与本轮指令冲突，按用户当前要求实施，规范正文未自行修改。

## 地面物品与 Model 绑定（2026-10-15，历史记录）

以下记录当时实现；动态图标碰撞盒、Vitals 与聚合 PlayerModel 已由上方 2026-10-16 的实现取代。

按用户最新要求，用一个按图标非透明范围生成的矩形同时处理地形碰撞与拾取查询，删除额外 Area2D、脚底小盒、阴影及动画阴影轨。DropConfig 源表移除失效 GroundPickupRadius，再经既有 Luban 命令生成；其余单元格和 XLSX 包内文件保持不变，命名空间检查无需修复。

参考 QFramework 原源码实现当前使用的 BindableProperty：Value、Register、RegisterWithInitValue、UnRegister、默认相等比较及事务静默提交/通知。模型对外只读，钱包、成长、装备集合、穿戴、存档目录和关卡状态改为绑定；窗口关闭对称解绑。关联字段使用不可变快照，跨背包/穿戴/余额交易先提交再通知；存档 DTO 不变。没有移植 EasyEvent、注销句柄链或增加生产测试接口。第 00/10 章的禁用 BindableProperty / 统一 Changed 条款与本轮指令冲突，按最新要求实现，规范正文未改。

- `dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误、5 个既有框架警告。新增脚本经 Godot `--headless --editor --build-solutions --quit --no-window` 扫描、构建，退出 0；仍有既有 ComponentInsoector 的 RefCounted/free 退出错误。
- `dotnet test Tests/ProfileTests --no-restore -v quiet`：104/104；BattleTests：145/145；LevelTests：18/18。覆盖绑定初始化、相等值与 null、退订重绑、初始化失败清理、初始化期间更新、成长一致性、目录集合所有权、穿戴及出售事务和结局发布顺序。NuGet 漏洞服务不可达的 NU1900 与既有可空注释警告分别保留。
- 正式工程 `--smoketest=ui`：SMOKE PASS、退出 0。验证唯一形状贴合图标非透明范围且与拾取查询共用同一 Shape2D；实际落地、非英雄/异会话拒绝、满包保留及原地重试、暂停、0.8 秒上移淡出、过期和池复用均通过。背包实时灵魂、穿脱出售、HUD 成长/Go、技能钱包及窗口解绑重开通过。`ground_pickups.png` 已查看，物品底部贴地、无阴影。
- `--smoketest=frontend` 与最终表产物下的 `--smoketest=level`：均 SMOKE PASS、退出 0；覆盖存档目录关闭零绑定/重开一份绑定、设置关闭退订、读档重入、整关五波、前进提示、结局、检查点与地图重读。所有烟测使用隔离 Validation 存档。
- `python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry`：441 路径、292 UID、23 关卡几何节点通过；`normalize_xlsx_namespaces.py Configs/GameConfig/Datas/DropConfig.xlsx`：0 文件需要修复。32 个手写 C# 格式化前后 token 哈希一致；`git diff --check` 通过。

烟测中的坏备份、过期会话、HUD 装配失败为既有失败注入；正常关停仍报告 Framework WebRequest 的 ObjectDisposedException 和 3/4 个 ObjectDB 泄漏，不能将完整日志称为零错误。最终 `--verbose --smoketest=ui` 再次 PASS，退出日志仍为 4 个 RefCounted 实例，没有报告 PhysicsShapeQueryParameters2D 泄漏。本轮未改 Framework 或 addons。日志在忽略的 `.godot/validation/bindable_{scan,ui,ui_final,frontend,level}.log`；使用方法见 [ProjectTour](ProjectTour.md#model-的属性绑定)。

## 编辑器 GCHandle 修补（2026-10-15）

按第 50 章的用户授权，仅修改 ExportInspector：先读资源依赖及全局脚本登记，只加载候选包标记并校验，文件列表和打包复用已识别标记，避免扫描时提前加载普通资源。ComponentInsoector 的 14 处释放修改按用户最新要求全部撤回，恢复原文件；这项修改处理退出错误，没有解决本次启动异常。未增加业务测试 API。

- 修补前，实际工程同程序集、同布局普通窗口启动：启用 ExportInspector 出现 **4 次 SwapGCHandleForType 和 2 次退出 SetGodotObjectPtr 异常**，仅临时禁用此插件为 **0 错误**，恢复启用再次为 **4 + 2**。插件扫描会提前加载 54 个候选资源、在条目扫描中再加载 37 个资源。该路径与 Godot 官方 [#112067](https://github.com/godotengine/godot/issues/112067) 的资源句柄问题相符，具体失效的原生资源未唯一定位。
- `dotnet build Godot/GodotProject --no-restore -v quiet`：**0 错误、5 个既有框架警告**。正式 ExportInspector 与已验证候选逐字节一致；原版和候选均识别 6 个正式包及 1 个二进制 .res 测试包，**748 项文件清单逐字节一致**，普通动画的提前缓存由 True 变为 False。PCK 在空白工程读回，普通资源存在、二进制标记已排除；副本重建资源索引后的首次扫描也通过。
- 撤回 ComponentInsoector 之前（当时仍包含它的释放修正），在实际工程保留全部插件、原布局和正式缓存，用 `--editor --path Godot/GodotProject --max-fps 60 --quit-after 600` 完成 **3 次普通窗口冷启动：均退出 0，0 错误、0 警告**。另一次旧程序集对照本轮也未复现，说明问题有间歇性；上述结果不代表 Godot 所有句柄缺陷均已修复。未使用最小化、离屏或副本启动替代实际工程验证。
- 最终仅保留 ExportInspector 修补后，再次执行上述 build：**0 错误、5 个既有框架警告**；实际工程普通窗口冷启动 **退出 0，SwapGCHandleForType / SetGodotObjectPtr 目标异常均为 0**。退出时恢复既有 **14 条 Can't free a RefCounted object 和 14 条 Nonexistent function 'free'**，堆栈均指向 ComponentInsoector._ExitTree；这项独立的退出问题按用户要求不纳入本轮修改，不能把本次完整退出日志称为零错误。`git diff --exit-code -- addons/ComponentInsoector/ComponentInsoector.cs` 为 0，确认该文件恢复原版。
- 验证后已恢复修补程序集及原布局，布局按原字节比对通过；project.godot、正式存档及 Git 索引未修改，未清理正式缓存。此前重载验证仍有 8 条断开不存在连接错误和 3 个 GodotBody2D RID 泄漏；本轮只验证启动，不宣称这些既有重载问题已解决。

日志在忽略的 `.godot/validation/`：修补前对照为 `editor_gc_ab_{baseline,no_export,reenabled}.{out,err}.log`；撤回前验证为 `editor_gc_scan_fixed_{1,2,3}.{out,err}.log`，旧程序集对照为 `editor_gc_scan_previous_control.{out,err}.log`，构建为 `editor_gc_scan_fixed_build.log`；最终最小修改的构建与冷启动为 `editor_gc_minimal_build.log`、`editor_gc_minimal_cold.{out,err}.log`；文件清单、首次扫描与打包为 `editor_gc_metadata_{before,probe,fresh}.{out,err}.log`、`editor_gc_bundle_pack.log`。诊断副本、探针、补丁和日志不提交。

## 当前架构与启动流程回归（2026-10-15）

按用户最新要求统一业务 System、实体 ActionData 和 IController 访问方式。飞行球删除英雄成员，由本关控制器逐帧提供目标坐标；UI Data 不再传递共享 Model，存档/设置/技能 Data 删除。Prepare 集中注册工具、模型和系统，Preload 预加载界面，LoadGame 建立档案作用域；Startup 与 AbstractMenuProcedure 删除。此前的物理接触、AI 状态工厂集合、统一输入采样和不可变装配仍保留。所有者、调用链及旧规范差异见 [项目导览](ProjectTour.md)。按第 10/30/40/50 章执行命名、文档、功能分组、UID 与隔离验证；第 50 章仅登记用户要求的必要流程注册项。

- 上述 `dotnet build`：**0 错误、5 个既有框架警告**；三组 `dotnet test` 分别 **145/145、96/96、18/18**，共 **259 项通过**。单测仍有 NU1900 联网告警及既有 nullable 告警。
- OpenGL `--smoketest=frontend`、`ui`、`level`：均 **PASS、退出 0**。前端覆盖 Prepare/LoadGame、新建/读档、选角、地图、回滚、保存提示与设置复用；Get 查询验证未注册时返回 null、同一引用、移除、销毁及 FSM 隔离。UI 覆盖直接读取 Model 的技能页、背包刷新/交易、订退、窗口/实体复用、失败清理、死亡保存和重入；飞行球覆盖暂停、目标位置更新、同场景其他英雄、拾取上限、池复用和死亡回收。关卡覆盖五波、相机、门、守关条、出口等待飞行掉落、检查点和地图重读。
- 上述 F6 命令：**场地就绪、退出 0**，测试入口直接建立资源组与临时 FSM，显示玩家和两只怪物；不启动正式流程或读取玩家档案。
- Godot `--headless --editor --build-solutions --quit --no-window`：文件扫描、脚本注册和构建完成，**退出 0**；仍报告既有 ComponentInsoector 的 RefCounted/free 释放错误。四个脚本迁移保留原 UID；引用检查 **441 路径、291 UID、23 几何节点通过**，场景几何未变。C# 格式、文档本地链接与 `git diff --check` 检查见本轮验证输出，Git 索引保持不变。
- 日志在 `.godot/validation/ui_flow_{scan,frontend,ui,level,arena,format}.log`。HUD 缺绑定、旧档案写入拒绝与损坏存档是预期失败注入；框架关停 WebRequest `ObjectDisposedException` 仍在，frontend/ui/level/F6 分别有 **3/4/4/3** 个退出泄漏告警，未计为玩法验证通过的依据。生成代码、源表和正式存档未改动；Framework 仅更新已登记的 Procedure.Procedures 清单。
- UI 订退简化：HUD、背包、存档、设置、技能五个窗口删除退订委托，打开时 `+=`、关闭时直接 `-=`；流程先关窗再移除 Model。再次执行上述 `dotnet build` 和 OpenGL `--smoketest=frontend`、`--smoketest=ui`：**0 编译错误，两组回归均 PASS、退出 0**；实际事件列表确认存档页关闭后处理器为 0、复用后为 1，设置关闭后不再订阅关卡；HUD/背包/技能关闭后不响应旧状态源，失败清理、暂停恢复、重绑和池复用正常。7 个 C# 文件格式、UTF-8/CRLF 与 `git diff --check` 通过，验证期间未改 Git 索引。日志为 `.godot/validation/ui_subscription_{build,frontend,ui}.log`，既有框架退出错误与前端/UI 的 3/4 个泄漏告警仍在。
- 初始化与 UI 状态简化：Prepare.OnEnter 直接注册资源组、工具、Model 和 System；删除语言初始化、返回标题、目录创建及标记创建等简单包装。核对 GGF 的 IUIForm、UIManager 与 UIComponent：交互直接查询 `GF.UI.HasUIForm(SerialId)`，8 个重复打开标志已删除，退订留在 OnClose，OnRecycle 不再查询已结束的共享模型。再次执行上述 build、frontend/ui 和 F6 命令：**0 编译错误，前端/UI 均 PASS，三组引擎进程均退出 0**；关闭后尚未回池时 HasUIForm/IsValidUIForm 已为 false，迟到存档按钮没有导航，确认提示没有执行旧回调；订退、暂停、失败清理及池复用通过。日志为 `.godot/validation/entry_ui_{build,frontend,ui,arena}.log`。16 个 C# 文件格式、UTF-8/CRLF、直接注册顺序和本地文档链接通过；引用检查仍为 **441 路径、291 UID、23 几何节点**，Git 索引未改。既有 5 个框架编译警告、WebRequest 退出错误及前端/UI/F6 的 3/4/3 个泄漏告警仍在；框架、生成物、源表和正式存档未改动。

## 作用域装配与引用生命周期（2026-10-14）

按用户最新要求借鉴 QFramework 的状态/业务分离，以 GGF 流程 FSM 类型化 SetData/GetData 统一访问；ProcedureLoadProfile 初始化并注册 Profile、SaveSystem、EquipmentSystem，Title 统一移除。删除 GameContext 及两个专用包装，保留纯状态/规则和 UI 子组件的显式参数。第 00/70 章的旧约定差异、初始化表、保存与运行时边界见 [项目导览](ProjectTour.md#所有者与初始化位置)。

- `dotnet build Godot/GodotProject --no-restore -v quiet`：**0 错误、5 个既有框架警告**。`dotnet test Tests/{BattleTests,ProfileTests,LevelTests} --no-restore -v quiet`：**137/137、92/92、18/18**；单测另有既有 NU1900 联网警告。
- OpenGL `--smoketest=frontend`：**PASS，退出 0**。真实双 FSM 验证同类型隔离、重复初始化拒绝、未初始化读取拒绝、移除与包装复用；选角到地图保持档案/模块身份，关卡和 HUD 借用同一状态，离关解除流程引用，回标题移除档案数据，再读档创建新实例并恢复数值。测试通过既有对象、实际 FSM 和反射观测。
- OpenGL `--smoketest=ui` 与 `--smoketest=level`：均 **PASS，退出 0**。覆盖背包刷新、换装、窗口和实体复用、收益回滚、死亡保存、备份回退、加载取消、失败清理，以及五波关卡、守关血条、出口结算、检查点写盘与地图重读。日志仍有既有证书读取错误、框架关停 ObjectDisposedException；frontend/ui/level 分别有 3/4/4 个退出泄漏实例。前端的旧槽写入拒绝、损坏档案和 UI 的 HUD 失败注入为预期用例。
- 新脚本经 `--headless --editor --quit` 扫描，退出 0；日志仍有既有编辑器插件 RefCounted/free 释放错误。25 份 C# 的 `dotnet format whitespace . --folder --include <绝对路径> --verify-no-changes`、`git diff --check` 通过。`validate_game_references.py --compare-staged-geometry`：**441 条路径、290 个 UID、23 个几何节点通过**；文档本地链接检查通过。日志为 `.godot/validation/typed_data_{scan,build,BattleTests,ProfileTests,LevelTests,frontend,ui,level_engine}.log`。

## 命名复查与扩展验证（2026-10-14）

按第 10 章检查手写业务、流程及测试的方法命名，统一组合输入、水平移动、巡逻折返、怪物选招、动画采样、状态机生命周期和窗口清理方法；调用、事件订退和测试反射同步更新。

- `dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误，5 个既有框架警告和 1 个 NU1900。三个 `dotnet test Tests/{BattleTests,ProfileTests,LevelTests} --no-restore -v quiet`：分别 **137/137、92/92、18/18**；新增一段/三段英雄的配置、循环和空中段号验证，覆盖 60/120Hz。
- OpenGL `--smoketest=ui`：**PASS，退出 0**；覆盖怪物行为、界面清理、实体复用、反射调用、加载失败清理与存档重入。日志仍有既有证书错误、预期 HUD 失败注入、框架关停 `ObjectDisposedException` 及 4 个退出泄漏实例；没有新增玩法失败。日志为 `.godot/validation/readable_names_{build,battle,profile,level,ui}.log`。
- 本轮 21 份 C# 的 `dotnet format whitespace . --folder --include <绝对路径> --verify-no-changes`、`git diff --check` 和引用检查通过（441 条路径、292 个 UID、23 个几何节点）；旧标识符检索无残留。唐僧弹道与飞行怪尚未实现，复用边界及现有落地限制见 [项目导览](ProjectTour.md)。

## 官方输入、状态职责与格式整理（2026-10-14）

按第 00/10/20/30/50 章维护所有权、手写命名、配置导出、身体状态机与验证边界；玩法以用户本轮要求的造一源码为准，规范正文未自行改写。

- Luban 导出成功；构建 0 错误、5 个既有框架警告及 1 个 NU1900 联网警告。BattleTests **133/133**、ProfileTests **92/92**、LevelTests **18/18**；覆盖 60/120Hz 第四段松手、有效输入即时换段、独立冷却、超时、组合键、跳跃、空中方向及受击打断；配置拒绝缺段、非正和非有限参数。另补空中攻击松方向再按回、一帧内松按、受击后续段、不同英雄独立序列及怪物第二招的切换参数；空中松按问题先复现失败再修复。
- `--smoketest` **PASS，退出 0**：真实五段连击、走跑、两次跳跃、空中下劈与命中；新增末轮打到第四段后注入过早按键并彻底松开，收招后不出现第五段。
- OpenGL `--smoketest=level` **PASS，退出 0**：五波战斗、怪物出招/收招、相机、物理门、守关血条、成功结算、检查点写盘及返回地图重读；新增近处 W＋J/K/S 不误触出口，再验证单独 W 通关。
- OpenGL `--smoketest=ui` **PASS，退出 0**：三座单向平台、真实英雄 S＋K 下落；背包刷新、穿戴外观、存档目录、技能目录、无双、受击、实体池与隔离存档重入继续通过。平台夹具按实际脚底和平台表面放置；移位后等待真实物理更新再读取落地事实。
- HeroConfig 的 XLSX Diff 普通/公式模式均识别 **24 格**目标变化，旧缓冲字段已删除，秒制冷却及移动数值可见；默认 XML 命名空间保持可读。引用检查 **441 条路径、292 个 UID、23 个几何节点**通过；`git diff --check` 通过。
- `HeroAttackSequence` 归英雄拥有，状态只调用序列并自行切换；删除基类反查攻击子状态的方法。怪物两处跨状态 `Arm` 调用改用 GGF 切换数据，进入时消费并归还池化参数。全项目手写业务不再有 `GetState<T>().方法` 的跨状态调用。
- 将格式配置集中到仓库根 `.editorconfig`；使用 `dotnet format whitespace . --folder --include <手写文件绝对路径> --verify-no-changes` 分批验证 **216 份**手写 C#，排除框架与生成物。展开拥挤语句，统一四空格缩进和换行；Roslyn 核对格式化前后语法标记一致。新增 C# 已经 Godot 扫描、构建并生成 UID；编辑器插件退出仍报告下文已有的释放错误，扫描退出码为 0。
- 手写玩法代码中的含糊组合输入、普攻起手、成长更新、目录更新、外观应用和背包刷新方法已更名，调用、订退与文档同步；框架契约与生成流程保持原有边界。日志：`.godot/validation/official_input_{build,battle,profile,level_unit,smoke,level_render,ui_render,scan}.log`；仍有下文已登记的证书、故意失败注入及框架关停异常。

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

## 完整待机、领取动画与角色页签（2026-10-14）

完成上面的基础与技能导入后，使用基础导出的完整 529 SVG 补齐待机。只追加姿势并定向更新待机轨道，不重生成其他动作：

```powershell
& 'S:/Godot4/Godot4CSharp_console.exe' --headless --path Godot/GodotProject `
  --script res://EditorScripts/import_official_wukong_idle.gd -- `
  (Join-Path $workPath 'manifest.json') res://.godot/validation/wukong_idle_manifest.json
python Tools/LegacyMigration/import_official_wukong_idle.py `
  Godot/GodotProject/.godot/validation/wukong_idle_manifest.json
& 'S:/Godot4/Godot4CSharp_console.exe' --headless --editor --path Godot/GodotProject `
  --script res://EditorScripts/regenerate_collection.gd
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --rendering-method gl_compatibility `
  --script res://EditorScripts/validate_official_wukong_idle.gd -- `
  (Join-Path $workPath 'manifest.json') res://.godot/validation/wukong_idle_manifest.json
```

- 待机逐帧 PASS、退出 0：400 个身体/武器源图层帧与正式 AnimationPlayer 取样像素一致，另核对引擎透明边缘导入处理、100 个背包帧与完整 100/24 秒循环。四种穿戴 × 双朝向截图 `official_idle_contact.png` 已查看。前 108 格 × 四张图与修改前暂存像素相同，仅追加 8 个姿势；完整动画库验证通过。
- `HeroConfig.xlsx` 删除无消费者的 IdleEmoteDelay，经 Luban 导出；其余单元格值与暂存版本逐项相同。命名空间检查及已安装 XLSX Diff worker 的普通/公式模式均能读出新表头与变化；没有运行时兼容代码。
- `dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误、5 个既有框架警告；BattleTests 106/106、ProfileTests 85/85。Godot 扫描及构建日志显示完成，编辑器关停停留在既有插件释放问题后，终止该验证进程；未终止用户编辑器。
- OpenGL `--smoketest=ui` PASS、退出 0：外置领取动画按 0.8 秒上移 100px 并二次缓出淡出，暂停冻结、收益只入账一次、满包不播、结束回池与复用重置通过；`pickup_rising.png` 已查看。技能窗口仅显示出战角色，禁用角色的模拟信号也不能切页；主动/被动、暂停、余额订退和复用通过。日志 `form_pickup_skills_ui.log`。
- OpenGL `--smoketest=frontend` 与 `--smoketest` 均 PASS、退出 0：六格存档、损坏格、建档读档、导航，以及真实输入走跑跳和五段攻击通过。日志 `saveform_frontend_final.log`、`idle_input_final.log`。仍有下文登记的根证书与框架关停异常，UI/基础输入报告 4 个、frontend 报告 3 个 ObjectDB 泄漏。
- 引用验证：441 条路径、291 个 UID，23 个关卡几何节点与暂存版本一致。本轮 `git diff --check` 通过；`git diff --cached --check` 报既有 Luban 技能生成文件的空白，未手改生成物或 Git 索引。

## 技能准备、通用飘字与 UI 组件（2026-10-14）

本轮按第 00/10/50 章的作用域、显式会话与 GGF 池约定实现。飘字由 `World/FloatingText` 开始/结束会话，共用一个 `FloatingTextNode` 池；UI 已按后续用户要求收敛到完整面板，见下节。第 00/10/60 章旧 Presenter/UI 目录示例及第 40 章 HUD 根绑定示例未自行改写；当前用户要求与规范差异见 [ProjectTour](ProjectTour.md)。

| 检查 | 当前结果 |
| --- | --- |
| `dotnet build Godot/GodotProject --no-restore -v quiet` | 0 错误；5 个框架既有警告及 NU1900 网络警告 |
| `dotnet test Tests/ProfileTests --no-restore -v quiet` | 85/85；两角色目录、费用、被动总效果、声音、11 个伤害阶段、五个动作/特效与 47 条事件的普通/无双位移、目标分支、独立攻击编号及非法数据拒绝 |
| `--smoketest=ui` | 最终 OpenGL PASS、退出 0；九种数字样式各 100 组相邻组合共 900 组，独立读取真实 PNG alpha 核对比例字宽、描边、完整高度及居中/左起；同池实例跨生命/十位暴击/魔法/闪避复用、暂停、作用域隔离、旧事件过滤、实际恢复品/蓝球领取通过 |
| UI 组件回归 | 最新背包根绑定 5、存档 3、HUD 5、技能 8；原背包出售/穿戴/详情、技能切页/暂停/余额/退订/复用、HUD 满值连续闪烁与失败清理继续通过 |
| 技能资源关联 | 五个正式动作、五个独立特效与 47 条事件的所属局部时间/跳转位置均处于真实动画长度内；七段 MP3 经 GGF SFX 代理播放、按编号停止；未接入施放 |
| `--smoketest=frontend` | 组件接线后 OpenGL PASS、退出 0；六格目录、建档/读档、选角、地图悬停、保存提示和放弃回滚通过 |
| `--smoketest` | 通用飘字合并后 OpenGL PASS、退出 0；真实五段连击、命中、死亡及池化伤害文字通过；TestArena 活动恢复节点退出场景通过 |
| XLSX Diff 1.6.4 | 四张技能战斗源表及两个 Luban 定义表在实际 worker 的普通/公式两种模式均可读；受控副本 SkillHit F6 单格变化准确识别。新事件表 905 个单元格；源表默认命名空间及 Luban 产物同步 |
| 资源登记 | 技能行展开、删除三个行子场景并新增装备槽 shader 后，既有 Collection Res 生成器在 editor 模式收集 440 个资源，无 basename 冲突，含事件表 bytes；入口脚本检查 editor 模式并等待扫描/导入完成后退出 |
| `validate_game_references.py --compare-staged-geometry` / `git diff --check` | 最新 450 条序列化路径、309 个 UID 通过；23 个关卡几何节点与暂存版本一致，diff 无空白错误 |

已经实际检查 `.godot/validation/floating_text_spacing.png`：九种 `111 / 108 / 888` 及 `10、17、24、36、59、70、99、1234567890`。宽字额外间隙默认 0px，窄字侧留白比例默认 1/6；统一公式覆盖 0–9，既有 PNG 不重写，首次使用后缓存字宽。背包、六格存档、悟空/唐僧/被动页截图亦已查看。

最终 UI 日志为 `.godot/validation/skills-source/final_ui.log`，组件 frontend 日志为 `combat_text/all_components_frontend.log`，Collection 为 `skills-source/final_collection.log`。系统根证书读取错误、编辑器插件 RefCounted/free 及关停 RID/ObjectDB 泄漏仍存在；UI 退出的 WebRequest ObjectDisposedException 和 4 个 ObjectDB 实例泄漏仍存在。HUD 缺绑定和 frontend 跨槽/损坏档属于故意失败注入；业务 PASS 与这些异常分开记录，未修改只读框架。

本轮已完成技能目录/面板、悟空动作/判定/特效/音效、伤害/动作/特效/条件分支与位移配置。学习记录、商店购买、扣费及实战施放依用户要求留待后续接入，当前不改存档格式。数据形状和源规则见 [OfficialReference](OfficialReference.md)。

### 技能面板直编与穿戴槽底图

- 将十五行全部展开到 `skill_form.tscn`，主动为五列 GridContainer、被动为六列；删除三个行子场景。预填全部名称、费用、按键、说明和图标；后续合并行脚本，由两个完整页统一绑定数据。编辑位置见 [ProjectTour](ProjectTour.md)。
- `dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误、5 个框架既有警告。`--smoketest=ui`：PASS、退出 0；新增所有行属于主场景、同页列对齐、列无重叠/溢出及长说明高度检查；将十项主动和五项被动的预填文字/图标扰动后重新绑定，全部恢复为真实表值。
- 武器与防具槽各用独立的局部 shader 材质；真实穿戴武器时只清除武器题签，穿戴防具后两槽均清除，分别卸装/关闭复用后恢复空槽题签，背包图标透明度保留。完整背包交易和旧 UI 回归继续通过。
- 独立 OpenGL 预览与像素检查 PASS、退出 0：不调用运行时数据绑定也可渲染三页；两张原槽图只清掉 765 个字样像素，渐变颜色与边框保持，occupied 从 true 切回 false 后逐像素恢复。截图 `skills_editor_{wukong,tangseng,passive}.png`、`equipment_slot_backgrounds.png`、实际 `inventory_wearing.png` 已查看。
- 日志在 `.godot/validation/inline_grids_{build,collection,preview,ui}.log`。UI 日志的 HUD 缺绑定仍是预期故障注入；根证书读取、编辑器插件关停及 WebRequest 关停异常/4 个 ObjectDB 泄漏仍存在，未修改只读依赖。

### 背包出售控件颜色（2026-10-14）

- 按用户最终参考，一键出售与确认/取消常态使用原版棕金渐变底图，悬停/按下保留原深棕纯色；禁用保持暗色，焦点只描边。品质上拉菜单为接近背包面板的深棕底与浅金字。主题与五个 StyleBox 外置到 `MainPack/Themes/` 及其 `Styles/` 子目录，重复状态共用样式；按第 40/50 章处理，未改业务或新增生产测试接口。
- `--smoketest=ui`：PASS、退出 0；背包出售、取消/Esc、确认、防重复、关窗复用及既有 UI 回归通过。日志 `.godot/validation/inventory_colors_ui.log`，实际背包、品质选项和确认框截图已查看。
- 一次性 OpenGL 预览：PASS、退出 0；真实鼠标悬停、按下、组合按下、焦点与禁用状态和菜单高亮截图已查看。脚本与图片仅在忽略目录 `.godot/validation/`，日志 `inventory_colors_preview.log`。预览仍有系统根证书读取错误；UI 回归的预期 HUD 缺绑定、WebRequest 关停异常及 4 个 ObjectDB 泄漏仍存在。
- `dotnet build Godot/GodotProject --no-restore -v quiet`：0 错误、5 个框架既有警告；通过既有 `EditorScripts/regenerate_collection.gd` 收集 446 个资源，新增主题与样式六项，无 basename 冲突。日志 `inventory_theme_{build,collection}.log`；编辑器既有 RefCounted/free 与关停泄漏仍在。
- `validate_game_references.py --compare-staged-geometry`：457 条序列化路径、309 个 UID 通过，23 个关卡几何节点与暂存版本一致；`git diff --check` 通过。
- 样式审计：新主题与五个外置 StyleBox 均有正式场景引用，确认框 panel 通过局部覆盖使用；装备格的 tooltip_theme / StyleBoxEmpty 用于隐藏引擎外壳，保留。删除 PopupMenu 不支持的 `font_pressed_color`，其他颜色与描边保留用户调整；实际 Godot 默认主题查询确认该项无效，清理后无未知颜色项。检查脚本仅在忽略目录，日志 `audit_ui_theme.log`。

### UI 职责合并与目录整理（2026-10-14）

- 按用户要求删除 18 个过细脚本及对应 UID：背包身份/装备/属性列/经验、分类/格子/底栏/分页/品质按钮，存档 Grid/Button，技能 Header/Footer/行组/行脚本，以及仅含三个处理方法的 InventoryForm.Bag partial。背包保留个人资料、背包和出售确认三个完整面板；技能保留主动、被动两页；SaveForm 直接管理六格。
- `GameScripts/UI/Widgets/` 中九个保留控件连同 UID 归入 InventoryForm、BattleForm、MapForm 的 Components，场景引用同步更新。检查全部 Form，其余简单表单无需增加或合并包装。保留场景分组、外置主题及自定义 tooltip，不新增生产测试接口；目录与职责依据第 00/10 章，生成与验证依据第 50 章。
- SaveForm、SkillForm 根绑定经 `regenerate_ui_form.py <场景> --namespace GameLogic.UI` 生成。Godot `--headless --build-solutions --quit --no-window` 完成扫描及构建；`dotnet build Godot/GodotProject --no-restore -v quiet` 为 0 错误、1 个 NU1900 网络警告。
- `--smoketest=ui` 与 `--smoketest=frontend` 均 PASS、退出 0：完整背包交易、弹层定位、技能数据覆盖/切页/暂停/复用、HUD、六格目录/建档/读档/损坏格及菜单导航回归通过。背包、技能和存档截图已查看。日志 `.godot/validation/ui_consolidation_{smoke,frontend}.log`。
- `validate_game_references.py --compare-staged-geometry` 为 440 条路径、291 个 UID 通过，23 个关卡节点与暂存版本一致；`git diff --check` 通过。另对本轮所有 UI 场景的节点树和非脚本属性与暂存版本逐项比对，布局及样式一致。
- 已有根证书读取错误、编辑器插件 RefCounted/free、运行关停 WebRequest ObjectDisposedException 仍出现；UI / frontend 分别报告 4 / 3 个 ObjectDB 泄漏。HUD 缺绑定及跨槽/损坏档报错是预期失败注入，与本轮业务断言分开记录，未修改只读框架。

## 最近完整战斗结果（2026-10-13）

| 检查 | 结果 |
| --- | --- |
| Luban 导出、dotnet build | 通过；装备/掉落/恢复品改动保留；本轮补齐造一第五段攻击、击退与声音分组，更新三张源表及对应产物；构建 0 错误、5 个框架既有警告 |
| BattleTests | 106/106；含五段连击循环、空中第三段及落地重置、无双秒制一致性和妖猴范围边缘回归 |
| ProfileTests / LevelTests | 63/63、18/18；装备具名属性、官方随机端点、互斥/独立掉落概率、满包、穿脱替换、异角色限制、v6 穿戴引用读档与回滚、出售价格/重复/穿戴中/灵魂溢出均通过；跨三页整批品质出售、排除穿戴物、一次通知及整批溢出拒绝通过；成长表 55 级魔抗全为 0 |
| Godot OpenGL UI | PASS、退出 0；四分类切换、75 格三页边界、第三页选中与品质确认后整包出售；取消/Esc、背后输入阻挡、数据变更取消、重复确认拒绝、关窗复用清除旧交易及出售放弃回滚通过；上拉菜单在 940×590 和 1175×738 下对齐；1/3 项属性提示保持固定头部与正确价格位置；地面真实落地、接触恢复、上限、满包、同件领取、10 秒过期、暂停和池复用；恢复飘字颜色、间距、秒制上升与回池；八件透明图标、造三详情/操作菜单、真实穿脱和出售、C 开关及重映射/长按、订阅复用、死亡保存/放弃回滚均通过；保留战斗与失败清理回归 |
| 悟空美术与两层动画 | 本轮四张独立身体/武器图集、特效图及特效时序逐字节不变；9 个基础动作和 RESET 的完整轨道不变。仍只滤掉固定棒影元件 246，保留 203/254/282 的弧光；四个角色动画库契约通过 |
| 矩形判定与残影逐帧 | OpenGL PASS、退出 0；完整 8/8/11/20/15 源帧 × 四种穿戴 × 两个朝向，共 496 次绘制；矩形覆盖武器与剑气并集，外扩 3px、最小厚度 14px。150 次真实剑气区域命中（第三段 72、第四段 32），含 78 次后续残留帧命中；近端、范围外及关闭窗口检查通过，原有效时序保持 |
| 造一真实输入攻击 | `--smoketest` PASS、退出 0；按动画长度完成五段连击、空中第三段下劈、落地后从第一段重开；真实猴子命中与死亡链路通过 |
| Godot OpenGL level | PASS、退出 0；真实五波、守关怪、W 出口 → 成功结算 → 返回地图 → 读盘，经验 204，检查点收益保留 |
| Godot OpenGL frontend | PASS、退出 0；新增地图默认闪烁、悬停切换、移开恢复、透明角不误点和放大轮廓命中；保留开始、六格存档、选角、放弃回滚、保存提示复用与 GGF 声音检查 |
| 妖猴瞬间举棒 | OpenGL UI PASS；左右追击、平台踱步及范围边缘往返共 48 个绘制帧均保持 run |
| 大立绘后半段 / 顶部 BOSS 条 | OpenGL UI / level 均 PASS、退出 0；末段动态火焰完整、名称与条体整体顶部居中 |
| 升级动画蓝线 | OpenGL 逐像素 PASS、退出 0；14 组原画只移除 362 个边缘像素，28 帧保持 122×136、24 fps |
| 资源、配置与文档 | 382 条序列化路径、253 个 UID 通过，Level_1 的 23 个几何节点与暂存版本相同；同款粗糙/普通装备保留官方相同透明前景；非目标表格值保持一致，本地文档链接有效 |
| XLSX Diff 格式修复 | 最新全部 18 张游戏配置经已安装插件的 worker 在普通/公式两种模式验证，工作表、单元格计数及中文/新字段值均符合 XML；受控副本 H9 单格变化准确识别为 1 格；命名空间修复不改变 XML 语义和其他 ZIP 部件，重复执行无需修改 |
| 背包表 XLSX Diff | ProfileConfig 普通/公式两种模式只识别 F5/F6 两格变化；__enums__ 两种模式识别新增四个品质的 12 格，既有枚举值和 ID 不变；源表与 Luban 产物同步 |
| AI 原生几何与受击朝向 | AiBox 替换为 Godot.Rect2，106 项战斗单测通过；真实 AI 巡逻/追击/出招/收招锁向/高处守候/落地恢复/丢失目标/死亡回收通过（测试侧临时固定 AttackDesire=100，结束恢复，生产概率不变）；OpenGL UI 验证悟空和三类怪物双朝向/双攻击方向均保持受击前朝向、击退方向正确 |
| 攻击表 XLSX Diff | 本轮 AttackConfig / SoundConfig / __enums__ 保持默认命名空间；已安装 XLSX Diff worker 的普通/公式两种模式均正确识别 33/9/6 个目标单元格变化，其余单元格值与修改前一致 |
| 正式开发档 | 已按用户授权备份原件至 GameData.backup-before-v6-20261013-025550，一次性转存原累计经验 1020、灵魂 835；无运行时兼容代码。最终只读反序列化主档/备份并经 ProfileSaveHelper 重建均通过 v6；主档已被用户后续游玩推进为经验 1224、灵魂 1008，未覆盖该进度 |

## 已知限制

第二、第三关最新结果及开发档处理见本文开头；下列为仍存在的依赖和未实现范围。

饰品原生导入脚本完成保存并输出 PASS 后，编辑器关停触发 `godotsharp_array_destroy` 的 GC `0xC0000005`，该次进程退出 1，不能计作无错误导入。保存后的资源已在独立真实游戏中完整加载、点击和复用通过，UI 进程退出 0；未修改引擎或插件来掩盖关停异常。

- 构建有框架既有的弃用警告；测试 NU1900 表示无法连接 NuGet 漏洞服务，与断言失败分开记录。
- AI 烟测曾在落地等待 6 秒内未出手而失败；复测中的范围与落地读数正常、行为恢复并通过。现测试夹具临时固定出手概率，只验证恢复能力，概率规则由确定性单测验证。
- 当前隔离环境的 Godot 进程会报告系统根证书存储读取失败；独立攻击渲染断言仍 PASS、退出 0，该错误不作为无错误运行忽略。
- UI 回归故意注入 HUD 缺绑定以检查失败清理，该段错误日志是预期夹具；必须继续到 SMOKE PASS 才算通过。
- frontend 回归故意注入跨槽写盘和损坏档案；拒绝写入与读取失败日志为预期。烟测使用隔离目录；获准的正式开发档一次性维护另行登记，不由烟测写入。当前格式 v9，无旧格式运行时兼容。
- 引擎关停的 `DefaultWebRequestAgentHelper.Reset` 仍抛 ObjectDisposedException，并报告 ObjectDB 实例未释放。业务断言 PASS、进程退出 0，不能称为无错误退出；独立升级渲染检查无此异常。
- 历史关停还出现过 GC `0xC0000005` 与编辑器 `ComponentInsoector._ExitTree` 释放错误；依赖只读，未在业务层绕过或修改。
- 当前悟空实战含造一五段普攻与五项主动技能；唐僧技能和被动购买尚未实现，造三攻击未迁入。本轮按获准的表格与目录分类同步第 20/40 章，存档 v8 契约见第 70 章；其余未实现范围按实际进度记录。

规范依据和依赖修改授权仅在 [第 50 章](ProjectGuidelines/50-FrameworkAndTools.md) 维护；本页只保留可执行命令、当前结果和限制。
