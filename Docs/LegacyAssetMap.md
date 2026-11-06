# 素材来源与复现

旧项目 `P:/Godot-Project/ZMXY_BHYH` 只读。下表旧路径相对旧工程，新路径相对 `Godot/GodotProject/TheGame/`；PNG/音频/字体原样复制，不迁入旧脚本或 `.import`。官方包、散列与玩法依据见 [OfficialReference](OfficialReference.md)。

## 唐僧官方声音（2026-11-05）

复用`Tools/LegacyMigration/export_official_skill_audio.py --hero tangseng`，校验既有Music.swf固定版本后由FFDec导出原始MP3载荷。正式目录为`Audios/SFX/tangseng/`，五技能放在`skills/`。SymbolClass映射：23→tangseng_attack、24→skills/tangseng_ice_dragon、25→skills/tangseng_dark_ice、20→skills/tangseng_water_clone、22→skills/tangseng_heavenly_dew、5→skills/tangseng_water_demon、4→tangseng_hit_impact、27→tangseng_hurt。全部无循环、无重编码，资源表LegacyId保留official.Music符号名。

Role2普通起手、满蓄/未满蓄、分身换位、甘露本体/分身双起手及水魔追加声音按OfficialReference的“唐僧已实装规则与项目取舍”登记。Role2Shadow玄冰不自行发声；本版本无独立Role2死亡音。8段真实GGF播放及实际输入链已验证，精确命令、音频源散列基线和退出诊断见Validation/OfficialReference。

一般地图按钮统一由`register_official_map_location.gd`登记（含蝙蝠窟、七星洞、迦楼罗）；旧enable/rename分步脚本已合并。沿海三地点继续走完整源矩阵专用流程，不能用普通按钮居中缩放替代。重建前保留正式人工位置；命令和本轮隔离副本验证见Validation。

## 官方地图技能入口与连击图元（2026-11-03）

`Tools/LegacyMigration/export_official_map_combo_ui.py`校验OtherMat_v9恢复后散列，通过FFDec原生固定舞台渲染，保留原始矩阵、滤镜及注册点。外层三态选择器停帧不停止内部动画：idle为1帧，hover的488/487为15帧单次展开并在末帧stop，available的491为19帧发光循环，均24fps。没有重画图案或用字体替代官方字图。

| 正式资源 | 官方身份及注册 |
| --- | --- |
| `Sprites/UI/frontend/map_skills{.png,_frames.tres}` | SelectPLace505的showBuySkill492，三个状态原显示列表；统一256×160透明格，原点(128,80)，地图根坐标(81.8,103.95) |
| `Sprites/UI/hud/combo_caption.png` | Batter567/shape566；224×80画布，注册原点(16,16)，节点逆偏移保留原图坐标 |
| `Sprites/UI/hud/combo_digits{.png,_frames.tres}` | num0..9=564/560/556/552/548/544/539/535/531/527，每数字5帧一次播放后保持末帧；统一320×320格、原点(160,160) |

`UIs/Battle/combo_hud.tscn`位于官方(694.95,234.95)，数字步长50。Batter的起点Number经ANumber的int参数截断，运行时一/二/三位分别为x=4/−44/−95，y=28。原SWF、XML和导出清单只保留在忽略的验证目录；可重跑命令及实机证据见Validation。

## 造一英雄受击闪光（2026-10-31）

官方Role_v7（散列见OfficialReference）的HeroBeHurt/sprite9共8源帧、24fps。经既有`export_official_combat_effects.py --group hero_hurt --hero wukong|tangseng`导出到`Sprites/Effects/hero_hurt/{wukong,tangseng}/`；悟空保留原PNG，唐僧按BaseHero.addBeAttackEffect的`my.ColorMatrix.adjustColor(0,0,0,160)`矩阵调整RGB，alpha与注册点不变。SpriteFrames单次播放，实体结束/回池隐藏。FFDec共用画布100×122，源全范围x=-48.1..51.55、y=-60.95..60.95，精灵offset=(1.9,0.05)，节点放在colipse原点(0.4,-0.05)，不按帧居中。身体/武器图集、伤害几何与动作库不重导。此动画在悟空无双霸体受击时也要播放；唐僧无双仍保留官方受击硬直。

设置滑条抓手是本项目新建的10×18白色/橙色SVG，非官方提取图；使用现有笔刷面板、字体与菜单交互色。样式和Theme位于MainPack/Themes，最终应用状态见Validation。

## 敌人注册与判定修复的重导顺序

源文件散列先经`audit_official_enemy_fidelity.py`核对。旧导入器只负责源素材初始装配，已有人工调整的动画不能无差别覆盖。确需重导时，先暂存并审阅差异，再执行以下作者流程；完整命令及结果集中在Validation。

- 海怪/地面怪从原始动作manifest导出后，用`prepare_grounded_enemy_art.py`读取海怪注册manifest并以`--extra-manifest`加入地面怪manifest；它只修正虾兵、龟、蟹、猴子、大猩猩的已核实注册，保留稳定SpriteFrames UID。`apply_grounded_enemy_art.gd`只绑定图集和偏移，保留原生方法轨道。
- 用`prepare_enemy_fidelity_geometry.py`读取Monster2完整XML和海怪注册manifest生成四冰柱计划，再由`repair_enemy_fidelity.gd`原生保存45帧四形状及复位轨道；不能重新使用旧union框。
- `repair_monster_health_bars.gd`检查全部13个小怪；原始敌人草稿生成后运行`register_enemy_perception.gd`，经共用作者工具装配Detection/Engage/Patrol及物种专属范围。已有Perception不重写几何；跨工程作者化后可加`-- --refresh-bindings`，仅原生重存外部UID绑定。各海怪/首领注册工具已接入同一装配；旧Python原始草稿不能作为最终资源直接运行。
- `apply_enemy_detection_ranges.gd`只在明确重导时按源码报告的`sight_range/attack_range`写入24个首次发现矩形；宽度不再从Enemy表读取。运行时场景是唯一空间来源，实际命中轨道仍独立。末尾补跑`validate_enemy_perception.gd`和`validate_enemy_ai_tools.gd`检查24场景与受影响作者工具。
- 最后完整`--headless --editor --import`，等导入扫描结束后再跑`validate_enemy_fidelity.gd`、`render_enemy_grounding.gd`和真实烟测。PNG紧凑重排后若沿用旧导入纹理尺寸会出现破碎图，不能据该缓存预览再裁源素材。

动作修正不移动物理根：虾兵待机+8px；死亡保留前三帧，再在7帧内过渡到最终修正：虾+8、龟壳+22、猴+8、大猩猩−8、蟹−16。原版抬脚/腾空/翻转保留，不能按每帧非透明边缘强行贴地。

## 角色、关卡与 HUD

### 隐藏龙宫（官方9-1）

