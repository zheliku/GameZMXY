"""Upgrade single-hero v12 saves and their backups to v14 outside the game runtime."""

import argparse
from datetime import datetime
import json
import os
from pathlib import Path
import shutil
import uuid


WORN_FIELDS = (
    "WeaponInstanceId", "ArmorInstanceId", "AccessoryInstanceId", "TreasureInstanceId"
)


def upgrade_save(data: dict, capacity: int) -> dict:
    """Validate v12 ownership and preserve all persistent values in v14 key order."""
    if len(data["Heroes"]) != 1:
        raise ValueError("Heroes must contain exactly one hero")
    hero = data["Heroes"][0]
    if data["ActiveHeroId"] != hero["HeroId"]:
        raise ValueError("ActiveHeroId does not match the only hero")

    instance_ids = [uuid.UUID(item["InstanceId"]) for item in data["EquipmentItems"]]
    if len(set(instance_ids)) != len(instance_ids):
        raise ValueError("EquipmentItems contains duplicate InstanceId values")
    worn_ids = {uuid.UUID(hero[field]) for field in WORN_FIELDS} - {uuid.UUID(int=0)}
    if not worn_ids.issubset(instance_ids):
        raise ValueError("A worn instance is missing from EquipmentItems")
    bag_ids = [item["InstanceId"] for item, instance_id in
               zip(data["EquipmentItems"], instance_ids) if instance_id not in worn_ids]
    if len(bag_ids) > capacity:
        raise ValueError(f"Bag has {len(bag_ids)} items, capacity is {capacity}")

    upgraded_hero = {
        "HeroId": hero["HeroId"],
        "TotalExperience": hero["TotalExperience"],
        "Soul": data["Soul"],
        "BagInstanceIds": bag_ids,
        "LearnedSkills": hero["LearnedSkills"],
        "PassiveLevels": hero["PassiveLevels"],
        **{field: hero[field] for field in WORN_FIELDS},
    }
    return {
        "SaveVersion": 14,
        "Heroes": [upgraded_hero],
        "HighestClearedLevelId": data["HighestClearedLevelId"],
        "EastSeaBannerAvailable": data["EastSeaBannerAvailable"],
        "EquipmentItems": data["EquipmentItems"],
        "UnitId": data["UnitId"],
    }


def describe_save(data: dict) -> str:
    """Summarize ownership without dumping equipment or other save contents."""
    heroes = data.get("Heroes", [])
    hero = heroes[0] if isinstance(heroes, list) and len(heroes) == 1 and isinstance(heroes[0], dict) else {}
    worn = sum(hero.get(field) not in (None, "00000000-0000-0000-0000-000000000000") for field in WORN_FIELDS)
    items = data.get("EquipmentItems")
    bag_ids = hero.get("BagInstanceIds")
    bag = len(bag_ids) if isinstance(bag_ids, list) else len(items) - worn if isinstance(items, list) else "?"
    return (f"version={data.get('SaveVersion', '?')} hero={hero.get('HeroId', '?')} "
            f"soul={data.get('Soul', hero.get('Soul', '?'))} bag={bag} worn={worn}")


def main() -> int:
    """Plan the entire batch before backing up and replacing any save file."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-data", type=Path,
                        default=Path(os.environ.get("APPDATA", "")) / "Godot/app_userdata/GameZMXY/GameData")
    parser.add_argument("--capacity", required=True, type=int)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    if args.capacity < 0:
        parser.error("--capacity must be non-negative")

    game_data = args.game_data.resolve()
    data_dir = game_data / "Data"
    if not data_dir.is_dir():
        print(f"ERROR {data_dir} version=? hero=? soul=? bag=? worn=? Data directory not found")
        print("SUMMARY OK=0 SKIP=0 ERROR=1 written=0")
        print("BACKUP none")
        return 1

    planned = []
    reports = []
    skipped = errors = 0
    paths = sorted([*data_dir.glob("*.sav"), *data_dir.glob("*.sav.bak")])
    for path in paths:
        details = "version=? hero=? soul=? bag=? worn=?"
        try:
            with path.open(encoding="utf-8-sig") as stream:
                data = json.load(stream)
            if not isinstance(data, dict):
                raise ValueError("Save root must be an object")
            details = describe_save(data)
            version = data["SaveVersion"]
            if version in (13, 14):
                reports.append(f"SKIP {path.name} {describe_save(data)} version is not v12")
                skipped += 1
                continue
            if version != 12:
                raise ValueError("Only v12 can be upgraded; v13/v14 are skipped")

            upgraded = upgrade_save(data, args.capacity)
            details = describe_save(data)
            has_bom = path.read_bytes().startswith(b"\xef\xbb\xbf")
            encoded = json.dumps(upgraded, ensure_ascii=False, indent=2).replace("\n", "\r\n").encode("utf-8")
            planned.append((path, (b"\xef\xbb\xbf" if has_bom else b"") + encoded))
            reports.append(f"OK {path.name} {details} -> v14 BOM={'yes' if has_bom else 'no'}")
        except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
            reports.append(f"ERROR {path.name} {details} {error}")
            errors += 1

    for report in reports:
        print(report)
    if errors:
        print(f"SUMMARY OK={len(planned)} SKIP={skipped} ERROR={errors} written=0")
        print("BACKUP none (validation failed; no files changed)")
        return 1
    if args.dry_run:
        print(f"SUMMARY OK={len(planned)} SKIP={skipped} ERROR=0 written=0 DRY-RUN")
        print("BACKUP none (dry-run; no files changed)")
        return 0

    backup = None
    try:
        if planned:
            backup = game_data.with_name(game_data.name + ".backup-before-v14-" + datetime.now().strftime("%Y%m%d_%H%M%S"))
            shutil.copytree(game_data, backup)
            for path, encoded in planned:
                temporary = path.with_name(path.name + ".tmp")
                temporary.write_bytes(encoded)
                os.replace(temporary, path)
    except OSError as error:
        print(f"ERROR write {error}")
        print(f"BACKUP {backup if backup else 'none'}")
        return 1
    print(f"SUMMARY OK={len(planned)} SKIP={skipped} ERROR=0 written={len(planned)}")
    print(f"BACKUP {backup if backup else 'none (nothing to upgrade)'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
