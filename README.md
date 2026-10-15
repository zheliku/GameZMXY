# GameZMXY

基于 Godot 4.7 .NET 与 GGF 的单人 2D 横版动作 ARPG 学习项目。游戏代码和资源位于 `Godot/GodotProject/TheGame/`。

先看 [项目导览](Docs/ProjectTour.md)：启动链路、作用域、战斗数据流、局部 UML 和职责表。唯一项目规范位于 [Docs/ProjectGuidelines/AGENTS.md](Docs/ProjectGuidelines/AGENTS.md)。

## 运行与构建

当前环境：Godot .NET SDK **4.7.2**、C# **net10.0**、Newtonsoft.Json **13.0.4**（NuGet）；Compatibility 渲染器、内置 2D 物理、940×590 设计视口。需要安装 .NET 10 SDK 与对应 Godot .NET 编辑器。

从仓库根执行：

```powershell
dotnet build Godot/GodotProject/GodotProject.csproj
& 'S:/Godot4/Godot4CSharp.exe' --path Godot/GodotProject --editor
```

编辑器中按 F5 从开始界面运行：开始游戏 → 六格存档选择 → 新档选角 / 已有档直接地图 → 点击花果山（当前五波，包含大猩猩守关与 W 出口）；打开 `TheGame/Scenes/TestArena.tscn` 后按 F6 运行独立战斗场地。场地不读写玩家存档。

新增 C# 后执行一次引擎构建，生成脚本 UID 并完成编辑器扫描：

```powershell
& 'S:/Godot4/Godot4CSharp_console.exe' --headless --build-solutions --path Godot/GodotProject --quit --no-window -q
```

上面的编辑器路径是项目约定的本机路径；其他机器替换为自己的 Godot .NET 可执行文件。

## 架构与目录

正式流程为 `Launch → Update → Prepare → Preload → Title（开始与六格存档选择）→ LoadGame → 新档选角 / 已有档地图 → Level`。`MainPack/Scripts/Procedure/ProcedurePrepare.cs` 的 OnEnter 直接注册资源组、工具、Model 与 System；独立 F6 场地由测试入口自行装配。

| 目录 | 内容 |
| --- | --- |
| `TheGame/MainPack/` | 启动流程、共享 UI、资源组与通用节点池 |
| `TheGame/GameScripts/Battle/` | 确定性战斗规则与战斗运行时 |
| `TheGame/GameScripts/Profile/`、`Save/` | 玩家档案、成长、SaveSystem、存档格式与映射 |
| `TheGame/GameScripts/Architecture/` | Model、System、Utility、Controller 契约与 GGF FSM 适配 |
| `TheGame/MainPack/Scripts/Procedure/FlowData/` | 流程数据键与 GGF 池化包装 |
| `TheGame/GameScripts/Entity/` | 英雄、怪物、身体/AI 状态机、判定与动画适配 |
| `TheGame/GameScripts/Level/`、`UI/` | LevelModel、LevelSystem、LevelViewController 及功能组件；HUD 与控件 |
| `TheGame/Scenes/`、`Entitys/`、`UIs/` | 正式场景、实体场景与 GGF 窗口 |
| `Configs/GameConfig/` | Luban 源表和生成入口 |
| `Tests/`、`MainPack/Scripts/Debug/`、`EditorScripts/` | 纯 C# 单测、真实引擎回归、场景与资源验证 |
| `Framework/`、`addons/` | 只读 GGF 依赖和编辑器插件 |

GGF 主流程 FSM 分别持有 Model 和 System，System 借用 Model。PlayerModel 拥有本槽档案；LevelModel 保存本局状态，LevelSystem 执行关卡业务，LevelViewController 协调节点；SaveSystem 负责读档与检查点写盘。实体拥有各自战斗运行时，HUD 观察状态并提交请求。初始化与生命周期见项目导览。

## 常用验证

```powershell
dotnet test Tests/BattleTests
dotnet test Tests/ProfileTests
dotnet test Tests/LevelTests
python Tools/ProjectMaintenance/validate_game_references.py
& 'S:/Godot4/Godot4CSharp_console.exe' --headless --path Godot/GodotProject --quit-after 2400 -- --smoketest=ui
```

菜单与 GGF 声音回归使用 `--smoketest=frontend`；非 headless 模式同时在 `.godot/validation/frontend/` 保存实际截图。验证结果与已知限制统一见 [验证记录](Docs/Validation.md)。

引擎回归还支持 `--smoketest`（英雄与命中）、`--smoketest=ai`、`--smoketest=level`、`--smoketest=level-cancel`。各模式使用随机的 `user://Validation/<模式>/<ID>` 目录，隔离玩家进度。以 `SMOKE PASS/FAIL` 判断断言，同时检查退出码；框架既有退出异常单独记录在 [验证规范](Docs/ProjectGuidelines/50-FrameworkAndTools.md)。

无双裁剪在 `BattleHud.tscn → MenuPanel/m_WsMax → Material → Shader Parameters` 调整：`clip_radius` 默认 32 原图像素，`clip_center` 默认 (0.5, 0.5)，`clip_feather` shader 默认 1 像素且只向内羽化（当前场景保留用户调整值 0）。运行下述命令可生成实际 GPU 渲染对比图：

```powershell
& 'S:/Godot4/Godot4CSharp_console.exe' --path Godot/GodotProject --script res://EditorScripts/validate_musou_flash.gd
```

## 文档与框架来源

- [项目导览](Docs/ProjectTour.md)：先读这里了解项目架构与分工。
- [集中规范](Docs/ProjectGuidelines/AGENTS.md)：目录、代码、配置、资源和存档约束。
- [GGF API 文档](Godot/docs/README.md)：查询框架接口；部分历史示例不代表当前游戏。
- [旧资源映射](Docs/LegacyAssetMap.md)：素材来源和迁移说明。
- [GGF](https://github.com/NuoYan/GGF) 是 [Game Framework](https://gameframework.cn/) 的 Godot 移植。本项目保留依赖来源及其许可证，框架和插件不随业务优化直接修改。

本项目仅用于学习；按项目规范禁止商用与公开发布。
