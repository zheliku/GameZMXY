"""Audit native body endpoints and motion stops from the matched Flash frame scripts.

Consumes source_inventory.json and FFDec script folders. The output is authoring
evidence only; gameplay reads the resulting Godot animations, never this JSON.
"""

import argparse
import json
from pathlib import Path
import re


def script_index(folder):
    result = {}
    reachable = set()
    for path in folder.rglob("*.as"):
        source = path.read_text(encoding="utf-8-sig")
        reachable.update(re.findall(r'curAction\s*=\s*"([^"]+)"', source))
        symbol = re.search(r'symbol="symbol(\d+)"', source)
        if symbol:
            result[int(symbol[1])] = (path, source)
    return result, reachable


def frame_events(source):
    declaration = re.search(r"addFrameScript\((.*?)\);", source, re.S)
    if not declaration:
        return []
    events = []
    for offset, name in re.findall(r"(\d+)\s*,\s*this\.(\w+)", declaration[1]):
        method = re.search(r"function\s+" + re.escape(name) + r"\s*\([^)]*\)[^{]*\{", source)
        if not method:
            raise ValueError(f"Missing frame method: {name}")
        start = method.end()
        end, depth = start, 1
        while depth:
            if source[end] == "{":
                depth += 1
            elif source[end] == "}":
                depth -= 1
            end += 1
        events.append((int(offset), source[start:end - 1].strip()))
    return events


def animation_name(label, hero):
    if label.startswith("hit"):
        number = int(label[3:])
        return f"attack_{number}" if not hero or number <= 5 else None
    return {"wait": "idle1" if hero else "idle", "walk": "walk" if hero else "run",
            "dead": "death", "jump1": "jump", "jump2": "jump_2", "jump3": "fall"}.get(label, label)


def export(args):
    inventory = json.loads(args.inventory.read_text(encoding="utf-8-sig"))
    groups = {name: script_index(getattr(args, name + "_scripts"))
              for name in ("role", "monster1", "monster2")}
    records = []
    for scene, actor in inventory.items():
        hero = scene == "wukong_entity"
        group = "role" if hero else "monster2" if actor["symbol"] in (
            "export.monster.Monster11", "export.monster.Monster13", "export.monster.Monster21") else "monster1"
        scripts, reachable = groups[group]
        for action in actor["actions"]:
            label = action["label"]
            # A timeline label is not proof that the game ever enters that action.
            if label == "afterHurt":
                if label in reachable:
                    raise ValueError("afterHurt became reachable; review its actual callers first")
                continue
            animation = animation_name(label, hero)
            script = scripts.get(action["sprite"])
            if not script or animation is None:
                continue
            path, source = script
            events = frame_events(source)
            entry = {"scene": scene, "animation": animation, "sprite": action["sprite"],
                     "frames": action["frames"], "script": str(path.resolve()),
                     "events": [{"frame": frame + 1, "body": body} for frame, body in events]}
            if events:
                last, body = events[-1]
                if animation.startswith("attack_") or animation in ("hurt", "death"):
                    terminals = [offset for offset, code in events
                                 if re.search(r'curAction\s*=\s*"(?:wait|walk|jump[123])"', code)
                                 or '.destroy()' in code or 'BossDead' in code]
                    if terminals:
                        entry["complete"] = min(terminals) / 24.0
                if 'standInObj' in body:
                    loop = re.search(r"gotoAndPlay\((\d+)\)", body)
                    if loop:
                        entry["air_loop"] = (int(loop[1]) - 1) / 24.0
            if animation in ("hurt", "death"):
                entry["motion_stops"] = [frame / 24.0 for frame, body in events if '.setStatic()' in body]
            records.append(entry)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps({"fps": 24, "actions": records}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"BODY EVENT SOURCE AUDIT: {len(records)} scripted actions, "
          f"{sum('complete' in row for row in records)} terminal frame markers, "
          f"{sum(len(row.get('motion_stops', [])) for row in records)} motion stops")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", type=Path, required=True)
    for group in ("role", "monster1", "monster2"):
        parser.add_argument(f"--{group}-scripts", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    export(parser.parse_args())
