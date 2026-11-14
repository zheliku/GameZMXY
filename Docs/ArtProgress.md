# 美术资源任务清单

更新时间：2026-11-15。本页是后续持续维护的可勾选清单，按**1920×1080**验收；4K显示器全屏允许继续放大。逻辑核心仍940×590。

**图像源文件：52 / 661 已完成制作、接入及本批必要验证（约7.9%，按文件数统计）；609项未完成，其中3项已有来源/保真限制结论、606项待处理。英雄/背包批次仍待用户审图。**

另列182份配套美术资源：字体、着色器、动画库、SpriteFrames、外观登记、主题与样式、地图透明命中遮罩，其中11份已完成本批密度联动或时序核对。这182份不加入661张图像的分母，也不重复计算图片完成量。

勾选含义：`[x]`表示该项对应的制作/接入/必要验证已完成；`[ ]`表示尚未完成。**完成实验、生成样稿或找到源文件，不等于素材高清通过；“等待用户审图”单独保留。** 原生SVG、字体和样式按1080p实际显示核对，需要时可保留原件，不强行栅格化或放大。

实施方案与分批顺序只维护在[CrossPlatformPlan 2.12](CrossPlatformPlan.md#212-美术前置阶段完整清单扩展规格与分批审查)；[LegacyAssetMap](LegacyAssetMap.md)记录来源，[Validation](Validation.md)记录验证命令、结果和退出诊断。本页只维护清单及完成状态。

以后每完成一批，同步更新本页的阶段勾选、分类计数、对应文件勾选和限制说明，再交用户审图；未通过的方法不勾成素材完成。

## 目前做了什么

- [x] 盘点661个图像源文件并检查引用/尺寸；本轮复核当前目录与原清单一一对应。
- [x] A1a：完成悟空31个代表姿态原图/1倍/2倍对照及1080p预览，矢量路线已获用户认可。
- [x] A1b：完成天空/木棍保真恢复试验；结论为不采用，两个正式原图保留。
- [x] 完成悟空13张战斗身体/武器图原源2倍重导并原路径替换。
- [x] 完成唐僧13张战斗身体/武器图原源2倍重导并原路径替换。
- [x] 完成悟空13张backpack_v2专用预览图原源2倍重导并原路径替换。
- [x] 完成唐僧13张backpack_v2专用预览图原源2倍重导并原路径替换。
- [x] 悟空174旧槽无损映射到86共享槽，逐项检查13×174=2262槽，遗漏0；所有播放键和时刻保持。
- [x] 复用原Appearance/两层Sprite完成密度补偿；200逻辑格、六列、角色尺度、位置和碰撞保持。
- [x] 完成6份英雄/背包动画库时序对照、实际换装、攻击覆盖、残影和光晕验证。
- [x] 默认窗口设为1920×1080；实测4K显示器全屏输出3840×2160，按用户确认接受放大。
- [x] 原PNG离线归档，原`.import`/UID及用户暂存内容保持；正式资源没有hd目录或后缀。
- [ ] 用户审查通过当前英雄/背包52图批次。

已完成这批的[1080p前后对照](../Godot/GodotProject/.godot/validation/art_a2_heroes_20261114/hero_review_contact.png)和[当前完整预览](../Godot/GodotProject/.godot/validation/art_a2_heroes_20261114/heroes_1920x1080_current.png)。完整验证见[Validation本批记录](Validation.md#美术-a2两英雄完整外观及专用背包2026-11-14原路径替换停审)。

## 后续阶段任务

- [ ] UI、头像、选角卡、地图地点、技能与物品入口：查源并逐批完成高清/原生显示验收。
- [ ] 英雄状态、小怪、Boss及装备外观配套效果：完整动作重导，保持源时序、装备身份和遮挡。
- [ ] 独立战斗特效、球、升级、死亡、出口动画：核对原生滤镜、透明度、注册点及全部动画帧。
- [ ] 装备/消耗品/地面图标：逐项核对高精度同内容源；来源不足或保真不通过时明确记录。
- [ ] 伤害/恢复数字和字图：保留字形、顺序、基线及比例字宽，完成1080p实际显示检查。
- [ ] 11关背景、地形、前景和平台：逐项查矢量/位图依赖，保留定位、接缝和碰撞。
- [ ] 字体、着色器、动画资源、主题/样式：按下方配套资源逐项完成联动或实际显示检查。
- [ ] A1c：只做journey_map外围扩图小样，完成保护区和四种比例预览后审图。
- [ ] A3：其余静态UI背景、选角侧景及五张共享天空的外围扩图；只在原画外新增环境。
- [ ] A4：两英雄48帧无双演出覆盖/高清及四张短关条件候选，取景裁决与实际效果分别验收。
- [ ] A5：汇总全库高清/仅扩图/保留原图结论和运行纹理预算，完成总审查。
- [ ] T2～T5：美术审查后恢复UI接入、关卡/正式宽屏、触屏、导出和真机验收。

这些是阶段任务，不额外计入下方文件总数。选角侧景等未来新增素材在实际创建时另行入表，不把不存在的文件加入当前661项。

## 图像完成量总览

| 分类 | 图像数 | 已完成实施 | 来源/保真限制未完成 | 其他待处理 |
| --- | ---: | ---: | ---: | ---: |
| [悟空战斗身体与武器](#art-images-01) | 13 | 13 | 0 | 0 |
| [唐僧战斗身体与武器](#art-images-02) | 13 | 13 | 0 | 0 |
| [悟空背包专用预览](#art-images-03) | 13 | 13 | 0 | 0 |
| [唐僧背包专用预览](#art-images-04) | 13 | 13 | 0 | 0 |
| [英雄状态与角色配套贴图](#art-images-05) | 4 | 0 | 0 | 4 |
| [敌人、Boss及其图集](#art-images-06) | 60 | 0 | 0 | 60 |
| [独立特效、掉落球、升级与演出](#art-images-07) | 189 | 0 | 0 | 189 |
| [UI、头像、地图、技能面板与无双画面](#art-images-08) | 225 | 0 | 0 | 225 |
| [装备、消耗品与地面图标](#art-images-09) | 79 | 0 | 1 | 78 |
| [伤害、恢复数字与文字图集](#art-images-10) | 10 | 0 | 0 | 10 |
| [关卡背景、地形、平台与前景](#art-images-11) | 37 | 0 | 2 | 35 |
| [主题SVG、工程图标与其他图像](#art-images-12) | 5 | 0 | 0 | 5 |
| **合计** | **661** | **52** | **3** | **606** |

## 全部图像文件（661项）

以下链接均指向当前正式文件；每个文件只列一次。目录相对`Godot/GodotProject/`；尺寸是本轮读取的当前源像素尺寸。`.import`与`.uid`随源资源维护，不作为第二张图重复列项。

<!-- art-images:start -->

<a id="art-images-01"></a>

### 悟空战斗身体与武器（13/13）

目录：`TheGame/Sprites/Characters/Heroes/wukong/`

- [x] [wukong_body_bare.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_body_bare.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_body_black_tortoise.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_body_black_tortoise.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_body_blood_sea.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_body_blood_sea.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_body_earth_fiend.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_body_earth_fiend.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_body_excellent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_body_excellent.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_body_pilgrim.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_body_pilgrim.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_weapon_azure_dragon.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_azure_dragon.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_weapon_default.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_default.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_weapon_excellent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_excellent.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_weapon_heavenly_fiend.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_heavenly_fiend.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_weapon_heirloom.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_heirloom.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_weapon_pilgrim.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_pilgrim.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_weapon_ruyi.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_ruyi.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，待批次审图。

<a id="art-images-02"></a>

### 唐僧战斗身体与武器（13/13）

目录：`TheGame/Sprites/Characters/Heroes/tangseng/`

- [x] [tangseng_body_bare.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_body_bare.png) — 1200×3400 → 2400×6800；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_body_cassock.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_body_cassock.png) — 1200×3400 → 2400×6800；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_body_excellent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_body_excellent.png) — 1200×3400 → 2400×6800；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_body_heavenly_fiend.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_body_heavenly_fiend.png) — 1200×3400 → 2400×6800；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_body_heirloom.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_body_heirloom.png) — 1200×3400 → 2400×6800；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_body_kirin.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_body_kirin.png) — 1200×3400 → 2400×6800；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_weapon_blood_sea.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_weapon_blood_sea.png) — 1200×1400 → 2400×2800；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_weapon_default.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_weapon_default.png) — 1200×1400 → 2400×2800；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_weapon_earth_fiend.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_weapon_earth_fiend.png) — 1200×1400 → 2400×2800；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_weapon_excellent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_weapon_excellent.png) — 1200×1400 → 2400×2800；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_weapon_nine_ring.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_weapon_nine_ring.png) — 1200×1400 → 2400×2800；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_weapon_pine.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_weapon_pine.png) — 1200×1400 → 2400×2800；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_weapon_white_tiger.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_weapon_white_tiger.png) — 1200×1400 → 2400×2800；原源2倍重导、已接入/验证，待批次审图。

<a id="art-images-03"></a>

### 悟空背包专用预览（13/13）

目录：`TheGame/Sprites/Characters/Heroes/wukong/preview/`

- [x] [wukong_preview_body_bare.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_body_bare.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_preview_body_black_tortoise.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_body_black_tortoise.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_preview_body_blood_sea.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_body_blood_sea.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_preview_body_earth_fiend.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_body_earth_fiend.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_preview_body_excellent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_body_excellent.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_preview_body_pilgrim.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_body_pilgrim.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_preview_weapon_azure_dragon.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_azure_dragon.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_preview_weapon_default.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_default.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_preview_weapon_excellent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_excellent.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_preview_weapon_heavenly_fiend.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_heavenly_fiend.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_preview_weapon_heirloom.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_heirloom.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_preview_weapon_pilgrim.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_pilgrim.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [wukong_preview_weapon_ruyi.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_ruyi.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。

<a id="art-images-04"></a>

### 唐僧背包专用预览（13/13）

目录：`TheGame/Sprites/Characters/Heroes/tangseng/preview/`

- [x] [tangseng_preview_body_bare.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_body_bare.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_preview_body_cassock.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_body_cassock.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_preview_body_excellent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_body_excellent.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_preview_body_heavenly_fiend.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_body_heavenly_fiend.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_preview_body_heirloom.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_body_heirloom.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_preview_body_kirin.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_body_kirin.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_preview_weapon_blood_sea.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_weapon_blood_sea.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_preview_weapon_default.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_weapon_default.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_preview_weapon_earth_fiend.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_weapon_earth_fiend.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_preview_weapon_excellent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_weapon_excellent.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_preview_weapon_nine_ring.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_weapon_nine_ring.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_preview_weapon_pine.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_weapon_pine.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。
- [x] [tangseng_preview_weapon_white_tiger.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_weapon_white_tiger.png) — 1200×200 → 2400×400；原源2倍重导、已接入/验证，待批次审图。

<a id="art-images-05"></a>

### 英雄状态与角色配套贴图（0/4）

目录：`TheGame/Sprites/Characters/Heroes/Status/`

- [ ] [hero_frozen.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/Status/hero_frozen.png) — 2048×896；待来源/清晰度处理及1080p验收。
- [ ] [poison_head.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/Status/poison_head.png) — 41×53；待来源/清晰度处理及1080p验收。
- [ ] [poison_up.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/Status/poison_up.png) — 1176×175；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Heroes/tangseng/`

- [ ] [tangseng_shadow_atlas.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_shadow_atlas.png) — 1200×4000；待来源/清晰度处理及1080p验收。

<a id="art-images-06"></a>

### 敌人、Boss及其图集（0/60）

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/`

- [ ] [macaque_king_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/macaque_king_body.png) — 2696×1560；待来源/清晰度处理及1080p验收。
- [ ] [macaque_king_effect.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/macaque_king_effect.png) — 2928×1490；待来源/清晰度处理及1080p验收。
- [ ] [macaque_wind_blade.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/macaque_wind_blade.png) — 5768×620；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_03_tamarin_king/`

- [ ] [tamarin_king_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_03_tamarin_king/tamarin_king_body.png) — 1528×810；待来源/清晰度处理及1080p验收。
- [ ] [tamarin_king_effect.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_03_tamarin_king/tamarin_king_effect.png) — 6520×3200；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_04_bat_cave/`

- [ ] [bat_cave_statue_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_04_bat_cave/bat_cave_statue_body.png) — 795×222；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_05_qixing_cave/`

- [ ] [lion_camel_king.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_king.png) — 5144×3179；待来源/清晰度处理及1080p验收。
- [ ] [lion_camel_poison_cloud.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_poison_cloud.png) — 2720×695；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_06_garuda_cave/`

- [ ] [peng_demon_king_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_06_garuda_cave/peng_demon_king_body.png) — 2088×2670；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/`

- [ ] [jiao_back_hit.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/jiao_back_hit.png) — 880×216；待来源/清晰度处理及1080p验收。
- [ ] [jiao_demon_king_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/jiao_demon_king_official_body.png) — 2048×3863；待来源/清晰度处理及1080p验收。
- [ ] [jiao_dragon_wave.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/jiao_dragon_wave.png) — 3808×388；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/`

- [ ] [shark_demon_king_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_demon_king_official_body.png) — 2048×3917；待来源/清晰度处理及1080p验收。
- [ ] [shark_king_bubble.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_bubble.png) — 2048×222；待来源/清晰度处理及1080p验收。
- [ ] [shark_king_bubble_warning.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_bubble_warning.png) — 512×324；待来源/清晰度处理及1080p验收。
- [ ] [shark_king_shark_wave.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_shark_wave.png) — 4096×972；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/`

- [ ] [bull_background.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_background.png) — 1024×472；待来源/清晰度处理及1080p验收。
- [ ] [bull_demon_beast_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_beast_official_body.png) — 512×787；待来源/清晰度处理及1080p验收。
- [ ] [bull_demon_heart_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_heart_official_body.png) — 512×61；待来源/清晰度处理及1080p验收。
- [ ] [bull_demon_king_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_king_official_body.png) — 2048×3838；待来源/清晰度处理及1080p验收。
- [ ] [bull_demon_king_tangseng_capture_atlas.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_king_tangseng_capture_atlas.png) — 476×289；待来源/清晰度处理及1080p验收。
- [ ] [bull_fire.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_fire.png) — 512×759；待来源/清晰度处理及1080p验收。
- [ ] [bull_hands.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_hands.png) — 512×185；待来源/清晰度处理及1080p验收。
- [ ] [bull_heart.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_heart.png) — 512×1218；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/`

- [ ] [dragon_king_ice_aura.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_ice_aura.png) — 512×4256；待来源/清晰度处理及1080p验收。
- [ ] [dragon_king_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_official_body.png) — 4096×2242；待来源/清晰度处理及1080p验收。
- [ ] [dragon_king_torrent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_torrent.png) — 512×4480；待来源/清晰度处理及1080p验收。
- [ ] [dragon_king_wave.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_wave.png) — 1024×312；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/bull_guard/`

- [ ] [bull_guard_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/bull_guard/bull_guard_official_body.png) — 512×1142；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/cave_brute/`

- [ ] [cave_brute.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/cave_brute/cave_brute.png) — 960×452；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/`

- [ ] [demon_monkey_attack.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_attack.png) — 520×134；待来源/清晰度处理及1080p验收。
- [ ] [demon_monkey_death.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_death.png) — 620×108；待来源/清晰度处理及1080p验收。
- [ ] [demon_monkey_hurt.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_hurt.png) — 89×87；待来源/清晰度处理及1080p验收。
- [ ] [demon_monkey_idle.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_idle.png) — 370×114；待来源/清晰度处理及1080p验收。
- [ ] [demon_monkey_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_official_body.png) — 1024×345；待来源/清晰度处理及1080p验收。
- [ ] [demon_monkey_run.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_run.png) — 464×105；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/`

- [ ] [gorilla_attack_1.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_attack_1.png) — 904×180；待来源/清晰度处理及1080p验收。
- [ ] [gorilla_death.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_death.png) — 708×142；待来源/清晰度处理及1080p验收。
- [ ] [gorilla_hurt.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_hurt.png) — 117×140；待来源/清晰度处理及1080p验收。
- [ ] [gorilla_idle.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_idle.png) — 650×137；待来源/清晰度处理及1080p验收。
- [ ] [gorilla_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_official_body.png) — 1024×592；待来源/清晰度处理及1080p验收。
- [ ] [gorilla_run.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_run.png) — 612×141；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/`

- [ ] [huaguoshan_monkey_attack.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_attack.png) — 546×83；待来源/清晰度处理及1080p验收。
- [ ] [huaguoshan_monkey_death.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_death.png) — 546×88；待来源/清晰度处理及1080p验收。
- [ ] [huaguoshan_monkey_hurt.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_hurt.png) — 136×86；待来源/清晰度处理及1080p验收。
- [ ] [huaguoshan_monkey_idle.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_idle.png) — 43×63；待来源/清晰度处理及1080p验收。
- [ ] [huaguoshan_monkey_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_official_body.png) — 1024×190；待来源/清晰度处理及1080p验收。
- [ ] [huaguoshan_monkey_run.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_run.png) — 256×88；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/knife_demon/`

- [ ] [knife_demon.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/knife_demon/knife_demon.png) — 1560×572；待来源/清晰度处理及1080p验收。
- [ ] [knife_demon_blade.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/knife_demon/knife_demon_blade.png) — 2664×57；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/lantern_fish/`

- [ ] [lantern_fish_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/lantern_fish/lantern_fish_official_body.png) — 2416×1088；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/`

- [ ] [shark_spawn_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/shark_spawn_official_body.png) — 2048×1968；待来源/清晰度处理及1080p验收。
- [ ] [shark_spawn_slash.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/shark_spawn_slash.png) — 512×1328；待来源/清晰度处理及1080p验收。
- [ ] [shark_spawn_tornado.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/shark_spawn_tornado.png) — 2048×137；待来源/清晰度处理及1080p验收。
- [ ] [shark_spawn_warning.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/shark_spawn_warning.png) — 512×464；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/shrimp_demon/`

- [ ] [shrimp_demon_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shrimp_demon/shrimp_demon_official_body.png) — 1024×590；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/tide_demon/`

- [ ] [tide_demon_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/tide_demon/tide_demon_official_body.png) — 1024×1528；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/turtle_minister/`

- [ ] [turtle_minister_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/turtle_minister/turtle_minister_official_body.png) — 2048×868；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sky/bat/`

- [ ] [bat_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sky/bat/bat_body.png) — 1456×1705；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sky/flying_demon/`

- [ ] [flying_demon_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sky/flying_demon/flying_demon_body.png) — 3120×1100；待来源/清晰度处理及1080p验收。

<a id="art-images-07"></a>

### 独立特效、掉落球、升级与演出（0/189）

目录：`TheGame/Sprites/Effects/Enemies/sea/`

- [ ] [shrimp_projectile.png](../Godot/GodotProject/TheGame/Sprites/Effects/Enemies/sea/shrimp_projectile.png) — 19×13；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Effects/Enemies/zm1_06_garuda_cave/`

- [ ] [boss_death.png](../Godot/GodotProject/TheGame/Sprites/Effects/Enemies/zm1_06_garuda_cave/boss_death.png) — 1002×167；待来源/清晰度处理及1080p验收。
- [ ] [peng_fire.png](../Godot/GodotProject/TheGame/Sprites/Effects/Enemies/zm1_06_garuda_cave/peng_fire.png) — 4976×1296；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Effects/hero_death/`

- [ ] [hero_tombstone_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_death/hero_tombstone_atlas.png) — 1832×1830；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Effects/hero_hurt/tangseng/`

- [ ] [tangseng_hurt_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_01.png) — 100×122；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hurt_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_02.png) — 100×122；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hurt_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_03.png) — 100×122；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hurt_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_04.png) — 100×122；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hurt_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_05.png) — 100×122；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hurt_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_06.png) — 100×122；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hurt_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_07.png) — 100×122；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hurt_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_08.png) — 100×122；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Effects/hero_hurt/wukong/`

- [ ] [hero_hurt_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_01.png) — 100×122；待来源/清晰度处理及1080p验收。
- [ ] [hero_hurt_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_02.png) — 100×122；待来源/清晰度处理及1080p验收。
- [ ] [hero_hurt_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_03.png) — 100×122；待来源/清晰度处理及1080p验收。
- [ ] [hero_hurt_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_04.png) — 100×122；待来源/清晰度处理及1080p验收。
- [ ] [hero_hurt_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_05.png) — 100×122；待来源/清晰度处理及1080p验收。
- [ ] [hero_hurt_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_06.png) — 100×122；待来源/清晰度处理及1080p验收。
- [ ] [hero_hurt_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_07.png) — 100×122；待来源/清晰度处理及1080p验收。
- [ ] [hero_hurt_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_08.png) — 100×122；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Effects/level_exit/`

