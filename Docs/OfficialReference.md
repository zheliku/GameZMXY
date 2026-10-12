# 造梦西游一：官方依据与项目差异

核对对象为第一代“七魔王篇”的 4399 公开版本，画布 940×590、动画 24 fps。不能混用造梦二、三的攻略或本地旧工程数值。以下秒数已从该版本时间轴换算，运行时不按游戏帧数计时。

## 原文件与复核入口

[4399 系列入口](https://www.4399.com/flash/zmhj.htm) → [游戏容器](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/v25928.htm)。只读解包、脚本和 XML 留在临时目录；工程只迁入登记的美术资源。

| 官方文件 | 核对内容 | SHA-256 |
| --- | --- | --- |
| [v25928.swf](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/v25928.swf) | 主程序、第一关标记、RoleInfo、GameInfo | `a5e9650ca4c3b779d62fb7987a0b7e7d5af5983b3197fa2f2e846a91b3f9ccce`（下载文件） |
| [Role_v7.swf](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/Role_v7.swf) | BaseHero、Role1、WsEffect、升级动画 | `11cae2f4f4e63dbb1e7199b9d1a8ed432036138b72e2d04d1da974329133fbe7`（恢复后） |
| [Monster_v1.swf](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/Monster_v1.swf) | 怪物属性、掉落球、BossBlood | `a6552637ac61553c3e021c79b725587cba66f6303e3245180ababd1f7db97026`（下载文件）；`5bd88da4211870f2ca2354b7cec8d1790747869641c11fe553910b10fe2a264a`（恢复后） |
| [OtherMat_v9.swf](https://sbai.4399.com/4399swf/upload_swf/ftp5/hanbao/20110624/3/OtherMat_v9.swf) | SelectRole 完整选角卡片 | `ef03008e68d64f613a2f7b3abfeec4cba3d4bd579c67e0f4703d8f15d3e115c9`（下载文件）；`fa7681aad99fb06f50228530b7f70f6334a1d7052a7bb6ef2ce3c3d81867fd5e`（恢复后） |

资源包按加载器恢复为 `bytes[100:110] + bytes[0:100] + bytes[110:]`；主程序另从外层 CWS/DefineBinaryData 读取。JPEXS FFDec 26.3 导出脚本和 XML，坐标 TWIPS ÷ 20。素材编号及复现脚本见 [LegacyAssetMap](LegacyAssetMap.md)。

## 无双：加成与蓄力分开

依据 `BaseHero.turnToGXP/turnToNormal`、`Role1.getRealPower`、`BaseRoleProperies.getBasePower`、`RoleInfo.addWs/lessWs/addWarriors`。

| 配置字段（BattleConfig.xlsx） | 官方值 | 生效方式 |
| --- | ---: | --- |
| WsMax | 100 | 蓄满才能开启 |
| WsGainPerDamageRatio | 2 | 每次有效命中增加 `round(实际伤害 / 基础攻击 × 2)`；不是攻击倍率 |
| WsAttackMultiplier | 1.5 | 整招威力乘 1.5，含固定加值；仅在出招装填时应用一次 |
| WsMoveSpeedMultiplier | 1.5 | 走、跑的横向速度 |
| WsJumpSpeedMultiplier | 1.5 | 起跳速度 |
| WsGravityMultiplier | 2.5 | 原重力 1.5 → 3.75；不能只提高跳速而漏掉重力 |
| WsDecayPerSecond | 1 | 未满时自然衰减；满值冻结相位，命中不重置；开启后未满仍生效 |
| WsDrainPerSecond | 6 | 开启后的额外消耗，与自然衰减叠加 |
| WsIntroPauseDuration | 1 秒 | 开启演出暂停战斗，暂停不扣无双 |

满值后停止自然衰减；开启期间不接受命中蓄力，但黄球仍能补充。超过上限的整次增加会被忽略，不截取剩余空间。没有拾取时，无双开启后的有效战斗时间约 14.3 秒，受开启前保留的自然衰减相位影响。

悟空开启后免受击硬直与击退，仍扣生命；没有额外生命、魔法、防御、暴击倍率。唐僧的受击分支不同，当前尚未实现其玩法，不能把悟空霸体泛称为所有角色的官方规则。

计时由 `MusouGauge.Advance(deltaSeconds)` 累积点数，满一点再扣；配置与运行时均不依赖 24/60/120 fps。原文件每 24 帧自然减一点、每 4 帧额外减一点，只作为换算依据。

## 球的颜色与用途

颜色以原始矢量帧为准，不能按 ActionScript 类名猜。`auraWhile` 实际呈黄色；`auraRed` 实际呈紫色。

| 原类 / Monster_v1 sprite | 实际颜色 | 用途 |
| --- | --- | --- |
| auraWhile / 5 | 黄色外环、白色中心 | 每颗 +5 无双，不加经验 |
| auraRed / 9 | 紫色 | 灵魂；猴子/妖猴/大猩猩每颗 1/2/3 |
| auraBlue / 7 | 蓝色 | 恢复 `max(10, floor(最大魔法 × 5%))` |
| auraGreen / 3 | 绿色 | 官方回血；当前项目尚未接入 |

官方死亡掉落灵魂 2～4 颗；绿、蓝、黄三类分别独立以 12% 概率生成 1～3 颗。当前实现灵魂、蓝球、黄球，经验在击败时立即结算，不存在经验球。

## 无双演出与 BOSS 条

- `WsEffect`（sprite 59）放在 `(470,295)`；背景 52 共 24 帧、悟空立绘 55 共 11 帧后停在末姿势。`WsEffect.as` 监听背景到末帧即移除整段演出，时长约 1 秒，没有额外大立绘循环。
- 开场后 `BaseHero.stepOther → BaseObject.shadowEffect` 持续产生角色残影：每 1/6 秒采样、每道淡出 1 秒；TweenMax 默认 Quad.easeOut，对应剩余透明度 `(1−t)²`，保留原色。无双耗尽后停止新增，已有残影继续淡完。
- `GameInfo.addBossBlood` 把完整 `BossBlood`（568）放在 `(450,50)`。条底形状 540 的边界为 `(-164.5,-14)` 起、339×28；名称子片段 567 在 `(-186,5)`，姓名字形围绕该点排列。项目复用旧笔刷 PNG 和字体，保留内部间距与条顶 y=36；按用户确认将“名称＋条体”整体顶部居中，不把原组件注册点误当作整组中心，不显示生命数字。
- 外置资源：`Sprites/UI/hud/musou_activation/musou_activation_frames.tres`（开场）、`hud_ws_full_frames.tres`（满值提示）、`UIs/Widgets/encounter_health_bar.tscn`（守关条）。

## 花果山标记与配方

`GameSence` 的 `1-1` 帧给出下表。官方生成器先等 delay，再等一次 interval，项目的首只 Delay 因而填写二者之和；单人并存上限 6。坐标为官方参考，当前场景中用户已调整的点位不被文档覆盖。

| 波次 | 生成点 ID | 官方 (x,y) | 怪物×数量 | 首只 / 后续间隔（秒） |
| --- | --- | --- | --- | --- |
| 1 | stage_1_a | 1256.40,377.30 | 猴子×3 | 3 / 1 |
| 2 | stage_2_a | 1863.00,428.65 | 猴子×5 | 3 / 1 |
| 2 | stage_2_b | 806.35,359.40 | 妖猴×1 | 6 / 1 |
| 2 | stage_2_c | 1863.00,377.25 | 妖猴×1 | 6 / 1 |
| 3 | stage_3_a | 2059.65,284.45 | 妖猴×1 | 4 / 2 |
| 3 | stage_3_b | 2349.75,116.75 | 妖猴×1 | 4 / 2 |
| 3 | stage_3_c | 2633.45,291.55 | 妖猴×1 | 4 / 2 |
| 3 | stage_3_d | 1705.35,369.35 | 妖猴×3 | 6 / 2 |
| 4 | stage_4_a | 3485.20,383.30 | 猴子×5 | 3 / 1 |
| 4 | stage_4_b | 3570.80,383.30 | 妖猴×1 | 4 / 1 |
| 4 | stage_4_c | 3520.15,383.30 | 妖猴×1 | 6 / 1 |
| 5 | stage_5_a | 4295.65,376.40 | 大猩猩×1 | 4 / 1 |
| 5 | stage_5_b | 4590.05,354.40 | 妖猴×4 | 4 / 1 |
| 5 | stage_5_c | 3772.85,382.20 | 妖猴×5 | 9 / 1 |

StopPoint 与 ViewControllor 可推导左右战斗区域：首波 `[0,1139.90]`，之后依次 `[359.95,1809.95]`、`[1520.15,2970.15]`、`[1963.50,3413.50]`、`[2752.85,4202.85]`。区域允许重叠；官方抵达采样存在约 ±40px 的窗口。英雄按屏幕 x=20～920 限制移动，场外怪物能走入画面。

## 基础数值与保留差异

| 怪物 | 基础 HP / 物防 / 普攻威力 / 经验 |
| --- | --- |
| 猴子 | 50 / 0 / 5 / 5 |
| 妖猴 | 100 / 1 / 12 / 6 |
| 大猩猩 | 300 / 5 / 20 / 7 |

花果山守关复用唯一大猩猩配置，配方生命倍率 3、物防/攻击倍率 1、经验覆盖 25；共 33 只、204 经验。倍率按每条 Recipes 独立配置，ExperienceOverride=-1 使用基础奖励。

悟空 HP=`80+50×(等级−1)`，MP=`50+20×(等级−1)`，基础攻击=`10+5×(等级−1)`，普攻额外随机整数 0～4；防御 15 级前每级 +2，之后每级 +5。一级升级需要 135 经验，新档通关为 2 级、69/145。

尚存项目选择：55 级上限、比例减伤/等级压制/闪避公式、怪物反应时间和前摇、中央 ±40px 相机死区、精确锚点触发、用户调整的地形/点位，以及小怪血条 4 秒淡出（官方 2 秒）。以上不宣称与官方逐项相同。
