# 素材来源与复现

旧项目 `P:/Godot-Project/ZMXY_BHYH` 只读。下表旧路径相对旧工程，新路径相对 `Godot/GodotProject/TheGame/`；PNG/音频/字体原样复制，不迁入旧脚本或 `.import`。官方包、散列与玩法依据见 [OfficialReference](OfficialReference.md)。

## 角色、关卡与 HUD

| 旧路径 | 新路径 | 规格 / 说明 |
| --- | --- | --- |
| `Art/Monster/Monster1/Wait.png` | `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_idle.png` | 43×63，1 帧 |
| `Art/Monster/Monster1/Walk.png` | `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_run.png` | 64×88 × 4 帧 |
| `Art/Monster/Monster1/Hit.png` | `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_attack.png` | 91×83 × 6 帧 |
| `Art/Monster/Monster1/Hurt.png` | `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_hurt.png` | 68×86 × 2 帧 |
| `Art/Monster/Monster1/Death.png` | `Sprites/Characters/Monsters/huaguoshan_monkey/huaguoshan_monkey_death.png` | 91×88 × 6 帧 |
| `Art/Monster/Monster2/Wait.png` | `Sprites/Characters/Monsters/demon_monkey/demon_monkey_idle.png` | 74×114 × 5 帧 |
| `Art/Monster/Monster2/Walk.png` | `Sprites/Characters/Monsters/demon_monkey/demon_monkey_run.png` | 116×105 × 4 帧 |
| `Art/Monster/Monster2/Hit.png` | `Sprites/Characters/Monsters/demon_monkey/demon_monkey_attack.png` | 130×134 × 4 帧 |
| `Art/Monster/Monster2/Hurt.png` | `Sprites/Characters/Monsters/demon_monkey/demon_monkey_hurt.png` | 89×87 × 1 帧 |
| `Art/Monster/Monster2/Death.png` | `Sprites/Characters/Monsters/demon_monkey/demon_monkey_death.png` | 124×108 × 5 帧 |
| `Art/Level/Level_1/19_1.png` | `Sprites/Levels/huaguoshan/level_1_bg_end.png` | 1440×690；旧 `BackGround/End/end2` 背景端块 |
| `Art/Level/Level_1/48.png` | `Sprites/Levels/huaguoshan/level_1_front.png` | 4957×633；旧 `BackGround/front` 前景视差层 |
| `Art/Level/Level_1/183.png` | `Sprites/Levels/huaguoshan/level_1_floor.png` | 原 4812×170；旧 `BackGround/floor3/floor2` 地板视差层。**已裁至 4700×170**：右侧 112px 圆角收尾（x≥4716 顶面下坠、右下透明）会露出背景，相机右界随之定在 4700 |
| `Art/Level/Gogo/1..67.png` | `Sprites/UI/gogo/gogo_sheet.png` + `gogo_animations.tres` | 67 帧 213×92 打包为 8×9 图集；旧 `Role_information` 的 `Gogo`（`AnimatedSprite2D`，speed 25、循环） |
| `Art/HeroPicture/RoleProperiesBox/408.png` | `Sprites/UI/hud/hud_status_panel.png` | 227×87，状态面板底图 |
| `Art/HeroPicture/RoleProperiesBox/345.png` | `Sprites/UI/hud/hud_hp_fill.png` | 144×12，血条填充 |
| `Art/HeroPicture/RoleProperiesBox/712.png` | `Sprites/UI/hud/hud_menu_panel.png` | 169×133，左下菜单面板；五个旧入口保留 |
| `Art/HeroPicture/RoleProperiesBox/swk.png` | `Sprites/UI/hud/hud_head_wukong.png` | 83×76，悟空头像 |
| `Art/HeroPicture/RoleProperiesBox/748.png` | `Sprites/UI/hud/hud_status_label.png` | 17×46，状态标签装饰 |

地板从 4812×170 裁至 4700×170，去掉右侧透明圆角；该裁剪不覆盖用户后续地形编辑。

旧 `Role1AllEquipment` 的身体、空手、如意金箍棒、木棍、青龙刀和紫金镔铁棍六张图集，以及 `Role1SpecialEffect/Role1Hit1..4.png` 四张棍气图，已由下文官方造一资源替换。工程内十张无引用副本及其 `.import` 已删除，原件仍在只读旧工程中；不将造三式四段轨道继续用于造一图集。

## 动画

怪物动作数据来自旧场景的帧、特效、判定盒值轨道；悟空普攻来自官方造一完整时间轴。当前由身体状态机驱动 AnimationPlayer，动画库位于 `Entitys/Animations/*_anim_library.tres`。悟空特效 SpriteFrames 为 `Sprites/Effects/wukong/wukong_effect_animations.tres`；序列中的 null 表示源时间轴上没有特效的帧，不能删除其播放时长。

`Tools/LegacyMigration/gen_animations.py` 只继续写出怪物初次迁入资源，已有文件默认跳过；当前关键帧以编辑器保存的 `.tres` 为准。悟空旧生成入口已停用，改用下文的官方导出与定向导入工具；不重跑旧 AnimationTree 构建器，也不整库覆盖人工调整。

