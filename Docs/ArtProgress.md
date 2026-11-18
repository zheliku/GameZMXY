# 美术资源任务清单

更新时间：2026-11-17。本页持续维护逐项进度。**电脑美术主设计16:9，以1920×1080验收；手机主设计20:9，以2400×1080作预览参考，实际按设备像素输出。** 其他手机比例有限留边/裁切兼容；4K屏允许继续放大1080p标准，940×590玩法核心保持。

**图像源文件：165 / 673 已完成制作、接入及必要验证。本批原矢量高清12图；双英雄无双48时刻人物部分提升，光束/字样仍用原密度。全库349项来源/保真受限（含75项部分提升）、159项其他待处理；配套76/183。上一批特效已审，本批UI待审。**

另列183份配套美术资源，其中76份已完成密度联动、时序核对或必要显示验证，不计入图片完成量。

勾选含义：`[x]`表示该项对应的制作/接入/必要验证已完成；`[ ]`表示尚未完成。**完成实验、生成样稿或找到源文件，不等于素材高清通过；“等待用户审图”单独保留。** 原生SVG、字体和样式按1080p实际显示核对，需要时可保留原件，不强行栅格化或放大。

实施方案与分批顺序只维护在[CrossPlatformPlan 2.12](CrossPlatformPlan.md#212-美术前置阶段完整清单扩展规格与分批审查)；[LegacyAssetMap](LegacyAssetMap.md)记录来源，[Validation](Validation.md)记录验证命令、结果和退出诊断。本页只维护清单及完成状态。已审光晕接入见2.12.24，文字分离及真实精度限制见2.12.26，最新统一题签正式接入见2.12.29。显示边界仍见2.12.21：同平台全流程固定Project Settings，DisplaySystem只提供构图尺寸，不切换窗口；当前主任务仍为美术，不推进T3玩法适配；显示配置本身不计高清完成。

正式图片/Label/动画已接入TheGame；电脑/手机审查图与对照图是过程证据。用户要求删除文档图片副本，目录现已不存在；既有`.godot/validation`保留原始过程。地图真实高清仍未完成，清单勾选保持。

本轮实际程序复测：电脑1920×1080完整前端通过；手机20:9桌面模拟的地图布局及交互通过，进关因首段边界宽不足失败，T3仍未完成。具体命令和诊断见[Validation](Validation.md#双端审查图入库与正式接入复核2026-11-17)。

用户全屏截图反馈后的原生密度复核：Godot确实仍加载1408×590底图、138×93保存、112×114令牌，导入没有降采样。新Swin2SR Classical x2恢复候选已做同尺寸及三比例原生对照，地图仍软、入口仅有限改善，未替换正式资源或新增高清勾选。过程位置见Validation。

最新交付节奏：**一类一批，整类制作、接入、测试后停止，等用户审查通过再进入下一类。** 不按单个怪物逐一停审，也不得自动跨类继续。每批同步阶段勾选、分类计数和来源限制；不合格或仅插值的素材不勾完成。

**回退历史（2026-11-17，后续本轮修复见2.12.35）：** 已撤回Boss之后连续跨类接入的资源和关联代码，恢复本页逐项清单至67/667、配套43/182；不沿用撤回批次的完成勾选。用户已确认保留普通怪物整类、锚点和英雄preview修复，当前停在普通怪物整批审查点；回退范围确认不等于本批画面通过。撤回的候选、来源和测试保存在忽略的验证目录及art_batch_rollback_20261117备份中，未丢弃；后续按本页分类逐批重新交付。本轮不新增高清成果。

**双端显示验收补充（最新标准）：** 主构图电脑16:9、手机20:9；19:9、19.5:9、21:9及范围内16:9手机沿用20:9构图作兼容，允许有限留边/裁外围，不逐机重排卡宽。选角四卡紧贴并铺满两套主画布，拓宽背景、核心角色和墨框边角/厚度保持；姓名改为项目字体的独立Label、锚定原相对位置。此规则取代上下黑框裁切样稿。已有高清勾选不等于Android整屏体验已验，详见CrossPlatformPlan 2.12.17。

## 目前做了什么

- [x] UI整类230项逐项复核候选、正式引用和源精度；12张矢量原路径2倍接入，新增1张共享光束图集，当前UI清单231项。
- [x] 悟空/唐僧无双原各24帧人物源重导；共享原密度光束和原暗幕延伸两侧，保持原遮挡、原点、24fps和暂停恢复。
- [x] 1920/2400/2520×1080合计5958项原生检查、138张图；实际1080p背包技能、悟空及唐僧无双专项通过。
- [ ] 本批UI画面审查；地图/技能底图和无双字图等位图高清、Android真机继续未完成。

- [x] 独立特效189项查源；84图/333源时刻原路径接入，83纯矢量图完整高清、1混合图部分；104位图与1未确认源保持原图。
- [x] 修正掉落球等高密度滤镜核/相位，保持原光晕和共同原点；最大新纹理2739，理论RGBA8约36.79MiB；原生三比例1998项/60图及五个实际专项通过。
- [x] 上一批特效画面获认可；混合位图、原位图/未确认源及Android真机仍未完成。

- [x] 本批Boss整类30项查源，12套原路径2倍密度接入（11套2058时刻＋唐僧被抓130时刻）；原点/动画/碰撞保持，新增6页使所有本批新纹理每边≤4096。
- [x] 原生4189项/73张1080p和22张手机比例图、实际Boss攻击/死亡/捕获/回池专项通过，详见Validation。
- [x] Boss上一批画面已获认可；9套混合图与18项位图效果/石像的真实细节恢复、Android真机/性能预算仍未完成。

- [x] 两英雄完整身体preview接入：悟空100时刻/13身体姿态、唐僧91时刻/10姿态，原源2倍像素、独立武器和24fps保持；26PNG原路径已改，原格/源像素逐项核对通过。
- [x] 真实普通背包自然播放、暂停/覆盖暂停、换装/回池重开通过；原生144装备组合/191动作时刻共2206项，角色尺度/注册/遮挡保持。[实际动作对照](../Godot/GodotProject/.godot/validation/art_preview_animated_idle_20261117/actual_motion_contact.png)。
- [x] 地图入口纠正为只恢复完整背景、不恢复烘焙文字：保存normal/hover及技能图仅原字形RGB归黑，alpha/墨底和遮罩外图形全等；文字用三原生Label，旧额外Ink层删除，save_hover有实际用途保留。三比例验证记录见Validation。
- [x] 全项目666图/1172引用文件/2535裁切区静态排查，无越界；13处透明变化对应既有悟空三槽等价置换、1170格RGBA全等。重复时序帧、原始来源图不批量删除。
- [ ] journey_map真实高清仍未完成：当前1408×590只扩外围，重新读取原包bitmap388仍为940×590；不以插值升尺寸交付。其余来源受限项和Android真机继续未完成。
- [ ] 手机20:9完整进关未通过：首段1139.9小于实际约1311逻辑像素视野；按计划留待T3校准，不将地图铺满验收等同整局兼容通过。

- [x] 最新按类别整批交付：普通怪物13物种及4个目录内专属效果已查源，新增12图集原路径密度接入；其中8图完整高清、4图位图部分未完成，另4个纯/主要位图资源保持原图，旧15张分动作图保持。
- [x] 悟空13张战斗图首格恢复原待机；只置换0/1/85三格，透明技能格移至85；1170格RGBA全等、动画仅改对应帧索引，六列/200逻辑格保持。
- [x] 修复独立hero_preview无驱动：预览节点自身按真实时间推进原生播放器，背包和独立场景共用；删除窗口与面板转发；暂停下四姿态及金光6帧自然播放通过。
- [x] 整批1002源帧、2059次原生帧/双向/原点及物理检查、55张1080p预览完成；妖猴另30张电脑/20:9/21:9预览通过。
- [ ] 本批来源受限8图的同内容高精度源；Android真机/预算及全关宽屏验收仍未完成。

整批查看：[普通怪物总览](../Godot/GodotProject/.godot/validation/art_ground_batch_install_20261116/monster_batch_overview.png)、[悟空首格修正](../Godot/GodotProject/.godot/validation/art_atlas_contract_repair_20261117/wukong_first_slot_repair.png)。方案/来源/命令见CrossPlatformPlan 2.12.33、LegacyAssetMap和Validation。

- [x] 盘点原661个图像源文件并检查引用/尺寸；累计保留3张墨框/1张背景/1张东海动画分图页，冗余题签墨底本轮删除后666项与实际目录逐项对应。
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
- [x] 用户认可当前英雄/背包52图批次并授权继续下一批（2026-11-15）。
- [x] 四张选角卡的矢量人物、剪影、问号与笔刷按原源2倍重导，原路径接入；四栏仍235×590，原姓名/背景内容保持。
- [x] 选角卡4/4原密度重导RGBA完全一致；12张固定输出对照、四栏定位、完整前端悬停/选角/双人重选/返回验证通过。
- [x] 核实四张选角卡混合位图来源，纠正“整张纯矢量”的历史描述；登记本批4项部分完成和来源限制。
- [x] 按用户要求移除烘焙姓名，接入项目字体的4个独立Label，保留原相对位置。
- [ ] 共享背景的同内容高精度来源/保真改善；当前仍保留源位图细节，不计整卡高清完成。
- [x] 用户认可分层加宽、独立字体及漏色修正版并授权正式接入（2026-11-15）。
- [x] 用户认可地图外围；一张共享背景原路径接入，电脑/手机按外围裁取，554600个原中心像素RGBA差0。
- [x] Project Settings保留940×590逻辑/1920×1080窗口及Android20:9构图配置；同平台菜单与战斗全程KeepHeight，删除所有临时Keep切换，真实前端验证配置保持。
- [x] 地图技能/保存/设置沿现有根节点左锚点贴左，6次目标尺寸/回缩检查、10个地点原几何及完整前端通过。
- [ ] 地图真高清：2816×1180 EDSR非生成式恢复对照已出，改善有限、未接入；更大尺寸和高相似度不等于高清通过。

已完成这批的[1080p前后对照](../Godot/GodotProject/.godot/validation/art_a2_heroes_20261114/hero_review_contact.png)和[当前完整预览](../Godot/GodotProject/.godot/validation/art_a2_heroes_20261114/heroes_1920x1080_current.png)。完整验证见[Validation本批记录](Validation.md#美术-a2两英雄完整外观及专用背包2026-11-14原路径替换停审)。

本批选角卡：[1080p同尺寸裁片对照](../Godot/GodotProject/.godot/validation/art_a2_character_cards_20261115/cards_1080p_comparison.png)、[1920×1080原版](../Godot/GodotProject/.godot/validation/art_a2_character_cards_20261115/cards_1920x1080_original_wukong.png) / [当前版](../Godot/GodotProject/.godot/validation/art_a2_character_cards_20261115/cards_1920x1080_current_wukong.png)。固定SubViewport预览包含keep留边；真实流程截图和命令见[Validation](Validation.md#美术-a2四张选角卡2026-11-15矢量部分接入位图部分受限)。

最新正式流程：[选角1920×1080](../Godot/GodotProject/.godot/validation/art_a1c_map_install_20261115/character_actual_windowed.png)、[选角4K全屏](../Godot/GodotProject/.godot/validation/art_a1c_map_install_20261115/character_actual_fullscreen.png)、[地图1920×1080](../Godot/GodotProject/.godot/validation/art_a1c_map_install_20261115/map_actual_windowed.png)、[地图4K全屏](../Godot/GodotProject/.godot/validation/art_a1c_map_install_20261115/map_actual_fullscreen.png)。这些由实际Procedure流程采集，菜单已铺满；此前[普通keep截图](../Godot/GodotProject/.godot/validation/art_a2_character_install_20261115/character_form.png)是修复前证据。手机画布检查：[地图20:9](../Godot/GodotProject/.godot/validation/art_a1c_map_install_20261115/map_shared_2400.png)、[地图21:9](../Godot/GodotProject/.godot/validation/art_a1c_map_install_20261115/map_shared_2520.png)，不是Android真机验收。

## 后续阶段任务

地图外围A1c[原图/扩图对照](../Godot/GodotProject/.godot/validation/art_a1c_map_20261115/map_review_comparison.png)已获审查通过并接入。新的[原图/插值/EDSR同逻辑1080p裁片对照](../Godot/GodotProject/.godot/validation/art_a1c_map_hd_20261115/map_hd_core_comparison_1080p.png)只用于高清恢复评估；森林/岩石仍软，暂不采用，不将2倍候选计为高清完成。

本轮审图修正版：[手机20:9漏色修正](../Godot/GodotProject/.godot/validation/art_a2_character_layout_20261115/cards_android_2400_mask_fixed.png)、[电脑16:9修正版](../Godot/GodotProject/.godot/validation/art_a2_character_layout_20261115/cards_windows_1920_mask_fixed.png)、[电脑旧图/加宽样稿数值对照](../Godot/GodotProject/.godot/validation/art_a2_character_layout_20261115/desktop_width_comparison.png)。

- [x] 核对黑边来源和四种比例；完成原生标题分区/选角布局讨论样稿，按用户反馈废弃四卡等距拉开方案。
- [x] 制作16:9～21:9四张连续四联画移动端裁切/黑框候选，仅供布局审查，不替换正式资源。
- [x] 用户最新制作方法已登记：四卡加宽/贴合铺满，原角色核心和墨框保持，去掉烘焙姓名并用项目字体独立排版；不再采用上下黑框裁切方案。
- [x] 按电脑16:9/手机20:9重出加宽/独立文字两主设计及四种手机兼容情况，共12张候选/现状对照；SVG核心保护像素RGBA差0、姓名字段清空、NinePatch边角/厚度和字体相对锚点验证通过。
- [x] 用户认可本批加宽/独立字体总体画面，并提出卡顶/卡缝漏红和电脑宽度两项问题。
- [x] 恢复原背景遮罩并修正漏红，6种候选顶部/接缝扫描通过；补电脑16:9数值对照（旧430px/卡、左右100px → 样稿480px/卡、画布外留边0）。
- [x] 用户认可漏色修正版；本批正式选角接入完成。标题分区仍单独待审。
- [x] 审定分层卡面/字体接入原CharacterForm：3倍人物、3张共用/变体墨框、1张原生背景，原遮罩、四卡紧贴和原生交互/复用验证通过；人物核心不横向拉伸，理论RGBA8纹理52.50MiB。
- [x] 正式C#场景独立画布6种目标/连续resize、12条布局PASS，完整前端及339/197/19规则测试通过。
- [x] 电脑/Android构图配置只由应用级DisplaySystem读取和换算，CharacterForm只消费MenuDesignSize；不再直接读取设置键、判断设备或维护UiLayoutSettings。
- [x] 选角/地图正式流程全屏铺满；DisplaySystem和ProcedureLevel已删除窗口策略写入/调用，LevelViewController不改Window；当前完整前端实测进花果山配置不变。其他关卡宽屏仍待T3验收，不能按前端PASS勾全关通过。
- [x] 修复ezpz_inspector临时Resource.Free及ComponentInsoector卸载同类错误；实际编辑器启动/Resource与Control检查通过，既有退出诊断单列Validation。
- [ ] 其余页面、关卡世界宽屏和Android真机：仍按T2～T5实施，不以两页菜单通过代替全游戏显示验收。
- [ ] 后续每批附电脑16:9/手机20:9两主设计及其他手机比例兼容、可读性/触达/安全区预览与纹理成本；Android真机另验，不以静态预览替代。
- [ ] UI、头像、选角卡、地图地点、技能与物品入口：查源并逐批完成高清/原生显示验收。
- [ ] 英雄状态、小怪、Boss及装备外观配套效果：完整动作重导，保持源时序、装备身份和遮挡。
- [ ] 独立战斗特效、球、升级、死亡、出口动画：核对原生滤镜、透明度、注册点及全部动画帧。
- [ ] 装备/消耗品/地面图标：逐项核对高精度同内容源；来源不足或保真不通过时明确记录。
- [ ] 伤害/恢复数字和字图：保留字形、顺序、基线及比例字宽，完成1080p实际显示检查。
- [ ] 11关背景、地形、前景和平台：逐项查矢量/位图依赖，保留定位、接缝和碰撞。
- [ ] 字体、着色器、动画资源、主题/样式：按下方配套资源逐项完成联动或实际显示检查。
- [x] A1c准备：核实地图上游SelectPLace505→shape389→JPEG388仍940×590；冻结原PNG中心，准备1408×590画布/遮罩、554600像素RGBA差0及6比例裁切计划，原地点坐标不动。
- [x] A1c候选：复用已授权本地ECPA/原gpt-image-2官方CLI，完成1408×590原密度外围扩图、554600中心像素差0、100%/200%接缝图及6目标×原图/候选共12张原生场景预览；原地图控件几何不变。
- [x] 用户审查通过A1c地图外围并授权下一步；原路径接入1408×590共享原密度扩图、保留原中心/.import/UID，左菜单及正式菜单铺满验证完成。
- [x] 针对1080p密度另制2816×1180非生成式EDSR小样，原图/普通插值/恢复候选同逻辑对照及9张原生场景输出完成，未替换正式资源。
- [ ] 地图真实高清验收：原940×590中心缺更高精度同内容来源，EDSR收益有限；暂保留正式原密度扩图，高清完成不勾。
- [x] A2地图地点查源：10地点×普通/悬停/当前共30图，沿SelectPLace505实际字段/三状态追溯；23张位图、2张混合、2张纯矢量当前动画、3张全透明输入状态已逐项登记。
- [x] 浅岸/东海原源1倍重导6/6与正式RGBA一致；2倍原源导出、38帧/24fps/注册点/原输入图核对及18张双端原生场景对照完成。
- [x] 上轮地点滤镜失败结论已登记：直接zoom2与补偿半径方案均改变原光晕，未接入；试验留忽略目录，正式导出器和全部素材原字节保持。
- [x] 按用户要求继续本批光晕：核对FFDec26.3原实现，定位模糊预算自动缩半径和整数核中心采样，废弃源半径额外倍乘方法。
- [x] 完成浅岸/东海2倍光晕采样修正版：原密度6图全等、38帧/24fps/注册点/原输入图核对，18张双端原生图和严格24fps帧对照动图已出；仅离线候选。
- [x] 用户审查通过光晕采样修正版（2026-11-16）：[原图/2倍同逻辑尺寸](../Godot/GodotProject/.godot/validation/art_a2_map_glow_20261116/glow_hover_comparison.png)、[全部帧动图](../Godot/GodotProject/.godot/validation/art_a2_map_glow_20261116/glow_pulse_comparison.png)、[双端预览](../Godot/GodotProject/.godot/validation/art_a2_map_glow_20261116/map_glow_review.png)。悬停alpha总量差约0.25%/0.12%；弱动画帧仍最多约5.25%/2.36%差，用户已接受；正式接入见下一项。
- [x] 已审浅岸/东海光晕原路径接入；38帧RGBA可还原、原24fps/全部时刻/注册点保持，301774原命中遮罩像素与真实鼠标检查通过。
- [x] 东海current拆成两页2832×2380，浅岸单页3060×2264；原生texture轨切页，最大单边3060，无删帧/新加载机制，理论current显存减少15.47%。
- [x] 默认1倍9图RGBA全等、76次原生帧/页、24张双端原生对照、构建/引用/格式检查完成；清单与来源/验证更新。
- [ ] 地图地点整体高清：其余位图源及两处hover文字仍受限；本批两张current与新增分图页完成，地图透明normal不为凑数量增密。完整海域进关回归受既有T3阶段宽度阻断，Android真机未验。

本批接入结果：[原图/当前双端对照](../Godot/GodotProject/.godot/validation/art_a2_map_glow_install_20261116/installed_review.png)、[电脑1920×1080](../Godot/GodotProject/.godot/validation/art_a2_map_glow_install_20261116/map_1920_installed_hover.png)、[手机20:9](../Godot/GodotProject/.godot/validation/art_a2_map_glow_install_20261116/map_2400_installed_hover.png)。本批停止，不自动开始下一类。

- [x] A2地图入口4图查源：技能为混合矢量/位图、保存两态为小位图组合、令牌旧原图精确对应；保存普通图与官方重导不全等，保护当前书本。
- [x] 按用户要求制作统一题签小样：保存/学习技能改独立Godot Label，复用令牌字体、20号字/金色/黑白描边；3张候选alpha及字形遮罩外RGBA保持，40张双端原生图、3个按钮点击和1/15/19帧24fps检查完成。
- [x] 用户认可本批题签画面并指出令牌位置偏下：[1080p原尺寸裁片对照](../Godot/GodotProject/.godot/validation/art_a2_map_menu_20261116/labels_detail_1080p.png)、[电脑16:9](../Godot/GodotProject/.godot/validation/art_a2_map_menu_20261116/map_1920_split_hover_end.png)、[手机20:9](../Godot/GodotProject/.godot/validation/art_a2_map_menu_20261116/map_2400_split_hover_end.png)。原分层样稿已认可，位置修正及正式接入见下一项；4张图图形仍受限，3张去字图只记部分完成，不将EDSR试验算高清。
- [x] 本批题签正式接入与位置修正：令牌文字右移2/上移5逻辑px，三个水平中心统一x77.5；新增2个Label，原3PNG去字、1/15/19帧24fps、按钮/图片/10地点几何保持。
- [x] 原图/.import/UID及35帧库保留，Ge补1绑定；20张正式比例/状态图、完整map-buttons-only前端（含1080p/4K、技能题签生命周期和回池复用）、构建/格式/引用通过。
- [ ] **地图底图与菜单图标真正高清：仍未完成。** 当前journey_map1408×590、保存138×93、令牌112×114，卷轴源59×33/58×37；用户反馈全屏模糊属实。本批只提升文字，不把输出1920×1080或4K当源细节增加。

本批当前：[文字位置对照](../Godot/GodotProject/.godot/validation/art_a2_map_menu_install_20261116/installed_labels_comparison.png)、[带参考线位置修正](../Godot/GodotProject/.godot/validation/art_a2_map_menu_install_20261116/caption_alignment_guides.png)、[电脑1920×1080](../Godot/GodotProject/.godot/validation/art_a2_map_menu_install_20261116/map_1920_installed_hover_end.png)、[手机20:9](../Godot/GodotProject/.godot/validation/art_a2_map_menu_install_20261116/map_2400_installed_hover_end.png)。

- [x] 底图/入口图标续研：补查4331张本地图像路径与官方返回按钮矢量元件；未发现可确认的更大同内容原件，官方244是文字而非令牌。
- [x] 完成地图/保存两态/令牌原图约束2倍恢复小样、1080p/200%对照和12张16:9/20:9/21:9原生图；最终视觉尺寸与原布局保持，原PNG/场景/代码/导入/Git索引不改。
- [ ] 位图恢复候选尚未采用、图形高清未通过：书本/令牌仅有限改善，地图森林/岩石仍软；回采样一致性不证明真实高清。高清仍58，新增题签墨底后总数667，详见CrossPlatformPlan 2.12.27/2.12.29。

本批恢复小样：[原生1080p图标对照](../Godot/GodotProject/.godot/validation/art_bitmap_consistency_20261116/native_icons_comparison_1080p.png)、[地图同逻辑尺寸对照](../Godot/GodotProject/.godot/validation/art_bitmap_consistency_20261116/map_detail_1080p.png)、[电脑1920×1080候选](../Godot/GodotProject/.godot/validation/art_bitmap_consistency_20261116/map_1920_candidate_hover_end.png)、[手机20:9候选](../Godot/GodotProject/.godot/validation/art_bitmap_consistency_20261116/map_2400_candidate_hover_end.png)。均为离线小样，不代表正式图已升级。

- [x] 按用户反馈复核三个入口的文字与黑框：确认原墨底137/137/112逻辑px宽不一，旧Label坐标相同不能证明视觉对齐。
- [x] 完成统一墨底/相对文字基线修正版小样：三个Label复用同一138×36逻辑墨底，实际字形bbox中心差≤0.25逻辑px；24张原生比例/状态图、字体独立渲染、四图源题签遮罩外RGBA保护核对通过。
- [x] 用户通过三入口统一题签修正版并授权按此版接入（2026-11-16）。
- [x] 三入口统一题签正式接入：四图原路径去旧墨底、共用一张138×36逻辑墨底及相对文字基线；原按钮/主体/35帧24fps及旧.import/UID保持，业务代码不改。
- [x] 20张正式比例/状态图、12张已审/接入像素全等、三次原生题签点击及真实GGF前端按钮/保存/失败保留/回池复用通过；构建、格式和引用通过。清单已增加新墨底为第667项，配套表按已有逐项勾选修正17/182汇总。
- [ ] 图标主体及共用墨底高清验收继续未完成。**本批只完成题签一致性，高清数量58，当前总量667**；地图/书本/卷轴/令牌的原生细节没有提高。

最新位置修正版：[带参考线前后对照](../Godot/GodotProject/.godot/validation/art_map_caption_uniform_20261116/caption_uniform_comparison_1080p.png)、[四状态预览](../Godot/GodotProject/.godot/validation/art_map_caption_uniform_20261116/caption_uniform_states.png)、[电脑1920×1080](../Godot/GodotProject/.godot/validation/art_map_caption_uniform_20261116/map_1920_uniform_hover_end.png)、[手机20:9](../Godot/GodotProject/.godot/validation/art_map_caption_uniform_20261116/map_2400_uniform_hover_end.png)。本批只审题签一致性，原源低清限制保持。

正式接入：[已审/正式对照](../Godot/GodotProject/.godot/validation/art_map_caption_install_20261116/installed_review.png)、[电脑1920×1080](../Godot/GodotProject/.godot/validation/art_map_caption_install_20261116/map_1920_installed_hover_end.png)、[手机20:9](../Godot/GodotProject/.godot/validation/art_map_caption_install_20261116/map_2400_installed_hover_end.png)。实际流程1920窗口/4K全屏、6次尺寸往返与诊断见Validation；原设置动画实为0.9→1.08提亮且scale保持1，修正之前沿用的1.04缩放描述。

- [x] A2后续UI/数字查源：六张背包操作图原密度重导RGBA全等；仅底板369是矢量，五按钮及四种当前伤害/闪避数字均为原生位图，纠正旧来源描述，新增9项来源受限。
- [x] 花果山猴兵official_body查源与2倍小样：五动作69帧原密度重现可见RGBA全等，原源2倍重导仍69帧/23姿态/24fps，无位图依赖或滤镜，原落地注册修正保持。
- [x] 猴兵原生场景双朝向138帧检查、15张电脑16:9/手机20:9及21:9预览、同逻辑1080p/全身200%对照与严格24fps动图完成；最小接入方案见CrossPlatformPlan 2.12.30。
- [x] 用户通过猴兵小样并要求按最小方案接入（2026-11-16）。
- [x] 猴兵原路径2倍正式接入：69帧/23姿态和UID保持，原SpriteFrames仅换像素region/margin，身体视觉scale0.5及6条offset键换算；判定/方法/时序/根尺度保持。
- [x] 修正共用SetFacing重设视觉缩放的问题；138次原生真实转向检查、30张双端/双向图、五物种真实GGF攻击/受击/回池及三物种60/120Hz死亡六组通过，构建/格式/引用检查完成。
- [x] 本批计猴兵official_body 1张高清图像及2份配套资源完成：**59/667、19/182**；旧5张未使用单动作图保持未勾，位图来源限制与Android真机项目仍未完成。

已审猴兵小样：[1080p原图/插值/2倍](../Godot/GodotProject/.godot/validation/art_monkey_sample_20261116/monkey_comparison_1080p.png)、[全身200%对照](../Godot/GodotProject/.godot/validation/art_monkey_sample_20261116/monkey_fullbody_200percent.png)、[24fps双朝向动图](../Godot/GodotProject/.godot/validation/art_monkey_sample_20261116/monkey_motion_24fps.png)、[电脑1920×1080](../Godot/GodotProject/.godot/validation/art_monkey_sample_20261116/monkey_1920_attack_1.png)、[手机20:9](../Godot/GodotProject/.godot/validation/art_monkey_sample_20261116/monkey_2400_death.png)。本版已通过并接入，正式结果见下一条，地图等位图限制保持。

- [x] 妖猴查源/当前消费者核对：原Monster_v1五动作78帧24fps、20共享姿态，无位图/滤镜；历史88帧含旧after_hurt，不是本批删帧。
- [x] 妖猴1倍78帧正式可见RGBA/alpha复现；解决原导出最小描边锁定输出1px导致2倍变细，临时167条线宽规范前后1倍78帧完整RGBA全等，矢量路径/颜色/深度/时间轴保持。
- [x] 妖猴最终2倍完整动作小样、156帧装箱解包、真实SetFacing双向156帧/204次原点及物理检查、30张电脑/手机预览和严格24fps动图完成；未改正式图或运行代码。
- [x] 用户审查妖猴最终保真小样并授权接入；当前alpha归一比1.004414～1.007418、质心最大差0.05385逻辑px，仍需视觉认可。
- [x] 妖猴已原路径接入并恢复原规则网格整体2倍，78帧和共同注册点保持；正式战斗/受击/死亡/池复用回归通过。当前全库计数见页首。

妖猴历史小样（后已通过并整批接入）：[1080p对照](../Godot/GodotProject/.godot/validation/art_demon_monkey_sample_20261116/demon_monkey_comparison_1080p.png)、[全身200%](../Godot/GodotProject/.godot/validation/art_demon_monkey_sample_20261116/demon_monkey_fullbody_200percent.png)、[24fps动图](../Godot/GodotProject/.godot/validation/art_demon_monkey_sample_20261116/demon_monkey_motion_24fps.png)、[手机20:9](../Godot/GodotProject/.godot/validation/art_demon_monkey_sample_20261116/demon_monkey_2400_attack_1_right.png)。本批方案见CrossPlatformPlan 2.12.32，正式五张旧分动作图保持。

猴兵正式接入：[原图/正式2倍同尺寸](../Godot/GodotProject/.godot/validation/art_monkey_install_20261116/installed_review_1080p.png)、[电脑1920×1080](../Godot/GodotProject/.godot/validation/art_monkey_install_20261116/monkey_1920_attack_1_left.png)、[手机20:9](../Godot/GodotProject/.godot/validation/art_monkey_install_20261116/monkey_2400_attack_1_right.png)。以真实SetFacing保留原offset随朝向镜像；首批FlipH静态小样不代替实机转向验证。详见CrossPlatformPlan 2.12.31和Validation。

- [ ] A3：其余静态UI背景与五张共享天空的外围扩图；选角按连续四联画方案，不强制卡间/两侧新增环境；只在原画外新增环境。
- [ ] A4：两英雄48帧无双演出覆盖/高清及四张短关条件候选，取景裁决与实际效果分别验收。
- [ ] A5：汇总全库高清/仅扩图/保留原图结论和运行纹理预算，完成总审查。
- [ ] T2～T5余项：除本批选角/地图菜单已接入外，其余UI、关卡世界宽屏、触屏、导出和真机仍按计划实施。

这些是阶段任务，不额外计入下方文件总数。未来新增素材在实际创建时另行入表，只计入本轮实际存在的文件，不预先计入未来素材；选角侧景不再作为必须新增的资源。

## 图像完成量总览

| 分类 | 图像数 | 已完成实施 | 来源/保真限制未完成 | 其他待处理 |
| --- | ---: | ---: | ---: | ---: |
| [悟空战斗身体与武器](#art-images-01) | 13 | 13 | 0 | 0 |
| [唐僧战斗身体与武器](#art-images-02) | 13 | 13 | 0 | 0 |
| [悟空背包专用预览](#art-images-03) | 13 | 13 | 0 | 0 |
| [唐僧背包专用预览](#art-images-04) | 13 | 13 | 0 | 0 |
| [英雄状态与角色配套贴图](#art-images-05) | 4 | 0 | 0 | 4 |
| [敌人、Boss及其图集](#art-images-06) | 66 | 12 | 39（含19项部分提升） | 15 |
| [独立特效、掉落球、升级与演出](#art-images-07) | 189 | 83 | 105（含1项部分提升） | 1 |
| [UI、头像、地图、技能面板与无双画面](#art-images-08) | 231 | 18 | 198（含55项部分提升） | 15 |
| [装备、消耗品与地面图标](#art-images-09) | 79 | 0 | 1 | 78 |
| [伤害、恢复数字与文字图集](#art-images-10) | 10 | 0 | 4 | 6 |
| [关卡背景、地形、平台与前景](#art-images-11) | 37 | 0 | 2 | 35 |
| [主题SVG、工程图标与其他图像](#art-images-12) | 5 | 0 | 0 | 5 |
| **合计** | **673** | **165** | **349（含75项部分提升）** | **159** |

## 全部图像文件（673项）

以下链接均指向当前正式文件；每个文件只列一次。目录相对`Godot/GodotProject/`；尺寸是本轮读取的当前源像素尺寸。`.import`与`.uid`随源资源维护，不作为第二张图重复列项。

<!-- art-images:start -->

<a id="art-images-01"></a>

### 悟空战斗身体与武器（13/13）

目录：`TheGame/Sprites/Characters/Heroes/wukong/`

- [x] [wukong_body_bare.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_body_bare.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [wukong_body_black_tortoise.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_body_black_tortoise.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [wukong_body_blood_sea.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_body_blood_sea.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [wukong_body_earth_fiend.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_body_earth_fiend.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [wukong_body_excellent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_body_excellent.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [wukong_body_pilgrim.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_body_pilgrim.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [wukong_weapon_azure_dragon.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_azure_dragon.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [wukong_weapon_default.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_default.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [wukong_weapon_excellent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_excellent.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [wukong_weapon_heavenly_fiend.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_heavenly_fiend.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [wukong_weapon_heirloom.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_heirloom.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [wukong_weapon_pilgrim.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_pilgrim.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [wukong_weapon_ruyi.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_ruyi.png) — 1200×5800 → 2400×6000；原源2倍重导、已接入/验证，本批已获认可继续。

<a id="art-images-02"></a>

### 唐僧战斗身体与武器（13/13）

目录：`TheGame/Sprites/Characters/Heroes/tangseng/`

- [x] [tangseng_body_bare.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_body_bare.png) — 1200×3400 → 2400×6800；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [tangseng_body_cassock.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_body_cassock.png) — 1200×3400 → 2400×6800；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [tangseng_body_excellent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_body_excellent.png) — 1200×3400 → 2400×6800；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [tangseng_body_heavenly_fiend.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_body_heavenly_fiend.png) — 1200×3400 → 2400×6800；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [tangseng_body_heirloom.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_body_heirloom.png) — 1200×3400 → 2400×6800；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [tangseng_body_kirin.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_body_kirin.png) — 1200×3400 → 2400×6800；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [tangseng_weapon_blood_sea.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_weapon_blood_sea.png) — 1200×1400 → 2400×2800；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [tangseng_weapon_default.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_weapon_default.png) — 1200×1400 → 2400×2800；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [tangseng_weapon_earth_fiend.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_weapon_earth_fiend.png) — 1200×1400 → 2400×2800；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [tangseng_weapon_excellent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_weapon_excellent.png) — 1200×1400 → 2400×2800；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [tangseng_weapon_nine_ring.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_weapon_nine_ring.png) — 1200×1400 → 2400×2800；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [tangseng_weapon_pine.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_weapon_pine.png) — 1200×1400 → 2400×2800；原源2倍重导、已接入/验证，本批已获认可继续。
- [x] [tangseng_weapon_white_tiger.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_weapon_white_tiger.png) — 1200×1400 → 2400×2800；原源2倍重导、已接入/验证，本批已获认可继续。

<a id="art-images-03"></a>

### 悟空背包专用预览（13/13）

目录：`TheGame/Sprites/Characters/Heroes/wukong/preview/`

- [x] [wukong_preview_body_bare.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_body_bare.png) — 2400×1200；复用原源2倍格的完整待机预览，100时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [wukong_preview_body_black_tortoise.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_body_black_tortoise.png) — 2400×1200；复用原源2倍格的完整待机预览，100时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [wukong_preview_body_blood_sea.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_body_blood_sea.png) — 2400×1200；复用原源2倍格的完整待机预览，100时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [wukong_preview_body_earth_fiend.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_body_earth_fiend.png) — 2400×1200；复用原源2倍格的完整待机预览，100时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [wukong_preview_body_excellent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_body_excellent.png) — 2400×1200；复用原源2倍格的完整待机预览，100时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [wukong_preview_body_pilgrim.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_body_pilgrim.png) — 2400×1200；复用原源2倍格的完整待机预览，100时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [wukong_preview_weapon_azure_dragon.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_azure_dragon.png) — 2400×800；复用原源2倍格的完整待机预览，100时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [wukong_preview_weapon_default.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_default.png) — 2400×800；复用原源2倍格的完整待机预览，100时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [wukong_preview_weapon_excellent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_excellent.png) — 2400×800；复用原源2倍格的完整待机预览，100时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [wukong_preview_weapon_heavenly_fiend.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_heavenly_fiend.png) — 2400×800；复用原源2倍格的完整待机预览，100时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [wukong_preview_weapon_heirloom.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_heirloom.png) — 2400×800；复用原源2倍格的完整待机预览，100时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [wukong_preview_weapon_pilgrim.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_pilgrim.png) — 2400×800；复用原源2倍格的完整待机预览，100时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [wukong_preview_weapon_ruyi.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_ruyi.png) — 2400×800；复用原源2倍格的完整待机预览，100时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。

<a id="art-images-04"></a>

### 唐僧背包专用预览（13/13）

目录：`TheGame/Sprites/Characters/Heroes/tangseng/preview/`

- [x] [tangseng_preview_body_bare.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_body_bare.png) — 2400×800；复用原源2倍格的完整待机预览，91时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [tangseng_preview_body_cassock.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_body_cassock.png) — 2400×800；复用原源2倍格的完整待机预览，91时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [tangseng_preview_body_excellent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_body_excellent.png) — 2400×800；复用原源2倍格的完整待机预览，91时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [tangseng_preview_body_heavenly_fiend.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_body_heavenly_fiend.png) — 2400×800；复用原源2倍格的完整待机预览，91时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [tangseng_preview_body_heirloom.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_body_heirloom.png) — 2400×800；复用原源2倍格的完整待机预览，91时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [tangseng_preview_body_kirin.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_body_kirin.png) — 2400×800；复用原源2倍格的完整待机预览，91时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [tangseng_preview_weapon_blood_sea.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_weapon_blood_sea.png) — 2400×800；复用原源2倍格的完整待机预览，91时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [tangseng_preview_weapon_default.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_weapon_default.png) — 2400×800；复用原源2倍格的完整待机预览，91时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [tangseng_preview_weapon_earth_fiend.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_weapon_earth_fiend.png) — 2400×800；复用原源2倍格的完整待机预览，91时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [tangseng_preview_weapon_excellent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_weapon_excellent.png) — 2400×800；复用原源2倍格的完整待机预览，91时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [tangseng_preview_weapon_nine_ring.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_weapon_nine_ring.png) — 2400×800；复用原源2倍格的完整待机预览，91时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [tangseng_preview_weapon_pine.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_weapon_pine.png) — 2400×800；复用原源2倍格的完整待机预览，91时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。
- [x] [tangseng_preview_weapon_white_tiger.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/preview/tangseng_preview_weapon_white_tiger.png) — 2400×800；复用原源2倍格的完整待机预览，91时刻/24fps，六列200逻辑格；源像素/注册/换装及实际播放已验，本批待审。

<a id="art-images-05"></a>

### 英雄状态与角色配套贴图（0/4）

目录：`TheGame/Sprites/Characters/Heroes/Status/`

- [ ] [hero_frozen.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/Status/hero_frozen.png) — 2048×896；待来源/清晰度处理及1080p验收。
- [ ] [poison_head.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/Status/poison_head.png) — 41×53；待来源/清晰度处理及1080p验收。
- [ ] [poison_up.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/Status/poison_up.png) — 1176×175；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Heroes/tangseng/`

- [ ] [tangseng_shadow_atlas.png](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/tangseng/tangseng_shadow_atlas.png) — 1200×4000；待来源/清晰度处理及1080p验收。

<a id="art-images-06"></a>

### 敌人、Boss及其图集（12/66）

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/`

- [ ] [macaque_king_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/macaque_king_body.png) — 2696×1560 → 2048×3476；原源2倍重导，209时刻/60共享姿态；共同画布/原点、24fps及遮挡保持；部分提升，1个位图依赖仍受源精度限制；已接入验证，上一批已审。
- [ ] [macaque_king_effect.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/macaque_king_effect.png) — 2928×1490；来源受限：原包含1个位图依赖；保留正式原图，未以插值放大计高清。
- [x] [macaque_wind_blade.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/macaque_wind_blade.png) — 5768×620 → 2048×2503；原源2倍重导，50时刻/50共享姿态；共同画布/原点、24fps及遮挡保持；纯矢量高清完成；已接入验证，上一批已审。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_03_tamarin_king/`

- [ ] [tamarin_king_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_03_tamarin_king/tamarin_king_body.png) — 1528×810 → 3056×1620；原源2倍重导，176时刻/36共享姿态；共同画布/原点、24fps及遮挡保持；部分提升，9个位图依赖仍受源精度限制；已接入验证，上一批已审。
- [ ] [tamarin_king_effect.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_03_tamarin_king/tamarin_king_effect.png) — 6520×3200；来源受限：原包含55个位图依赖；保留正式原图，未以插值放大计高清。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_04_bat_cave/`

- [ ] [bat_cave_statue_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_04_bat_cave/bat_cave_statue_body.png) — 795×222；来源受限：原包含1个位图依赖；保留正式原图，未以插值放大计高清。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_05_qixing_cave/`

- [ ] [lion_camel_king.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_king.png) — 5144×3179 → 4096×3984；原源2倍重导，148时刻/82共享姿态；共同画布/原点、24fps及遮挡保持；部分提升，30个位图依赖仍受源精度限制；2页，每边≤4096；已接入验证，上一批已审。
- [ ] [lion_camel_king_2.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_king_2.png) — 4096×2643；部分提升：lion_camel_king原生分页第2页，源像素/虚拟画布逐格全等；主体位图限制保持，上一批已审。
- [ ] [lion_camel_poison_cloud.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_poison_cloud.png) — 2720×695；来源受限：原包含1个位图依赖；保留正式原图，未以插值放大计高清。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_06_garuda_cave/`

- [ ] [peng_demon_king_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_06_garuda_cave/peng_demon_king_body.png) — 2088×2670 → 2048×3237；原源2倍重导，223时刻/78共享姿态；共同画布/原点、24fps及遮挡保持；部分提升，31个位图依赖仍受源精度限制；已接入验证，上一批已审。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/`

- [ ] [jiao_back_hit.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/jiao_back_hit.png) — 880×216；来源受限：原包含10个位图依赖；保留正式原图，未以插值放大计高清。
- [ ] [jiao_demon_king_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/jiao_demon_king_official_body.png) — 2048×3863 → 4096×3729；原源2倍重导，158时刻/84共享姿态；共同画布/原点、24fps及遮挡保持；部分提升，6个位图依赖仍受源精度限制；2页，每边≤4096；已接入验证，上一批已审。
- [ ] [jiao_demon_king_official_body_2.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/jiao_demon_king_official_body_2.png) — 4096×3032；部分提升：jiao_demon_king_official_body原生分页第2页，源像素/虚拟画布逐格全等；主体位图限制保持，上一批已审。
- [ ] [jiao_dragon_wave.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/jiao_dragon_wave.png) — 3808×388；来源受限：原包含14个位图依赖；保留正式原图，未以插值放大计高清。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/`

- [ ] [shark_demon_king_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_demon_king_official_body.png) — 2048×3917 → 4096×3772；原源2倍重导，200时刻/109共享姿态；共同画布/原点、24fps及遮挡保持；部分提升，14个位图依赖仍受源精度限制；2页，每边≤4096；已接入验证，上一批已审。
- [ ] [shark_demon_king_official_body_2.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_demon_king_official_body_2.png) — 4096×3026；部分提升：shark_demon_king_official_body原生分页第2页，源像素/虚拟画布逐格全等；主体位图限制保持，上一批已审。
- [ ] [shark_king_bubble.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_bubble.png) — 2048×222；来源受限：原包含1个位图依赖；保留正式原图，未以插值放大计高清。
- [ ] [shark_king_bubble_warning.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_bubble_warning.png) — 512×324；来源受限：原包含1个位图依赖；保留正式原图，未以插值放大计高清。
- [ ] [shark_king_shark_wave.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_shark_wave.png) — 4096×972；来源受限：原包含5个位图依赖；保留正式原图，未以插值放大计高清。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/`

- [ ] [bull_background.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_background.png) — 1024×472；来源受限：原包含1个位图依赖；保留正式原图，未以插值放大计高清。
- [x] [bull_demon_beast_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_beast_official_body.png) — 512×787 → 1024×1574；原源2倍重导，139时刻/16共享姿态；共同画布/原点、24fps及遮挡保持；纯矢量高清完成；已接入验证，上一批已审。
- [x] [bull_demon_heart_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_heart_official_body.png) — 512×61 → 1024×122；原源2倍重导，2时刻/1共享姿态；共同画布/原点、24fps及遮挡保持；纯矢量高清完成；已接入验证，上一批已审。
- [ ] [bull_demon_king_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_king_official_body.png) — 2048×3838 → 4096×3760；原源2倍重导，438时刻/167共享姿态；共同画布/原点、24fps及遮挡保持；部分提升，45个位图依赖仍受源精度限制；2页，每边≤4096；已接入验证，上一批已审。
- [ ] [bull_demon_king_official_body_2.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_king_official_body_2.png) — 4096×2654；部分提升：bull_demon_king_official_body原生分页第2页，源像素/虚拟画布逐格全等；主体位图限制保持，上一批已审。
- [ ] [bull_demon_king_tangseng_capture_atlas.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_king_tangseng_capture_atlas.png) — 476×289 → 952×578；部分提升：原源2倍唐僧被抓两姿态，130时刻保持；其他姿态共用主体分页；混合位图未完整高清，上一批已审。
- [ ] [bull_fire.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_fire.png) — 512×759；来源受限：原包含7个位图依赖；保留正式原图，未以插值放大计高清。
- [ ] [bull_hands.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_hands.png) — 512×185；来源受限：原包含1个位图依赖；保留正式原图，未以插值放大计高清。
- [ ] [bull_heart.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_heart.png) — 512×1218；来源受限：原包含2个位图依赖；保留正式原图，未以插值放大计高清。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/`

- [ ] [dragon_king_ice_aura.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_ice_aura.png) — 512×4256；来源受限：原包含1个位图依赖；保留正式原图，未以插值放大计高清。
- [ ] [dragon_king_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_official_body.png) — 4096×2242 → 4096×3702；原源2倍重导，315时刻/102共享姿态；共同画布/原点、24fps及遮挡保持；部分提升，4个位图依赖仍受源精度限制；3页，每边≤4096；已接入验证，上一批已审。
- [ ] [dragon_king_official_body_2.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_official_body_2.png) — 4096×4090；部分提升：dragon_king_official_body原生分页第2页，源像素/虚拟画布逐格全等；主体位图限制保持，上一批已审。
- [ ] [dragon_king_official_body_3.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_official_body_3.png) — 4096×852；部分提升：dragon_king_official_body原生分页第3页，源像素/虚拟画布逐格全等；主体位图限制保持，上一批已审。
- [ ] [dragon_king_torrent.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_torrent.png) — 512×4480；来源受限：原包含1个位图依赖；保留正式原图，未以插值放大计高清。
- [ ] [dragon_king_wave.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_wave.png) — 1024×312；来源受限：原包含1个位图依赖；保留正式原图，未以插值放大计高清。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/bull_guard/`

- [x] [bull_guard_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/bull_guard/bull_guard_official_body.png) — 1024×2284；原源2倍原路径接入，83帧/27姿态；共同逻辑画布/注册点、时序与碰撞保持，双向原生验证通过。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/cave_brute/`

- [ ] [cave_brute.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/cave_brute/cave_brute.png) — 来源受限：源动作由31个位图组成；没有可确认的更高精度源，不以插值增大计高清。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/`

- [ ] [demon_monkey_attack.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_attack.png) — 520×134；待来源/清晰度处理及1080p验收。
- [ ] [demon_monkey_death.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_death.png) — 620×108；待来源/清晰度处理及1080p验收。
- [ ] [demon_monkey_hurt.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_hurt.png) — 89×87；待来源/清晰度处理及1080p验收。
- [ ] [demon_monkey_idle.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_idle.png) — 370×114；待来源/清晰度处理及1080p验收。
- [x] [demon_monkey_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_official_body.png) — 2048×690；原源2倍原路径接入，78帧/20姿态；共同逻辑画布/注册点、时序与碰撞保持，双向原生验证通过。
- [ ] [demon_monkey_run.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_run.png) — 464×105；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/`

- [ ] [gorilla_attack_1.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_attack_1.png) — 904×180；待来源/清晰度处理及1080p验收。
- [ ] [gorilla_death.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_death.png) — 708×142；待来源/清晰度处理及1080p验收。
- [ ] [gorilla_hurt.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_hurt.png) — 117×140；待来源/清晰度处理及1080p验收。
- [ ] [gorilla_idle.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_idle.png) — 650×137；待来源/清晰度处理及1080p验收。
- [x] [gorilla_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_official_body.png) — 2048×1184；原源2倍原路径接入，85帧/23姿态；共同逻辑画布/注册点、时序与碰撞保持，双向原生验证通过。
- [ ] [gorilla_run.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_run.png) — 612×141；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/`

- [ ] [huaguoshan_monkey_attack.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_attack.png) — 546×83；待来源/清晰度处理及1080p验收。
- [ ] [huaguoshan_monkey_death.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_death.png) — 546×88；待来源/清晰度处理及1080p验收。
- [ ] [huaguoshan_monkey_hurt.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_hurt.png) — 136×86；待来源/清晰度处理及1080p验收。
- [ ] [huaguoshan_monkey_idle.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_idle.png) — 43×63；待来源/清晰度处理及1080p验收。
- [x] [huaguoshan_monkey_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_official_body.png) — 512×1018；已审原矢量2倍，五动作69帧/23共享姿态，原路径/UID接入，逻辑89×72、原点、尺度及碰撞保持；双向/实机攻击受击死亡/回池验证通过。
- [ ] [huaguoshan_monkey_run.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_run.png) — 256×88；待来源/清晰度处理及1080p验收。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/knife_demon/`

- [ ] [knife_demon.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/knife_demon/knife_demon.png) — 3120×1144；原源2倍原路径接入，83帧/25姿态；共同逻辑画布/注册点、时序与碰撞保持，双向原生验证通过。 部分完成：矢量清晰度提高，嵌套位图仍受源限制，不计整图高清完成。
- [x] [knife_demon_blade.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/knife_demon/knife_demon_blade.png) — 5328×114；原源2倍原路径接入，27帧/22姿态；共同逻辑画布/注册点、时序与碰撞保持，双向原生验证通过。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/lantern_fish/`

- [x] [lantern_fish_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/lantern_fish/lantern_fish_official_body.png) — 4832×2176；原源2倍原路径接入，87帧/87姿态；共同逻辑画布/注册点、时序与碰撞保持，双向原生验证通过。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/`

- [ ] [shark_spawn_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/shark_spawn_official_body.png) — 4096×3936；原源2倍原路径接入，177帧/74姿态；共同逻辑画布/注册点、时序与碰撞保持，双向原生验证通过。 部分完成：矢量清晰度提高，嵌套位图仍受源限制，不计整图高清完成。
- [ ] [shark_spawn_slash.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/shark_spawn_slash.png) — 来源受限：主体嵌套bitmap293，原图保留；没有可确认的更高精度源，不以插值增大计高清。
- [ ] [shark_spawn_tornado.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/shark_spawn_tornado.png) — 来源受限：10个位图帧序，原图保留；没有可确认的更高精度源，不以插值增大计高清。
- [x] [shark_spawn_warning.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/shark_spawn_warning.png) — 1024×928；原源2倍原路径接入，25帧/23姿态；共同逻辑画布/注册点、时序与碰撞保持，双向原生验证通过。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/shrimp_demon/`

- [x] [shrimp_demon_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shrimp_demon/shrimp_demon_official_body.png) — 2048×1180；原源2倍原路径接入，79帧/29姿态；共同逻辑画布/注册点、时序与碰撞保持，双向原生验证通过。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/tide_demon/`

- [ ] [tide_demon_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/tide_demon/tide_demon_official_body.png) — 2048×3056；原源2倍原路径接入，95帧/46姿态；共同逻辑画布/注册点、时序与碰撞保持，双向原生验证通过。 部分完成：矢量清晰度提高，嵌套位图仍受源限制，不计整图高清完成。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/turtle_minister/`

- [ ] [turtle_minister_official_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/turtle_minister/turtle_minister_official_body.png) — 4096×1736；原源2倍原路径接入，98帧/57姿态；共同逻辑画布/注册点、时序与碰撞保持，双向原生验证通过。 部分完成：矢量清晰度提高，嵌套位图仍受源限制，不计整图高清完成。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sky/bat/`

- [ ] [bat_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sky/bat/bat_body.png) — 来源受限：源动作由26个位图组成；没有可确认的更高精度源，不以插值增大计高清。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sky/flying_demon/`

- [x] [flying_demon_body.png](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sky/flying_demon/flying_demon_body.png) — 6240×2200；原源2倍原路径接入，85帧/33姿态；共同逻辑画布/注册点、时序与碰撞保持，双向原生验证通过。

<a id="art-images-07"></a>

### 独立特效、掉落球、升级与演出（83/189）

目录：`TheGame/Sprites/Effects/Enemies/sea/`

- [x] [shrimp_projectile.png](../Godot/GodotProject/TheGame/Sprites/Effects/Enemies/sea/shrimp_projectile.png) — 19×13 → 38×26；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成。

目录：`TheGame/Sprites/Effects/Enemies/zm1_06_garuda_cave/`

- [ ] [boss_death.png](../Godot/GodotProject/TheGame/Sprites/Effects/Enemies/zm1_06_garuda_cave/boss_death.png) — 1002×167；来源受限：Monster_v1原位图；正式原图保持，不以插值放大计高清。
- [ ] [peng_fire.png](../Godot/GodotProject/TheGame/Sprites/Effects/Enemies/zm1_06_garuda_cave/peng_fire.png) — 4976×1296；来源受限：Monster2_v4原位图；正式原图保持，不以插值放大计高清。

目录：`TheGame/Sprites/Effects/hero_death/`

- [ ] [hero_tombstone_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_death/hero_tombstone_atlas.png) — 1832×1830；来源尚未确认：旧RoleDeath墓碑；正式原图保持，未重画或放大冒充高清。

目录：`TheGame/Sprites/Effects/hero_hurt/tangseng/`

- [ ] [tangseng_hurt_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_01.png) — 100×122；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hurt_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_02.png) — 100×122；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hurt_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_03.png) — 100×122；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hurt_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_04.png) — 100×122；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hurt_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_05.png) — 100×122；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hurt_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_06.png) — 100×122；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hurt_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_07.png) — 100×122；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hurt_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/tangseng/tangseng_hurt_08.png) — 100×122；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。

目录：`TheGame/Sprites/Effects/hero_hurt/wukong/`

- [ ] [hero_hurt_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_01.png) — 100×122；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [hero_hurt_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_02.png) — 100×122；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [hero_hurt_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_03.png) — 100×122；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [hero_hurt_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_04.png) — 100×122；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [hero_hurt_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_05.png) — 100×122；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [hero_hurt_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_06.png) — 100×122；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [hero_hurt_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_07.png) — 100×122；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [hero_hurt_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/hero_hurt/wukong/hero_hurt_08.png) — 100×122；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。

目录：`TheGame/Sprites/Effects/level_exit/`

- [ ] [level_exit_00.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_00.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_01.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_02.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_03.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_04.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_05.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_06.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_07.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_08.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_09.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_09.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_10.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_10.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_11.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_11.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_12.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_12.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_13.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_13.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_14.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_14.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_15.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_15.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_16.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_16.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_17.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_17.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_18.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_18.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_exit_19.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_exit/level_exit_19.png) — 186×165；来源受限：v25928 decoded main原位图；正式原图保持，不以插值放大计高清。

目录：`TheGame/Sprites/Effects/level_up/`

- [ ] [level_up_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_01.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_02.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_03.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_04.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_05.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_06.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_07.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_08.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_09.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_09.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_10.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_10.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_11.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_11.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_12.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_12.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_13.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_13.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_14.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_14.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_15.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_15.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_16.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_16.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_17.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_17.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_18.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_18.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_19.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_19.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_20.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_20.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_21.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_21.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_22.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_22.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_23.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_23.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_24.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_24.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_25.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_25.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_26.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_26.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_27.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_27.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [level_up_28.png](../Godot/GodotProject/TheGame/Sprites/Effects/level_up/level_up_28.png) — 122×136；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。

目录：`TheGame/Sprites/Effects/loot/`

- [x] [health_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_01.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_02.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_03.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_04.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_05.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_06.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_07.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_08.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_09.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_09.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_10.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_10.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_11.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_11.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_12.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_12.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_13.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_13.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_14.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_14.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_15.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_15.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_16.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_16.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_17.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_17.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_18.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_18.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [health_19.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/health_19.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_01.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_02.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_03.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_04.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_05.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_06.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_07.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_08.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_09.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_09.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_10.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_10.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_11.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_11.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_12.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_12.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_13.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_13.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_14.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_14.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_15.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_15.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_16.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_16.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_17.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_17.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_18.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_18.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [mana_19.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/mana_19.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_01.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_02.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_03.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_04.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_05.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_06.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_07.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_08.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_09.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_09.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_10.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_10.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_11.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_11.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_12.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_12.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_13.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_13.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_14.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_14.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_15.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_15.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_16.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_16.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_17.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_17.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_18.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_18.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [musou_19.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/musou_19.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_01.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_02.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_03.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_04.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_05.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_06.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_07.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_08.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_09.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_09.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_10.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_10.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_11.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_11.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_12.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_12.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_13.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_13.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_14.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_14.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_15.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_15.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_16.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_16.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_17.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_17.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_18.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_18.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。
- [x] [soul_19.png](../Godot/GodotProject/TheGame/Sprites/Effects/loot/soul_19.png) — 18×20 → 36×40；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成；已修高密度滤镜核/相位，未逐帧居中。

目录：`TheGame/Sprites/Effects/monster_hit/tangseng/`

- [ ] [tangseng_hit_1_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_1_01.png) — 168×107；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hit_1_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_1_02.png) — 168×107；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hit_1_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_1_03.png) — 168×107；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hit_1_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_1_04.png) — 168×107；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hit_1_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_1_05.png) — 168×107；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hit_1_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_1_06.png) — 168×107；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hit_1_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_1_07.png) — 168×107；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hit_1_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_1_08.png) — 168×107；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hit_2_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_2_01.png) — 125×89；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hit_2_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_2_02.png) — 125×89；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hit_2_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_2_03.png) — 125×89；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hit_2_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_2_04.png) — 125×89；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hit_2_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_2_05.png) — 125×89；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hit_2_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_2_06.png) — 125×89；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hit_2_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_2_07.png) — 125×89；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_hit_2_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_hit_2_08.png) — 125×89；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。

目录：`TheGame/Sprites/Effects/monster_hit/wukong/`

- [ ] [wukong_hit_1_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_1_01.png) — 168×107；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_hit_1_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_1_02.png) — 168×107；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_hit_1_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_1_03.png) — 168×107；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_hit_1_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_1_04.png) — 168×107；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_hit_1_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_1_05.png) — 168×107；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_hit_1_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_1_06.png) — 168×107；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_hit_1_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_1_07.png) — 168×107；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_hit_1_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_1_08.png) — 168×107；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_hit_2_01.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_2_01.png) — 125×89；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_hit_2_02.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_2_02.png) — 125×89；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_hit_2_03.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_2_03.png) — 125×89；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_hit_2_04.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_2_04.png) — 125×89；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_hit_2_05.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_2_05.png) — 125×89；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_hit_2_06.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_2_06.png) — 125×89；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_hit_2_07.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_2_07.png) — 125×89；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_hit_2_08.png](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_hit_2_08.png) — 125×89；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。

目录：`TheGame/Sprites/Effects/tangseng/skills/`

- [ ] [tangseng_cure_role_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_cure_role_atlas.png) — 1024×2090；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [tangseng_dark_ice_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_dark_ice_atlas.png) — 2048×980；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [x] [tangseng_ice_dragon_bolt.png](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_ice_dragon_bolt.png) — 129×90 → 258×180；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成。
- [x] [tangseng_water_demon_ball_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_water_demon_ball_atlas.png) — 4096×91 → 2048×592；原源2倍重导，20源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成。
- [ ] [tangseng_water_demon_fall_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_water_demon_fall_atlas.png) — 1024×1972；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。

目录：`TheGame/Sprites/Effects/tangseng/`

- [x] [tangseng_normal_bolt.png](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/tangseng_normal_bolt.png) — 140×58 → 280×116；原源2倍重导，1源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成。

目录：`TheGame/Sprites/Effects/wukong/skills/`

- [x] [wukong_blazing_flash_trail_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_blazing_flash_trail_atlas.png) — 3528×204 → 1024×1385；原源2倍重导，10源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成。
- [ ] [wukong_fire_demon_combo_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_combo_atlas.png) — 2214×1856；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_fire_demon_descent_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_descent_atlas.png) — 508×295；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [ ] [wukong_fire_demon_landing_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_landing_atlas.png) — 942×262；来源受限：Role_v7原位图；正式原图保持，不以插值放大计高清。
- [x] [wukong_fire_demon_prelude_atlas.png](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_prelude_atlas.png) — 1326×546 → 512×1157；原源2倍重导，40源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成。
- [ ] [wukong_skill_inline_effects.png](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_skill_inline_effects.png) — 3200×4400 → 2048×2739；部分提升：原源2倍重导，122源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；混合位图原精度限制保持。

目录：`TheGame/Sprites/Effects/wukong/`

- [x] [wukong_attack_effects.png](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/wukong_attack_effects.png) — 2400×1200 → 512×1216；原源2倍重导，62源时刻；原点/逻辑画布/遮挡/时序保持，实际接入验证，上一批已审；纯矢量高清完成。

<a id="art-images-08"></a>

### UI、头像、地图、技能面板与无双画面（18/231完整完成，55项部分提升）

目录：`TheGame/Sprites/UI/frontend/`

- [ ] [character_bajie_card.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/character_bajie_card.png) — 1271×1830；sprite515/shape514原矢量3倍人物层、原路径/UID；独立字体与原遮罩/墨框已接入，画面获认可。仍按整卡目标保守记部分完成：共享原生背景精度受限。
- [ ] [character_sanzang_card.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/character_sanzang_card.png) — 1271×1830；sprite521/shape520原矢量3倍人物层、原路径/UID；独立字体与原遮罩/墨框已接入，画面获认可。仍按整卡目标保守记部分完成：共享原生背景精度受限。
- [ ] [character_wujing_card.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/character_wujing_card.png) — 1271×1830；sprite518/shape517原矢量3倍人物层、原路径/UID；独立字体与原遮罩/墨框已接入，画面获认可。仍按整卡目标保守记部分完成：共享原生背景精度受限。
- [ ] [character_wukong_card.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/character_wukong_card.png) — 1271×1830；sprite511/shape509原矢量3倍人物层、原路径/UID；独立字体与原遮罩/墨框已接入，画面获认可。仍按整卡目标保守记部分完成：共享原生背景精度受限。
- [x] [character_card_ink.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/character_card_ink.png) — 711×1830；原shape510三倍重导，悟空/沙僧共用，NinePatch保持边角/厚度；接入及6比例验证通过。
- [x] [character_sanzang_ink.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/character_sanzang_ink.png) — 711×1830；原shape510唐僧变换，三倍重导及原生NinePatch验证通过。
- [x] [character_bajie_ink.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/character_bajie_ink.png) — 711×1830；原shape510八戒变换，三倍重导及原生NinePatch验证通过。
- [ ] [character_card_background.jpg](../Godot/GodotProject/TheGame/Sprites/UI/frontend/character_card_background.jpg) — 940×590；原bitmap508的JPEG原字节，原shape506遮罩已接入；未提高真实精度，来源受限。
- [ ] [delete_confirm_cancel.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/delete_confirm_cancel.png) — 68×33；已复核other3元件217.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [delete_confirm_cancel_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/delete_confirm_cancel_hover.png) — 68×33；已复核Commonv3720 bitmap78；现有源像素或合成保真受限，正式原图保持。
- [ ] [delete_confirm_confirm.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/delete_confirm_confirm.png) — 68×33；已复核other3元件212.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [delete_confirm_confirm_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/delete_confirm_confirm_hover.png) — 68×33；已复核Commonv3720 bitmap73；现有源像素或合成保真受限，正式原图保持。
- [ ] [delete_confirm_panel.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/delete_confirm_panel.png) — 231×133；已复核other3元件210.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [journey_map.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/journey_map.png) — 940×590 → 1408×590；部分完成：外围已审/原路径接入，原中心像素保持，正式菜单铺满通过；中心真实高清受源限制，2816×1180恢复小样收益有限、未采用。
- [ ] [map_bianfuku_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_bianfuku_current.png) — 425×232；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_bianfuku_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_bianfuku_hover.png) — 174×119；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_bianfuku_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_bianfuku_normal.png) — 57×30；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [x] [map_donghai_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_donghai_current.png) — 2832×2380；已审2倍矢量光晕第一页（源帧0～9），与第二页共同保持原19帧/24fps，正式接入/验证完成。
- [x] [map_donghai_current_2.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_donghai_current_2.png) — 2832×2380；东海动画第二页（源帧10～18），原生贴图轨切页，所有帧及原时刻保持，正式接入/验证完成。
- [ ] [map_donghai_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_donghai_hover.png) — 1484×530；部分完成：已审2倍矢量光晕已接入，原137×28位图文字仍无高清源，整图继续未勾。
- [ ] [map_donghai_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_donghai_normal.png) — 742×265；原图全透明，仅作为输入状态；本批原PNG与命中配置保持，高清不计数。
- [ ] [map_huaguoshan_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_huaguoshan_current.png) — 480×424；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_huaguoshan_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_huaguoshan_hover.png) — 184×198；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_huaguoshan_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_huaguoshan_normal.png) — 68×78；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_jialuoluo_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_jialuoluo_current.png) — 765×320；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_jialuoluo_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_jialuoluo_hover.png) — 246×143；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_jialuoluo_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_jialuoluo_normal.png) — 125×52；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_niumogu_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_niumogu_current.png) — 540×432；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_niumogu_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_niumogu_hover.png) — 176×125；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_niumogu_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_niumogu_normal.png) — 57×56；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [x] [map_qianan_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qianan_current.png) — 3060×2264；已审原矢量光晕2倍、19帧/24fps；共同透明裁切及offset补偿，原路径接入/帧/命中/双端检查完成。
- [ ] [map_qianan_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qianan_hover.png) — 674×624；部分完成：已审2倍矢量光晕已接入，原137×28位图文字仍无高清源，整图继续未勾。
- [ ] [map_qianan_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qianan_normal.png) — 337×312；原图全透明，仅作为输入状态；本批原PNG与命中配置保持，高清不计数。
- [ ] [map_qixingdong_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qixingdong_current.png) — 570×368；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_qixingdong_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qixingdong_hover.png) — 177×132；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_qixingdong_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qixingdong_normal.png) — 86×64；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_return_title.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_return_title.png) — 112×114；已恢复HEAD完整原图及原文字/墨底，无中间扣空；原生位图高清仍受限。
- [ ] [map_save_game.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_save_game.png) — 138×93；完整原alpha/笔刷背景保持，仅去原字形RGB，文字用Godot字体Label；无中间扣空，主体高清仍受源限制。
- [ ] [map_save_game_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_save_game_hover.png) — 138×93；完整原alpha/笔刷背景保持，仅去原字形RGB，文字用Godot字体Label；无中间扣空，主体高清仍受源限制。 原悬停引用保留、与普通态不同。
- [ ] [map_shazhiwoliu_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shazhiwoliu_current.png) — 1930×1640；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_shazhiwoliu_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shazhiwoliu_hover.png) — 386×164；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_shazhiwoliu_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shazhiwoliu_normal.png) — 386×164；原图全透明，仅作为输入状态；本批原PNG与命中配置保持，高清不计数。
- [ ] [map_shuiliandong_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shuiliandong_current.png) — 440×344；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_shuiliandong_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shuiliandong_hover.png) — 201×162；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_shuiliandong_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shuiliandong_normal.png) — 60×58；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_skills.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_skills.png) — 1280×1120；完整原alpha/笔刷背景保持，仅去原字形RGB，文字用Godot字体Label；无中间扣空，主体高清仍受源限制。
- [ ] [map_taohuayuan_current.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_taohuayuan_current.png) — 460×372；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_taohuayuan_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_taohuayuan_hover.png) — 186×187；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [map_taohuayuan_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_taohuayuan_normal.png) — 64×65；当前登记源为小位图；外层shape/sprite及放大导出不计高清，保留正式原图，来源/保真受限。
- [ ] [menu_close.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/menu_close.png) — 41×41；已复核other3元件43.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_close_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/menu_close_hover.png) — 41×41；已复核backpack3_bitmap元件14；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_separator.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/menu_separator.png) — 241×21；已复核other元件161.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [save_slot_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/save_slot_hover.png) — 260×87；已查现有导出与旧图，尚未确认可保真提高细节的同内容源，正式原图保持。
- [ ] [save_slot_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/save_slot_normal.png) — 260×87；已查现有导出与旧图，尚未确认可保真提高细节的同内容源，正式原图保持。
- [ ] [save_slots_backdrop.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/save_slots_backdrop.png) — 941×591；已复核common_shape元件165；现有源像素或合成保真受限，正式原图保持。
- [ ] [settings_scroll.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/settings_scroll.png) — 326×341；已复核other原位图222.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [title_backdrop.png](../Godot/GodotProject/TheGame/Sprites/UI/frontend/title_backdrop.png) — 941×591；现有中心为低分辨率位图，已有外围扩图候选不增加中心细节，本批保留正式原图。

目录：`TheGame/Sprites/UI/gogo/`

- [ ] [gogo_sheet.png](../Godot/GodotProject/TheGame/Sprites/UI/gogo/gogo_sheet.png) — 1704×828；已复核v25928 main GOGO44；现有源像素或合成保真受限，正式原图保持。

目录：`TheGame/Sprites/UI/hud/`

- [ ] [battle_menu_ws_fill.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_fill.png) — 221×12；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_00.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_00.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_01.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_01.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_02.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_02.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_03.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_03.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_04.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_04.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_05.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_05.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_06.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_06.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_07.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_07.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_08.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_08.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_09.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_09.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_10.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_10.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_11.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_11.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_12.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_12.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_13.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_13.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_14.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_14.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_15.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_15.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_16.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_16.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_17.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_17.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_glow_18.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_18.png) — 158×158；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_idle.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_idle.png) — 68×68；已复核other原位图335.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_menu_ws_under.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_under.png) — 221×27；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [battle_skill_icons.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_skill_icons.png) — 495×45；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [boss_health_fill.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/boss_health_fill.png) — 340×29；已复核other3元件263.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [boss_health_under.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/boss_health_under.png) — 340×29；已复核main原位图3.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [combo_caption.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/combo_caption.png) — 224×80；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [combo_digits.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/combo_digits.png) — 1600×3200；已复核OtherMat_v9原位图/原生滤镜：330→329，333→332，340→337，328→306..326，567→565；原图保持；现有源像素或合成保真受限，正式原图保持。
- [ ] [hud_exp_fill.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_exp_fill.png) — 144×12；已复核OtherMatv3570；现有源像素或合成保真受限，正式原图保持。
- [x] [hud_head_tangseng.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_head_tangseng.png) — 83×76 → 166×152；原矢量同造型头像固定原点2倍重导；83×76逻辑尺寸和P2镜像保持，1倍栅格采样有差，见Validation。已实际接入，待本批审查。
- [x] [hud_head_wukong.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_head_wukong.png) — 83×76 → 166×152；原矢量同造型头像固定原点2倍重导；83×76逻辑尺寸和P2镜像保持，1倍栅格采样有差，见Validation。已实际接入，待本批审查。
- [ ] [hud_hp_fill.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_hp_fill.png) — 144×12；已复核OtherMatv3570；现有源像素或合成保真受限，正式原图保持。
- [ ] [hud_menu_panel.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_menu_panel.png) — 169×133；已复核other3元件95.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [hud_mp_fill.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_mp_fill.png) — 144×12；已复核OtherMatv3570；现有源像素或合成保真受限，正式原图保持。
- [x] [hud_status_label.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_status_label.png) — 17×46 → 34×92；OtherMat_v9 shape357原白字轮廓2倍重导；P1/P2镜像和逻辑位置保持。已实际接入，待本批审查。
- [ ] [hud_status_panel.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_status_panel.png) — 227×87；已复核other原位图297.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [hud_ws_frame.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/hud_ws_frame.png) — 260×22；已复核other原位图303.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_inventory_caption.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_inventory_caption.png) — 30×17；已复核other3元件140.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_inventory_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_inventory_hover.png) — 47×37；已复核other3元件143；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_inventory_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_inventory_normal.png) — 47×37；已复核other3元件141；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_pet_caption.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_pet_caption.png) — 30×17；已复核other3元件158.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_pet_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_pet_hover.png) — 47×37；已复核other3元件161；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_pet_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_pet_normal.png) — 47×37；已复核other3元件159；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_settings_caption.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_settings_caption.png) — 30×17；已复核other3元件133.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_settings_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_settings_hover.png) — 47×37；已复核other3元件136；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_settings_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_settings_normal.png) — 47×37；已复核other3元件134；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_skills_caption.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_skills_caption.png) — 30×17；已复核other3元件146.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_skills_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_skills_hover.png) — 47×37；已复核other3元件149；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_skills_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_skills_normal.png) — 47×37；已复核other3元件147；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_weapon_caption.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_weapon_caption.png) — 30×17；已复核other3元件152.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_weapon_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_weapon_hover.png) — 47×37；已复核other3元件155；现有源像素或合成保真受限，正式原图保持。
- [ ] [menu_weapon_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/menu_weapon_normal.png) — 47×37；已复核other3元件153；现有源像素或合成保真受限，正式原图保持。

目录：`TheGame/Sprites/UI/hud/musou_activation/shared/`

- [ ] [musou_activation_light.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/shared/musou_activation_light.png) — 2048×3938；共用光束/火焰/字图图集，19个独立姿态对应24个时刻；原密度，1408×590覆盖画布，含原低精度位图，不计高清。

目录：`TheGame/Sprites/UI/hud/musou_activation/tangseng/`

- [ ] [tangseng_musou_activation_01.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_01.png) — 原940×590合成帧 → 1×1人物存储；部分提升：本时刻原无可见角色；保留透明帧及实际背景。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_02.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_02.png) — 原940×590合成帧 → 1×1人物存储；部分提升：本时刻原无可见角色；保留透明帧及实际背景。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_03.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_03.png) — 原940×590合成帧 → 1×1人物存储；部分提升：本时刻原无可见角色；保留透明帧及实际背景。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_04.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_04.png) — 原940×590合成帧 → 1×1人物存储；部分提升：本时刻原无可见角色；保留透明帧及实际背景。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_05.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_05.png) — 原940×590合成帧 → 97×201人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_06.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_06.png) — 原940×590合成帧 → 228×501人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_07.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_07.png) — 原940×590合成帧 → 359×678人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_08.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_08.png) — 原940×590合成帧 → 490×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_09.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_09.png) — 原940×590合成帧 → 621×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_10.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_10.png) — 原940×590合成帧 → 622×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_11.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_11.png) — 原940×590合成帧 → 622×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_12.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_12.png) — 原940×590合成帧 → 622×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_13.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_13.png) — 原940×590合成帧 → 622×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_14.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_14.png) — 原940×590合成帧 → 622×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_15.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_15.png) — 原940×590合成帧 → 622×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_16.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_16.png) — 原940×590合成帧 → 622×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_17.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_17.png) — 原940×590合成帧 → 622×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_18.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_18.png) — 原940×590合成帧 → 622×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_19.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_19.png) — 原940×590合成帧 → 622×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_20.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_20.png) — 原940×590合成帧 → 622×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_21.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_21.png) — 原940×590合成帧 → 622×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_22.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_22.png) — 原940×590合成帧 → 622×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_23.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_23.png) — 原940×590合成帧 → 622×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [tangseng_musou_activation_24.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_24.png) — 原940×590合成帧 → 622×704人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。

目录：`TheGame/Sprites/UI/hud/musou_activation/wukong/`

- [ ] [wukong_musou_activation_01.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_01.png) — 原940×590合成帧 → 1×1人物存储；部分提升：本时刻原无可见角色；保留透明帧及实际背景。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_02.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_02.png) — 原940×590合成帧 → 1×1人物存储；部分提升：本时刻原无可见角色；保留透明帧及实际背景。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_03.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_03.png) — 原940×590合成帧 → 1×1人物存储；部分提升：本时刻原无可见角色；保留透明帧及实际背景。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_04.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_04.png) — 原940×590合成帧 → 1×1人物存储；部分提升：本时刻原无可见角色；保留透明帧及实际背景。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_05.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_05.png) — 原940×590合成帧 → 34×65人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_06.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_06.png) — 原940×590合成帧 → 134×117人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_07.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_07.png) — 原940×590合成帧 → 235×384人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_08.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_08.png) — 原940×590合成帧 → 335×606人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_09.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_09.png) — 原940×590合成帧 → 435×629人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_10.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_10.png) — 原940×590合成帧 → 536×633人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_11.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_11.png) — 原940×590合成帧 → 608×633人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_12.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_12.png) — 原940×590合成帧 → 608×633人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_13.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_13.png) — 原940×590合成帧 → 608×633人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_14.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_14.png) — 原940×590合成帧 → 608×633人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_15.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_15.png) — 原940×590合成帧 → 608×633人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_16.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_16.png) — 原940×590合成帧 → 608×633人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_17.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_17.png) — 原940×590合成帧 → 608×633人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_18.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_18.png) — 原940×590合成帧 → 608×633人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_19.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_19.png) — 原940×590合成帧 → 608×633人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_20.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_20.png) — 原940×590合成帧 → 608×633人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_21.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_21.png) — 原940×590合成帧 → 608×633人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_22.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_22.png) — 原940×590合成帧 → 608×633人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_23.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_23.png) — 原940×590合成帧 → 608×633人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。
- [ ] [wukong_musou_activation_24.png](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_24.png) — 原940×590合成帧 → 608×633人物存储；部分提升：原矢量角色2倍重导；保留原点的透明裁片。逻辑画布940×590，原24fps和帧序不变；背景/位图字样仍用原密度。

目录：`TheGame/Sprites/UI/inventory/`

- [ ] [inventory_action_equip.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_action_equip.png) — 77×31；Common376第1帧含原生Lossless2位图；原密度RGBA重现通过，不能当矢量高清，来源受限。
- [ ] [inventory_action_equip_disabled.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_action_equip_disabled.png) — 77×31；Common376第2帧为位图组合，1倍RGBA重现通过；暂无已证实高清原件，来源受限。
- [ ] [inventory_action_gift.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_action_gift.png) — 77×31；Common367第1帧为位图组合，1倍RGBA重现通过；来源受限，原图保持。
- [ ] [inventory_action_gift_disabled.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_action_gift_disabled.png) — 77×31；Common367第2帧为位图组合，1倍RGBA重现通过；来源受限，未机械放大。
- [ ] [inventory_action_sell.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_action_sell.png) — 77×31；Common381第1帧含位图依赖，1倍RGBA重现通过；来源受限，原图保持。
- [x] [inventory_actions_background.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_actions_background.png) — 87×115 → 174×230；Commonv3720 shape369原矢量底板2倍重导；按钮及显示尺寸保持。已实际接入，待本批审查。
- [ ] [inventory_attributes.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_attributes.png) — 325×170；已复核BackPackInterfacev1000 shape51；现有源像素或合成保真受限，正式原图保持。
- [ ] [inventory_cell.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_cell.png) — 50×50；已复核common元件43.png；现有源像素或合成保真受限，正式原图保持。
- [x] [inventory_exp_fill.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_exp_fill.png) — 241×18 → 482×36；backpack_v2 shape7原矢量2倍重导；原位置、原逻辑显示范围和经验进度保持。已实际接入，待本批审查。
- [x] [inventory_exp_frame.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_exp_frame.png) — 213×20 → 426×40；backpack_v2 shape9原矢量2倍重导；原位置、原逻辑显示范围和经验进度保持。已实际接入，待本批审查。
- [ ] [inventory_ink_frame.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_ink_frame.png) — 702×490；已复核backpack原位图2.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [inventory_level_badge.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_level_badge.png) — 83×59；已复核BackPackInterfacev1000 bitmap22；现有源像素或合成保真受限，正式原图保持。
- [ ] [inventory_name_plate.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_name_plate.png) — 213×51；已查现有导出与旧图，尚未确认可保真提高细节的同内容源，正式原图保持。
- [ ] [inventory_next_page.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_next_page.png) — 87×33；已查现有导出与旧图，尚未确认可保真提高细节的同内容源，正式原图保持。
- [ ] [inventory_next_page_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_next_page_hover.png) — 87×33；已查现有导出与旧图，尚未确认可保真提高细节的同内容源，正式原图保持。
- [ ] [inventory_panels.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_panels.png) — 941×591；已复核backpack3_shape元件4；现有源像素或合成保真受限，正式原图保持。
- [ ] [inventory_preview_light.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_preview_light.png) — 960×1152；已复核BackPackInterfacev1000 bitmap74；现有源像素或合成保真受限，正式原图保持。
- [ ] [inventory_previous_page.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_previous_page.png) — 87×33；已查现有导出与旧图，尚未确认可保真提高细节的同内容源，正式原图保持。
- [ ] [inventory_previous_page_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_previous_page_hover.png) — 87×33；已查现有导出与旧图，尚未确认可保真提高细节的同内容源，正式原图保持。
- [ ] [inventory_slot_accessory.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_slot_accessory.png) — 51×51；已复核BackPackInterfacev1000；现有源像素或合成保真受限，正式原图保持。
- [ ] [inventory_slot_armor.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_slot_armor.png) — 51×51；已复核BackPackInterfacev1000；现有源像素或合成保真受限，正式原图保持。
- [ ] [inventory_slot_costume.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_slot_costume.png) — 50×50；已查现有导出与旧图，尚未确认可保真提高细节的同内容源，正式原图保持。
- [ ] [inventory_slot_title.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_slot_title.png) — 50×50；已查现有导出与旧图，尚未确认可保真提高细节的同内容源，正式原图保持。
- [ ] [inventory_slot_treasure.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_slot_treasure.png) — 51×51；已复核BackPackInterfacev1000；现有源像素或合成保真受限，正式原图保持。
- [ ] [inventory_slot_weapon.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_slot_weapon.png) — 51×51；已复核BackPackInterfacev1000；现有源像素或合成保真受限，正式原图保持。
- [ ] [inventory_soul_plate.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_soul_plate.png) — 129×28；已查现有导出与旧图，尚未确认可保真提高细节的同内容源，正式原图保持。
- [ ] [inventory_tab_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_tab_normal.png) — 71×25；已查现有导出与旧图，尚未确认可保真提高细节的同内容源，正式原图保持。
- [ ] [inventory_tab_selected.png](../Godot/GodotProject/TheGame/Sprites/UI/inventory/inventory_tab_selected.png) — 71×25；已查现有导出与旧图，尚未确认可保真提高细节的同内容源，正式原图保持。

目录：`TheGame/Sprites/UI/settlement/`

- [ ] [settlement_backdrop.png](../Godot/GodotProject/TheGame/Sprites/UI/settlement/settlement_backdrop.png) — 940×590；现有中心为低分辨率位图，已有外围扩图候选不增加中心细节，本批保留正式原图。
- [ ] [settlement_failure.png](../Godot/GodotProject/TheGame/Sprites/UI/settlement/settlement_failure.png) — 510×304；已复核OtherMatv3570 bitmap248；现有源像素或合成保真受限，正式原图保持。
- [ ] [settlement_map_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/settlement/settlement_map_hover.png) — 160×59；已复核OtherMatv3570 bitmap239；现有源像素或合成保真受限，正式原图保持。
- [ ] [settlement_map_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/settlement/settlement_map_normal.png) — 160×59；已复核other3元件237.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [settlement_results.png](../Godot/GodotProject/TheGame/Sprites/UI/settlement/settlement_results.png) — 402×79；已复核other3_bitmap元件230；现有源像素或合成保真受限，正式原图保持。
- [ ] [settlement_retry_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/settlement/settlement_retry_hover.png) — 161×60；已复核other3_bitmap元件253；现有源像素或合成保真受限，正式原图保持。
- [ ] [settlement_retry_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/settlement/settlement_retry_normal.png) — 161×60；已复核other3元件250.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [settlement_success.png](../Godot/GodotProject/TheGame/Sprites/UI/settlement/settlement_success.png) — 510×304；已复核OtherMatv3570 bitmap226；现有源像素或合成保真受限，正式原图保持。

目录：`TheGame/Sprites/UI/skills/`

- [ ] [skill_blazing_flash_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_blazing_flash_learned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_blazing_flash_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_blazing_flash_unlearned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_dark_ice_thorn_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_dark_ice_thorn_learned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_dark_ice_thorn_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_dark_ice_thorn_unlearned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_fire_demon_slash_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_fire_demon_slash_learned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_fire_demon_slash_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_fire_demon_slash_unlearned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_flame_storm_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_flame_storm_learned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_flame_storm_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_flame_storm_unlearned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_flame_thrust_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_flame_thrust_learned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_flame_thrust_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_flame_thrust_unlearned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_heavenly_dew_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_heavenly_dew_learned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_heavenly_dew_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_heavenly_dew_unlearned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_ice_dragon_wave_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_ice_dragon_wave_learned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_ice_dragon_wave_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_ice_dragon_wave_unlearned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_rising_dragon_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_rising_dragon_learned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_rising_dragon_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_rising_dragon_unlearned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_water_clone_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_water_clone_learned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_water_clone_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_water_clone_unlearned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_water_demon_burst_learned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_water_demon_burst_learned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skill_water_demon_burst_unlearned.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skill_water_demon_burst_unlearned.png) — 65×65；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [x] [skills_active_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_active_hover.png) — 79×19 → 158×38；OtherMat_v9原技能按钮轮廓2倍重导；普通/悬停及命中范围保持。已实际接入，待本批审查。
- [x] [skills_active_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_active_normal.png) — 79×19 → 158×38；OtherMat_v9原技能按钮轮廓2倍重导；普通/悬停及命中范围保持。已实际接入，待本批审查。
- [x] [skills_back_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_back_hover.png) — 54×20 → 108×40；OtherMat_v9原技能按钮轮廓2倍重导；普通/悬停及命中范围保持。已实际接入，待本批审查。
- [x] [skills_back_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_back_normal.png) — 54×20 → 108×40；OtherMat_v9原技能按钮轮廓2倍重导；普通/悬停及命中范围保持。已实际接入，待本批审查。
- [ ] [skills_background.jpg](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_background.jpg) — 940×590；现有中心为低分辨率位图，已有外围扩图候选不增加中心细节，本批保留正式原图。
- [x] [skills_passive_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_passive_hover.png) — 79×19 → 158×38；OtherMat_v9原技能按钮轮廓2倍重导；普通/悬停及命中范围保持。已实际接入，待本批审查。
- [x] [skills_passive_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_passive_normal.png) — 79×19 → 158×38；OtherMat_v9原技能按钮轮廓2倍重导；普通/悬停及命中范围保持。已实际接入，待本批审查。
- [ ] [skills_sanzang_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_sanzang_normal.png) — 61×35；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skills_sanzang_selected.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_sanzang_selected.png) — 61×35；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skills_soul_badge.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_soul_badge.png) — 74×66；已复核other原位图586.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [skills_table_active.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_table_active.png) — 746×429；已复核other原位图581.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [skills_table_passive.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_table_passive.png) — 746×429；已复核other原位图583.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [skills_upgrade_hover.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_upgrade_hover.png) — 48×31；已复核other原位图569.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [skills_upgrade_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_upgrade_normal.png) — 48×31；已复核other原位图569.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [skills_upgrade_pressed.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_upgrade_pressed.png) — 48×31；已复核other原位图569.png；现有源像素或合成保真受限，正式原图保持。
- [ ] [skills_wukong_normal.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_wukong_normal.png) — 60×34；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。
- [ ] [skills_wukong_selected.png](../Godot/GodotProject/TheGame/Sprites/UI/skills/skills_wukong_selected.png) — 60×34；已复核OtherMat_v9，原技能导出器；1倍37图逐项可见RGBA全等；现有源像素或合成保真受限，正式原图保持。

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

- [ ] [damage_number_hero_magic.png](../Godot/GodotProject/TheGame/Sprites/Number/damage_number_hero_magic.png) — 300×30；已确认pnum0～9为原生30×30位图，各符号/尺寸登记；来源受限，未提高精度。
- [ ] [damage_number_hero_physics.png](../Godot/GodotProject/TheGame/Sprites/Number/damage_number_hero_physics.png) — 300×30；待来源/清晰度处理及1080p验收。
- [ ] [damage_number_miss.png](../Godot/GodotProject/TheGame/Sprites/Number/damage_number_miss.png) — 55×24；OtherMat miss/bitmap76为原生55×24位图，来源受限，未提高精度。
- [ ] [damage_number_monster_magic.png](../Godot/GodotProject/TheGame/Sprites/Number/damage_number_monster_magic.png) — 300×30；待来源/清晰度处理及1080p验收。
- [ ] [damage_number_monster_magic_crit.png](../Godot/GodotProject/TheGame/Sprites/Number/damage_number_monster_magic_crit.png) — 320×32；待来源/清晰度处理及1080p验收。
- [ ] [damage_number_monster_physics.png](../Godot/GodotProject/TheGame/Sprites/Number/damage_number_monster_physics.png) — 300×30；hurtnum0～9为原生30×30位图，各符号/尺寸登记；来源受限，不把重新拼接当高清。
- [ ] [damage_number_monster_physics_crit.png](../Godot/GodotProject/TheGame/Sprites/Number/damage_number_monster_physics_crit.png) — 420×42；bnum0～9为原生42×42位图，各符号/尺寸登记；来源受限，未提高精度。
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

## 配套美术资源（183项，不计入图像673项）

这部分列全部业务字体、着色器、独立美术`.tres`文件及地图BitMap遮罩；嵌入其中的子资源不重复记为文件。帧动画的源PNG仍在上表，不能因为动画库已核对就把它引用的所有图片一并勾掉。音频、业务数值Resource、bundle、C#和场景消费者不计为本表美术源；它们必要的接入任务已在上方及Validation记录。

| 分类 | 总数 | 本批已联动/核对 | 待处理 |
| --- | ---: | ---: | ---: |
| [字体](#art-support-01) | 8 | 0 | 8 |
| [着色器](#art-support-02) | 4 | 2 | 2 |
| [动画库](#art-support-03) | 71 | 34 | 37 |
| [逐帧动画资源](#art-support-04) | 79 | 34 | 45 |
| [外观与物品美术登记](#art-support-05) | 5 | 4 | 1 |
| [主题与样式](#art-support-06) | 13 | 0 | 13 |
| [地图透明命中遮罩](#art-support-07) | 3 | 2 | 1 |
| **合计** | **183** | **76** | **107** |

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

### 着色器（2/4）

目录：`TheGame/Sprites/Characters/Heroes/Status/`

- [x] [hero_invulnerability.gdshader](../Godot/GodotProject/TheGame/Sprites/Characters/Heroes/Status/hero_invulnerability.gdshader) — CanvasItem Shader；已按像素格/密度保持原光晕范围，60/120Hz验证通过。

目录：`TheGame/Sprites/UI/frontend/`

- [x] [character_card.gdshader](../Godot/GodotProject/TheGame/Sprites/UI/frontend/character_card.gdshader) — CanvasItem Shader；原UV算法保持；已验证正式分层共用材质的灰度/悬停颜色、透明边和原遮罩；仅注释同步新分层语义。
- [ ] [map_location_outline.gdshader](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_location_outline.gdshader) — CanvasItem Shader；待相关高清素材的像素采样与逻辑范围联动。

目录：`TheGame/Sprites/UI/inventory/`

- [ ] [equipment_slot_background.gdshader](../Godot/GodotProject/TheGame/Sprites/UI/inventory/equipment_slot_background.gdshader) — CanvasItem Shader；待相关高清素材的像素采样与逻辑范围联动。

<a id="art-support-03"></a>

### 动画库（34/71）

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_02_macaque_king/`

- [x] [macaque_king_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_02_macaque_king/macaque_king_library.tres) — AnimationLibrary；仅换算视觉offset，方法/时序/判定轨保持；上一批验证通过，已审。
- [x] [macaque_wind_blade_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_02_macaque_king/macaque_wind_blade_library.tres) — AnimationLibrary；仅换算视觉offset，方法/时序/判定轨保持；上一批验证通过，已审。

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_03_tamarin_king/`

- [x] [tamarin_king_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_03_tamarin_king/tamarin_king_library.tres) — AnimationLibrary；仅换算视觉offset，方法/时序/判定轨保持；上一批验证通过，已审。

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_04_bat_cave/`

- [ ] [bat_cave_statue_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_04_bat_cave/bat_cave_statue_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_05_qixing_cave/`

- [x] [lion_camel_king_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_king_library.tres) — AnimationLibrary；仅换算视觉offset，方法/时序/判定轨保持；上一批验证通过，已审。
- [ ] [lion_camel_poison_cloud_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_poison_cloud_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_06_garuda_cave/`

- [ ] [boss_death_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_06_garuda_cave/boss_death_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [x] [peng_demon_king_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_06_garuda_cave/peng_demon_king_library.tres) — AnimationLibrary；仅换算视觉offset，方法/时序/判定轨保持；上一批验证通过，已审。
- [ ] [peng_fire_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_06_garuda_cave/peng_fire_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_08_east_sea/`

- [x] [jiao_demon_king_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_08_east_sea/jiao_demon_king_library.tres) — AnimationLibrary；仅换算视觉offset，方法/时序/判定轨保持；上一批验证通过，已审。
- [ ] [jiao_dragon_wave_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_08_east_sea/jiao_dragon_wave_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_09_shark_whirlpool/`

- [x] [shark_demon_king_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_09_shark_whirlpool/shark_demon_king_library.tres) — AnimationLibrary；仅换算视觉offset，方法/时序/判定轨保持；上一批验证通过，已审。
- [ ] [shark_king_bubble_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_bubble_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [shark_king_bubble_warning_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_bubble_warning_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [shark_king_shark_wave_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_shark_wave_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_10_bull_demon_valley/`

- [x] [bull_demon_beast_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_beast_library.tres) — AnimationLibrary；仅换算视觉offset，方法/时序/判定轨保持；上一批验证通过，已审。
- [x] [bull_demon_heart_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_heart_library.tres) — AnimationLibrary；仅换算视觉offset，方法/时序/判定轨保持；上一批验证通过，已审。
- [x] [bull_demon_king_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_king_library.tres) — AnimationLibrary；仅换算视觉offset，方法/时序/判定轨保持；上一批验证通过，已审。
- [ ] [bull_fire_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_10_bull_demon_valley/bull_fire_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [bull_heart_fade_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_10_bull_demon_valley/bull_heart_fade_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [bull_presentation_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_10_bull_demon_valley/bull_presentation_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Bosses/zm1_11_hidden_dragon_palace/`

- [x] [dragon_king_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_library.tres) — AnimationLibrary；仅换算视觉offset，方法/时序/判定轨保持；上一批验证通过，已审。
- [ ] [dragon_king_torrent_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_torrent_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [dragon_king_wave_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_wave_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Ground/bull_guard/`

- [x] [bull_guard_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Ground/bull_guard/bull_guard_library.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Ground/cave_brute/`

- [ ] [cave_brute_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Ground/cave_brute/cave_brute_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Ground/demon_monkey/`

- [x] [demon_monkey_anim_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Ground/demon_monkey/demon_monkey_anim_library.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Ground/gorilla/`

- [x] [gorilla_anim_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Ground/gorilla/gorilla_anim_library.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Ground/huaguoshan_monkey/`

- [x] [huaguoshan_monkey_anim_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_anim_library.tres) — AnimationLibrary；仅6个身体offset值密度换算，全部方法/标记/形状/时间保持，138帧真实朝向/原点及实机攻击受击死亡/复用验证通过。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Ground/knife_demon/`

- [x] [knife_demon_blade_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Ground/knife_demon/knife_demon_blade_library.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。
- [x] [knife_demon_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Ground/knife_demon/knife_demon_library.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Sea/lantern_fish/`

- [x] [lantern_fish_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sea/lantern_fish/lantern_fish_library.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Sea/shark_spawn/`

- [x] [shark_spawn_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sea/shark_spawn/shark_spawn_library.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。
- [ ] [shark_spawn_slash_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sea/shark_spawn/shark_spawn_slash_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [shark_spawn_tornado_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sea/shark_spawn/shark_spawn_tornado_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [x] [shark_spawn_warning_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sea/shark_spawn/shark_spawn_warning_library.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Sea/shrimp_demon/`

- [x] [shrimp_demon_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sea/shrimp_demon/shrimp_demon_library.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Sea/tide_demon/`

- [x] [tide_demon_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sea/tide_demon/tide_demon_library.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Sea/turtle_minister/`

- [x] [turtle_minister_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sea/turtle_minister/turtle_minister_library.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Sky/bat/`

- [ ] [bat_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sky/bat/bat_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Enemies/Monsters/Sky/flying_demon/`

- [x] [flying_demon_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Enemies/Monsters/Sky/flying_demon/flying_demon_library.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Entitys/Animations/Heroes/Tangseng/`

- [x] [tangseng_anim_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Tangseng/tangseng_anim_library.tres) — AnimationLibrary；已核对全部时序/引用并验证高清显示；库文件原字节保持。
- [ ] [tangseng_cure_role_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Tangseng/tangseng_cure_role_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [tangseng_dark_ice_effect_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Tangseng/tangseng_dark_ice_effect_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [x] [tangseng_preview_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Tangseng/tangseng_preview_library.tres) — AnimationLibrary；已核对全部时序/引用并验证高清显示；库文件原字节保持。
- [x] [tangseng_skill_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Tangseng/tangseng_skill_library.tres) — AnimationLibrary；已核对全部时序/引用并验证高清显示；库文件原字节保持。
- [ ] [tangseng_water_clone_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Tangseng/tangseng_water_clone_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [tangseng_water_demon_fall_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Tangseng/tangseng_water_demon_fall_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/Heroes/Wukong/`

- [x] [wukong_anim_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Wukong/wukong_anim_library.tres) — AnimationLibrary；只换特效offset像素与视觉scale，方法/时刻/判定保持；本批原生及实际行为已验，待审。
- [x] [wukong_blazing_flash_trail_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Wukong/wukong_blazing_flash_trail_library.tres) — AnimationLibrary；原资源字节/时序保持，已验证高清帧绑定、实际播放与回收，本批待审。
- [ ] [wukong_fire_demon_combo_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Wukong/wukong_fire_demon_combo_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [wukong_fire_demon_descent_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Wukong/wukong_fire_demon_descent_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [wukong_fire_demon_landing_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Wukong/wukong_fire_demon_landing_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [x] [wukong_preview_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Wukong/wukong_preview_library.tres) — AnimationLibrary；已核对全部时序/引用并验证高清显示；库文件原字节保持。
- [x] [wukong_skill_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/Wukong/wukong_skill_library.tres) — AnimationLibrary；只换特效offset像素与视觉scale，方法/时刻/判定保持；本批原生及实际行为已验，待审。

目录：`TheGame/Entitys/Animations/Heroes/`

- [ ] [hero_tombstone_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/Heroes/hero_tombstone_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Entitys/Animations/`

- [ ] [ground_item_pickup_library.tres](../Godot/GodotProject/TheGame/Entitys/Animations/ground_item_pickup_library.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [shuiliandong_river_animation.tres](../Godot/GodotProject/TheGame/Entitys/Animations/shuiliandong_river_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Scenes/Levels/zm1_02_shuiliandong/`

- [ ] [hidden_entrance_fade.tres](../Godot/GodotProject/TheGame/Scenes/Levels/zm1_02_shuiliandong/hidden_entrance_fade.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Sprites/UI/frontend/`

- [ ] [map_bianfuku_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_bianfuku_hover_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [x] [map_donghai_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_donghai_hover_animation.tres) — AnimationLibrary；原19帧24fps/全部时刻保持；东海原生页轨及循环、浅岸原字节检查通过。
- [ ] [map_jialuoluo_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_jialuoluo_hover_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [map_location_hover.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_location_hover.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [map_niumogu_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_niumogu_hover_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [x] [map_qianan_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qianan_hover_animation.tres) — AnimationLibrary；原19帧24fps/全部时刻保持；东海原生页轨及循环、浅岸原字节检查通过。
- [ ] [map_qixingdong_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qixingdong_hover_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [map_shazhiwoliu_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shazhiwoliu_hover_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [map_shuiliandong_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shuiliandong_hover_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [map_taohuayuan_hover_animation.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_taohuayuan_hover_animation.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。
- [ ] [save_feedback.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/save_feedback.tres) — AnimationLibrary；待关联美术批次的帧引用、时序、定位及实际播放检查。

目录：`TheGame/Sprites/UI/hud/musou_activation/shared/`

- [x] [musou_activation_library.tres](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/shared/musou_activation_library.tres) — 原24时刻与一秒长度保持，加入同帧共用光束轨道，原生与实际播放已验。

<a id="art-support-04"></a>

### 逐帧动画资源（34/79）

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/`

- [x] [macaque_king_body_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/macaque_king_body_frames.tres) — SpriteFrames；原生分页region/margin与完整帧序/24fps保持；上一批验证通过，已审。
- [ ] [macaque_king_effect_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/macaque_king_effect_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [x] [macaque_wind_blade_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_02_macaque_king/macaque_wind_blade_frames.tres) — SpriteFrames；原生分页region/margin与完整帧序/24fps保持；上一批验证通过，已审。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_03_tamarin_king/`

- [x] [tamarin_king_body_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_03_tamarin_king/tamarin_king_body_frames.tres) — SpriteFrames；原生分页region/margin与完整帧序/24fps保持；上一批验证通过，已审。
- [ ] [tamarin_king_effect_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_03_tamarin_king/tamarin_king_effect_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_04_bat_cave/`

- [ ] [bat_cave_statue_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_04_bat_cave/bat_cave_statue_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_05_qixing_cave/`

- [x] [lion_camel_king_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_king_frames.tres) — SpriteFrames；原生分页region/margin与完整帧序/24fps保持；上一批验证通过，已审。
- [ ] [lion_camel_poison_cloud_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_05_qixing_cave/lion_camel_poison_cloud_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_06_garuda_cave/`

- [x] [peng_demon_king_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_06_garuda_cave/peng_demon_king_frames.tres) — SpriteFrames；原生分页region/margin与完整帧序/24fps保持；上一批验证通过，已审。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/`

- [ ] [jiao_back_hit_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/jiao_back_hit_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [x] [jiao_demon_king_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/jiao_demon_king_official_frames.tres) — SpriteFrames；原生分页region/margin与完整帧序/24fps保持；上一批验证通过，已审。
- [ ] [jiao_dragon_wave_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_08_east_sea/jiao_dragon_wave_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/`

- [x] [shark_demon_king_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_demon_king_official_frames.tres) — SpriteFrames；原生分页region/margin与完整帧序/24fps保持；上一批验证通过，已审。
- [ ] [shark_king_bubble_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_bubble_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [shark_king_bubble_warning_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_bubble_warning_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [shark_king_shark_wave_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_09_shark_whirlpool/shark_king_shark_wave_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/`

- [ ] [bull_background_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_background_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [x] [bull_demon_beast_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_beast_official_frames.tres) — SpriteFrames；原生分页region/margin与完整帧序/24fps保持；上一批验证通过，已审。
- [x] [bull_demon_heart_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_heart_official_frames.tres) — SpriteFrames；原生分页region/margin与完整帧序/24fps保持；上一批验证通过，已审。
- [x] [bull_demon_king_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_king_official_frames.tres) — SpriteFrames；原生分页region/margin与完整帧序/24fps保持；上一批验证通过，已审。
- [x] [bull_demon_king_tangseng_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_demon_king_tangseng_frames.tres) — SpriteFrames；原生分页region/margin与完整帧序/24fps保持；上一批验证通过，已审。
- [ ] [bull_fire_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_fire_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [bull_hands_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_hands_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [bull_heart_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_10_bull_demon_valley/bull_heart_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/`

- [ ] [dragon_king_ice_aura_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_ice_aura_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [x] [dragon_king_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_official_frames.tres) — SpriteFrames；原生分页region/margin与完整帧序/24fps保持；上一批验证通过，已审。
- [ ] [dragon_king_torrent_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_torrent_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [dragon_king_wave_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Bosses/zm1_11_hidden_dragon_palace/dragon_king_wave_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/bull_guard/`

- [x] [bull_guard_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/bull_guard/bull_guard_official_frames.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/cave_brute/`

- [ ] [cave_brute_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/cave_brute/cave_brute_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/`

- [ ] [demon_monkey_animations.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_animations.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [x] [demon_monkey_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/demon_monkey/demon_monkey_official_frames.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/`

- [ ] [gorilla_animations.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_animations.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [x] [gorilla_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/gorilla/gorilla_official_frames.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/`

- [ ] [huaguoshan_monkey_animations.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_animations.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [x] [huaguoshan_monkey_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/huaguoshan_monkey/huaguoshan_monkey_official_frames.tres) — SpriteFrames；23原姿态ID/69帧引用顺序、duration/24fps/loop/UID保持，仅更新高清region/margin，69帧解包与已审RGBA全等。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Ground/knife_demon/`

- [x] [knife_demon_blade_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/knife_demon/knife_demon_blade_frames.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。
- [x] [knife_demon_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Ground/knife_demon/knife_demon_frames.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/lantern_fish/`

- [x] [lantern_fish_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/lantern_fish/lantern_fish_official_frames.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/`

- [x] [shark_spawn_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/shark_spawn_official_frames.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。
- [ ] [shark_spawn_slash_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/shark_spawn_slash_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [shark_spawn_tornado_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/shark_spawn_tornado_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [x] [shark_spawn_warning_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shark_spawn/shark_spawn_warning_frames.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/shrimp_demon/`

- [x] [shrimp_demon_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/shrimp_demon/shrimp_demon_official_frames.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/tide_demon/`

- [x] [tide_demon_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/tide_demon/tide_demon_official_frames.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sea/turtle_minister/`

- [x] [turtle_minister_official_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sea/turtle_minister/turtle_minister_official_frames.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sky/bat/`

- [ ] [bat_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sky/bat/bat_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Characters/Enemies/Monsters/Sky/flying_demon/`

- [x] [flying_demon_frames.tres](../Godot/GodotProject/TheGame/Sprites/Characters/Enemies/Monsters/Sky/flying_demon/flying_demon_frames.tres) — 本批密度/region/margin与实际逐帧播放核对通过；原身份、时序、方法、判定及世界原点保持。

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

- [x] [loot_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/loot/loot_frames.tres) — SpriteFrames；原资源字节/时序保持，已验证高清帧绑定、实际播放与回收，上一批已审。

目录：`TheGame/Sprites/Effects/monster_hit/tangseng/`

- [ ] [tangseng_monster_hit_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/tangseng/tangseng_monster_hit_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Effects/monster_hit/wukong/`

- [ ] [wukong_monster_hit_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/monster_hit/wukong/wukong_monster_hit_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Effects/tangseng/skills/`

- [ ] [tangseng_cure_role_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_cure_role_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [tangseng_dark_ice_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_dark_ice_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [x] [tangseng_water_demon_ball_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_water_demon_ball_frames.tres) — SpriteFrames；只换region/margin，原帧序/duration/24fps保持；上一批原生及实际行为已验，已审。
- [ ] [tangseng_water_demon_fall_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/tangseng/skills/tangseng_water_demon_fall_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/Effects/wukong/skills/`

- [x] [wukong_blazing_flash_trail_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_blazing_flash_trail_frames.tres) — SpriteFrames；只换region/margin，原帧序/duration/24fps保持；上一批原生及实际行为已验，已审。
- [ ] [wukong_fire_demon_combo_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_combo_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [wukong_fire_demon_descent_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_descent_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [wukong_fire_demon_landing_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_landing_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [x] [wukong_fire_demon_prelude_frames.tres](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/skills/wukong_fire_demon_prelude_frames.tres) — SpriteFrames；只换region/margin，原帧序/duration/24fps保持；上一批原生及实际行为已验，已审。

目录：`TheGame/Sprites/Effects/wukong/`

- [x] [wukong_effect_animations.tres](../Godot/GodotProject/TheGame/Sprites/Effects/wukong/wukong_effect_animations.tres) — SpriteFrames；只换region/margin，原帧序/duration/24fps保持；上一批原生及实际行为已验，已审。

目录：`TheGame/Sprites/Levels/zm1_04_bat_cave/`

- [ ] [bat_cave_spike_frames.tres](../Godot/GodotProject/TheGame/Sprites/Levels/zm1_04_bat_cave/bat_cave_spike_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/UI/frontend/`

- [x] [map_skills_frames.tres](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_skills_frames.tres) — SpriteFrames；原资源字节、1/15/19帧、24fps及duration保持；独立题签仅hover显示，真实播放/退出/返回/回池复用检查通过。

目录：`TheGame/Sprites/UI/gogo/`

- [ ] [gogo_animations.tres](../Godot/GodotProject/TheGame/Sprites/UI/gogo/gogo_animations.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/UI/hud/`

- [ ] [battle_menu_ws_glow_frames.tres](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_menu_ws_glow_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [battle_skill_icon_frames.tres](../Godot/GodotProject/TheGame/Sprites/UI/hud/battle_skill_icon_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。
- [ ] [combo_digits_frames.tres](../Godot/GodotProject/TheGame/Sprites/UI/hud/combo_digits_frames.tres) — SpriteFrames；待原源高清帧、region/margin、时序、alpha和显示检查。

目录：`TheGame/Sprites/UI/hud/musou_activation/shared/`

- [x] [musou_activation_light_frames.tres](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/shared/musou_activation_light_frames.tres) — SpriteFrames；原密度共享光束图，24时刻/19姿态，与人物共用原播放器，原生与实际播放已验。

目录：`TheGame/Sprites/UI/hud/musou_activation/tangseng/`

- [x] [tangseng_musou_activation_frames.tres](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/tangseng/tangseng_musou_activation_frames.tres) — 人物2倍源、完整逻辑画布、原24帧与24fps保持，原生与实际播放已验。

目录：`TheGame/Sprites/UI/hud/musou_activation/wukong/`

- [x] [wukong_musou_activation_frames.tres](../Godot/GodotProject/TheGame/Sprites/UI/hud/musou_activation/wukong/wukong_musou_activation_frames.tres) — 人物2倍源、完整逻辑画布、原24帧与24fps保持，原生与实际播放已验。

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

### 地图透明命中遮罩（2/3）

目录：`TheGame/Sprites/UI/frontend/`

- [x] [map_donghai_hit_mask.res](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_donghai_hit_mask.res) — BitMap；原文件/尺寸保持，本批301774遮罩像素逐项核对及真实C#按钮鼠标进入/悬停/点击/退出通过。
- [x] [map_qianan_hit_mask.res](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_qianan_hit_mask.res) — BitMap；原文件/尺寸保持，本批301774遮罩像素逐项核对及真实C#按钮鼠标进入/悬停/点击/退出通过。
- [ ] [map_shazhiwoliu_hit_mask.res](../Godot/GodotProject/TheGame/Sprites/UI/frontend/map_shazhiwoliu_hit_mask.res) — BitMap；待地图地点高清后按原逻辑轮廓核对透明命中，不能随像素增密扩大点击范围。

<!-- art-support:end -->

## 当前未完成项与限制

- 本批UI中源像素受限的原图、未确认项与演出背景继续按上表追踪；选角分层与独立字体已接入，地图及整卡背景精度仍受限。
- 三项位图限制仍未勾选：花果山平台缺同内容高精度源，蝙蝠天空恢复收益不明确，粗糙行者棍恢复颜色不保真。原图继续使用，不伪报高清完成。
- 英雄/背包和选角修正画面已获用户认可；Android压缩、目标GPU纹理/显存、加载/FPS及真机触控未验。
- 当前保留keep，默认窗口1920×1080；正式横向扩展视野、外围覆盖和16:9～21:9整体适配未完成。
- 现有验证中的预期故障注入、编辑器插件关停和资源释放诊断详见Validation，不因出现PASS而隐藏。
