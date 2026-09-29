# Tools/ 目录规范（开发工具）

> 裁决顺序见根规范 §2。工具不参与打包、不进运行时；工具输出不是数值真相（真相在 `Configs/GameConfig/Datas/`）。

| 目录 | 用途 | 约束 |
| --- | --- | --- |
| `Luban/` | vendored 导表工具 | 不改二进制与模板；导表一律走 `Configs` 的 bat |
| `LegacyMigration/` | 旧项目素材/动画一次性脚本 | 旧项目只读；产物登记 `Docs/LegacyAssetMap.md` |
| `ConfigBootstrap/` | Luban 表种子/修复脚本 | 不重建工作簿、不覆盖手工数值；结构变更须 `force_rows=True` |
| `FileServer/` | 本地静态服务器（热更调试） | 按需启动，不进发布流程 |

- 旧项目路径只允许出现在 `LegacyMigration/`。
- 一次性脚本保留可重跑、可追溯，跑完在交付说明里写清命令与结果；新增工具先在此登记再提交。
