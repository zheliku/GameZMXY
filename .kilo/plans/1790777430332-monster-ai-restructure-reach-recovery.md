# 怪物 AI 整理：目录重组 + 显式大脑原型 + 判定盒推导攻击范围 + 收招硬直

## 背景（已核实）

- **目录混乱**：`HeroEntity.cs` 在 `Entity/` 根目录，`WukongEntity.cs` 却在 `Heroes/`；`WukongEntity` 的命名空间是 `GameLogic.Entity`，猴子是 `GameLogic.Entity.Monsters`；`Entity/AI/` 下 13 个文件全是怪物专用。
- **空中目标 bug**：感知和选招只比水平距离（`MonsterAiRules.InAttackRange`、`MonsterSkillBook.IsUsable`），`IMonsterAiAgent` 没有垂直信息（从旧 `follow_Hero` 继承来的）。
- **"攻击被打断再转身"**：代码里出招期间朝向有三道锁（`Face` 要求段已归位；出招时 `UpdateLocomotion` 不调 `SetFacing`；AI 出招中直接 return）。能在出招中途转身的只有两条路：受击打断（`OnHurt` 里 `InterruptAttack` 加上面向攻击者），或者 0.4s 的 `attack_1` 一收招，下一帧 Attack 状态就 `Face(目标)`。两种都说明缺一段**收招硬直**。
- **猴子类是空的**：它用哪套 AI，全靠 `MonsterEntity` 的默认值暗中决定。

## 人类已裁决

1. **显式大脑原型**：`MonsterEntity` 改为抽象的身体层，每种怪必须覆写 `CreateBrain()` 选一套原型（如 `MonsterBrains.GroundMelee()`），需要时再 Bind 或 AddExtra。参考：tModLoader `aiStyle`、Unity Game Kit `EnemyController` + 每怪一个 Behaviour、Hollow Knight 共享 HealthManager + 每怪一份 FSM。
2. **"能不能打到"从动画判定盒推导**：唯一真相源是 `attack_N` 的判定盒值轨道。删除 `MonsterConfig.AttackRange`；`AttackConfig.AiRange` 只用于没有身体判定盒的远程技能（`0,0` 表示按判定盒推导）；`AttackRangeSlack` 保留，作为离开站定的滞回。
3. **收招硬直**：`AttackConfig` 表尾追加 `AiRecovery`（float 秒，猴子初值 0.3）。受击**仍然**打断出招（保持旧项目手感）。
4. **文件移动**：`.cs` 与 `.cs.uid` 一起用 `git mv`，同时改 `.tscn` 里 ext_resource 的 `path` 文本（uid 不变）。这是 §10 "只在编辑器内移动"的一次性例外，只用于脚本，交付时要报告。

## 目标目录（命名空间 = GameLogic.Entity 加子目录段；按种类分的文件夹不再加命名空间段）

```
GameScripts/Entity/
├─ ActorEntity.cs  HurtBox.cs                 GameLogic.Entity
├─ Heroes/
│  ├─ HeroEntity.cs                           GameLogic.Entity.Heroes
│  └─ Wukong/WukongEntity.cs                  GameLogic.Entity.Heroes
└─ Monsters/
   ├─ MonsterEntity.cs  AttackReachReader.cs  GameLogic.Entity.Monsters
   ├─ AI/  IMonsterAiAgent MonsterAiState MonsterAiRole MonsterAiStateSet
   │       MonsterBrains(新) MonsterAiRules MonsterAiParams MonsterSkillBook AiBox(新)
   │                                          GameLogic.Entity.Monsters.AI
   │  └─ States/ Idle Patrol Chase Attack CcLocked Death   GameLogic.Entity.Monsters.AI.States
   └─ HuaguoshanMonkey/HuaguoshanMonkeyEntity.cs  GameLogic.Entity.Monsters
```

以后某只怪的专属状态放在 `Monsters/<种类>/` 下。`UI/`、`Manager/`、`MainPack/Scripts/Debug/` 保持不动（职责已经清楚）。

## 任务

