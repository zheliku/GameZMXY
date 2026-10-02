# EditorScripts/ 目录规范（编辑器脚本）

> 裁决顺序见根规范 §2。GDScript 脚本（`extends SceneTree`），只校验资源、不写产物（生成已并入 `Tools/LegacyMigration/gen_animations.py`），不参与打包。

```bat
"S:\Godot4\Godot4CSharp_console.exe" --headless --path Godot/GodotProject --script res://EditorScripts/<脚本>.gd
```

- `validate_anim_libraries.gd`：校验角色动画库（每个角色在脚本头部配置表占一行），退出码非 0 = 失败。规则（契约见 `GameScripts/Entity/AGENTS.md`「状态与动画」）：
  - 必需动画齐全、`attack_<i>` 编号连续；
  - **禁止一切方法轨道**——动画库只放表现数据，逻辑/时点/音效归代码（A 方案：动画纯数据、代码唯一时钟）；
  - 身体状态要计时的动画（`attack_*` / `hurt` / `death` / 角色憨笑动画）**不得循环**（时长 = 长度是隐含契约）；
  - **值轨道完备性**：库内任一动画写过的值轨道路径，每个动画（含 RESET）都必须带（AnimationPlayer 直驱不回卷轨道，缺轨会残留上一个动画的值）。
- **编辑器为真相源**（2026-09-30 人类裁决）：动画库在编辑器里改，改完跑一次本脚本校验，再跑冒烟测试（`SmokeTestDriver`）。首版生成走 `Tools/LegacyMigration/gen_animations.py`（一次性迁移工具，产物登记 `Docs/LegacyAssetMap.md`）。
- 新角色 = 配置表加一行（必需动画/憨笑动画名）+ `gen_animations.py` 加生成段。
- 只读旧项目，不写入。