| 正式资源 | 官方来源与复现入口 |
| --- | --- |
| `Scenes/Levels/zm1_11_hidden_dragon_palace/level_11.tscn`及同名贴图目录 | 同版GameSence 9-1；`export_official_remaining_level_layers.py`暂存，`import_official_dragon_level.gd`原生保存；天空共用官方海底图，音乐bg4 |
| 龙王身体、冰光、水波、水柱 | Monster3_v3的Monster26及两个Bullet类；初次导入经`export_official_final_actors.py --group dragon`→`audit_official_dragon_geometry.py`→`import_official_dragon_art.py`→`prepare_official_dragon_scenes.py`→`register_dragon_art.gd`。最新判定必须再经`prepare_enemy_attack_geometry.py --dragon-only`→`repair_dragon_hitbox_nodes.gd`→`repair_new_enemy_attack_geometry.gd`；保留父矩阵、ADD/clipDepth，七柱独立，水波/水柱按阶段排除留白。旧`--update-library/--update-effects`会重建基础库，不能跳过最新判定步骤；验证用新attack_geometry报告，不再用旧clipped_geometry单并集 |
| `Sprites/Characters/Heroes/Status/hero_frozen{.png,_frames.tres}` | Monster3_v3官方35帧冰冻ADD层，`export_official_frozen_effect.py`；位置(-90,-115)，末帧保持至解冻 |
| 水帘洞隐藏入口与`hidden_entrance_fade.tres` | 主程序PhysicsWorld/MainGame的水下矩形与一秒淡出；`register_dragon_entrance.gd`，淡出期间动作/重力保留，暂停冻结 |

### 牛魔谷（官方4-1）

| 正式资源 | 官方来源与复现入口 |
| --- | --- |
| `Scenes/Levels/zm1_10_bull_demon_valley/level_10.tscn` 与同名贴图目录 | GameSence 4-1 的两阶段、3条官方出生流、3道独立区域门、前景246、地形31125、天空16；`import_official_bull_level.gd -- <all_levels.json>`。原版最终形态无传送门，流程由完整死亡/奖励结束自动结算 |
| 牛魔王三形态、牛妖、心脏/双手/火柱/背景 | Monster2_v4 的Monster22/23/24及Monster23Child，Monster_v1 的Monster25；`export_official_final_actors.py --group bull/guard` → `import_official_bull_actors.py` → `register_bull_actor_resources.gd`。吞噬五次源帧、心脏84px紫色Glow和各部分独立生命周期均保留 |
| `map_niumogu_current.png` 与19帧地图动画 | OtherMat_v9 `SelectPlace.s4_1`（wrapper445/current444，base441）；`export_official_map_hints.py --only niumogu` → `register_official_map_location.gd -- niumogu Location10 m_Niumogu 牛魔谷 19` |

牛魔谷资源重导后，再用`repair_bull_map_layout.gd`恢复s4_1原点(283.2,385.05)和current的1:1比例；不要沿用旧160px提示缩放。`import_official_bull_actors.py`的Fire读取755第25～36帧，原生动画使用六个独立形状。已有人工装配只更新`repair_bull_fire_geometry.gd`，不整场覆盖。人形hit4另经`prepare_enemy_attack_geometry.py --bull-only` → `repair_new_enemy_attack_geometry.gd`保存六阶段有效矩形；原始manifest保留官方union范围以便追溯，不能重导后直接覆盖已审范围。素材本身未重导，来源身份与UID保持。

### 牛魔谷精良装备（官方4-1）

| 正式资源 | 官方来源与复现入口 |
| --- | --- |
| `Sprites/Items/equipment/{equipment,ground}_fine_{pilgrim_clothes,pilgrim_staff,cassock,pine_staff}.png` | OtherMat_v9的SymbolClass对应jlxzf/jlxzg/jljs/jlsmz；`export_official_equipment.py --only fine_pilgrim_clothes fine_pilgrim_staff fine_cassock fine_pine_staff` → `--headless --editor --script res://EditorScripts/register_bull_equipment.gd`。四件分别保留透明背包/地面图，悟空与唐僧归属不混用 |
| 悟空精良行者服、行者棍动作绑定 | AllEquipment明确使用身体/武器selector3，与优秀品质共用现有完整`wukong_body_excellent.png`、`wukong_weapon_excellent.png`，不复制相同图集；按各件EquipmentId绑定，完整动作、双向组合、真实分页穿卸和关内拾取均通过 |

### 龙宫装备与法宝

| 正式资源 | 官方来源与复现入口 |
| --- | --- |
| 玄武甲、青龙刀、如意金箍棒、白虎杖、麒麟袍、朱雀戒、七星守护、九环禅杖及两件法宝的透明背包/地面图 | OtherMat_v9 的对应 SymbolClass；`export_official_equipment.py --only <对应英文资源名>` → `register_dragon_equipment.gd`。身份对应关系由导出器集中维护 |
| `wukong_body_black_tortoise.png`、`wukong_weapon_azure_dragon.png`、`wukong_weapon_ruyi.png` | Role_v7 身体selector5、武器selector6/5，各166格；使用已有`export_official_wukong_equipment.py`与`import_official_wukong_equipment.gd`作者化，`register_dragon_equipment.gd`分别绑定EquipmentId29/30/31，身体与武器仍为两层 |
| 金钢琢/东海旗幡的正式数据与背包表现 | AllEquipment的jgz/dhqf均为zbfb；已接入获取、四槽穿戴/属性与存档；该版无已核实主动施放入口。金钢琢由最终形态直接赠送；东海旗幡在首次龙王出生消费资格后保证地面掉落，后续普通/高级池按Monster26脚本 |

### 第九关鲨之涡流（官方 3-3）

| 正式资源 | 官方来源与复现入口 |
| --- | --- |
| `Scenes/Levels/zm1_09_shark_whirlpool/level_9.tscn`、同名贴图目录 | GameSence 3-3：前景/地形、16条生成流、5个重叠战斗区域、9道独立门；`export_official_remaining_level_layers.py` → `import_official_sea_level.gd -- <all_levels.json> 3-3 9 zm1_09_shark_whirlpool shark_whirlpool` |
| 鲨魔王、召唤鲨鱼、鲨波/水牢/旋风/斩击 | Monster2_v4 的 Monster18/19 及五组嵌套效果；`export_official_sea_monsters.py --only 18 19` → `import_official_sea_monsters.py` → `register_shark_actor_resources.gd`。鲨魔王高低生命动作、一次性召唤、效果所有权与召唤失败出口由本关实体分支负责 |
| `Sprites/UI/frontend/map_shazhiwoliu_*` 与外置地点动画 | OtherMat_v9元件478的三状态及477的48帧旋转；走export_official_coastal_map.py与apply_official_coastal_map.gd。SelectPlace父(766.55,500.15)、Y缩放0.5322113必须烘入图片，恢复源字图比例；不得再套普通居中缩小或19帧提示。旧hover_registered图已删除 |

### 第九关鲨之涡流（官方 3-3）装备

| 正式资源 | 官方来源与复现入口 |
| --- | --- |
| `Sprites/Items/equipment/{equipment,ground}_blood_sea_{demon_armor,evil_staff}.png` | OtherMat_v9 图元90/89、88/87；`export_official_equipment.py --only blood_sea_demon_armor blood_sea_evil_staff` → `register_shark_equipment.gd`。血海魔甲归悟空，血海邪皇归唐僧，不跨角色复用动作 |
| `Sprites/Characters/Heroes/wukong/wukong_body_blood_sea.png` | Role_v7 官方 selector6；`export_official_wukong_equipment.py --kind body --selector 6` → `import_official_wukong_equipment.gd`。六列200px格、166姿态，含完整基础动作和战斗引用帧 |

### 第八关东海（官方3-2）