### 1. 目录重组（纯重构，行为不变）
- `git mv` 上表所有 `.cs` + `.cs.uid`，更新命名空间和 `using`（包括 `SmokeTestDriver.cs`、`MonsterAiSmokeScenario.cs`、`Tests/BattleTests/MonsterAiTests.cs`）。
- 更新 `WukongEntity.tscn`、`HuaguoshanMonkeyEntity.tscn` 的 ext_resource `path`（HurtBox 路径不变）。
- `Tests/BattleTests/BattleTests.csproj`：AI 的 glob 改为 `Entity\Monsters\AI\**\*.cs`。
- 验证：`dotnet build`（工作目录 `Godot/GodotProject`）→ `--build-solutions` → `dotnet test Tests/BattleTests` → `--smoketest=ai` 仍然通过。完成这一步再开始改行为。

### 2. 配表（先改表再写代码）
- 先 dump 当前 `MonsterConfig.xlsx` / `AttackConfig.xlsx` 的数据行，把 `build_m2_tables.py` 的种子行对齐到当前手工值。
- `MonsterConfig`：删除 `AttackRange` 列（中间列属于结构变更，要 `force_rows=True`）。同步更新 `SightRange` 旁边的注释和 `AttackRangeSlack` 的描述（"判定盒外的水平滞回 px"）。
- `AttackConfig`：表尾追加 `("AiRecovery","float","AI 收招硬直秒:收招后原地不动、不转身、不出招")`；把 `AiRange` 的描述改为"仅远程/无身体判定盒的招式;0,0=按动画判定盒推导"；猴子 2001 行 `AiRange` 改为 `0,0`，`AiRecovery=0.3`；悟空行填 0。改 AiRange 数值要用 `force_rows=True`。
- 导表；前后 dump 做 diff，确认只有上面这些变化。`GameProto/` 与 `DataTables/` 一起更新。

### 3. 判定盒推导攻击范围
- 新增 `AI/AiBox.cs`：纯 C# 值类型 `{Left, Right, Top, Bottom}`，坐标相对怪物原点。`AI/` 不引用 Godot 类型。
- `SkillSpec` 增加 `Reach`（原生朝左坐标下的 AiBox，空 = 无判定盒）和 `Recovery`。
- 新增 `Monsters/AttackReachReader.cs`（静态、无状态）：从 `AnimPlayer.GetAnimation("attack_N")` 里按"AnimPlayer 根节点 → m_HitBox 下 CollisionShape2D"的相对路径（`GetPathTo` 推出来，不硬编码）找到 `:disabled` / `:shape` / `:position` 三条轨道。在每个 disabled=false 的区间内，取区间起点和区间内每个关键帧时刻的 shape 和 position（离散轨道取 ≤t 的最后一个 key），用 `shape.GetRect()` 加上 position 求并集，再加上 `m_HitBoxRoot` 的本地位置。没有启用窗口时返回空。
- `MonsterEntity.OnInit`（`isNewInstance`）：为每招算出 Reach。遇到 `Reach 为空 且 AiRange == 0,0 且 AiWeight>0` 的招式要 Log.Warning（AI 用不了这招）。删掉旧的 AiRange 与 AttackRange 比较告警。
- `HurtBox.cs` 增加 `Rect2 GetGlobalBounds()`（子 CollisionShape2D 的 `GetRect()` 乘以 GlobalTransform）。
- `IMonsterAiAgent`：用 `AiBox TargetBox`（目标受击盒相对自己原点；没有 HurtBox 时退化成点）替换掉单独使用的 `TargetDeltaX`。`TargetDeltaX` 保留给巡逻/朝向用，取 TargetBox 的中心。
- `MonsterAiRules`（纯函数，替换 `InAttackRange`）：
  - `ToFacing(AiBox reach, int dir)`：原生朝左，dir=1 时镜像 X。
  - `HorizontalGap(reach, target, dir)`：两个 X 区间的间隙，重叠时为 0。
  - `Reaches(reach, target, dir)`：X 和 Y 都重叠。
  - 远程招式（Reach 为空）继续用 `|dx| ∈ AiRange`。
  - dir 一律取"面向目标"的方向（出招提交时本来就会转向目标）。
- `MonsterSkillBook.IsUsable/SelectBasic/SelectSkill/HasBasicInRange` 的参数从 `distance` 改为 `(AiBox target, int dir)`。
- 状态逻辑：
  - Chase：任一普攻 `HorizontalGap==0` → Attack（不看高度）。
  - Attack：普攻的最小 `HorizontalGap > AttackRangeSlack` → Chase。水平够得着但**高度够不着**时：站定、面向目标、**不掷骰不出招**（目标落地后再打）。技能照旧按 `IsUsable` 判定。
  - `MonsterAiParams` 删除 `AttackRange`。

