# Entitys/ 目录规范（实体场景）

> 裁决顺序见根规范 §2。实体表 `AssetPath` 指向这里，`GF.Entity.ShowEntity` 按表加载。

- 场景名 = Logic 类名（`WukongEntity.tscn` ↔ `WukongEntity.cs`）；根节点挂 Logic 脚本（实体脚本手写，见 `GameScripts/Entity/AGENTS.md`）。
- 根节点 `[Export]` 节点引用必须声明 `node_paths=PackedStringArray("m_...")`，否则赋值被静默忽略；子节点用 `m_` 前缀（`m_Body` / `m_Weapon` / `m_EffectRoot` / `m_AnimPlayer` / `m_AnimTree` / `m_HurtBox` / `m_HitBox`），引擎默认名（`CollisionShape2D`）除外。`m_Weapon` / `m_EffectRoot` 只有英雄有。
- `m_HurtBox` 的形状按素材身形摆：伤害飘字从受击盒顶边中点冒出（`ActorEntity.HeadPosition`），不再逐角色写偏移常数。
- 判定区 layer/mask 按物理层语义设置（层表见 `GameScripts/Entity/AGENTS.md`），不写魔法数字。
- 角色动画资源放 `Animations/` 子目录：`<角色>_anim_library.tres`（AnimationLibrary：帧/特效/判定盒/方法轨道）+ `<角色>_animation_tree.tres`（AnimationNodeStateMachine）。它们是实体行为资源，不放 `Sprites/`（`Sprites/` 只放贴图与 SpriteFrames）。以编辑器保存的版本为准，生成器只校验不覆盖（见 `EditorScripts/AGENTS.md`）。
- 三件套同步：场景 + Logic 类 + 实体表行一起改；新增实体先加表拿 `EntityId` / `AssetPath`。移动/重命名只在编辑器内做，`.uid` 提交 git。
