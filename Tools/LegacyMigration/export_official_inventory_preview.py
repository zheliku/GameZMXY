"""Export the actual backpack_v2 Wukong/Tangseng preview timelines and equipment.

Uses existing selector freezing, depth-aware body/weapon splitting and Godot
rasterization. Combat atlases and animation libraries are never rewritten.
"""
import argparse
from bisect import bisect_right
import hashlib
from io import BytesIO
import json
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as E

from PIL import Image

import export_official_wukong as source
from import_official_wukong_attacks import track
from import_official_tangseng_art import compact_atlases

SOURCE_SHA = "47215d42043801243e7beae00ca4601d6040b343be8bd8a29a9c4aa4e8df17ea"
PROJECT = Path(__file__).resolve().parents[2] / "Godot/GodotProject"


def export_animated_idle(args):
    """Pack the existing high-density Role idle cells into the original preview paths."""
    project = args.project.resolve()
    destination = (args.output_project or project).resolve()
    args.work.mkdir(parents=True, exist_ok=True)
    report = {"source": "current Role_v7 idle1", "density": 2, "heroes": {}}
    for hero, shift in [("wukong", (-2, -2)), ("tangseng", (2, 10))]:
        art = Path(f"TheGame/Sprites/Characters/Heroes/{hero}")
        library_path = Path(f"TheGame/Entitys/Animations/Heroes/{hero.title()}/{hero}_preview_library.tres")
        combat_path = library_path.with_name(hero + "_anim_library.tres")
        combat_text = (project / combat_path).read_text(encoding="utf-8")
        idle = re.search(r'^\[sub_resource type="Animation" id="idle1"\].*?(?=^\[|\Z)',
                         combat_text, re.M | re.S)[0]
        length = float(re.search(r'^length = ([^\n]+)', idle, re.M)[1])
        frame_count = round(length * 24)
        sequences, source_cells = {}, {}
        for index, kind in enumerate(["body", "weapon"]):
            keys = re.search(r'tracks/' + str(index) + r'/keys = \{(.*?)\n\}', idle, re.S)[1]
            times = [float(value) for value in re.search(r'"times": PackedFloat32Array\((.*?)\)', keys)[1].split(",")]
            values = [int(value) for value in re.search(r'"values": \[(.*?)\]', keys)[1].split(",")]
            sequences[kind] = [values[bisect_right(times, frame / 24 + 1e-6) - 1]
                               for frame in range(frame_count)]
            source_cells[kind] = list(dict.fromkeys(sequences[kind]))
        appearance = (project / art / (hero + "_inventory_appearance.tres")).read_text(encoding="utf-8")
        paths = re.findall(r'path="res://([^\"]+/preview/[^\"]+\.png)"', appearance)
        variants, sources = {}, []
        unique_count = max(map(len, source_cells.values()))
        for relative in paths:
            kind = "body" if "_body_" in relative else "weapon"
            source_path = Path(relative.replace("/preview/", "/").replace("_preview_", "_"))
            image = Image.open(project / source_path).convert("RGBA")
            assert image.width == 2400 and image.height % 400 == 0, source_path
            selected = Image.new("RGBA", (2400, ((unique_count + 5) // 6) * 400))
            for index in range(unique_count):
                cell_id = source_cells[kind][min(index, len(source_cells[kind]) - 1)]
                cell = image.crop((cell_id % 6 * 400, cell_id // 6 * 400,
                                   cell_id % 6 * 400 + 400, cell_id // 6 * 400 + 400))
                bounds = cell.getchannel("A").getbbox()
                assert bounds and bounds[0] + shift[0] >= 0 and bounds[1] + shift[1] >= 0 and \
                    bounds[2] + shift[0] <= 400 and bounds[3] + shift[1] <= 400, (relative, cell_id, bounds)
                selected.paste(cell, (index % 6 * 400 + shift[0], index // 6 * 400 + shift[1]))
            scratch = args.work / "selected" / (Path(relative).stem + ".png")
            scratch.parent.mkdir(parents=True, exist_ok=True)
            selected.save(scratch)
            key = kind, Path(relative).stem
            variants[key] = {"path": str(scratch), "sha256": hashlib.sha256(scratch.read_bytes()).hexdigest()}
            sources.append({"source": source_path.as_posix(), "source_sha256": hashlib.sha256((project / source_path).read_bytes()).hexdigest(),
                            "preview": relative, "kind": kind})
        packed, mapping, counts = compact_atlases(variants, frame_count=unique_count, cell_size=400)
        cell_maps = {kind: {original: mapping[kind][index] for index, original in enumerate(source_cells[kind])}
                     for kind in ["body", "weapon"]}
        for row in sources:
            content = packed[row["kind"], Path(row["preview"]).stem]
            target = destination / row["preview"]
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(content)
            row["preview_sha256"] = hashlib.sha256(content).hexdigest()
            row["size"] = list(Image.open(BytesIO(content)).size)
        previous = (project / library_path).read_text(encoding="utf-8")
        uid = re.search(r'uid="([^"]+)"', previous)[1]
        result = f'[gd_resource type="AnimationLibrary" format=3 uid="{uid}"]\n\n[sub_resource type="Animation" id="idle"]\nresource_name = "idle"\nlength = {length}\nloop_mode = 1\n'
        for index, kind in enumerate(["body", "weapon"]):
            result += track(index, kind.title() + ":frame", [frame / 24 for frame in range(frame_count)],
                            [str(cell_maps[kind][cell]) for cell in sequences[kind]])
        aura_frames = int(length * 15) + 1
        result += track(2, "Aura:frame", [frame / 15 for frame in range(aura_frames)],
                        [str(3 + frame % 6) for frame in range(aura_frames)])
        result += track(3, "Body:position", [0], ["Vector2(0, 0)"]) + track(4, "Weapon:position", [0], ["Vector2(0, 0)"])
        result += '[resource]\n_data = {&"idle": SubResource("idle")}\n'
        target = destination / library_path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(result.replace("\n", "\r\n").encode("utf-8"))
        report["heroes"][hero] = {"source_library": combat_path.as_posix(), "source_library_sha256": hashlib.sha256((project / combat_path).read_bytes()).hexdigest(),
            "frames": frame_count, "fps": 24, "length": length, "fixed_source_pixel_translation": shift,
            "source_sequences": sequences, "source_cell_maps": cell_maps, "packed_poses": counts, "variants": sources}
    (args.work / "animated_idle_manifest.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("ANIMATED PREVIEW: 26 atlases; original Role idle timing and source pixels; no combat resources changed.")


def export(args):
    """Install both official preview libraries and independently replaceable wardrobe atlases."""
    if hashlib.sha256(args.swf.read_bytes()).hexdigest() != SOURCE_SHA:
        raise ValueError("Unexpected backpack_v2 revision")
    args.work.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, APPDATA=str((args.work / "profile").resolve()))
    Path(env["APPDATA"]).mkdir(exist_ok=True)
    destination = args.output_project or PROJECT
    command = [str(args.java), "-Duser.home=" + str(args.work.resolve()), "-jar", str(args.ffdec)]
    def run(*argv):
        with (args.work / "export.log").open("a", encoding="utf-8") as log:
            subprocess.run(command + list(argv), env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
    all_manifests = {}
    for root in args.manifests:
        for path in root.rglob("manifest.json"):
            data = json.loads(path.read_text(encoding="utf-8-sig"))
            if "selector" in data and "output" in data and "kind" in data:
                all_manifests[Path(data["output"]).stem] = data
    for hero, sid, frames, clothes, weapon, transform in [
        ("wukong", 252, 62, [159, 167, 175, 200], 192, [1, 0, 0, 1, .65, -.2]),
        ("tangseng", 283, 1, [276, 282], 269, [1, 0, 0, 1, 0, -.05]),
    ]:
        appearance_path = PROJECT / f"TheGame/Sprites/Characters/Heroes/{hero}/{hero}_appearance.tres"
        appearance = appearance_path.read_text(encoding="utf-8-sig")
        resources = {id: path for path, id in re.findall(r'\[ext_resource[^\n]*path="([^"]+)" id="([^"]+)"', appearance)}
        texture_ids = set(re.findall(r'ExtResource\("([^"]+)"\)', appearance))
        textures = {id: path for id, path in resources.items() if id in texture_ids and path.endswith(".png")}
        preview_resources = []
        variants = {}
        for id, old_path in textures.items():
            stem = Path(old_path).stem
            data = all_manifests.get(stem)
            if hero == "tangseng":
                names = {"body": {1:"bare",2:"cassock",3:"excellent",4:"heirloom",5:"kirin",6:"heavenly_fiend"},
                         "weapon":{1:"default",2:"pine",3:"excellent",4:"earth_fiend",5:"white_tiger",6:"blood_sea",7:"nine_ring"}}
                kind = "body" if "_body_" in stem else "weapon"
                selector = next(i for i, name in names[kind].items() if stem == hero + "_" + kind + "_" + name)
            else:
                if data is None:
                    raise ValueError("Missing reviewed selector manifest for " + stem)
                kind, selector = data["kind"], int(data["selector"])
            work = args.work / stem
            work.mkdir(exist_ok=True)
            tree = E.parse(args.xml)
            sprites = {int(t.get("spriteId")): t for t in tree.getroot().find("tags") if t.get("type") == "DefineSpriteTag"}
            for cid in clothes:
                source.freeze(sprites[cid], selector if kind == "body" else 1)
            source.freeze(sprites[weapon], selector if kind == "weapon" else 1)
            xml, swf = work / "variant.xml", work / "variant.swf"
            tree.write(xml, encoding="utf-8", xml_declaration=True)
            run("-xml2swf", str(xml), str(swf))
            run("-format", "sprite:svg", "-selectid", str(sid), "-ignorebackground", "-export", "sprite", str(work / "vectors"), str(swf))
            vectors = next((work / "vectors").glob("DefineSprite_*"))
            layers = work / "layers"
            layers.mkdir(exist_ok=True)
            source.WEAPON_IDS, source.EFFECT_IDS = {str(weapon)}, set()
            for svg in vectors.glob("*.svg"):
                source.split_pose(svg, layers, transform, excluded_ids=frozenset())
            out = destination / f"TheGame/Sprites/Characters/Heroes/{hero}/preview/{hero}_preview_{stem.removeprefix(hero + '_')}.png"
            out.parent.mkdir(parents=True, exist_ok=True)
            raster = {"kind":kind,"selector":selector,"columns":6,"rows":(frames+5)//6,"density":args.density,
                      "poses":{str(i):[sid,i+1] for i in range(frames)},"inputs":{str(sid):str(layers.resolve())},"output":str((work / "atlas.png").resolve())}
            raster_path = work / "raster.json"
            raster_path.write_text(json.dumps(raster), encoding="utf-8")
            if not Path(raster["output"]).exists():
                with (work / "raster.log").open("w",encoding="utf-8") as log:
                    subprocess.run([str(args.godot),"--headless","--path",str(PROJECT),"--script","res://EditorScripts/import_official_wukong_equipment.gd","--",str(raster_path.resolve())],env=env,stdout=log,stderr=subprocess.STDOUT,check=True)
            out.write_bytes(Path(raster["output"]).read_bytes())
            variants[kind, id] = {"path": str(out), "sha256": hashlib.sha256(out.read_bytes()).hexdigest()}
            preview_resources.append(f'[ext_resource type="Texture2D" path="res://{out.relative_to(destination).as_posix()}" id="{id}"]')
            print("INVENTORY PREVIEW",hero,kind,selector,frames,"frames",flush=True)
        packed, mapping, counts = compact_atlases(variants, frame_count=frames, cell_size=200 * args.density)
        for key, item in variants.items():
            Path(item["path"]).write_bytes(packed[key])
        preview_path = destination / appearance_path.relative_to(PROJECT).with_name(hero + "_inventory_appearance.tres")
        body = appearance.split("[resource]",1)[1]
        body = re.sub(r'_previewAnimations = [^\n]+\n',"",body)
        body = re.sub(r'_previewAppearance = [^\n]+\n',"",body)
        body = re.sub(r'_textureDensity = [^\n]+\n',"",body)
        body += f'\n_textureDensity = {args.density}\n'
        script = next(line for line in appearance.splitlines() if 'type="Script"' in line)
        preview_path.write_text('[gd_resource type="Resource" script_class="HeroAppearanceResource" format=3]\n\n'+script+'\n'+'\n'.join(preview_resources)+'\n\n[resource]'+body,encoding="utf-8")
        if 'id="inventory_appearance"' not in appearance:
            appearance = appearance.replace('[resource]',f'[ext_resource type="Resource" path="res://{preview_path.relative_to(destination).as_posix()}" id="inventory_appearance"]\n\n[resource]')
            appearance = appearance.replace('script = ExtResource("script")',
                'script = ExtResource("script")\nm_PreviewAppearance = ExtResource("inventory_appearance")')
            (destination / appearance_path.relative_to(PROJECT)).write_text(appearance,encoding="utf-8")
        animation_source = PROJECT / f"TheGame/Entitys/Animations/Heroes/{hero.title()}/{hero}_preview_library.tres"
        animation_path = destination / animation_source.relative_to(PROJECT)
        animation_path.parent.mkdir(parents=True, exist_ok=True)
        uid = re.search(r'uid="([^"]+)"',animation_source.read_text('utf-8'))[1]
        length = max(frames / 24, 1)
        result = f'[gd_resource type="AnimationLibrary" format=3 uid="{uid}"]\n\n[sub_resource type="Animation" id="idle"]\nresource_name = "idle"\nlength = {length}\nloop_mode = 1\n'
        for i, layer in enumerate(["Body", "Weapon"]):
            result += track(i,layer+":frame",[n/24 for n in range(frames)],
                            [str(n) for n in mapping["body" if i == 0 else "weapon"]])
        result += track(2,"Aura:frame",[n/15 for n in range(int(length*15)+1)],[str(3+n%6) for n in range(int(length*15)+1)])
        result += track(3,"Body:position",[0],["Vector2(0, 0)"])+track(4,"Weapon:position",[0],["Vector2(0, 0)"])
        result += '[resource]\n_data = {&"idle": SubResource("idle")}\n'
        animation_path.write_text(result,encoding="utf-8")


if __name__ == "__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ["swf","xml","work","java","ffdec","godot"]:
        parser.add_argument("--"+name,type=Path,required=name == "work")
    parser.add_argument("--animated-idle", action="store_true",
                        help="Repack current original-density Role idle cells into the existing preview paths")
    parser.add_argument("--project", type=Path, default=PROJECT)
    parser.add_argument("--output-project",type=Path,
                        help="Redirect authored preview resources; read identities from the current project")
    parser.add_argument("--density",type=int,choices=[1,2],default=1)
    parser.add_argument("--manifests",type=Path,nargs="+")
    args = parser.parse_args()
    if args.animated_idle:
        export_animated_idle(args)
    else:
        if any(getattr(args, name) is None for name in ["swf", "xml", "java", "ffdec", "godot", "manifests"]):
            parser.error("The backpack_v2 source export requires --swf --xml --java --ffdec --godot --manifests")
        export(args)
