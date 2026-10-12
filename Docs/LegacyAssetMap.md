# 素材来源与复现

旧项目 `P:/Godot-Project/ZMXY_BHYH` 只读。下表旧路径相对旧工程，新路径相对 `Godot/GodotProject/TheGame/`；PNG/音频/字体原样复制，不迁入旧脚本或 `.import`。官方包、散列与玩法依据见 [OfficialReference](OfficialReference.md)。

## 角色、关卡与 HUD

| 旧路径 | 新路径 | 规格 / 说明 |
| --- | --- | --- |
| `Art/HeroPicture/Role1AllEquipment/Role_1_Body_Empty.png` | `Sprites/Characters/Heroes/wukong/wukong_body.png` | 1200×2800，6 列 × 14 行，200×200 / 帧（84 帧） |
| `Art/HeroPicture/Role1AllEquipment/Role_1_Eq_Empty.png` | `Sprites/Characters/Heroes/wukong/wukong_weapon_empty.png` | 同上网格；**武器层**（空手），见下「武器层」 |
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
| `Art/HeroPicture/RoleProperiesBox/742.png` | `Sprites/UI/hud/hud_hp_delay.png` | 144×12，血条残影/经验填充 |
| `Art/HeroPicture/RoleProperiesBox/712.png` | `Sprites/UI/hud/hud_menu_panel.png` | 169×133，右下菜单面板 |
| `Art/HeroPicture/RoleProperiesBox/718.png` | `Sprites/UI/hud/hud_ws_frame.png` | 260×22，无双条框 |
| `Art/HeroPicture/RoleProperiesBox/720.png` | `Sprites/UI/hud/hud_ws_under.png` | 221×28，无双条底 |
| `Art/HeroPicture/RoleProperiesBox/724.png` | `Sprites/UI/hud/hud_ws_fill.png` | 221×13，无双条填充 |
| `Art/HeroPicture/WSGrey.png` | `Sprites/UI/hud/hud_ws_grey.png` | 72×71，无双灰底 |
| `Art/HeroPicture/WSBar.png` | `Sprites/UI/hud/hud_ws_max_sheet.png` | 890×89，10 张 89×89 帧；满值时 `AnimatedSprite2D` 以 25 FPS 往返循环。`hud_ws_flash.gdshader` 用首帧轮廓和可调圆形范围限制透明度；默认半径 32px、圆心 (0.5, 0.5)、向内羽化 1px，源图保留不变 |
| `Art/HeroPicture/RoleProperiesBox/swk.png` | `Sprites/UI/hud/hud_head_wukong.png` | 83×76，悟空头像 |
| `Art/HeroPicture/RoleProperiesBox/748.png` | `Sprites/UI/hud/hud_status_label.png` | 17×46，状态标签装饰 |
| `Art/HeroPicture/Role1SpecialEffect/Role1Hit1.png` | `Sprites/Effects/wukong/wukong_hit_1.png` | 210×206 × 5 帧；普攻 1 段棍气 |
| `Art/HeroPicture/Role1SpecialEffect/Role1Hit2.png` | `Sprites/Effects/wukong/wukong_hit_2.png` | 159×54 × 2 帧；普攻 2 段棍气 |
| `Art/HeroPicture/Role1SpecialEffect/Role1Hit3.png` | `Sprites/Effects/wukong/wukong_hit_3.png` | 326×66 × 4 帧；普攻 3 段棍气 |
| `Art/HeroPicture/Role1SpecialEffect/Role1Hit4.png` | `Sprites/Effects/wukong/wukong_hit_4.png` | 315×82 × 2 帧；普攻 4 段棍气 |

地板从 4812×170 裁至 4700×170，去掉右侧透明圆角；该裁剪不覆盖用户后续地形编辑。

## 武器

下列文件均在旧 `Art/HeroPicture/Role1AllEquipment/`，新 `Sprites/Characters/Heroes/wukong/`；身体和武器共用 6×14、每格 200×200 网格。

| 旧文件 | 新文件 | 旧键 / 名称 |
| --- | --- | --- |
| `Role_1_Eq_Empty.png` | `wukong_weapon_empty.png` | 空手 |
| `Role_1_Eq_ryjgb.png` | `wukong_weapon_golden_cudgel.png` | `ryjgb` 如意金箍棒 |
| `Role_1_Eq_wkjdyhwq.png` | `wukong_weapon_wooden_stick.png` | `wkjdyhwq` 经典原画·木棍 |
| `Role_1_Eq_qld.png` | `wukong_weapon_dragon_blade.png` | `qld` 青龙刀 |
| `Role_1_Eq_zjbtg.png` | `wukong_weapon_purple_gold_cudgel.png` | `zjbtg` 紫金镔铁棍 |

