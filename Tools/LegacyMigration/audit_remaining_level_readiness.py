"""Build a readiness matrix for every remaining ZMXY1 level.

This is an authoring gate, not runtime code.  It joins the verified official
dependency inventory with the current source workbooks, scene directories,
registered entity scenes, music assets, and existing appearance audit.  It
never edits formal tables or Godot resources.  Use ``--strict`` only after a
level is expected to be shippable; the normal report intentionally lists open
work so an unfinished level is visible instead of silently passing.
"""

import argparse
import hashlib
import json
import math
import re
from pathlib import Path

from PIL import Image

from audit_official_level_dependencies import xlsx_rows
from audit_equipment_appearances import _equipment_rows, _appearance_entries, _texture_report


LEVELS = {
    "2-3": (6, "zm1_06_garuda_cave", "garuda_cave"),
    "3-1": (7, "zm1_07_shore", "shallow_shore"),
    "3-2": (8, "zm1_08_east_sea", "east_sea"),
    "3-3": (9, "zm1_09_shark_whirlpool", "shark_whirlpool"),
    "4-1": (10, "zm1_10_bull_demon_valley", "bull_demon_valley"),
    "9-1": (11, "zm1_11_hidden_dragon_palace", "hidden_dragon_palace"),
}

MUSIC_ASSETS = {
    "bg2": "TheGame/Audios/Music/cave_theme.mp3",
    "bg3": "TheGame/Audios/Music/sea_theme.mp3",
    "bg4": "TheGame/Audios/Music/bull_demon_valley_theme.mp3",
}

MUSIC_KEYS = {
    "bg2": "CaveTheme",
    "bg3": "SeaTheme",
    "bg4": "BullDemonValleyTheme",
}

PLAYABLE_WARDROBE_AUDITS = {
    "悟空": ("audit_official_wukong_wardrobe.py",
             "validate_wukong_wardrobe_motion.gd",
             "validate_official_wukong_attacks.gd",
             "--equipment-only"),
    "唐僧": ("audit_official_tangseng_wardrobe.py",
             "validate_tangseng_wardrobe_motion.gd",
             "--tangseng-equipment-only"),
}

CHECKLIST = (
    "official_source_and_dependency_inventory",
    "level_config_and_nested_recipes",
    "entity_enum_resource_scene_and_animation",
    "terrain_geometry_spawn_gates_traps_and_camera",
    "animation_attack_geometry_and_lifecycle",
    "music_sound_events_and_audio_assets",
    "drops_equipment_appearance_and_wardrobe",
    "playable_equipment_identity_and_motion",
    "settlement_checkpoint_retry_map_return_and_reload",
    "real_input_physics_render_audio_smoke_test",
)