| 正式资源 | 官方来源与复现入口 |
| --- | --- |
| `Scenes/Levels/zm1_08_east_sea/level_8.tscn`、同名关卡贴图目录 | GameSence 3-2：前景239、地形122（导出31122）、海域天空17，五阶段24流、七矩形含四平台；`export_official_remaining_level_layers.py` → `import_official_sea_level.gd -- <all_levels.json> 3-2 8 zm1_08_east_sea east_sea` |
| 鲛魔王、龙波、背击反馈与灯笼鱼 | Monster2_v4的Monster10/20及Boss5Bullet1(sprite1194)、Monster10BeAttack(sprite1165)，共280源帧；`export_official_sea_monsters.py --only 10 20` → `import_official_sea_monsters.py` → `register_east_sea_resources.gd`。鲛魔王图集以region/margin压紧存储，保留统一虚拟画布；死亡复用同一官方BossDead |
| `Sprites/Items/equipment/{equipment,ground}_blood_sea_shell.png` | OtherMat_v9图元38/37；`export_official_equipment.py --only blood_sea_shell` → `register_east_sea_resources.gd`，通用饰品无悟空穿戴动作层 |
| `Sprites/UI/frontend/map_donghai_*`与外置地点动画 | OtherMat_v9的SelectPlace.s3_2：父(612.7,418.85)、三状态原始矩阵、元件467的19帧；`export_official_coastal_map.py` → `apply_official_coastal_map.gd`。普通画面透明，点击mask来自形状453经本状态变换后的区域；hover/current共用注册画布，不套居中缩小 |

### 第七关浅岸（官方 3-1）

| 正式资源 | 官方来源与复现入口 |
| --- | --- |
| `Scenes/Levels/zm1_07_shore/level_7.tscn`、同名贴图目录 | GameSence 3-1：前景233原点(-30,97)、地形121原点(-11.6,195.2)、天空位图17；`export_official_remaining_level_layers.py` → `import_official_sea_level.gd`。16流、5重叠区域、9矩形含6平台，GGF SeaTheme |
| 海怪 `Monsters/Sea/<物种>/` 场景、动画与贴图 | Monster2_v4 的Monster14/16/17；`export_official_sea_monsters.py` → `import_official_sea_monsters.py` → `register_sea_actor_resources.gd`。272帧保留父级矩阵和统一注册点；虾兵弹体来自sprite39，潮汐妖不生成未调用的飞刀；鲨鱼召唤物也归Sea，保留独立垂直运动 |
| `Sprites/Items/equipment/{equipment,ground}_excellent_{pilgrim_clothes,pilgrim_staff,cassock,pine_staff}.png` | OtherMat_v9 背包/地面图元50/27、58/32、46/23、53/62；`export_official_equipment.py` → `register_sea_equipment.gd`，使用独立透明前景 |
| `Sprites/Characters/Heroes/wukong/wukong_{body,weapon}_excellent.png` | Role_v7 身体/武器selector3，每层166姿态，含51个完整基础动作帧；既有装备导出/导入流程，独立层和统一原点 |
| `Sprites/UI/frontend/map_qianan_*`与外置地点动画 | OtherMat_v9的SelectPlace.s3_1：父(460,373.15)、三状态原始矩阵、元件459的19帧；与东海同一沿海导出/作者流程，保留完整20px悬停光晕和透明输入形状，图集1:1显示 |

### 第六关迦楼罗之穴（官方 2-3）

| 正式资源 | 官方来源与复现入口 |
| --- | --- |
| `Scenes/Levels/zm1_06_garuda_cave/level_6.tscn`、同名关卡贴图目录 | GameSence 2-3：前景197、地形118（导出31118）、22条出生流、5个重叠区域、7个矩形和39个尖刺；`export_official_remaining_level_layers.py` → `import_official_garuda_level.gd`。天空复用第二章位图18，音乐为GGF CaveTheme |
| 彭魔王、飞行魔及彭魔王火焰/死亡旋涡场景与动作库 | Monster2_v4 的 Monster9/15、Boss4Bullet1，Monster_v1 的 BossDead；`export_official_garuda_cave.py` → `prepare_official_garuda_level.py` → `import_garuda_actor_assets.py`。335源帧保留注册点；火焰使用三条基础矩形排除中间空气，0.6秒淡入期间保持判定 |
| `Sprites/Items/equipment/{equipment,ground}_heavenly_fiend_{moon_halberd,robe}.png` | OtherMat_v9：月戟126/125、羽袍124/123；`export_official_equipment.py` → `register_garuda_equipment.gd`，使用完整透明前景 |
| `Sprites/Characters/Heroes/wukong/wukong_weapon_heavenly_fiend.png` | Role_v7 武器selector4，166姿态含全部51个基础动作帧；`export_official_wukong_equipment.py` → `import_official_wukong_equipment.gd`。与身体独立，保持既有原点和遮挡裁切 |
| `Sprites/UI/frontend/map_jialuoluo_current.png` 及外置地点动画 | OtherMat_v9 元件420/423，19帧原始光晕；`export_official_map_hints.py --only jialuoluo` → `register_official_map_location.gd -- jialuoluo Location6 m_Jialuoluo 迦楼罗之穴 19` |

### 第五关七星洞（官方 2-2）

以下资源来自同一 v25928 的 `GameSence` 2-2 与 `Monster_v1`/`Monster2_v4`，不使用旧工程替代素材。

| 正式资源 | 官方来源与复现入口 |
| --- | --- |
| `Scenes/Levels/zm1_05_qixing_cave/level_5.tscn`、`Sprites/Levels/zm1_05_qixing_cave/` | `later_levels.json` 的20条出生流、五阶段/九门登记及35尖刺；源八个几何体中的三段坡合成一个连续六点坡体，其余五个矩形保留。背景sprite190、FloorBg位图18，shape115/116/117为三层地形；`export_official_qixing_level_art.py` → `import_official_qixing_level.gd`（参数为出生清单与地形layout.json） |
| `Sprites/Characters/Enemies/Monsters/Ground/{cave_brute,knife_demon}/`、对应实体与动画目录 | Monster_v1 的Monster7、Monster2_v4 的Monster13；`export_official_qixing_cave.py`、`import_official_qixing_art.gd`、`create_qixing_actor_scenes.gd`；飞刀使用独立场景与节点池 |
| `Sprites/Characters/Enemies/Bosses/zm1_05_qixing_cave/`、对应实体与动画目录 | Monster2_v4 的 Monster11；身体三段招式、毒雾独立 SpriteFrames，来源同上 |
| `Sprites/Characters/Heroes/Status/`、悟空场景的 `m_StatusEffects` | 官方poison_head/poison_up；`register_qixing_poison.gd` 登记状态播放器及外置动画，不展开已有升级子场景 |
| `Sprites/Items/equipment/{equipment,ground}_heavenly_fiend_necklace.png` | OtherMat_v9 的背包符号43、透明地面符号20；`export_official_equipment.py` → `register_qixing_equipment.gd`，两种用途均用透明前景 |
| `Sprites/UI/frontend/map_qixingdong_hover_animation.tres` | 原地图七星洞普通/悬停图；`register_official_map_location.gd -- qixingdong Location5 m_Qixingdong 七星洞 19` 外置悬停动画 |

### 后续关卡共享音乐与作者化暂存

| 暂存资源 | 官方来源与用途 |
| --- | --- |
| `.godot/validation/remaining_levels/art/layers/bg2.mp3` → `Audios/Music/cave_theme.mp3` | Music.swf 的 bg2；蝙蝠窟、七星洞、迦楼罗之穴；通过 `register_official_chapter_music.gd` 配置循环 |
| `.godot/validation/remaining_levels/art/layers/bg3.mp3` → `Audios/Music/sea_theme.mp3` | Music.swf 的 bg3；浅岸、东海、鲨之涡流 |
| `.godot/validation/remaining_levels/art/layers/bg4.mp3` → `Audios/Music/bull_demon_valley_theme.mp3` | Music.swf 的 bg4；牛魔谷、隐藏龙宫 |
| `remaining_levels/garuda_art/` | 第六关的源帧、探针、帧事件和装备selector清单；忽略目录保留复现证据，正式资源见上表 |

