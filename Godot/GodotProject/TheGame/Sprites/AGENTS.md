# Sprites/ 目录规范

> 裁决顺序见根规范 §2；bundle 机制见 `../AGENTS.md`。

```
Sprites/
├─ Characters/Heroes/<HeroId>/       # wukong / tangseng / ...
├─ Characters/Monsters/<MonsterId>/  # 如 huaguoshan_monkey
├─ Equipments/<HeroId>/              # 装备外观（按需）
├─ Icons/{Items,Skills,Buffs,Equipments}/  # 按需
├─ Effects/                          # 特效
├─ UI/  Levels/  Number/             # 界面 / 关卡 / 伤害数字（UI、Number 按需）
```

## 命名

- 新资源 snake_case 英文小写；图集 `<entity>_<state>.png`（如 `wukong_idle.png`），basename 全树唯一（原因见下节）；图标 `icon_<itemId>.png` 等。
- 动画名标准：`idle1/idle2 / walk / run / jump / jump_2 / fall / attack_1..n / hurt / death`，技能 `skill_<skillId>` 随技能系统。
- 旧项目搬入的文件名可保留原样，但目录归位 + `LegacyAssetMap.md` 登记（根规范 §11），禁止新增拼音缩写名。
- 扩展名统一小写；`.import`/`.uid` 提交 git；移动/重命名只在编辑器内做。

## Collection Res 全树索引约束

TopMenu「Generate File → Collection Res」扫描 `res://TheGame/` 全部非 `.cs`/`.import`/`.uid` 文件生成常量（`<父目录名>_<文件名>`）：

1. **全树同名 → 生成中止**：这是图集必须带实体前缀的原因，不只是审美。
2. 数字开头文件名生成 `_数字` 常量；旧项目大量 `1.png` 会冲突——搬入后发现冲突就在编辑器重命名（生成器输出冲突名单）。
3. 生成失败不影响运行（配置走 Luban `AssetPath`、场景走 UID），但本项目把 Collection Res 当 CI，提交前必须通过。
4. 动画 `.tres` 放 `Characters/.../<entity>_animations.tres`，源图与帧规格登记 `LegacyAssetMap.md`。
