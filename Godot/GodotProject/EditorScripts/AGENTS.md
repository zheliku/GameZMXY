# EditorScripts/ 目录规范（生成器脚本）

> 裁决顺序见根规范 §2。GDScript 生成器（`extends SceneTree`），产物是 `.tres` 资源，不参与打包。

```bat
"S:\Godot4\Godot4CSharp_console.exe" --headless --path Godot/GodotProject --script res://EditorScripts/<脚本>.gd
```

- 脚本自带校验（缺资源/普攻段不连续在保存前拦截）；退出码非 0 = 失败，校验不过不写产物。
- **编辑器为真相源**（2026-09-30 人类裁决）：产物已存在时以 Godot 编辑器保存的 `.tres` 为准，脚本默认只校验、不覆盖；首次生成 / 产物被删 / 命令末尾加 `-- --force` 才重建（`--force` 会冲掉编辑器修改，用前先提交）。改图直接在编辑器里改，改完跑一次脚本做校验 + 冒烟测试（`SmokeTestDriver`）；脚本里的状态表/边表是初版设计记录。
- headless 保存不写 `uid`，脚本须补写固定 `OUT_UID`；改 UID 必须同步所有按 uid 引用它的场景。
- 输出路径、UID、角色差异表写在脚本头部常量；新角色 = 复制一份生成器只改差异。
- 只读旧项目，不写入。