### 4. 收招硬直
- `MonsterEntity`：`OnAttackEnd` 时按本招 `Recovery` 设置 `m_RecoveryTime`。硬直期间 `MoveInput=0`，`Face` 和 `CommitPendingAttack` 都无效，速度 X 归 0。`IMonsterAiAgent.IsAttacking` 覆盖"段 ≥0 / 待提交 / 硬直中"三种情况，注释更新为"出招中（含收招硬直）"。
- `InterruptAttack`（受击/死亡）和 `OnShow` 都清零硬直。动画不用改：硬直期间 `AttackSegment=-1`，播放 Ground/idle。

### 5. 显式大脑原型
- `MonsterEntity` 改为 `abstract partial class`。新增 `protected abstract MonsterAiStateSet CreateBrain();`，删除 `ConfigureAi`。`CreateAi` 改为调用 `CreateBrain()`，返回 null 时 Log.Error 并不建 AI。
- 新增 `AI/MonsterBrains.cs`（静态工厂，每次都 new 一套新实例）：`GroundMelee()` = 现在 `MonsterAiStateSet.CreateDefault()` 的内容。`CreateDefault` 删除或改为内部调用。
- `HuaguoshanMonkeyEntity`：覆写 `CreateBrain() => MonsterBrains.GroundMelee();`，并把 `PopAnchor (0,-50)` 从 `MonsterEntity` 挪到猴子类里（这是猴子素材高度决定的布局常数）。类注释写清：它用哪套原型、数值在哪几行表、有哪些专属覆写。
- 风险：Godot C# 的 abstract 脚本类没有场景直接挂载时应该能编译。如果 `--build-solutions` 或编辑器报错，退回为 `virtual` 方法，默认实现是 Log.Error + 返回 null，并在交付时报告。

### 6. 测试
- `MonsterAiTests`（假宿主增加 TargetBox 和硬直）：
  - `HorizontalGap/Reaches` 覆盖左右朝向和上下越界。
  - 目标在头顶时 Attack 状态不 `RequestAttack`、也不回 Chase。
  - 硬直期间状态不做决策。
  - 远程招 AiRange 的路径仍然有效。
- 新增 `AttackReachReader` 的冒烟断言：猴子 attack_1 推导出的矩形 = 50×50 @(-24,-23) 的并集，即 X∈[-49,1]，Y∈[-48,2]。放进 `--smoketest=ai` 启动时的日志断言。
- 扩展 `MonsterAiSmokeScenario`（时间轴可以重排，保持 Chase→Attack→CcLocked→Death 的原断言）：
  - **空中**：把英雄固定在猴子正上方约 120px 处 2s（每物理帧重设位置、速度归零），断言这段时间 `AttackSegment` 一直是 -1，AI 停在 Attack。
  - **转身**：观察到猴子出招（seg≥0）的当帧，把英雄瞬移到它身后。断言 `Facing` 保持不变、动画一直是 `Attack/attack_1`，直到 seg 回到 -1 并且硬直结束（约 0.3s）之后才转向。
- 全部验证：`dotnet build` → `--build-solutions` → `dotnet test Tests/BattleTests` → `build_huaguoshan_monkey_anim_tree.gd`（只校验）→ `--smoketest=ai`（以及现有的其它冒烟测试）。

### 7. 文档（人类发起的结构变更，同步规范）
- 根 `AGENTS.md` §3.1 的 `Entity/` 子树；`GameScripts/AGENTS.md` 表格里的 `Entity/` 行（`Monsters/AI/`）。
- `Entity/AGENTS.md`「怪物 AI」：路径、感知（判定盒推导、高度、滞回、收招硬直）、扩展分层表：
  - 第 1 层 = 覆写 `CreateBrain` 选原型 + 数据；
  - 第 2 层 = 在原型上 Bind 替换；
  - 第 3 层 = AddExtra；
  - 第 4 层 = 招式效果。
- `Configs/AGENTS.md` 第 25 行：删 `AttackRange`，加 `AiRecovery`，写清 `AiRange` 的新语义，删掉"AiRange 上限 ≥ AttackRange+Slack"那条规则。
- 不提交 git（用户没要求）。交付时建议分两次提交：`refactor:`（第 1 步）和 `feat:`（第 2–7 步）。

## 超出范围
- 猴子的独有行为（后跳、呼叫同伴等）等人类以后再定。
- 飞行怪 / 远程怪原型（`MonsterBrains` 里只留扩展位）。
- 英雄侧的判定盒推导。
