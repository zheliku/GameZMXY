# 20 配置与 Luban

- 源数据位于 `Configs/GameConfig/Datas/`，生成 C# 与二进制位于 `TheGame/GameScripts/GameProto/GameConfig/`、`TheGame/DataTables/GameConfigs/`。
- 改表后运行 `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`，提交时将源表和对应生成物保持同步。不得手改生成代码或 `.bytes`。
- 业务表包含中文名称与说明；多行表有显式主键，枚举值显式赋值。字段中文注释会进入生成代码文档。
- 有旧项目对应项时保留 `LegacyId` 作为溯源；资源路径使用明确字段，不由名称拼接。
- 可调玩法数值进表；工程时序、缓存上限等非玩法常量可留在代码中，并命名说明。
- 当前攻击、战斗、英雄、怪物、音效及实体资源配置见 `Configs/GameConfig/Datas/`。经验表尚无运行时消费者；新表和数值先按任务需要补源表。
- 配置目录细节与字段约定只维护在此文件，校验改表后执行 `dotnet build`。