### 第四关蝙蝠窟与家传装备

以下均来自同一 v25928 及其加载器指定资源包，未采用旧项目的石像召唤规则。

| 正式资源 | 官方来源与复现入口 |
| --- | --- |
| `Scenes/Levels/zm1_04_bat_cave/level_4.tscn`、`Sprites/Levels/zm1_04_bat_cave/` | GameSence 2-1；FloorBg 第2帧位图18、背景185、地形107及35个113尖刺；`export_official_bat_cave.py` → `import_official_bat_cave_level.gd`。尖刺20帧打包为190×256图集与外置SpriteFrames |
| `Sprites/Characters/Enemies/Monsters/Sky/bat/`、`Entitys/Enemies/Monsters/Sky/bat/` | Monster_v1 / Monster8；原生攻击时点和矩形判定，经 `import_official_bat_cave_actors.gd` 保存 |
| `Sprites/Characters/Enemies/Bosses/zm1_04_bat_cave/`、对应实体与动画目录 | Monster2_v4 / Monster21；固定石像与死亡动画，同上；出生流和出口归关卡 |
| `Sprites/Items/equipment/{equipment,ground}_heirloom_{bracelet,sword,clothes}.png` | OtherMat_v9 对应家传手镯、宝剑、衣裳；透明背包/地面图，经 `register_heirloom_equipment.gd` 登记 |
| `Sprites/Characters/Heroes/wukong/wukong_weapon_heirloom.png` | Role_v7 selector7，包含完整基础动作、普攻、技能与待机；`export_official_wukong_equipment.py`，原点、帧映射和遮挡沿用双层规则 |

### 第二、第三关及守关装备

以下从 OfficialReference 登记的同版官方包提取，未使用旧工程重做数值或重绘 BOSS：

| 正式资源 | 官方来源与复现入口 |
| --- | --- |
| `Scenes/Levels/zm1_02_shuiliandong/level_2.tscn`、`Sprites/Levels/zm1_02_shuiliandong/` | 主程序 GameSence 的 1-2 显示列表；`extract_official_levels.py` 记录出生点、边界和碰撞面，`import_official_levels.gd` 原生建场景 |
| `Scenes/Levels/zm1_03_taohuayuan/level_3.tscn`、`Sprites/Levels/zm1_03_taohuayuan/` | GameSence 的 1-3；同上，不覆盖已编辑场景 |
| `Entitys/Animations/shuiliandong_river_animation.tres` | 水波元件 94 的完整 325 帧，24fps；保留原生位置关键帧与矩形裁切 |
| `Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/` | Monster_v1 的 export.monster.Monster4；完整 209 帧，身体与攻击特效独立图集 |
| `Sprites/Characters/Enemies/Bosses/zm1_03_tamarin_king/` | Monster_v1 的 export.monster.Monster6；完整 176 帧，身体与落雷独立图集 |
| `Entitys/Enemies/Bosses/zm1_02_macaque_king/macaque_wind_blade_effect.tscn` | Monster_v1 的 Boss2Bullet1，50 帧；`export_official_bosses.py` → `import_official_bosses.gd` → `import_official_boss_scenes.gd`，保留原点、判定与新攻击编号时点 |
| `Sprites/Items/equipment/*earth_fiend*` | OtherMat_v9 的灵戒 44/21、猿甲 114/113、权杖 112/111（背包/掉落元件）；`export_official_equipment.py`、`import_official_boss_equipment.gd`；使用透明装备前景，避免烘焙格底 |

BOSS 导出校验恢复后的 Monster_v1 SHA-256 为 `5bd88da4211870f2ca2354b7cec8d1790747869641c11fe553910b10fe2a264a`。源中间件不入正式资源目录，原生动画按源 24fps 换成秒，运行物理不绑定渲染帧率。代码、场景与表格分类见 ProjectTour。

### 既有角色与界面

| 旧路径 | 新路径 | 规格 / 说明 |
| --- | --- | --- |
| `Art/Monster/Monster1/Wait.png` | `Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_idle.png` | 43×63，1 帧 |
| `Art/Monster/Monster1/Walk.png` | `Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_run.png` | 64×88 × 4 帧 |
| `Art/Monster/Monster1/Hit.png` | `Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_attack.png` | 91×83 × 6 帧 |
| `Art/Monster/Monster1/Hurt.png` | `Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_hurt.png` | 68×86 × 2 帧 |
| `Art/Monster/Monster1/Death.png` | `Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_death.png` | 91×88 × 6 帧 |
| `Art/Monster/Monster2/Wait.png` | `Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_idle.png` | 74×114 × 5 帧 |
| `Art/Monster/Monster2/Walk.png` | `Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_run.png` | 116×105 × 4 帧 |
| `Art/Monster/Monster2/Hit.png` | `Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_attack.png` | 130×134 × 4 帧 |
| `Art/Monster/Monster2/Hurt.png` | `Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_hurt.png` | 89×87 × 1 帧 |
| `Art/Monster/Monster2/Death.png` | `Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_death.png` | 124×108 × 5 帧 |
| `Art/Level/Level_1/19_1.png` | `Sprites/Levels/zm1_01_huaguoshan/level_1_bg_end.png` | 1440×690；旧 `BackGround/End/end2` 背景端块 |
| `Art/Level/Level_1/48.png` | `Sprites/Levels/zm1_01_huaguoshan/level_1_front.png` | 4957×633；旧 `BackGround/front` 前景视差层 |
| `Art/Level/Level_1/183.png` | `Sprites/Levels/zm1_01_huaguoshan/level_1_floor.png` | 原 4812×170；旧 `BackGround/floor3/floor2` 地板视差层。**已裁至 4700×170**：右侧 112px 圆角收尾（x≥4716 顶面下坠、右下透明）会露出背景，相机右界随之定在 4700 |
| `Art/Level/Gogo/1..67.png` | `Sprites/UI/gogo/gogo_sheet.png` + `gogo_animations.tres` | 67 帧 213×92 打包为 8×9 图集；旧 `Role_information` 的 `Gogo`（`AnimatedSprite2D`，speed 25、循环） |
| `Art/HeroPicture/RoleProperiesBox/408.png` | `Sprites/UI/hud/hud_status_panel.png` | 227×87，状态面板底图 |
| `Art/HeroPicture/RoleProperiesBox/345.png` | `Sprites/UI/hud/hud_hp_fill.png` | 144×12，血条填充 |
| `Art/HeroPicture/RoleProperiesBox/712.png` | `Sprites/UI/hud/hud_menu_panel.png` | 169×133，左下菜单面板；五个旧入口保留 |
| `Art/HeroPicture/RoleProperiesBox/718.png` | `Sprites/UI/hud/hud_ws_frame.png` | 260×22，无双条黑色长底板，原样恢复；由 `import_frontend_assets.py` 登记 |
| `Art/HeroPicture/RoleProperiesBox/swk.png` | `Sprites/UI/hud/hud_head_wukong.png` | 83×76，悟空头像 |
| `Art/HeroPicture/RoleProperiesBox/748.png` | `Sprites/UI/hud/hud_status_label.png` | 17×46，状态标签装饰 |
| `Art/HeroPicture/RoleDeath.png` | `Sprites/Effects/hero_death/hero_tombstone_atlas.png` | 原图片与UID保留，1832×1830、8×5；共用 `Entitys/Effects/hero_tombstone.tscn` 自有落地碰撞和78源帧动画；提取入口 `extract_shared_hero_tombstone.gd`，英雄只触发，不再由悟空身体轨道播放 |

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
| `damage_number_monster_physics.png` | `hurtnum/hurtnum_N`（源目录名仍沿用旧迁入名） | 30×30 | 怪物普通受伤；物理与魔法共用官方 `hurtnum` |
| `damage_number_monster_physics_crit.png` | `bnum/bnum_N` | 42×42 | 怪物暴击受伤；伤害类型不再分物理/魔法 |
| `damage_number_monster_magic.png` | `magic/magic_N` | 30×30 | 旧迁入样式，当前战斗飘字不再使用 |
| `damage_number_monster_magic_crit.png` | `magiccrit/magic_N` | 32×32 | 旧迁入样式，当前战斗飘字不再使用 |
| `damage_number_hero_physics.png` | `monster/physics/physics_N`（旧放在 monster 目录） | 30×30 | 旧迁入样式，当前英雄受伤统一使用官方 `pnum` |
| `damage_number_hero_magic.png` | `pnum/pnum_N`（文件名保留迁入兼容名） | 30×30 | 英雄受伤；物理与魔法共用官方 `pnum` |
| `damage_number_real.png` | `real/real_N` | 30×30 | 旧迁入样式，当前通用伤害不按真实伤害另选图集 |
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

