# 素材来源与复现

## 装备、技能图标与近景浮台的原位图（2026-11-19）

| 正式素材 | 来源及保真登记 |
| --- | --- |
| 38 套装备的 `equipment_*` 与 `ground_*` | 已登记的 OtherMat_v9 内嵌位图及 1 倍正式导出，原身份与文件映射见下文。普通品质按各自原 RGBA 保留背包白边，粗糙品质保留比掉落图多出的每边 4 逻辑像素留白。每张 2 倍图按原像素覆盖重建，同一图形的锚点、轮廓和颜色层保留。掉落复用 `item_appearances.tres` 原字段；只对装备设置 0.35 密度补偿。 |
| `battle_skill_icons.png`、`skill_*_learned.png` 及 `skill_*_unlearned.png` | OtherMat_v9 原缩略素材及已核对的原生滤镜。战斗图集 11 格分开处理，一格是通用未学，另 10 格是各技能已学；源格从 45 到 90，SpriteFrames 的状态、时序与映射保持。学习页 20 图从 65 到 130，继续使用原图内的效果与原生按钮绘制框。 |
| `huaguoshan_platform.png` / `shuiliandong_platform.png` | 主程序 v25928 的 shape 56 → bitmap 55（263×75）、shape 88 → bitmap 87（179×51），均用本关自己的 1 倍正式导出。平台边缘重建为 526×150 和 358×102；分别有 3、5 处注册，现用 `Sprite2D.scale=0.5` 绘制于原位置。导出器仍可取原密度源；花果山编辑器装配复用原 Sprite2D 设置密度。 |
| 花果山起点路牌 | sprite 161 → shape 160 内的 bitmap 142（500×500，含透明区）。其原矩阵 20 TWIPS/像素，平移 (-45678, -3679)。路牌字样和木纹已烘焙；限制在不透明内部的试样保留原合成和 alpha，原位对照无足够提升，不替换。 |