## 战斗音效

| 旧文件 | 新路径 | `SoundId` | 语义与触发 |
| --- | --- | --- | --- |
| `Music/Hero/40_Role1_hit1AndHit2.mp3` | `Audios/SFX/wukong/wukong_attack_2.mp3` | `WukongAttack1And2` | 造一普攻第 1、2 段共用 |
| `Music/Hero/39_Role1_hit3AndHit4.mp3` | `Audios/SFX/wukong/wukong_attack_1_3.mp3` | `WukongAttack3And4` | 造一普攻第 3、4 段共用 |
| `Music/Hero/38_Role1_hit5.mp3` | `Audios/SFX/wukong/wukong_attack_4.mp3` | `WukongAttack5` | 造一普攻第 5 段 |
| `Music/Hero/49_Role1_beAttack.mp3` | `Audios/SFX/wukong/wukong_hurt.mp3` | `WukongHurt` | **受害者**的受击语音（`BaseHero.gd:592` 按 `self` 选音） |
| `Music/Hero/59_Role1_dead.mp3` | `Audios/SFX/wukong/wukong_death.mp3` | `WukongDeath` | 死亡语音（旧 death 动画的 `add_music(7)`） |
| `Music/MonsterHurt/6_BeattackByRole1.mp3` | `Audios/SFX/wukong/wukong_hit_impact.mp3` | `WukongImpact` | **攻击者**的命中音（`BaseMonster.gd:652` 按攻击者选音）；旧名 `monster_hurt` 是错的——它不是怪物语音，是"悟空的棍打中东西"的声音 |

音频文件名保留迁入时的历史名称，实际段号以表中 SoundId 为准。起手/命中音绑定 AttackConfig，受击/死亡语音绑定受害者配置；音频播放统一经 GGF，职责规则见第 30/50 章。

## 伤害数字

旧来源相对 `Art/AllNumber/`，目标位于 `Sprites/Number/`；生成入口 `Tools/LegacyMigration/gen_damage_numbers.gd`。

| 新文件 | 旧来源 | 格尺寸 | 用途 |
| --- | --- | --- | --- |
| `damage_number_monster_physics.png` | `magic/physics_N`（旧放在 magic 目录） | 30×30 | 怪物受物理伤害 |
| `damage_number_monster_physics_crit.png` | `physicscrit/physics_N` | 42×42 | 怪物受物理暴击 |
| `damage_number_monster_magic.png` | `magic/magic_N` | 30×30 | 怪物受魔法伤害 |
| `damage_number_monster_magic_crit.png` | `magiccrit/magic_N` | 32×32 | 怪物受魔法暴击 |
| `damage_number_hero_physics.png` | `monster/physics/physics_N`（旧放在 monster 目录） | 30×30 | 英雄受物理伤害 |
| `damage_number_hero_magic.png` | `monster/magic/magic_N` | 30×30 | 英雄受魔法伤害 |
| `damage_number_real.png` | `real/real_N` | 30×30 | 真实伤害（人怪共用） |
| `damage_number_miss.png` | `miss.png` | 55×24 | 闪避 |

## HUD 补充与字体

| 新资源（`TheGame/Sprites/UI/hud/`） | 旧资源（`ZMXY_BHYH/`） | 用途 |
| --- | --- | --- |
| `hud_mp_fill.png` | `Art/HeroPicture/RoleProperiesBox/739.png` | 角色魔法填充 |
| `hud_exp_fill.png` | `Art/HeroPicture/RoleProperiesBox/742.png` | 本级经验填充 |

| 新资源（`TheGame/MainPack/Fonts/`） | 旧资源（`ZMXY_BHYH/Font/`） | 用途 |
| --- | --- | --- |
| `fz_cu_yuan_hud.ttf` | `8_FZCuYuan-M03S.ttf` | 旧 `Role_information.tscn` 的生命、魔法、经验数字字体 |
| `fz_cu_yuan.ttf` | `1_fonts.FZCY_FZCuYuan-M03.ttf` | 粗圆中文完整字形回退 |
| `dfp_hai_bao_w12.ttc` | `华康海报体W12 &amp; 华康海报体W12(P).ttc` | 旧 HUD 等级数字字体 |

守关姓名另用 `MainPack/Fonts/df_poster_w12.ttc`，源为旧 `Font/华康海报体W12 &amp; 华康海报体W12(P).ttc`；SHA-256 `b47e00483c38ec74c63601c9200faadb27809374a6a0b720c2f029723342fe3d`，迁入函数 `import_gorilla_assets.import_boss_name_font`。这些字体来自旧工程，不称为官方原包提取字体。

## 菜单、地图、存档与音乐

迁入入口 `Tools/LegacyMigration/import_frontend_assets.py`；布局参考旧 Main_Menu、Map_1、set_menu、read_archive_interface 场景，仅取素材和布局。