PRODUCTION_REQUIREMENTS = (
    {
        "key": "official_source_and_dependency_inventory",
        "owner": "OfficialReference + validation inventory",
        "artifacts": "匹配版本 SWF/XML、SHA、关卡标签、敌人动作/帧脚本、动态对象和掉落来源",
        "evidence": "audit_official_level_dependencies.py 与本关 source inventory",
    },
    {
        "key": "level_config_and_nested_recipes",
        "owner": "LevelConfig.xlsx + Luban",
        "artifacts": "LevelId、场景路径、阶段、左右门、并行配方与具名关卡专属配置",
        "evidence": "源表、Luban 产物、LevelViewController 初始化校验",
    },
    {
        "key": "entity_enum_resource_scene_and_animation",
        "owner": "EntityId/Resources + Entitys + GameScripts",
        "artifacts": "Monster/Boss 继承链、实体场景、动作库、身体/AI 状态和特效场景",
        "evidence": "实体注册、场景资源引用、动画库装配和构建",
    },
    {
        "key": "terrain_geometry_spawn_gates_traps_and_camera",
        "owner": "Level scene",
        "artifacts": "背景/前景、地形、出生点、阶段门、陷阱、相机限位和出口",
        "evidence": "场景节点审计与真实走跑/跳跃/陷阱烟测",
    },
    {
        "key": "animation_attack_geometry_and_lifecycle",
        "owner": "AnimationLibrary + concrete states",
        "artifacts": "逐招帧时点、判定形状、攻击刷新、运动、受击/死亡/形态生命周期",
        "evidence": "源帧探针、动画轨道检查和逐帧战斗烟测",
    },
    {
        "key": "music_sound_events_and_audio_assets",
        "owner": "Resources.SoundConfig + GGF Sound",
        "artifacts": "章节音乐、招式/命中/受击/死亡/UI 声音和停止/暂停绑定",
        "evidence": "SoundConfig 路径、帧事件映射、真实代理播放日志",
    },
    {
        "key": "drops_equipment_appearance_and_wardrobe",
        "owner": "Items/EquipmentDropPool + appearance resources",
        "artifacts": "掉落池、装备数值、背包图、地面图、透明区域和可穿戴身份",
        "evidence": "equipment appearance audit 与本关实际拾取烟测",
    },
    {
        "key": "playable_equipment_identity_and_motion",
        "owner": "HeroAppearanceResource + official wardrobe audits",
        "artifacts": "本关实际掉落的可玩角色装备、独立官方selector、完整基础/攻击/技能动作与双向穿卸",
        "evidence": "本角色官方身份、原生播放器及--equipment-only真实背包/地面专项；仅有图标或暂存图集不算完成",
    },
    {
        "key": "settlement_checkpoint_retry_map_return_and_reload",
        "owner": "LevelSystem + ProcedureLevel + SaveSystem",
        "artifacts": "清波/守关目标、成功/失败、检查点、重试、地图返回和读档",
        "evidence": "隔离存档真实流程回归",
    },
    {
        "key": "real_input_physics_render_audio_smoke_test",
        "owner": "OpenGL validation",
        "artifacts": "键鼠输入、物理、渲染、音频、暂停、退出码和错误日志",
        "evidence": "本关独立 validation receipt，不能以泛化 PASS 代替",
    },
)


def _cells(row):
    return {re.sub(r"\d+$", "", key): value for key, value in row.items()}


def _configured_recipes(config_dir, level_id):
    """Read nested source rows without inheriting blank recipe values."""
    recipes = {}
    owner = stage = None
    stage_delay = 0
    rows = xlsx_rows(config_dir / 'LevelConfig.xlsx')['LevelConfig']
    for row in map(_cells, rows[4:]):
        if row.get('B'):
            owner = int(row['B'])
        if row.get('H'):
            stage = int(row['H'])
            stage_delay = float(row.get('O') or 0)
        if owner != level_id or not row.get('P'):
            continue
        point = row['Q']
        if point in recipes:
            raise ValueError(f'Duplicate spawn recipe: {level_id}/{point}')
        recipes[point] = {'stage': stage, 'entity': row['P'], 'count': int(row['R']),
                          'first_spawn_seconds': stage_delay + float(row.get('S') or 0),
                          'interval_seconds': float(row['T'])}
    return recipes


def _recipe_errors(expected, actual, enemy_names):
    """Reject missing streams, wrong enemies/counts, or shifted first/later timing."""
    if not expected:
        return ['Official inventory has no per-stream recipes; regenerate it from verified sources']
    errors = []
    points = {recipe['spawn_point_id'] for recipe in expected}
    if points != set(actual):
        errors.append(f'Spawn points differ: missing={sorted(points - set(actual))}, extra={sorted(set(actual) - points)}')
    for recipe in expected:
        point = recipe['spawn_point_id']
        if point not in actual:
            continue
        fields = {key: recipe[key] for key in ('stage', 'count', 'first_spawn_seconds', 'interval_seconds')}
        fields['entity'] = enemy_names.get(recipe['enemy_legacy_id'])
        for field, value in fields.items():
            configured = actual[point][field]
            matches = (math.isclose(configured, value, abs_tol=1e-5)
                       if isinstance(value, float) else configured == value)
            if not matches:
                errors.append(f'{point}/{field}: configured {configured}, official {value}')
    return errors


