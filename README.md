# GameZMXY

基于Godot 4.7 .NET、GGF与QF职责划分的2D横版动作学习项目。当前悟空、唐僧均可单人游玩，十关主线与隐藏龙宫已接入；尚未完整复刻造一全部内容，双人合作/竞技未实现。

从Godot .NET编辑器导入`Godot/GodotProject/project.godot`，F5进入正式流程；打开`TheGame/Scenes/test_arena.tscn`后F6运行独立内存战斗场地，不读写玩家存档。当前依赖为Godot.NET.Sdk 4.7.2、.NET 10、Newtonsoft.Json 13.0.4；Compatibility渲染、940×590设计视口。

| 入口 | 内容 |
| --- | --- |
| [项目导览](Docs/ProjectTour.md) | 当前完成边界、单人缺口、双人方案、代码与资源入口 |
| [唯一项目规范](Docs/ProjectGuidelines/AGENTS.md) | Godot/GGF/QF架构、代码、配置、场景和工具约束 |
| [官方依据与差异](Docs/OfficialReference.md) | 造一公开包版本、规则和项目保留差异 |
| [素材来源](Docs/LegacyAssetMap.md) | 可重跑导出、角色selector、原点与分层 |
| [验证](Docs/Validation.md) | 构建/测试/引擎命令、实际证据及已知限制 |
| [GGF资料](Godot/docs/README.md) | 只读框架接口参考，不覆盖项目规范 |

游戏业务位于`Godot/GodotProject/TheGame/`，源表位于`Configs/GameConfig/`，规则测试位于`Tests/`，维护与迁移工具位于`Tools/`。生成物经既有入口更新；Framework、addons、Luban和旧项目遵守只读边界。

[GGF](https://github.com/NuoYan/GGF)是[Game Framework](https://gameframework.cn/)的Godot移植，本项目保留依赖来源与许可证。项目仅用于学习，禁止商用与公开发布。
