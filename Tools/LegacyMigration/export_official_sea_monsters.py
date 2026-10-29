"""Export sea actors and their own dynamic effects from verified Monster2_v4.

The source uses two different authoring conventions: Monster14 has no named
body child, while Monster16/17 do. This exporter resolves the visible child
from the timeline instead of assuming a name, and leaves gameplay values to
the formal tables and concrete entities.
"""
import argparse
from copy import deepcopy
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as E
from zipfile import ZipFile

from export_official_wukong import freeze, matrix, timeline_frames
from export_official_body_events import frame_events
from prepare_official_garuda_level import _probe_reader, _pack_layer, compact_registered_atlas
from audit_official_level_dependencies import SOURCE_HASHES


ACTORS = {
    10: ("jiao_demon_king", {"walk": "run", "wait": "idle", "hit1": "attack_1", "hit2": "attack_2", "hit3": "attack_3", "hurt": "hurt", "dead": "death"}),
    14: ("shrimp_demon", {"walk": "run", "wait": "idle", "hit1": "attack_1", "hurt": "hurt", "dead": "death"}),
    16: ("tide_demon", {"walk": "run", "wait": "idle", "hit1": "attack_1", "hit2": "attack_2", "hurt": "hurt", "dead": "death"}),
    17: ("turtle_minister", {"walk": "run", "wait": "idle", "hit1": "attack_1", "hurt": "hurt", "dead": "death"}),
    18: ("shark_spawn", {"walk": "run", "wait": "idle", "hit1": "attack_1", "hit2": "attack_2", "hurt": "hurt", "dead": "death"}),
    19: ("shark_demon_king", {"walk1": "run", "wait1": "idle", "walk2": "run_low", "wait2": "idle_low", "hit1-1": "attack_1", "hit2-1": "attack_2", "hit3-1": "attack_3", "hit1-2": "attack_1_low", "hit2-2": "attack_2_low", "hit3-2": "attack_3_low", "hurt": "hurt", "dead": "death"}),
    20: ("lantern_fish", {"walk": "run", "wait": "idle", "hit1": "attack_1", "hurt": "hurt", "dead": "death"}),
}


def compose(parent, child):
    a, b, c, d, x, y = parent
    ia, ib, ic, id_, ix, iy = child
    return [a * ia + c * ib, b * ia + d * ib, a * ic + c * id_, b * ic + d * id_,
            a * ix + c * iy + x, b * ix + d * iy + y]


def merge(regions):
    regions = [item for item in regions if item]
    if not regions:
        return None
    left = min(item[0] for item in regions)
    top = min(item[1] for item in regions)
    right = max(item[0] + item[2] for item in regions)
    bottom = max(item[1] + item[3] for item in regions)
    return [left, top, right - left, bottom - top]


