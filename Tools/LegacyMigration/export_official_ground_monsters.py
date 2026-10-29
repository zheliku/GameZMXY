"""Export complete Monster1 body timelines for the first three ground monsters.

The output is an authoring manifest consumed by
``EditorScripts/import_official_ground_monsters.gd``.  It keeps Flash parent
registration, every source frame and named stick hit window; no runtime code
uses the manifest.
"""

import argparse
from copy import deepcopy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
from zipfile import ZipFile

from export_official_wukong import matrix, timeline_frames

ACTORS = [
    ("huaguoshan_monkey", "export.monster.Monster1", 1),
    ("demon_monkey", "export.monster.Monster2", 2),
    ("gorilla", "export.monster.Monster3", 3),
]
# The source hurt frame scripts return directly to wait; afterHurt has no runtime caller.
LABELS = {"walk": "run", "wait": "idle", "hurt": "hurt", "dead": "death", "hit1": "attack_1"}


def transformed_bounds(bounds, transform):
    a, b, c, d, tx, ty = transform
    points = [(a * x + c * y + tx, b * x + d * y + ty)
              for x in (bounds[0], bounds[2]) for y in (bounds[1], bounds[3])]
    xs, ys = zip(*points)
    return [min(xs), min(ys), max(xs) - min(xs), max(ys) - min(ys)]


