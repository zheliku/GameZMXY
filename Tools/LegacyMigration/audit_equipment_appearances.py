"""Audit the shared inventory and ground appearance table for every equipment row.

The workbook owns equipment identity and ownership.  The native resource owns
the two visual representations used by the inventory and ground pickup.  This
authoring audit joins them by the stable EquipmentId and fails before a new
level can ship an item with a missing, duplicated, or non-existent texture.
It does not modify the workbook, resource, or generated project files.
"""

import argparse
import json
import re
from pathlib import Path

from PIL import Image

from audit_official_level_dependencies import xlsx_rows


def _cells(row):
    """Drop the physical row suffix from an XLSX cell address."""
    return {re.sub(r"\d+$", "", key): value for key, value in row.items()}


def _equipment_ids(config_dir):
    """Read stable enum identities without maintaining a second item list."""
    result = {}
    active = None
    for row in xlsx_rows(config_dir / "__enums__.xlsx")["Sheet1"]:
        cells = _cells(row)
        active = cells.get("B") or active
        if active != "Equipment.EquipmentId" or not cells.get("H") or not cells.get("J"):
            continue
        for name in (cells["H"], cells.get("I")):
            if name:
                result[name] = int(cells["J"])
    return result


def _equipment_rows(config_dir):
    """Return one identity record for every EquipmentConfig row."""
    identities = _equipment_ids(config_dir)
    result = []
    for row in xlsx_rows(config_dir / "Items.xlsx")["EquipmentConfig"][3:]:
        cells = _cells(row)
        if (cells.get("A") or "").startswith("##"):
            continue
        if not cells.get("B") or not cells.get("C") or not cells.get("G"):
            continue
        try:
            equipment_id = identities[cells["B"]]
        except KeyError as exception:
            raise ValueError(f"EquipmentConfig references an unknown EquipmentId: {cells['B']}") from exception
        result.append({
            "id": equipment_id,
            "enum": cells["B"],
            "legacy_id": cells["C"],
            "name": cells.get("D"),
            "owner": cells.get("F"),
            "slot": cells.get("G"),
            "quality": cells.get("H"),
        })
    return result


def _appearance_entries(project):
    """Parse the serialized ItemAppearancesResource without loading Godot."""
    path = project / "TheGame/Sprites/Items/item_appearances.tres"
    text = path.read_text(encoding="utf-8")
    resources = {
        identifier: resource_path
        for resource_path, identifier in re.findall(
            r'\[ext_resource[^\n]*path="([^"]+)" id="([^"]+)"', text
        )
    }
    entries = {}
    for resource_id, body in re.findall(
        r'\[sub_resource type="Resource" id="([^"]+)"\](.*?)(?=\n\[|\Z)',
        text,
        re.S,
    ):
        equipment_id = re.search(r"_equipmentId = (\d+)", body)
        if equipment_id is None:
            continue
        textures = {
            field: resources[ext_id]
            for field, ext_id in re.findall(
                r'(_inventoryIcon|_groundIcon) = ExtResource\("([^"]+)"\)', body
            )
        }
        if int(equipment_id.group(1)) in entries:
            raise ValueError(f"Duplicate ItemAppearanceResource for EquipmentId {equipment_id.group(1)}")
        entries[int(equipment_id.group(1))] = {
            "resource_id": resource_id,
            "textures": textures,
        }
    return entries


def _texture_report(project, resource_path):
    """Check an asset exists, is a readable PNG, and has visible alpha."""
    if not resource_path:
        return {"path": None, "error": "missing resource path"}
    path = project / resource_path.removeprefix("res://")
    if not path.is_file():
        return {"path": resource_path, "error": "file does not exist"}
    try:
        with Image.open(path) as image:
            rgba = image.convert("RGBA")
            alpha = rgba.getchannel("A")
            alpha_box = alpha.getbbox()
            if alpha_box is None:
                return {
                    "path": resource_path,
                    "size": list(rgba.size),
                    "error": "texture has no visible alpha",
                }
            if alpha.getextrema()[0] == 255:
                return {
                    "path": resource_path,
                    "size": list(rgba.size),
                    "error": "texture has an opaque background; expected transparent equipment art",
                }
            return {
                "path": resource_path,
                "size": list(rgba.size),
                "alpha_bounds": list(alpha_box),
            }
    except OSError as exception:
        return {"path": resource_path, "error": f"cannot read texture: {exception}"}


def _hero_atlas_bindings(project):
    """Read the playable hero's independent body and weapon atlas bindings."""
    path = project / "TheGame/Sprites/Characters/Heroes/wukong/wukong_appearance.tres"
    text = path.read_text(encoding="utf-8")
    resources = {
        identifier: resource_path
        for resource_path, identifier in re.findall(
            r'\[ext_resource[^\n]*path="([^"]+)" id="([^"]+)"', text
        )
    }
    bindings = {}
    for kind, field, base_field in (
        ("body", "_armorTextures", "_baseBody"),
        ("weapon", "_weaponTextures", "_baseWeapon"),
    ):
        dictionary = re.search(field + r' = Dictionary\[int, Texture2D\]\(\{(.*?)\}\)', text, re.S)
        base = re.search(base_field + r' = ExtResource\("([^"]+)"\)', text)
        if dictionary is None or base is None:
            raise ValueError(f"Missing native Wukong {kind} atlas bindings")
        entries = {int(key): resources[resource_id] for key, resource_id in re.findall(
            r'(\d+): ExtResource\("([^"]+)"\)', dictionary[1]
        )}
        entries[0] = resources[base[1]]
        bindings[kind] = entries
    return bindings


