# 20 配置与 Luban

- 源数据位于 `Configs/GameConfig/Datas/`，生成 C# 与二进制位于 `TheGame/GameScripts/GameProto/GameConfig/`、`TheGame/DataTables/GameConfigs/`。
- 改表后运行 `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`，提交时将源表和对应生成物保持同步。不得手改生成代码或 `.bytes`。
- 业务表包含中文名称与说明；多行表有显式主键，枚举值显式赋值。字段中文注释会进入生成代码文档。
- 关卡 `RowId` 只用于稳定标识表行，不作为业务查询键；阶段与配方使用 `LevelId` / `StageOrder` 关联，怪物使用 `MonsterEntityId` 枚举名。
- 有旧项目对应项时保留 `LegacyId` 作为溯源；资源路径使用明确字段，不由名称拼接。
- 可调玩法数值进表；工程时序、缓存上限等非玩法常量可留在代码中，并命名说明。
- 当前攻击、战斗、英雄、怪物、音效及实体资源配置见 `Configs/GameConfig/Datas/`。经验表尚无运行时消费者；新表和数值先按任务需要补源表。
- 关卡使用 `LevelConfig.xlsx` 和 `LevelStageConfig.xlsx` 两张表，分别生成 `TbLevelConfig`、`TbLevelStageConfig`。`LevelConfig` 使用 Luban `list` 模式：一个关卡按阶段占多行，`LevelId` 作为分组键重复填写，关卡名/场景路径/出生点/阶段总数只在 `StageOrder=1` 行填写，阶段字段每行填写。`LevelStageConfig` 每行是一条怪物生成配方，使用 `MonsterEntityId: Entity.EntityId` 直接填写 `HuaguoshanMonkey` 等可读枚举名，以 `LevelId + StageOrder` 关联阶段，并通过 `Sequence` 配置同一阶段的多种怪物/批次。
- 当关卡配方使用 `MonsterEntityId` 查找 `MonsterConfig` 时，`MonsterConfig.EntityId` 必须与怪物配置保持一对一；运行时会校验并拒绝零条或多条匹配。若以后需要同一实体外观对应多个数值变体，应再引入可读的 MonsterConfig 业务键，而不是在关卡表重复填写枚举和数字 ID。
- Luban 空单元格不会继承上一行；首阶段以外的关卡级字段必须保持空白并在运行时读作默认值，不能把“视觉上的空白”当作自动合并。
- 表只描述关卡身份、阶段编排和生成配方；空间位置只写 `SpawnPointId` 外键，不复制场景坐标。`LevelController` 校验关卡首行元数据、阶段数量/顺序、怪物 ID 与场景锚点；缺失或冲突时中止关卡初始化，禁止静默退回 `(0, 0)`。改表后先导表，再执行构建。
- 配置目录细节与字段约定只维护在此文件，校验改表后执行 `dotnet build`。