def _enum_map(config_dir):
    result = {}
    active = None
    for row in xlsx_rows(config_dir / "__enums__.xlsx")["Sheet1"]:
        cells = _cells(row)
        active = cells.get("B") or active
        if active != "Entity.EntityId" or not cells.get("H") or not cells.get("J"):
            continue
        # Formal source tables use the Chinese alias; C# keeps the stable H name.
        result[int(cells["J"])] = cells.get("I") or cells["H"]
    return result


def _registered_entities(config_dir):
    rows = xlsx_rows(config_dir / "Resources.xlsx")["EntityConfig"]
    return {
        cells["C"]: cells.get("D")
        for cells in (_cells(row) for row in rows[5:])
        if cells.get("C") and cells.get("D")
    }


def _registered_levels(config_dir):
    rows = xlsx_rows(config_dir / "LevelConfig.xlsx")["LevelConfig"]
    return {
        int(cells["B"]): cells.get("F")
        for cells in (_cells(row) for row in rows[4:])
        if cells.get("B") and str(cells["B"]).isdigit()
    }


def _sound_keys(config_dir):
    rows = xlsx_rows(config_dir / "Resources.xlsx")["SoundConfig"]
    return {
        cells["C"]: cells.get("H")
        for cells in (_cells(row) for row in rows[5:])
        if cells.get("C") and cells.get("B")
    }


def _sha256(path):
    """Hash current bytes, including sources used to reject stale validation."""
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _project_fingerprint(repo):
    """Invalidate evidence after any formal table, script, scene or asset changes."""
    digest = hashlib.sha256()
    for directory in ("Configs/GameConfig", "Godot/GodotProject/TheGame",
                      "Godot/GodotProject/EditorScripts", "Tools/LegacyMigration"):
        for path in sorted((repo / directory).rglob("*")):
            if path.is_file() and "__pycache__" not in path.parts and path.suffix != ".pyc":
                digest.update(path.relative_to(repo).as_posix().encode("utf-8") + b"\0")
                digest.update(bytes.fromhex(_sha256(path)))
    return digest.hexdigest()


def _source_audit(repo, inventory):
    """Do not mark a cached inventory as verified without checking its sources."""
    errors = []
    for source in inventory["sources"]:
        path = repo / source["swf"]
        if not path.is_file() or _sha256(path) != source["sha256"]:
            errors.append(f"Official source missing or changed: {source['name']}")
    return errors


def _required_actors(level, actors):
    """Include script-created enemies and transformation chains, not only spawns."""
    required = set(map(int, level["enemy_counts"]))
    pending = list(required)
    while pending:
        actor_id = pending.pop()
        for child in actors[actor_id]["spawned_enemies"]:
            if child not in required:
                required.add(child)
                pending.append(child)
    return sorted(required)


def _drop_audit(required_ids, actors, equipment, appearances, project):
    """Check this level's actual official drops against current formal resources."""
    required = sorted({legacy for actor_id in required_ids
                       for legacy in actors[actor_id]["equipment_drops"]})
    records = {item["legacy_id"]: item for item in equipment}
    missing = sorted(set(required) - records.keys())
    errors = [f"Official drop not configured: {legacy}" for legacy in missing]
    for legacy in required:
        if legacy not in records:
            continue
        item = records[legacy]
        appearance = appearances.get(item["id"])
        if appearance is None:
            errors.append(f"Drop {legacy} has no appearance entry")
            continue
        for field in ("m_InventoryIcon", "m_GroundIcon"):
            texture = _texture_report(project, appearance["textures"].get(field))
            if texture.get("error"):
                errors.append(f"Drop {legacy}/{field}: {texture['error']}")
    return {"required_official_drops": required, "missing_equipment": missing, "errors": errors}