def _animation_frame_cells(project):
    """Collect every body/weapon frame used by the live Wukong animations."""
    cells = {"body": set(), "weapon": set()}
    animation_paths = (
        project / "TheGame/Entitys/Animations/Heroes/Wukong/wukong_anim_library.tres",
        project / "TheGame/Entitys/Animations/Heroes/Wukong/wukong_skill_library.tres",
    )
    for path in animation_paths:
        text = path.read_text(encoding="utf-8")
        for block in re.findall(
            r'\[sub_resource type="Animation" id="[^"]+"\](.*?)(?=\n\[|\Z)', text, re.S
        ):
            for track, layer in re.findall(
                r'tracks/(\d+)/path = NodePath\("(m_Body|m_Weapon):frame"\)', block
            ):
                keys = re.search(
                    r'tracks/' + track + r'/keys = \{.*?"values": \[([^\]]*)\]', block, re.S
                )
                if keys:
                    cells["body" if layer == "m_Body" else "weapon"].update(
                        int(value) for value in re.findall(r"-?\d+", keys[1])
                    )
    return cells


def _atlas_motion_report(project, bindings):
    """Check every mapped atlas against the shared animation frame contract."""
    cells = _animation_frame_cells(project)
    errors = []
    records = []
    for kind, entries in bindings.items():
        loaded = {}
        for equipment_id, resource_path in entries.items():
            report = _texture_report(project, resource_path)
            if report.get("error"):
                errors.append(f"Wukong {kind} EquipmentId {equipment_id}: {report['error']}")
                continue
            width, height = report["size"]
            if width % 200 or height % 200 or width // 200 != 6:
                errors.append(
                    f"Wukong {kind} EquipmentId {equipment_id}: atlas must be six 200px columns, got {width}x{height}"
                )
                continue
            loaded[equipment_id] = Image.open(project / resource_path.removeprefix("res://")).convert("RGBA")

        base = loaded.get(0)
        if base is None:
            continue
        base_height = base.height // 200
        max_cell = 6 * base_height
        for equipment_id, image in loaded.items():
            if image.size != base.size:
                errors.append(
                    f"Wukong {kind} EquipmentId {equipment_id}: atlas size {image.size} "
                    f"differs from base atlas {base.size}"
                )
                continue
            for cell in sorted(cells[kind]):
                if cell < 0 or cell >= max_cell:
                    errors.append(f"Wukong {kind} EquipmentId {equipment_id}: animation references cell {cell} outside {max_cell}")
                    continue
                x, y = (cell % 6) * 200, (cell // 6) * 200
                actual_visible = image.crop((x, y, x + 200, y + 200)).getchannel("A").getbbox() is not None
                base_visible = base.crop((x, y, x + 200, y + 200)).getchannel("A").getbbox() is not None
                if actual_visible != base_visible:
                    errors.append(
                        f"Wukong {kind} EquipmentId {equipment_id}: cell {cell} alpha coverage differs from base atlas"
                    )
            image.close()
        base.close()
        records.append({"kind": kind, "equipment_ids": sorted(entries), "frame_count": len(cells[kind])})
    return records, errors


def _wardrobe_binding_errors(items, bindings):
    """Require every playable wearable, not just already registered textures."""
    expected = {"body": set(), "weapon": set()}
    errors = []
    for item in items:
        if item["owner"] != "悟空" or item["slot"] not in ("防具", "武器"):
            continue
        kind = "body" if item["slot"] == "防具" else "weapon"
        expected[kind].add(item["id"])
        if item["id"] not in bindings[kind]:
            errors.append(
                f"EquipmentId {item['id']} ({item['name']}) has no playable {kind} atlas binding"
            )
    for kind, entries in bindings.items():
        for equipment_id in sorted(set(entries) - expected[kind] - {0}):
            errors.append(f"Wukong {kind} binding has no matching wearable: EquipmentId {equipment_id}")
    return errors


def audit(args):
    repo = args.repo.resolve()
    project = repo / "Godot/GodotProject"
    config_dir = repo / "Configs/GameConfig/Datas"
    items = _equipment_rows(config_dir)
    appearances = _appearance_entries(project)
    atlas_bindings = _hero_atlas_bindings(project)
    atlas_motion, atlas_errors = _atlas_motion_report(project, atlas_bindings)
    item_ids = {item["id"] for item in items}
    errors = list(atlas_errors) + _wardrobe_binding_errors(items, atlas_bindings)
    records = []
    if len(item_ids) != len(items):
        errors.append("Items.xlsx contains duplicate EquipmentId rows")
    for item in items:
        appearance = appearances.get(item["id"])
        if appearance is None:
            errors.append(f"EquipmentId {item['id']} ({item['name']}) has no appearance entry")
            continue
        textures = {
            field: _texture_report(project, appearance["textures"].get(field))
            for field in ("_inventoryIcon", "_groundIcon")
        }
        for field, report in textures.items():
            if report.get("error"):
                errors.append(f"EquipmentId {item['id']} {field}: {report['error']}")
        records.append({**item, "appearance": textures})
    extra = sorted(set(appearances) - item_ids)
    errors.extend(f"appearance resource has unconfigured EquipmentId {equipment_id}" for equipment_id in extra)
    result = {
        "equipment_count": len(items),
        "appearance_count": len(appearances),
        "items": records,
        "unconfigured_appearance_ids": extra,
        "hero_atlas_motion": atlas_motion,
        "errors": errors,
    }
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if errors:
        for error in errors:
            print(f"EQUIPMENT APPEARANCE FAIL: {error}")
        raise SystemExit(1)
    print(
        f"EQUIPMENT APPEARANCE PASS: {len(items)} equipment identities, "
        f"{len(appearances)} unique appearance entries, inventory and ground textures present."
    )


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output", type=Path)
    audit(parser.parse_args())