def exporter(args, actor_specs=None, effect_symbols=(), hidden_children=(), compact=False,
             action_splits=(), effect_blend_modes=None, selector_frames=()):
    """Export verified actor timelines; optional splits keep independently driven parts separate."""
    repo = Path(__file__).resolve().parents[2]
    source = args.source.resolve()
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    source_pack = next((name for name in ('monster1', 'monster2', 'monster3')
                        if SOURCE_HASHES[name] == digest), None)
    if source_pack is None:
        raise ValueError("Unexpected official monster source revision")
    specs = ACTORS if actor_specs is None else actor_specs
    tree = E.parse(args.xml)
    tags = tree.getroot().find("tags")
    definitions = {int(tag.get("spriteId") or tag.get("shapeId")): tag
                   for tag in tags if tag.get("spriteId") or tag.get("shapeId")}
    symbols = {name.text: int(value.text)
               for tag in tags if tag.get("type") == "SymbolClassTag"
               for name, value in zip(tag.find("names"), tag.find("tags"))}
    inventory = json.loads(args.inventory.read_text(encoding="utf-8"))
    records = {int(item["id"]): item for item in inventory["actors"]}
    source_frames, source_bounds = _probe_reader(tags)
    work = args.work.resolve()
    work.mkdir(parents=True, exist_ok=True)
    profile = work / "profile"
    profile.mkdir(exist_ok=True)
    environment = dict(os.environ, APPDATA=str(profile))
    command = [str(args.java), f"-Duser.home={work}", "-jar", str(args.ffdec)]

    def run(*parts):
        with (work / "export.log").open("a", encoding="utf-8") as log:
            subprocess.run(command + list(parts), env=environment, stdout=log,
                           stderr=subprocess.STDOUT, check=True)

    selected = set()
    actors = []
    requested = (args.only or [14, 16, 17]) if specs else []
    for legacy_id in requested:
        name, labels = specs[legacy_id]
        record = records[legacy_id]
        if record['source_pack'] != source_pack:
            raise ValueError(f'Monster{legacy_id} belongs to {record["source_pack"]}, not {source_pack}')
        root_id = symbols[record["symbol"]]
        root = definitions[root_id]
        root_history = list(timeline_frames(root))
        # The script index is keyed by the source action sprite ID.
        script_by_sprite = {}
        for item in record.get("timeline_scripts", []):
            script = repo / item["path"]
            match = re.search(r"symbol(\d+)", script.read_text(encoding="utf-8-sig"))
            if match:
                script_by_sprite[int(match[1])] = script
        actions = []
        # The outer timeline is a label/action selector; inspect each source action
        # from the inventory because frame labels are stable across exporter runs.
        for source_action in record["actions"]:
            label = source_action["label"]
            if label not in labels:
                continue
            sid = int(source_action["sprite"])
            action = {"name": labels[label], "sprite": sid,
                      "frames": int(source_action["frames"]),
                      "matrix": source_action["matrix"], "hit_bounds": []}
            for display in source_frames(sid):
                sticks = [(item, age) for item, age in display if item.get("name") == "stick"]
                # A remote action can have no local stick; its effect owns the hitbox.
                regions = []
                for stick, age in sticks:
                    regions.append(source_bounds(int(stick.get("characterId")), age,
                        compose(action["matrix"], matrix(stick))))
                action["hit_bounds"].append(merge(regions))
            if sid in script_by_sprite:
                action["frame_events"] = [{"frame": frame + 1, "body": body}
                                             for frame, body in frame_events(script_by_sprite[sid].read_text(encoding="utf-8-sig"))]
            else:
                action["frame_events"] = []
            # Monster10's repeated attack IDs live on the nested stick timeline,
            # not its body selector. Preserve their real parent-frame positions.
            for frame, display in enumerate(source_frames(sid)):
                for child, age in display:
                    nested = int(child.get("characterId", 0))
                    if nested not in script_by_sprite or age >= int(definitions[nested].get("frameCount", 1)):
                        continue
                    events = frame_events(script_by_sprite[nested].read_text(encoding="utf-8-sig"))
                    for event_frame, body in events:
                        if event_frame == age and '.newAttackId()' in body:
                            action["frame_events"].append({"frame": frame + 1, "body": body, "nested_sprite": nested})
            action["frame_events"].sort(key=lambda event: event["frame"])
            action["complete_frames"] = [event["frame"] for event in action["frame_events"]
                if re.search(r'curAction\s*=\s*"wait[12]?"', event["body"]) or '.destroy()' in event["body"]]
            actions.append(action)
            selected.add(sid)
        # Resolve the hurt box from the first source root frame. Monster14 has three
        # colipse parts; merging them preserves the official silhouette without alpha polygons.
        hurt_regions = []
        first_display = root_history[0]
        for item in first_display.values():
            if item.get("name") == "colipse":
                hurt_regions.append(source_bounds(int(item.get("characterId")), 0, matrix(item)))
        if not hurt_regions:
            raise ValueError(f"Missing official hurt probe: Monster{legacy_id}")
        actors.append({"name": name, "legacy_id": legacy_id, "symbol": record["symbol"],
                       "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                       "hurt_bounds": merge(hurt_regions), "actions": actions})

    # Monster14's remote effect is a one-frame wrapper around the official
    # projectile symbol; export it separately so the runtime never reuses a
    # different monster's visual as a placeholder.
    # Monster16.throwKnife is unreachable in this revision. Its hit2 timeline
    # only changes curAction at frame 25; do not invent a projectile method key.
    effect_sources = [(name, symbols[symbol], int(definitions[symbols[symbol]].get('frameCount')))
                      for name, symbol in effect_symbols]
    if 14 in requested:
        effect_sources.append(("shrimp_projectile", 39, 1))
    if 10 in requested:
        effect_sources.extend([("jiao_dragon_wave", 1194, 25), ("jiao_back_hit", 1165, 10)])
    for actor_id, effects in [
        (18, [("shark_spawn_slash", "Monster18Bullet1"), ("shark_spawn_warning", "Monster18Bullet2Pre"),
              ("shark_spawn_tornado", "Monster18Bullet2")]),
        (19, [("shark_king_shark_wave", "Monster19Bullet1"), ("shark_king_bubble_warning", "Monster19Bullet2Pre"),
              ("shark_king_bubble", "Monster19Bullet2")])]:
        if actor_id in requested:
            for effect_name, symbol in effects:
                sprite = symbols[symbol]
                effect_sources.append((effect_name, sprite, int(definitions[sprite].get("frameCount"))))
    # A separately controlled heart cannot also remain baked into the body PNG.
    # The new wrapper retains the parent's placement filters and registration;
    # exporting the child symbol alone would silently lose the purple glow.
    splits = []
    effect_matrices = {}
    for sprite_id, child_name in hidden_children:
        children = definitions[sprite_id].find('subTags')
        matches = [item for item in children if item.get('name') == child_name]
        if len(matches) != 1:
            raise ValueError(f'Expected one independently controlled child: {sprite_id}/{child_name}')
        item = matches[0]
        child_id = int(item.get('characterId'))
        count = int(definitions[child_id].get('frameCount'))
        wrapper_id = max(int(tag.get('spriteId') or tag.get('shapeId') or tag.get('characterId') or 0)
                         for tag in tags) + 1
        wrapper = E.Element('item', type='DefineSpriteTag', spriteId=str(wrapper_id), frameCount=str(count))
        contents = E.SubElement(wrapper, 'subTags')
        contents.append(deepcopy(item))
        for _ in range(count):
            E.SubElement(contents, 'item', type='ShowFrameTag')
        E.SubElement(contents, 'item', type='EndTag')
        tags.insert(list(tags).index(definitions[sprite_id]) + 1, wrapper)
        definitions[wrapper_id] = wrapper
        effect_sources = [(name, wrapper_id if source_id == child_id else source_id, frames)
                          for name, source_id, frames in effect_sources]
        parent_matrices = [action['matrix'] for actor in actors for action in actor['actions']
                           if action['sprite'] == sprite_id]
        if not parent_matrices or any(value != parent_matrices[0] for value in parent_matrices):
            raise ValueError(f'Independent child requires one parent registration: {sprite_id}/{child_name}')
        effect_matrices[wrapper_id] = parent_matrices[0]
        splits.append({'parent_sprite': sprite_id, 'child_name': child_name,
                       'sprite': child_id, 'matrix': matrix(item), 'wrapper_sprite': wrapper_id,
                       'parent_matrix': parent_matrices[0],
                       'placement_filters_preserved': item.find('surfaceFilterList') is not None})
        children.remove(item)
    for parent_id, depth, name in action_splits:
        parent = definitions[parent_id]
        wrapper_id = max(int(tag.get('spriteId') or tag.get('shapeId') or tag.get('characterId') or 0)
                         for tag in tags) + 1
        wrapper = deepcopy(parent)
        wrapper.set('spriteId', str(wrapper_id))
        contents = wrapper.find('subTags')
        original = parent.find('subTags')
        found = False
        for item in list(contents):
            kind = item.get('type', '')
            if kind.startswith(('PlaceObject', 'RemoveObject')):
                if item.get('depth') != str(depth):
                    contents.remove(item)
                else:
                    found = True
        if not found:
            raise ValueError(f'Missing independently blended action layer: {parent_id}/{depth}')
        for item in list(original):
            if item.get('type', '').startswith(('PlaceObject', 'RemoveObject')) and item.get('depth') == str(depth):
                original.remove(item)
        tags.insert(list(tags).index(parent) + 1, wrapper)
        definitions[wrapper_id] = wrapper
        count = int(wrapper.get('frameCount'))
        effect_sources.append((name, wrapper_id, count))
        effect_matrices[wrapper_id] = next(action['matrix'] for actor in actors
                                          for action in actor['actions'] if action['sprite'] == parent_id)
        splits.append({'parent_sprite': parent_id, 'depth': depth, 'effect': name,
                       'wrapper_sprite': wrapper_id, 'matrix': effect_matrices[wrapper_id]})
    selected.update(sprite for _, sprite, _ in effect_sources)
    for sprite_id, frame in selector_frames:
        freeze(definitions[sprite_id], frame)
    # Geometry above deliberately used the untouched source. New wrappers are
    # visible-only exports and need their own current definition index here.
    _, export_bounds = _probe_reader(tags)

    xml = work / "sea_selected.xml"
    art_swf = work / "sea_selected.swf"
    # Re-serialize the original XML; selected sprite IDs are exported independently.
    tree.write(xml, encoding="utf-8", xml_declaration=True)
    run("-xml2swf", str(xml), str(art_swf))
    run("-format", "sprite:png", "-selectid", ",".join(map(str, sorted(selected))),
        "-ignorebackground", "-export", "sprite", str(work / "png"), str(art_swf))
    compiler = work / "bounds-classpath.jar"
    with ZipFile(args.ffdec.parent / "lib/ffdec_lib.jar") as source_jar, ZipFile(compiler, "w") as target:
        for item in source_jar.infolist():
            if item.filename.upper() != "META-INF/MANIFEST.MF":
                target.writestr(item, source_jar.read(item.filename))
    subprocess.run([str(args.java.with_name("javac.exe")), "-proc:none", "-cp", str(compiler),
                    "-d", str(work), str(Path(__file__).with_name("FlashSpriteBounds.java"))],
                   env=environment, check=True)
    bounds_text = subprocess.check_output([str(args.java), "-cp",
        os.pathsep.join([str(work), str(args.ffdec), str(args.ffdec.parent / "lib/*")]),
        "FlashSpriteBounds", str(art_swf), *map(str, sorted(selected))], env=environment, text=True)
    raster_bounds = {int(row[0]): [int(value) / 20 for value in row[1:]] for line in bounds_text.splitlines()
                     if len(row := line.split()) == 5}
    folders = {int(path.name.split("_")[1]): path for path in (work / "png").iterdir() if path.is_dir()}
    for actor in actors:
        actor["layers"] = [{"name": "body", "actions": [], "output": str(work / (actor["name"] + "_body.png"))}]
        for action in actor["actions"]:
            action["input"] = str(folders[action["sprite"]])
            action["bounds"] = raster_bounds[action["sprite"]]
            actor["layers"][0]["actions"].append(action)
        _pack_layer(actor["layers"][0])
        if compact or actor['name'] in ('jiao_demon_king', 'shark_demon_king', 'shark_spawn'):
            compact_registered_atlas(actor['layers'][0])
    effects = []
    for name, sprite, count in effect_sources:
        action = {"name": "play", "sprite": sprite, "frames": count,
            "matrix": effect_matrices.get(sprite, [1, 0, 0, 1, 0, 0]),
            "bounds": raster_bounds[sprite], "input": str(folders[sprite])}
        layer = {"name": "body", "actions": [action], "output": str(work / f"{name}.png")}
        _pack_layer(layer)
        if compact or name.startswith('shark_'):
            compact_registered_atlas(layer)
        effects.append({"name": name, "sprite": sprite, "actions": [action], "layers": [layer],
                        "blend_mode": (effect_blend_modes or {}).get(name, 'mix'),
                        "hit_bounds": export_bounds(sprite, 0, [1, 0, 0, 1, 0, 0])})
    notes = ["Original source registration and frame order are preserved; no alpha-based recentering."]
    if 16 in requested:
        notes.append("Monster16.throwKnife has no reachable caller in this source; hit2 uses its body stick.")
    if splits:
        notes.append("Independently controlled children are excluded from their parent raster and retain their source matrix.")
    output = work / "manifest.json"
    output.write_text(json.dumps({"fps": 24, "source_sha256": digest,
                                  "source_pack": source_pack, "independent_children": splits,
                                  "selector_frames": [list(value) for value in selector_frames],
                                  "actors": actors,
                                  "effects": effects,
                                  "notes": notes},
                                 ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    marker = "SEA MONSTER SOURCE PASS:" if actor_specs is None else "OFFICIAL ACTOR SOURCE PASS:"
    print(marker, [(item["name"], [action["name"] for action in item["actions"]]) for item in actors])


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--xml", type=Path, required=True)
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--java", type=Path, required=True)
    parser.add_argument("--ffdec", type=Path, required=True)
    parser.add_argument("--work", type=Path, required=True)
    parser.add_argument("--only", type=int, nargs='+', choices=ACTORS,
                        help="Default: the three shallow-shore monsters; select 10 20 for East Sea.")
    exporter(parser.parse_args())
