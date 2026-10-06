# 20 配置与 Luban

- 源数据位于 `Configs/GameConfig/Datas/`，生成 C# 与二进制位于 `TheGame/GameScripts/GameProto/GameConfig/`、`TheGame/DataTables/GameConfigs/`。
- 改表后运行 `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`，提交时将源表和对应生成物保持同步。不得手改生成代码或 `.bytes`。
- 业务表包含中文名称与说明；多行表有显式主键，枚举值显式赋值。字段中文注释会进入生成代码文档。
- `LevelId` 是关卡唯一主键；阶段归属由嵌套列表表达（不再用 `LevelId`/`StageOrder` 平铺外键），怪物使用 `MonsterEntityId` 枚举名。
- 有旧项目对应项时保留 `LegacyId` 作为溯源；资源路径使用明确字段，不由名称拼接。
- 可调玩法数值进表；工程时序、缓存上限等非玩法常量可留在代码中，并命名说明。
- 当前攻击、战斗、英雄、怪物、音效及实体资源配置见 `Configs/GameConfig/Datas/`。经验表尚无运行时消费者；新表和数值先按任务需要补源表。
- 关卡只使用 `LevelConfig.xlsx` 一张表（`Level.TbLevelConfig`，map 模式，主键 `LevelId`，`read_schema_from_file=false`）。记录 bean `Level.LevelConfig`、`Level.LevelStage`、`Level.LevelSpawnRecipe` 定义在 `__beans__.xlsx`；数据表用多级 `##var` 标题行做列限定，不再有 `LevelStageConfig` / `RowId` / `Sequence` / `StageCount` 等外键与冗余字段。
- 关卡表采用嵌套多行列表（Luban `*name` 语法，参考 datable.cn《嵌套结构与容器》）：`*Stages` 是关卡行下的阶段列表，同一 `LevelId` 下每行一个阶段；每个阶段内 `*Recipes` 是生成配方列表，**同阶段各配方并行生成（同一波，如 stage_1_a 与 stage_1_b 同时出怪）**，行序只是表格可读性，`Delay`/`Interval`/`Count` 是每条配方自己的生成流参数。填写规则：
  - 外层字段的标题行做列合并，合并范围必须覆盖元素的完整宽度（含嵌套多行字段），不足会解析失败；
  - 配方续行的阶段列、关卡列全部留空（原子列留空 = 类型默认值），只在首行填写阶段/关卡字段；
  - 数据表不需要 `##type` 行，类型来自 `__beans__.xlsx` 的 bean 定义。
- 阶段的 `GateId`（可空）引用场景内唯一阻挡门，语义是**本阶段区域右边界**：行进阶段关闭右门；相机抵达后关闭前门防回头；本阶段清除后开启右门放行；空表示区域右界延伸到关卡原始边界（如最终阶段）。一门一阶段；门的位置（关卡空间事实）决定阶段区域，运行时校验区域宽度不小于一屏视口。
- 人类可读性优先：策划可见的表列与场景导出一律用可读标识（`MonsterEntityId` 填 `HuaguoshanMonkey` 等枚举名），程序运行时仍按 ID/枚举查找；不在表或场景里出现需要反查的裸数字 ID。当关卡配方使用 `MonsterEntityId` 查找 `MonsterConfig` 时，`MonsterConfig.EntityId` 必须与怪物配置保持一对一；运行时会校验并拒绝零条或多条匹配。若以后需要同一实体外观对应多个数值变体，应再引入可读的 MonsterConfig 业务键，而不是回退到数字 ID。
- `##` 注释行的中文注释与列一一对齐：每个数据列一个注释；多行/合并字段把整段说明写在首列，禁止注释整行错位。
- Luban 空单元格不会继承上一行：多行列表续行里留空只是“该元素此列为默认值”，关卡级/阶段级字段不会被视觉空白自动合并进续行。
- 表只描述关卡身份、阶段编排和生成配方；空间位置只写 `SpawnPointId` 外键，不复制场景坐标。各职责所有者在初始化时校验：阶段序列校验 `StageOrder` 等于行序且从 1 连续递增及激活方式；空间目录校验外键唯一且存在；刷怪服务校验配方数值与实体配置；缺失或冲突时中止关卡初始化，禁止静默退回 `(0, 0)`。改表后先导表，再执行构建。
- 配置目录细节与字段约定只维护在此文件，校验改表后执行 `dotnet build`。

- 普通阶段的 `Activation` 填 `CameraArrived`：以实际相机右缘抵达本阶段右界为启动条件，首阶段也遵守此规则。`Trigger` 保留给确实需要区域激活的特殊阶段；只有该方式填写 `TriggerId`。不再提供清波即开战的 `OnClear`。
- `SpawnDelay` 是阶段启动后的统一延迟（秒）；每条配方首只实际等待 `SpawnDelay + Delay`，后续按本条 `Interval` 生成。同阶段各配方独立计时并共享 `MaxActive`，预约但尚未显示的实体也占名额。
- 当前只有“配方发完且敌人清空”一种清波方式，删除无实际分支的 `ClearPolicy`；末阶段由列表末项确定，删除冗余 `IsFinal`。以后有实际新玩法时再设计新字段。
- 配方续行保持外层列为空，不复制阶段延迟；中文说明行逐列对齐。可读枚举在 Godot `.tscn` 中仍会序列化为整数，这是引擎存储格式；策划在检查器中看到枚举名称。
