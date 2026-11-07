"""Export the actual backpack_v2 Wukong/Tangseng preview timelines and equipment.

Uses existing selector freezing, depth-aware body/weapon splitting and Godot
rasterization. Combat atlases and animation libraries are never rewritten.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as E

import export_official_wukong as source
from import_official_wukong_attacks import track
from import_official_tangseng_art import compact_atlases

SOURCE_SHA = "47215d42043801243e7beae00ca4601d6040b343be8bd8a29a9c4aa4e8df17ea"
PROJECT = Path(__file__).resolve().parents[2] / "Godot/GodotProject"


def export(args):
    """Install both official preview libraries and independently replaceable wardrobe atlases."""
    if hashlib.sha256(args.swf.read_bytes()).hexdigest() != SOURCE_SHA:
        raise ValueError("Unexpected backpack_v2 revision")
    args.work.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, APPDATA=str((args.work / "profile").resolve()))
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
            out = PROJECT / f"TheGame/Sprites/Characters/Heroes/{hero}/preview/{hero}_preview_{stem.removeprefix(hero + '_')}.png"
            out.parent.mkdir(exist_ok=True)
            raster = {"kind":kind,"selector":selector,"columns":6,"rows":(frames+5)//6,
                      "poses":{str(i):[sid,i+1] for i in range(frames)},"inputs":{str(sid):str(layers.resolve())},"output":str((work / "atlas.png").resolve())}
            raster_path = work / "raster.json"
            raster_path.write_text(json.dumps(raster), encoding="utf-8")
            if not Path(raster["output"]).exists():
                with (work / "raster.log").open("w",encoding="utf-8") as log:
                    subprocess.run([str(args.godot),"--headless","--path",str(PROJECT),"--script","res://EditorScripts/import_official_wukong_equipment.gd","--",str(raster_path.resolve())],stdout=log,stderr=subprocess.STDOUT,check=True)
            out.write_bytes(Path(raster["output"]).read_bytes())
            variants[kind, id] = {"path": str(out), "sha256": hashlib.sha256(out.read_bytes()).hexdigest()}
            preview_resources.append(f'[ext_resource type="Texture2D" path="res://{out.relative_to(PROJECT).as_posix()}" id="{id}"]')
            print("INVENTORY PREVIEW",hero,kind,selector,frames,"frames",flush=True)
        packed, mapping, counts = compact_atlases(variants, frame_count=frames)
        for key, item in variants.items():
            Path(item["path"]).write_bytes(packed[key])
        preview_path = appearance_path.with_name(hero + "_inventory_appearance.tres")
        body = appearance.split("[resource]",1)[1]
        body = re.sub(r'_previewAnimations = [^\n]+\n',"",body)
        body = re.sub(r'_previewAppearance = [^\n]+\n',"",body)
        script = next(line for line in appearance.splitlines() if 'type="Script"' in line)
        preview_path.write_text('[gd_resource type="Resource" script_class="HeroAppearanceResource" format=3]\n\n'+script+'\n'+'\n'.join(preview_resources)+'\n\n[resource]'+body,encoding="utf-8")
        if 'id="inventory_appearance"' not in appearance:
            appearance = appearance.replace('[resource]',f'[ext_resource type="Resource" path="res://{preview_path.relative_to(PROJECT).as_posix()}" id="inventory_appearance"]\n\n[resource]')
            appearance = appearance.replace('script = ExtResource("script")',
                'script = ExtResource("script")\nm_PreviewAppearance = ExtResource("inventory_appearance")')
            appearance_path.write_text(appearance,encoding="utf-8")
        animation_path = PROJECT / f"TheGame/Entitys/Animations/Heroes/{hero.title()}/{hero}_preview_library.tres"
        uid = re.search(r'uid="([^"]+)"',animation_path.read_text('utf-8'))[1]
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
        parser.add_argument("--"+name,type=Path,required=True)
    parser.add_argument("--manifests",type=Path,nargs="+",required=True)
    export(parser.parse_args())