2026-11-06：存档删除确认沿用旧 `Scene/show_text/choose_scene.tscn` 的实际资产。原存档删除的调用链为 `Script/ArchiveInterface/read_archive_interface.gd` → `Global.add_choose_text` → 该场景；只迁入以下原始字节，不复制旧脚本、倒计时或全局信号。

| 旧项目相对路径 | 正式 TheGame 相对路径 | 用途 |
| --- | --- | --- |
| `Art/BackPack/117.png` | `Sprites/UI/frontend/delete_confirm_panel.png` | 原棕色确认面板，保持原图比例放大约1.5倍 |
| `Art/BackPack/124.png`、`126.png` | `Sprites/UI/frontend/delete_confirm_cancel{,_hover}.png` | 原取消按钮及悬停字图 |
| `Art/BackPack/119.png`、`121.png` | `Sprites/UI/frontend/delete_confirm_confirm{,_hover}.png` | 原确定按钮及悬停字图 |
| `Font/华康宋体W3.ttc` | `MainPack/Fonts/df_song_w3.ttc` | 旧确认窗使用的字体 |

上述六文件逐字节相同，补入既有 `Tools/LegacyMigration/import_frontend_assets.py` 的映射；新Godot导入元数据独立生成。删除入口复用现有 `menu_close{,_hover}.png`，按当前用户要求缩为24×24、默认压暗，只有可读档案显示。

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
| `Art/Monster/Monster3/Wait.png` | `Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_idle.png` |
| `Art/Monster/Monster3/Walk.png` | `Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_run.png` |
| `Art/Monster/Monster3/Hit.png` | `Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_attack_1.png` |
| `Art/Monster/Monster3/Hurt.png` | `Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_hurt.png` |
| `Art/Monster/Monster3/Death.png` | `Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_death.png` |
| `Art/HeroPicture/RoleProperiesBox/128.png` | `Sprites/UI/hud/boss_health_fill.png` |
| `Art/HeroPicture/RoleProperiesBox/124.png` | `Sprites/UI/hud/boss_health_under.png` |

平台来源：旧 `Art/Level/level_2/177.png` → `Sprites/Levels/zm1_01_huaguoshan/level_1_platform.png`。

正式花果山地形和出口现改用主程序 v25928 原文件：shape 60 → `huaguoshan_ground.png`，shape 56 → `huaguoshan_platform.png`；sprite 85 → `Sprites/Effects/level_exit/level_exit_00..19.png`。导出工具为 `export_official_level_art.py`，原点写入 layout.json；Godot 用 `repair_terrain_registration.gd` 保存原生资源。旧 11 帧出口已删除，旧导入器不再生成它。`level_1_floor.png` 仍供独立调试场地使用。

同一导出工具现覆盖前三关全部可见地形与前景：161/163/168 为三关前景，88/97 为水帘洞平台/地面，101/103 为桃花源地面/石墙。花果山 `level_1_front.png` 已替换为官方原生输出，保持路径和 UID；源矩阵及图像左上来自 `extract_official_levels.py`，`align_official_level_art.gd` 负责原生保存。隐藏的碰撞、转向、副本等调试元件不导入为可见图像。

前三关地面的原生栅格输出在图内x=3779各有92个透明像素，形成透背景的单像素竖缝。`export_official_level_art.py` 在导出地面后修补两侧均为不透明地面的长单列裂缝；只插值该列，不改变尺寸、原点或其他像素。蝙蝠窟/七星洞相机右界按各自地面图右端向下取整为4696/4681，远景视差跟随相机行程计算。

结算配乐取旧工程 `Music/level/13_Game_Victory.mp3`、`12_over.mp3`，原样迁为 `Audios/Music/settlement_victory.mp3`、`settlement_defeat.mp3`；SHA-256 分别为 `c7bf53f7ac759efdd04defed2f1a8e61b11d5c57745220ded7bed6cd4ba4ab35`、`88a4ef5d27ff38bd9c9964e9b3864eb8940ef261bd9fcb110ce39880f10abc0d`。两曲由 GGF Music 播放，Godot 导入开启循环；ProcedureLevel 拥有关卡和结算音乐的唯一请求。

## 官方升级、掉落球与无双演出

| 目标 | 官方资源编号 | 复现入口 |
| --- | --- | --- |
| `Sprites/Effects/level_up/` | Role_v7 sprite 240，28 帧 | `export_official_combat_effects.py` |
| `Sprites/Effects/loot/musou_01..19.png` | Monster_v1 sprite 5，黄球 | 同上 |
| `Sprites/Effects/loot/soul_*.png` | Monster_v1 sprite 9，紫球 | 同上 |
| `Sprites/Effects/loot/mana_*.png` | Monster_v1 sprite 7，蓝球 | 同上 |
| `Sprites/Effects/loot/health_01..19.png` | Monster_v1 sprite 3，实际为红色回血球；AS类名auraGreen与颜色不符 | 同上，`--group loot`；逐字节复制官方19帧，不改色/裁剪/原点 |
| `Sprites/UI/hud/musou_activation/{wukong,tangseng,shared}/` | Role_v7 WsEffect 59：背景52、悟空55、唐僧58；按BaseHero.doWsEffect隐藏另一角色，共用24帧时钟 | `export_official_musou_activation.py --hero wukong|tangseng` |

升级动画保留原始 PNG；后半段位图外的蓝色标记会被 FFDec 边缘采样带入，导出器通过 `AtlasTexture.region` 裁掉这一行，并用 `margin` 补回透明留白、启用 `filter_clip`。28 帧仍为 122×136，播放位置和时长不变；只重导升级动画可加 `--group level_up`。

两个导出器均用 Java/FFDec 26.3，在临时目录校验散列和渲染，再复制 PNG 与外置 SpriteFrames。无双开场在 `(470,295)` 输出 940×590、24 帧；悟空末姿势保持时，火焰子动画继续播放。调用参数见各脚本 `--help`。

满值提示已改为 OtherMat_v9 的 `340`：`battle_menu_ws_glow_frames.tres` 引用 19 张 78×78 原版透明帧，24 fps 循环；不再使用旧 WSBar 图集与裁剪 shader。原始注册范围为 `(-44,-44)..(34,34)`；最亮帧边缘保留原版低 alpha 柔光。

`Tools/LegacyMigration/export_official_battle_menu.py` 校验同一 OtherMat_v9 散列后导出：