def _playable_wardrobe_audit(required_ids, actors, equipment):
    """Find dropped wearables that need identity-specific motion validation."""
    required = sorted({legacy for actor_id in required_ids
                       for legacy in actors[actor_id]["equipment_drops"]})
    records = {item["legacy_id"]: item for item in equipment}
    wearables = []
    errors = []
    for legacy in required:
        item = records.get(legacy)
        if item is None or item.get("slot") not in ("武器", "防具"):
            continue
        owner = item.get("owner")
        if owner in PLAYABLE_WARDROBE_AUDITS:
            wearables.append({"legacy_id": legacy, "equipment_id": item["id"],
                              "name": item.get("name"), "owner": owner,
                              "slot": item["slot"]})
        else:
            errors.append(f"No playable wardrobe audit is registered for equipment owner {owner!r}: {legacy}")
    return {"required_equipment": wearables,
            "required_owners": sorted({item["owner"] for item in wearables}),
            "errors": errors}


def _validation_evidence(work, label, project_sha, inventory_sha, wardrobe_owners=()):
    """Require level-specific, current, successful command output for each gate.

    The evidence file is a run receipt, never a list of manually checked booleans.
    Its project fingerprint must match after authoring and generation finish.
    Logs remain reviewable; a receipt cannot prove a test's coverage by itself.
    """
    path = work / "validation" / label / "evidence.json"
    if not path.is_file():
        return {}, ["No level-specific validation receipt"]
    data = json.loads(path.read_text(encoding="utf-8"))
    errors = []
    if (data.get("official_label") != label or data.get("project_sha256") != project_sha
            or data.get("inventory_sha256") != inventory_sha):
        return {}, ["Validation receipt belongs to another level or stale inputs"]
    accepted = {}
    for gate, receipt in data.get("checks", {}).items():
        log = path.parent / receipt.get("log", "")
        marker = receipt.get("pass_marker", "")
        if (gate not in CHECKLIST or not receipt.get("command") or receipt.get("exit_code") != 0
                or not marker or label not in marker or "PASS" not in marker
                or not log.is_file() or _sha256(log) != receipt.get("log_sha256")
                or marker not in log.read_text(encoding="utf-8", errors="replace")):
            errors.append(f"Invalid command evidence: {gate}")
        elif gate == "playable_equipment_identity_and_motion":
            command = " ".join(map(str, receipt["command"])).casefold()
            required_commands = {tool for owner in wardrobe_owners
                                 for tool in PLAYABLE_WARDROBE_AUDITS[owner]}
            missing_commands = sorted(tool for tool in required_commands if tool.casefold() not in command)
            if missing_commands:
                errors.append("Wardrobe evidence omits required identity or motion audit: "
                              + ", ".join(missing_commands))
            else:
                accepted[gate] = {"command": receipt["command"], "log": str(log), "pass_marker": marker}
        else:
            accepted[gate] = {"command": receipt["command"], "log": str(log), "pass_marker": marker}
    return accepted, errors


def _staged_layers(work):
    layout = work / "art/layout.json"
    if not layout.is_file():
        return {"present": False, "levels": [], "music": [], "errors": []}
    data = json.loads(layout.read_text(encoding="utf-8"))
    ready, errors = [], []
    for level in data.get("levels", []):
        paths = [layout.parent / "layers" / level["front_file"]]
        paths.extend(layout.parent / "layers" / part["file"] for part in level["terrain"])
        for character_id in level["floor_characters"]:
            images = list((layout.parent / "images").glob(f"{character_id}_*"))
            if len(images) != 1:
                errors.append(f"{level['label']}: missing or ambiguous sky image {character_id}")
            paths.extend(images)
        invalid = False
        for path in paths:
            try:
                with Image.open(path) as image:
                    image.verify()
            except (OSError, ValueError) as exception:
                errors.append(f"{level['label']}: unreadable layer {path}: {exception}")
                invalid = True
        if not invalid and not any(error.startswith(level['label'] + ':') for error in errors):
            ready.append(level["label"])
    return {"present": True, "levels": ready, "errors": errors,
            "music": [item.get("symbol") for item in data.get("music", [])]}