三类均沿用现有导出的正式原图约束局部像素覆盖；没有改为新绘，也没有接受外来高精度图。原图、散列、脚本和结果见[验证](Validation.md#装备技能图标与近景浮台2026-11-19)。历史 A1a 的 2 倍是原位图参照；本轮只把浮台边缘局部改善合入正式路径，未改其精度来源结论。

## HUD 彩色原件、长图无损分页与图鉴原裁框（2026-11-19）

| 产物 | 来源与装配 |
| --- | --- |
| 5 个 HUD 彩色按钮普通及悬停 | 保留 OtherMat 3 旧图元件 134/136（设置）、141/143（背包）、147/149（技能）、153/155（法宝）、159/161（宠物）。原 47×37 PNG 做颜色和透明覆盖约束重建，存 94×74；Godot `TextureButton` 用原逻辑矩形按比例采样，仍保留原右下空隙、题签及交互。原件与 [原位生成数据](../Godot/GodotProject/.godot/validation/art_hud_buttons_trial_20261119/coverage.json) 留本机验证。 |
| 气血／法力／经验、Boss 与无双条 | OtherMatv3570 原 144×12 三色填充，Boss OtherMat 3 / 主程序两层各 340×29，无双 OtherMat_v9 的 329/332 位图 221×27/221×12；按颜色与 alpha 面积重建为 2 倍。保留原引擎九宫格缩放；无双自然尺寸的两条仅按半缩放补回同样大小和原偏移。 |
| 7 套怪物/Boss、9 张关卡 PNG | 现正式 PNG 必要像素和 alpha 均原样复制，新页仅拆分。不删重复帧，不重新修剪锚点。全图合回 RGBA 逐像素等同，所有 `SpriteFrames` 原动作、速度、循环、margin 和帧时刻保持。原图/资源及页裁框见 [图集清单](../Godot/GodotProject/.godot/validation/art_texture_pages_20261119/atlas_manifest.json)、[地形清单](../Godot/GodotProject/.godot/validation/art_texture_pages_20261119/terrain_manifest.json)；复用原 Godot AtlasTexture/Region 及 Collection Res。 |
| 图鉴 `map_guide_portraits.png` | 真实 `level_guide_tooltip.tscn` 的 21 个 `AtlasTexture` 全裁框（含原透明留白、不重新取最小包围框），使用 `compact_registered_atlas` 的摆放，成图 1024×1452；原像素和 8px 采样邻边复制。资源里只换底图与图集原点。[原 PNG／裁框和新裁框](../Godot/GodotProject/.godot/validation/art_map_guide_atlas_20261119/guide_atlas_manifest.json) 对应每个 EntityId，原战斗资源留原用途。图鉴作者脚本重建控件时从当前图鉴读取正式肖像字典。 |

原件在 Git 和相应验证目录的 `before/`。屏幕裁切与 GPU 采样的结果见 [Validation](Validation.md#hud-边缘分页与地图图鉴减载2026-11-19)。

## 战斗 HUD、英雄状态与三套菜单外围（2026-11-19）

| 正式资源 | 原件、范围与复现 |
| --- | --- |
| 战斗 HUD 九 PNG | 既有战斗 UI 迁入记录及正式原图：OtherMat 3 元件 133/140/146/152/158 是五张 30×17 字图，95 是 169×133 墨底；OtherMat_v9 位图 297/303/335 是状态墨底、无双条墨底、灰无双。工作目录 `art_hud_caption_delivery_20261119` 的 `before/` 保留原件，复用飘字覆盖计算，对原黑白与透明覆盖重建 2 倍亚像素边缘。按原像素面积还原，预乘色和透明度最大差小于 0.26/255；4 倍样本字内采样不自然，未采用。彩色按钮、存档槽和标题分隔线未换。 |
| 唐僧水幻影 | Role_v7 唐僧分身 1050/1081/1131，混合源 SHA-256 `11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7`，1 倍复现 116 时刻可见 RGBA 全等。原矢量轮廓重导，位图细节受原件精度限制。已有 `compact_atlases` 共享等同画格生成 2400×800，保持六列；第 0～115 时刻逐一映射在 [安装清单](../Godot/GodotProject/.godot/validation/art_hero_status_delivery_20261118/install_manifest.json)。图集中 400 像素格经缩放为原 200 逻辑格，原库只变画格索引。 |
| 中毒、冰冻和墓碑 | Monster2/Frozen 官方现有帧注册：骷髅 sprite 204 以原矢量重导，原缩小烟雾按原层合成，图出 82×106，定位和源范围保持；身上毒雾 sprite 220 的 41 时刻、冰冻 sprite 114 的 35 时刻与现用原精度可见 RGBA 全等，保留。墓碑用已迁入的旧 `RoleDeath.png`；浏览候选 `OtherMat_v9` sprite 273 图形和帧内容不匹配，[对照](../Godot/GodotProject/.godot/validation/art_hero_status_20261117/tombstone_source_mismatch.png) 留档。 |
| 标题、技能和结算背景 | 复用已有 [外围合成清单](../Godot/GodotProject/.godot/validation/art_static_ui_outskirts_20261117/composition.json)；原 `Art/MainGame/Bg1.png` 941×591、OtherMat 技能 JPEG 940×590（Godot 解码）、OtherMatv3570 bitmap 231 结算 940×590 均按原位置 +234 回贴，中心严格同像素，只用已有 ECPA 候选的两侧环境。标题原透明边有同色黑底，技能保留原 JPG 并新增正式 `skills_backdrop.png`；原按钮、正文、动画无改。 |

Godot 运行实际调用场景仍为 `battle_form.tscn`、`start_form.tscn`、`skill_form.tscn`、`settlement_form.tscn`，以及原两英雄混合源。除技能新 PNG 及其原生导入外，原图路径、UID 和消费入口保持。完整进度见 [ArtProgress](ArtProgress.md)。

## 设置主题与图标的原件和实际消费者（2026-11-18）

基线 `72df176f`。SVG、PNG、UID、消费者路径都保留；正式只改三份 `.import`。原导入参数记在Git与本批 `before/`。

| 资源 | 来源与处理 |
| --- | --- |
| `TheGame/MainPack/Themes/Styles/settings_slider_grabber.svg`、`settings_slider_grabber_active.svg` | 项目现有两张10×18 SVG：同一圆角矩形、`#222`描边、白色或`#ff7700`。`settings_theme.tres` 绑定 `HSlider` 的 `grabber`/`grabber_highlight`，两条音量控件同用。Godot `svg` 导入器生成 `DPITexture`，`base_scale=1.0`、沿原透明边处理；原 UID `811jgm0gwwvb` / `dtryy3l5lfah3`。 |
| `icon.svg` | Godot 现有128×128矢量；Windows `application/icon` 经 UID `c47qf54rnfq8f` 使用。导入 `svg/scale=2.0`，256档在实际 EXE 中与原 SVG 2倍栅格和透明边处理后的 RGBA 全等；其余5档保留同一标识。 |
| `TheGame/Sprites/bhpic.png` | `application/config/icon` 的 UID `0qvr220neg1y`。文件与旧工程 `Art/MainGame/bhpic.png` 字节相同，SHA256 `ec85df2fea6e35f297d90b665afa877fa261a7a074c20bf1d236256632502b87`。旧 `bhico.ico` 内含相同512×512图像，另两份 `GameIcon*.ico` 是较小的254×223图标。当前512原件用于窗口，保留。 |
| `addons/asset_bundle/images/asset_bundle.svg` | 插件 `AssetBundle.gd` 通过 `@icon` 使用的64×64编辑器图标；编辑器比例与主题颜色转换开启，现有 `.editor.ctex` 变体验证通过；插件只读原件保留。 |

[滑块同尺寸对照](../Godot/GodotProject/.godot/validation/art_theme_icons_20261118/slider_1080p_comparison.png) · [EXE 提取图标与工程实际资源](../Godot/GodotProject/.godot/validation/art_theme_icons_20261118/icon_review.png)。

## 关卡整类：原矩阵精度与外围来源（2026-11-18）

[37原图清册](../Godot/GodotProject/.godot/validation/art_terrain_delivery_20261118/manifest.json)记录运行路径、消费者、Git原PNG与导入文件散列、SWF依赖、原图尺寸及分片逻辑裁框。原件保留Git `898f494f` 和本批 `before/`。官方主程序内层SWF SHA256 `f8e3e1f5b545bc2423a02f5c6155f87215e01c14d21554d77681f32a7defb75b`；34张在官方素材/原补缝规则下1倍可见RGBA完全复现，另3张对应旧工程PNG。旧183裁图可见区少量RGB不同、alpha一致，继续保留当前裁图；旧19_1与官方bitmap19同为1440×690且同构图，解码颜色略有差，不以官方替换当前中心。旧177原样保留。

| 原路径文件/层 | 本次原源与处理 |
| --- | --- |
| 花果山、水帘洞、桃花源前景 | sprites 161/163/168，分别恢复19/16/53处原缩小位图填充；整体注册和色彩层序沿用。 |
| 蝙蝠窟、七星洞、迦楼罗前景 | sprites 185/190/197，分别恢复6/11/4处填充。 |
| 东海、鲨之漩涡前景 | sprites 239/243，各1处填充恢复。 |
| 蝙蝠窟地面、七星洞地面和上下平台 | shapes 107/115/116/117，分别恢复2/1/6/3处位图104填充。复用已有shape包装与FFDec重导；原物理与图层原点不参与放大。 |
| 五天空 | 旧19_1及bitmap 18/18/17/16。蝙蝠窟与七星洞的1倍源RGBA相同，复用同一外围结果另保留各自原路径与UID。四份外围来自之前存留的本地ECPA生成，按原图掩码仅合成右侧608，对原画4,968,000像素逐个核对RGBA。没有重画原画或新增第二套平台用地形图。 |
| 水帘洞水与波浪、蝙蝠窟刺锥 | bitmap89/91与sprite113；水流两轨各325键、原24fps。刺锥20帧38×64、5列×4行、24fps，保持原像素与序列。 |

原源2倍导出会使4张前景重复铺贴位置出现细竖线。按源矩阵相接列及1倍原图连续区域校正602个高分辨率像素，见[接缝坐标](../Godot/GodotProject/.godot/validation/art_terrain_delivery_20261118/tile_joins.json)。压入分片后逐像素复原整幅已注册母版；分片的补边仅用于线性采样，不进入逻辑画布。

## 在用飘字的原覆盖重建与接入（2026-11-18）

像素来源仍是下文确认的OtherMat_v9和迁入PNG。用原始透明外形固定每格位置，把样本拆成透明、黑描边、白描边、原色扫描行渐变的覆盖比例。橙伤、紫伤、回血和旧英雄物理红色可相互佐证轮廓；对齐时仅分析紫字的1px偏移，输出恢复原坐标。各格独立恢复，不让相邻格贡献像素，不换字体或绘制新图。旧原件通过散列对应当前Git基线，另保存在`.godot/validation/art_numbers_coverage_20261118/before/`，复算脚本为`recover.py`，逐字校验和候选散列在`coverage_metrics.json`。

| 正式原路径（`TheGame/Sprites/Number/`） | 源尺寸→接入尺寸 | 逻辑每格 |
| --- | --- | --- |
| `damage_number_monster_physics.png`、`damage_number_hero_magic.png` | 300×30→1800×180 | 30×30，十格 |
| `damage_number_monster_physics_crit.png` | 420×42→2520×252 | 42×42，十格 |
| `recovery_number_health.png` | 300×30→600×60 | 30×30，十格 |
| `recovery_number_mana.png` | 330×34→660×68 | 33×34，十格 |
| `damage_number_miss.png` | 55×24→110×48 | 55×24，单图 |

[伤害十格逐字对照](../Godot/GodotProject/.godot/validation/art_numbers_coverage_20261118/all_damage_comparison.png)和[恢复/MISS逐字对照](../Godot/GodotProject/.godot/validation/art_numbers_coverage_20261118/all_recovery_comparison.png)保留原色与原逻辑尺寸。重建保住每个原像素的覆盖和颜色能量，源格以内的边缘形状由邻域约束，和原栅格相比允许抗锯齿变化；这不是官方原矢量。四张正式未引用的旧伤害样式不变。

## 伤害、恢复数字与MISS整类溯源（2026-11-18）

现用图以`TheGame/Entitys/Effects/floating_text.tscn`的6个贴图引用为准。旧`Art/AllNumber/`原件和OtherMat_v9（恢复后SHA-256 `fa7681aad99fb06f50228530b7f70f6334a1d7052a7bb6ef2ce3c3d81867fd5e`）的SymbolClass、FFDec原位图与正式文件逐字核对。原件和精确alpha框、ID、散列在`.godot/validation/art_numbers_resume_20261118/before/`及`category_catalog.json`。

| 当前样式 | 原源与原格 | 可见像素结论 |
| --- | --- | --- |
| 怪物普伤、英雄受击、回血 | `hurtnum` 122/110/107/104/102/100/98/96/94/92；`pnum` 75～66；`bunum` 86～77，均30×30 | 30字逐字可见RGBA全等；回血也与旧`cure`全等。 |
| 红色暴击 | `bnum` 136～127，42×42 | 10字逐字可见RGBA全等。 |
| 蓝色回魔 | 现用旧`CureMp/CureMp_0..9.png`，33×34 | 与旧原件全等。OtherMat_v9 `bulnum` 121/109/106/103/101/99/97/95/93/91原格30×30，在项目是旧`damage_number_monster_magic.png`，像素全等，但与现用蓝色回魔渐变、轮廓及注册均不同。 |
| 闪避 | `miss` ID 76，55×24 | 与现用PNG有97个像素差，最大2/255；保留迁入原件。 |
| 旧英雄物理、旧怪物魔法、旧怪物魔法暴击、旧真实伤害 | `Tools/LegacyMigration/gen_damage_numbers.gd`对应四组旧PNG，30×30或32×32 | 四套均逐字可见RGBA全等；正式飘字场景未引用，原件继续保存。 |

连击`num0..9`各是有位图的sprite，底图98×98，字形与此批完全不同，见[连击与暴击形状](../Godot/GodotProject/.godot/validation/art_numbers_resume_20261118/source_combo_vs_crit.png)。试用同包42×42的`bnum`给其他颜色找高密度轮廓，即使按可见框重新匹配，彩色笔画掩膜交并比也只有约0.68～0.92；不借用异形或异色内容。第一轮只作原格约束试验，未替换十张正式图片；第二轮沿原源覆盖重建并接入见本页顶部。

## 装备图标查源与地面恢复品2倍接入（2026-11-18）

沿用`OtherMat_v9.swf`恢复版（SHA-256 `fa7681aad99fb06f50228530b7f70f6334a1d7052a7bb6ef2ce3c3d81867fd5e`）和现有装备导出工具。正式以`1acfbd79`为基线；原PNG、登记资源和逐项源ID及散列在`.godot/validation/art_items_resume_20261118/before/`与`category_catalog.json`。旧候选只在原件和导出像素匹配后复用。

| 正式资源 | 原源 | 当前结果 |
| --- | --- | --- |
| `pickup_small_peach.png`、`pickup_large_peach.png` | shape274（BigHP中的同一桃子）。项目原小恢复品本来就与大恢复品同桃子图；源SmallHP中的胡萝卜不用于当前小桃子。 | 48×45→96×90，两种显示尺寸继续由缩放0.35/0.45区分。只导同形状，不带源容器阴影。 |
| `pickup_mana_bottle.png` | shape280（蓝瓶） | 48×44→96×88，缩放0.35；原色及右侧透明留白保持。 |
| `equipment_*.png`、`ground_*.png`共76张 | 对应背包/地面均为位图；透明前景源72个52×54、4个52×55（背包/地面分用途计数）。 | 当前正式图按原导出约定采用地面透明前景，粗糙/普通背包图另有既有图标处理，可见RGBA全部匹配。保留原件。 |

恢复品两shape无位图/滤镜依赖，原舞台对齐后重导，不逐图裁紧。原密度导出与旧栅格仍有抗锯齿差异，可见RGBA平均绝对差为1.02～1.08/255；2倍归一alpha总量差0.53%～0.72%，密度折回同一源画布的透明度质心偏差小于0.031像素。`GroundItemNode.Show`仍以可见底边贴地；末行采样变化产生至多0.45逻辑像素的透明画布纵向调整。原显示大小、命中和领取时间轨保持。复现用上次`art_items_numbers_20261117`中的`prepare_consumables.py`、`render_consumables.py`形状导出证据，本轮`prepare.py`核对后仅接入三张恢复品。

复用同一原图散列的粗糙行者棍位图实验：SwinIR 2倍回采样可见RGB MAE为19.77/255（普通插值1.12/255），约束后回采样误差可降至0.82/255，仍无证据说明新增细纹来自原图。另核对普通/精良行者服及地煞戒指，同尺寸插值只增加采样，未见更高精度的同内容来源。对照留在本轮忽略验证目录，未替换装备位图。

## UI按钮、头像、背包与无双演出（2026-11-18）

以013d5f85当前正式UI为准，旧候选只在原件或源证据散列匹配后复用。12张原矢量图从旧候选逐字节复用，一律替换到原路径，现有导入UID保持；没有以旧场景覆盖当前项目。逐图来源、当前散列和受限项在`.godot/validation/art_ui_resume_20261118/installation.json`及`category_catalog_final.json`。

| 来源 | 接入 | 保真验证 |
| --- | --- | --- |
| OtherMat_v9 button295/657/653、Commonv3720 shape369 | 技能返回/主动/被动各两态，装备操作菜单底板 | 七图原密度可见RGBA全等。按钮和底板现有控件保持原尺寸及点击范围。 |
| backpack_v2 shape7/9、OtherMat_v9 shape357 | 背包经验填充/框、HUD状态字样 | 三图原矢量依赖不含位图；原密度可见MAE分别为0、0.0722、4.2308/255，均保留轮廓、填充、原屏幕位置及P2镜像。 |
| 已登记的同造型OtherMatv3570 sprite94 | `hud_head_wukong`与`hud_head_tangseng`从83×76至166×152 | 同一固定原点(41.75,36.30)，原矢量/配色及有效逻辑尺寸保持。与旧栅格原密度有采样差，MAE分别3.2345/9.7150；2倍逻辑质心各轴偏差最大0.2494，不宣称逐像素复现。 |
| Role_v7 WsEffect59→52、55/58 | 共用原密度背景和原暗幕；两英雄原48时刻，40个可见人物帧2倍重导 | 复用既有24fps舞台和固定(470,295)；原密度拆分重合最大差2/255。沿上一批高密度整数核和相位补偿输出原人物光晕，原图案/时间轴不改。8个空人物时刻和重复时刻保留。 |

无双共享`musou_activation_light.png`为2048×3938，一张图存19个原密度姿态，仍播放24个时刻。光束用原sprite52画布外的内容扩至1408×590；原940×590可见像素原位贴回，外围32列内按预乘alpha衔接；21个时刻原最右端全透明的单列沿光束延续，已有可见像素不变。字样/火焰等嵌入位图保留，原shape28纯黑207/255暗幕随界面覆盖。48个人物文件用透明裁片及各自的AtlasTexture原画布注册，非逐帧居中。原源SWF不改；派生XML、工具和逐帧衔接记录在本批忽略目录。旧地图1408×590只扩外围，地图中心、技能底图等仍受原像素精度限制，本轮未以插值替换。

## 独立特效、掉落球及演出来源复用与滤镜采样（2026-11-17）

用户认可Boss批次并要求下一批，本轮处理Effects分类189PNG，保持已暂存Boss成果及用户索引。复用art_effects_batch_20261117的查源与原源候选，84项对应的正式原图、场景和库与当时before逐字节相同；没有整包恢复旧工作区。正式替换84图，其中83纯矢量、1混合；104原位图与1未确认墓碑不替换。完整逐图分类见ArtProgress，来源集合见本批category_catalog.json。

| 来源/原元件 | 当前接入 | 保持与限制 |
| --- | --- | --- |
| Monster_v1 sprites3/5/7/9 | health/musou/mana/soul四类各19帧，76张原路径PNG | 原红/黄/蓝/紫、共同画布、19时刻及24fps保持；类名auraGreen等不代表画面颜色。 |
| Role_v7 sprite66/129 | 烈焰闪残焰10时刻、火魔斩前奏40源时刻（8共用姿态） | 原高精度shape与滤镜重导，原时长与重复时刻不删；透明光晕不锐化。 |
| Role_v7 sprite611/608及既有唐僧水魔球1133作者入口 | 普攻飞弹、冰龙波飞弹、水魔球20时刻 | 原位置/画布与作者父矩阵保持；无额外美术内容或新运行加载层。 |
| Role_v7 211/426/272/308/333与原SVG遮罩 | 悟空普攻特效62时刻/19姿态 | 沿原身体/武器遮挡和变换，原源1倍复现；2倍矢量图形提升。 |
| Role_v7 344/384/417/569 | 悟空内联技能122时刻/41姿态 | 混合源，矢量部分提升；位图内容精度仍受限，只记部分。 |
| Monster_v1 sprite39 | 虾兵飞弹 | 复用既有描边密度修正版，原19×13逻辑画布、判定/飞行行为保持。 |

源Role_v7恢复SHA256 `11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7`、Monster_v1恢复SHA256 `5bd88da4211870f2ca2354b7cec8d1790747869641c11fe553910b10fe2a264a`，本轮重核一致。源SWF/XML不改，非生成式绘图、非插值素材放大。原源SVG/位图遮挡和共同矩阵仍保留；滤镜的整数采样只在本轮作者副本内处理。

旧掉落球2倍候选存在最大1.73逻辑px透明度质心偏移，未直接接入。核对FFDec26.3原BLURFILTER/GLOWFILTER/Filtering实现：原1倍先整数化核，偶数核逐次有半像素相位，高倍直接round(blur×zoom)会改变核及相位。沿已有地图光晕修正方法，在忽略目录复制两滤镜类，显式rasterDensity=2时使用原1倍整数核×2并补偿每次偶数核相位；density1执行原算法。原颜色/strength/passes/knockout/composite保留，既有全局工具安装和原SWF不改。四类76帧及相关Role滤镜帧的1倍与正式原件全等；2倍掉落球alpha归一0.996593～1.013667、质心偏差≤0.03975逻辑px。完整作者源码/编译类散列、原始引用链接与度量在source_sampling_verification.json和renderer_docs/renderer；没有逐帧移动可见框。

图集存储复用现有compact_registered_atlas显式frames/max_dimension参数和原生region/margin；本次所有图单边最大2739，不需要新PNG分页，首路径/PNG.import/UID保持。五个图集里的97独立姿态和79张直接PNG共176个可见纹理/姿态逐项核对，旧177验证口径含水魔球重复一时刻；所有333源时刻保持。理论RGBA8约36.79MiB（缓存稀疏原布局约93.78MiB），不表示Android真机驻留预算已验。原子资源/动作身份保持，98资源重复接入字节一致。

本批候选、原件、滤镜作者副本和过程图留`.godot/validation/art_effects_resume_20261117/`。正式84PNG、4份SpriteFrames、两悟空库和既有视觉节点均已接入；火魔斩前奏朝向只保留scale幅度，不改实体根和碰撞。升级、命中、冰块等104位图继续未完成，墓碑原源未确认；不自动跨入下一类别。

## Boss整类重新接入与分页来源（2026-11-17）

本批以67fb4f7为新基线，复用art_boss_batch_install_20261117中保留的FFDec26.3原源2倍导出，未重新绘制或插值角色。30项source_catalog的XML和11套已接入SWF散列复核一致，正式旧PNG/SpriteFrames/场景/库与当时导出前原件逐字节相同；既有1倍2058时刻注册证据有效。Monster_v1/Monster2_v4/Monster3_v3原包身份仍见OfficialReference，不修改只读原包。

| 来源组 | 本次接入 | 原始精度限制 |
| --- | --- | --- |
| stages_2_3/bosses | 猕猴王209、禺狨王176时刻主体，猕猴风刃50时刻 | 两主体分别含1/9位图依赖；风刃纯矢量。原父矩阵和共同画布保持。 |
| official_continuation/qixing、remaining_levels/garuda_art | 狮驼王148、鹏魔王223时刻 | 30/31位图依赖，矢量部分提升；各专属低精度效果保持原图。 |
| remaining_levels/east_sea_art、shark_art | 蛟魔王158、鲨魔王200时刻 | 6/14位图依赖，部分提升，不把源位图计作高清。 |
| remaining_levels/bull_art | 兽形139、人形438、牛心2时刻 | 兽形/牛心纯矢量；人形45位图依赖，部分提升。 |
| tangseng_capture_20261105/bull_art | 唐僧被抓130时刻，两独有姿态 | 原Role2选择器/遮挡保持；167姿态中其余共用主体，混合来源不勾完整高清。 |
| remaining_levels/dragon_art | 龙王315时刻 | 4位图依赖，冰块/雾等仍受源限制。 |

2倍可见RGBA逐姿态散列与缓存的原源导出一致。沿原线宽下限修正；牛魔王沿既有renderer2描边采样，不改源内容。此次仅改变存储：复用compact_registered_atlas显式frames/max_dimension/paginate参数，保留16像素隔离透明padding；6张新增页使用普通_2/_3后缀，原首PNG/import/UID不变。每帧region/margin恢复原逻辑画布，视觉层scale0.5及offset2倍抵消密度；根/碰撞/动画事件不动。两猴王子Effect净变换保持；52资源重复接入字节相同。

7个地图图鉴裁片继续引用对应主体页，按旧裁片在共同画布中的位置精确换算2倍，原姿态及宽高比保持，不生成另一套UI美术。来源裁片与正式新页逐像素一致；Godot整页透明边缘处理后的采样另经原生检查。18项低精度效果/石像原件保持，完整明细在ArtProgress。

本批来源、候选、旧原件、映射及验证留`.godot/validation/art_boss_batch_resume_20261117/`，包括source_verification.json、preparation_verification.json、density_manifest.json、portraits/mapping.json、functional_results.json。正式资源和6张分页已在TheGame，未重建文档审图图片目录；单边≤4096不等于Android预算/真机通过。

## 完整待机preview与地图原图恢复（2026-11-17）

按用户最新要求，背包从backpack_v2短展示/单姿态切换为Git历史已有的Role_v7完整wait，不重画新动作。复用已审当前战斗PNG的原源2倍像素，export_official_inventory_preview.py新增--animated-idle入口，只提取idle1身体/武器帧到原26个preview路径，独立外观字典与UID不改。保留原100/91时刻和24fps、完整共同格及逐帧遮挡；六列400源像素格=200逻辑格。悟空13身体/11武器姿态，唐僧10/8，去重不删除任何时刻。

每英雄两层采用同一固定舞台平移，悟空(-2,-2)、唐僧(2,10)源像素，使首姿态接回当前展示注册；所有时刻相同，不按可见框居中。26图逐格与经相同平移的高精度源完全相同、无内容裁损或插值。原动画/图集和本轮来源散列、source_sequences/source_cell_maps、候选及自然播放录像保存在art_preview_animated_idle_20261117；战斗源和玩法资源保持原字节。

地图最终去字图复用art_map_caption_install_20261116/before中的先前source-glyph-only版本，其字形遮罩来自原独立bitmap497/500/489。map_save_game/map_save_game_hover/map_skills只在字形遮罩内将RGB归黑，原alpha和遮罩外RGBA严格一致；完整黑色笔刷不挖空。map_return_title保持HEAD原件，三入口Label显示文字，不叠额外Ink层。保存hover有实际引用、普通/悬停不同，保留。旧import/UID保持，仅无消费者的新map_menu_caption_ink及import删除。误恢复烘焙文字的中间版本已纠正；修正版与当前Git基线备份在art_map_caption_background_fix_20261117。

电脑16:9、手机20:9/21:9及去字图、英雄实际动作的审查证据留在既有.godot/validation各批过程目录；文档图片副本已解除引用，按用户要求待删除，目前执行工具拒绝删除。正式引用仍是TheGame原路径，不复制手机运行素材。

本轮再次读取OtherMat_v9的shape389→bitmap388内嵌JPEG，原生仍940×590；journey_map中央940×590与只读旧183.png的554600像素全等，正式1408×590仅扩外围。没有同内容高精度原画可供本轮重导，不将普通放大当作高清替换。全项目扫描与实际地图截图见同目录；原来源和重复动画帧保留。

全屏模糊复核后另试Swin2SR Classical x2恢复：ONNX来自[Xenova/swin2SR-classical-sr-x2-64](https://huggingface.co/Xenova/swin2SR-classical-sr-x2-64)，冻结revision `71e28a83cfb61f5dc95bafdeebf2815167914f2b`、模型SHA256 `6dde3fe2440543ccae7c40d175609f83c18aeaa3d8456745c4329ef97ae744bd`，上游caidas/mv-lab、Apache-2.0。使用本地DirectML的Classical x2推理，不使用生成式提示词、GAN/realworld分支或图像重画；复用已有残差回投影与透明RGB延展，仅离线诊断，不是高精度原源。正式资源未替换，模型/来源/环境和候选留art_map_density_recheck_20261117，有限改善未达到1080p主体验收；同目录保留地图/入口1080p对照。

## 跨类别接入撤回后的来源状态（2026-11-17）

本轮按用户要求撤回Boss及其后续连续跨类接入，不制作新素材。正式文件恢复自art_boss_batch_install_20261117/baseline.json对应的原文件散列，覆盖原资源、动画、场景、作者工具及消费者；撤回批次21个新增文件同步撤出运行路径。所有256个恢复目标都有散列匹配的原件；其中255个直接来自既有before备份，UiSmokeScenario.Bull.cs来自HEAD并按原CRLF恢复，散列严格一致。

回退前400个改动文件、原Git索引与二进制补丁完整保存在art_batch_rollback_20261117；后续各类候选和来源目录仍保留，不把撤回等同来源丢失。用户已确认保留普通怪物、此前已审成果及英雄规范修复，普通怪物整批画面仍待审查。唐僧/悟空preview各13张、2400×400六列，均属HEAD既有英雄重导批次；本轮不重新放大、不改图集或动作。

保留上述怪物资源时，地图浮层沿用原各身体atlas，只同步11个AtlasTexture.region到当前idle可见裁片（本批10主体+此前猴兵）。复用art_map_portraits_20261117/sources与原散列、原生裁片证据核对；没有保留后批共享头像新图集/导出入口。11处region外序列化内容严格相同，来源裁片RGBA全等，详见Validation回退后验证。

## 普通怪物类别批量原路径接入与英雄图集修复

最新授权按“全部普通怪物一批、全部Boss一批”直接制作接入并留Git整体查阅，取代逐物种待审。妖猴小样已经用户通过，实际正式采用原网格2048×690（不是历史1024×845紧凑候选）。所有当前PNG/.import/UID、SpriteFrames子资源ID和动作序列保持；仅源像素、region/margin、Sprite身体scale0.5及原offset像素值换算。其余怪物保留原槽位注册基准，透明padding容纳采样边缘；未改变逻辑画布、绘制顺序或判定。

来源仍为OfficialReference核验的Monster_v1/Monster2_v4及既有stick清理版。gorilla/shrimp/tide/turtle沿enemy_fidelity_20261030/grounded，保留逐帧死亡修正；妖猴沿ground_monsters原注册，bull_guard沿bull_art，knife沿qixing/registered，飞妖沿garuda/native_manifest，灯笼鱼沿east_sea，召唤鲨鱼/其效果沿shark_art原manifest。所有父矩阵均保持；不重新运行会覆盖玩法的旧原生导入器。

| 图集 | 源帧/姿态 | 正式2倍尺寸 | 来源结果 |
| --- | --- | --- | --- |
| bull_guard | 83/27 | 1024×2284 | 原矢量完整高清 |
| demon_monkey | 78/20 | 2048×690 | 原矢量完整高清 |
| flying_demon | 85/33 | 6240×2200 | 原矢量完整高清 |
| gorilla | 85/23 | 2048×1184 | 原矢量完整高清 |
| knife_demon | 83/25 | 3120×1144 | 部分完成，bitmap 478保持源精度 |
| knife_demon_blade | 27/22 | 5328×114 | 原矢量完整高清 |
| lantern_fish | 87/87 | 4832×2176 | 原矢量完整高清 |
| shark_spawn | 177/74 | 4096×3936 | 部分完成，bitmap 244保持源精度 |
| shark_spawn_warning | 25/23 | 1024×928 | 原矢量完整高清 |
| shrimp_demon | 79/29 | 2048×1180 | 原矢量完整高清 |
| tide_demon | 95/46 | 2048×3056 | 部分完成，bitmap 699保持源精度 |
| turtle_minister | 98/57 | 4096×1736 | 部分完成，bitmap 1203,1207保持源精度 |


source/variant散列、纯矢量/位图/滤镜依赖及原密度逐帧复现结果在`art_ground_batch_install_20261116/<名称>/density_manifest.json`；正式4资源与before/对照在installation.json及installation_verification.json。复用prepare_grounded_enemy_art.py已授权密度入口，读取批次before，避免重复接入累计缩放；prepare_batch.py沿既有FFDec源注册链试导，原XML/SWF不动。最低线宽按原1倍有效20TWIPS离线规范，滤镜预算依密度四次方扩大，不更改源滤镜半径/颜色/强度/时序。

以下四图明确保留原版：bat（26个位图）、cave_brute（位图姿态序列）；shark_spawn_slash主体bitmap293、shark_spawn_tornado十位图267..285。斩击/旋风2倍试导已撤回到本轮before，候选仅离线保留，不计高清。上述四图和四混合主体合计新增8来源限制，仍需同内容高精度来源；没有AI重画。旧15张分动作图不参与当前正式播放，保留未勾。

悟空战斗图集仅置换0/1/85三格：原待机置0、原85姿态置1、透明格置85。200逻辑格/六列和86共享姿态不变，13图/1170格RGBA逐项全等；技能透明时刻同步指向85，不能把透明格直接填成身体。install_official_hero_hd.py --visible-first复用原remap_frame_tracks，同步两个库与场景，原canonical installed/*.json的poses/motion_cells/source_cell_map已同步，未来--frame-map仍能复现。原52图高清内容未重画；首格修复不增加完成量。预览沿backpack_v2官方62帧/1帧，修的是播放器驱动：自身真实时间_Process+原生autoplay取代窗口/面板转发，身体/武器图和预览库帧序不改。

## 妖猴原矢量2倍完整动作小样（历史；已审，正式保留原网格见上节）

来源沿既有export_official_ground_monsters.py：Monster_v1恢复SHA256=`5bd88da4211870f2ca2354b7cec8d1790747869641c11fe553910b10fe2a264a`，export.monster.Monster2；复用`action_origin_review/ground_monsters_export/monster1_art.swf`及原manifest父矩阵，不使用不含妖猴的grounded清单。sprite57/60/67/73/80分别run16/hurt13/attack_1 12/death22/idle15，78帧24fps，递归依赖只有shape/sprite、无bitmap/filter。旧2026-10-26“88帧含after_hurt”是历史导入状态；当前正式五动作78帧，本批不删帧、不恢复未使用的旧动作。

FFDec26.3在1倍逐帧复现正式SpriteFrames可见RGBA/alpha；共用128×115逻辑画布，左上(-68,-71)、offset(-4,-13.5)，原父平移按逻辑floor注册，无额外死亡落地修正。普通zoom2导致原细描边变细，核实原BitmapExporter.lineStyle的最小线宽为输出1px。最终仅离线临时XML将依赖内167条LINESTYLE.width从1TWIP规范成原密度有效20TWIPS，保持逻辑1px描边；两版1倍78帧完整RGBA全等。恢复167字段后XML与原树完全相同，路径/颜色/绘制深度/时间轴不改；不是AI重绘或插值放大。原SWF/XML文件保持，临时variant和全部散列/字段变更记录在sample_manifest.json.stroke_fidelity。

最终候选`art_demon_monkey_sample_20261116/demon_monkey_2x.png`为1024×845、20共享姿态、256×230注册画布；原78帧时间索引完整，compact_registered_atlas裁透明边并以margin恢复共同画布。1倍/2倍各78帧装箱解包RGBA全等；实际采样差异与审图见Validation。正式PNG UID `uid://duybemtonqsd7`、SpriteFrames `uid://dx8qlss6tc7ff`、AnimationLibrary `uid://denuc0ot4c0gs`和全部原文件本轮保持。旧五张分动作图不是本批替换目标，不勾完成。

## 花果山猴兵已审2倍正式接入（2026-11-16）

用户通过下节小样并要求最小接入。原Monster_v1/grounded注册来源、69帧与23姿态保持；正式huaguoshan_monkey_official_body.png逐字节采用已审monkey_2x.png（512×1018），原PNG `.import`/UID `uid://u5ac8itnrlqb`保持。原SpriteFrames UID `uid://dsjfyh1o0sr4w`、23子资源ID、69帧引用/顺序/duration/24fps/loop不变，只改region/margin像素。场景只改m_Body scale0.5、offset(-9,-2)，动画库只改6条offset值，其他轨道/方法/标记/几何不变。

作者入口复用Tools/LegacyMigration/prepare_grounded_enemy_art.py的`--approved-density-sample`分支，输入已审sample_manifest，校验23共享姿态映射后写原4个资源并输出installation.json；原`--manifest`路线保持。旧PNG/导入、帧资源、库、场景与代码备份在`.godot/validation/art_monkey_install_20261116/before/`，旧5张分动作PNG不改。未新增高清目录、运行时素材副本或密度系统。

真实调用链的AbstractActorEntity.SetFacing此前把AnimatedSprite2D身体scale重设±1，本轮只改为保留缩放幅度、切换横轴符号；原offset继续随scale镜像，判定容器仍±1。正式69帧解包与已审RGBA全等，138真实朝向检查/30张双端图及GGF攻击受击死亡回池验证见Validation；图像完成59/667、配套19/182。Android真机和其他角色/位图仍未完成。

## 花果山猴兵原矢量2倍完整动作小样（2026-11-16，历史；已审接入见上节）

原包Monster_v1恢复后SHA256仍`5bd88da4211870f2ca2354b7cec8d1790747869641c11fe553910b10fe2a264a`。沿既有export_official_ground_monsters.py产物`action_origin_review/ground_monsters_export/monster1_art.swf`（只移除源stick判定显示元件），与prepare_grounded_enemy_art.py已审注册结果`enemy_fidelity_20261030/grounded/manifest.json`重导，不直接覆盖正式导出器。五动作sprite89/92/101/108/109分别run16/hurt13/attack_1 24/death15/idle1，完整69帧24fps；实际递归依赖无位图或滤镜，具体ID和XML/SWF散列留sample_manifest.json。

FFDec26.3在zoom1/2原生渲染，1倍69帧按当前共同89×72逻辑画布及offset(-4.5,-1)还原，与正式SpriteFrames每帧可见RGBA/alpha严格相同。2倍注册画布178×144，原动作父平移的逻辑floor规则及死亡逐帧落地修正保留；不按可见框重新居中。复用compact_registered_atlas装箱仍23个共享姿态、69帧序，2倍存储512×1018，无帧删减。

候选及源记录在`.godot/validation/art_monkey_sample_20261116/`。当前正式PNG/.import/UID/SpriteFrames/动画库/场景不改；仅内存预览m_Body scale0.5与offset键按密度换算，实体根/碰撞/判定轨保持。候选出现栅格采样差而非字节全等，待用户审图；旧五张分动作PNG不是当前正式动画消费者，本轮保持，不能按一张候选勾全猴兵六图完成。

## UI与伤害数字源类型复核（2026-11-16）

同版OtherMat：当前hurtnum0～9、bnum0～9、pnum0～9以及miss分别直接对应30×30、42×42、30×30、55×24的DefineBitsLossless2Tag；本轮核对所有符号ID/原尺寸，未做数字RGBA逐字验证或高清替换。技能页头像382/386亦有377/379/383小位图依赖，不能推广“sprite就是矢量”；尚未把技能页头像等同HUD83×76的来源身份。

同版Commonv3720散列`53722c6c8615d2542de2e3cc255a68c5d80712c6b8097aaecae6bc7be0a40ab2`：369底板是纯矢量；装备376、赠送367、出售381含Lossless2位图依赖。复用export_official_inventory_actions.py的IMAGES映射离线导出六PNG，与正式1倍RGBA六图全部一致，旧“菜单来源为矢量元件”不能用于五张按钮，已更正下文。新增9项来源限制（五按钮/四种当前数字）；菜单底板查源完成但尚未制作2倍。具体ID/尺寸/散列见本轮ui_source_audit.json，所有正式原图保持，不声称外部高精度源穷尽。

## 三入口已审统一题签正式接入（2026-11-16）

用户审查通过并要求按版接入。正式map_save_game{,_hover}.png、map_return_title.png、map_skills.png逐字节采用`.godot/validation/art_map_caption_uniform_20261116/`已审artwork输出，原路径及全部旧.import/UID保持；新增map_menu_caption_ink.png（276×72，导入UID `uid://fndbdvmnxm2n`），逐字节采用已审共用墨迹。来源仍为下节原497/500/489 alpha与原令牌题签带近黑像素，不采用EDSR恢复候选，不新增生成式内容。

原PNG/导入/场景/测试/资源常量及文档备份位于`.godot/validation/art_map_caption_install_20261116/before/`。install_approved.py校验四源散列后接入，原SpriteFrames字节、四图题签遮罩外RGBA保持；三个Caption下各一原生TextureRect复用同纹理，138×36逻辑尺寸、相对位置(-19,-2.5)、中心x74与已审样稿一致。技能随原Caption显隐，业务脚本与绑定不改。既有Collection Res生成器登记新增1图，未手改生成文件。

正式20张双端比例/状态图、12张已审对应像素全等与真实鼠标/GGF复用验证见Validation。本批仅完成题签结构/视觉统一；书本/卷轴/令牌细节及原墨迹alpha未恢复真实高精度，清单新增该图后58/667，未把题签接入算图形高清。

## 三入口统一墨底/文字位置小样（2026-11-16，历史小样；已审接入见上节）

修正用户指出的题签不一致：当前保存/技能源题签约137px宽、令牌112px，本轮仅离线分离旧底，统一使用一张由原墨迹合成的276×72 PNG，按138×36逻辑尺寸显示；不是新绘图案，也不计高清。原Font/20号字和描边保持，三个Label同100×31，根坐标中心x74，Label相对墨底偏移固定；保存中心y79、技能84保持，令牌局部中心95.5。按钮、书本/卷轴/令牌主体位置及原动画时序不动。

来源仍为既有OtherMat bitmap497/500/489独立alpha和当前map_return_title112×114。保存两态及技能15个hover槽只清原题签alpha覆盖的像素；令牌只清旧题签带y80～111的近黑像素，彩色主体与下尖保留。四图遮罩外RGBA严格相同，before/、遮罩、来源散列与prepare_uniform.py留`.godot/validation/art_map_caption_uniform_20261116/`。共用墨底取上述原alpha对齐后的并集，无生成式补画；2倍alpha插值不冒称恢复了原图细节。

preview_uniform.gd沿原场景内存挂同纹理的TextureRect在Caption下，MouseFilter.Ignore、show_behind_parent；技能随Caption只在hover显示。正式PNG/.import/UID、SpriteFrames/场景/C#不改。当前样稿及实际字形/源保护验证见Validation，待审图后再接入。

## 底图与入口图标原图约束恢复小样（2026-11-16，未接入）

输入为当前正式journey_map1408×590、已去字的map_save_game两态138×93、原map_return_title112×114，逐张散列与PNG副本留`.godot/validation/art_bitmap_consistency_20261116/`。原EDSR_x2.pb仍为`585623221baa070279a0d1e7e113a4c3faba0f318ca7fdd9a65d9afc0763d9b4`，未使用生成式重画；地图原推理输出仅在输入散列相同时复用。小样使用透明区最近可见RGB延展、原模型2倍恢复及12次原图残差回投影；alpha仅线性增密，PNG尺寸翻倍不代表新增真实源细节。复现脚本restore_sample.py和独立测量/原生预览在同目录，无正式工具或资源变化。

更大位图补查：P:/Godot-Project共4331张图像路径，当前工程外3669张可读；按尺寸和感知哈希筛查未发现同内容高精度原件。结果larger_bitmap_source_search.json只作本地候选清单，不能推断外部无原稿。官方矢量补查：同版OtherMat RoleInfo.btn_back_menu244的241/242/243形状实际是“返回主菜单”字形，已原生2倍导出查看，不是红色令牌；没有借用该元件换图。

输出地图2816×1180、保存两态276×186、令牌224×228，只作未审恢复候选。独立回采样与1080p/200%检查见Validation：改善有限、地图仍软，不能称真高清；正式来源/路径/UID与此前登记保持，图形高清状态不勾。

## A2地图入口原生题签与位置修正接入（2026-11-16）

用户已认可分层小样，随后要求令牌文字不要偏下。正式map_save_game{,_hover}.png及map_skills.png原路径接入已审去字版；书页/毛笔/卷轴保持，3PNG完整alpha和源字形遮罩外RGBA与before原图全等。map_return_title.png、map_skills_frames.tres和全部旧.import/UID原字节保持。新增保存/技能原生Label复用已有caption_font，令牌原生Label从(8.5,83)改(10.5,78)，即右2/上5，三个标题根坐标水平中心x77.5；保存(72,79)、技能(51.5,84)局部中心保持小样。原1/15/19帧24fps不改，技能只在hover显示文字。

原图/场景/C#备份和核对在`.godot/validation/art_a2_map_menu_install_20261116/before/`，来源处理仍为上轮source_audit/layers/make_text_split.py，不以新的官方重导替换像素不同的保存普通图。Ge绑定从原生成器产生，原生场景与4处既有生命周期同步题签，没有新增运行时统一布局系统。

**图形精度未提高：** 当前地图1408×590（旧中心940×590）、保存138×93、令牌112×114、卷轴59×33/58×37原位图保持；本批改善来自字体渲染，不把去字PNG称高清来源。用户指出全屏仍糊属实，窗口/4K输出不增加源细节。本地fla/psd/xcf/ai/svg/ora/kra/clip扫描只找到工程图标/主题SVG，未找到这些素材高精度原稿；扫描范围记录local_high_precision_source_search.json，不泛称外部已无来源。EDSR失败候选仍不采用；地图/图标图形高清继续未完成。

## A2地图入口题签分层与来源限制（2026-11-16，小样待审）

本批沿OtherMat_v9同散列和现有export_official_map_combo_ui.py查源，不修改正式导出器。用户明确要求保存按钮去烘焙文字、复用令牌原生Label，故分离题签属于本轮授权调整，不把字体变化冒称原位图完全保真。书本、毛笔、卷轴、墨框外围和原动画仍保护。

| 资源 | 实际来源 | 本批处理/限制 |
| --- | --- | --- |
| map_skills.png | SelectPLace505.showBuySkill492；482静态、488展开、491光晕。bitmap479为59×33卷轴，484为58×37书页，489为137×28题签；483矢量morph做mask、486为边框。 | 混合来源，35帧按1/15/19和24fps保持；原图1倍重现RGBA全等。最终仅hover15帧删除原题签彩色字形，在按钮原位置放项目Label；位图图形仍未高清。 |
| map_save_game.png | 旧Art/game_pic/level_choose/294.png；官方SimpleButton502 up/down为shape499，含bitmap498书本104×84与497题签137×28。 | 旧正式图与官方1倍重导并不全等（含书页/alpha差），所以保护当前原图，不直接换shape499。文字样稿从源497取得字形遮罩，只清字为墨色，保存位置/alpha保持。 |
| map_save_game_hover.png | 官方SimpleButton502 over的shape501，复用498书本与500题签137×28。 | 1倍重现RGBA全等；按源500题签遮罩去字，字体复用令牌20号海报/金色/黑白描边。整张图仍受104×84书本源限制。 |
| map_return_title.png | 旧Art/Game_2/Button/ButtonReturnMainMenu.png，112×114。 | 正式与旧源文件字节一致。已有独立“游戏设置”Label作为本批样式模板；令牌PNG不变，尚无已证实更高精度源。 |

源题签字形遮罩从497/500的JPEG3独立alpha层及489的Lossless2 ARGB解码取得；最终仅覆盖其彩色字形及1像素邻域为原黑墨，整图alpha不变，遮罩外RGBA逐像素一致，不AI补画书本或环境。保存Label中心原图内(72,79)，技能hoverLabel中心按钮内(51.5,84)；源35帧、原轮廓/定位不变。原图片和场景保持，候选在`.godot/validation/art_a2_map_menu_20261116/text_split/`。

另有原源2倍图和3张EDSR诊断，均未进入最终图形候选或正式资源。EDSR沿用登记的非GAN模型SHA256，保存普通/悬停可见RGB回采样MAE4.664/4.690，不能视为保真成功。最终样稿的提升来自Godot原生文字，书本/卷轴/令牌不因PNG更大而计高清完成。40张原生预览与3按钮点击/时序证据见Validation，清单4项来源受限继续未勾，完成量仍58/666。

## A2已审光晕正式资源与分页（2026-11-16）

用户已通过下节采样修正版画面。美术源仍为同散列OtherMat_v9及原shape454/461，沿既有coastal导出/作者链原路径接入：浅岸hover674×624、current3060×2264；东海hover1484×530、current与新增current_2各2832×2380。原normal透明图、hit_mask.res、全部已有.import/UID保留；原PNG/库/场景在`.godot/validation/art_a2_map_glow_install_20261116/before/`及Git历史，不设hd目录/后缀。

| 当前动画 | 原2倍注册画布 | 共同透明裁切（输出像素） | 原生装箱与补偿 |
| --- | --- | --- | --- |
| 浅岸19帧 | 674×624 | [19,20,631,586]，每格612×566 | 5列×4行单页；CurrentHint scale0.5、offset(-12,-9)，原position(168.5,156)保持。 |
| 东海19帧 | 1484×530 | [20,21,1436,497]，每格1416×476 | 两页各2列×5行，帧0～9/10～18；scale0.5、offset(-14,-6)，原position(371,132.5)保持。 |

仅裁共同透明外围、无每帧居中；38帧还原到已审注册画布RGBA全等。脉动对称帧仍有颜色差，全部19帧保持，不能按alpha相同去重。原生库只为东海新增texture值轨（原0和10帧时刻），原frame键映射页内0～9/0～8；原时刻、24fps、长度、hover/RESET/其他轨不改。三张current理论RGBA8合计77.85MiB，较未裁切2倍减少15.47%，最大单边3060，仍不等于Android预算/性能通过。

正式export_official_coastal_map.py新增显式--density/--only；密度2仅本批浅岸/东海已审源，采样补偿与下节相同。密度1默认全三地点9图仍与原RGBA全等。apply_official_coastal_map.gd读该manifest，用原生Sprite2D密度/offset和AnimationLibrary页轨写入，定向保留文本/UID；调用前校验已安装页SHA256，避免错把旧贴图配新网格。新页经现有Collection Res生成器登记，没有改框架、插件或另建加载系统。

两处current纯矢量来源已完整接入；hover仅矢量光晕提升，137×28位图字图继续原精度，不计整图完成。采样残差为用户明确审查接受，未宣称像素全等；同一图集后续不得以光晕通过推断文字高清。正式预览、帧/输入/流程结果及T3进关阻断见Validation；本批图像文件计数58/666，来源受限34项仍明列。

## A2地图光晕续研与采样修正版来源（2026-11-16，后获审查通过）

用户要求继续本批。美术源仍为下节同散列OtherMat_v9；保留原SWF/原PNG，复用已核验coastal父矩阵/注册流程。最终试验入口为`.godot/validation/art_a2_map_glow_20261116/export_glow_fidelity_trial.py`，所有候选留忽略目录；正式export_official_coastal_map.py及游戏资源不改。

核对安装版FFDec26.3字节码，并取同版本[Configuration.java](https://github.com/jindrapetrik/jpexs-decompiler/blob/version26.3.0/libsrc/ffdec_lib/src/com/jpexs/decompiler/flash/configuration/Configuration.java)、[Filtering.java](https://github.com/jindrapetrik/jpexs-decompiler/blob/version26.3.0/libsrc/ffdec_lib/src/com/jpexs/decompiler/flash/types/filters/Filtering.java)、[GLOWFILTER.java](https://github.com/jindrapetrik/jpexs-decompiler/blob/version26.3.0/libsrc/ffdec_lib/src/com/jpexs/decompiler/flash/types/filters/GLOWFILTER.java)和DROPSHADOWFILTER.java留档，URL/SHA256见本轮`references/provenance.json`。没有以master源码代替安装版作结论。Glow已有zoom倍乘；boxBlurPixelsLimit=1000的模糊预算才是厚光晕被压窄的主因，单纯再乘源半径不能修复。

最终小样将离线模糊预算按密度四次方提高（1倍1000、2倍16000），并保留原1倍整数核宽及中心相位。临时2倍XML把相关40条Glow记录转换为相同底层dropShadow调用，核宽用原1倍round(blur)、中心补偿±0.25逻辑px，颜色、强度、passes、knockout和合成标记逐项保持。原Glow在FFDec内部本就委托零距离dropShadow；这里只表达密度变化的取样补偿。原源未改，所有非滤镜取样字段及完整轮廓/矩阵/时序经语义树核对一致，未调色、锐化、AI重画或插值旧PNG作为高清。最终只用已安装原CLI，不依赖研究期隔离Java滤镜原型。

原密度6/6图RGBA严格重现；2倍悬停alpha总量为原1.00248/1.00120，当前19帧范围浅岸1.00211～1.05242、东海1.00327～1.02353，质心差最大0.697/0.774逻辑px。仍有小半径knockout/抗锯齿采样差，尚未像素全等或获审图认可。悬停的137×28位图字图仍原精度，两处current才是纯矢量光晕来源；全图高清状态和其他23张位图来源结论不因此改变。保留旧失败样本作根因对照，最新同尺寸/双端/动图和命令见Validation；四张可见图均未正式替换，完成量仍55/665。

## A2地图地点来源核实与原源2倍失败小样（2026-11-15，历史）

源为已登记OtherMat_v9，恢复后SHA256=`fa7681aad99fb06f50228530b7f70f6334a1d7052a7bb6ef2ce3c3d81867fd5e`。从SelectPLace505的s1_1～s4_1实际placement查wrapper的三状态身份，不按邻近数字猜源；完整30图依赖审计在`.godot/validation/art_a2_map_locations_20261115/source_audit.json`，既有XML与本轮新导出的source.xml逐tag一致。普通地点的current是19帧24fps位图加原GLOWFILTER，鲨之涡流48帧是位图旋转；均不能因外层是sprite/shape或动画而计纯矢量。

| 地点/字段 | wrapper与normal/hover/current | 嵌入原精度与结论 |
| --- | --- | --- |
| 花果山s1_1 | 396 → 392/394/395 | bitmap390为68×78、393为160×174；current复用390加光晕。三图位图受限。 |
| 水帘洞s1_2 | 403 → 399/401/402 | bitmap397为60×58、400为177×138；current复用397。三图位图受限。 |
| 桃花源s1_3 | 410 → 406/408/409 | bitmap404为64×65、407为162×163；current复用404。三图位图受限。 |
| 蝙蝠窟s2_1 | 417 → 413/415/416 | bitmap411为57×30、414为150×95；current复用411。三图位图受限。 |
| 七星洞s2_2 | 431 → 427/429/430 | bitmap425为86×64、428为153×108；current复用425。三图位图受限。 |
| 迦楼罗s2_3 | 424 → 420/422/423 | bitmap418为125×52、421为222×119；current复用418。三图位图受限。 |
| 浅岸s3_1 | 460 → 453/458/459 | normal显示图全透明、输入为shape453；hover矢量shape454轮廓加bitmap456的137×28字图，current为shape454+原滤镜19帧。矢量线可重栅格，但本轮滤镜保真未通过。 |
| 东海s3_2 | 468 → 453/465/467 | normal全透明、输入shape453；hover矢量shape461+bitmap463的137×28字图，current经466播放原shape461/滤镜19帧。限制同上。 |
| 鲨之涡流s3_3 | 478 → 453/474/477 | normal全透明；hover依赖472(215×208)、471(137×28)、470(124×88)、469(110×76)，current48帧复用472。两张可见图位图受限。 |
| 牛魔谷s4_1 | 445 → 441/443/444 | bitmap439为57×56、442为152×101；current复用439。三图位图受限。 |

本批可见图共27项：23位图、2混合、2矢量滤镜动画。没有找到登记同版包中的同内容更高精度位图，未AI重画或增强；3张透明normal保持输入状态，不算新增高清。源XML tag审计与SWF散列冻结，不将模型产物/放大图片当官方源。

浅岸/东海试制复用原export_official_coastal_map.py的同源共同注册流程，在忽略目录给试验副本增加density2和选图参数。原源1倍6图与正式PNG尺寸/解码RGBA完全一致；2倍保留原父矩阵、原逻辑裁切offset、19帧顺序/24fps，hit图仍原逻辑密度且字节像素一致。输出current分别3370×2496、7420×2120，RGBA8无mipmap理论32.09/60.01MiB；Android最大纹理/驻留未验，不能直接作为正式运行图。

**保真未通过，不采用：** FFDec `-zoom 2`没有保留原逻辑光晕；离线把滤镜blurX/blurY/distance按密度补偿后仍显著变细/变透明。hover回采样alpha总量浅岸约0.5383、东海约0.4041；current19帧范围分别0.6262～1.9654、0.4627～1.9179，不能按PNG尺寸称高清。实际滤镜轮廓/透明度变化不属于允许的抗锯齿差异。两版诊断保留；试验修改已归档`export_official_coastal_map_density_trial.py`，正式导出器恢复开工原字节，正式素材/场景/动画不改。命令与18张原生预览、38帧及失败对照见Validation。

## A1c已审地图接入与高清恢复限制（2026-11-15，当前）

用户已批准下节外围样稿并要求继续。现正式`Sprites/UI/frontend/journey_map.png`原路径接入1408×590共享原密度扩图，SHA256=`40995cde090ae8dba497f43d4bbe626fd93b642228d212c0317e040345c85fba`；原`.import`/UID保留，原PNG归档于`.godot/validation/art_a1c_map_install_20261115/before/`及Git。原import_frontend_assets.py新增`--approved-map`入口，校验尺寸和中央940×590与只读旧183.png的RGBA字节一致后复制；错误尺寸/中心改变会拒绝替换。仍只一张正式背景，不复制PC/Android版本。

背景逻辑位置(-234,0)、尺寸1408×590，中心554600像素RGBA差0，地点与原Frame940×590保持。技能/保存/设置只迁到既有全屏根的左锚点，局部位置/大小/动画保留。最新显示边界见CrossPlatformPlan §2.12.21：同平台全流程固定Project Settings、KeepHeight，DisplaySystem只换算构图，不再临时切换战斗显示；关卡宽屏仍单列T3验收，本轮不修改玩法。1920×1080窗口及3840×2160菜单全屏通过；来源/纹理字节不改，执行命令和完整保护结果见Validation。

**1408×590是覆盖画布，不是高清结论。** 原中心仍来自940×590 JPEG388，未找到更高精度同内容原件。另在忽略目录`.godot/validation/art_a1c_map_hd_20261115/`试制非生成式EDSR x2恢复：输入为上述正式共享图，输出2816×1180；OpenCV DNN CPU4线程，推理231.43s，不使用扩散/GAN/文字生成或改画。模型来自[Saafke/EDSR_Tensorflow EDSR_x2.pb](https://raw.githubusercontent.com/Saafke/EDSR_Tensorflow/master/models/EDSR_x2.pb)，38,490,162字节，SHA256=`585623221baa070279a0d1e7e113a4c3faba0f318ca7fdd9a65d9afc0763d9b4`；上游commit查询HTTP403，故只按实际二进制散列冻结，不伪报版本。

离线产物包含原图、普通Bicubic2倍基准、EDSR RGB及亮度/原色度诊断，`sample_restoration.py`/`model_source.json`/`restoration_measurements.json`记录过程。RGB候选比额外色度转换方案的回采样颜色误差更小，选作对照；核心回采样RGB MAE0.2794/255、SSIM约0.9974～0.9980、估计平移小于0.007原像素。这些指标只检验偏离，不能证明新增细节真实；1080p森林/岩石仍软，清晰度收益有限，**暂不采用、未替换正式图、不计地图高清**。原生9张同逻辑输出、裁片和全部候选留给本批审查，后续不推广此方法。

## A1c地图外围扩图小样（2026-11-15，历史制作记录；外围已审）

扩图接入前`Sprites/UI/frontend/journey_map.png`与旧`Art/game_pic/183.png`字节相同，SHA256=`e35f886d3f040a7dd6cab3237e1251d74c15ef4f54c5aaa2b746a43e073ab15c`。本轮查明官方上游为OtherMat_v9（恢复SHA256 `fa7681aa…7fd5e`）SelectPLace505在depth1以零平移放shape389；shape389边界18800×11800 TWIPS、bitmapMatrix scale20，直接引用940×590 JPEG388，是原生位图。提取JPEG SHA256=`25ee85ff173f6be01f70b7a48bb0194d35169f6435e643689c92ce8905219e35`，与源XML imageData字节相同；JPEG解码与扩图前PNG最大RGBA差[1,1,1,0]，中心继续冻结该PNG，不替换为重新提取的JPEG。

**复用原ECPA，而非另建接口：** 用户此前已授权并成功两次，本轮明确重申。现场核对原cpa-gui provider `http://127.0.0.1:8317/v1`、现有进程及gpt-image-2模型，使用既有`imagegen/scripts/image_gen.py edit` CLI和本机已有凭据。凭据只注入子进程环境，未写工程/日志、未变更用户配置。入口与实际参数见Validation、离线`generation_route.json`。此前“缺内置工具需另配API”的判断漏查ECPA，现已纠正；已有授权继续适用。

输入是1408×592透明外围画布，原图置(234,1)，中央940宽和1px上下注册垫行保护；左右234px生成。模型`gpt-image-2`、quality high，请求2816×1184，22.4s完成、退出0，**实际返回1934×813，未达到2倍**。这份原始生成结果只作外围来源，中央生成内容全部丢弃。归一化到1408×592并裁上下垫行后，只保留新外围；源中心940×590在(234,0)逐字节贴回。最终`journey_map_expanded_native.png`为1408×590原密度候选，554600原核心像素RGBA差0。没有通过插值、锐化或AI重画原中心来报高清。

直接贴回中心的首版有色阶接缝；试用既有OpenCV融合仍未消除。最终在生成的两侧内做固定原边列颜色的平滑颜色过渡（cross-neighbor relaxation1600次），外边保持生成色，原中心最后完整贴回。过渡只修改新增外围，不新增岛屿、路线、入口/发光、文字、建筑或地标，不做镜像/重复原画填边。`compose_candidate.py`和`candidate_composition.json`保存准确过程；100%/200%接缝图和原生场景图供审查，不把边列RGB连续这一数值当作视觉审查替代。

输出统一留在`.godot/validation/art_a1c_map_20261115/`：原生成图`output/imagegen/journey_map_ecpa_raw_01.png`；原密度候选`journey_map_expanded_native.png`；提示词`outpaint_prompt.txt`；12张Windows1920×1080和Android1920/2280/2340/2400/2520×1080原图/候选预览；`seams_100percent.png`、`seams_200percent.png`和`map_review_comparison.png`。六种比例共用同一母版按外围裁切，不重新排地图地点。Frame16个直属节点原数据已记录，原生14个Control的位置/尺寸/缩放保持；正式场景/资源引用不改。

理论单张RGBA8无mipmap成本约3.17MiB（原图约2.12MiB，新增约1.05MiB），不是实际GPU/Android验收。当前只交付原密度外围扩图：未交付真正2倍高清母版，原中心和地图字图/地点清晰度仍待相应素材批处理。正式原图/.import/UID、共享布局配置和全局keep保持；本批停在样稿审查，不将候选加入665正式图像总数或勾成地图高清完成。


## A2选角审定分层正式接入（2026-11-15）

上一节加宽/独立字体/漏色修正样稿已获用户认可，本批接入原CharacterForm。源仍OtherMat_v9恢复SHA256 `fa7681aad99fb06f50228530b7f70f6334a1d7052a7bb6ef2ce3c3d81867fd5e`，所有人物/墨框均从原SVG以Godot栅格器3倍重导；无AI重画、无旧PNG插值。下方“未接入”内容为历史样稿记录，正式状态以本节为准。

| 正式文件 | 原源与显示契约 |
| --- | --- |
| `character_{wukong,sanzang,bajie,wujing}_card.png`（4图，原路径/UID） | sprite511/521/515/518，shape509/520/514/517；1271×1830人物/剪影/问号层。420×590逻辑母版，原235×590核心不横向拉伸；原姓名区域透明，使用独立项目字体。 |
| `character_card_ink.png` | shape510的悟空/沙僧原变换结果逐字节相同，711×1830一图共用；保留原框边角/厚度，中段NinePatch延展。 |
| `character_sanzang_ink.png`、`character_bajie_ink.png` | 同shape510在两张卡中原平移不同，分别711×1830；保留差异，不强行共图。 |
| `character_card_background.jpg` | 原bitmap508的940×590 JPEG原字节提取；未增加真实细节。Polygon2D使用各卡原shape506四点遮罩，只平移右边界，源像素UV不变，框外黑底。 |

3张墨框和1张原生背景为本批新增正式图像，已入ArtProgress；四人物原`.import`字节/UID保持，新4图由Godot生成`.import`。原完整卡留Git和`.godot/validation/art_a2_character_install_20261115/before/`，正式场景去掉原AtlasTexture，不保留平行完整卡加载路线。工具`export_official_character_cards.py`默认`--mode layers --density 3`，需`--godot`；历史`--mode complete`必须显式隔离输出目录，防止覆盖正式人物层。来源/遮罩/姓名锚点输出到离线`layer_sources.json`。

8份人物/墨框层对照与认可PNG的RGBA相同（共用墨框比较两次），JPEG字节相同；沿用样稿4,079,328保护像素RGBA差0证据。PNG RGBA8无mipmap理论成本：人物35.49MiB、共享/变体墨框14.89MiB、背景按RGBA8保守2.12MiB，共52.50MiB；不含字体/视口/驱动成本，实际格式/运行峰值以设备测量为准。未视为Android压缩、峰值或性能验收。正式导入均无mipmap、无size_limit、无有损重压缩。

原生正式C#场景独立画布与真实流程截图分别记录Validation。keep尚未启用整屏适配，地图外围未制作；原生背景精度仍受限，四卡整幅高清目标仍部分完成。

## A2选角加宽/独立项目字体样稿（2026-11-15，电脑16:9、手机20:9，未接入）

本批按用户最新方法制作分层候选，正式四张PNG和场景仍保留下一节2倍接入状态；没有新增正式并行hd资源。源仍同版OtherMat_v9，恢复散列`fa7681aad99fb06f50228530b7f70f6334a1d7052a7bb6ef2ce3c3d81867fd5e`。复用FFDec26.3导出sprite511/521/515/518的SVG，沿用Godot原生SVG栅格链；源文件与中间文件位于`.godot/validation/art_a2_character_layout_20261115/`。

- 角色/剪影/问号：原shape509/520/514/517；保持所有绘制路径、颜色、渐变和父矩阵，去除独立背景bitmap508及姓名bitmap507/519/513/516的绘制字段。原有姓名区域内可能共用路径的旧字形由区域裁切整体排除，角色核心在此前的官方遮罩内逐像素对照；原阴影保留。扩大源画布提供同元件原有外围武器，候选根据卡面裁切，未AI生成新武器。
- 核心原viewBox为各卡`crop_x,0,237,610`；绘制时核心仍235×590。分层母版扩至420逻辑宽以容纳外围，3倍PNG为1271×1830；绘制路径不横向拉长。背景直接复用原生bitmap508（940×590 JPEG）的更多区域，不据更大运行画布宣称背景高清。
- 墨框来自原shape510，栅格711×1830；NinePatch保留原边角与边线厚度，仅延展中段。角色和墨框分开处理，不把整张卡机械横向拉伸。
- 背景遮罩来自原shape506，源四点为`(-117.5,-349),(-117.5,242.65),(116.85,242.65),(115.5,-349)`；经各卡原父矩阵与裁切原点换算到235×590参考。拓宽时只将右侧两点平移新增卡宽，左边/上下边保持，原生Polygon2D按原背景像素UV绘制，框外为黑底。首次分层样稿缺了这层约束，导致顶部/接缝漏红，用户审图指出后已修正；不修改bitmap508配色来遮盖。
- 姓名按用户明确要求改用项目`TheGame/MainPack/Fonts/game_font.tres`原生Label（该资源仍引用原fz_cu_yuan_hud/fz_cu_yuan字体）；36逻辑字号、黑字白描边为候选。原bitmap字段矩阵换算成卡内归一化横向中心和原Y/高度，文字独立绘制且scale=1，不受图片宽高比调整影响。源PNG原件未抹字覆盖，旧字仍在离线原件/正式上一批状态中。

电脑16:9与手机20:9分别设计、四卡紧贴，其他手机比例沿用20:9构图有限留边/裁外围。当前12张原生对照、姓名锚点、核心RGBA差0与参数见Validation；没有正式接入、正式宽屏或Android真机完成结论。既有导出器仅新增`--density 3`，1倍复现仍精确一致。旧逐比例扩卡、上下黑框和官方姓名位图下框候选均已被本方法替代。

## A2四张选角卡原路径重导（2026-11-15，矢量部分已接入，位图部分受限）

复用`export_official_character_cards.py`和同版OtherMat_v9，下载/恢复散列仍为OfficialReference所列`ef03008e…15c9` / `fa7681aa…7fd5e`。四张原密度重导与接入前PNG的RGBA全部一致。工具新增`--density 1/2`与隔离`--output-directory`，保留完整元件、源矩阵、裁切遮罩、颜色、层序和源位图。正式PNG直接替换原路径，原UID/.import不变，原版归档在`.godot/validation/art_a2_character_cards_20261115/before/`及Git；没有hd平行资源。

| 正式PNG | 原元件 / 矢量主体 | 原生位图依赖 | 原尺寸 → 当前尺寸 |
| --- | --- | --- | --- |
| `character_wukong_card.png` | sprite511 / shape509，人物与装备 | 姓名bitmap507：162×65；背景bitmap508：940×590 | 334×610 → 668×1220 |
| `character_sanzang_card.png` | sprite521 / shape520，人物与装备 | 姓名bitmap519：100×65；背景bitmap508 | 317×617 → 634×1234 |
| `character_bajie_card.png` | sprite515 / shape514，剪影与问号 | 姓名bitmap513：139×65；背景bitmap508 | 357×617 → 714×1234 |
| `character_wujing_card.png` | sprite518 / shape517，剪影与问号 | 姓名bitmap516：100×65；背景bitmap508 | 351×617 → 702×1234 |

每卡均含shape506原遮罩、shape510笔刷覆盖层，共15个去重依赖：4个单帧sprite、6个shape、5个位图。姓名为DefineBitsLossless2透明位图，背景为DefineBitsJPEG2；不因外层为shape就判作纯矢量。历史“完整原始矢量卡”的描述在下方同步纠正。**当前仅矢量部分获得真实密度提升，位图仍使用原嵌入数据，不将整卡计为高清完成；同内容更高精度姓名/背景待解决。**

FFDec26.3原生`-zoom 2`输出悟空668×1219、唐僧633×1234，源边界逐密度取整；只在下边/右边各补1px透明画布，内容不缩放、不偏移，其余两张原生尺寸即双倍。原场景只把AtlasTexture区域倍增；四栏仍235×590、原shader和交互脚本字节保持。1080p固定输出对照、原生灰度/悬停/双人重选等结果见Validation；本批停止交审。

## A2英雄与背包原路径高清替换（2026-11-14，2026-11-15已获用户授权继续下一批）

本批52张正式PNG按用户最新要求直接替换原路径，原`.import`及UID保持，不保留hd目录/后缀。旧PNG在Git历史及`.godot/validation/art_a2_heroes_20261114/original_pngs/`冻结；同目录`before/`保存接入前资源/动画/场景，`replacement_sources.json`列出52项原/新散列、来源和尺寸。后文A1a及旧200px物理格记录属于对应历史版本；当前逻辑格200、像素格400、六列，运行时密度2。

| 本批正式资源 | 原源与处理 |
| --- | --- |
| 悟空13张战斗身体/武器 | 同版Role_v7（恢复SHA256 `11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7`），衣装143/152/160/186的selector1～6、武器177的selector1～7。沿用完整父矩阵、原viewBox(-100,-128,200,200)、逐片遮挡和已登记特效排除。复用`export_official_wukong_equipment.py`、原Godot栅格器，以密度2从SVG重栅格；使用接入前动作库及既有技能/待机映射覆盖所有源槽。 |
| 悟空装箱与时序 | 原174槽按全13图联合签名无损去重为86个共享槽（含透明格），六列15行、2400×6000；4个尾格补齐。`compact_atlases(...,cell_size=400,shared_layers=True)`保留同一身体/武器映射；`remap_frame_tracks`只替换原生动画的格索引，不重建时间或其他轨道。13×174=2262槽逐项对照原源2倍栅格全部相同或保持透明，未省略播放阶段。 |
| 唐僧13张战斗身体/武器 | 同版Role_v7，既有`source_audit.json`与本次XML散列相同；复用`export_official_tangseng.py`的selector冻结、两段衣装播放烘焙、完整父矩阵与深度遮挡。保留已审311源帧到身体100/武器37格的原映射；当前13图均从同源1倍精确重现，再2倍栅格，身体2400×6800、武器2400×2800。 |
| 两英雄26张专用背包图 | 同版backpack_v2：下载SHA256 `e4360195f803d7fe0b36f70c2ea88ade5e7fe9de7ffacde3d2fa7e0452c4a0f9`，恢复SHA256 `47215d42043801243e7beae00ca4601d6040b343be8bd8a29a9c4aa4e8df17ea`。复用`export_official_inventory_preview.py`，新增隔离输出及密度参数；Wukong252/62帧、Tangseng283/1帧的父矩阵/selector沿原登记。819源帧的1倍重现和2倍装箱均精确通过，原预览动画库字节保持，输出均2400×400。 |

正式登记经`Tools/LegacyMigration/install_official_hero_hd.py --work <本批目录>`，保留原路径、UID、实例结构及其他文本；图像必须先按52项散列匹配，原生资源只允许接入前字节或本次预期结果，防止重跑覆盖后续人工修改。脚本名说明作者工具用途，正式资源中没有并行hd版本。Source/原图/全部格与当前引用明细在本批manifest和[661项进度CSV](../Godot/GodotProject/.godot/validation/art_expansion_audit_20261114/hd_progress.csv)，运行命令和结果统一见Validation。

后续单件悟空外观重导使用原`export_official_wukong_equipment.py --frame-map <本批installed/同源图manifest.json> --density 2`复用86槽源映射，不拿已紧凑的正式motion库套旧174槽构造；原始母版复现则显式传`--motion-library <before/原动画库>`。两条路径仍由同一selector冻结/分层/栅格器完成，不建立第二套运行图集系统。已实测紧凑裸身重导与当前正式PNG精确相同。

本批没有AI重画、插值英雄、调整装备身份、裁人物或改地形。悟空少数1倍技能边缘延续历史800px画布差异，当前统一从原矢量高密度栅格，差异仅抗锯齿；详见Validation。独立技能效果、UI、敌人、数字及地形尚未高清。以1080p验收，4K全屏放大已获用户接受，未登记原生4K完成。

## A1b位图保真小样（2026-11-14，当前方法不采用）

天空来自已核验v25928内部主程序的bitmap18/DefineBitsJPEG2，原生JPEG1440×690，载荷SHA256=`fd69594dae5142aecc1bd822911793640e56d030a01e27e80942eea16685124a`；木棍复用`export_official_equipment.py --only rough_pilgrim_staff`，从同版OtherMat_v9的bitmap16/fall_ccxzg直接提取透明52×54。两个正式PNG均与原始载荷解码像素完全一致。高精度同内容源未在本次登记包中发现，不能把重提取算高清。

临时恢复候选使用[FSRCNN_Tensorflow固定提交](https://github.com/Saafke/FSRCNN_Tensorflow/tree/6a4812c4ef1c4f5947d79beafa32a05a6eb4a94d)的`FSRCNN_x2.pb`，SHA256=`366b33f0084c7b3f2bf6724f0a2c77bca94fcec9d7b6d72389d330073b380d5c`；OpenCV contrib4.12.0.88仅在忽略目录安装。该网络作亮度回归，色度按原图采样，alpha由原alpha独立Lanczos×2；插值基准使用相同alpha，避免黑底边缘污染。没有生成式重画、改字或扩图。

输出天空2880×1380、木棍104×108及原图/插值/候选对照只留`.godot/validation/art_a1b_20261114/`。天空实际显示收益不明显，木棍颜色偏移，当前恢复方法**不接入、不推广**，正式图与UID保留。命令、原生预览和数值见Validation；不将“有2倍PNG”登记为高清完成。

## A1a隔离高清重导小样（2026-11-14，英雄路线已获用户认可）

只制作`.godot/validation/art_a1a_20261114/`内候选，不替换正式资源；源SWF/XML、分帧PNG、manifest及临时复现脚本均留忽略目录。官方URL和固定版本沿用OfficialReference。本次Role_v7下载SHA256为`fdedcd581dcaa02406aab12bf137b2acaadef0402fe8d82b1e43ee85782b9582`，恢复后为既有`11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7`；v25928下载及内部主程序分别核验为既有`a5e9650c…f9ccce`、`f8e3e1f5…defb75b`，完整值见本批results.json及OfficialReference。

| 小样 | 来源、导出及逻辑映射 |
| --- | --- |
| 悟空裸身/默认木棍 | Role1/sprite575完整body父矩阵；衣装143/152/160/186与武器177冻结selector1。选529的8姿态、440的4姿态及211/272全部8/11帧。复用`export_official_wukong.py`的`freeze/matrix/split_pose`，保留246/205排除规则和逐片前景遮挡；`motion_poses`从正式动画库取当前走路格。被选身体/武器/遮挡SVG不含image或有效filter引用，独立攻击效果仍单独排除，不把其滤镜混入英雄。 |
| `hero_1x/`与`hero_2x/` | 同一分层SVG，viewBox(-100,-128,200,200)不变，Godot `Image.load_svg_from_string`密度1/2；武器片与遮挡在各自密度重新栅格并按原alpha公式裁切、合成。分帧200/400px；六列稀疏小样1200×1800/2400×3600，未选格透明，不是完整正式图集。manifest的`native_cell`为正式格、`cell`为样本格；走路120/124/128/132对应样本9/10/11/12。 |
| `huaguoshan_platform.png` | v25928内部主程序的shape56，经既有`shape_sprite(56,31056)`包装，FFDec原生`-zoom 1/2`透明PNG导出为263×75/526×150。shape bounds为(44209,3850)～(49469,5350)TWIPS，填充bitmap55/DefineBitsJPEG3，内嵌JPEG实际263×75；源矩阵scale20、translate(44209,3850)。正式平台与1倍重导RGBA完全相同。**2倍只作采样基准，未增加真实细节，不登记为高清通过。** 本次登记源与旧第一关三张素材未提供同内容更高精度源。 |

FFDec沿用本机26.3 jar，英雄栅格与预览使用Godot 4.7.2 .NET；没有AI生成、位图恢复、锐化、扩图、接缝改画或正式导入。平台继续保留原图，英雄仅本批代表姿态待审。具体复现命令和检查结果统一见Validation的A1a记录，方案见CrossPlatformPlan 2.12.13。

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

2026-11-11前端调整：按用户要求检索并导出了已核验造梦三Commonv3720的位图230（352×201灰黑面板），用户复查后认为灰色不适配主界面，明确改成黑色。最终删除/覆盖确认采用Godot原生StyleBoxFlat：近黑底、1px灰色细边、白色局内字体与开始菜单橙色悬停文字按钮；未保留该灰色素材替换，原图片文件还原，官方导出和候选图仅保留在忽略的验证目录。没有新正式美术资产，也不改其他场景共享Theme。选角底部按钮用原生深色StyleBoxFlat、2px边框与橙色悬停，通过居中HBoxContainer在单双人显示时自动居中；使用同一局内game_font.tres。

以下是旧确认框资产的历史来源；当前删除/覆盖场景已不引用棕色底板、字图和宋体，原文件保留供已有资源索引使用。

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

官方选角卡片来自OtherMat_v9的矢量与内嵌位图组合，直接渲染；人物、剪影、问号和笔刷为矢量，姓名与背景是位图，详见本页2026-11-15来源纠正记录：

| 目标 PNG（Sprites/UI/frontend） | 官方元件编号 | 原1倍 → 当前2倍AtlasTexture区域 |
| --- | --- | --- |
| `character_wukong_card.png` | DefineSprite 511 | (9, 0, 237, 610) → (18, 0, 474, 1220) |
| `character_sanzang_card.png` | DefineSprite 521 | (21, 0, 237, 610) → (42, 0, 474, 1220) |
| `character_bajie_card.png` | DefineSprite 515 | (15, 0, 237, 610) → (30, 0, 474, 1220) |
| `character_wujing_card.png` | DefineSprite 518 | (22, 0, 237, 610) → (44, 0, 474, 1220) |

复现入口仍为`Tools/LegacyMigration/export_official_character_cards.py`，当前正式密度2、早期密度1；使用隔离输出制作基线，实际命令见Validation本批记录。贴图自带姓名、笔刷、剪影和问号；AtlasTexture只排除原透明侧边。

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

早期由`Tools/LegacyMigration/import_equipment_assets.py`复制旧项目 `Art/ItemIcon/peach.png` 和`Bble.png`，现在两桃与蓝瓶已按本页顶部的原矢量方案接入。

旧工程的粗糙装备图标缺失，曾暂用普通图；现已删除这组临时替代图。当前38件装备的图标来源均为官方 [OtherMat_v9.swf](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/OtherMat_v9.swf)，恢复后 SHA-256 见 OfficialReference。原背包位图带黑色底板；按用户要求，背包和地面均使用对应的透明前景位图，粗糙/普通背包图沿用既有图标处理，未重绘装备。

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

战斗使用官方 `Role_v7.swf` 的 `export.hero.Role1`（sprite 575）动作；背包用backpack_v2独立图集。**当前尺寸/映射见本页A2记录：13图均已原路径替换为2400×6000。** 下述为早期1倍导入布局，用于追溯原源：六张早期图1200×5800、6×29、200px格，0..119为普攻/技能/待机，120..170为基础动作，171..173为空。A2只将这些原槽无损映射到共享86槽，原动画时序保持：

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
| `Sprites/Number/recovery_number_health.png` | 旧 `Art/AllNumber/cure/cure_0..9.png`，与OtherMat_v9 `bunum0..9`可见RGBA全等 | 原位图横向拼接，绿色恢复生命数字 |
| `Sprites/Number/recovery_number_mana.png` | 旧 `Art/AllNumber/CureMp/CureMp_0..9.png`，33×34/格 | 蓝色恢复魔法数字，保留现用版；与更小的官方`bulnum`区别见本页顶部 |
| `Sprites/UI/inventory/inventory_actions_background.png` | 造梦三 `Commonv3720.swf`，shape 369 | 87×115 菜单底板 |
| `inventory_action_equip.png` / `inventory_action_equip_disabled.png` | 同包 sprite 376，第 1/2 帧 | 77×31 装备按钮 |
| `inventory_action_gift_disabled.png` | 同包 sprite 367，第 2 帧 | 单人赠送禁用 |
| `inventory_action_gift.png` | 同包 sprite 367，第 1 帧 | 双人赠送可用 |
| `inventory_action_sell.png` | 同包 sprite 381，第 1 帧 | 出售按钮 |

恢复数字由 `Tools/LegacyMigration/gen_damage_numbers.gd` 拼接，与伤害数字、闪避共用 `Entitys/Effects/floating_text.tscn` 和 `FloatingTextNode`，表现规则见 OfficialReference。菜单由 `Tools/LegacyMigration/export_official_inventory_actions.py` 核对官方包散列后导出；2026-11-16复核确认仅369底板为纯矢量，五张操作按钮为sprite内位图组合，不能因SWF来源计矢量高清；均未截图或重绘。提示框按官方 `AttributeCon.as` 的字体、间距、颜色和装饰重建为原生 Godot 控件。

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