## 动画

动作数据来自旧场景的帧、特效、判定盒值轨道；当前由身体状态机驱动 AnimationPlayer。动画库位于 `Entitys/Animations/*_anim_library.tres`，棍气 SpriteFrames 为 `Sprites/Effects/wukong/wukong_effect_animations.tres`。攻击特效序列中的 null 是收招空白帧，不能删除。

`Tools/LegacyMigration/gen_animations.py` 负责初次迁入，已有文件默认跳过；当前关键帧以编辑器保存的 `.tres` 为准。`--force` 会覆盖人工调整，仅用于明确需要的资源，不重跑旧 AnimationTree 构建器。

## 战斗音效

| 旧文件 | 新路径 | `SoundId` | 语义与触发 |
| --- | --- | --- | --- |
| `Music/Hero/40_Role1_hit1AndHit2.mp3` | `Audios/SFX/wukong/wukong_attack_2.mp3` | `WukongAttack2` | 挥棍起手（普攻第 2 段）；旧文件名与实际用法不符，以 method 轨道实测为准 |
| `Music/Hero/39_Role1_hit3AndHit4.mp3` | `Audios/SFX/wukong/wukong_attack_1_3.mp3` | `WukongAttack1And3` | 挥棍起手（普攻 1、3 段共用） |
| `Music/Hero/38_Role1_hit5.mp3` | `Audios/SFX/wukong/wukong_attack_4.mp3` | `WukongAttack4` | 挥棍起手（普攻第 4 段） |
| `Music/Hero/49_Role1_beAttack.mp3` | `Audios/SFX/wukong/wukong_hurt.mp3` | `WukongHurt` | **受害者**的受击语音（`BaseHero.gd:592` 按 `self` 选音） |
| `Music/Hero/59_Role1_dead.mp3` | `Audios/SFX/wukong/wukong_death.mp3` | `WukongDeath` | 死亡语音（旧 death 动画的 `add_music(7)`） |
| `Music/MonsterHurt/6_BeattackByRole1.mp3` | `Audios/SFX/wukong/wukong_hit_impact.mp3` | `WukongImpact` | **攻击者**的命中音（`BaseMonster.gd:652` 按攻击者选音）；旧名 `monster_hurt` 是错的——它不是怪物语音，是"悟空的棍打中东西"的声音 |

起手/命中音绑定 AttackConfig，受击/死亡语音绑定受害者配置；音频播放统一经 GGF，职责规则见第 30/50 章。

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

满值提示另来自旧 WSBar.png：`hud_ws_full_frames.tres` 保存 10 个 AtlasTexture 的 20 帧往返循环（25 fps）；圆形裁剪只在 shader 中进行，不修改源图。

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
| `Art/BackPack/123.png` | `Sprites/UI/inventory/inventory_page_one.png` |
| `Art/BackPack/118.png` | `Sprites/UI/inventory/inventory_page_two.png` |
| `Art/BackPack/zb_button.png` | `Sprites/UI/inventory/inventory_tab_normal.png` |
| `Art/BackPack/zb_button_choose.png` | `Sprites/UI/inventory/inventory_tab_selected.png` |
| `Art/BackPack/wq.png` | `Sprites/UI/inventory/inventory_slot_weapon.png` |
| `Art/BackPack/fj.png` | `Sprites/UI/inventory/inventory_slot_armor.png` |
| `Art/BackPack/sp.png` | `Sprites/UI/inventory/inventory_slot_accessory.png` |
| `Art/BackPack/fb.png` | `Sprites/UI/inventory/inventory_slot_treasure.png` |
| `Art/BackPack/sz.png` | `Sprites/UI/inventory/inventory_slot_costume.png` |
| `Art/BackPack/tx.png` | `Sprites/UI/inventory/inventory_slot_title.png` |

`UIs/Widgets/hero_preview.tscn` 复用现有悟空身体和空手武器图集。外置 `inventory_preview_animation.tres` 取当前待机的 0..5 帧（0.8 秒循环）；金光取旧背包 BackPackLight 的第 3..8 格（192×192，15 fps，竖直翻转）。动画只改变显示帧，不带攻击或碰撞轨道。