| 原版元件 | 正式资源（`Sprites/UI/hud/`） | 规格 |
| --- | --- | --- |
| `328` 的 11 帧 | `battle_skill_icons.png`、`battle_skill_icon_frames.tres` | 45×45；第 1 帧为灰色空槽，后 10 帧为两位角色的已学习图标；每技能引用空槽/彩色两帧 |
| `330`、`333` | `battle_menu_ws_under.png`、`battle_menu_ws_fill.png` | 221×27 底条、221×12 填充；填充偏移 (0,9) |
| 位图 `335` | `battle_menu_ws_idle.png` | 68×68 无双未满图 |
| `340` | `battle_menu_ws_glow_00..18.png`、`battle_menu_ws_glow_frames.tres` | 19 帧、24 fps、78×78 |

按用户要求，HUD 左侧保留造三的法宝、宠物、背包、设置、技能五入口及 `hud_menu_panel.png` 布局。只替换无双和五个主动技能图标；法宝、宠物仍为未实现入口，素材为后续版本保留。无双条另有 `hud_ws_frame.png` 黑色长底板，不能把细灰底条当成完整底板删除；技能栏随这套菜单排在条上方。

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

`UIs/Inventory/hero_preview.tscn` 每次打开从当前英雄的 `HeroAppearanceResource.PreviewAppearance` 借用专用背包身体/武器图集，动画使用该英雄的PreviewAnimations。悟空为backpack_v2的62帧短循环，唐僧为1帧静态姿势；源24fps，保持原帧序。金光取旧背包BackPackLight的第3..8格（192×192，15fps，竖直翻转），两角色共用并独立播放。动画不带攻击或碰撞轨道，源及导出映射见下方官方背包节。

## 装备与地面恢复品

`Tools/LegacyMigration/import_equipment_assets.py` 逐字节复制旧项目 `Art/ItemIcon/peach.png` 为 `Sprites/Items/consumables/pickup_small_peach.png` 与 `pickup_large_peach.png`（大桃子通过显示比例区分），`Bble.png` 为 `pickup_mana_bottle.png`。

旧工程的粗糙装备图标缺失，曾暂用普通图；现已删除这组临时替代图。目前登记的十五件装备图片均直接导出自官方 [OtherMat_v9.swf](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/OtherMat_v9.swf)，恢复后 SHA-256 见 OfficialReference。原背包位图带黑色底板；按用户要求，背包和地面现在均使用对应的透明前景位图，保留原始 alpha，未重绘或调色。

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

所有装备外观的身份与文件绑定可用 `python Tools/LegacyMigration/audit_equipment_appearances.py --repo . --output Godot/GodotProject/.godot/validation/equipment_appearance_audit.json` 检查。它从 `Items.xlsx` 和 `__enums__.xlsx` 读取稳定 ID，再核对 `item_appearances.tres` 的唯一条目、背包/地面两张贴图、文件存在性和可见 alpha；新关卡增加装备时必须先通过此检查。悟空动作图集仍需另外按官方 selector 做逐帧审计，不能用图标检查替代。

地面图保留原始透明画布。`GroundItemNode.Show` 每次显示时读取非透明区域，将其底边按显示缩放对齐场景碰撞体底边；通过 Sprite2D.Offset 定位，Position 留给领取时的上移淡出动画。更换图标或池复用时重新计算，不按整张图片底边定位，也不改碰撞尺寸。此规则仅用于静态掉落图标，角色动作仍保留统一原点。

粗糙与普通的原背包位图不同，但同款装备的两个透明前景位图在原文件中内容相同；目前保留原样，品质由名称和详情区分，不人为改色。

## 悟空独立身体与武器

战斗使用官方 `Role_v7.swf` 的 `export.hero.Role1`（sprite 575）动作；背包现用下节backpack_v2独立图集。早期六张战斗图均为 1200×5800、6×29、每格 200×200；0..119 保留既有普攻、技能与完整待机映射，120..170 为完整基础动作，171..173 为空：

| 新文件（`Sprites/Characters/Heroes/wukong/`） | 源选择帧 | 用途 |
| --- | --- | --- |
| `wukong_body_bare.png` | 衣服元件 143/152/160/186 的第 1 帧 | 未穿防具的身体 |
| `wukong_body_pilgrim.png` | 同上第 2 帧 | 粗糙/普通行者服共用外观 |
| `wukong_body_earth_fiend.png` | 同上第 4 帧，AllEquipment 的 dsyj.showid=4 | 地煞猿甲的完整动作外观 |
| `wukong_weapon_default.png` | 武器元件 177 第 1 帧 | 默认木棍 |
| `wukong_weapon_pilgrim.png` | 同上第 2 帧 | 粗糙/普通行者棍共用外观 |
| `wukong_weapon_heirloom.png` | 同上第 7 帧，AllEquipment 的 jcbj.showid=7 | 家传宝剑的完整动作外观 |

沿用旧 `Scene/Hero/Role_1/Role1.tscn` 的两层结构：完整身体在后，武器在前；源绘制深度位于武器前方的头部、手臂等作为导出遮罩，裁掉武器被遮挡的部分。遮罩只在导出时使用，不创建第三个运行时层，也不导出武器和防具的组合图集。各套防具须保持相同的前景遮挡轮廓，便于武器独立替换。

`HeroAppearanceResource` 的防具/武器字典按 EquipmentId 绑定具体纹理；战斗用 `wukong_appearance.tres`，它引用 `wukong_inventory_appearance.tres` 供背包换装。猿甲经 `export_official_wukong_equipment.py` → `import_official_wukong_equipment.gd` 导出，`register_official_wukong_equipment.gd` 原生保存绑定；沿用现有帧号及原点，不覆盖人工动画。338/371/416/568 等技能能量继续使用独立特效，不能混入身体 SVG，否则模糊滤镜会成为矩形。唐僧现已可玩并有完整独立战斗及背包换装资源。

基础动作使用 walk 440（16帧）、run 452（8帧）、jump 533（1帧）、jump_2 539（10帧）、fall 543（1帧）、hurt 553（15帧）。每套装备都必须按自身选择帧导出这51帧，不能拿相近装备的动作补齐。装备导出器读取正式动画库的帧映射，自动覆盖完整基础动作并计算图集行数；`validate_wukong_wardrobe_motion.gd` 将六张图逐帧与各自源图层比较，再验证九种独立穿戴组合的双向播放。2026-10-26 修复了猿甲与宝剑新增51格被误填为行者服/棍的问题，既有0..119格逐字节不变。

每关新增悟空装备的验收沿用 `audit_official_wukong_wardrobe.py` → `validate_wukong_wardrobe_motion.gd`：前者读源表、稳定枚举、官方AllEquipment选择帧与原生绑定，不维护另一份装备清单；后者核对整张图的有效格、实际导入纹理和所有原生动作帧键。粗糙/普通行者服及行者棍的共享选择帧2为官方规则，允许共用完整图集。不能只检查图集非空、文件路径或站立截图。现有四张早期图集的少数技能格保留800px源画布的栅格结果，新装备使用200px画布；两者只在抗锯齿边缘有微差，验证采用各自源画布的精确结果，不人为重绘。

所有动作保留 Role1 父级中的完整矩阵，统一 SVG viewBox `(-100,-128,200,200)`，原 Flash 原点位于格内 `(100,128)`。例如待机平移 `(1.45,0.8)`、慢走 `(-6.85,0.85)`、跑步 `(3.75,0.2)`、受击 `(-17.95,2.6)`；漏掉这些矩阵会造成动作切换跳位。不能按各帧可见边界重新居中，也不能在运行时移动角色根节点补偿。

