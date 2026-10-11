# 业务代码风格审查与整理

2026-10-11。用户确认入口界面整理版本作为后续风格；规范集中在 [10-CSharp.md](ProjectGuidelines/10-CSharp.md)，本页记录审查发现，不另立规则。

范围：扫描 `TheGame/GameScripts` 与 `MainPack/Scripts` 的手写生产 C#，排除 GameProto、生成 UI 绑定和 Debug 验证代码。按字段命名、具名订阅、状态重复、异步作用域、主要类型落位、测试专用 API 和模块依赖筛查；重点阅读并修改流程、存档、共享 UI 和实体物理入口。没有把未运行的联网热更分支记作已验证。

| 范围 | 发现与处理 |
| --- | --- |
| Title / Character / Map / MenuProcedure | 删除地图的档案 Data 与页脚；静态图片页面只发请求。角色/地图返回标题共用保存等待，等待前捕获所属周期。存档选择由标题拥有，避免加一个无必要的框架流程。 |
| Save / Session | 明确按选中 UnitId 加载；检查点核对文件所有权。目录错误和损坏格保留原文件。Save 按 Data / Slots 分类；小枚举随所属结果或状态类型，LevelRunOutcome / RunStats 随 LevelRun；移除无参“读最新档”入口。 |
| QuestionTips / LoadingForm | 匿名按钮订阅改具名；关闭即清理回调，点击先捕获回调再关闭。补充池复用层级；清理空区域模板，加载窗口每次打开复位关闭请求。ITips 独立文件，保留框架实际使用的契约。 |
| Preload / Launch / Update | 预加载异步结果核对进入周期；删除只调用基类的空重写。热更字段统一命名、私有方法补调用契约；联网下载算法保持既有实现，本轮只验证 Package 启动路径。 |
| Hero / Monster / 身体与 AI 状态 | 物理暂停入口保持一致，怪物零步长不推进身体状态或移动。BodyFsm 保留 C# 14 扩展调度职责，状态仍只写决策。两类猴子的状态集声明保留，以符合第 30 章具体行为类型自己选择状态集的约定；CombatSide 与结算快照同文件的既有人类裁决保留。 |
| Profile / Battle / 飘字 | 删除仅测试使用的 IsMaxLevel 转发，断言直接读经验曲线与 Level。飘字固定数值使用 const；英雄无双修正来源字段统一命名。纯成长、公式与阶段规则由既有 124 项单测覆盖。 |
| Level / 配置 / 资源 | 阶段、出生点、门、刷怪与相机职责保持独立；官方区域重叠契约继续生效。窗口和配置绑定从正式源生成，不手改生成代码。 |

生产代码未添加观测 getter、状态名 API 或测试开关；引擎验证继续使用真实 UI、IFsm、节点和反射。完整命令、结果及框架退出限制见 [FrontendDelivery.md](FrontendDelivery.md)。

本轮继续检查了手写生产代码中的独立小枚举、流程键、短转发类型及攻击范围依赖。`FlowKeys` 与包装集中管理；删除 `LevelSelection` 常量壳、分散的 DataKey、独立枚举文件与静态选角页的闲置参数。怪物范围推导移入攻击 partial，删除两类猴子完全相同的重写；身体调度扩展、纯成长规则、存档映射和具名池化事件仍有明确职责，不为减少文件数破坏边界。

关于惯例：微软的 [枚举设计指南](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/enum) 讨论枚举用途和命名；它没有要求每个小枚举单独成文件。本项目选择按职责放置配套类型，同时保持顶级类型名和公开契约。地图使用引擎原生 [TextureButton 点击遮罩](https://docs.godotengine.org/en/stable/classes/class_texturebutton.html#class-texturebutton-property-texture-click-mask)，无需新增自定义点击组件。

用户随后取消旧存档兼容：删除 PlayerSaveDataV0、SaveMigrator、SaveOrigin.Migrated、历史等级 Restore 重载和旧目录推导；只支持 v3，ProfileMapper 与 DTO 集中在 Save/Data。替换迁移用例为当前格式、必需字段及版本拒绝验证，删除历史等级保留测试。现有回滚 Restore 方法仍承担关卡事务职责。