| 新资源 | 旧来源 | 用途 |
| --- | --- | --- |
| `Sprites/UI/frontend/title_backdrop.png` | `Art/MainGame/Bg1.png` | 开始页插画（旧作品标题画面） |
| `Sprites/UI/frontend/journey_map.png` | `Art/game_pic/183.png` | 地图底图 |
| `Audios/Music/map_theme.mp3` | `Music/MainSceneMusic/zmxy_1BGM.mp3` | 标题、选角与地图共用造梦一音乐，145.76 秒 |
| `Audios/Music/huaguoshan_theme.mp3` | `Music/level/1_music.mp3` | 花果山音乐 |

| 旧项目相对路径 | 新项目 TheGame 相对路径 |
| --- | --- |
| `Art/game_pic/679.PNG` | `Sprites/UI/frontend/menu_separator.png` |
| `Art/set_menu/653.png` | `Sprites/UI/frontend/settings_scroll.png` |
| `Art/BackPack/49.png` | `Sprites/UI/frontend/menu_close.png` |
| `Font/Aa我读山月山月独我_mianfeiziti.com.ttf` | `MainPack/Fonts/aa_shan_yue.ttf` |
| `Font/Aa文徵明琴赋小楷_mianfeiziti.com.ttf` | `MainPack/Fonts/aa_wen_zheng_ming.ttf` |
| `Art/game_pic/level_choose/185.png` | `Sprites/UI/frontend/map_huaguoshan_normal.png` |
| `Art/game_pic/level_choose/188.png` | `Sprites/UI/frontend/map_huaguoshan_hover.png` |
| `Art/game_pic/level_choose/192.png` | `Sprites/UI/frontend/map_shuiliandong_normal.png` |
| `Art/game_pic/level_choose/195.png` | `Sprites/UI/frontend/map_shuiliandong_hover.png` |
| `Art/game_pic/level_choose/199.png` | `Sprites/UI/frontend/map_taohuayuan_normal.png` |
| `Art/game_pic/level_choose/202.png` | `Sprites/UI/frontend/map_taohuayuan_hover.png` |
| `Art/game_pic/level_choose/206.png` | `Sprites/UI/frontend/map_bianfuku_normal.png` |
| `Art/game_pic/level_choose/209.png` | `Sprites/UI/frontend/map_bianfuku_hover.png` |
| `Art/game_pic/level_choose/220.png` | `Sprites/UI/frontend/map_qixingdong_normal.png` |
| `Art/game_pic/level_choose/223.png` | `Sprites/UI/frontend/map_qixingdong_hover.png` |
| `Art/game_pic/level_choose/213.png` | `Sprites/UI/frontend/map_jialuoluo_normal.png` |
| `Art/game_pic/level_choose/216.png` | `Sprites/UI/frontend/map_jialuoluo_hover.png` |
| `Art/game_pic/qa.png` | `Sprites/UI/frontend/map_qianan_normal.png` |
| `Art/game_pic/level_choose/ShallowShore.png` | `Sprites/UI/frontend/map_qianan_hover.png` |
| `Art/game_pic/dh.png` | `Sprites/UI/frontend/map_donghai_normal.png` |
| `Art/game_pic/level_choose/EastSea.png` | `Sprites/UI/frontend/map_donghai_hover.png` |
| `Art/game_pic/level_choose/267.png` | `Sprites/UI/frontend/map_shazhiwoliu_normal.png` |
| `Art/game_pic/level_choose/SharkVortex.png` | `Sprites/UI/frontend/map_shazhiwoliu_hover.png` |
| `Art/game_pic/level_choose/234.png` | `Sprites/UI/frontend/map_niumogu_normal.png` |
| `Art/game_pic/level_choose/237.png` | `Sprites/UI/frontend/map_niumogu_hover.png` |
| `Art/HeroPicture/RoleProperiesBox/757.png` | `Sprites/UI/hud/menu_inventory_normal.png` |
| `Art/HeroPicture/RoleProperiesBox/759.png` | `Sprites/UI/hud/menu_inventory_hover.png` |
| `Art/HeroPicture/RoleProperiesBox/756.png` | `Sprites/UI/hud/menu_inventory_caption.png` |
| `Art/HeroPicture/RoleProperiesBox/751.png` | `Sprites/UI/hud/menu_settings_normal.png` |
| `Art/HeroPicture/RoleProperiesBox/753.png` | `Sprites/UI/hud/menu_settings_hover.png` |
| `Art/HeroPicture/RoleProperiesBox/750.png` | `Sprites/UI/hud/menu_settings_caption.png` |
| `Art/HeroPicture/RoleProperiesBox/763.png` | `Sprites/UI/hud/menu_skills_normal.png` |
| `Art/HeroPicture/RoleProperiesBox/765.png` | `Sprites/UI/hud/menu_skills_hover.png` |
| `Art/HeroPicture/RoleProperiesBox/762.png` | `Sprites/UI/hud/menu_skills_caption.png` |
| `Art/HeroPicture/RoleProperiesBox/769.png` | `Sprites/UI/hud/menu_weapon_normal.png` |
| `Art/HeroPicture/RoleProperiesBox/771.png` | `Sprites/UI/hud/menu_weapon_hover.png` |
| `Art/HeroPicture/RoleProperiesBox/768.png` | `Sprites/UI/hud/menu_weapon_caption.png` |
| `Art/HeroPicture/RoleProperiesBox/775.png` | `Sprites/UI/hud/menu_pet_normal.png` |
| `Art/HeroPicture/RoleProperiesBox/777.png` | `Sprites/UI/hud/menu_pet_hover.png` |
| `Art/HeroPicture/RoleProperiesBox/774.png` | `Sprites/UI/hud/menu_pet_caption.png` |