帧号为零起：基础动作 0..24；五段普攻依次为 25..32、33..40、41..51、52..71、72..86。身体与武器共用帧号，源动作暂时隐藏武器的 3 帧保留透明，不能因此移除武器槽并破坏同步。技能 122 个源帧按四张图共同的像素签名复用 20 格，映射记录在技能导出 manifest 的 `frame_cells`；透明阶段共用一格，原始时间轴仍完整保留，不预留整片空槽。

固定红金色金箍棒形状的残影只来自元件 246，导出时在所有动作中过滤；保留 203/254/282 的弧光和旋转特效。第三、四段仍有原版动态效果，第五段去掉棒影后没有独立可见特效。身体、真实武器及源时序不变。特效图 `Sprites/Effects/wukong/wukong_attack_effects.png` 为 2400×1200、6×4、每格 400×300，19 个可见源帧、末行余 5 格。五段共 62 个源帧，43 个无特效帧用 null 保留时序而不占图集。特效 viewBox `(-200,-178,400,300)`，Flash 原点对应本地 `(0,28)`；按源深度裁去被后续身体/武器遮挡的部分，不混入装备层。

判定使用两种真实武器 alpha≥0.25 与当帧剑气 alpha≥0.1 的像素并集，生成覆盖武器和剑气残留的矩形；水平与长轴旋转两种候选取面积较小者，外围 3px 余量、最小厚度 14px。防具和武器外观不改变命中范围。原版探针提供有效时间窗，保留剑气均在窗口内；第四段前三帧只有弧光也保持判定。修改美术后经导出工具更新几何，再用逐帧截图及真实武器近端、剑气区域和范围外目标检查对齐。

复现入口为 `Tools/LegacyMigration/export_official_wukong.py`（核对源 SHA、保留矩阵和逐帧深度）与 `EditorScripts/import_official_wukong.gd`（原生 SVG 栅格化、紧凑打包、拟合判定并输出 manifest）。必须显式指定工程内的特效输出路径；完整命令统一放在 [Validation](Validation.md)。随后 `Tools/LegacyMigration/import_official_wukong_attacks.py` 按 manifest 定向替换五段攻击、特效序列和矩形判定轨道，移除无消费者的 skill_*，一次性映射基础帧号并更新两处场景网格；其余基础动作只补齐判定旋转归零轨道，手调时序及已有轨道保持原样。重复执行不会再次映射。

当前 13 个玩法动作（8 个基础动作、5 段攻击）和 RESET 使用同一两层战斗资源。`wait` 子元件529的普通待机、立棍和张望属于同一条100帧时间轴，只用于战斗 `idle1`，不再截取两帧拼成 `idle2` 或额外随机触发。`import_official_wukong_idle.gd` 只追加缺少姿势，`Tools/LegacyMigration/import_official_wukong_idle.py` 只更新战斗待机及实体网格，不能覆盖背包库。已有攻击、技能及手调时序保持不变。复现时在基础与技能导入后执行，命令见Validation。悟空五项造一技能另挂 `skills` 动画库；造三攻击没有混入这些资源。

### 官方背包专用预览（2026-11-07）

来源[backpack_v2.swf](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/backpack_v2.swf)：下载SHA-256为 `e4360195f803d7fe0b36f70c2ea88ade5e7fe9de7ffacde3d2fa7e0452c4a0f9`，恢复后为 `47215d42043801243e7beae00ca4601d6040b343be8bd8a29a9c4aa4e8df17ea`。根285的personmc284位置为(252,221.4)，悟空252/唐僧283分别位于(.65,-.2)、(0,-.05)。完整父矩阵烘焙进统一200px画布，原先用战斗姿势推导的唐僧(2,6)补偿已删除；共用根(149,128)、缩放0.95。

悟空252为62帧站立/低头短循环，没有立棍；唐僧283只有1个外层帧。BackPack.changeWKShowEquip/changeTSShowEquip控制的嵌套帧是装备选择，不可当动画播放。悟空衣装selector159/167/175/200、武器192；唐僧衣装276/头部282、武器269，身份沿用正式外观EquipmentId。

`Tools/LegacyMigration/export_official_inventory_preview.py`复用selector冻结、按深度分层、原生Godot栅格器和compact_atlases，共导出26张去重紧凑图：`Sprites/Characters/Heroes/{hero}/preview/{hero}_preview_{body,weapon}_*.png`，均6列200px、当前1行。62帧时序通过映射保留，不把重复姿势存满图集。两个 `{hero}_inventory_appearance.tres`复用现有HeroAppearanceResource字典；主资源分别引用专用预览外观。两个preview_library只播放各自官方展示及共用光圈。战斗导入器不再写背包动画或网格。完整命令和两角色实际换装/自然播放证据集中于Validation。

首领死亡继续复用 `Entitys/Enemies/Bosses/zm1_06_garuda_cave/boss_death_effect.tscn` 及已导出的官方12帧BossDead，不复制七份图片或场景。猕猴王、禺狨王、狮驼王及蝙蝠洞石像新增的只是角色死亡库中原生可见性/第25帧方法轨道和此场景引用；其余三个普通魔王已有资源。精英与形态特例的实际帧脚本依据见OfficialReference。

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

`Sprites/UI/skills/` 的 37 张图片由 `Tools/LegacyMigration/export_official_skill_ui.py` 直接从上述官方 `OtherMat_v9.swf` 导出，没有截图、重绘或迁入旧逻辑。脚本检查恢复后的 SHA-256，使用 FFDec 原生 PNG 渲染保留位图图案和透明度；简单按钮按原始 Up/Over 显示列表导出，升级字图保留原始像素。

| 原元件 | 新资源 | 状态 / 尺寸 |
| --- | --- | --- |
| bitmap 375 / 586 | `skills_background.jpg` / `skills_soul_badge.png` | 940×590 背景 / 74×66 灵魂徽章 |
| sprite 585 | `skills_table_active.png` / `skills_table_passive.png` | 第 1/2 帧，746×429 表格 |
| sprite 382 / 386 | `skills_wukong_*` / `skills_sanzang_*` | 第 3 帧灰色未选中、第 2 帧彩色选中；两页均可查看 |
| button 295 / 657 / 653 | `skills_back_*` / `skills_active_*` / `skills_passive_*` | 普通与悬停 |
| bitmap 569 / button 571 | `skills_upgrade_normal/hover/pressed.png` | 原始48×26彩色文字；共用48×31画布，悬停向下5px、按下回原位，与官方三态一致 |
| sprite 594/599/604/609/614 | 五项悟空 `skill_*_unlearned/learned.png` | 第 1 帧灰色含“学习”、第 2 帧彩色，65×65 |
| sprite 624/629/634/639/644 | 五项唐僧 `skill_*_unlearned/learned.png` | 同上；629 对应玄冰阵 |

原点取 `BuySkill`、`SkillControl` 和子元件矩阵：角色页签 `(89.35,6.85)` / `(228.35,5.85)`、返回 `(854,15.05)`、表格左上 `(100,85)`。十行主动与五行被动直接位于技能主场景，以 GridContainer 对齐，不使用行子场景；唐僧长说明在行内 MarginContainer + Label 控制留白和换行。预填文本和图标仅供编辑预览，运行时读取正式技能表覆盖。悟空主动学习和共用被动购买已接入Command/System；被动满级按原版隐藏升级按钮。

## 唐僧技能效果视觉复核（2026-11-04）

