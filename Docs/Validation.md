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
```

按修改范围选用 `--smoketest=level-cancel`、`ai`；规则单测无需打开编辑器。场景装配用 `--headless --script res://EditorScripts/validate_level_scene.gd -- --resize --fractional`，编辑器 Tool 再加 `--editor`；触发区用 `validate_level_triggers.gd`，动画库用 `validate_anim_libraries.gd`。有渲染器时，`validate_musou_flash.gd` 检查无双圆形裁剪，`validate_level_up.gd` 检查升级蓝线裁剪、画面和注册尺寸。

引擎截图写入忽略的 `.godot/validation/`；无双开场及后半段分别为 `musou_activation.png`、`musou_activation_tail.png`，升级为 `level_up.png`、`level_up_tail.png`，原帧/裁剪帧对照为 `level_up_frames.png`，BOSS 条为 `encounter/featured_health_bar.png`。通过 PASS/FAIL、退出码、错误与画面共同判断结果。

战斗背包为 `inventory_form.png`，失败结算为 `settlement_failure.png`，成功结算为 `encounter/settlement_success.png`。成功页截图在真实检查点完成后立即采集，可能带有短暂的保存成功提示；提示按真实时间淡出，背包和结算暂停不会将它冻结。

## 最近结果（2026-10-12）

| 检查 | 结果 |
| --- | --- |
| Luban 导出、dotnet build | 通过；新增背包与结算窗口配置，死亡等待字段改为 DeathSettlementDelay；构建 0 错误、5 个框架既有警告 |
| BattleTests | 106/106；含无双秒制一致性、妖猴左右/高低目标及范围边缘移动的 6 个新回归用例 |
| ProfileTests / LevelTests | 45/45、18/18；含装配入口与钱包实际变更通知 |
| Godot OpenGL UI | PASS、退出 0；保留战斗、存档与失败清理回归；新增背包 25 格、真实属性/灵魂、暂停计时、预览播放、退订复用及失败页重试/返回地图 |
| Godot OpenGL level | PASS、退出 0；真实五波、守关怪、W 出口 → 成功结算 → 返回地图 → 读盘，经验 204，检查点收益保留 |
| Godot OpenGL frontend | PASS、退出 0；开始、六格存档、选角、地图、关卡、放弃回滚、保存提示复用与 GGF 声音；提示在暂停期间也会结束 |
| 妖猴瞬间举棒 | OpenGL UI PASS；左右追击、平台踱步及范围边缘往返共 48 个绘制帧均保持 run |
| 大立绘后半段 / 顶部 BOSS 条 | OpenGL UI / level 均 PASS、退出 0；末段动态火焰完整、名称与条体整体顶部居中 |
| 升级动画蓝线 | OpenGL 逐像素 PASS、退出 0；14 组原画只移除 362 个边缘像素，28 帧保持 122×136、24 fps |
| 资源、地形与文档链接 | 358 条序列化路径、224 个 UID 通过；23 个已暂存地形节点保持一致；Docs 55 个本地链接无失效 |

## 已知限制

- 构建有框架既有的弃用警告；测试 NU1900 表示无法连接 NuGet 漏洞服务，与断言失败分开记录。
- UI 回归故意注入 HUD 缺绑定以检查失败清理，该段错误日志是预期夹具；必须继续到 SMOKE PASS 才算通过。
- 引擎关停的 `DefaultWebRequestAgentHelper.Reset` 仍抛 ObjectDisposedException，并报告 ObjectDB 实例未释放。业务断言 PASS、进程退出 0，不能称为无错误退出；独立升级渲染检查无此异常。
- 历史关停还出现过 GC `0xC0000005` 与编辑器 `ComponentInsoector._ExitTree` 释放错误；依赖只读，未在业务层绕过或修改。

规范依据和依赖修改授权仅在 [第 50 章](ProjectGuidelines/50-FrameworkAndTools.md) 维护；本页只保留可执行命令、当前结果和限制。
