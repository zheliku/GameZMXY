"""Read-only checks for serialized resource paths, script UIDs and optional staged geometry.

Run from any directory:
    python Tools/ProjectMaintenance/validate_game_references.py
    python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry
"""

import argparse
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "Godot/GodotProject"
GAME = PROJECT / "TheGame"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--compare-staged-geometry", action="store_true")
    args = parser.parse_args()
    failures = []
    path_count = 0
    for source in GAME.rglob("*"):
        if source.suffix not in {".tscn", ".tres"}:
            continue
        for uri in re.findall(r'\bpath="(res://[^"\n]+)"', source.read_text(encoding="utf-8-sig")):
            path_count += 1
            if not (PROJECT / uri[6:]).is_file():
                failures.append(f"{source.relative_to(ROOT)}: missing {uri}")

    uids = {}
    for source in GAME.rglob("*.uid"):
        uid = source.read_text(encoding="utf-8").strip()
        if uid in uids:
            failures.append(f"Duplicate UID {uid}: {source} / {uids[uid]}")
        uids[uid] = source
        if not Path(str(source)[:-4]).is_file():
            failures.append(f"Orphan UID: {source.relative_to(ROOT)}")

    if args.compare_staged_geometry:
        # Opt-in only: intentional terrain work should not fail normal reference checks.
        scene = "Godot/GodotProject/TheGame/Scenes/Level_1.tscn"
        staged = subprocess.check_output(["git", "show", ":" + scene], cwd=ROOT, text=True, encoding="utf-8")
        current = (ROOT / scene).read_text(encoding="utf-8")
        pattern = r'(?ms)^\[node[^\n]*parent="World/Geometry[^\n]*\n.*?(?=^\[|\Z)'
        staged_nodes = [node.strip() for node in re.findall(pattern, staged)]
        current_nodes = [node.strip() for node in re.findall(pattern, current)]
        if staged_nodes != current_nodes:
            failures.append("Level_1 geometry differs from the current Git index")
        print(f"STAGED GEOMETRY: {len(staged_nodes)} nodes compared")

    for failure in failures:
        print("REFERENCE FAIL: " + failure)
    if failures:
        raise SystemExit(1)
    print(f"REFERENCE PASS: {path_count} serialized paths and {len(uids)} UIDs")


if __name__ == "__main__":
    main()