| 旧项目相对路径 | 新项目 TheGame 相对路径 | 用途 |
| --- | --- | --- |
| `Art/MagicWeapon/222.png` | `Sprites/UI/frontend/menu_close_hover.png` | 41×41 关闭叉白光悬停 |
| `Art/Game_2/Button/ButtonReturnMainMenu.png` | `Sprites/UI/frontend/map_return_title.png` | 地图返回主菜单，题签沿用旧场景的叠字 |
| `Art/game_pic/level_choose/294.png` | `Sprites/UI/frontend/map_save_game.png` | 地图手动保存图片 |
| `Art/game_pic/ArchivePicture/87.png` | `Sprites/UI/frontend/save_slots_backdrop.png` | 造梦二风格存档记录框 |
| `Art/MainGame/BasicPicCd_1.png` | `Sprites/UI/frontend/save_slot_normal.png` | 存档格普通图 |
| `Art/MainGame/BasicPicCd_2.png` | `Sprites/UI/frontend/save_slot_hover.png` | 存档格白框悬停图 |

`Audios/UI/menu_confirm.wav` 是迁入脚本生成的原创 0.12 秒双正弦衰减音，44100 Hz 单声道 PCM16；没有旧文件来源。Music 资源导入开启循环，由 GGF Music 组管理。

官方选角卡片来自 OtherMat_v9 原始矢量，直接渲染而非截图或 AI 重绘：

| 目标 PNG（Sprites/UI/frontend） | 官方矢量编号 | AtlasTexture 区域 |
| --- | --- | --- |
| `character_wukong_card.png` | DefineSprite 511 | (9, 0, 237, 610) |
| `character_sanzang_card.png` | DefineSprite 521 | (21, 0, 237, 610) |
| `character_bajie_card.png` | DefineSprite 515 | (15, 0, 237, 610) |
| `character_wujing_card.png` | DefineSprite 518 | (22, 0, 237, 610) |

复现：`python Tools/LegacyMigration/export_official_character_cards.py --java <java.exe> --ffdec <ffdec.jar> --swf <OtherMat_v9.swf>`。贴图自带姓名、笔刷、剪影和问号；AtlasTexture 只排除透明侧边。

## 大猩猩、出口与血条

迁入入口 `Tools/LegacyMigration/import_gorilla_assets.py`；已有产物默认跳过。

| 旧项目相对路径 | 新项目 TheGame 相对路径 |
| --- | --- |
| `Art/Monster/Monster3/Wait.png` | `Sprites/Characters/Monsters/gorilla/gorilla_idle.png` |
| `Art/Monster/Monster3/Walk.png` | `Sprites/Characters/Monsters/gorilla/gorilla_run.png` |
| `Art/Monster/Monster3/Hit.png` | `Sprites/Characters/Monsters/gorilla/gorilla_attack_1.png` |
| `Art/Monster/Monster3/Hurt.png` | `Sprites/Characters/Monsters/gorilla/gorilla_hurt.png` |
| `Art/Monster/Monster3/Death.png` | `Sprites/Characters/Monsters/gorilla/gorilla_death.png` |
| `Art/Level/Export.png` | `Sprites/Effects/level_exit/level_exit_sheet.png` |
| `Art/HeroPicture/RoleProperiesBox/128.png` | `Sprites/UI/hud/boss_health_fill.png` |
| `Art/HeroPicture/RoleProperiesBox/124.png` | `Sprites/UI/hud/boss_health_under.png` |

平台来源：旧 `Art/Level/level_2/177.png` → `Sprites/Levels/huaguoshan/level_1_platform.png`。

## 官方升级、掉落球与无双演出

| 目标 | 官方资源编号 | 复现入口 |
| --- | --- | --- |
| `Sprites/Effects/level_up/` | Role_v7 sprite 240，28 帧 | `export_official_combat_effects.py` |
| `Sprites/Effects/loot/musou_01..19.png` | Monster_v1 sprite 5，黄球 | 同上 |
| `Sprites/Effects/loot/soul_*.png` | Monster_v1 sprite 9，紫球 | 同上 |
| `Sprites/Effects/loot/mana_*.png` | Monster_v1 sprite 7，蓝球 | 同上 |
| `Sprites/UI/hud/musou_activation/` | Role_v7 WsEffect 59：背景 52、悟空 55 | `export_official_musou_activation.py` |

升级动画保留原始 PNG；后半段位图外的蓝色标记会被 FFDec 边缘采样带入，导出器通过 `AtlasTexture.region` 裁掉这一行，并用 `margin` 补回透明留白、启用 `filter_clip`。28 帧仍为 122×136，播放位置和时长不变；只重导升级动画可加 `--group level_up`。

两个导出器均用 Java/FFDec 26.3，在临时目录校验散列和渲染，再复制 PNG 与外置 SpriteFrames。无双开场在 `(470,295)` 输出 940×590、24 帧；悟空末姿势保持时，火焰子动画继续播放。调用参数见各脚本 `--help`。