def _level_status(level, repo, config_dir, enums, entities, levels, sounds,
                  staged_layers, inventory, equipment, appearances, source_errors,
                  work, project_sha, inventory_sha):
    label = level["label"]
    level_id, directory, _ = LEVELS[label]
    actors = {actor["id"]: actor for actor in inventory["actors"]}
    required_ids = _required_actors(level, actors)
    required_dynamic_objects = sorted({
        dynamic
        for actor_id in required_ids
        for dynamic in actors[actor_id]["dynamic_objects"]
    })
    required_source_sounds = sorted({
        sound
        for actor_id in required_ids
        for sound in actors[actor_id]["script_sounds"] + actors[actor_id]["embedded_sounds"]
    })
    required_action_frames = {
        str(actor_id): {
            action["label"]: action["frames"]
            for action in actors[actor_id]["actions"]
        }
        for actor_id in required_ids
    }
    # Legacy monster numbers are not a contract for the project's EntityId values.
    enemy_names = {}
    for sheet in xlsx_rows(config_dir / "Enemy.xlsx").values():
        for cells in map(_cells, sheet[3:]):
            if cells.get("C") and str(cells["C"]).isdigit() and cells.get("F"):
                enemy_names[int(cells["C"])] = cells["F"]
    required_entities = {str(actor_id): enemy_names.get(actor_id, f"Monster{actor_id}")
                         for actor_id in required_ids}
    recipe_errors = _recipe_errors(level.get('spawn_recipes'),
                                  _configured_recipes(config_dir, level_id), enemy_names)
    missing_entities = [name for name in required_entities.values() if name not in entities]
    missing_scenes = [entities[name] for name in required_entities.values() if name in entities
                      and not (repo / "Godot/GodotProject" / entities[name].removeprefix("res://")).is_file()]
    missing_enums = [name for name in required_entities.values() if name not in enums.values()]
    scene_path = repo / "Godot/GodotProject/TheGame/Scenes/Levels" / directory
    scene_file = scene_path / f"level_{level_id}.tscn"
    sprite_path = repo / "Godot/GodotProject/TheGame/Sprites/Levels" / directory
    music_symbol = level["music_symbol"]
    music_relative = MUSIC_ASSETS.get(music_symbol)
    music_path = repo / "Godot/GodotProject" / music_relative if music_relative else None
    layer_ready = label in staged_layers["levels"]
    drops = _drop_audit(required_ids, actors, equipment, appearances, repo / "Godot/GodotProject")
    wardrobe = _playable_wardrobe_audit(required_ids, actors, equipment)
    evidence, evidence_errors = _validation_evidence(
        work, label, project_sha, inventory_sha, wardrobe["required_owners"])
    prerequisites = {
        "official_source_and_dependency_inventory": not source_errors,
        "level_config_and_nested_recipes": levels.get(level_id) ==
            f"res://TheGame/Scenes/Levels/{directory}/level_{level_id}.tscn" and scene_file.is_file() and not recipe_errors,
        "entity_enum_resource_scene_and_animation": not missing_entities and not missing_scenes and not missing_enums,
        "terrain_geometry_spawn_gates_traps_and_camera": scene_file.is_file() and layer_ready,
        "animation_attack_geometry_and_lifecycle": not missing_entities and not missing_scenes,
        "music_sound_events_and_audio_assets": sounds.get(MUSIC_KEYS.get(music_symbol)) ==
            f"res://{music_relative}" and music_path is not None and music_path.is_file(),
        "drops_equipment_appearance_and_wardrobe": not drops["errors"],
        "playable_equipment_identity_and_motion": not wardrobe["errors"],
        "settlement_checkpoint_retry_map_return_and_reload": level_id in levels and scene_file.is_file(),
        "real_input_physics_render_audio_smoke_test": level_id in levels and scene_file.is_file(),
    }
    checks = {gate: ready and (gate == "official_source_and_dependency_inventory"
                               or (gate == "playable_equipment_identity_and_motion"
                                   and not wardrobe["required_equipment"])
                               or gate in evidence)
              for gate, ready in prerequisites.items()}
    return {
        "official_label": label,
        "level_id": level_id,
        "scene_directory": directory,
        "stage_count": len(level["stages"]),
        "spawn_stream_count": level["spawn_streams"],
        "recipe_count": sum(stage["recipes"] for stage in level["stages"]),
        "recipe_errors": recipe_errors,
        "geometry_count": level["geometry"],
        "enemy_counts": level["enemy_counts"],
        "required_entities": required_entities,
        "missing_entity_registrations": missing_entities,
        "missing_entity_scenes": missing_scenes,
        "missing_entity_enums": missing_enums,
        "required_actor_ids_including_dynamic": required_ids,
        "required_dynamic_objects": required_dynamic_objects,
        "required_source_sounds": required_source_sounds,
        "required_action_frames": required_action_frames,
        "drops": drops,
        "playable_wardrobe": wardrobe,
        "music_symbol": music_symbol,
        "music_asset": music_relative,
        "scene_directory_present": scene_path.is_dir(),
        "level_sprite_directory_present": sprite_path.is_dir(),
        "official_layers_staged": layer_ready,
        "prerequisites_present": prerequisites,
        "validation_evidence": evidence,
        "evidence_errors": evidence_errors,
        "checks": checks,
        "open_checklist": [name for name in CHECKLIST if not checks[name]],
    }


