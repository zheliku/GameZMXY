# Audios/ 目录规范

> 裁决顺序见根规范 §2；bundle 机制见 `../AGENTS.md`。

```
Audios/
├─ BGM/              # 按需创建
├─ SFX/<owner>/      # 按归属者分子目录（如 wukong/）
└─ AudioBus.tres     # 总线布局（Music / SFX / UI，与 SoundGroupRes 对齐）
```

- 命名 snake_case、basename 全树唯一，按"声音本身是什么"命名（`wukong_hit_impact.mp3`），不按用途；旧项目文件名可保留 + `LegacyAssetMap.md` 登记。
- 接入：在 `SoundConfig` 表登记（表结构与触发者规则见 `Configs/AGENTS.md`，单一真相源）；代码只引用 `SoundId`（`ActorEntity.PlaySound`），统一走 `GF.Sound.PlaySound(path, group)`，不出现资源路径。
- 不新增声音组：Music / SFX / UI 即全部，表 `Group` 与总线名对齐。