满值提示已改为 OtherMat_v9 的 `340`：`battle_menu_ws_glow_frames.tres` 引用 19 张 78×78 原版透明帧，24 fps 循环；不再使用旧 WSBar 图集与裁剪 shader。原始注册范围为 `(-44,-44)..(34,34)`；最亮帧边缘保留原版低 alpha 柔光。

`Tools/LegacyMigration/export_official_battle_menu.py` 校验同一 OtherMat_v9 散列后导出：

| 原版元件 | 正式资源（`Sprites/UI/hud/`） | 规格 |
| --- | --- | --- |
| `328` 的 11 帧 | `battle_skill_icons.png`、`battle_skill_icon_frames.tres` | 45×45；第 1 帧为未学习槽，后 10 帧为两位角色的已学习图标；每技能保留灰/彩两帧 |
| `330`、`333` | `battle_menu_ws_under.png`、`battle_menu_ws_fill.png` | 221×27 底条、221×12 填充；填充偏移 (0,9) |
| 位图 `335` | `battle_menu_ws_idle.png` | 68×68 无双未满图 |
| `340` | `battle_menu_ws_glow_00..18.png`、`battle_menu_ws_glow_frames.tres` | 19 帧、24 fps、78×78 |

按用户要求，HUD 保留旧版法宝、宠物、背包、设置、技能入口及 `hud_menu_panel.png` 的布局。只替换无双和五个主动技能图标；法宝、宠物仍为未实现入口，素材为后续版本保留。

## 结算与战斗背包

`Tools/LegacyMigration/import_battle_ui_assets.py` 逐字节复制以下旧工程图片，不迁入旧脚本、场景或 `.import`。此次图片来源为旧工程，未将其宣称为新下载的官方原包。

| 旧项目相对路径 | 新项目 TheGame 相对路径 |
| --- | --- |
| `Art/Level/Settlement/623.png` | `Sprites/UI/settlement/settlement_backdrop.png` |
| `Art/Level/Settlement/637.png` | `Sprites/UI/settlement/settlement_success.png` |
| `Art/Level/Settlement/622.png` | `Sprites/UI/settlement/settlement_failure.png` |
| `Art/Level/Settlement/436.png` | `Sprites/UI/settlement/settlement_results.png` |
| `Art/Level/Settlement/630.png` | `Sprites/UI/settlement/settlement_map_normal.png` |
| `Art/Level/Settlement/632.png` | `Sprites/UI/settlement/settlement_map_hover.png` |
| `Art/Level/Settlement/457.png` | `Sprites/UI/settlement/settlement_retry_normal.png` |
| `Art/Level/Settlement/459.png` | `Sprites/UI/settlement/settlement_retry_hover.png` |
| `Art/BackPack/BackPackLight.png` | `Sprites/UI/inventory/inventory_preview_light.png` |
| `Art/BackPack/207.png` | `Sprites/UI/inventory/inventory_panels.png` |
| `Art/BackPack/28.png` | `Sprites/UI/inventory/inventory_ink_frame.png` |
| `Art/BackPack/113.png` | `Sprites/UI/inventory/inventory_cell.png` |
| `Art/BackPack/244.png` | `Sprites/UI/inventory/inventory_attributes.png` |
| `Art/BackPack/219.png` | `Sprites/UI/inventory/inventory_level_badge.png` |
| `Art/BackPack/zdlnc.png` | `Sprites/UI/inventory/inventory_name_plate.png` |
| `Art/BackPack/lh_wk.png` | `Sprites/UI/inventory/inventory_soul_plate.png` |
| `Art/BackPack/209.png` | `Sprites/UI/inventory/inventory_exp_fill.png` |
| `Art/BackPack/211.png` | `Sprites/UI/inventory/inventory_exp_frame.png` |
| `Art/BackPack/zb_button.png` | `Sprites/UI/inventory/inventory_tab_normal.png` |
| `Art/BackPack/zb_button_choose.png` | `Sprites/UI/inventory/inventory_tab_selected.png` |
| `Art/BackPack/wq.png` | `Sprites/UI/inventory/inventory_slot_weapon.png` |
| `Art/BackPack/fj.png` | `Sprites/UI/inventory/inventory_slot_armor.png` |
| `Art/BackPack/sp.png` | `Sprites/UI/inventory/inventory_slot_accessory.png` |
| `Art/BackPack/fb.png` | `Sprites/UI/inventory/inventory_slot_treasure.png` |
| `Art/BackPack/sz.png` | `Sprites/UI/inventory/inventory_slot_costume.png` |
| `Art/BackPack/tx.png` | `Sprites/UI/inventory/inventory_slot_title.png` |

`UIs/Widgets/hero_preview.tscn` 与战斗实体共用下述官方身体、武器图集及 `wukong_appearance.tres`。外置 `inventory_preview_animation.tres` 使用原版完整待机 100 帧（24 fps，约 4.167 秒循环）；金光取旧背包 BackPackLight 的第 3..8 格（192×192，15 fps，竖直翻转）。动画只改变显示帧，不带攻击或碰撞轨道。