def audit(args):
    repo = args.repo.resolve()
    project = repo / "Godot/GodotProject"
    config_dir = repo / "Configs/GameConfig/Datas"
    inventory = json.loads(args.inventory.resolve().read_text(encoding="utf-8"))
    enums = _enum_map(config_dir)
    entities = _registered_entities(config_dir)
    levels = _registered_levels(config_dir)
    sounds = _sound_keys(config_dir)
    staged_layers = _staged_layers(args.work.resolve())
    equipment = _equipment_rows(config_dir)
    appearances = _appearance_entries(project)
    source_errors = _source_audit(repo, inventory)
    project_sha = _project_fingerprint(repo)
    inventory_sha = _sha256(args.inventory.resolve())
    reports = [_level_status(level, repo, config_dir, enums, entities, levels, sounds,
                             staged_layers, inventory, equipment, appearances, source_errors,
                             args.work.resolve(), project_sha, inventory_sha)
               for level in inventory["levels"] if level["label"] in LEVELS
               and (not args.level or level["label"] in args.level)]
    result = {
        "source_inventory": str(args.inventory.resolve()),
        "remaining_level_count": len(reports),
        "checklist": list(CHECKLIST),
        "production_requirements": list(PRODUCTION_REQUIREMENTS),
        "project_sha256": project_sha,
        "inventory_sha256": inventory_sha,
        "source_errors": source_errors,
        "staged_layers": staged_layers,
        "levels": reports,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    incomplete = [item for item in reports if item["open_checklist"]]
    for item in reports:
        print(f"{item['official_label']}: {len(item['open_checklist'])} open gates; "
              f"missing entities={item['missing_entity_registrations']}")
    print(f"REMAINING LEVEL READINESS: {len(reports) - len(incomplete)}/{len(reports)} complete; {args.output}")
    if args.strict and incomplete:
        raise SystemExit(1)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--work", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--strict", action="store_true")
    parser.add_argument("--level", action="append", choices=LEVELS,
                        help="Audit selected official labels; repeat for multiple levels.")
    audit(parser.parse_args())
