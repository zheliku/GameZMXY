# Sprites/ 目录规范（贴图命名与 Collection Res）

> 根规范：仓库根 `AGENTS.md`（先读）；TheGame 规范：`../AGENTS.md`（bundle 机制）。冲突时：根规范 > TheGame > 本文件。
> 本文件只写本目录特有约定；规范变更由人类发起。

## 目录结构

```
Sprites/
├─ Characters/Heroes/<HeroId>/      # wukong / tangseng / bajie / wujing / bailong
├─ Characters/Monsters/<MonsterId>/ # 语义英文 ID，如 huaguoshan_monkey
├─ Equipments/<HeroId>/             # 装备外观图层
├─ Icons/Items/  Icons/Skills/  Icons/Buffs/  Icons/Equipments/
├─ Effects/                         # 打击/技能/buff 特效
├─ UI/                              # 界面素材（按界面分子目录）
├─ Levels/                          # 关卡背景与地块
└─ Number/                          # 伤害数字贴图
```

## 命名规则

- **新制作/新整理的资源**：一律 snake_case 英文小写，语义完整。
  - 图集：`<entity>_<state>.png`，如 `wukong_idle.png`、`huaguoshan_monkey_hurt.png`（**basename 必须全局唯一**，理由见下节）
  - 动画名标准：`idle / run / jump / attack_1..n / skill_<skillId> / hurt / death`
  - 图标：`icon_<itemId>.png`、`skill_<skillId>.png`、`buff_<buffId>.png`
- **从旧项目搬来的资源**：文件名可保留原样（根规范 §11.3），但目录必须按上表归位，并在 `Docs/LegacyAssetMap.md` 登记。禁止出现新的拼音缩写名。
- 大小写扩展名（`.PNG` / `.TTF`）统一为小写；`.import`/`.uid` 边车文件提交进 git；移动/重命名只在 Godot 编辑器内做。

## Collection Res 全树索引约束（重要）

TopMenu「Generate File → Collection Res」会扫描 `res://TheGame/` **全部**非 `.cs`/非 `GameScripts/`/非 `.import`/非 `.uid` 文件生成常量（`Sprites_Characters/Heroes/...` 拼成 `<父目录名>_<文件名>`）：

1. **全树同名文件会导致生成中止**（如 `Wait.png` × 145 这类全树重名即触发）。这正是本目录要求图集带实体前缀（`wukong_idle.png` 而非 `idle.png`）的原因——不只是审美，是生成器的硬性约束。
2. 文件名开头是数字会生成 `_数字` 常量（合法但丑）；迁移遗留资源时，旧项目大量 `1.png / 2.png / 637.png` 会造成全树重名冲突。**处理方法**：搬入后如发现冲突，在编辑器内重命名该文件（生成器会输出具体冲突名单），不要为了"保持原样"牺牲生成器。
3. 生成失败不影响运行（配置走 Luban `AssetPath`、场景走 UID），但本项目把 `Collection Res` 作为常规资源索引手段，视为 CI 的一部分，每次提交前跑一次确保通过。
4. 动画数据放 `Sprites/Characters/.../<entity>_animations.tres`（SpriteFrames 资源），源图与帧规格登记进 `Docs/LegacyAssetMap.md`（旧项目迁移流程见根规范 §11.3）。