## 装备与地面恢复品

`Tools/LegacyMigration/import_equipment_assets.py` 逐字节复制旧项目 `Art/ItemIcon/peach.png` 为 `Sprites/Items/consumables/pickup_small_peach.png` 与 `pickup_large_peach.png`（大桃子通过显示比例区分），`Bble.png` 为 `pickup_mana_bottle.png`。

旧工程的粗糙装备图标缺失，曾暂用普通图；现已删除这组临时替代图。八件装备的图片均直接导出自官方 [OtherMat_v9.swf](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/OtherMat_v9.swf)，恢复后 SHA-256 见 OfficialReference。原背包位图带黑色底板；按用户要求，背包和地面现在均使用对应的透明前景位图，保留原始 alpha，未重绘或调色。

| 装备 | 背包位图 ID | 地面位图 ID | TheGame/Sprites/Items/equipment 文件名后缀 |
| --- | ---: | ---: | --- |
| 粗糙行者服 | 52 | 29 | rough_pilgrim_clothes |
| 粗糙行者棍 | 60 | 16 | rough_pilgrim_staff |
| 粗糙袈裟 | 48 | 25 | rough_cassock |
| 粗糙松木杖 | 55 | 14 | rough_pine_staff |
| 普通行者服 | 51 | 28 | common_pilgrim_clothes |
| 普通行者棍 | 59 | 15 | common_pilgrim_staff |
| 普通袈裟 | 47 | 24 | common_cassock |
| 普通松木杖 | 54 | 13 | common_pine_staff |

背包文件为 `equipment_<后缀>.png`，地面文件为 `ground_<后缀>.png`。复现：`python Tools/LegacyMigration/export_official_equipment.py --java <java.exe> --ffdec <ffdec.jar> --source <OtherMat_v9.swf>`；源包、解包 XML 与工具留在临时目录，不提交旧项目脚本。

地面图保留原始透明画布。`GroundItemNode.Show` 每次显示时读取非透明区域，将其底边按显示缩放对齐场景碰撞体底边；通过 Sprite2D.Offset 定位，Position 留给领取时的上移淡出动画。更换图标或池复用时重新计算，不按整张图片底边定位，也不改碰撞尺寸。此规则仅用于静态掉落图标，角色动作仍保留统一原点。

粗糙与普通的原背包位图不同，但同款装备的两个透明前景位图在原文件中内容相同；目前保留原样，品质由名称和详情区分，不人为改色。

## 悟空独立身体与武器

当前战斗和背包使用官方 `Role_v7.swf` 的 `export.hero.Role1`（sprite 575）动作。四张图均为 1200×4000、6×20、每格 200×200；保留基础与技能的前 108 格（含原填充格），追加完整待机缺少的 8 个姿势，末行余 4 格：

| 新文件（`Sprites/Characters/Heroes/wukong/`） | 源选择帧 | 用途 |
| --- | --- | --- |
| `wukong_body_bare.png` | 衣服元件 143/152/160/186 的第 1 帧 | 未穿防具的身体 |
| `wukong_body_pilgrim.png` | 同上第 2 帧 | 粗糙/普通行者服共用外观 |
| `wukong_weapon_default.png` | 武器元件 177 第 1 帧 | 默认木棍 |
| `wukong_weapon_pilgrim.png` | 同上第 2 帧 | 粗糙/普通行者棍共用外观 |

沿用旧 `Scene/Hero/Role_1/Role1.tscn` 的两层结构：完整身体在后，武器在前；源绘制深度位于武器前方的头部、手臂等作为导出遮罩，裁掉武器被遮挡的部分。遮罩只在导出时使用，不创建第三个运行时层，也不导出武器和防具的组合图集。两种防具须保持相同的前景遮挡轮廓，便于武器独立替换。

所有动作保留 Role1 父级中的完整矩阵，统一 SVG viewBox `(-100,-128,200,200)`，原 Flash 原点位于格内 `(100,128)`。例如待机平移 `(1.45,0.8)`、慢走 `(-6.85,0.85)`、跑步 `(3.75,0.2)`、受击 `(-17.95,2.6)`；漏掉这些矩阵会造成动作切换跳位。不能按各帧可见边界重新居中，也不能在运行时移动角色根节点补偿。

帧号为零起：基础动作 0..24；五段普攻依次为 25..32、33..40、41..51、52..71、72..86。身体与武器共用帧号，源动作暂时隐藏武器的 3 帧保留透明，不能因此移除武器槽并破坏同步。技能 122 个源帧按四张图共同的像素签名复用 20 格，映射记录在技能导出 manifest 的 `frame_cells`；透明阶段共用一格，原始时间轴仍完整保留，不预留整片空槽。

固定红金色金箍棒形状的残影只来自元件 246，导出时在所有动作中过滤；保留 203/254/282 的弧光和旋转特效。第三、四段仍有原版动态效果，第五段去掉棒影后没有独立可见特效。身体、真实武器及源时序不变。特效图 `Sprites/Effects/wukong/wukong_attack_effects.png` 为 2400×1200、6×4、每格 400×300，19 个可见源帧、末行余 5 格。五段共 62 个源帧，43 个无特效帧用 null 保留时序而不占图集。特效 viewBox `(-200,-178,400,300)`，Flash 原点对应本地 `(0,28)`；按源深度裁去被后续身体/武器遮挡的部分，不混入装备层。

