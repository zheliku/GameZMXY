在 Godot 4 中，人物控制输入我比较推荐采用：

输入采集放在 `_process()` 或 `_input()`，人物移动和物理逻辑放在 `_physics_process()`。

也就是说，最好不要简单地把所有人物控制代码都塞进其中一个函数，而是根据职责分开。

## 1. 两种帧有什么区别？

|
对比

|

`_process(delta)`

|

`_physics_process(delta)`

|
| --- | --- | --- |
|

执行频率

|

跟随渲染帧率

|

固定物理频率

|
|

默认频率

|

不固定

|

通常 60 次/秒

|
|

适合用途

|

UI、视觉更新

|

物理计算、角色移动

|
|

帧率影响

|

受 FPS 影响

|

不受渲染 FPS 直接影响

|

例如：

* 游戏运行在 144 FPS，`_process()` 每秒执行约 144 次。

* 游戏运行在 30 FPS，`_process()` 每秒执行约 30 次。

* `_physics_process()` 默认每秒执行 60 次。

所以，涉及 `CharacterBody3D` 的移动、碰撞和 `move_and_slide()`，应该放在 `_physics_process()`。

## 2. 我推荐的输入处理方式

以第三人称角色为例：

gdscript

```
extends CharacterBody3D

var move_input := Vector2.ZERO
var jump_requested := false

func _process(delta: float) -> void:
    # 采集持续性输入
    move_input = Input.get_vector(
        "move_left",
        "move_right",
        "move_forward",
        "move_back"
    )

    # 采集一次性输入
    if Input.is_action_just_pressed("jump"):
        jump_requested = true


func _physics_process(delta: float) -> void:
    # 处理移动
    handle_movement(delta)

    # 处理跳跃
    if jump_requested:
        jump_requested = false
        handle_jump()

    # 执行物理移动
    move_and_slide()
```

这里有一个细节：`jump_requested` 是一个输入缓冲标记。

为什么要这样做？

假设渲染帧率只有 30 FPS，而物理帧率是 60 FPS。

如果直接在 `_physics_process()` 中使用：

gdscript

```
if Input.is_action_just_pressed("jump"):
    jump()
```

一次性输入事件可能因为物理帧和输入事件更新时机不同而被漏掉。

把输入请求保存起来，可以让物理逻辑在下一次物理帧消费它。

## 3. 还有一种更推荐的方式：使用 `_input()`

对于动作游戏，我个人更喜欢把输入采集和输入处理进一步分开。

例如：

gdscript

```
extends CharacterBody3D

var move_input := Vector2.ZERO
var jump_requested := false
var attack_requested := false


func _input(event: InputEvent) -> void:
    if event.is_action_pressed("jump"):
        jump_requested = true

    if event.is_action_pressed("attack"):
        attack_requested = true


func _process(delta: float) -> void:
    move_input = Input.get_vector(
        "move_left",
        "move_right",
        "move_forward",
        "move_back"
    )


func _physics_process(delta: float) -> void:
    handle_movement(delta)

    if jump_requested:
        jump_requested = false
        handle_jump()

    if attack_requested:
        attack_requested = false
        handle_attack()

    move_and_slide()
```

这里的分工是：

* `_input()`：响应输入事件，捕获跳跃、攻击等一次性操作。

* `_process()`：读取持续性输入，例如 WASD、摇杆。

* `_physics_process()`：执行实际游戏逻辑。

注意，`_input()` 是事件回调，不是固定每帧调用的函数。

另外，如果使用 `_input()`，还要考虑 UI 是否消费了输入事件，以及输入是否需要在游戏暂停时生效。

## 4. 对于前面讨论的 FSM，应该怎么结合？

结合我们之前设计的 Gameplay FSM + AnimationTree，我推荐这样的结构：

Input Layer

_input() / _process()

采集移动、攻击、跳跃、闪避请求

Gameplay FSM

_physics_process()

判断能否移动、攻击、跳跃、闪避

Movement

速度、重力、碰撞

Animation

AnimationTree

CharacterBody3D

move_and_slide()

这里有个小细节：AnimationTree 不一定非要在物理帧更新，普通动画表现可以跟随渲染帧；但如果涉及动画事件与攻击判定的精确同步，就需要仔细设计它们之间的时间和物理更新关系。

## 5. 我的实际建议

如果你正在开发 Godot 4 的 3D 动作游戏，可以记住下面这张表：

|
功能

|

推荐位置

|
| --- | --- |
|

键盘、鼠标事件采集

|

`_input()`

|
|

持续移动输入

|

`_process()` 或 `_physics_process()`

|
|

跳跃、攻击输入缓冲

|

`_input()`

|
|

FSM 状态更新

|

`_physics_process()`

|
|

CharacterBody3D 移动

|

`_physics_process()`

|
|

碰撞检测

|

`_physics_process()`

|
|

AnimationTree 参数更新

|

通常 `_process()`

|
|

UI 更新

|

`_process()`

|

特别提醒： 如果你的游戏需要严格的物理同步、网络同步或者回放系统，可以考虑将输入统一采样并缓存，然后在物理帧消费。这样整个游戏逻辑会更加容易管理。

我的核心建议是：

输入采集与游戏逻辑执行分离。输入负责产生 Request，物理帧负责消费 Request 并执行 Gameplay FSM。

这套设计和我们前面讨论的玩家 FSM + AnimationTree 架构是非常契合的。