- [ ] [level_exit_00.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_00.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_01.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_02.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_03.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_04.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_05.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_06.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_07.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_08.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_09.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_09.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_10.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_10.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_11.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_11.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_12.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_12.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_13.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_13.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_14.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_14.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_15.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_15.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_16.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_16.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_17.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_17.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_18.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_18.png) — 186×165；待来源/清晰度处理及1080p验收。
- [ ] [level_exit_19.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_19.png) — 186×165；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Effects/level_up/`

- [ ] [level_up_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_01.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_02.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_03.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_04.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_05.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_06.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_07.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_08.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_09.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_09.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_10.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_10.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_11.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_11.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_12.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_12.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_13.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_13.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_14.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_14.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_15.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_15.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_16.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_16.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_17.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_17.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_18.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_18.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_19.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_19.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_20.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_20.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_21.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_21.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_22.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_22.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_23.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_23.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_24.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_24.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_25.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_25.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_26.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_26.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_27.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_27.png) — 122×136；待来源/清晰度处理及1080p验收。
- [ ] [level_up_28.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_28.png) — 122×136；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Effects/loot/`

- [ ] [health_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_01.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_02.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_03.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_04.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_05.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_06.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_07.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_08.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_09.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_09.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_10.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_10.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_11.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_11.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_12.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_12.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_13.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_13.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_14.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_14.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_15.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_15.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_16.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_16.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_17.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_17.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_18.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_18.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [health_19.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_19.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_01.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_02.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_03.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_04.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_05.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_06.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_07.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_08.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_09.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_09.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_10.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_10.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_11.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_11.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_12.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_12.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_13.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_13.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_14.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_14.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_15.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_15.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_16.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_16.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_17.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_17.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_18.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_18.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [mana_19.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_19.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_01.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_02.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_03.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_04.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_05.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_06.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_07.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_08.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_09.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_09.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_10.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_10.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_11.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_11.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_12.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_12.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_13.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_13.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_14.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_14.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_15.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_15.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_16.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_16.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_17.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_17.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_18.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_18.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [musou_19.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_19.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_01.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_02.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_03.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_04.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_05.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_06.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_07.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_08.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_09.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_09.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_10.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_10.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_11.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_11.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_12.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_12.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_13.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_13.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_14.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_14.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_15.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_15.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_16.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_16.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_17.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_17.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_18.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_18.png) — 18×20；待来源/清晰度处理及1080p验收。
- [ ] [soul_19.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_19.png) — 18×20；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Effects/monster_hit/tangseng/`

- [ ] [tangseng_hit_1_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_1_01.png) — 168×107；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hit_1_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_1_02.png) — 168×107；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hit_1_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_1_03.png) — 168×107；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hit_1_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_1_04.png) — 168×107；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hit_1_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_1_05.png) — 168×107；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hit_1_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_1_06.png) — 168×107；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hit_1_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_1_07.png) — 168×107；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hit_1_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_1_08.png) — 168×107；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hit_2_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_2_01.png) — 125×89；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hit_2_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_2_02.png) — 125×89；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hit_2_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_2_03.png) — 125×89；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hit_2_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_2_04.png) — 125×89；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hit_2_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_2_05.png) — 125×89；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hit_2_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_2_06.png) — 125×89；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hit_2_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_2_07.png) — 125×89；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_hit_2_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_2_08.png) — 125×89；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Effects/monster_hit/wukong/`

- [ ] [wukong_hit_1_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_1_01.png) — 168×107；待来源/清晰度处理及1080p验收。
- [ ] [wukong_hit_1_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_1_02.png) — 168×107；待来源/清晰度处理及1080p验收。
- [ ] [wukong_hit_1_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_1_03.png) — 168×107；待来源/清晰度处理及1080p验收。
- [ ] [wukong_hit_1_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_1_04.png) — 168×107；待来源/清晰度处理及1080p验收。
- [ ] [wukong_hit_1_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_1_05.png) — 168×107；待来源/清晰度处理及1080p验收。
- [ ] [wukong_hit_1_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_1_06.png) — 168×107；待来源/清晰度处理及1080p验收。
- [ ] [wukong_hit_1_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_1_07.png) — 168×107；待来源/清晰度处理及1080p验收。
- [ ] [wukong_hit_1_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_1_08.png) — 168×107；待来源/清晰度处理及1080p验收。
- [ ] [wukong_hit_2_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_2_01.png) — 125×89；待来源/清晰度处理及1080p验收。
- [ ] [wukong_hit_2_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_2_02.png) — 125×89；待来源/清晰度处理及1080p验收。
- [ ] [wukong_hit_2_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_2_03.png) — 125×89；待来源/清晰度处理及1080p验收。
- [ ] [wukong_hit_2_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_2_04.png) — 125×89；待来源/清晰度处理及1080p验收。
- [ ] [wukong_hit_2_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_2_05.png) — 125×89；待来源/清晰度处理及1080p验收。
- [ ] [wukong_hit_2_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_2_06.png) — 125×89；待来源/清晰度处理及1080p验收。
- [ ] [wukong_hit_2_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_2_07.png) — 125×89；待来源/清晰度处理及1080p验收。
- [ ] [wukong_hit_2_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_2_08.png) — 125×89；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Effects/tangseng/skills/`

- [ ] [tangseng_cure_role_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_cure_role_atlas.png) — 1024×2090；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_dark_ice_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_dark_ice_atlas.png) — 2048×980；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_ice_dragon_bolt.png](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_ice_dragon_bolt.png) — 129×90；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_water_demon_ball_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_water_demon_ball_atlas.png) — 4096×91；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_water_demon_fall_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_water_demon_fall_atlas.png) — 1024×1972；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Effects/tangseng/`

- [ ] [tangseng_normal_bolt.png](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/tangseng_normal_bolt.png) — 140×58；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Effects/wukong/skills/`

- [ ] [wukong_blazing_flash_trail_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_blazing_flash_trail_atlas.png) — 3528×204；待来源/清晰度处理及1080p验收。
- [ ] [wukong_fire_demon_combo_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_combo_atlas.png) — 2214×1856；待来源/清晰度处理及1080p验收。
- [ ] [wukong_fire_demon_descent_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_descent_atlas.png) — 508×295；待来源/清晰度处理及1080p验收。
- [ ] [wukong_fire_demon_landing_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_landing_atlas.png) — 942×262；待来源/清晰度处理及1080p验收。
- [ ] [wukong_fire_demon_prelude_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_prelude_atlas.png) — 1326×546；待来源/清晰度处理及1080p验收。
- [ ] [wukong_skill_inline_effects.png](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_skill_inline_effects.png) — 3200×4400；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Effects/wukong/`

- [ ] [wukong_attack_effects.png](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/wukong_attack_effects.png) — 2400×1200；待来源/清晰度处理及1080p验收。

<a id="art-images-08"></a>

### UI、头像、地图、技能面板与无双画面（0/225）

目录：`TheGame/Sprites/UI/frontend/`

- [ ] [character_bajie_card.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/character_bajie_card.png) — 357×617；待来源/清晰度处理及1080p验收。
- [ ] [character_sanzang_card.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/character_sanzang_card.png) — 317×617；待来源/清晰度处理及1080p验收。
- [ ] [character_wujing_card.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/character_wujing_card.png) — 351×617；待来源/清晰度处理及1080p验收。
- [ ] [character_wukong_card.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/character_wukong_card.png) — 334×610；待来源/清晰度处理及1080p验收。
- [ ] [delete_confirm_cancel.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/delete_confirm_cancel.png) — 68×33；待来源/清晰度处理及1080p验收。
- [ ] [delete_confirm_cancel_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/delete_confirm_cancel_hover.png) — 68×33；待来源/清晰度处理及1080p验收。
- [ ] [delete_confirm_confirm.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/delete_confirm_confirm.png) — 68×33；待来源/清晰度处理及1080p验收。
- [ ] [delete_confirm_confirm_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/delete_confirm_confirm_hover.png) — 68×33；待来源/清晰度处理及1080p验收。
- [ ] [delete_confirm_panel.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/delete_confirm_panel.png) — 231×133；待来源/清晰度处理及1080p验收。
- [ ] [journey_map.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/journey_map.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [map_bianfuku_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_bianfuku_current.png) — 425×232；待来源/清晰度处理及1080p验收。
- [ ] [map_bianfuku_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_bianfuku_hover.png) — 174×119；待来源/清晰度处理及1080p验收。
- [ ] [map_bianfuku_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_bianfuku_normal.png) — 57×30；待来源/清晰度处理及1080p验收。
- [ ] [map_donghai_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_donghai_current.png) — 3710×1060；待来源/清晰度处理及1080p验收。
- [ ] [map_donghai_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_donghai_hover.png) — 742×265；待来源/清晰度处理及1080p验收。
- [ ] [map_donghai_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_donghai_normal.png) — 742×265；待来源/清晰度处理及1080p验收。
- [ ] [map_huaguoshan_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_huaguoshan_current.png) — 480×424；待来源/清晰度处理及1080p验收。
- [ ] [map_huaguoshan_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_huaguoshan_hover.png) — 184×198；待来源/清晰度处理及1080p验收。
- [ ] [map_huaguoshan_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_huaguoshan_normal.png) — 68×78；待来源/清晰度处理及1080p验收。
- [ ] [map_jialuoluo_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_jialuoluo_current.png) — 765×320；待来源/清晰度处理及1080p验收。
- [ ] [map_jialuoluo_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_jialuoluo_hover.png) — 246×143；待来源/清晰度处理及1080p验收。
- [ ] [map_jialuoluo_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_jialuoluo_normal.png) — 125×52；待来源/清晰度处理及1080p验收。
- [ ] [map_niumogu_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_niumogu_current.png) — 540×432；待来源/清晰度处理及1080p验收。
- [ ] [map_niumogu_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_niumogu_hover.png) — 176×125；待来源/清晰度处理及1080p验收。
- [ ] [map_niumogu_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_niumogu_normal.png) — 57×56；待来源/清晰度处理及1080p验收。
- [ ] [map_qianan_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qianan_current.png) — 1685×1248；待来源/清晰度处理及1080p验收。
- [ ] [map_qianan_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qianan_hover.png) — 337×312；待来源/清晰度处理及1080p验收。
- [ ] [map_qianan_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qianan_normal.png) — 337×312；待来源/清晰度处理及1080p验收。
- [ ] [map_qixingdong_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qixingdong_current.png) — 570×368；待来源/清晰度处理及1080p验收。
- [ ] [map_qixingdong_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qixingdong_hover.png) — 177×132；待来源/清晰度处理及1080p验收。
- [ ] [map_qixingdong_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qixingdong_normal.png) — 86×64；待来源/清晰度处理及1080p验收。
- [ ] [map_return_title.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_return_title.png) — 112×114；待来源/清晰度处理及1080p验收。
- [ ] [map_save_game.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_save_game.png) — 138×93；待来源/清晰度处理及1080p验收。
- [ ] [map_save_game_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_save_game_hover.png) — 138×93；待来源/清晰度处理及1080p验收。
- [ ] [map_shazhiwoliu_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shazhiwoliu_current.png) — 1930×1640；待来源/清晰度处理及1080p验收。
- [ ] [map_shazhiwoliu_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shazhiwoliu_hover.png) — 386×164；待来源/清晰度处理及1080p验收。
- [ ] [map_shazhiwoliu_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shazhiwoliu_normal.png) — 386×164；待来源/清晰度处理及1080p验收。
- [ ] [map_shuiliandong_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shuiliandong_current.png) — 440×344；待来源/清晰度处理及1080p验收。
- [ ] [map_shuiliandong_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shuiliandong_hover.png) — 201×162；待来源/清晰度处理及1080p验收。
- [ ] [map_shuiliandong_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shuiliandong_normal.png) — 60×58；待来源/清晰度处理及1080p验收。
- [ ] [map_skills.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_skills.png) — 1280×1120；待来源/清晰度处理及1080p验收。
- [ ] [map_taohuayuan_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_taohuayuan_current.png) — 460×372；待来源/清晰度处理及1080p验收。
- [ ] [map_taohuayuan_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_taohuayuan_hover.png) — 186×187；待来源/清晰度处理及1080p验收。
- [ ] [map_taohuayuan_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_taohuayuan_normal.png) — 64×65；待来源/清晰度处理及1080p验收。
- [ ] [menu_close.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/menu_close.png) — 41×41；待来源/清晰度处理及1080p验收。
- [ ] [menu_close_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/menu_close_hover.png) — 41×41；待来源/清晰度处理及1080p验收。
- [ ] [menu_separator.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/menu_separator.png) — 241×21；待来源/清晰度处理及1080p验收。
- [ ] [save_slot_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/save_slot_hover.png) — 260×87；待来源/清晰度处理及1080p验收。
- [ ] [save_slot_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/save_slot_normal.png) — 260×87；待来源/清晰度处理及1080p验收。
- [ ] [save_slots_backdrop.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/save_slots_backdrop.png) — 941×591；待来源/清晰度处理及1080p验收。
- [ ] [settings_scroll.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/settings_scroll.png) — 326×341；待来源/清晰度处理及1080p验收。
- [ ] [title_backdrop.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/title_backdrop.png) — 941×591；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/UI/gogo/`

- [ ] [gogo_sheet.png](../Godot/GodotProject/TheGame/Sprites/UI/gogo/gogo_sheet.png) — 1704×828；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/UI/hud/`

- [ ] [battle_menu_ws_fill.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_fill.png) — 221×12；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_00.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_00.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_01.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_01.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_02.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_02.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_03.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_03.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_04.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_04.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_05.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_05.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_06.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_06.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_07.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_07.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_08.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_08.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_09.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_09.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_10.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_10.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_11.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_11.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_12.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_12.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_13.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_13.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_14.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_14.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_15.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_15.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_16.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_16.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_17.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_17.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_glow_18.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_18.png) — 158×158；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_idle.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_idle.png) — 68×68；待来源/清晰度处理及1080p验收。
- [ ] [battle_menu_ws_under.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_under.png) — 221×27；待来源/清晰度处理及1080p验收。
- [ ] [battle_skill_icons.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_skill_icons.png) — 495×45；待来源/清晰度处理及1080p验收。
- [ ] [boss_health_fill.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/boss_health_fill.png) — 340×29；待来源/清晰度处理及1080p验收。
- [ ] [boss_health_under.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/boss_health_under.png) — 340×29；待来源/清晰度处理及1080p验收。
- [ ] [combo_caption.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/combo_caption.png) — 224×80；待来源/清晰度处理及1080p验收。
- [ ] [combo_digits.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/combo_digits.png) — 1600×3200；待来源/清晰度处理及1080p验收。
- [ ] [hud_exp_fill.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_exp_fill.png) — 144×12；待来源/清晰度处理及1080p验收。
- [ ] [hud_head_tangseng.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_head_tangseng.png) — 83×76；待来源/清晰度处理及1080p验收。
- [ ] [hud_head_wukong.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_head_wukong.png) — 83×76；待来源/清晰度处理及1080p验收。
- [ ] [hud_hp_fill.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_hp_fill.png) — 144×12；待来源/清晰度处理及1080p验收。
- [ ] [hud_menu_panel.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_menu_panel.png) — 169×133；待来源/清晰度处理及1080p验收。
- [ ] [hud_mp_fill.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_mp_fill.png) — 144×12；待来源/清晰度处理及1080p验收。
- [ ] [hud_status_label.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_status_label.png) — 17×46；待来源/清晰度处理及1080p验收。
- [ ] [hud_status_panel.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_status_panel.png) — 227×87；待来源/清晰度处理及1080p验收。
- [ ] [hud_ws_frame.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_ws_frame.png) — 260×22；待来源/清晰度处理及1080p验收。
- [ ] [menu_inventory_caption.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_inventory_caption.png) — 30×17；待来源/清晰度处理及1080p验收。
- [ ] [menu_inventory_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_inventory_hover.png) — 47×37；待来源/清晰度处理及1080p验收。
- [ ] [menu_inventory_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_inventory_normal.png) — 47×37；待来源/清晰度处理及1080p验收。
- [ ] [menu_pet_caption.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_pet_caption.png) — 30×17；待来源/清晰度处理及1080p验收。
- [ ] [menu_pet_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_pet_hover.png) — 47×37；待来源/清晰度处理及1080p验收。
- [ ] [menu_pet_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_pet_normal.png) — 47×37；待来源/清晰度处理及1080p验收。
- [ ] [menu_settings_caption.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_settings_caption.png) — 30×17；待来源/清晰度处理及1080p验收。
- [ ] [menu_settings_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_settings_hover.png) — 47×37；待来源/清晰度处理及1080p验收。
- [ ] [menu_settings_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_settings_normal.png) — 47×37；待来源/清晰度处理及1080p验收。
- [ ] [menu_skills_caption.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_skills_caption.png) — 30×17；待来源/清晰度处理及1080p验收。
- [ ] [menu_skills_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_skills_hover.png) — 47×37；待来源/清晰度处理及1080p验收。
- [ ] [menu_skills_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_skills_normal.png) — 47×37；待来源/清晰度处理及1080p验收。
- [ ] [menu_weapon_caption.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_weapon_caption.png) — 30×17；待来源/清晰度处理及1080p验收。
- [ ] [menu_weapon_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_weapon_hover.png) — 47×37；待来源/清晰度处理及1080p验收。
- [ ] [menu_weapon_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_weapon_normal.png) — 47×37；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/UI/hud/musou_activation/tangseng/`

- [ ] [tangseng_musou_activation_01.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_01.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_02.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_02.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_03.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_03.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_04.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_04.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_05.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_05.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_06.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_06.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_07.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_07.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_08.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_08.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_09.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_09.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_10.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_10.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_11.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_11.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_12.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_12.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_13.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_13.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_14.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_14.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_15.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_15.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_16.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_16.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_17.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_17.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_18.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_18.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_19.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_19.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_20.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_20.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_21.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_21.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_22.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_22.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_23.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_23.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [tangseng_musou_activation_24.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_24.png) — 940×590；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/UI/hud/musou_activation/wukong/`

- [ ] [wukong_musou_activation_01.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_01.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_02.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_02.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_03.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_03.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_04.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_04.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_05.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_05.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_06.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_06.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_07.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_07.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_08.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_08.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_09.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_09.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_10.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_10.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_11.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_11.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_12.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_12.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_13.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_13.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_14.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_14.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_15.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_15.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_16.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_16.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_17.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_17.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_18.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_18.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_19.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_19.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_20.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_20.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_21.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_21.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_22.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_22.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_23.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_23.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [wukong_musou_activation_24.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_24.png) — 940×590；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/UI/inventory/`

- [ ] [inventory_action_equip.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_action_equip.png) — 77×31；待来源/清晰度处理及1080p验收。
- [ ] [inventory_action_equip_disabled.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_action_equip_disabled.png) — 77×31；待来源/清晰度处理及1080p验收。
- [ ] [inventory_action_gift.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_action_gift.png) — 77×31；待来源/清晰度处理及1080p验收。
- [ ] [inventory_action_gift_disabled.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_action_gift_disabled.png) — 77×31；待来源/清晰度处理及1080p验收。
- [ ] [inventory_action_sell.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_action_sell.png) — 77×31；待来源/清晰度处理及1080p验收。
- [ ] [inventory_actions_background.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_actions_background.png) — 87×115；待来源/清晰度处理及1080p验收。
- [ ] [inventory_attributes.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_attributes.png) — 325×170；待来源/清晰度处理及1080p验收。
- [ ] [inventory_cell.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_cell.png) — 50×50；待来源/清晰度处理及1080p验收。
- [ ] [inventory_exp_fill.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_exp_fill.png) — 241×18；待来源/清晰度处理及1080p验收。
- [ ] [inventory_exp_frame.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_exp_frame.png) — 213×20；待来源/清晰度处理及1080p验收。
- [ ] [inventory_ink_frame.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_ink_frame.png) — 702×490；待来源/清晰度处理及1080p验收。
- [ ] [inventory_level_badge.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_level_badge.png) — 83×59；待来源/清晰度处理及1080p验收。
- [ ] [inventory_name_plate.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_name_plate.png) — 213×51；待来源/清晰度处理及1080p验收。
- [ ] [inventory_next_page.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_next_page.png) — 87×33；待来源/清晰度处理及1080p验收。
- [ ] [inventory_next_page_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_next_page_hover.png) — 87×33；待来源/清晰度处理及1080p验收。
- [ ] [inventory_panels.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_panels.png) — 941×591；待来源/清晰度处理及1080p验收。
- [ ] [inventory_preview_light.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_preview_light.png) — 960×1152；待来源/清晰度处理及1080p验收。
- [ ] [inventory_previous_page.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_previous_page.png) — 87×33；待来源/清晰度处理及1080p验收。
- [ ] [inventory_previous_page_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_previous_page_hover.png) — 87×33；待来源/清晰度处理及1080p验收。
- [ ] [inventory_slot_accessory.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_slot_accessory.png) — 51×51；待来源/清晰度处理及1080p验收。
- [ ] [inventory_slot_armor.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_slot_armor.png) — 51×51；待来源/清晰度处理及1080p验收。
- [ ] [inventory_slot_costume.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_slot_costume.png) — 50×50；待来源/清晰度处理及1080p验收。
- [ ] [inventory_slot_title.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_slot_title.png) — 50×50；待来源/清晰度处理及1080p验收。
- [ ] [inventory_slot_treasure.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_slot_treasure.png) — 51×51；待来源/清晰度处理及1080p验收。
- [ ] [inventory_slot_weapon.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_slot_weapon.png) — 51×51；待来源/清晰度处理及1080p验收。
- [ ] [inventory_soul_plate.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_soul_plate.png) — 129×28；待来源/清晰度处理及1080p验收。
- [ ] [inventory_tab_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_tab_normal.png) — 71×25；待来源/清晰度处理及1080p验收。
- [ ] [inventory_tab_selected.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_tab_selected.png) — 71×25；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/UI/settlement/`

- [ ] [settlement_backdrop.png](../Godot/GodotProject/TheGame/Sprites/UI/settlement/settlement_backdrop.png) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [settlement_failure.png](../Godot/GodotProject/TheGame/Sprites/UI/settlement/settlement_failure.png) — 510×304；待来源/清晰度处理及1080p验收。
- [ ] [settlement_map_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/settlement/settlement_map_hover.png) — 160×59；待来源/清晰度处理及1080p验收。
- [ ] [settlement_map_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/settlement/settlement_map_normal.png) — 160×59；待来源/清晰度处理及1080p验收。
- [ ] [settlement_results.png](../Godot/GodotProject/TheGame/Sprites/UI/settlement/settlement_results.png) — 402×79；待来源/清晰度处理及1080p验收。
- [ ] [settlement_retry_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/settlement/settlement_retry_hover.png) — 161×60；待来源/清晰度处理及1080p验收。
- [ ] [settlement_retry_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/settlement/settlement_retry_normal.png) — 161×60；待来源/清晰度处理及1080p验收。
- [ ] [settlement_success.png](../Godot/GodotProject/TheGame/Sprites/UI/settlement/settlement_success.png) — 510×304；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/UI/skills/`

- [ ] [skill_blazing_flash_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_blazing_flash_learned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_blazing_flash_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_blazing_flash_unlearned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_dark_ice_thorn_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_dark_ice_thorn_learned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_dark_ice_thorn_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_dark_ice_thorn_unlearned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_fire_demon_slash_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_fire_demon_slash_learned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_fire_demon_slash_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_fire_demon_slash_unlearned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_flame_storm_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_flame_storm_learned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_flame_storm_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_flame_storm_unlearned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_flame_thrust_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_flame_thrust_learned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_flame_thrust_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_flame_thrust_unlearned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_heavenly_dew_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_heavenly_dew_learned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_heavenly_dew_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_heavenly_dew_unlearned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_ice_dragon_wave_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_ice_dragon_wave_learned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_ice_dragon_wave_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_ice_dragon_wave_unlearned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_rising_dragon_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_rising_dragon_learned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_rising_dragon_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_rising_dragon_unlearned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_water_clone_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_water_clone_learned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_water_clone_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_water_clone_unlearned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_water_demon_burst_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_water_demon_burst_learned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skill_water_demon_burst_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_water_demon_burst_unlearned.png) — 65×65；待来源/清晰度处理及1080p验收。
- [ ] [skills_active_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_active_hover.png) — 79×19；待来源/清晰度处理及1080p验收。
- [ ] [skills_active_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_active_normal.png) — 79×19；待来源/清晰度处理及1080p验收。
- [ ] [skills_back_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_back_hover.png) — 54×20；待来源/清晰度处理及1080p验收。
- [ ] [skills_back_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_back_normal.png) — 54×20；待来源/清晰度处理及1080p验收。
- [ ] [skills_background.jpg](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_background.jpg) — 940×590；待来源/清晰度处理及1080p验收。
- [ ] [skills_passive_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_passive_hover.png) — 79×19；待来源/清晰度处理及1080p验收。
- [ ] [skills_passive_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_passive_normal.png) — 79×19；待来源/清晰度处理及1080p验收。
- [ ] [skills_sanzang_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_sanzang_normal.png) — 61×35；待来源/清晰度处理及1080p验收。
- [ ] [skills_sanzang_selected.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_sanzang_selected.png) — 61×35；待来源/清晰度处理及1080p验收。
- [ ] [skills_soul_badge.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_soul_badge.png) — 74×66；待来源/清晰度处理及1080p验收。
- [ ] [skills_table_active.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_table_active.png) — 746×429；待来源/清晰度处理及1080p验收。
- [ ] [skills_table_passive.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_table_passive.png) — 746×429；待来源/清晰度处理及1080p验收。
- [ ] [skills_upgrade_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_upgrade_hover.png) — 48×31；待来源/清晰度处理及1080p验收。
- [ ] [skills_upgrade_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_upgrade_normal.png) — 48×31；待来源/清晰度处理及1080p验收。
- [ ] [skills_upgrade_pressed.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_upgrade_pressed.png) — 48×31；待来源/清晰度处理及1080p验收。
- [ ] [skills_wukong_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_wukong_normal.png) — 60×34；待来源/清晰度处理及1080p验收。
- [ ] [skills_wukong_selected.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_wukong_selected.png) — 60×34；待来源/清晰度处理及1080p验收。

<a id="art-images-09"></a>

### 装备、消耗品与地面图标（0/79）

目录：`TheGame/Sprites/Items/consumables/`

- [ ] [pickup_large_peach.png](../Godot/GodotProject/TheGame/Sprites/Items/consumables/pickup_large_peach.png) — 48×45；待来源/清晰度处理及1080p验收。
- [ ] [pickup_mana_bottle.png](../Godot/GodotProject/TheGame/Sprites/Items/consumables/pickup_mana_bottle.png) — 48×44；待来源/清晰度处理及1080p验收。
- [ ] [pickup_small_peach.png](../Godot/GodotProject/TheGame/Sprites/Items/consumables/pickup_small_peach.png) — 48×45；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Items/equipment/`

- [ ] [equipment_azure_dragon_blade.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_azure_dragon_blade.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_black_tortoise_armor.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_black_tortoise_armor.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_blood_sea_demon_armor.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_blood_sea_demon_armor.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_blood_sea_evil_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_blood_sea_evil_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_blood_sea_shell.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_blood_sea_shell.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_common_cassock.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_common_cassock.png) — 60×62；待来源/清晰度处理及1080p验收。
- [ ] [equipment_common_pilgrim_clothes.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_common_pilgrim_clothes.png) — 60×62；待来源/清晰度处理及1080p验收。
- [ ] [equipment_common_pilgrim_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_common_pilgrim_staff.png) — 60×62；待来源/清晰度处理及1080p验收。
- [ ] [equipment_common_pine_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_common_pine_staff.png) — 60×62；待来源/清晰度处理及1080p验收。
- [ ] [equipment_diamond_snare.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_diamond_snare.png) — 52×55；待来源/清晰度处理及1080p验收。
- [ ] [equipment_earth_fiend_ape_armor.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_earth_fiend_ape_armor.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_earth_fiend_ring.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_earth_fiend_ring.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_earth_fiend_scepter.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_earth_fiend_scepter.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_east_sea_banner.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_east_sea_banner.png) — 52×55；待来源/清晰度处理及1080p验收。
- [ ] [equipment_excellent_cassock.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_excellent_cassock.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_excellent_pilgrim_clothes.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_excellent_pilgrim_clothes.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_excellent_pilgrim_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_excellent_pilgrim_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_excellent_pine_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_excellent_pine_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_fine_cassock.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_fine_cassock.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_fine_pilgrim_clothes.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_fine_pilgrim_clothes.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_fine_pilgrim_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_fine_pilgrim_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_fine_pine_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_fine_pine_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_heavenly_fiend_moon_halberd.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_heavenly_fiend_moon_halberd.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_heavenly_fiend_necklace.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_heavenly_fiend_necklace.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_heavenly_fiend_robe.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_heavenly_fiend_robe.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_heirloom_bracelet.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_heirloom_bracelet.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_heirloom_clothes.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_heirloom_clothes.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_heirloom_sword.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_heirloom_sword.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_kirin_robe.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_kirin_robe.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_nine_ring_monk_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_nine_ring_monk_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_rough_cassock.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_rough_cassock.png) — 60×62；待来源/清晰度处理及1080p验收。
- [ ] [equipment_rough_pilgrim_clothes.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_rough_pilgrim_clothes.png) — 60×62；待来源/清晰度处理及1080p验收。
- [ ] [equipment_rough_pilgrim_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_rough_pilgrim_staff.png) — 60×62；待来源/清晰度处理及1080p验收。
- [ ] [equipment_rough_pine_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_rough_pine_staff.png) — 60×62；待来源/清晰度处理及1080p验收。
- [ ] [equipment_ruyi_golden_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_ruyi_golden_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_seven_star_guard.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_seven_star_guard.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_vermilion_bird_ring.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_vermilion_bird_ring.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [equipment_white_tiger_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/equipment_white_tiger_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_azure_dragon_blade.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_azure_dragon_blade.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_black_tortoise_armor.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_black_tortoise_armor.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_blood_sea_demon_armor.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_blood_sea_demon_armor.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_blood_sea_evil_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_blood_sea_evil_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_blood_sea_shell.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_blood_sea_shell.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_common_cassock.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_common_cassock.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_common_pilgrim_clothes.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_common_pilgrim_clothes.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_common_pilgrim_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_common_pilgrim_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_common_pine_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_common_pine_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_diamond_snare.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_diamond_snare.png) — 52×55；待来源/清晰度处理及1080p验收。
- [ ] [ground_earth_fiend_ape_armor.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_earth_fiend_ape_armor.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_earth_fiend_ring.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_earth_fiend_ring.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_earth_fiend_scepter.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_earth_fiend_scepter.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_east_sea_banner.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_east_sea_banner.png) — 52×55；待来源/清晰度处理及1080p验收。
- [ ] [ground_excellent_cassock.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_excellent_cassock.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_excellent_pilgrim_clothes.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_excellent_pilgrim_clothes.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_excellent_pilgrim_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_excellent_pilgrim_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_excellent_pine_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_excellent_pine_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_fine_cassock.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_fine_cassock.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_fine_pilgrim_clothes.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_fine_pilgrim_clothes.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_fine_pilgrim_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_fine_pilgrim_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_fine_pine_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_fine_pine_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_heavenly_fiend_moon_halberd.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_heavenly_fiend_moon_halberd.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_heavenly_fiend_necklace.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_heavenly_fiend_necklace.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_heavenly_fiend_robe.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_heavenly_fiend_robe.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_heirloom_bracelet.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_heirloom_bracelet.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_heirloom_clothes.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_heirloom_clothes.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_heirloom_sword.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_heirloom_sword.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_kirin_robe.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_kirin_robe.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_nine_ring_monk_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_nine_ring_monk_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_rough_cassock.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_rough_cassock.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_rough_pilgrim_clothes.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_rough_pilgrim_clothes.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_rough_pilgrim_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_rough_pilgrim_staff.png) — 52×54；保真方法未通过：原生52×54位图，超分颜色偏移；暂保留原图。
- [ ] [ground_rough_pine_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_rough_pine_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_ruyi_golden_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_ruyi_golden_staff.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_seven_star_guard.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_seven_star_guard.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_vermilion_bird_ring.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_vermilion_bird_ring.png) — 52×54；待来源/清晰度处理及1080p验收。
- [ ] [ground_white_tiger_staff.png](../Godot/GodotProject/TheGame/Sprites/Items/equipment/ground_white_tiger_staff.png) — 52×54；待来源/清晰度处理及1080p验收。

<a id="art-images-10"></a>

### 伤害、恢复数字与文字图集（0/10）

目录：`TheGame/Sprites/Number/`

- [ ] [damage_number_hero_magic.png](../Godot/GodotProject/TheGame/Sprites/Number/damage_number_hero_magic.png) — 300×30；待来源/清晰度处理及1080p验收。
- [ ] [damage_number_hero_physics.png](../Godot/GodotProject/TheGame/Sprites/Number/damage_number_hero_physics.png) — 300×30；待来源/清晰度处理及1080p验收。
- [ ] [damage_number_miss.png](../Godot/GodotProject/TheGame/Sprites/Number/damage_number_miss.png) — 55×24；待来源/清晰度处理及1080p验收。
- [ ] [damage_number_monster_magic.png](../Godot/GodotProject/TheGame/Sprites/Number/damage_number_monster_magic.png) — 300×30；待来源/清晰度处理及1080p验收。
- [ ] [damage_number_monster_magic_crit.png](../Godot/GodotProject/TheGame/Sprites/Number/damage_number_monster_magic_crit.png) — 320×32；待来源/清晰度处理及1080p验收。
- [ ] [damage_number_monster_physics.png](../Godot/GodotProject/TheGame/Sprites/Number/damage_number_monster_physics.png) — 300×30；待来源/清晰度处理及1080p验收。
- [ ] [damage_number_monster_physics_crit.png](../Godot/GodotProject/TheGame/Sprites/Number/damage_number_monster_physics_crit.png) — 420×42；待来源/清晰度处理及1080p验收。
- [ ] [damage_number_real.png](../Godot/GodotProject/TheGame/Sprites/Number/damage_number_real.png) — 300×30；待来源/清晰度处理及1080p验收。
- [ ] [recovery_number_health.png](../Godot/GodotProject/TheGame/Sprites/Number/recovery_number_health.png) — 300×30；待来源/清晰度处理及1080p验收。
- [ ] [recovery_number_mana.png](../Godot/GodotProject/TheGame/Sprites/Number/recovery_number_mana.png) — 330×34；待来源/清晰度处理及1080p验收。

<a id="art-images-11"></a>

### 关卡背景、地形、平台与前景（0/37）

目录：`TheGame/Sprites/Levels/zm1_01_huaguoshan/`

- [ ] [huaguoshan_ground.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_01_huaguoshan/huaguoshan_ground.png) — 4812×170；待来源/清晰度处理及1080p验收。
- [ ] [huaguoshan_platform.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_01_huaguoshan/huaguoshan_platform.png) — 263×75；来源受限：shape56实为263×75位图填充；2倍采样不算高清，暂保留原图。
- [ ] [level_1_bg_end.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_01_huaguoshan/level_1_bg_end.png) — 1440×690；待来源/清晰度处理及1080p验收。
- [ ] [level_1_floor.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_01_huaguoshan/level_1_floor.png) — 4700×170；待来源/清晰度处理及1080p验收。
- [ ] [level_1_front.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_01_huaguoshan/level_1_front.png) — 4957×633；待来源/清晰度处理及1080p验收。
- [ ] [level_1_platform.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_01_huaguoshan/level_1_platform.png) — 264×76；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Levels/zm1_02_shuiliandong/`

- [ ] [shuiliandong_front.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_02_shuiliandong/shuiliandong_front.png) — 5112×542；待来源/清晰度处理及1080p验收。
- [ ] [shuiliandong_ground.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_02_shuiliandong/shuiliandong_ground.png) — 4724×314；待来源/清晰度处理及1080p验收。
- [ ] [shuiliandong_platform.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_02_shuiliandong/shuiliandong_platform.png) — 179×51；待来源/清晰度处理及1080p验收。
- [ ] [shuiliandong_water.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_02_shuiliandong/shuiliandong_water.png) — 549×83；待来源/清晰度处理及1080p验收。
- [ ] [shuiliandong_waves.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_02_shuiliandong/shuiliandong_waves.png) — 1600×50；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Levels/zm1_03_taohuayuan/`

- [ ] [taohuayuan_front.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_03_taohuayuan/taohuayuan_front.png) — 4965×489；待来源/清晰度处理及1080p验收。
- [ ] [taohuayuan_ground.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_03_taohuayuan/taohuayuan_ground.png) — 4724×148；待来源/清晰度处理及1080p验收。
- [ ] [taohuayuan_rock.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_03_taohuayuan/taohuayuan_rock.png) — 189×155；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Levels/zm1_04_bat_cave/`

- [ ] [bat_cave_front.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_04_bat_cave/bat_cave_front.png) — 4944×632；待来源/清晰度处理及1080p验收。
- [ ] [bat_cave_ground.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_04_bat_cave/bat_cave_ground.png) — 4700×308；待来源/清晰度处理及1080p验收。
- [ ] [bat_cave_sky.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_04_bat_cave/bat_cave_sky.png) — 1440×690；保真方法未通过：原生位图，超分在1080p无明确收益；暂保留原图。
- [ ] [bat_cave_spike_atlas.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_04_bat_cave/bat_cave_spike_atlas.png) — 190×256；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Levels/zm1_05_qixing_cave/`

- [ ] [qixing_cave_front.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_05_qixing_cave/qixing_cave_front.png) — 5025×634；待来源/清晰度处理及1080p验收。
- [ ] [qixing_cave_sky.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_05_qixing_cave/qixing_cave_sky.png) — 1440×690；待来源/清晰度处理及1080p验收。
- [ ] [qixing_ground.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_05_qixing_cave/qixing_ground.png) — 4700×407；待来源/清晰度处理及1080p验收。
- [ ] [qixing_lower_platform.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_05_qixing_cave/qixing_lower_platform.png) — 421×139；待来源/清晰度处理及1080p验收。
- [ ] [qixing_upper_platform.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_05_qixing_cave/qixing_upper_platform.png) — 412×451；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Levels/zm1_06_garuda_cave/`

- [ ] [garuda_cave_front.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_06_garuda_cave/garuda_cave_front.png) — 4942×651；待来源/清晰度处理及1080p验收。
- [ ] [garuda_cave_ground.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_06_garuda_cave/garuda_cave_ground.png) — 4700×474；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Levels/zm1_07_shore/`

- [ ] [sea_shore_front.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_07_shore/sea_shore_front.png) — 5023×509；待来源/清晰度处理及1080p验收。
- [ ] [sea_shore_ground.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_07_shore/sea_shore_ground.png) — 4700×402；待来源/清晰度处理及1080p验收。
- [ ] [sea_shore_sky.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_07_shore/sea_shore_sky.png) — 1440×690；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Levels/zm1_08_east_sea/`

- [ ] [east_sea_front.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_08_east_sea/east_sea_front.png) — 4959×557；待来源/清晰度处理及1080p验收。
- [ ] [east_sea_ground.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_08_east_sea/east_sea_ground.png) — 4700×391；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Levels/zm1_09_shark_whirlpool/`

- [ ] [shark_whirlpool_front.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_09_shark_whirlpool/shark_whirlpool_front.png) — 4964×509；待来源/清晰度处理及1080p验收。
- [ ] [shark_whirlpool_ground.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_09_shark_whirlpool/shark_whirlpool_ground.png) — 4700×95；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Levels/zm1_10_bull_demon_valley/`

- [ ] [bull_demon_valley_front.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_10_bull_demon_valley/bull_demon_valley_front.png) — 1880×590；待来源/清晰度处理及1080p验收。
- [ ] [bull_demon_valley_ground.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_10_bull_demon_valley/bull_demon_valley_ground.png) — 1896×95；待来源/清晰度处理及1080p验收。
- [ ] [bull_demon_valley_sky.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_10_bull_demon_valley/bull_demon_valley_sky.png) — 1440×690；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Levels/zm1_11_hidden_dragon_palace/`

- [ ] [dragon_palace_front.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_11_hidden_dragon_palace/dragon_palace_front.png) — 944×504；待来源/清晰度处理及1080p验收。
- [ ] [dragon_palace_ground.png](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_11_hidden_dragon_palace/dragon_palace_ground.png) — 1300×97；待来源/清晰度处理及1080p验收。

<a id="art-images-12"></a>

### 主题SVG、工程图标与其他图像（0/5）

目录：`TheGame/MainPack/Themes/Styles/`

- [ ] [settings_slider_grabber.svg](../Godot/GodotProject/TheGame/MainPack/Themes/Styles/settings_slider_grabber.svg) — SVG矢量；原生矢量，待1080p显示核对。
- [ ] [settings_slider_grabber_active.svg](../Godot/GodotProject/TheGame/MainPack/Themes/Styles/settings_slider_grabber_active.svg) — SVG矢量；原生矢量，待1080p显示核对。

目录：`TheGame/Sprites/`

- [ ] [bhpic.png](../Godot/GodotProject/TheGame/Sprites/bhpic.png) — 512×512；待来源/清晰度处理及1080p验收。

目录：`addons/asset_bundle/images/`

- [ ] [asset_bundle.svg](../Godot/GodotProject/addons/asset_bundle/images/asset_bundle.svg) — SVG矢量；只读插件图标，登记在册，待显示核对，不擅改插件。

目录：`./`

- [ ] [icon.svg](../Godot/GodotProject/icon.svg) — SVG矢量；工程图标，待目标输出显示核对。

<!-- art-images:end -->

## 配套美术资源（182项，不计入图像661项）

这部分列全部业务字体、着色器、独立美术`.tres`文件及地图BitMap遮罩；嵌入其中的子资源不重复记为文件。帧动画的源PNG仍在上表，不能因为动画库已核对就把它引用的所有图片一并勾掉。音频、业务数值Resource、bundle、C#和场景消费者不计为本表美术源；它们必要的接入任务已在上方及Validation记录。

| 分类 | 总数 | 本批已联动/核对 | 待处理 |
| --- | ---: | ---: | ---: |
| [字体](#art-support-01) | 8 | 0 | 8 |
| [着色器](#art-support-02) | 4 | 1 | 3 |
| [动画库](#art-support-03) | 71 | 6 | 65 |
| [逐帧动画资源](#art-support-04) | 78 | 0 | 78 |
| [外观与物品美术登记](#art-support-05) | 5 | 4 | 1 |
| [主题与样式](#art-support-06) | 13 | 0 | 13 |
| [地图透明命中遮罩](#art-support-07) | 3 | 0 | 3 |
| **合计** | **182** | **11** | **171** |

<!-- art-support:start -->

<a id="art-support-01"></a>

### 字体（0/8）

目录：`TheGame/MainPack/Fonts/`

- [ ] [aa_shan_yue.ttf](../Godot/GodotProject/TheGame/MainPack/Fonts/aa_shan_yue.ttf) — TTF；待1080p文字大小、描边/字形及实际显示核对；沿用原生字体。
- [ ] [aa_wen_zheng_ming.ttf](../Godot/GodotProject/TheGame/MainPack/Fonts/aa_wen_zheng_ming.ttf) — TTF；待1080p文字大小、描边/字形及实际显示核对；沿用原生字体。
- [ ] [df_poster_w12.ttc](../Godot/GodotProject/TheGame/MainPack/Fonts/df_poster_w12.ttc) — TTC；待1080p文字大小、描边/字形及实际显示核对；沿用原生字体。
- [ ] [df_song_w3.ttc](../Godot/GodotProject/TheGame/MainPack/Fonts/df_song_w3.ttc) — TTC；待1080p文字大小、描边/字形及实际显示核对；沿用原生字体。
- [ ] [dfp_hai_bao_w12.ttc](../Godot/GodotProject/TheGame/MainPack/Fonts/dfp_hai_bao_w12.ttc) — TTC；待1080p文字大小、描边/字形及实际显示核对；沿用原生字体。
- [ ] [fz_cu_yuan.ttf](../Godot/GodotProject/TheGame/MainPack/Fonts/fz_cu_yuan.ttf) — TTF；待1080p文字大小、描边/字形及实际显示核对；沿用原生字体。
- [ ] [fz_cu_yuan_hud.ttf](../Godot/GodotProject/TheGame/MainPack/Fonts/fz_cu_yuan_hud.ttf) — TTF；待1080p文字大小、描边/字形及实际显示核对；沿用原生字体。
- [ ] [simhei.ttf](../Godot/GodotProject/TheGame/MainPack/Fonts/simhei.ttf) — TTF；待1080p文字大小、描边/字形及实际显示核对；沿用原生字体。

<a id="art-support-02"></a>

### 着色器（1/4）

目录：`TheGame/Sprites/Characters/Heroes/Status/`

- [x] [hero_invulnerability.gdshader](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/Status/hero_invulnerability.gdshader) — CanvasItem Shader；已按像素格/密度保持原光晕范围，60/120Hz验证通过。

目录：`TheGame/Sprites/UI/frontend/`

- [ ] [character_card.gdshader](../Godot/GodotProject/TheGame/Sprites/UI/frontend/character_card.gdshader) — CanvasItem Shader；待相关高清素材的像素采样与逻辑范围联动。
- [ ] [map_location_outline.gdshader](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_location_outline.gdshader) — CanvasItem Shader；待相关高清素材的像素采样与逻辑范围联动。

目录：`TheGame/Sprites/UI/inventory/`

- [ ] [equipment_slot_background.gdshader](../Godot/GodotProject/TheGame/Sprites/UI/inventory/equipment_slot_background.gdshader) — CanvasItem Shader；待相关高清素材的像素采样与逻辑范围联动。

<a id="art-support-03"></a>

### 动画库（6/71）

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_02_macaque_king/`

- [ ] [macaque_king_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_02_macaque_king/macaque_king_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [macaque_wind_blade_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_02_macaque_king/macaque_wind_blade_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_03_tamarin_king/`

- [ ] [tamarin_king_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_03_tamarin_king/tamarin_king_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_04_bat_cave/`

- [ ] [bat_cave_statue_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_04_bat_cave/bat_cave_statue_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_05_qixing_cave/`

- [ ] [lion_camel_king_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_king_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [lion_camel_poison_cloud_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_poison_cloud_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_06_garuda_cave/`

- [ ] [boss_death_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_06_garuda_cave/boss_death_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [peng_demon_king_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_06_garuda_cave/peng_demon_king_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [peng_fire_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_06_garuda_cave/peng_fire_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_08_east_sea/`

- [ ] [jiao_demon_king_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_08_east_sea/jiao_demon_king_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [jiao_dragon_wave_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_08_east_sea/jiao_dragon_wave_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_09_shark_whirlpool/`

- [ ] [shark_demon_king_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_09_shark_whirlpool/shark_demon_king_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [shark_king_bubble_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_bubble_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [shark_king_bubble_warning_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_bubble_warning_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [shark_king_shark_wave_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_shark_wave_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_10_bull_demon_valley/`

- [ ] [bull_demon_beast_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_beast_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [bull_demon_heart_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_heart_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [bull_demon_king_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_king_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [bull_fire_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_10_bull_demon_valley/bull_fire_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [bull_heart_fade_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_10_bull_demon_valley/bull_heart_fade_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [bull_presentation_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_10_bull_demon_valley/bull_presentation_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_11_hidden_dragon_palace/`

- [ ] [dragon_king_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [dragon_king_torrent_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_torrent_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [dragon_king_wave_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_wave_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Ground/bull_guard/`

- [ ] [bull_guard_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Ground/bull_guard/bull_guard_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Ground/cave_brute/`

- [ ] [cave_brute_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Ground/cave_brute/cave_brute_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Ground/demon_monkey/`

- [ ] [demon_monkey_anim_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Ground/demon_monkey/demon_monkey_anim_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Ground/gorilla/`

- [ ] [gorilla_anim_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Ground/gorilla/gorilla_anim_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Ground/huaguoshan_monkey/`

- [ ] [huaguoshan_monkey_anim_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_anim_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Ground/knife_demon/`

- [ ] [knife_demon_blade_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Ground/knife_demon/knife_demon_blade_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [knife_demon_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Ground/knife_demon/knife_demon_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Sea/lantern_fish/`

- [ ] [lantern_fish_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sea/lantern_fish/lantern_fish_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Sea/shark_spawn/`

- [ ] [shark_spawn_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sea/shark_spawn/shark_spawn_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [shark_spawn_slash_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sea/shark_spawn/shark_spawn_slash_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [shark_spawn_tornado_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sea/shark_spawn/shark_spawn_tornado_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [shark_spawn_warning_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sea/shark_spawn/shark_spawn_warning_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Sea/shrimp_demon/`

- [ ] [shrimp_demon_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sea/shrimp_demon/shrimp_demon_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Sea/tide_demon/`

- [ ] [tide_demon_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sea/tide_demon/tide_demon_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Sea/turtle_minister/`

- [ ] [turtle_minister_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sea/turtle_minister/turtle_minister_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Sky/bat/`

- [ ] [bat_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sky/bat/bat_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Sky/flying_demon/`

- [ ] [flying_demon_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sky/flying_demon/flying_demon_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Heroes/Tangseng/`

- [x] [tangseng_anim_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Tangseng/tangseng_anim_library.tres) — AnimationLibrary；已核对全部时序/引用并验证高清显示；库文件原字节保持。
- [ ] [tangseng_cure_role_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Tangseng/tangseng_cure_role_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [tangseng_dark_ice_effect_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Tangseng/tangseng_dark_ice_effect_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [x] [tangseng_preview_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Tangseng/tangseng_preview_library.tres) — AnimationLibrary；已核对全部时序/引用并验证高清显示；库文件原字节保持。
- [x] [tangseng_skill_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Tangseng/tangseng_skill_library.tres) — AnimationLibrary；已核对全部时序/引用并验证高清显示；库文件原字节保持。
- [ ] [tangseng_water_clone_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Tangseng/tangseng_water_clone_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [tangseng_water_demon_fall_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Tangseng/tangseng_water_demon_fall_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Heroes/Wukong/`

- [x] [wukong_anim_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Wukong/wukong_anim_library.tres) — AnimationLibrary；已同步等价格索引；关键帧数量、时序、事件与判定保持。
- [ ] [wukong_blazing_flash_trail_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Wukong/wukong_blazing_flash_trail_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [wukong_fire_demon_combo_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Wukong/wukong_fire_demon_combo_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [wukong_fire_demon_descent_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Wukong/wukong_fire_demon_descent_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [wukong_fire_demon_landing_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Wukong/wukong_fire_demon_landing_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [x] [wukong_preview_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Wukong/wukong_preview_library.tres) — AnimationLibrary；已核对全部时序/引用并验证高清显示；库文件原字节保持。
- [x] [wukong_skill_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Wukong/wukong_skill_library.tres) — AnimationLibrary；已同步等价格索引；关键帧数量、时序、事件与判定保持。

目录：`TheGame/Entitys/Animations/Heroes/`

- [ ] [hero_tombstone_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/hero_tombstone_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/`

- [ ] [ground_item_pickup_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/ground_item_pickup_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [shuiliandong_river_animation.tres](../Godot/GodotProject/TheGame/Entitys/Animations/shuiliandong_river_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Scenes/Levels/zm1_02_shuiliandong/`

- [ ] [hidden_entrance_fade.tres](../Godot/GodotProject/TheGame/Scenes/Levels/zm1_02_shuiliandong/hidden_entrance_fade.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Sprites/UI/frontend/`

- [ ] [map_bianfuku_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_bianfuku_hover_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [map_donghai_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_donghai_hover_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [map_jialuoluo_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_jialuoluo_hover_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [map_location_hover.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_location_hover.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [map_niumogu_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_niumogu_hover_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [map_qianan_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qianan_hover_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [map_qixingdong_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qixingdong_hover_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [map_shazhiwoliu_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shazhiwoliu_hover_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [map_shuiliandong_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shuiliandong_hover_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [map_taohuayuan_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_taohuayuan_hover_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [save_feedback.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/save_feedback.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Sprites/UI/hud/musou_activation/shared/`

- [ ] [musou_activation_library.tres](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/shared/musou_activation_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

<a id="art-support-04"></a>

### 逐帧动画资源（0/78）

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/`

- [ ] [macaque_king_body_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/macaque_king_body_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [macaque_king_effect_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/macaque_king_effect_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [macaque_wind_blade_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/macaque_wind_blade_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_03_tamarin_king/`

- [ ] [tamarin_king_body_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_03_tamarin_king/tamarin_king_body_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [tamarin_king_effect_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_03_tamarin_king/tamarin_king_effect_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_04_bat_cave/`

- [ ] [bat_cave_statue_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_04_bat_cave/bat_cave_statue_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_05_qixing_cave/`

- [ ] [lion_camel_king_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_king_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [lion_camel_poison_cloud_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_poison_cloud_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_06_garuda_cave/`

- [ ] [peng_demon_king_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_06_garuda_cave/peng_demon_king_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/`

- [ ] [jiao_back_hit_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/jiao_back_hit_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [jiao_demon_king_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/jiao_demon_king_official_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [jiao_dragon_wave_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/jiao_dragon_wave_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/`

- [ ] [shark_demon_king_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_demon_king_official_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [shark_king_bubble_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_bubble_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [shark_king_bubble_warning_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_bubble_warning_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [shark_king_shark_wave_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_shark_wave_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/`

- [ ] [bull_background_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_background_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [bull_demon_beast_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_beast_official_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [bull_demon_heart_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_heart_official_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [bull_demon_king_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_king_official_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [bull_demon_king_tangseng_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_king_tangseng_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [bull_fire_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_fire_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [bull_hands_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_hands_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [bull_heart_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_heart_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/`

- [ ] [dragon_king_ice_aura_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_ice_aura_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [dragon_king_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_official_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [dragon_king_torrent_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_torrent_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [dragon_king_wave_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_wave_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/bull_guard/`

- [ ] [bull_guard_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/bull_guard/bull_guard_official_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/cave_brute/`

- [ ] [cave_brute_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/cave_brute/cave_brute_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/`

- [ ] [demon_monkey_animations.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_animations.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [demon_monkey_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_official_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/`

- [ ] [gorilla_animations.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_animations.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [gorilla_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_official_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/`

- [ ] [huaguoshan_monkey_animations.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_animations.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [huaguoshan_monkey_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_official_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/knife_demon/`

- [ ] [knife_demon_blade_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/knife_demon/knife_demon_blade_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [knife_demon_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/knife_demon/knife_demon_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/lantern_fish/`

- [ ] [lantern_fish_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/lantern_fish/lantern_fish_official_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/`

- [ ] [shark_spawn_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/shark_spawn_official_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [shark_spawn_slash_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/shark_spawn_slash_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [shark_spawn_tornado_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/shark_spawn_tornado_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [shark_spawn_warning_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/shark_spawn_warning_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/shrimp_demon/`

- [ ] [shrimp_demon_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shrimp_demon/shrimp_demon_official_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/tide_demon/`

- [ ] [tide_demon_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/tide_demon/tide_demon_official_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/turtle_minister/`

- [ ] [turtle_minister_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/turtle_minister/turtle_minister_official_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sky/bat/`

- [ ] [bat_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sky/bat/bat_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sky/flying_demon/`

- [ ] [flying_demon_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sky/flying_demon/flying_demon_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Heroes/Status/`

- [ ] [hero_frozen_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/Status/hero_frozen_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [poison_head_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/Status/poison_head_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [poison_up_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/Status/poison_up_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Effects/Enemies/zm1_06_garuda_cave/`

- [ ] [boss_death_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/Enemies/zm1_06_garuda_cave/boss_death_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [peng_fire_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/Enemies/zm1_06_garuda_cave/peng_fire_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Effects/hero_hurt/tangseng/`

- [ ] [tangseng_hurt_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Effects/hero_hurt/wukong/`

- [ ] [hero_hurt_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Effects/level_exit/`

- [ ] [level_exit_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Effects/level_up/`

- [ ] [level_up_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Effects/loot/`

- [ ] [loot_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/loot/loot_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Effects/monster_hit/tangseng/`

- [ ] [tangseng_monster_hit_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_monster_hit_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Effects/monster_hit/wukong/`

- [ ] [wukong_monster_hit_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_monster_hit_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Effects/tangseng/skills/`

- [ ] [tangseng_cure_role_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_cure_role_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [tangseng_dark_ice_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_dark_ice_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [tangseng_water_demon_ball_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_water_demon_ball_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [tangseng_water_demon_fall_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_water_demon_fall_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Effects/wukong/skills/`

- [ ] [wukong_blazing_flash_trail_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_blazing_flash_trail_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [wukong_fire_demon_combo_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_combo_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [wukong_fire_demon_descent_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_descent_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [wukong_fire_demon_landing_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_landing_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [wukong_fire_demon_prelude_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_prelude_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Effects/wukong/`

- [ ] [wukong_effect_animations.tres](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/wukong_effect_animations.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Levels/zm1_04_bat_cave/`

- [ ] [bat_cave_spike_frames.tres](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_04_bat_cave/bat_cave_spike_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/UI/frontend/`

- [ ] [map_skills_frames.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_skills_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/UI/gogo/`

- [ ] [gogo_animations.tres](../Godot/GodotProject/TheGame/Sprites/UI/gogo/gogo_animations.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/UI/hud/`

- [ ] [battle_menu_ws_glow_frames.tres](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [battle_skill_icon_frames.tres](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_skill_icon_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [combo_digits_frames.tres](../Godot/GodotProject/TheGame/Sprites/UI/hud/combo_digits_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/UI/hud/musou_activation/tangseng/`

- [ ] [tangseng_musou_activation_frames.tres](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/UI/hud/musou_activation/wukong/`

- [ ] [wukong_musou_activation_frames.tres](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

<a id="art-support-05"></a>

### 外观与物品美术登记（4/5）

目录：`TheGame/Sprites/Characters/Heroes/tangseng/`

- [x] [tangseng_appearance.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_appearance.tres) — HeroAppearanceResource；密度2已接入；原路径、UID及装备身份保持。
- [x] [tangseng_inventory_appearance.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_inventory_appearance.tres) — HeroAppearanceResource；密度2已接入；原路径、UID及装备身份保持。

目录：`TheGame/Sprites/Characters/Heroes/wukong/`

- [x] [wukong_appearance.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_appearance.tres) — HeroAppearanceResource；密度2已接入；原路径、UID及装备身份保持。
- [x] [wukong_inventory_appearance.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_inventory_appearance.tres) — HeroAppearanceResource；密度2已接入；原路径、UID及装备身份保持。

目录：`TheGame/Sprites/Items/`

- [ ] [item_appearances.tres](../Godot/GodotProject/TheGame/Sprites/Items/item_appearances.tres) — ItemAppearancesResource；待全部相关图像身份/尺寸/引用与实际显示检查。

<a id="art-support-06"></a>

### 主题与样式（0/13）

目录：`TheGame/MainPack/Fonts/`

- [ ] [game_font.tres](../Godot/GodotProject/TheGame/MainPack/Fonts/game_font.tres) — FontVariation；待1080p原生布局/字体/纹理采样检查。

目录：`TheGame/MainPack/Themes/`

- [ ] [MainThemes.tres](../Godot/GodotProject/TheGame/MainPack/Themes/MainThemes.tres) — Theme；待1080p原生布局/字体/纹理采样检查。
- [ ] [brown_gold_theme.tres](../Godot/GodotProject/TheGame/MainPack/Themes/brown_gold_theme.tres) — Theme；待1080p原生布局/字体/纹理采样检查。
- [ ] [legacy_menu_theme.tres](../Godot/GodotProject/TheGame/MainPack/Themes/legacy_menu_theme.tres) — Theme；待1080p原生布局/字体/纹理采样检查。
- [ ] [settings_theme.tres](../Godot/GodotProject/TheGame/MainPack/Themes/settings_theme.tres) — Theme；待1080p原生布局/字体/纹理采样检查。

目录：`TheGame/MainPack/Themes/Styles/`

- [ ] [brown_dialog_panel.tres](../Godot/GodotProject/TheGame/MainPack/Themes/Styles/brown_dialog_panel.tres) — StyleBoxFlat；待1080p原生布局/字体/纹理采样检查。
- [ ] [brown_gold_button.tres](../Godot/GodotProject/TheGame/MainPack/Themes/Styles/brown_gold_button.tres) — StyleBoxTexture；待1080p原生布局/字体/纹理采样检查。
- [ ] [brown_gold_button_active.tres](../Godot/GodotProject/TheGame/MainPack/Themes/Styles/brown_gold_button_active.tres) — StyleBoxFlat；待1080p原生布局/字体/纹理采样检查。
- [ ] [brown_guide_panel.tres](../Godot/GodotProject/TheGame/MainPack/Themes/Styles/brown_guide_panel.tres) — StyleBoxFlat；待1080p原生布局/字体/纹理采样检查。
- [ ] [brown_popup_panel.tres](../Godot/GodotProject/TheGame/MainPack/Themes/Styles/brown_popup_panel.tres) — StyleBoxFlat；待1080p原生布局/字体/纹理采样检查。
- [ ] [gold_focus_border.tres](../Godot/GodotProject/TheGame/MainPack/Themes/Styles/gold_focus_border.tres) — StyleBoxFlat；待1080p原生布局/字体/纹理采样检查。
- [ ] [settings_slider_fill.tres](../Godot/GodotProject/TheGame/MainPack/Themes/Styles/settings_slider_fill.tres) — StyleBoxFlat；待1080p原生布局/字体/纹理采样检查。
- [ ] [settings_slider_track.tres](../Godot/GodotProject/TheGame/MainPack/Themes/Styles/settings_slider_track.tres) — StyleBoxFlat；待1080p原生布局/字体/纹理采样检查。

<a id="art-support-07"></a>

### 地图透明命中遮罩（0/3）

目录：`TheGame/Sprites/UI/frontend/`

- [ ] [map_donghai_hit_mask.res](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_donghai_hit_mask.res) — BitMap；待地图地点高清后按原逻辑轮廓核对透明命中，不能随像素增密扩大点击范围。
- [ ] [map_qianan_hit_mask.res](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qianan_hit_mask.res) — BitMap；待地图地点高清后按原逻辑轮廓核对透明命中，不能随像素增密扩大点击范围。
- [ ] [map_shazhiwoliu_hit_mask.res](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shazhiwoliu_hit_mask.res) — BitMap；待地图地点高清后按原逻辑轮廓核对透明命中，不能随像素增密扩大点击范围。

<!-- art-support:end -->

## 当前未完成项与限制

- 当前只完成两英雄身体/武器与专用背包这52张图；英雄状态、UI、独立特效、敌人/Boss、图标、数字及地形仍按上表逐项推进。
- 三项位图限制仍未勾选：花果山平台缺同内容高精度源，蝙蝠天空恢复收益不明确，粗糙行者棍恢复颜色不保真。原图继续使用，不伪报高清完成。
- 英雄/背包批次已完成本地必要验证，用户审图仍待；Android压缩、目标GPU纹理/显存、加载/FPS及真机触控未验。
- 当前保留keep，默认窗口1920×1080；正式横向扩展视野、外围覆盖和16:9～21:9整体适配未完成。
- 现有验证中的预期故障注入、编辑器插件关停和资源释放诊断详见Validation，不因出现PASS而隐藏。