判定使用两种真实武器 alpha≥0.25 与当帧剑气 alpha≥0.1 的像素并集，生成覆盖武器和剑气残留的矩形；水平与长轴旋转两种候选取面积较小者，外围 3px 余量、最小厚度 14px。防具和武器外观不改变命中范围。原版探针提供有效时间窗，保留剑气均在窗口内；第四段前三帧只有弧光也保持判定。修改美术后经导出工具更新几何，再用逐帧截图及真实武器近端、剑气区域和范围外目标检查对齐。

复现入口为 `Tools/LegacyMigration/export_official_wukong.py`（核对源 SHA、保留矩阵和逐帧深度）与 `EditorScripts/import_official_wukong.gd`（原生 SVG 栅格化、紧凑打包、拟合判定并输出 manifest）。必须显式指定工程内的特效输出路径；完整命令统一放在 [Validation](Validation.md)。随后 `Tools/LegacyMigration/import_official_wukong_attacks.py` 按 manifest 定向替换五段攻击、特效序列和矩形判定轨道，移除无消费者的 skill_*，一次性映射基础帧号并更新两处场景网格；其余基础动作只补齐判定旋转归零轨道，手调时序及已有轨道保持原样。重复执行不会再次映射。

当前 13 个玩法动作（8 个基础动作、5 段攻击）和 RESET 使用同一两层资源。`wait` 子元件 529 的普通待机、立棍和张望属于同一条 100 帧时间轴，完整导入 `idle1` 与背包 `idle`，不再截取两帧拼成 `idle2` 或额外随机触发。`import_official_wukong_idle.gd` 只追加缺少姿势，`Tools/LegacyMigration/import_official_wukong_idle.py` 只更新待机轨道和两处网格；已有攻击、技能及手调时序保持不变。复现时在基础与技能导入后执行，命令见 Validation。悟空五项造一技能另挂 `skills` 动画库，仅供准备与预览；造三攻击没有混入这些资源。

## 恢复飘字与装备菜单

| 新资源 | 来源 | 处理 |
| --- | --- | --- |
| `Sprites/Number/recovery_number_health.png` | 旧 `Art/AllNumber/cure/cure_0..9.png` | 原位图横向拼接，绿色恢复生命数字 |
| `Sprites/Number/recovery_number_mana.png` | 旧 `Art/AllNumber/CureMp/CureMp_0..9.png` | 原位图横向拼接，蓝色恢复魔法数字 |
| `Sprites/UI/inventory/inventory_actions_background.png` | 造梦三 `Commonv3720.swf`，shape 369 | 87×115 菜单底板 |
| `inventory_action_equip.png` / `inventory_action_equip_disabled.png` | 同包 sprite 376，第 1/2 帧 | 77×31 装备按钮 |
| `inventory_action_gift_disabled.png` | 同包 sprite 367，第 2 帧 | 单人赠送禁用 |
| `inventory_action_sell.png` | 同包 sprite 381，第 1 帧 | 出售按钮 |

恢复数字由 `Tools/LegacyMigration/gen_damage_numbers.gd` 拼接，与伤害数字、闪避共用 `Entitys/Effects/floating_text.tscn` 和 `FloatingTextNode`，表现规则见 OfficialReference。菜单由 `Tools/LegacyMigration/export_official_inventory_actions.py` 核对官方包散列后导出；来源为矢量元件，未截图或重绘。提示框按官方 `AttributeCon.as` 的字体、间距、颜色和装饰重建为原生 Godot 控件。

数字排布参考旧 `Script/Hittext/DamageText.gd` 的 HBox 组合和造一 `my/ANumber.as` 的固定步长，再按用户要求改为比例字宽。图集仍为等宽十格存储；显示时仅去除各数字横向的全透明列，包含所有非透明描边，保留完整高度及纵向注册位置。后续美术应保持各数字同一基线、风格和格高，无需把“1”等窄字符拉宽；不得以图集格宽代替字符可见宽度。场景 `floating_text.tscn` 的 `m_DigitGap` 控制可见轮廓之间的间隙，默认 0px；`m_NarrowGlyphPadding` 默认 1/6，按当前字符与同样式最宽字符的宽度差给窄字补侧留白，适用于全部 0–9，而非特判 1/8。既有 PNG 未修改。

翻页按钮直接迁自旧项目 `Art/BackPack/LastPageN/Y.png`、`NextPageN/Y.png`，分别保留普通/悬停图，命名为 `inventory_previous_page[_hover].png`、`inventory_next_page[_hover].png`；图片已有箭头和文字，不叠加 Label。只复制 PNG，Godot 在新路径生成导入信息。

## 造一技能面板

`Sprites/UI/skills/` 的 34 张图片由 `Tools/LegacyMigration/export_official_skill_ui.py` 直接从上述官方 `OtherMat_v9.swf` 导出，没有截图、重绘或迁入旧逻辑。脚本检查恢复后的 SHA-256，使用 FFDec 原生 PNG 渲染保留位图图案和透明度；简单按钮按原始 Up/Over 显示列表导出。