def export(args):
    work = args.work.resolve()
    work.mkdir(parents=True, exist_ok=True)
    profile = work / "profile"
    profile.mkdir(exist_ok=True)
    environment = dict(os.environ, APPDATA=str(profile))
    command = [str(args.java), f"-Duser.home={work}", "-jar", str(args.ffdec)]

    def run(*arguments):
        with (work / "export.log").open("a", encoding="utf-8") as log:
            subprocess.run(command + list(arguments), env=environment, stdout=log,
                           stderr=subprocess.STDOUT, check=True)

    compiler = work / "bounds-classpath.jar"
    with ZipFile(args.ffdec.parent / "lib/ffdec_lib.jar") as source, ZipFile(compiler, "w") as destination:
        for member in source.infolist():
            if member.filename.upper() != "META-INF/MANIFEST.MF":
                destination.writestr(member, source.read(member.filename))
    subprocess.run([str(args.java.with_name("javac.exe")), "-proc:none", "-cp", str(compiler),
                    "-d", str(work), str(Path(__file__).with_name("FlashSpriteBounds.java"))],
                   env=environment, check=True)

    content = args.source.read_bytes()
    if content[:3] not in (b"FWS", b"CWS", b"ZWS"):
        content = content[100:110] + content[:100] + content[110:]
    expected = "5bd88da4211870f2ca2354b7cec8d1790747869641c11fe553910b10fe2a264a"
    if hashlib.sha256(content).hexdigest() != expected:
        raise ValueError("Unexpected Monster1 source revision")
    swf = work / "monster1.swf"
    xml = work / "monster1.xml"
    swf.write_bytes(content)
    if not xml.exists():
        run("-swf2xml", str(swf), str(xml))
    tree = ET.parse(xml)
    tags = tree.getroot().find("tags")
    sprites = {int(tag.get("spriteId")): tag for tag in tags if tag.get("spriteId")}
    shapes = {int(tag.get("shapeId")): tag for tag in tags if tag.get("shapeId")}
    symbols = {}
    for tag in tags:
        if tag.get("type") == "SymbolClassTag":
            symbols.update(zip((item.text for item in tag.find("names")),
                               (int(item.text) for item in tag.find("tags"))))

    runtime_classpath = os.pathsep.join([str(work), str(args.ffdec), str(args.ffdec.parent / "lib/*")])
    bounds_ids = set()

    def rectangle(sprite_id, transform):
        if sprite_id in sprites:
            display = next(timeline_frames(sprites[sprite_id]))
            child = next(iter(display.values()))
            inner = matrix(child)
            a, b, c, d, x, y = transform
            ia, ib, ic, id_, ix, iy = inner
            return rectangle(int(child.get("characterId")),
                             [a * ia + c * ib, b * ia + d * ib,
                              a * ic + c * id_, b * ic + d * id_,
                              a * ix + c * iy + x, b * ix + d * iy + y])
        box = shapes[sprite_id].find("shapeBounds")
        values = [float(box.get(key)) / 20 for key in ("Xmin", "Ymin", "Xmax", "Ymax")]
        return transformed_bounds(values, transform)

    actors = []
    selected = set()
    for name, symbol, entity_id in ACTORS:
        actor = {"name": name, "entity_id": entity_id, "symbol": symbol,
                 "source_sha256": expected, "actions": []}
        label = None
        root = sprites[symbols[symbol]]
        for tag in root.find("subTags"):
            if tag.get("type") == "FrameLabelTag":
                label = tag.get("name")
            elif tag.get("name") == "colipse" and tag.get("characterId") and "hurt_bounds" not in actor:
                sid = int(tag.get("characterId"))
                actor["hurt_bounds"] = rectangle(sid, matrix(tag))
                bounds_ids.add(sid)
            elif tag.get("name") == "body" and tag.get("characterId"):
                sid = int(tag.get("characterId"))
                if label not in LABELS:
                    continue
                action = {"name": LABELS[label], "sprite": sid, "matrix": matrix(tag),
                          "frames": int(sprites[sid].get("frameCount")), "hit_bounds": []}
                for display in timeline_frames(sprites[sid]):
                    probe = next((item for item in display.values() if item.get("name") == "stick"), None)
                    if probe is None:
                        action["hit_bounds"].append(None)
                    else:
                        probe_id = int(probe.get("characterId"))
                        transform = matrix(probe)
                        transform[4] += action["matrix"][4]
                        transform[5] += action["matrix"][5]
                        action["hit_bounds"].append(rectangle(probe_id, transform))
                        bounds_ids.add(probe_id)
                actor["actions"].append(action)
                selected.add(sid)
        actors.append(actor)

    variant = deepcopy(tree)
    variant_sprites = {int(tag.get("spriteId")): tag for tag in variant.getroot().find("tags") if tag.get("spriteId")}
    for sid in selected:
        children = variant_sprites[sid].find("subTags")
        removed = set()
        for tag in list(children):
            if tag.get("name") == "stick":
                removed.add(tag.get("depth"))
            if tag.get("depth") in removed:
                children.remove(tag)
    art_xml, art_swf = work / "monster1_art.xml", work / "monster1_art.swf"
    variant.write(art_xml, encoding="utf-8", xml_declaration=True)
    run("-xml2swf", str(art_xml), str(art_swf))
    png = work / "monster1_png"
    run("-format", "sprite:png", "-selectid", ",".join(map(str, sorted(selected))),
        "-ignorebackground", "-export", "sprite", str(png), str(art_swf))
    bounds_text = subprocess.check_output([
        str(args.java), "-cp", runtime_classpath, "FlashSpriteBounds", str(art_swf),
        *map(str, sorted(selected))], env=environment, text=True)
    art_bounds = {int(row[0]): [int(value) / 20 for value in row[1:]]
                  for line in bounds_text.splitlines() if len(row := line.split()) == 5}
    folders = {int(path.name.split("_")[1]): path for path in png.iterdir() if path.is_dir()}
    for actor in actors:
        actor["layers"] = [{"name": "body", "actions": [
            {**action, "bounds": art_bounds[action["sprite"]], "input": str(folders[action["sprite"]])}
            for action in actor["actions"]]}]
    destination = work / "manifest.json"
    destination.write_text(json.dumps({"fps": 24, "actors": actors}, ensure_ascii=False, indent=2), encoding="utf-8")
    print("GROUND MONSTER EXPORT PASS:", [(a["name"], sum(x["frames"] for x in a["actions"])) for a in actors])


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--java", type=Path, required=True)
    parser.add_argument("--ffdec", type=Path, required=True)
    parser.add_argument("--work", type=Path, required=True)
    export(parser.parse_args())
