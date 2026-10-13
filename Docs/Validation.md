# 验证

在仓库根执行。实际引擎：`S:/Godot4/Godot4CSharp_console.exe`；可换成本机对应路径。

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
```

按修改范围选用 `--smoketest=level-cancel`、`ai`；规则单测无需打开编辑器。场景装配用 `--headless --script res://EditorScripts/validate_level_scene.gd -- --resize --fractional`，编辑器 Tool 再加 `--editor`；触发区用 `validate_level_triggers.gd`，动画库用 `validate_anim_libraries.gd`。有渲染器时，`validate_musou_flash.gd` 检查无双圆形裁剪，`validate_level_up.gd` 检查升级蓝线裁剪、画面和注册尺寸。

XLSX Diff 1.6.4 的解析器无法读取带 `x:` 前缀的 SpreadsheetML 标签，会错误地显示零张表。导表工具仍能读取这类文件。遇到该问题时，用 `python Tools/ProjectMaintenance/normalize_xlsx_namespaces.py --write <文件.xlsx>` 将标签改为等价的默认命名空间；脚本逐项验证 XML 内容并原子替换，不重新生成单元格或样式。不带 `--write` 时只检查。已暂存的文件需要重新暂存修复后的版本，工作区修复不改 Git 索引；历史版本若含相同前缀，插件仍无法读取历史侧。

引擎截图写入忽略的 `.godot/validation/`；无双开场及后半段分别为 `musou_activation.png`、`musou_activation_tail.png`，升级为 `level_up.png`、`level_up_tail.png`，原帧/裁剪帧对照为 `level_up_frames.png`，BOSS 条为 `encounter/featured_health_bar.png`。通过 PASS/FAIL、退出码、错误与画面共同判断结果。

战斗背包为 `inventory_form.png`，品质上拉菜单为 `inventory_quality_sale.png` / `inventory_quality_sale_scaled.png`，出售确认框为 `inventory_sale_confirmation.png` / `inventory_sale_confirmation_scaled.png`，多属性详情为 `inventory_tooltip_attributes.png`，失败结算为 `settlement_failure.png`，成功结算为 `encounter/settlement_success.png`。成功页截图在真实检查点完成后立即采集，可能带有短暂的保存成功提示；提示按真实时间淡出，背包和结算暂停不会将它冻结。

装备背包与实际详情为 `inventory_equipment.png`，操作菜单为 `inventory_actions.png`，武器/防具同时穿戴为 `inventory_wearing.png`，绿色/蓝色恢复数字为 `recovery_numbers.png`；无名称的地面恢复品和装备为 `ground_pickups.png`；地图默认边缘闪烁的两端画面为 `frontend/map_current_low.png`、`frontend/map_current.png`。

## 悟空造一资源复现与逐帧检查

官方源包地址与散列见 [OfficialReference](OfficialReference.md)，需先准备 Java、FFDec 26.3 和原始 Role_v7.swf；工具自行恢复包头并核对 SHA。以下只更新悟空美术、五段攻击和资源登记，不重生成其他角色或基础动作时序。

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

逐帧检查覆盖全部 62 个源攻击帧 × 四种穿戴 × 两个朝向，共 496 次实际绘制；核对 24 fps 秒制关键帧、身体/武器帧号、固定金箍棒棒影已移除、原版弧光保留、朝向、根坐标及矩形覆盖。独立读取武器与剑气像素验证范围、3px 余量和 14px 最小厚度；检查判定窗口未早于可见剑气结束。真实 Area2D 验证近端命中、武器之外的剑气及残留命中、矩形外和关闭窗口不命中。带矩形轮廓的图片在 `.godot/validation/official_attacks/attacks_armor*_weapon*_facing*.png`；轮廓仅由验证脚本绘制。真实输入用 `--smoketest` 检查五段连击、按动画长度结束、空中第三段下劈和近距离命中；`--smoketest=ui` 检查全部 14 个玩法动作、四种穿戴及背包。

## 最近结果（2026-10-13）

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

- 构建有框架既有的弃用警告；测试 NU1900 表示无法连接 NuGet 漏洞服务，与断言失败分开记录。
- AI 烟测曾在落地等待 6 秒内未出手而失败；复测中的范围与落地读数正常、行为恢复并通过。现测试夹具临时固定出手概率，只验证恢复能力，概率规则由确定性单测验证。
- 当前隔离环境的 Godot 进程会报告系统根证书存储读取失败；独立攻击渲染断言仍 PASS、退出 0，该错误不作为无错误运行忽略。
- UI 回归故意注入 HUD 缺绑定以检查失败清理，该段错误日志是预期夹具；必须继续到 SMOKE PASS 才算通过。
- frontend 回归故意注入跨槽写盘和损坏档案；拒绝写入与读取失败日志为预期。烟测使用隔离目录，不改正式档；用户授权的正式开发档一次性维护单独登记在上表。当前格式 v6，无旧格式运行时兼容。
- 引擎关停的 `DefaultWebRequestAgentHelper.Reset` 仍抛 ObjectDisposedException，并报告 ObjectDB 实例未释放。业务断言 PASS、进程退出 0，不能称为无错误退出；独立升级渲染检查无此异常。
- 历史关停还出现过 GC `0xC0000005` 与编辑器 `ComponentInsoector._ExitTree` 释放错误；依赖只读，未在业务层绕过或修改。
- 当前悟空已接入基础动作与造一五段普攻；无消费者的旧 skill_* 已删除，技能及造三攻击尚未实现，后续须同时迁入对应素材、特效与时间轴。规范第 20/30/50/60/70 章仍有旧属性名、存档版本和装配描述，规范正文未自行改写；本轮资源遵循第 40 章既有美术规则。

规范依据和依赖修改授权仅在 [第 50 章](ProjectGuidelines/50-FrameworkAndTools.md) 维护；本页只保留可执行命令、当前结果和限制。