| 原元件 | 新资源 | 状态 / 尺寸 |
| --- | --- | --- |
| bitmap 375 / 586 | `skills_background.jpg` / `skills_soul_badge.png` | 940×590 背景 / 74×66 灵魂徽章 |
| sprite 585 | `skills_table_active.png` / `skills_table_passive.png` | 第 1/2 帧，746×429 表格 |
| sprite 382 / 386 | `skills_wukong_*` / `skills_sanzang_*` | 第 3 帧灰色未选中、第 2 帧彩色选中；两页均可查看 |
| button 295 / 657 / 653 | `skills_back_*` / `skills_active_*` / `skills_passive_*` | 普通与悬停 |
| sprite 594/599/604/609/614 | 五项悟空 `skill_*_unlearned/learned.png` | 第 1 帧灰色含“学习”、第 2 帧彩色，65×65 |
| sprite 624/629/634/639/644 | 五项唐僧 `skill_*_unlearned/learned.png` | 同上；629 对应玄冰阵 |

原点取 `BuySkill`、`SkillControl` 和子元件矩阵：角色页签 `(89.35,6.85)` / `(228.35,5.85)`、返回 `(854,15.05)`、表格左上 `(100,85)`。十行主动与五行被动直接位于技能主场景，以 GridContainer 对齐，不使用行子场景；唐僧长说明在行内 MarginContainer + Label 控制留白和换行。预填文本和图标仅供编辑预览，运行时读取正式技能表覆盖。当前全部技能展示未学习状态，图标和升级字样不提交购买。

## 悟空造一技能动作与声音

`Tools/LegacyMigration/export_official_wukong_skills.py` 检查同一 Role_v7 散列，冻结衣服与武器选择器并保留 Role1 父级矩阵；`EditorScripts/import_official_wukong_skills.gd` 栅格化两层与遮罩；`Tools/LegacyMigration/import_official_wukong_skills.py` 紧凑打包并生成外置值轨道。原有 87 格逐像素保持相同，基础动作库不重生成。

| 动画库名称（`skills/`） | 源 body 元件 | 源帧数 / 24 fps | Role1 父级变换 |
| --- | --- | --- | --- |
| rising_dragon 升龙斩 | 384 | 15 / 0.625 秒 | 平移 (2.15,-1.6) |
| flame_thrust 火炎突击 | 569 | 15 / 0.625 秒 | 平移 (-15.2,-3.8) |
| blazing_flash 烈焰闪 | 344 | 12 / 0.5 秒 | 平移 (0.75,0.25) |
| flame_storm 烈焰风暴 | 417 | 10 / 0.416667 秒 | 平移 (-17.55,2.3) |
| fire_demon_slash 火魔斩 | 574 | 70 / 2.916667 秒 | X 镜像、平移 (4.75,26) |

统一角色原点仍为格内 `(100,128)`、Sprite 本地 `(0,28)`；技能只改变帧和判定，不移动角色根节点。源元件 338 是烈焰闪的模糊轮廓，Godot SVG 滤镜会画成矩形色块，因此与 371/416/568 的弧光改用 FFDec 原生 PNG。`FlashSpriteBounds.java` 读取 `Timeline.getDisplayRectWithFilters` 的精确导出矩形后再应用父级变换，不能按可见像素重新居中或猜测滤镜留白。41 个可见帧存于 `wukong_skill_inline_effects.png`（800×400 格，4 列）；无光效帧保留 null。

烈焰闪不把模糊光效当作超宽命中区：保留原透明探针 205 的几何，只在验证/拟合时转为不透明。其他有 stick 的技能按源窗口及可见能量拟合两种武器共同的矩形，沿用 3px 余量、最小厚度 14px；透明能量阶段关闭判定。火魔斩主体没有 stick，70 帧内的大段透明是原文件事实，伤害与表现由独立特效承担。

独立特效位于 `Sprites/Effects/wukong/skills/`、外置库 `Entitys/Animations/Skills/`、场景 `Entitys/Effects/Skills/`；图片用 `_atlas`、场景用 `_effect`、序列用 `_frames`、库用 `_library` 后缀，保证全树 basename 唯一：烈焰闪拖尾 66（10 帧）、火魔起手 129（40 帧）、九连斩 112（44 帧）、下冲 133（2 帧）、落地斩 123（13 帧）。后三项带独立 HitBox；无方法轨道、玩法脚本或自动播放，不提前接入实战。

七段声音取自[官方 Music.swf](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/Music.swf)，恢复后 SHA-256 `5cad2f1ef3ce592c2e9e44c631748e042d8cacae2c7571e57b1a0546998a7578`。`export_official_skill_audio.py` 原样导出 MP3：6/8/11/1 分别为升龙斩/火炎突击/烈焰闪/烈焰风暴，13/14/15 为火魔起手/九连斩/落地。正式路径 `Audios/SFX/wukong/skills/`，SoundId 7..13，全部登记到 GGF SFX，无循环。
