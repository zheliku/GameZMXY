# EditorScripts/ 目录规范（生成器脚本）

> 裁决顺序见根规范 §2。GDScript 生成器（`extends SceneTree`），产物是 `.tres` 资源，不参与打包。

```bat
"S:\Godot4\Godot4CSharp_console.exe" --headless --path Godot/GodotProject --script res://EditorScripts/<脚本>.gd
```

- 脚本自带校验（缺资源/普攻段不连续在保存前拦截）；退出码非 0 = 失败，校验不过不写产物。
- 产物禁手改：改产物 = 改脚本里的状态表/边表（`GROUND_STATES` / `ROOT_EDGES` 等）+ 重跑 + 冒烟测试（`SmokeTestDriver`）。
- headless 保存不写 `uid`，脚本须补写固定 `OUT_UID`；改 UID 必须同步所有按 uid 引用它的场景。
- 输出路径、UID、角色差异表写在脚本头部常量；新角色 = 复制一份生成器只改差异。
- 只读旧项目，不写入。