源为同版Role_v7，导出入口`Tools/LegacyMigration/export_official_tangseng_effects.py`。正式效果为`Sprites/Effects/tangseng/`下的紧凑PNG与SpriteFrames，源原点和动画仍由角色目录的原生场景/AnimationLibrary保存。

| 正式资源 | 官方符号与保留内容 |
| --- | --- |
| `tangseng_dark_ice_atlas.png` | 604/Role2Bullet3共46帧；593子冰柱按真实出生帧烘焙并在30帧stop。按用户2026-11-05要求排除第5–11帧595/597/599/601/603蓝色过渡形状；冰柱与滤镜保留。紧凑图集以margin恢复原画布。 |
| `tangseng_cure_role_atlas.png` | 703外层的702内轴34帧，父偏移(-4.5,-24.5)、blendMode8对应原生加法材质。颜色强度转入alpha，黑底透明化；fix_alpha_border=false，深浅背景实渲染相对源帧最大差1/255。起手播放，末帧治疗。 |
| `tangseng_water_demon_ball_atlas.png` | 身体965中的951/roundMc共20帧，保留父级Glow/Blur与0.18756纵缩放；位置与帧序由身体原生轨道驱动，素材源注册offset=(-0.85,-11.3)。正式Sprite叠加hit4/body父矩阵(12.45,-8.05)，抵消EffectRoot的y=-28后offset=(11.6,8.65)。margin恢复原196×83画布。 |
| `tangseng_water_demon_fall_atlas.png` | 657/hit4FallDown共25帧，保持(-3.45,117.4)栅格注册；裁切存储并共享空白帧，margin恢复原267×432画布，末帧时长不变；二次输入在当前球位置上方300px创建独立效果。 |

之前误用的580/Role2Bullet4光柱无官方showSkillL调用，已撤出正式池、场景、动画与素材。旧逐帧PNG目录及静态甘露占位均已清除，原始导出留在忽略的验证目录。命令和真实技能截图验证见Validation。

## 悟空造一技能动作与声音

`Tools/LegacyMigration/export_official_wukong_skills.py` 检查同一 Role_v7 散列，冻结衣服与武器选择器并保留 Role1 父级矩阵；`EditorScripts/import_official_wukong_skills.gd` 栅格化两层与遮罩；`Tools/LegacyMigration/import_official_wukong_skills.py` 紧凑打包并生成外置值轨道。原有 87 格逐像素保持相同，基础动作库不重生成。

| 动画库名称（`skills/`） | 源 body 元件 | 源帧数 / 24 fps | Role1 父级变换 |
| --- | --- | --- | --- |
| rising_dragon 升龙斩 | 384 | 15 / 0.625 秒 | 平移 (2.15,-1.6) |
| flame_thrust 火炎突击 | 569 | 15 / 0.625 秒 | 平移 (-15.2,-3.8) |
| blazing_flash 烈焰闪 | 344 | 12 / 0.5 秒 | 平移 (0.75,0.25) |
| flame_storm 烈焰风暴 | 417 | 10 / 0.416667 秒 | 平移 (-17.55,2.3) |
| fire_demon_slash 火魔斩 | 574 | 70 / 2.916667 秒 | X 镜像、平移 (4.75,26) |

身体/武器图集沿用历史格内原点 `(100,128)`，场景四个对应层用 `(0,-28)` 抵消画布平移，使物理根对应官方角色 `x/y`；独立特效不加28，详见 OfficialReference。源元件 338 是烈焰闪的模糊轮廓，Godot SVG 滤镜会画成矩形色块，因此与 371/416/568 的弧光改用 FFDec 原生 PNG。`FlashSpriteBounds.java` 读取 `Timeline.getDisplayRectWithFilters` 的精确导出矩形后再应用父级变换，不能按可见像素重新居中或猜测滤镜留白。41 个可见帧存于 `wukong_skill_inline_effects.png`（800×400 格，4 列）；无光效帧保留 null。

烈焰闪不把模糊光效当作超宽命中区：保留原透明探针 205 的几何，只在验证/拟合时转为不透明。其他有 stick 的技能按源窗口及可见能量拟合两种武器共同的矩形，沿用 3px 余量、最小厚度 14px；透明能量阶段关闭判定。火魔斩主体没有 stick，70 帧内的大段透明是原文件事实，伤害与表现由独立特效承担。

技能特效图在 `Sprites/Effects/wukong/skills/`、外置库在 `Entitys/Animations/Heroes/Wukong/`、角色与独立效果在 `Entitys/Heroes/Wukong/`。源元件为烈焰闪拖尾 66（10 帧）、火魔起手 129（40 帧）、九连斩 112（44 帧）、下冲 133（2 帧）、落地斩 123（13 帧）。起手只有视觉，现并入悟空的 TopLevel AnimatedSprite2D，保留出招位置与完成隐藏；不再生成空 HitBox、独立场景或节点池条目。其他四项仍由 SkillEffectNode 独立播放，后三项拥有独立 HitBox；Visual 同时放图像与判定，原生方法轨道更新攻击包、播放 GGF 声音及请求回池。图片、原点、源帧与场景内的镜像和偏移保留，详见 ProjectTour。

七段声音取自[官方 Music.swf](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/Music.swf)，恢复后 SHA-256 `5cad2f1ef3ce592c2e9e44c631748e042d8cacae2c7571e57b1a0546998a7578`。`export_official_skill_audio.py` 原样导出 MP3：6/8/11/1 分别为升龙斩/火炎突击/烈焰闪/烈焰风暴，13/14/15 为火魔起手/九连斩/落地。正式路径 `Audios/SFX/wukong/skills/`，SoundId 7..13，全部登记到 GGF SFX，无循环。

2026-10-26 补齐同一 Role_v7 的走、跑、起跳、二段跳、下落与受伤51个源帧，六套身体/武器图集共306层帧，沿用原格尺寸、两层和穿戴映射。三类初期地面怪从 v25928 `Monster1`（SHA-256 `5bd88da4211870f2ca2354b7cec8d1790747869641c11fe553910b10fe2a264a`）重新导入完整动作：猴子69、妖猴88、大猩猩92帧，含后两者的 `after_hurt`。资源位于各自角色贴图目录的 `<角色>_official_body.png` / `<角色>_official_frames.tres`，外置动画库保持原路径。导出入口 `export_official_ground_monsters.py`，原生导入入口 `import_official_ground_monsters.gd`；后者共享相同形状并压缩重复值键，不改声音和动作方法轨道。
# 2026-11-06 新增表现资源

| 来源 | 正式资源 | 用途 |
| --- | --- | --- |
| Role_v7 `MonsterBeHurt1/2`（symbol27/18） | `Sprites/Effects/monster_hit/{wukong,tangseng}/` | 怪物受击官方8帧闪光；唐僧目录为受官方 hue+160 后的原图变体。 |
| 原项目地图令牌图 | `MapForm/m_Settings` 复用 `map_return_title.png` | 设置入口保留原版令牌，只替换文字为“游戏设置”，不重画按钮。 |

2026-11-06按用户纠正，技能恢复1帧静止普通态、15帧单次展开停帧、19帧可购黄光；前轮“普通态循环”是AI误读，不是用户取舍。保存新增 `map_save_game_hover.png`，直接渲染官方SimpleButton502的悬停shape501，普通/按下保留既有499图片，同画布138×93/原点(75,54.45)。既有export_official_map_combo_ui增加 `--map-only`，避免改写连击素材。设置文字为20号海报字形、金色黑白描边，三个标题按源字图中心x约75.5视觉对齐；令牌保持一次亮度过渡。主菜单继续收在现有设置窗内。
