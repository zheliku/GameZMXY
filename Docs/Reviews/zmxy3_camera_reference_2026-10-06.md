# 造梦西游3原版相机数值核查（2026-10-06）

通过 4399 官方入口取得公开游戏资源，只读解析画布和镜头阈值；不在项目中移植原版脚本或资源。原版采用非对称滚动窗口，本项目按用户后续要求采用中央小死区。

## 来源与可重复识别

- [4399 造梦西游专题第三代入口](https://www.4399.com/flash/zmhj.htm?g=3)：页面 `game_url_arr[3]` 指向第三代游戏，`flash23` 嵌入区域宽 940、高 590。
- [官方第三代加载页](https://sda.4399.com/4399swf/upload_swf/ftp7/hanbao/20120107/6/v260714.htm)：通过专题的正常入口访问；`left_box` 为 940×590，加载相邻的 `v260714.swf`。
- [官方公开 SWF](https://sda.4399.com/4399swf/upload_swf/ftp7/hanbao/20120107/6/v260714.swf)：本轮取得的文件为 3,158,482 字节，SHA-256 为 `a8704316853d44f59e2e55e447f9396721dbc50d53f7dc7641f6c3358beacbc7`。该 URL 可能继续更新，比较结果以此哈希对应的资源为准。
- SWF 为 `CWS` 压缩格式，RECT 头给出 940×590。内部 DefineBinaryData 包含游戏 SWF；其 `ViewControllor` 类的 `step` 方法提供滚动阈值，构造方法以 940 计算地图/背景右侧滚动上限。
- 读取的 `ViewControllor` ABC 数据 SHA-256 为 `d06dfeee395baa09bc51f4df12d8857e1e4436b2e4bd6464643033fc6da6c7af`。研究缓存与只读检查脚本位于 `.godot/validation/source_refs/`，不进入提交或运行时资源包。

## 实际数值与解释边界

| 项目 | 原版证据 | 本项目决定 |
| --- | --- | --- |
| 画布 | SWF RECT、官方加载页均为 940×590 | 保持 940×590 构图，窗口使用 `keep` |
| 普通前进滚动阈值 | `step` 的角色 X 比较使用 `626.6666666666666`，即屏宽的 2/3 | 跟随窗口放在中央附近，符合用户最新的抵达时人物居中要求 |
| 普通后退滚动阈值 | 对应反向分支使用 `188`，即屏宽的 1/5 | 中央死区半宽 40px，屏幕区间 [430, 510]；两侧对称 |
| 取景边界 | 地图/背景有独立滚动上限，存在停止点与允许回退状态 | 相机视野约束与角色物理门分开管理 |

阈值比较中的 X 来自角色碰撞范围在场景父坐标系中的矩形左缘，不能直接当作角色图像中心。单人时两份角色范围引用同一角色，因此普通前进/后退分支分别在上述位置起作用；双人、特殊关卡、自动镜头与回退状态另有分支，本轮没有把这些阈值泛化到所有关卡。

原版窗口是非对称的，普通区间约为 [188, 626.67]；本项目中央窗口为 [430, 510]，总宽 80px（约 8.51% 屏宽）。40px 是结合用户最新要求选择的项目取景参数，不能称为“原版 40px”或“原版 6%”。保留原版的关键行为：角色推动卷屏、相机在区域边界停止、角色继续走到物理墙前。

## 外部设计依据

- [Itay Keren《Scroll Back》](https://www.gamedeveloper.com/design/scroll-back-the-theory-and-practice-of-cameras-in-side-scrollers)：分析 camera-window、position-locking 与 edge-snapping；以 Streets of Rage 说明清版动作游戏常用窗口跟随，也说明平滑移动对避免突变的作用。
- [Cinemachine Position Composer 3.1](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachinePositionComposer.html)：Dead Zone 围绕 Screen Position，目标处于区间内时相机不调整。其 Hard Limits 是目标在屏幕上的构图限制，不能与关卡物理墙或相机世界区域混为一谈。
- [Godot Camera2D](https://docs.godotengine.org/en/stable/classes/class_camera2d.html)：`global_position` 可能因平滑或限位而不同于实际画面位置；`get_screen_center_position()` 返回实际中心。`limit_smoothed` 在未启用位置平滑时无效。本项目由窗口算法统一管理实际中心与帧位移，不依赖两套平滑同时工作。

以上页面已实际取得并阅读，缓存保留本轮证据；采用窗口分类和空间约束设计，未引入 Cinemachine 或第三方相机运行时。
