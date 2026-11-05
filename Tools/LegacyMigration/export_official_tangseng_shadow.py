"""Export the audited Role2Shadow walk and skill timelines as an atlas.

The input is the same verified Role_v7/XML pair used by the Tang Seng body
export.  The script keeps the source matrices and frame order, freezes the
official wardrobe selectors, and delegates SVG rasterization to the existing
Godot wardrobe importer.
"""
import argparse
from copy import deepcopy
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

import export_official_wukong as base
from export_official_tangseng import freeze_wardrobe
from audit_official_tangseng import ROLE_HASH


ACTION_ORDER = ["walk", "hit3", "hit5"]


def actor_actions(audit):
    actor = audit["actors"]["export.hero.Role2Shadow"]
    actions = {item["label"]: item for item in actor["actions"]}
    missing = [name for name in ACTION_ORDER if name not in actions]
    if missing:
        raise ValueError("Official Role2Shadow action missing: " + ", ".join(missing))
    return [actions[name] for name in ACTION_ORDER]


def export(args):
    if hashlib.sha256(args.role.read_bytes()).hexdigest() != ROLE_HASH:
        raise ValueError("Unexpected official Role_v7 revision")
    audit = json.loads(args.audit.read_text(encoding="utf-8"))
    if audit["role_sha256"] != ROLE_HASH:
        raise ValueError("Shadow audit does not match the official role")
    if hashlib.sha256(args.xml.read_bytes()).hexdigest() != audit["xml_sha256"]:
        raise ValueError("Shadow audit XML hash mismatch")

    actions = actor_actions(audit)
    tree = ET.parse(args.xml)
    freeze_wardrobe(tree, audit, args.cloth, args.weapon)
    args.work.mkdir(parents=True, exist_ok=True)
    (args.work / "profile").mkdir(exist_ok=True)
    environment = dict(os.environ, APPDATA=str((args.work / "profile").resolve()))
    command = [str(args.java), f"-Duser.home={args.work.resolve()}", "-jar", str(args.ffdec)]

    source_sprites = {
        int(tag.get("spriteId")): tag
        for tag in tree.getroot().find("tags")
        if tag.get("type") == "DefineSpriteTag" and tag.get("spriteId")
    }
    shadow = source_sprites[audit["actors"]["export.hero.Role2Shadow"]["sprite"]]
    labels = {}
    label = None
    for tag in shadow.find("subTags"):
        if tag.get("type") == "FrameLabelTag":
            label = tag.get("name")
        elif tag.get("name") == "body" and tag.get("characterId"):
            labels[label] = int(tag.get("characterId"))
    for action in actions:
        if labels.get(action["label"]) != action["sprite"]:
            raise ValueError(f"Role2Shadow label identity differs: {action['label']}")

    filtered = args.work / "role2_shadow_variant.xml"
    tree.write(filtered, encoding="utf-8", xml_declaration=True)
    swf = args.work / "role2_shadow_variant.swf"
    selected = ",".join(str(action["sprite"]) for action in actions)
    with (args.work / "export.log").open("w", encoding="utf-8") as log:
        subprocess.run(command + ["-xml2swf", str(filtered), str(swf)], env=environment,
                       stdout=log, stderr=subprocess.STDOUT, check=True)
        subprocess.run(command + ["-format", "sprite:svg", "-selectid", selected,
                                  "-ignorebackground", "-export", "sprite",
                                  str(args.work / "vectors"), str(swf)], env=environment,
                       stdout=log, stderr=subprocess.STDOUT, check=True)

    folders = {
        int(match.group(1)): path
        for path in (args.work / "vectors").iterdir()
        if path.is_dir() and (match := re.match(r"DefineSprite_(\d+)", path.name))
    }
    if set(int(action["sprite"]) for action in actions) - folders.keys():
        raise ValueError("FFDec did not export every Role2Shadow action")

    base.WEAPON_IDS = {"723"}
    base.EFFECT_IDS = set()
    frames = []
    for action in actions:
        action_dir = args.work / "layers" / action["label"]
        action_dir.mkdir(parents=True, exist_ok=True)
        sources = sorted(folders[int(action["sprite"])].glob("*.svg"),
                         key=lambda path: int(re.findall(r"\d+", path.stem)[-1]))
        if len(sources) != int(action["frames"]):
            raise ValueError(f"Incomplete Shadow frames for {action['label']}: {len(sources)}")
        for source_frame, source in enumerate(sources, 1):
            base.split_pose(source, action_dir, action["matrix"])
            body = action_dir / f"{source.stem}_body.svg"
            if not body.is_file():
                raise ValueError(f"Missing Shadow body frame {action['label']}/{source_frame}")
            frames.append({"action": action["label"], "source_sprite": action["sprite"],
                           "source_frame": source_frame, "body_svg": str(body.relative_to(args.work).as_posix())})

    manifest = {
        "source_sha256": ROLE_HASH,
        "xml_sha256": audit["xml_sha256"],
        "role": "Role2Shadow",
        "kind": "body",
        "selector": args.cloth,
        "columns": 6,
        "action_order": ACTION_ORDER,
        "frames": frames,
        "lifetime_source_frames": 192,
        "layer_root": str((args.work / "layers").resolve()),
    }
    (args.work / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"TANGSENG SHADOW FRAME EXPORT PASS: {len(frames)} frames; actions {ACTION_ORDER}")


def rasterize(args):
    manifest = json.loads((args.work / "manifest.json").read_text(encoding="utf-8"))
    if manifest["source_sha256"] != ROLE_HASH or manifest["role"] != "Role2Shadow":
        raise ValueError("Only the audited Role2Shadow manifest may be rasterized")
    output = args.output.resolve()
    if output.exists():
        raise ValueError(f"Refusing to overwrite authored shadow atlas: {output}")
    poses = {}
    inputs = {}
    for index, frame in enumerate(manifest["frames"]):
        poses[str(index)] = [frame["source_sprite"], frame["source_frame"]]
        inputs[str(frame["source_sprite"])] = str((args.work / "layers" / frame["action"]).resolve())
    raster = {
        "kind": "body", "selector": manifest["selector"], "columns": manifest["columns"],
        "rows": (len(poses) + manifest["columns"] - 1) // manifest["columns"],
        "poses": poses, "inputs": inputs,
        "output": str(output),
    }
    target = args.work / "raster_manifest.json"
    target.write_text(json.dumps(raster, indent=2), encoding="utf-8")
    project = Path(__file__).resolve().parents[2] / "Godot/GodotProject"
    subprocess.run([str(args.godot), "--headless", "--path", str(project), "--script",
                    "res://EditorScripts/import_official_wukong_equipment.gd", "--",
                    str(target.resolve())], check=True)
    if not output.is_file():
        raise ValueError("Godot did not save the Shadow atlas")
    print("TANGSENG SHADOW ATLAS PASS:", output)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--role", type=Path, required=True)
    parser.add_argument("--xml", type=Path, required=True)
    parser.add_argument("--audit", type=Path, required=True)
    parser.add_argument("--java", type=Path, required=True)
    parser.add_argument("--ffdec", type=Path, required=True)
    parser.add_argument("--work", type=Path, required=True)
    parser.add_argument("--cloth", type=int, default=1)
    parser.add_argument("--weapon", type=int, default=1)
    parser.add_argument("--rasterize-only", action="store_true")
    parser.add_argument("--godot", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if args.rasterize_only:
        if args.godot is None or args.output is None:
            parser.error("--rasterize-only requires --godot and --output")
        rasterize(args)
    else:
        export(args)
